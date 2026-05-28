using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    // Drives the native function-calling conversation for one AI turn:
    //   1. Ask Gemini (with tools) given the running `contents`.
    //   2. If it returns functionCalls: echo them back into contents, execute each via
    //      LlmTool.Dispatch (collecting action intents + building functionResponses),
    //      append the responses, and loop so the model can react / make its final reply.
    //   3. If it returns plain text: that's the spoken reply — finish.
    //
    // The collected ParsedIntents are handed to the caller, which enqueues them as
    // AiIntentCommands (the deterministic, networked path). All LLM/tool work here is
    // non-deterministic and local to the owner client; nothing mutates sim state.
    public static class LlmToolLoop
    {
        private const int MaxIterations = 4; // round-trips per turn: e.g. read → act → reply

        // contents must already contain the conversation history plus the new user/event
        // message. The loop appends model + functionResponse turns to it as it runs.
        public static IEnumerator Run(
            string apiKey, string systemPrompt, List<GeminiClient.Content> contents,
            GameSimulation sim, int aiPlayerId, int humanPlayerId,
            Action<List<LlmIntentSchema.ParsedIntent>, string> onComplete,
            Action<string> onFailure)
        {
            string toolsJson = LlmTool.BuildToolsJson();
            var intents = new List<LlmIntentSchema.ParsedIntent>();
            string fallbackText = string.Empty;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                GeminiClient.Response resp = null;
                string err = null;
                yield return GeminiClient.Generate(apiKey, systemPrompt, contents, toolsJson,
                    r => resp = r, e => err = e);

                if (err != null) { onFailure?.Invoke(err); yield break; }
                if (resp == null) { onFailure?.Invoke("null-response"); yield break; }

                if (!string.IsNullOrEmpty(resp.Text)) fallbackText = resp.Text;

                if (!resp.HasCalls)
                {
                    // Plain text turn → final spoken reply.
                    onComplete?.Invoke(intents, resp.Text ?? string.Empty);
                    yield break;
                }

                // Echo the model's tool calls back into the transcript (Gemini requires the
                // call to appear before its matching functionResponse).
                var modelTurn = new GeminiClient.Content("model");
                for (int i = 0; i < resp.Calls.Count; i++)
                {
                    modelTurn.Parts.Add(new GeminiClient.Part
                    {
                        Call = new GeminiClient.FunctionCall
                        {
                            Name = resp.Calls[i].Name,
                            Args = resp.Calls[i].Args,
                            ThoughtSignature = resp.Calls[i].ThoughtSignature,
                        }
                    });
                }
                contents.Add(modelTurn);

                // Execute each call, collecting intents and building the response turn.
                var responseTurn = new GeminiClient.Content("user");
                for (int i = 0; i < resp.Calls.Count; i++)
                {
                    var call = resp.Calls[i];
                    var result = LlmTool.Dispatch(call.Name, call.Args, sim, aiPlayerId, humanPlayerId);

                    string resultText = result.ResultText ?? string.Empty;
                    if (result.HasIntent)
                    {
                        if (intents.Count < LlmIntentSchema.MaxIntentsPerTurn)
                            intents.Add(result.Intent);
                        else
                            resultText = "Action limit reached for this turn; not queued.";
                    }

                    responseTurn.Parts.Add(new GeminiClient.Part
                    {
                        Response = new GeminiClient.FunctionResponse { Name = call.Name, ResultText = resultText }
                    });
                }
                contents.Add(responseTurn);
            }

            // Hit the iteration cap while still calling tools. Do one final pass WITHOUT
            // tools to force a closing spoken reply grounded in what already ran.
            {
                GeminiClient.Response resp = null;
                string err = null;
                yield return GeminiClient.Generate(apiKey, systemPrompt, contents, null,
                    r => resp = r, e => err = e);
                if (err == null && resp != null && !string.IsNullOrEmpty(resp.Text))
                    fallbackText = resp.Text;
            }
            onComplete?.Invoke(intents, fallbackText);
        }

        // Builds the initial contents list from stored history plus the new message.
        public static List<GeminiClient.Content> BuildInitialContents(
            List<GeminiClient.Turn> history, string userMessage)
        {
            var contents = new List<GeminiClient.Content>();
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    contents.Add(history[i].IsUser
                        ? GeminiClient.Content.UserText(history[i].Text)
                        : GeminiClient.Content.ModelText(history[i].Text));
                }
            }
            contents.Add(GeminiClient.Content.UserText(userMessage));
            return contents;
        }
    }
}
