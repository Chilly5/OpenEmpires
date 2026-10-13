using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5COffline")]
    public sealed class CommanderPhase5CRestartPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayAgainTwice_RecreatesOneMinimizedLauncherWithNewRuntime()
        {
            CommanderChatUI previousChat = null;
            GameSimulation previousSimulation = null;

            for (int restart = 0; restart < 2; restart++)
            {
                var load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
                while (!load.isDone) yield return null;

                var bootstrap = GameBootstrapper.Instance;
                Assert.That(bootstrap, Is.Not.Null, "Loaded match must have its bootstrapper.");
                var network = bootstrap.Network;
                Assert.That(network, Is.Not.Null, "SampleScene must have its match-start controller.");
                typeof(NetworkManager).GetField("<GameStarted>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(network, true);

                for (int frame = 0; frame < 120 && (bootstrap.Simulation == null
                    || CommanderChatUI.Instance == null || !CommanderChatUI.Instance.IsReady); frame++)
                    yield return null;

                var chat = CommanderChatUI.Instance;
                Assert.That(chat, Is.Not.Null, "Play Again must restore Commander after scene load.");
                Assert.That(chat.IsReady, Is.True, "The launcher must bind to the new match runtime.");
                Assert.That(chat.IsExpanded, Is.False);
                Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/Launcher"), Is.Not.Null);
                Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy,
                    Is.True, "The minimized launcher must be visible.");
                Assert.That(Object.FindObjectsByType<CommanderChatUI>().Length, Is.EqualTo(1));
                Assert.That(bootstrap.Simulation, Is.Not.SameAs(previousSimulation));
                Assert.That(chat, Is.Not.SameAs(previousChat));
                Assert.That(chat.DisplayedTranscript, Is.Empty, "Old match conversation must not survive restart.");

                chat.OpenCommander();
                Assert.That(chat.IsExpanded, Is.True, "The new launcher must accept input.");
                chat.InputText = "new match input";
                Assert.That(chat.InputText, Is.EqualTo("new match input"));
                chat.MinimizeCommander();
                Assert.That(chat.IsExpanded, Is.False);

                previousChat = chat;
                previousSimulation = bootstrap.Simulation;
            }
        }
    }
}
