using System;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif
using UnityEngine;

namespace OpenEmpires
{
    public class WebSocketClient : MonoBehaviour
    {
        public string ServerUrl { get; set; } = "wss://openempires.onrender.com/ws";

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        public event Action<ServerMessage, long> OnMessageReceived;

        public struct TimestampedMessage
        {
            public ServerMessage Message;
            public long ReceivedAtTicks;
            public long ConnectionEpoch;
        }

        private Queue<TimestampedMessage> messageQueue = new Queue<TimestampedMessage>();
        private bool connecting;
        private long connectionEpoch;
        public long ConnectionEpoch => connectionEpoch;

#if UNITY_WEBGL && !UNITY_EDITOR

        // ========== WebGL: Browser native WebSocket via .jslib ==========

        [DllImport("__Internal")] private static extern void WebSocketConnect(string url, string gameObjectName);
        [DllImport("__Internal")] private static extern void WebSocketSend(string message);
        [DllImport("__Internal")] private static extern void WebSocketClose();
        [DllImport("__Internal")] private static extern void WebSocketRegisterVisibility(string gameObjectName);

        private bool webglConnected;

        public bool IsConnected => webglConnected;

        public void Connect()
        {
            if (webglConnected || connecting) return;

            connecting = true;
            connectionEpoch++; messageQueue.Clear();
            Debug.Log($"[WebSocket] Connecting to {ServerUrl}...");
            WebSocketConnect(ServerUrl, gameObject.name);
            WebSocketRegisterVisibility("");
        }

        public void Send(ClientMessage message)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[WebSocket] Cannot send - not connected");
                return;
            }

