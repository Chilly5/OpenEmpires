using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Optional hooks a game mode can plug into <see cref="GameSetup"/> (assign a MonoBehaviour
    /// implementing this to GameSetup's "Setup Extension" field). Lets a mode such as
    /// AI Village reshape the map before rendering and replace the default base spawn.
    /// </summary>
    public interface IGameSetupExtension
    {
        /// <summary>Called after the sim exists but before the terrain mesh / resources are built.</summary>
        void OnBeforeMapRender(GameSetup setup, GameSimulation sim, Vector2Int[] basePositions);

        /// <summary>Return true to replace the default TC + villagers + scout + sheep spawn for this player.</summary>
        bool SpawnPlayerBase(GameSetup setup, GameSimulation sim, int playerId, int tileX, int tileZ);
    }
}
