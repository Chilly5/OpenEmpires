mergeInto(LibraryManager.library,
{
    WebSocketConnect: function(urlPtr, gameObjectNamePtr)
    {
        var url = UTF8ToString(urlPtr);
        var goName = UTF8ToString(gameObjectNamePtr);

        if (window._oeWebSocket)
        {
            window._oeWebSocket.close();
            window._oeWebSocket = null;
        }

        window._oeWebSocketGoName = goName;

        var ws = new WebSocket(url);
        window._oeWebSocket = ws;

        ws.onopen = function()
        {
            SendMessage(goName, 'OnWebGLOpen', '');
        };

        ws.onerror = function()
        {
            SendMessage(goName, 'OnWebGLError', 'WebSocket error');
        };

        ws.onclose = function(event)
        {
            window._oeWebSocket = null;
            SendMessage(goName, 'OnWebGLClose', event.reason || 'Connection closed');
        };

        ws.onmessage = function(event)
        {
            SendMessage(goName, 'OnWebGLMessage', event.data);
        };
    },

    WebSocketRegisterVisibility: function(unused)
    {
        if (window._oeVisibilityHandler)
        {
            document.removeEventListener('visibilitychange', window._oeVisibilityHandler);
        }

        window._oeVisibilityHandler = function()
        {
            var visible = !document.hidden;
            var ws = window._oeWebSocket;
            if (ws && ws.readyState === WebSocket.OPEN)
            {
                ws.send('{"type":"TabVisibility","data":{"visible":' + visible + '}}');
            }

            // Start/stop JS-level keepalive pings while hidden
            if (!visible)
            {
                window._oeKeepAlive = setInterval(function()
                {
                    var ws2 = window._oeWebSocket;
                    if (ws2 && ws2.readyState === WebSocket.OPEN)
                    {
                        ws2.send('{"type":"Ping","data":{"timestamp":' + Date.now() + '}}');
                    }
                }, 10000);
            }
            else
            {
                if (window._oeKeepAlive)
                {
                    clearInterval(window._oeKeepAlive);
                    window._oeKeepAlive = null;
                }
            }
        };

        document.addEventListener('visibilitychange', window._oeVisibilityHandler);
    },

    WebSocketSend: function(messagePtr)
    {
        var message = UTF8ToString(messagePtr);
        if (window._oeWebSocket && window._oeWebSocket.readyState === WebSocket.OPEN)
        {
            window._oeWebSocket.send(message);
        }
    },

    WebSocketClose: function()
    {
        if (window._oeWebSocket)
        {
            window._oeWebSocket.close();
            window._oeWebSocket = null;
        }
    }
});
