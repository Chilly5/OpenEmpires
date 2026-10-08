using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixHttpTests
    {
        // Real loopback HTTP, no vendor/credential/audio/paid request and no transport double.
        [TestCase(false, 200)] [TestCase(true, 200)] [TestCase(true, 500)]
        public async Task OversizedDeclaredChunkedAndErrorBodies_AreRejectedDuringReceive(bool chunked, int status)
        {
            await WithResponse(new string('x', 65537), status, chunked, false, async uri =>
            {
                var error = await ExpectOversized(uri).ConfigureAwait(false);
                Assert.That(error.Message, Does.Contain("oversized"));
            }).ConfigureAwait(false);
        }

        [Test]
        public async Task ByteLimit_IsIndependentOfDecodedCharacterCount()
        {
            // Fewer than65536 chars, but more than65536 UTF8 bytes.
            await WithResponse(new string('\u754c', 22000), 200, true, false, async uri =>
            {
                await ExpectOversized(uri).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [Test]
        public async Task CompressedBody_IsBoundedAfterDecompression()
        {
            await WithResponse(new string('x', 65537), 200, false, true, async uri =>
            {
                await ExpectOversized(uri).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestCase(false)] [TestCase(true)]
        public async Task BoundedUtf8Body_PreservesStatusAndText(bool compressed)
        {
            const string body = "{\"message\":\"bounded \u754c text\"}";
            await WithResponse(body, 429, true, compressed, async uri =>
            {
                var response = await ReadResponse(uri).ConfigureAwait(false);
                Assert.That(response.StatusCode, Is.EqualTo(429));
                Assert.That(response.Body, Is.EqualTo(body));
            }).ConfigureAwait(false);
        }

        [TestCase(401, "authentication")] [TestCase(429, "quota")]
        public async Task OversizedErrorBody_PreservesProviderStatusWithoutBodyOrFallback(int status, string category)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                var context = new CommanderContextBuilder().Build(sim, manager);
                var request = new CommanderAIRequest("make four spearmen", context, Array.Empty<CommanderConversationMessage>());
                foreach (bool gemini in new[] { false, true })
                {
                    var transport = new OversizedTransport(status);
                    ICommanderAIProvider provider = gemini ? (ICommanderAIProvider)new GeminiAIProvider("fixture-only", transport)
                        : new OpenRouterCommanderProvider("fixture-only", transport);
                    var result = await provider.TranslateAsync(request, CancellationToken.None);
                    Assert.That(result.Success, Is.False);
                    Assert.That(result.FailureReason.ToLowerInvariant(), Does.Contain(category));
                    Assert.That(transport.Calls, Is.EqualTo(1));
                    Assert.That(manager.Goals, Is.Empty);
                    Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public async Task CancellationDuringBodyRead_DoesNotReturnPartialSuccess()
        {
            await WithResponse("bounded", 200, false, false, async uri =>
            {
                using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                bool cancelled = false;
                try { await new CommanderHttpClientTransport().PostJsonAsync(uri, "{}", null, cancel.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { cancelled = true; }
                Assert.That(cancelled, Is.True);
            }, bodyDelayMilliseconds:500).ConfigureAwait(false);
        }

        private sealed class OversizedTransport : ICommanderHttpTransport
        {
            private readonly int status;
            internal int Calls { get; private set; }
            internal OversizedTransport(int status) { this.status = status; }
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json, IReadOnlyDictionary<string,string> headers,
                CancellationToken token)
            {
                Calls++;
                return Task.FromException<CommanderHttpResponse>(new CommanderHttpResponseLimitException(status));
            }
        }

        private static async Task<CommanderHttpResponse> ReadResponse(Uri uri)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await new CommanderHttpClientTransport().PostJsonAsync(uri, "{}", null, deadline.Token).ConfigureAwait(false);
        }

        private static async Task<IOException> ExpectOversized(Uri uri)
        {
            IOException failure = null;
            try { await ReadResponse(uri).ConfigureAwait(false); }
            catch (IOException error) { failure = error; }
            Assert.That(failure, Is.Not.Null, "Oversized application-visible response must fail during receive.");
            return failure;
        }

        private static async Task WithResponse(string body, int status, bool chunked, bool compressed,
            Func<Uri, Task> assertion, int bodyDelayMilliseconds = 0)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = client.GetStream();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var request = new byte[4096];
                int received = 0;
                while (received < request.Length)
                {
                    int count = await stream.ReadAsync(request, received, request.Length - received, deadline.Token).ConfigureAwait(false);
                    if (count == 0) throw new IOException("Fixture client closed before request.");
                    received += count;
                    int end = Encoding.ASCII.GetString(request, 0, received).IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (end >= 0 && received >= end + 6) break; // Test requests always contain exactly {}.
                }
                byte[] payload = Encoding.UTF8.GetBytes(body);
                if (compressed)
                {
                    using var encoded = new MemoryStream();
                    using (var gzip = new GZipStream(encoded, CompressionMode.Compress, true)) gzip.Write(payload, 0, payload.Length);
                    payload = encoded.ToArray();
                }
                string headers = "HTTP/1.1 " + status + " Fixture\r\nConnection: close\r\nContent-Type: application/json; charset=utf-8\r\n"
                    + (compressed ? "Content-Encoding: gzip\r\n" : "")
                    + (chunked ? "Transfer-Encoding: chunked\r\n" : "Content-Length: " + payload.Length + "\r\n") + "\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length, deadline.Token).ConfigureAwait(false);
                if (bodyDelayMilliseconds > 0) await Task.Delay(bodyDelayMilliseconds, deadline.Token).ConfigureAwait(false);
                try
                {
                    if (chunked)
                    {
                        byte[] size = Encoding.ASCII.GetBytes(payload.Length.ToString("X") + "\r\n");
                        await stream.WriteAsync(size, 0, size.Length, deadline.Token).ConfigureAwait(false);
                    }
                    await stream.WriteAsync(payload, 0, payload.Length, deadline.Token).ConfigureAwait(false);
                    if (chunked)
                    {
                        byte[] end = Encoding.ASCII.GetBytes("\r\n0\r\n\r\n");
                        await stream.WriteAsync(end, 0, end.Length, deadline.Token).ConfigureAwait(false);
                    }
                }
                catch (IOException) { /* Cap can close the client early; this is expected. */ }
                catch (SocketException) { /* Same expected early close at the socket layer. */ }
            });
            try { await assertion(new Uri("http://127.0.0.1:" + endpoint.Port + "/fixture")).ConfigureAwait(false); }
            finally
            {
                listener.Stop();
                var completed = await Task.WhenAny(server, Task.Delay(6000)).ConfigureAwait(false);
                if (completed != server)
                    _ = server.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                else
                {
                    try { await server.ConfigureAwait(false); }
                    catch (SocketException) { /* Listener stopped with accept still pending. */ }
                    catch (ObjectDisposedException) { /* Owned listener teardown, not assertion failure. */ }
                }
            }
        }
    }
}
