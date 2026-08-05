using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class FormationFacingTests
    {
        [Test]
        public void GroupedFacingFormation_KeepsDestinationAtCentroid()
        {
            Vector3 destination = new Vector3(12.5f, 0f, 18.5f);

            List<Vector3> positions = GameSetup.ComputeGroupedFacingFormation(
                destination, Vector3.forward, new[] { 3, 4, 2 });

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < positions.Count; i++)
                centroid += positions[i];
            centroid /= positions.Count;

            Assert.That(centroid.x, Is.EqualTo(destination.x).Within(0.0001f));
            Assert.That(centroid.z, Is.EqualTo(destination.z).Within(0.0001f));
        }

        [Test]
        public void GroupedFacingFormation_PutsEarlierGroupsInFront()
        {
            List<Vector3> positions = GameSetup.ComputeGroupedFacingFormation(
                Vector3.zero, Vector3.forward, new[] { 2, 2 });

            float frontGroupZ = (positions[0].z + positions[1].z) * 0.5f;
            float rearGroupZ = (positions[2].z + positions[3].z) * 0.5f;

            Assert.That(frontGroupZ, Is.GreaterThan(rearGroupZ));
        }

        [Test]
        public void GroupedFacingFormation_RotatesRowsWithFacingDirection()
        {
            List<Vector3> positions = GameSetup.ComputeGroupedFacingFormation(
                Vector3.zero, Vector3.right, new[] { 2, 2 });

            float frontGroupX = (positions[0].x + positions[1].x) * 0.5f;
            float rearGroupX = (positions[2].x + positions[3].x) * 0.5f;

            Assert.That(frontGroupX, Is.GreaterThan(rearGroupX));
        }

        [Test]
        public void MovementArrival_CommitsRequestedFacingToSimulation()
        {
            var registry = new UnitRegistry();
            var map = new MapData(40, 40);
            UnitData unit = registry.CreateUnit(
                0,
                new FixedVector3(Fixed32.FromFloat(20.5f), Fixed32.Zero, Fixed32.FromFloat(20.5f)),
                Fixed32.One,
                Fixed32.FromFloat(0.4f),
                Fixed32.One);

            unit.SetPath(new List<Vector2Int> { new Vector2Int(21, 20) });
            unit.FinalDestination = new FixedVector3(
                Fixed32.FromFloat(21.5f), Fixed32.Zero, Fixed32.FromFloat(20.5f));
            unit.State = UnitState.Moving;
            unit.HasTargetFacing = true;
            unit.TargetFacing = new FixedVector3(Fixed32.Zero, Fixed32.Zero, -Fixed32.One);

            new UnitMovementSystem().Tick(registry, map, Fixed32.FromInt(2));

            Assert.That(unit.State, Is.EqualTo(UnitState.Idle));
            Assert.That(unit.SimFacing.x.Raw, Is.EqualTo(0));
            Assert.That(unit.SimFacing.z.ToFloat(), Is.EqualTo(-1f).Within(0.0001f));
        }
    }
}
