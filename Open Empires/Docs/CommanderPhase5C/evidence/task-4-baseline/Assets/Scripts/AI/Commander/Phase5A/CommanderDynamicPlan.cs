using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenEmpires
{
    public enum CommanderDynamicMechanic
    {
        SelectWorkers, PartitionWorkers, SelectUnits, SelectStructures,
        SelectResources, ResolveLocation, Build, AllocateWorkers, Produce
    }

    public enum CommanderDynamicResultKind
    {
        WorkerSet, UnitSet, StructureSet, ResourceSet, LocationIntent,
        AssignmentEffect, UnitResult
    }

    public enum CommanderDynamicEffectClass
    {
        Selection, Location, Construction, Allocation, Production
    }

    public enum CommanderDynamicFieldKind
    {
        Count, Offset, ClearGapTiles, UnitId, BuildingId, Resource,
        SourceKind, WorkerState, ResourceMode, Anchor, Relation,
        QuantityMode, UnitKind
    }

    public sealed class CommanderDynamicField
    {
        public string Name { get; }
        public CommanderDynamicFieldKind Kind { get; }
        public bool Required { get; }
        internal CommanderDynamicField(string name, CommanderDynamicFieldKind kind, bool required)
        { Name = name; Kind = kind; Required = required; }
    }

    public sealed class CommanderDynamicInput
    {
        public string Role { get; }
        public CommanderDynamicResultKind Accepts { get; }
        public bool Required { get; }
        internal CommanderDynamicInput(string role, CommanderDynamicResultKind accepts, bool required)
        { Role = role; Accepts = accepts; Required = required; }
    }

    public sealed class CommanderDynamicPrimitive
    {
        public string Id { get; }
        public CommanderDynamicMechanic Mechanic { get; }
        public CommanderDynamicResultKind Result { get; }
        public CommanderDynamicEffectClass Effect { get; }
        public IReadOnlyList<CommanderDynamicField> Parameters { get; }
        public IReadOnlyList<CommanderDynamicInput> Inputs { get; }

        internal CommanderDynamicPrimitive(string id, CommanderDynamicMechanic mechanic,
            CommanderDynamicResultKind result, CommanderDynamicEffectClass effect,
            CommanderDynamicField[] parameters, CommanderDynamicInput[] inputs)
        {
            Id = id; Mechanic = mechanic; Result = result; Effect = effect;
            Parameters = Array.AsReadOnly((CommanderDynamicField[])parameters.Clone());
            Inputs = Array.AsReadOnly((CommanderDynamicInput[])inputs.Clone());
        }
    }

    // Schema metadata only. Compiler dispatch must switch on Mechanic, never reflect on Id.
    public static class CommanderDynamicPrimitiveRegistry
    {
        private static CommanderDynamicField P(string name, CommanderDynamicFieldKind kind,
            bool required = true) => new CommanderDynamicField(name, kind, required);
        private static CommanderDynamicInput I(string role, CommanderDynamicResultKind kind,
            bool required = true) => new CommanderDynamicInput(role, kind, required);

        public static readonly IReadOnlyList<CommanderDynamicPrimitive> All =
            Array.AsReadOnly(new[]
            {
                new CommanderDynamicPrimitive("select-workers", CommanderDynamicMechanic.SelectWorkers,
                    CommanderDynamicResultKind.WorkerSet, CommanderDynamicEffectClass.Selection,
                    new[] { P("count", CommanderDynamicFieldKind.Count),
                        P("state", CommanderDynamicFieldKind.WorkerState, false),
                        P("currentResource", CommanderDynamicFieldKind.Resource, false) },
                    Array.Empty<CommanderDynamicInput>()),
                new CommanderDynamicPrimitive("partition-workers", CommanderDynamicMechanic.PartitionWorkers,
                    CommanderDynamicResultKind.WorkerSet, CommanderDynamicEffectClass.Selection,
                    new[] { P("offset", CommanderDynamicFieldKind.Offset),
                        P("count", CommanderDynamicFieldKind.Count) },
                    new[] { I("workers", CommanderDynamicResultKind.WorkerSet) }),
                new CommanderDynamicPrimitive("select-units", CommanderDynamicMechanic.SelectUnits,
                    CommanderDynamicResultKind.UnitSet, CommanderDynamicEffectClass.Selection,
                    new[] { P("unit", CommanderDynamicFieldKind.UnitId, false),
                        P("kind", CommanderDynamicFieldKind.UnitKind, false),
                        P("count", CommanderDynamicFieldKind.Count) },
                    Array.Empty<CommanderDynamicInput>()),
                new CommanderDynamicPrimitive("select-structures", CommanderDynamicMechanic.SelectStructures,
                    CommanderDynamicResultKind.StructureSet, CommanderDynamicEffectClass.Selection,
                    new[] { P("building", CommanderDynamicFieldKind.BuildingId),
                        P("count", CommanderDynamicFieldKind.Count) },
                    Array.Empty<CommanderDynamicInput>()),
                new CommanderDynamicPrimitive("select-resources", CommanderDynamicMechanic.SelectResources,
                    CommanderDynamicResultKind.ResourceSet, CommanderDynamicEffectClass.Selection,
                    new[] { P("resource", CommanderDynamicFieldKind.Resource),
                        P("sourceKind", CommanderDynamicFieldKind.SourceKind),
                        P("mode", CommanderDynamicFieldKind.ResourceMode),
                        P("count", CommanderDynamicFieldKind.Count) },
                    Array.Empty<CommanderDynamicInput>()),
                new CommanderDynamicPrimitive("resolve-location", CommanderDynamicMechanic.ResolveLocation,
                    CommanderDynamicResultKind.LocationIntent, CommanderDynamicEffectClass.Location,
                    new[] { P("anchor", CommanderDynamicFieldKind.Anchor, false),
                        P("relation", CommanderDynamicFieldKind.Relation),
                        P("clearGapTiles", CommanderDynamicFieldKind.ClearGapTiles) },
                    new[] { I("structures", CommanderDynamicResultKind.StructureSet, false),
                        I("resources", CommanderDynamicResultKind.ResourceSet, false) }),
                new CommanderDynamicPrimitive("build", CommanderDynamicMechanic.Build,
                    CommanderDynamicResultKind.StructureSet, CommanderDynamicEffectClass.Construction,
                    new[] { P("building", CommanderDynamicFieldKind.BuildingId),
                        P("count", CommanderDynamicFieldKind.Count) },
                    new[] { I("workers", CommanderDynamicResultKind.WorkerSet, false),
                        I("location", CommanderDynamicResultKind.LocationIntent, false) }),
                new CommanderDynamicPrimitive("allocate-workers", CommanderDynamicMechanic.AllocateWorkers,
                    CommanderDynamicResultKind.AssignmentEffect, CommanderDynamicEffectClass.Allocation,
                    new[] { P("resource", CommanderDynamicFieldKind.Resource),
                        P("sourceKind", CommanderDynamicFieldKind.SourceKind) },
                    new[] { I("workers", CommanderDynamicResultKind.WorkerSet) }),
                new CommanderDynamicPrimitive("produce", CommanderDynamicMechanic.Produce,
                    CommanderDynamicResultKind.UnitResult, CommanderDynamicEffectClass.Production,
                    new[] { P("unit", CommanderDynamicFieldKind.UnitId),
                        P("count", CommanderDynamicFieldKind.Count),
                        P("quantityMode", CommanderDynamicFieldKind.QuantityMode) },
                    new[] { I("producers", CommanderDynamicResultKind.StructureSet, false) })
            });

        public static CommanderDynamicPrimitive Find(string id)
        {
            foreach (var primitive in All)
                if (string.Equals(primitive.Id, id, StringComparison.Ordinal)) return primitive;
            return null;
        }
    }

    public sealed class CommanderDynamicNode
    {
        public string Id { get; }
        public CommanderDynamicPrimitive Primitive { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }
        public IReadOnlyDictionary<string, string> Inputs { get; }
        public IReadOnlyList<string> DependsOn { get; }

        internal CommanderDynamicNode(string id, CommanderDynamicPrimitive primitive,
            Dictionary<string, object> parameters, Dictionary<string, string> inputs,
            List<string> dependsOn)
        {
            Id = id; Primitive = primitive;
            Parameters = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(parameters, StringComparer.Ordinal));
            Inputs = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(inputs, StringComparer.Ordinal));
            DependsOn = Array.AsReadOnly(dependsOn.ToArray());
        }

        public T Parameter<T>(string name) => (T)Parameters[name];
    }

    public sealed class CommanderDynamicPlan
    {
        public const int Version = 1;
        public const int MaximumCharacters = 32768;
        public const int MaximumNodes = 12;
        public const int MaximumReferencesPerNode = 4;
        public const int MaximumDependencyDepth = 5;
        public const int MaximumAggregateCount = 200;
        public const int MaximumIdCharacters = 32;
        public const int MaximumCanonicalIdCharacters = 64;
        public const int MaximumEnumCharacters = 32;
        public IReadOnlyList<CommanderDynamicNode> Nodes { get; }
        public IReadOnlyList<CommanderConstraint> Constraints { get; }

        internal CommanderDynamicPlan(List<CommanderDynamicNode> nodes, IReadOnlyList<CommanderConstraint> constraints = null)
        {
            Nodes = Array.AsReadOnly(nodes.ToArray());
            Constraints = Array.AsReadOnly(new List<CommanderConstraint>(constraints ?? Array.Empty<CommanderConstraint>()).ToArray());
        }
    }
}
