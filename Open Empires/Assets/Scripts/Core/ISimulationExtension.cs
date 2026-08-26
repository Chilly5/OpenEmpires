namespace OpenEmpires
{
    /// <summary>
    /// A pluggable per-tick hook for game modes layered on top of the core simulation.
    /// Implementations must follow the same rules as any sim system: deterministic,
    /// Fixed32-only, and they must act on the world only through ordinary commands
    /// (enqueue into <see cref="GameSimulation.AiCommandBuffer"/>) or public sim APIs.
    /// </summary>
    public interface ISimulationExtension
    {
        void Tick(GameSimulation sim);
    }
}
