#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTools
{
    public sealed class CommanderIntentDebugWindow : EditorWindow
    {
        [SerializeField] private string commandText = "make 10 spearmen";
        [SerializeField] private string lastResponse = "Enter a local Commander command.";
        [SerializeField] private bool malformedMockResponse;

        public static void Open()
        {
            CommanderIntentDebugWindow window = GetWindow<CommanderIntentDebugWindow>();
            window.titleContent = new GUIContent("Commander Command");
            window.minSize = new Vector2(420f, 150f);
            window.Show();
        }

        private void OnEnable()
        {
            CommanderIntentDebugSession.ResponseReceived += HandleResponse;
            CommanderIntentDebugSession.StateReceived += HandleState;
        }

        private void OnDisable()
        {
            CommanderIntentDebugSession.ResponseReceived -= HandleResponse;
            CommanderIntentDebugSession.StateReceived -= HandleState;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Commander Command", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Local debug input only. Text is interpreted locally and is never sent over multiplayer.",
                MessageType.Info);

            GUI.SetNextControlName("CommanderCommandInput");
            commandText = EditorGUILayout.TextField(commandText);

            malformedMockResponse = EditorGUILayout.Toggle("Mock malformed JSON", malformedMockResponse);
            EditorGUILayout.LabelField("State", CommanderIntentDebugSession.State.ToString());
            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || CommanderIntentDebugSession.IsInterpreting);
            if (GUILayout.Button("Submit to mock interpreter", GUILayout.Height(28f))) ExecuteCurrentCommand();
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!CommanderIntentDebugSession.IsInterpreting);
            if (GUILayout.Button("Cancel interpretation")) CommanderIntentDebugSession.Cancel();
            EditorGUI.EndDisabledGroup();

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode before executing a command.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commander Response", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(lastResponse, EditorStyles.wordWrappedLabel,
                GUILayout.MinHeight(36f));
        }

        private async void ExecuteCurrentCommand()
        {
            try
            {
                var submission = await CommanderIntentDebugSession.ExecuteAsync(commandText, malformedMockResponse);
                if (this == null) return;
                lastResponse = submission.Response;
            }
            catch (Exception error)
            {
                if (this == null) return;
                lastResponse = error.Message;
            }
            Repaint();
        }

        private void HandleState(CommanderSubmissionState state)
        {
            if (state == CommanderSubmissionState.WaitingForInterpretation) lastResponse = "Commander: Thinking...";
            Repaint();
        }

        private void HandleResponse(string response)
        {
            lastResponse = response;
            Repaint();
        }
    }

    internal static class CommanderIntentDebugSession
    {
        private static CommanderGoalManager manager;
        private static CommanderIntentDispatcher dispatcher;
        private static bool malformed;
        public static CommanderSubmissionState State => dispatcher?.State ?? CommanderSubmissionState.Idle;
        public static bool IsInterpreting => dispatcher != null && dispatcher.IsInterpreting;
        public static event Action<CommanderSubmissionState> StateReceived;

        public static event Action<string> ResponseReceived;

        public static void Cancel() => dispatcher?.CancelPendingSubmission();

        public static Task<CommanderIntentSubmission> ExecuteAsync(string text, bool malformedResponse)
        {
            var bootstrapper = GameBootstrapper.Instance;
            if (!EditorApplication.isPlaying || bootstrapper == null || bootstrapper.Simulation == null || bootstrapper.Commander == null)
                throw new InvalidOperationException("Enter Play Mode and wait for Commander initialization.");
            EnsureDispatcher(bootstrapper.Simulation, bootstrapper.Commander, malformedResponse);
            return dispatcher.SubmitTextAsync(text);
        }

        static CommanderIntentDebugSession()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static CommanderIntentSubmission Execute(string text, out string error)
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            if (!EditorApplication.isPlaying || bootstrapper == null
                || bootstrapper.Simulation == null || bootstrapper.Commander == null)
            {
                error = "Enter Play Mode and wait for the simulation and Commander to initialize.";
                return null;
            }

            EnsureDispatcher(bootstrapper.Simulation, bootstrapper.Commander);
            error = string.Empty;
            var interpreted = new SimpleTextIntentParser().Interpret(text, manager.PlayerId);
            if (!interpreted.Success)
                return new CommanderIntentSubmission(interpreted, null,
                    new CommanderResponseGenerator().GenerateInterpretationRejection(interpreted));
            return dispatcher.SubmitIntent(interpreted.Intent);
        }

        private static void EnsureDispatcher(GameSimulation simulation,
            CommanderGoalManager currentManager, bool malformedResponse = false)
        {
            if (dispatcher != null && ReferenceEquals(manager, currentManager) && malformed == malformedResponse) return;

            if (dispatcher != null)
            {
                dispatcher.ResponseGenerated -= HandleResponse;
                dispatcher.StateChanged -= HandleState;
                dispatcher.Dispose();
            }

            manager = currentManager;
            malformed = malformedResponse;
            dispatcher = new CommanderIntentDispatcher(simulation, currentManager,
                new MockLlmIntentInterpreter(750, malformedResponse ? "{broken JSON" : null));
            dispatcher.ResponseGenerated += HandleResponse;
            dispatcher.StateChanged += HandleState;
        }

        private static void HandleState(CommanderSubmissionState state) => StateReceived?.Invoke(state);

        private static void HandleResponse(string response)
        {
            string singleLine = response.Replace('\n', ' ');
            Debug.Log("[Commander Response] " + singleLine);
            ResponseReceived?.Invoke(response);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode
                && state != PlayModeStateChange.EnteredEditMode) return;

            if (dispatcher != null)
            {
                dispatcher.ResponseGenerated -= HandleResponse;
                dispatcher.StateChanged -= HandleState;
                dispatcher.Dispose();
            }
            dispatcher = null;
            manager = null;
        }
    }
}
#endif