            WebSocketSend(message.ToJson());
        }

        public void Disconnect()
        {
            connectionEpoch++; messageQueue.Clear();
            if (webglConnected || connecting)
            {
                WebSocketClose();
                webglConnected = false;
                connecting = false;
            }
        }

        // Called from JavaScript via SendMessage
        public void OnWebGLOpen(string unused)
        {
            Debug.Log("[WebSocket] Connected");
            connecting = false;
            webglConnected = true;
            OnConnected?.Invoke();
        }

        public void OnWebGLClose(string reason)
        {
            bool wasConnected = webglConnected;
            connecting = false;
            webglConnected = false;

            if (wasConnected)
            {
                Debug.Log($"[WebSocket] Closed: {reason}");
                OnDisconnected?.Invoke(string.IsNullOrEmpty(reason) ? "Connection closed" : reason);
            }
        }

        public void OnWebGLError(string error)
        {
            Debug.LogError($"[WebSocket] Error: {error}");
            connecting = false;
            OnError?.Invoke(error);
        }

        public void OnWebGLMessage(string json)
        {
            try
            {
                var message = ServerMessageParser.Parse(json);
                if (message != null)
                {
                    messageQueue.Enqueue(new TimestampedMessage
                    {
                        Message = message,
                        ReceivedAtTicks = DateTime.UtcNow.Ticks
                        ,ConnectionEpoch = connectionEpoch
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocket] Parse error: {ex.Message}");
            }
        }

        private void Update()
        {
            while (messageQueue.Count > 0)
            {
                var tsMsg = messageQueue.Dequeue();
                if(tsMsg.ConnectionEpoch!=connectionEpoch||!IsConnected)continue;
                OnMessageReceived?.Invoke(tsMsg.Message, tsMsg.ReceivedAtTicks);
            }
        }

#else

        // ========== Desktop: .NET ClientWebSocket ==========

        private ClientWebSocket webSocket;
        private Queue<string> sendQueue = new Queue<string>();
        private CancellationTokenSource cancellationTokenSource;
        private readonly object lockObj = new object();

        public bool IsConnected => webSocket?.State == WebSocketState.Open;

        public async void Connect()
        {
            if (webSocket != null || connecting) return;

            connecting = true;
            long epoch=++connectionEpoch;
            lock(lockObj){messageQueue.Clear();sendQueue.Clear();}
            Debug.Log($"[WebSocket] Connecting to {ServerUrl}...");

            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                webSocket = new ClientWebSocket();

                await webSocket.ConnectAsync(new Uri(ServerUrl), cancellationTokenSource.Token);
                if(epoch!=connectionEpoch)return;

                Debug.Log("[WebSocket] Connected");
                connecting = false;
                OnConnected?.Invoke();

                // Start receive loop
                if(epoch==connectionEpoch&&webSocket!=null)_ = ReceiveLoop(webSocket,cancellationTokenSource.Token,epoch);
            }
            catch (Exception e)
            {
                if(epoch!=connectionEpoch)return;
                Debug.LogError($"[WebSocket] Connection failed: {e.Message}");
                connecting = false;
                webSocket?.Dispose();
                webSocket = null;
                OnError?.Invoke(e.Message);
            }
        }

        private async Task ReceiveLoop(ClientWebSocket socket,CancellationToken token,long epoch)
        {
            var buffer = new byte[8192];

            try
            {
                while (epoch==connectionEpoch&&socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        token
                    ).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[WebSocket] Server initiated close");
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None
                        ).ConfigureAwait(false);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        // Handle fragmented messages
                        if (!result.EndOfMessage)
                        {
                            var fullMessage = new StringBuilder(json);
                            while (!result.EndOfMessage)
                            {
                                result = await socket.ReceiveAsync(
                                    new ArraySegment<byte>(buffer),
                                    token
                                ).ConfigureAwait(false);
                                fullMessage.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                            }
                            json = fullMessage.ToString();
                        }

                        try
                        {
                            var message = ServerMessageParser.Parse(json);
                            if (message != null)
                            {
                                var timestamped = new TimestampedMessage
                                {
                                    Message = message,
                                    ReceivedAtTicks = DateTime.UtcNow.Ticks
                                    ,ConnectionEpoch = epoch
                                };
                                lock (lockObj)
                                {
                                    if(epoch==connectionEpoch)messageQueue.Enqueue(timestamped);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[WebSocket] Parse error: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocket] Receive error: {e.Message}");
                if(epoch==connectionEpoch)OnError?.Invoke("WebSocket receive failed.");
            }
            finally
            {
                if(epoch==connectionEpoch)OnDisconnected?.Invoke("Connection closed");
            }
        }

        public async void Disconnect()
        {
            connectionEpoch++;connecting=false;
            lock(lockObj){messageQueue.Clear();sendQueue.Clear();}
            var socket=webSocket;var cancellation=cancellationTokenSource;
            webSocket=null;cancellationTokenSource=null;
            if (socket != null)
            {
                try
                {
                    cancellation?.Cancel();

                    if (socket.State == WebSocketState.Open)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Client disconnecting",
                            CancellationToken.None
                        );
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebSocket] Disconnect error: {e.Message}");
                }
                finally
                {
                    socket.Dispose();cancellation?.Dispose();
                }
            }
        }

        public void Send(ClientMessage message)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[WebSocket] Cannot send - not connected");
                return;
            }

            string json = message.ToJson();

            lock (lockObj)
            {
                sendQueue.Enqueue(json);
            }
        }

        private async Task ProcessSendQueue()
        {
            string json = null;

            lock (lockObj)
            {
                if (sendQueue.Count > 0)
                {
                    json = sendQueue.Dequeue();
                }
            }

            if (json != null && webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        cancellationTokenSource.Token
                    );
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebSocket] Send error: {e.Message}");
                }
            }
        }

        private void Update()
        {
            // Process send queue
            lock (lockObj)
            {
                while (sendQueue.Count > 0)
                {
                    _ = ProcessSendQueue();
                }
            }

            // Dispatch received messages on main thread
            lock (lockObj)
            {
                while (messageQueue.Count > 0)
                {
                    var tsMsg = messageQueue.Dequeue();
                    if(tsMsg.ConnectionEpoch!=connectionEpoch||!IsConnected)continue;
                    OnMessageReceived?.Invoke(tsMsg.Message, tsMsg.ReceivedAtTicks);
                }
            }
        }

#endif

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }
    }
}
