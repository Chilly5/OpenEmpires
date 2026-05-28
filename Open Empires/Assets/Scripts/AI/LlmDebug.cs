using UnityEngine;

namespace OpenEmpires
{
    // Central switch for AI-teammate debug logging. Flipped from the LlmTeammateController
    // inspector (see its verboseLogging fields). Verbose logs the decision trace
    // (reasoning, tool calls, results, reply); VerboseHttp additionally dumps raw Gemini
    // request/response bodies, which is very noisy.
    public static class LlmDebug
    {
        public static bool Verbose = true;
        public static bool VerboseHttp = false;

        public static void Log(string message)
        {
            if (Verbose) Debug.Log("[LlmAI] " + message);
        }

        // Command-level trace: when the teammate's decisions become game commands.
        public static void Cmd(string message)
        {
            if (Verbose) Debug.Log("[LlmAI cmd] " + message);
        }

        public static void Http(string message)
        {
            if (VerboseHttp) Debug.Log("[LlmAI/http] " + message);
        }
    }
}
