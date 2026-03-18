using System;
using UnityEngine;

namespace OpenEmpires
{
    public enum ResourceType
    {
        Food,
        Wood,
        Gold,
        Stone
    }

    public class ResourceNodeData
    {
        public int Id;
        public ResourceType Type;
        public FixedVector3 Position;
        public int RemainingAmount;
        public bool IsDepleted => RemainingAmount <= 0;
        public int TileX;
        public int TileZ;
        public int FootprintWidth;
        public int FootprintHeight;

        // Visual strike feedback (mirroring BuildingData pattern)
        public int LastDamageTick;
        public FixedVector3 LastDamageFromPos;

        // Farm linkage
        public int LinkedBuildingId = -1;
        public bool IsFarmNode;
        public bool IsCarcass;

        public ResourceNodeData(int id, ResourceType type, FixedVector3 position, int startingAmount, int footprintWidth = 1, int footprintHeight = 1)
        {
            Id = id;
            Type = type;
            Position = position;
            RemainingAmount = startingAmount;
            FootprintWidth = footprintWidth;
            FootprintHeight = footprintHeight;

            // Compute origin tile: floor(center - footprint/2)
            // For 2x2 at center (6.0, 6.0): origin = floor(6.0 - 1.0) = (5, 5)
            // For 1x1 at center (5.5, 5.5): origin = floor(5.5 - 0.5) = (5, 5)
            int halfWRaw = (footprintWidth * Fixed32.Scale) / 2;
            int originXRaw = position.x.Raw - halfWRaw;
            TileX = originXRaw >= 0 ? originXRaw >> Fixed32.FractionalBits
                : (originXRaw >> Fixed32.FractionalBits) - (((originXRaw & (Fixed32.Scale - 1)) != 0) ? 1 : 0);

            int halfHRaw = (footprintHeight * Fixed32.Scale) / 2;
            int originZRaw = position.z.Raw - halfHRaw;
            TileZ = originZRaw >= 0 ? originZRaw >> Fixed32.FractionalBits
                : (originZRaw >> Fixed32.FractionalBits) - (((originZRaw & (Fixed32.Scale - 1)) != 0) ? 1 : 0);
        }

        public int Harvest(int amount)
        {
            int harvested = Math.Min(amount, RemainingAmount);
            RemainingAmount -= harvested;
            return harvested;
        }
    }
}
