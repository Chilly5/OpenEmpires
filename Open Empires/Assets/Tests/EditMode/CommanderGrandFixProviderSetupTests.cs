using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixProviderSetupTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat, previous;
        private bool voiceEnabled;
        private static readonly FieldInfo Instance=typeof(CommanderChatUI).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        private const string FixtureKey="fixture-session-credential-not-real";
        [SetUp] public void Setup()
        {
            previous=Instance.GetValue(null) as CommanderChatUI;Instance.SetValue(null,null);
            voiceEnabled=CommanderVoiceSettings.VoiceEnabled;CommanderVoiceSettings.VoiceEnabled=false;
            config=ScriptableObject.CreateInstance<SimulationConfig>();sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
            manager=new CommanderGoalManager(sim,0);dispatcher=new CommanderIntentDispatcher(sim,manager);
            chat=new GameObject("ProviderSetupFixture").AddComponent<CommanderChatUI>();
            chat.Initialize(new MockAIProvider(),sim,manager,dispatcher);Instance.SetValue(null,chat);
        }
        [TearDown] public void Cleanup()
        {
            if(chat!=null)UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();manager?.Dispose();UnityEngine.Object.DestroyImmediate(config);
            Instance.SetValue(null,previous);CommanderVoiceSettings.VoiceEnabled=voiceEnabled;
        }
        private object Call(string name,params object[] args)
        {
            var method=typeof(CommanderChatUI).GetMethod(name,BindingFlags.Public|BindingFlags.Instance);
            Assert.That(method,Is.Not.Null,"Missing semantic setup action: "+name);
            return method.Invoke(chat,args);
        }
        private string Status()
        {
            var property=typeof(CommanderChatUI).GetProperty("SemanticSetupStatus");
            Assert.That(property,Is.Not.Null,"Missing safe setup status.");return (string)property.GetValue(chat);
        }
        private object SecretField()
        {
            var field=typeof(CommanderChatUI).GetField("semanticKeyField",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null);return field.GetValue(chat);
        }
        private void SetSecret(string value)=>SecretField().GetType().GetProperty("text").SetValue(SecretField(),value);
        private string ReadSecret()=>(string)SecretField().GetType().GetProperty("text").GetValue(SecretField());
        [Test] public void MaskedSessionKeyApply_PerformsNoValidationRequestAndClearsEntry()
        {
            var transport=new HttpFixture();Call("ShowSemanticSetup");var field=SecretField();SetSecret(FixtureKey);
            Assert.That(field.GetType().GetProperty("contentType").GetValue(field).ToString(),Is.EqualTo("Password"));
            Assert.That(Call("ConfigureSessionOpenRouter",FixtureKey,transport),Is.EqualTo(true));
            Assert.That(transport.Calls,Is.Zero);Assert.That(ReadSecret(),Is.Empty);
            Assert.That(Status(),Does.Contain("first request"));Assert.That(Status(),Does.Not.Contain(FixtureKey));
            Assert.That(PlayerPrefs.HasKey("commander_semantic_key"),Is.False);
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
            Assert.That(chat.IsExpanded,Is.False);
        }
        [TestCase("")][TestCase("invalid\nheader")][TestCase("contains space in credential")]
        public void InvalidKey_DoesNotReplaceProviderOrMakeRequest(string invalid)
        {
            var before=typeof(CommanderChatUI).GetField("semanticProvider",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            var transport=new HttpFixture();Assert.That(Call("ConfigureSessionOpenRouter",invalid,transport),Is.EqualTo(false));
            Assert.That(transport.Calls,Is.Zero);
            Assert.That(typeof(CommanderChatUI).GetField("semanticProvider",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat),Is.SameAs(before));
            Assert.That(Status(),Does.Contain("key"));
        }
        [TestCase(401,"key")][TestCase(402,"credits")][TestCase(429,"wait")]
        public async Task ActualRequestFailures_ShowActionableSafeSetupStatus(int status,string action)
        {
            var transport=new HttpFixture{Status=status};Call("ConfigureSessionOpenRouter",FixtureKey,transport);
            await chat.SubmitMessageAsync("build a barracks");
            Assert.That(transport.Calls,Is.EqualTo(1));Assert.That(Status().ToLowerInvariant(),Does.Contain(action));
            Assert.That(Status(),Does.Not.Contain(FixtureKey));Assert.That(manager.Goals,Is.Empty);
        }
        [Test] public async Task NetworkFailure_ShowsConnectionActionWithoutEchoingException()
        {
            var transport=new HttpFixture{NetworkFailure=true};Call("ConfigureSessionOpenRouter",FixtureKey,transport);
            await chat.SubmitMessageAsync("build a barracks");
            Assert.That(Status().ToLowerInvariant(),Does.Contain("connection"));Assert.That(Status(),Does.Not.Contain(FixtureKey));
            Assert.That(manager.Goals,Is.Empty);
        }
        [Test] public async Task DisableDuringIgnoringResponse_RejectsLateEffectAndPreservesAcceptedGoal()
        {
            var provider=new DeferredProvider();chat.Initialize(provider,sim,manager,dispatcher);
            var accepted=manager.SubmitEnsureUnitCount(1,4);var pending=chat.SubmitMessageAsync("build a barracks");
            Call("DisableSemanticTranslation");
            provider.Pending.SetResult(CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}"));
            await pending;
            Assert.That(manager.Goals.Count,Is.EqualTo(1));Assert.That(accepted.IsTerminal,Is.False);
            Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);Assert.That(Status(),Does.Contain("disabled"));
            await chat.SubmitMessageAsync("build a barracks");Assert.That(manager.Goals.Count,Is.EqualTo(1));
        }
        [Test] public async Task GatewayTextSetup_DoesNotFetchAudioPolicyOrGrantRecordingConsent()
        {
            var transport=new GatewayFixture();
            Assert.That(Call("ConfigureSemanticGateway","https://gateway.example.invalid",(Func<string>)(()=>"11111111-1111-4111-8111-111111111111"),transport),Is.EqualTo(true));
            Assert.That(transport.Calls,Is.Zero);Assert.That(CommanderVoiceSettings.VoiceEnabled,Is.False);
            await chat.SubmitMessageAsync("build a barracks");
            Assert.That(transport.Calls,Is.EqualTo(1));Assert.That(transport.Kind,Is.EqualTo(CommanderGatewayRequestKind.Semantic));
            Assert.That(manager.Goals,Is.Empty);Assert.That(Status(),Does.Contain("gateway"));
        }
        [Test] public void HideSetup_ClearsUnappliedSecretAndEscapeDoesNotTouchPlans()
        {
            var accepted=manager.SubmitEnsureUnitCount(1,4);Call("ShowSemanticSetup");SetSecret(FixtureKey);
            Assert.That(CommanderChatUI.TryHandleEscapeInput(),Is.True);Assert.That(ReadSecret(),Is.Empty);
            Assert.That(accepted.IsTerminal,Is.False);Assert.That(chat.IsExpanded,Is.False);
        }
        [Test] public void ReplacingGateway_DisposesOnlyTheRetiredOwnedTransport()
        {
            var first=new GatewayFixture();var second=new GatewayFixture();
            Func<string> token=()=>"11111111-1111-4111-8111-111111111111";
            Call("ConfigureSemanticGateway","https://gateway.example.invalid",token,first);
            Call("ConfigureSemanticGateway","https://gateway.example.invalid",token,second);
            Assert.That(first.Disposed,Is.True,"Replacing a gateway must release its owned transport/session closure.");
            Assert.That(second.Disposed,Is.False);
            Call("DisableSemanticTranslation");Assert.That(second.Disposed,Is.True);
        }
        [Test] public async Task InvalidReplacementAttempt_DoesNotHideActualActiveRouteAuthenticationFailure()
        {
            var transport=new HttpFixture{Status=401};Call("ConfigureSessionOpenRouter",FixtureKey,transport);
            Call("ConfigureSessionOpenRouter","",new HttpFixture());
            await chat.SubmitMessageAsync("build a barracks");
            Assert.That(transport.Calls,Is.EqualTo(1));Assert.That(Status(),Does.Contain("authentication failed"));
        }
        [Test] public async Task GatewayNetworkError_ReportsConnectionActionNotInventedHttpServiceFailure()
        {
            var transport=new GatewayFixture{NetworkFailure=true};
            Call("ConfigureSemanticGateway","https://gateway.example.invalid",(Func<string>)(()=>"11111111-1111-4111-8111-111111111111"),transport);
            await chat.SubmitMessageAsync("build a barracks");
            Assert.That(Status(),Does.Contain("connection failed"));Assert.That(manager.Goals,Is.Empty);
        }
        [Test] public async Task ChangedSessionToPaddedUuid_IsRejectedBeforeGatewayUpload()
        {
            string token="11111111-1111-4111-8111-111111111111";var transport=new GatewayFixture();
            Call("ConfigureSemanticGateway","https://gateway.example.invalid",(Func<string>)(()=>token),transport);
            token=" "+token+" ";await chat.SubmitMessageAsync("build a barracks");
            Assert.That(transport.Calls,Is.Zero,"A changed credential must remain exactly bounded, not trimmed and forwarded.");
            Assert.That(manager.Goals,Is.Empty);
        }
        private sealed class HttpFixture:ICommanderHttpTransport
        {
            public int Calls,Status=200;public bool NetworkFailure;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string json,IReadOnlyDictionary<string,string> headers,CancellationToken token)
            {
                Calls++;if(NetworkFailure)throw new HttpRequestException(FixtureKey);
                return Task.FromResult(new CommanderHttpResponse(Status,"{\"choices\":[{\"message\":{\"content\":\"{\\\"outcome\\\":\\\"Answer\\\",\\\"message\\\":\\\"fixture\\\"}\"},\"finish_reason\":\"stop\"}]}"));
            }
        }
        private sealed class DeferredProvider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            public readonly TaskCompletionSource<CommanderSemanticResult> Pending=new TaskCompletionSource<CommanderSemanticResult>();
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)=>Pending.Task;
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)=>throw new InvalidOperationException();
        }
        private sealed class GatewayFixture:ICommanderGatewayTransport
        {
            public int Calls;public bool Disposed,NetworkFailure;public CommanderGatewayRequestKind Kind;
            public Task<CommanderGatewayResponse> SendAsync(string gateway,Guid id,string token,string policy,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken cancel)
            {Calls++;Kind=kind;if(NetworkFailure)return Task.FromResult(new CommanderGatewayResponse(0,null,"GATEWAY_NETWORK_ERROR"));return Task.FromResult(new CommanderGatewayResponse(200,"{\"choices\":[{\"message\":{\"content\":\"{\\\"outcome\\\":\\\"Answer\\\",\\\"message\\\":\\\"fixture\\\"}\"},\"finish_reason\":\"stop\"}]}"));}
            public void Dispose(){Disposed=true;}
        }
    }
}
