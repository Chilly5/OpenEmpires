mergeInto(LibraryManager.library,
{
    RegisterKeyboardOverrides: function()
    {
        document.addEventListener('keydown', function(e)
        {
            if (e.ctrlKey && !e.shiftKey && !e.altKey &&
                e.code >= 'Digit0' && e.code <= 'Digit9')
            {
                e.preventDefault();
            }
        });
    }
});
