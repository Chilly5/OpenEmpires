using System.Runtime.CompilerServices;

// Commander integration tests use bounded internal diagnostics without widening the public API.
[assembly: InternalsVisibleTo("OpenEmpires.EditModeTests")]
[assembly: InternalsVisibleTo("OpenEmpires.PlayModeTests")]
