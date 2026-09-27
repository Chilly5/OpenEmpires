using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E2")]
    public sealed class CommanderPhase4E2PlacementCandidatesTests
    {
        // Mutation caught: interpreting five tiles as center distance or as five occupied columns.
        [Test]
        public void MapWest_FiveClearColumnsPlacesExactOriginBeforeFallbacks()
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, 4, 3, 3, 80, 80,
                CommanderSemanticPlacementRelation.MapWest, 5);

            Assert.That(candidates[0], Is.EqualTo(new Vector2Int(12, 20)));
            Assert.That(20 - (candidates[0].x + 3), Is.EqualTo(5));
            Assert.That(candidates.FindAll(tile => tile == candidates[0]).Count, Is.EqualTo(1));
        }

        // Mutation caught: C# truncation toward zero mis-centers a larger odd-height new footprint.
        [TestCase(2, 3, 19)]
        [TestCase(4, 3, 20)]
        [TestCase(3, 4, 19)]
        public void MapWest_CenterlineHalfTileTieUsesFloorAndLowerZ(int anchorHeight,
            int newHeight, int expectedZ)
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, anchorHeight, 3, newHeight,
                80, 80, CommanderSemanticPlacementRelation.MapWest, 5);

            Assert.That(candidates[0], Is.EqualTo(new Vector2Int(12, expectedZ)));
        }

        // Mutation caught: registry/map enumeration order or distance-first ranking could place a fallback first.
        [Test]
        public void MapWest_FallbacksRankGapErrorThenDisplacementThenXThenZ()
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, 4, 3, 3, 80, 80,
                CommanderSemanticPlacementRelation.MapWest, 5);

            Assert.That(candidates.GetRange(0, 5), Is.EqualTo(new[]
            {
                new Vector2Int(12, 20), new Vector2Int(12, 19),
                new Vector2Int(12, 21), new Vector2Int(12, 18),
                new Vector2Int(12, 22)
            }));
            Assert.That(candidates[5], Is.EqualTo(new Vector2Int(11, 20)));
        }

        // Mutation caught: a west request silently relocating east or outside the approved tolerance.
        [Test]
        public void MapWest_CandidatesNeverReverseDirectionOrExceedBoundedTolerance()
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, 4, 3, 3, 80, 80,
                CommanderSemanticPlacementRelation.MapWest, 5);

            foreach (Vector2Int tile in candidates)
            {
                int actualGap = 20 - (tile.x + 3);
                Assert.That(actualGap, Is.InRange(4, 6));
                Assert.That(Math.Abs(tile.x - 12), Is.LessThanOrEqualTo(2));
                Assert.That(Math.Abs(tile.y - 20), Is.LessThanOrEqualTo(2));
                Assert.That(tile.y, Is.LessThan(24));
                Assert.That(tile.y + 3, Is.GreaterThan(20));
            }
            Assert.That(candidates, Has.Count.EqualTo(15));
        }

        // Mutation caught: east is incorrectly treated as camera-right or measured to anchor minimum X.
        [Test]
        public void MapEast_ExactOriginStartsAfterAnchorMaxEdgeAndFiveClearColumns()
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, 4, 3, 3, 80, 80,
                CommanderSemanticPlacementRelation.MapEast, 5);

            Assert.That(candidates[0], Is.EqualTo(new Vector2Int(29, 20)));
            Assert.That(candidates[0].x - (20 + 4), Is.EqualTo(5));
            Assert.That(candidates.TrueForAll(tile => tile.x >= 24), Is.True);
        }

        // Mutation caught: clamping an invalid exact location into the map fabricates an unrelated candidate.
        [Test]
        public void MapWest_AtMapEdgeReturnsNoOutOfBoundsOrUnrelatedCandidate()
        {
            List<Vector2Int> candidates = Generate(2, 20, 4, 4, 3, 3, 40, 40,
                CommanderSemanticPlacementRelation.MapWest, 5);

            Assert.That(candidates, Is.Empty);
        }

        // Mutation caught: a footprint may fit by origin while its far edge crosses the map boundary.
        [Test]
        public void MapEast_ExcludesCandidatesWhoseFullFootprintCrossesMapBounds()
        {
            List<Vector2Int> candidates = Generate(30, 20, 4, 4, 3, 3, 40, 40,
                CommanderSemanticPlacementRelation.MapEast, 5);

            Assert.That(candidates, Is.Empty);
        }

        // Mutation caught: near is implemented as an arbitrary radial spiral or only one direction.
        [Test]
        public void Near_VisitsFourExactCardinalOneClearTileSidesBeforeFallbacks()
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, 4, 2, 2, 80, 80,
                CommanderSemanticPlacementRelation.Near, 1);

            Assert.That(candidates.GetRange(0, 4), Is.EqualTo(new[]
            {
                new Vector2Int(17, 21), new Vector2Int(21, 17),
                new Vector2Int(21, 25), new Vector2Int(25, 21)
            }));
        }

        // Mutation caught: near candidates leak to diagonal/unrelated sites or unbounded gaps.
        [Test]
        public void Near_AllCandidatesStayOnOverlappingCardinalSideWithinGapAndCenterlineTolerance()
        {
            List<Vector2Int> candidates = Generate(20, 20, 4, 4, 2, 2, 80, 80,
                CommanderSemanticPlacementRelation.Near, 1);

            foreach (Vector2Int tile in candidates)
            {
                bool west = tile.x + 2 <= 20 && tile.y < 24 && tile.y + 2 > 20;
                bool east = tile.x >= 24 && tile.y < 24 && tile.y + 2 > 20;
                bool south = tile.y + 2 <= 20 && tile.x < 24 && tile.x + 2 > 20;
                bool north = tile.y >= 24 && tile.x < 24 && tile.x + 2 > 20;
                Assert.That(west || east || south || north, Is.True, tile.ToString());
                int gap = west ? 20 - (tile.x + 2) : east ? tile.x - 24
                    : south ? 20 - (tile.y + 2) : tile.y - 24;
                Assert.That(gap, Is.InRange(0, 3), tile.ToString());
            }
            Assert.That(candidates, Is.Unique);
        }

        private static List<Vector2Int> Generate(int anchorX, int anchorZ, int anchorWidth,
            int anchorHeight, int newWidth, int newHeight, int mapWidth, int mapHeight,
            CommanderSemanticPlacementRelation relation, int clearGapTiles)
        {
            // Reflection makes the missing production type a RED assertion, not an assembly import error.
            Type type = typeof(CommanderSemanticJson).Assembly.GetType(
                "OpenEmpires.CommanderSemanticPlacementCandidates");
            Assert.That(type, Is.Not.Null, "Task 3 requires pure deterministic candidate generation.");
            MethodInfo method = type.GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            object result = method.Invoke(null, new object[] { anchorX, anchorZ, anchorWidth,
                anchorHeight, newWidth, newHeight, mapWidth, mapHeight, relation, clearGapTiles });
            Assert.That(result, Is.AssignableTo<IEnumerable>());
            var candidates = new List<Vector2Int>();
            foreach (object item in (IEnumerable)result) candidates.Add((Vector2Int)item);
            return candidates;
        }
    }
}
