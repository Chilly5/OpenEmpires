mergeInto(LibraryManager.library,
{
    RequestBrowserFullscreen: function(gameObjectNamePtr)
    {
        var goName = UTF8ToString(gameObjectNamePtr);
        var canvas = document.querySelector("#unity-canvas");
        if (!canvas)
        {
            SendMessage(goName, 'OnFullscreenError', 'Canvas not found');
            return;
        }

        var promise = canvas.requestFullscreen();
        if (promise)
        {
            promise.then(function()
            {
                SendMessage(goName, 'OnFullscreenEntered', '');
            }).catch(function(err)
            {
                SendMessage(goName, 'OnFullscreenError', err.message || 'Fullscreen request denied');
            });
        }
    },

    RegisterFullscreenChangeListener: function(gameObjectNamePtr)
    {
        var goName = UTF8ToString(gameObjectNamePtr);

        // Remove previous listener if any
        if (window._oeFullscreenHandler)
        {
            document.removeEventListener('fullscreenchange', window._oeFullscreenHandler);
        }

        window._oeFullscreenHandler = function()
        {
            var isFullscreen = document.fullscreenElement != null ? "1" : "0";
            SendMessage(goName, 'OnFullscreenChanged', isFullscreen);
        };

        document.addEventListener('fullscreenchange', window._oeFullscreenHandler);
    }
});
