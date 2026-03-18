mergeInto(LibraryManager.library,
{
    IsSoftwareRenderer: function()
    {
        try
        {
            var canvas = document.createElement("canvas");
            var gl = canvas.getContext("webgl") || canvas.getContext("experimental-webgl");
            if (!gl) return 1;

            var ext = gl.getExtension("WEBGL_debug_renderer_info");
            if (!ext) return 0;

            var renderer = gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) || "";
            var lower = renderer.toLowerCase();

            if (lower.indexOf("swiftshader") !== -1) return 1;
            if (lower.indexOf("llvmpipe") !== -1) return 1;
            if (lower.indexOf("microsoft basic render driver") !== -1) return 1;

            return 0;
        }
        catch (e)
        {
            return 0;
        }
    }
});
