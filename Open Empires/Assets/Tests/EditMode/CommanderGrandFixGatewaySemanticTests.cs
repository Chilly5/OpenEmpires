using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixGatewaySemanticTests
    {
        private const string Session="11111111-1111-4111-8111-111111111111";
        private static ICommanderHttpTransport Create(Fixture transport,Func<string> token=null)
        {
            var type=typeof(CommanderVoiceInputController).Assembly.GetType("OpenEmpires.CommanderGatewaySemanticTransport");Assert.That(type,Is.Not.Null);
            return (ICommanderHttpTransport)Activator.CreateInstance(type,new object[]{"https://gateway.example.invalid",token??(()=>Session),transport});
        }
        [Test]public async Task SemanticBody_UsesFixedGatewayAndBackendSessionNotClientProviderCredential()
        {
            var fixture=new Fixture();var transport=Create(fixture);
            const string body="{\"model\":\"openai/gpt-6-luna\",\"messages\":[{\"role\":\"user\",\"content\":\"fixture\"}],\"max_tokens\":4096}";
            var response=await transport.PostJsonAsync(new Uri("https://openrouter.ai/api/v1/chat/completions"),body,
                new Dictionary<string,string>{{"Authorization","Bearer client-credential-must-not-forward"}},CancellationToken.None);
            Assert.That(fixture.Token,Is.EqualTo(Session));Assert.That(fixture.Kind,Is.EqualTo(CommanderGatewayRequestKind.Semantic));
            Assert.That(Encoding.UTF8.GetString(fixture.Body),Is.EqualTo(body));Assert.That(response.StatusCode,Is.EqualTo(429));
            Assert.That(response.Body,Is.EqualTo("{\"error\":\"quota\"}"));Assert.That(fixture.Calls,Is.EqualTo(1));
        }
        [Test]public async Task DifferentProviderUrl_IsRejectedWithoutSilentModelSwitchOrNetwork()
        {
            var fixture=new Fixture();var transport=Create(fixture);
            var response=await transport.PostJsonAsync(new Uri("https://generativelanguage.googleapis.com/fixture"),"{}",null,CancellationToken.None);
            Assert.That(response.StatusCode,Is.EqualTo(400));Assert.That(fixture.Calls,Is.Zero);
        }
        [Test]public async Task ChangedBackendSession_RejectsLateSemanticResponse()
        {
            string token=Session;var fixture=new Fixture{Deferred=true};var transport=Create(fixture,()=>token);
            var task=transport.PostJsonAsync(new Uri("https://openrouter.ai/api/v1/chat/completions"),"{}",null,CancellationToken.None);
            token="33333333-3333-4333-8333-333333333333";fixture.Complete();var response=await task;
            Assert.That(response.StatusCode,Is.EqualTo(401));Assert.That(response.Body,Does.Not.Contain("quota"));
        }
        private sealed class Fixture:ICommanderGatewayTransport
        {
            public bool Deferred;public int Calls;public string Token;public byte[] Body;public CommanderGatewayRequestKind Kind;
            private TaskCompletionSource<CommanderGatewayResponse> pending;
            public Task<CommanderGatewayResponse> SendAsync(string gateway,Guid job,string token,string policy,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken cancellation)
            {Calls++;Token=token;Body=body;Kind=kind;if(!Deferred)return Task.FromResult(new CommanderGatewayResponse(429,"{\"error\":\"quota\"}"));pending=new TaskCompletionSource<CommanderGatewayResponse>();return pending.Task;}
            public void Complete()=>pending.TrySetResult(new CommanderGatewayResponse(429,"{\"error\":\"quota\"}"));public void Dispose(){}
        }
    }
}
