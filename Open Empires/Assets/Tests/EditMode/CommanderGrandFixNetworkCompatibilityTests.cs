using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixNetworkCompatibilityTests
    {
        private MatchmakingManager manager;
        [SetUp]public void Setup(){manager=new GameObject("CompatibilityFixture").AddComponent<MatchmakingManager>();}
        [TearDown]public void Cleanup(){if(manager!=null)UnityEngine.Object.DestroyImmediate(manager.gameObject);}
        private void Receive(ServerMessage message)=>typeof(MatchmakingManager).GetMethod("HandleServerMessage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(manager,new object[]{message});
        private static JObject Current()
        {
            var type=typeof(MatchmakingManager).Assembly.GetType("OpenEmpires.NetworkCompatibility");
            Assert.That(type,Is.Not.Null,"Source-matched compatibility identity is missing.");
            var profile=type.GetProperty("Current",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            Assert.That(profile,Is.Not.Null,"Generate current compatibility data before running this fixture.");
            return JObject.FromObject(profile);
        }
        private static ServerMessage Ack(JObject profile)
        {
            var data=new JObject{["player_id"]="11111111-1111-4111-8111-111111111111",["username"]="fixture",["compatibility"]=profile};
            return ServerMessageParser.Parse(new JObject{["type"]="Authenticated",["data"]=data}.ToString(Newtonsoft.Json.Formatting.None));
        }
        [Test]public void AuthenticatePacket_DeclaresSourceAndRestrictedCommandProfileWithoutUnsafeInterpolation()
        {
            var packet=JObject.Parse(new AuthenticateMessage("fixture-\"quoted").ToJson());
            Assert.That((string)packet["data"]["token"],Is.EqualTo("fixture-\"quoted"));
            Assert.That(packet["data"]["compatibility"],Is.Not.Null);
            Assert.That(packet["data"]["compatibility"],Is.EqualTo(Current()));
        }
        [Test]public void LegacyAuthenticationWithoutProfile_IsNotGameplayAdmission()
        {
            manager.OverrideState(MatchmakingState.Authenticating);
            Receive(new AuthenticatedMessage{player_id="fixture",username="fixture"});
            Assert.That(manager.State,Is.EqualTo(MatchmakingState.Disconnected));
            Assert.That(manager.IsInMatch,Is.False);
        }
        [TestCase("protocol_revision")][TestCase("source_sha256")][TestCase("command_encoding")][TestCase("recovery_policy")]
        public void EachIncompatibleField_RejectsBeforeMatch(string field)
        {
            manager.OverrideState(MatchmakingState.Authenticating);
            var profile=Current();profile[field]=field=="protocol_revision"?(JToken)999:(JToken)"unsupported";
            Receive(Ack(profile));Assert.That(manager.State,Is.EqualTo(MatchmakingState.Disconnected));
        }
        [Test]public void MatchingServerAcknowledgement_EnablesAuthenticationOnly()
        {
            manager.OverrideState(MatchmakingState.Authenticating);
            Receive(Ack(Current()));Assert.That(manager.State,Is.EqualTo(MatchmakingState.Authenticated));
            Assert.That(manager.IsInMatch,Is.False);
        }
        [Test]public void LateMatchingAcknowledgement_AfterDisconnectCannotReopenAdmission()
        {
            manager.Disconnect();Receive(Ack(Current()));
            Assert.That(manager.State,Is.EqualTo(MatchmakingState.Disconnected));
        }
        [Test]public void DuplicateAcknowledgement_CannotRewindInGameState()
        {
            manager.OverrideState(MatchmakingState.Authenticating);Receive(Ack(Current()));
            manager.StartGame();Receive(Ack(Current()));
            Assert.That(manager.State,Is.EqualTo(MatchmakingState.InGame));
        }
        [Test]public void Disconnect_ClearsQueuedMessagesBeforeNextConnection()
        {
            var socket=new GameObject("SocketEpochFixture").AddComponent<WebSocketClient>();
            try
            {
                var queue=(Queue<WebSocketClient.TimestampedMessage>)typeof(WebSocketClient).GetField("messageQueue",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(socket);
                queue.Enqueue(new WebSocketClient.TimestampedMessage{Message=Ack(Current()),ReceivedAtTicks=1});
                socket.Disconnect();Assert.That(queue,Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(socket.gameObject);}
        }
        [Test]public void BrowserRuntimeSourceChanges_ChangeSourceIdentity()
        {
            string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"oe-compat-"+Guid.NewGuid().ToString("N"));
            try
            {
                foreach(string dir in new[]{"Assets/Plugins/WebGL","Packages","ProjectSettings"})System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root,dir));
                foreach(string path in new[]{"Packages/manifest.json","Packages/packages-lock.json","ProjectSettings/ProjectVersion.txt"})System.IO.File.WriteAllText(System.IO.Path.Combine(root,path),"fixture");
                string script=System.IO.Path.Combine(root,"Assets/Plugins/WebGL/WebSocket.jslib");System.IO.File.WriteAllText(script,"fixture first");
                var method=typeof(MatchmakingManager).Assembly.GetType("OpenEmpires.NetworkCompatibility").GetMethod("ComputeSourceIdentity");
                var first=method.Invoke(null,new object[]{root});System.IO.File.WriteAllText(script,"fixture changed");
                Assert.That(method.Invoke(null,new object[]{root}),Is.Not.EqualTo(first));
            }
            finally{if(System.IO.Path.GetFileName(root).StartsWith("oe-compat-",StringComparison.Ordinal))System.IO.Directory.Delete(root,true);}
        }
        [Test]public void UnacknowledgedMatchAndCommands_AreNotPublishedToSimulation()
        {
            int events=0;manager.OnMatchFound+=_=>events++;manager.OnMatchStarting+=()=>events++;manager.OnGameCommandReceived+=_=>events++;
            Receive(new MatchFoundMessage());Receive(new MatchStartingMessage());Receive(new ServerGameCommandMessage());
            Assert.That(events,Is.Zero);Assert.That(manager.IsInMatch,Is.False);
        }
        [Test]public void MalformedServerPayload_IsNotEchoedToDiagnostics()
        {
            const string body="fixture-credential-that-is-not-json";var logs=new List<string>();
            Application.LogCallback capture=(message,stack,type)=>logs.Add(message);
            bool old=LogAssert.ignoreFailingMessages;LogAssert.ignoreFailingMessages=true;Application.logMessageReceived+=capture;
            try{Assert.That(ServerMessageParser.Parse(body),Is.Null);Assert.That(string.Join("\n",logs),Does.Not.Contain(body));}
            finally{Application.logMessageReceived-=capture;LogAssert.ignoreFailingMessages=old;}
        }
    }
}
