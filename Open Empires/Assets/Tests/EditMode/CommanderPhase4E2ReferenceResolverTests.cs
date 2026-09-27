using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E2")]
    public sealed class CommanderPhase4E2ReferenceResolverTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        // Mutation caught: IsMainTownCenter precedence would choose the newer TC.
        [Test]
        public void MyTownCenter_ChoosesOldestSurvivingOwnedIdInsteadOfMainFlag()
        {
            BuildingData oldest = TownCenter(0, x, z);
            BuildingData newerMain = TownCenter(0, x + 20, z, main: true);

            Assert.That(oldest.Id, Is.LessThan(newerMain.Id));
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, null), Is.SameAs(oldest));
        }

        // Mutation caught: BuildingRegistry's swap-with-last removal makes list order differ from ID order.
        [Test]
        public void MyTownCenter_OrdinalUsesOwnedSurvivingIdOrderAfterRegistryReorder()
        {
            BuildingData removed = TownCenter(0, x, z);
            TownCenter(1, x + 10, z);
            BuildingData first = TownCenter(0, x + 20, z);
            BuildingData second = TownCenter(0, x + 30, z);
            simulation.BuildingRegistry.RemoveBuilding(removed.Id);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, 1), Is.SameAs(first));
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, 2), Is.SameAs(second));
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, 3), Is.Null);
        }

        // Mutation caught: dead or foreign TCs must not become semantic anchors.
        [Test]
        public void MyTownCenter_ExcludesDestroyedAndForeignBuildings()
        {
            TownCenter(1, x, z);
            BuildingData destroyed = TownCenter(0, x + 10, z);
            destroyed.CurrentHealth = 0;
            BuildingData live = TownCenter(0, x + 20, z);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, null), Is.SameAs(live));
        }

        // Mutation caught: copying FindPrimaryTownCenter's completed-only filter would reject a surviving foundation.
        [Test]
        public void MyTownCenter_IncludesSurvivingUnderConstructionTownCenter()
        {
            BuildingData foundation = TownCenter(0, x, z, underConstruction: true);
            TownCenter(0, x + 20, z, main: true);

            Assert.That(foundation.IsUnderConstruction, Is.True);
            Assert.That(foundation.IsDestroyed, Is.False);
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, null), Is.SameAs(foundation));
        }

        // Mutation caught: invalid ordinals or an absent owned TC must not select a fallback foreign building.
        [Test]
        public void MyTownCenter_FailsClosedForInvalidOrdinalAndNoOwnedTownCenter()
        {
            TownCenter(1, x, z);
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, null), Is.Null);
            TownCenter(0, x + 20, z);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, 0), Is.Null);
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, 9), Is.Null);
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyTownCenter, 2), Is.Null);
        }

        // Mutation caught: selecting the oldest Barracks instead of the nearest owned one.
        [Test]
        public void MyBarracks_ChoosesNearestOwnedBarracksToOldestTownCenter()
        {
            TownCenter(0, x, z);
            Barracks(0, x + 25, z);
            BuildingData near = Barracks(0, x + 8, z);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyBarracks, null), Is.SameAs(near));
        }

        // Mutation caught: a distance tie must resolve by stable Id, not unstable registry order.
        [Test]
        public void MyBarracks_EqualDistanceChoosesLowerIdAfterRegistryReorder()
        {
            TownCenter(0, x, z);
            BuildingData removed = Barracks(0, x + 30, z);
            int oppositeZ = z + config.TownCenterFootprintHeight
                - config.BarracksFootprintHeight + 10;
            BuildingData lowerId = Barracks(0, x + 10, z - 10);
            Barracks(1, x + 10, z);
            Barracks(0, x + 10, oppositeZ);
            simulation.BuildingRegistry.RemoveBuilding(removed.Id);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyBarracks, null), Is.SameAs(lowerId));
        }

        // Mutation caught: dead/foreign Barracks and a Barracks ordinal are not legal MyBarracks results.
        [Test]
        public void MyBarracks_ExcludesDestroyedForeignAndUnsupportedOrdinal()
        {
            TownCenter(0, x, z);
            Barracks(1, x + 3, z);
            BuildingData dead = Barracks(0, x + 5, z);
            dead.CurrentHealth = 0;
            BuildingData owned = Barracks(0, x + 15, z);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyBarracks, null), Is.SameAs(owned));
            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyBarracks, 1), Is.Null);
        }

        // Mutation caught: without an owned TC there is no trusted base-relative nearest Barracks.
        [Test]
        public void MyBarracks_WithoutOwnedTownCenterFailsClosed()
        {
            TownCenter(1, x, z);
            Barracks(0, x + 8, z);

            Assert.That(ResolveAnchor(CommanderSemanticAnchorSelector.MyBarracks, null), Is.Null);
        }

        // Mutation caught: a hidden, explored-only, depleted or wrong-type node may not outrank visible Gold.
        [Test]
        public void NearestGold_UsesOnlyCurrentlyVisibleNondepletedMatchingResources()
        {
            TownCenter(0, x, z);
            ResourceNodeData explored = Resource(ResourceType.Gold, x + 2, z, 100);
            simulation.FogOfWar.SetVisible(0, explored.TileX, explored.TileZ);
            simulation.FogOfWar.DemoteAllVisible(0);
            Resource(ResourceType.Gold, x + 3, z, 100); // Unexplored.
            ResourceNodeData depleted = Resource(ResourceType.Gold, x + 4, z, 0);
            simulation.FogOfWar.SetVisible(0, depleted.TileX, depleted.TileZ);
            ResourceNodeData wood = Resource(ResourceType.Wood, x + 5, z, 100);
            simulation.FogOfWar.SetVisible(0, wood.TileX, wood.TileZ);
            ResourceNodeData visible = Resource(ResourceType.Gold, x + 12, z, 100);
            simulation.FogOfWar.SetVisible(0, visible.TileX, visible.TileZ);

            Assert.That(ResolveResource(ResourceType.Gold, null), Is.SameAs(visible));
            Assert.That(simulation.FogOfWar.GetVisibility(0, explored.TileX, explored.TileZ),
                Is.EqualTo(TileVisibility.Explored), "The selector must not reveal a resource tile.");
        }

        // Mutation caught: globally inspecting hidden nodes would return a resource even with no visible candidate.
        [Test]
        public void NearestGold_NoCurrentlyVisibleCandidateFailsWithoutRevealingFog()
        {
            TownCenter(0, x, z);
            ResourceNodeData hidden = Resource(ResourceType.Gold, x + 5, z, 100);

            Assert.That(ResolveResource(ResourceType.Gold, null), Is.Null);
            Assert.That(simulation.FogOfWar.GetVisibility(0, hidden.TileX, hidden.TileZ),
                Is.EqualTo(TileVisibility.Unexplored));
        }

        // Mutation caught: equidistant candidates must use resource Id as the stable tie-breaker.
        [Test]
        public void NearestGold_EqualDistanceChoosesLowerResourceId()
        {
            TownCenter(0, x, z);
            ResourceNodeData lowerId = Resource(ResourceType.Gold, x + 10, z - 10, 100);
            ResourceNodeData higherId = Resource(ResourceType.Gold, x + 10,
                z + config.TownCenterFootprintHeight + 9, 100);
            simulation.FogOfWar.SetVisible(0, lowerId.TileX, lowerId.TileZ);
            simulation.FogOfWar.SetVisible(0, higherId.TileX, higherId.TileZ);

            Assert.That(ResolveResource(ResourceType.Gold, null), Is.SameAs(lowerId));
        }

        // Mutation caught: an ordinal TC must be the distance anchor when the player specified it.
        [Test]
        public void NearestGold_UsesRequestedOwnedTownCenterOrdinal()
        {
            TownCenter(0, x - 25, z);
            TownCenter(0, x + 25, z);
            ResourceNodeData left = Resource(ResourceType.Gold, x - 15, z, 100);
            ResourceNodeData right = Resource(ResourceType.Gold, x + 35, z, 100);
            simulation.FogOfWar.SetVisible(0, left.TileX, left.TileZ);
            simulation.FogOfWar.SetVisible(0, right.TileX, right.TileZ);

            Assert.That(ResolveResource(ResourceType.Gold, 1), Is.SameAs(left));
            Assert.That(ResolveResource(ResourceType.Gold, 2), Is.SameAs(right));
        }

        private BuildingData TownCenter(int playerId, int tileX, int tileZ,
            bool underConstruction = false, bool main = false)
        {
            return simulation.CreateBuilding(playerId, BuildingType.TownCenter, tileX, tileZ,
                underConstruction, main);
        }

        private BuildingData Barracks(int playerId, int tileX, int tileZ)
        {
            return simulation.CreateBuilding(playerId, BuildingType.Barracks, tileX, tileZ);
        }

        private ResourceNodeData Resource(ResourceType type, int tileX, int tileZ, int amount)
        {
            return simulation.MapData.AddResourceNode(type,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), amount);
        }

        private BuildingData ResolveAnchor(CommanderSemanticAnchorSelector selector, int? ordinal)
        {
            object[] arguments = { 0, selector, ordinal, null };
            bool resolved = InvokeResolver("TryResolveOwnedAnchor", arguments);
            Assert.That(resolved, Is.EqualTo(arguments[3] != null));
            return (BuildingData)arguments[3];
        }

        private ResourceNodeData ResolveResource(ResourceType type, int? townCenterOrdinal)
        {
            object[] arguments = { 0, type, townCenterOrdinal, null };
            bool resolved = InvokeResolver("TryResolveVisibleResourceNearestToTownCenter", arguments);
            Assert.That(resolved, Is.EqualTo(arguments[3] != null));
            return (ResourceNodeData)arguments[3];
        }

        private bool InvokeResolver(string methodName, object[] arguments)
        {
            // Reflection keeps RED as a failing assertion rather than a compiler/import error.
            Type resolverType = typeof(CommanderSemanticJson).Assembly.GetType(
                "OpenEmpires.CommanderSemanticReferenceResolver");
            Assert.That(resolverType, Is.Not.Null, "Task 2 requires a game-side resolver.");
            ConstructorInfo constructor = resolverType.GetConstructor(new[] { typeof(GameSimulation) });
            Assert.That(constructor, Is.Not.Null, "The resolver must use trusted GameSimulation state.");
            MethodInfo method = resolverType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Missing semantic reference API: " + methodName);
            object resolver = constructor.Invoke(new object[] { simulation });
            return (bool)method.Invoke(resolver, arguments);
        }
    }
}
