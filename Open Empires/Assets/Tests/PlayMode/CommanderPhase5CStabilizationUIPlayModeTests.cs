using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CStabilizationUIPlayModeTests
    {
        [Serializable] private sealed class UiReceipt
        {
            public string schema="commander-stabilization-ui@1", input="synthetic Input System mouse; actual terrain callback and existing UI buttons",stage="starting";
            public bool recognizedPress;
            public int goalsAfterMarker,providerCallsAfterMarker,providerCalls,newMinimizedUiCount,nativeTickBeforeRestart;
            public long requestId;
        }
        private sealed class Provider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            internal int Calls;
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)
                =>Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"fixture"));
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
            {
                Calls++;
                return Task.FromResult(CommanderSemanticJson.ParseProviderResponse("{\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"quantityMode\":\"New\"},{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"PlayerMarker\",\"dependsOn\":[0],\"resultFromNode\":0}]}"));
            }
        }

        [UnityTest]
        public IEnumerator ActualSceneMarkerInputAndPlayAgain_ClearOldDraftsWithoutApprovalBypass()
        {
            var previousMouse=Mouse.current;
            var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
            var previousLock=Cursor.lockState;Cursor.lockState=CursorLockMode.None;
            bool previousVoice=CommanderVoiceSettings.VoiceEnabled;CommanderVoiceSettings.VoiceEnabled=false;
            var record=new UiReceipt();
            string output=Path.GetFullPath("Docs/CommanderPhase5C/evidence/stabilization-ui");Directory.CreateDirectory(output);
            void Save()=>File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(record,true)+"\n");
            try
            {
                var load=SceneManager.LoadSceneAsync("SampleScene",LoadSceneMode.Single);
                float deadline=Time.realtimeSinceStartup+15;
                while(!load.isDone&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(load.isDone,Is.True,"Controlled scene setup timed out.");
                var bootstrap=GameBootstrapper.Instance;
                Assert.That(bootstrap,Is.Not.Null);
                typeof(NetworkManager).GetField("<GameStarted>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bootstrap.Network,true);
                deadline=Time.realtimeSinceStartup+15;
                while((bootstrap.Simulation==null||CommanderChatUI.Instance?.IsReady!=true)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(CommanderChatUI.Instance?.IsReady,Is.True);
                var chat=CommanderChatUI.Instance;var sim=bootstrap.Simulation;var manager=bootstrap.Commander;
                var provider=new Provider();chat.Initialize(provider,sim,manager,bootstrap.CommanderDispatcher);
                Assert.That(chat.IsExpanded,Is.False);Assert.That(UnityEngine.Object.FindObjectsByType<CommanderChatUI>().Length,Is.EqualTo(1));
                SettingsMenuUI.Close();UnitSelectionManager.SetChatFocused(false);
                var camera=Camera.main;Assert.That(camera,Is.Not.Null);
                Physics.SyncTransforms();
                Vector2 screen=Vector2.zero;RaycastHit hit=default;bool found=false;
                foreach(var viewport in new[]{new Vector2(.65f,.5f),new Vector2(.7f,.4f),new Vector2(.55f,.4f)})
                    if(Physics.Raycast(camera.ViewportPointToRay(viewport),out hit,1000,LayerMask.GetMask("Ground")))
                    {screen=new Vector2(viewport.x*Screen.width,viewport.y*Screen.height);found=true;break;}
                Assert.That(found,Is.True,"Actual scene ground raycast must resolve.");
                var tile=sim.MapData.WorldToTile(FixedVector3.FromVector3(hit.point));
                sim.FogOfWar.SetVisible(0,tile.x,tile.y); // Controlled visible starting condition only.
                int before=manager.Goals.Count;
                var set=chat.transform.Find("CommanderCanvas/CompactCommander/Destination/SetDestination")?.GetComponent<Button>()
                    ??chat.GetComponentsInChildren<Button>(true).Single(b=>b.name=="SetDestination"&&b.transform.IsChildOf(chat.transform.Find("CommanderCanvas/CompactCommander")));
                set.onClick.Invoke();yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});InputSystem.Update();yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});InputSystem.Update();
                record.recognizedPress=mouse.leftButton.wasPressedThisFrame;Save();
                Assert.That(mouse.leftButton.wasPressedThisFrame,Is.True,"Synthetic press must be recognized before checking production polling.");
                Cursor.lockState=CursorLockMode.None; // This fixture exercises the native unlocked-pointer route.
                // The actual RTS camera can move while the synthetic pointer settles.
                // Freeze the click's CURRENT ground ray rather than an earlier viewport ray.
                Assert.That(Physics.Raycast(camera.ScreenPointToRay(screen),out hit,1000,LayerMask.GetMask("Ground")),Is.True);
                tile=sim.MapData.WorldToTile(FixedVector3.FromVector3(hit.point));
                sim.FogOfWar.SetVisible(0,tile.x,tile.y);
                // Invoke the exact production LateUpdate callback in this recognized input frame.
                typeof(CommanderChatUI).GetMethod("PollDestinationPlacement",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out int x,out int z),Is.True);
                Assert.That((x,z),Is.EqualTo((tile.x,tile.y)));
                Assert.That(manager.Goals.Count,Is.EqualTo(before));Assert.That(provider.Calls,Is.Zero);
                Assert.That(UnitSelectionManager.UIInputSuppressed,Is.True,"Press/release remains owned by marker placement.");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});InputSystem.Update();yield return null;yield return null;
                Assert.That(chat.transform.Find("Commander player destination"),Is.Not.Null);
                record.stage="marker-visible";record.goalsAfterMarker=manager.Goals.Count;record.providerCallsAfterMarker=provider.Calls;Save();
                yield return new WaitForEndOfFrame();
                var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output,"actual-scene-marker.png"),screenshot.EncodeToPNG());UnityEngine.Object.Destroy(screenshot);
                chat.ClearDestinationMarker();
                chat.OpenCommander();
                var request=chat.SubmitMessageAsync("Train three new Spearmen then send only them to my marker");
                while(!request.IsCompleted)yield return null;
                Assert.That(chat.DisplayedTranscript,Does.Contain("Where?"));Assert.That(provider.Calls,Is.EqualTo(1));
                set.onClick.Invoke();yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});InputSystem.Update();
                Cursor.lockState=CursorLockMode.None;
                typeof(CommanderChatUI).GetMethod("PollDestinationPlacement",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
                Assert.That(chat.PendingActionPlan,Is.Not.Null);Assert.That(manager.Goals.Count,Is.EqualTo(before));
                Assert.That(provider.Calls,Is.EqualTo(1));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});InputSystem.Update();yield return null;yield return null;
                chat.OpenCommander();yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
                screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output,"actual-scene-awaiting-approval.png"),screenshot.EncodeToPNG());UnityEngine.Object.Destroy(screenshot);
                record.stage="pending-plan-no-approval";record.requestId=chat.PendingActionPlan.RequestId;record.providerCalls=provider.Calls;Save();
                // End the short controlled match through an ordinary command, then invoke
                // the actual visible Play Again button callback instead of direct scene reload.
                sim.CommandBuffer.EnqueueCommand(new SurrenderVoteCommand(0,true));
                deadline=Time.realtimeSinceStartup+10;
                while(!sim.IsMatchOver&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(sim.IsMatchOver,Is.True,"Normal surrender command must end the controlled match.");yield return null;
                var results=UnityEngine.Object.FindFirstObjectByType<MatchResultUI>();Assert.That(results,Is.Not.Null);
                var again=results.transform.Find("MatchResultCanvas/Panel/QueueAgainButton").GetComponent<Button>();
                Assert.That(again.gameObject.activeInHierarchy,Is.True);
                again.onClick.Invoke();yield return null;
                bootstrap=GameBootstrapper.Instance;
                typeof(NetworkManager).GetField("<GameStarted>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bootstrap.Network,true);
                deadline=Time.realtimeSinceStartup+15;
                while((bootstrap.Simulation==null||CommanderChatUI.Instance?.IsReady!=true)&&Time.realtimeSinceStartup<deadline)yield return null;
                var next=CommanderChatUI.Instance;Assert.That(next?.IsReady,Is.True);
                Assert.That(next,Is.Not.SameAs(chat));Assert.That(bootstrap.Simulation,Is.Not.SameAs(sim));
                Assert.That(UnityEngine.Object.FindObjectsByType<CommanderChatUI>().Length,Is.EqualTo(1));
                Assert.That(next.IsExpanded,Is.False);Assert.That(next.PendingActionPlan,Is.Null);Assert.That(next.PendingClarification,Is.Null);
                Assert.That(next.DisplayedTranscript,Is.Empty);Assert.That(bootstrap.Commander.TryGetPlayerMarkerSnapshot(out _,out _),Is.False);
                Assert.That(chat.TryPlaceDestinationMarker(tile.x,tile.y),Is.False,"A callback on the destroyed old host cannot resume its old request.");
                Assert.That(bootstrap.Commander.Goals,Is.Empty);
                record.stage="play-again-cleared";record.newMinimizedUiCount=1;record.nativeTickBeforeRestart=sim.CurrentTick;Save();
            }
            finally
            {
                if(mouse!=null)InputSystem.RemoveDevice(mouse);previousMouse?.MakeCurrent();Cursor.lockState=previousLock;
                CommanderVoiceSettings.VoiceEnabled=previousVoice;
            }
        }
    }
}
