using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Provider-facing schema metadata only. This never validates, compiles or admits a plan.
    internal static class CommanderDynamicProviderVocabulary
    {
        internal static string Build(string serializedContext)
        {
            var context = JObject.Parse(serializedContext);
            var text = new StringBuilder(4096);
            text.Append(" For genuine composition or representation gaps, use the strict envelope ")
                .Append("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[")
                .Append("{\"id\":\"symbol\",\"mechanic\":\"registry-id\",\"parameters\":{},")
                .Append("\"inputs\":{},\"dependsOn\":[]}]}. ")
                .Append("Each node id is only a bounded symbolic reference, never a runtime/entity ID. ")
                .Append("Use exact registry mechanics and their parameter/input names and types: ");
            foreach (CommanderDynamicPrimitive primitive in CommanderDynamicPrimitiveRegistry.All)
            {
                text.Append(primitive.Id).Append(" => ")
                    .Append(primitive.Result).Append('/').Append(primitive.Effect).Append(" parameters[");
                foreach (CommanderDynamicField field in primitive.Parameters)
                    text.Append(field.Name).Append(':').Append(field.Kind)
                        .Append(field.Required ? " required;" : " optional;");
                text.Append("] inputs[");
                foreach (CommanderDynamicInput input in primitive.Inputs)
                    text.Append(input.Role).Append(':').Append(input.Accepts)
                        .Append(input.Required ? " required;" : " optional;");
                text.Append("]; ");
            }
            text.Append("All nodes require parameters, inputs, dependsOn objects/array, including empty ones. ")
                .Append("An optional top-level constraints array (at most four, unique types) reuses NoConstruction {type:NoConstruction}, PreferredWorkers {type:PreferredWorkers,mode:IdleOnly}, MaximumQueue {type:MaximumQueue,amount:integer1..")
                .Append(CommanderIntentValidator.MaximumQueuePolicy)
                .Append("}, ProtectedResource {type:ProtectedResource,resource:Food|Wood|Gold|Stone,amount:integer0..200 optional}. These are semantic restrictions, never authority. NoConstruction forbids new and resumed prerequisites and conflicts with explicit build nodes; do not drop it. ")
                .Append("ResourceSource {type:ResourceSource,resource:Food|Wood|Gold|Stone,sourceKind:compatible source kind} restricts implicit preparation too; do not fall back to another food/resource source. The total shared constraint limit remains four. ")
                .Append("Inputs reference matching typed symbolic nodes and must also appear in dependsOn. ")
                .Append("At most 12 nodes; per-node reference count is inputs plus dependsOn, at most four; root dependency depth zero, maximum depth five; total declared non-partition counts at most 200. ")
                .Append("Count/offset/clearGapTiles are JSON integers, not strings. ")
                .Append("Build count at most 20; selected unit count at most 50; clearGapTiles at most 20 for map directions, and Near requires exactly 1. ")
                .Append("Keep simple known requests in their existing typed Request fast path, including a plain counted Farm build. ")
                .Append("Use DynamicPlan for representation gaps, not just large effect counts: repeated placed construction needs one semantic resolve-location feeding build count greater than one because legacy placed construction represents only one structure. ")
                .Append("When an explicit new producer is requested, connect its exact build result set as the exact producer set in the producers input of produce; use quantityMode New for explicitly new units. Do not split this into an invalid legacy Request or choose unrelated producers. ")
                .Append("For a compound request sharing workers across effects, make one shared worker selection and partition-workers into non-overlapping source-relative ranges; pass those exact partition results to build and allocate-workers. Never independently reselect the same workers for both effects. ")
                .Append("Use only these detached current canonical unit IDs: ");
            if (context["canonicalUnitIds"] is JArray units)
                foreach (JToken unit in units)
                    if (unit.Type == JTokenType.String) text.Append((string)unit).Append(';');
            text.Append(" Detached current canonical building IDs: ");
            if (context["canonicalBuildingIds"] is JArray buildings)
                foreach (JToken building in buildings)
                    if (building.Type == JTokenType.String) text.Append((string)building).Append(';');
            text.Append(" Resource values Food|Wood|Gold|Stone; sourceKind Any|Sheep|Berries|Farm|Tree|GoldMine|StoneMine; ")
                .Append("worker state Any|Idle|Gathering; resource mode Visible|Worked; ")
                .Append("anchor MyTownCenter|MyBarracks; relation Near|MapWest|MapEast; ")
                .Append("quantityMode New|TargetTotal; unit kind Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary. ")
                .Append("No arbitrary mechanics, code, coordinates, hidden state, new strategic choice or unsupported content IDs. ")
                .Append("A DynamicPlan is only untrusted semantic data: game-side validation and player confirmation remain mandatory.\n");
            if (HasId(context, "canonicalBuildingIds", "building:Mill"))
                text.Append("SharedWorkersExample:").Append(SharedWorkersExample().ToString(Formatting.None)).Append('\n');
            if (HasId(context, "canonicalBuildingIds", "building:Barracks") &&
                HasId(context, "canonicalUnitIds", "unit:1"))
                text.Append("ExactProducersExample:").Append(ExactProducersExample().ToString(Formatting.None)).Append('\n');
            return text.ToString();
        }

        private static bool HasId(JObject context, string field, string canonicalId)
        {
            if (!(context[field] is JArray ids)) return false;
            foreach (JToken id in ids)
                if (id.Type == JTokenType.String && (string)id == canonicalId) return true;
            return false;
        }

        private static JObject SharedWorkersExample()
        {
            CommanderDynamicPrimitive workers = Primitive(CommanderDynamicMechanic.SelectWorkers);
            CommanderDynamicPrimitive partition = Primitive(CommanderDynamicMechanic.PartitionWorkers);
            CommanderDynamicPrimitive resources = Primitive(CommanderDynamicMechanic.SelectResources);
            CommanderDynamicPrimitive location = Primitive(CommanderDynamicMechanic.ResolveLocation);
            CommanderDynamicPrimitive build = Primitive(CommanderDynamicMechanic.Build);
            CommanderDynamicPrimitive allocate = Primitive(CommanderDynamicMechanic.AllocateWorkers);
            return Envelope(new JArray
            {
                Node("all", workers, new JObject { [Field(workers, CommanderDynamicFieldKind.Count)] = 3,
                    [Field(workers, CommanderDynamicFieldKind.WorkerState)] = "Idle" }),
                Node("first", partition, new JObject { [Field(partition, CommanderDynamicFieldKind.Offset)] = 0,
                    [Field(partition, CommanderDynamicFieldKind.Count)] = 2 },
                    new JObject { [Input(partition, CommanderDynamicResultKind.WorkerSet)] = "all" }, new JArray("all")),
                Node("third", partition, new JObject { [Field(partition, CommanderDynamicFieldKind.Offset)] = 2,
                    [Field(partition, CommanderDynamicFieldKind.Count)] = 1 },
                    new JObject { [Input(partition, CommanderDynamicResultKind.WorkerSet)] = "all" }, new JArray("all")),
                Node("berries", resources, new JObject { [Field(resources, CommanderDynamicFieldKind.Resource)] = "Food",
                    [Field(resources, CommanderDynamicFieldKind.SourceKind)] = "Berries",
                    [Field(resources, CommanderDynamicFieldKind.ResourceMode)] = "Visible",
                    [Field(resources, CommanderDynamicFieldKind.Count)] = 1 }),
                Node("site", location, new JObject { [Field(location, CommanderDynamicFieldKind.Relation)] = "Near",
                    [Field(location, CommanderDynamicFieldKind.ClearGapTiles)] = 1 },
                    new JObject { [Input(location, CommanderDynamicResultKind.ResourceSet)] = "berries" },
                    new JArray("berries")),
                Node("mill", build, new JObject { [Field(build, CommanderDynamicFieldKind.BuildingId)] = "building:Mill",
                    [Field(build, CommanderDynamicFieldKind.Count)] = 1 },
                    new JObject { [Input(build, CommanderDynamicResultKind.WorkerSet)] = "third",
                        [Input(build, CommanderDynamicResultKind.LocationIntent)] = "site" },
                    new JArray("third", "site")),
                Node("sheep", allocate, new JObject { [Field(allocate, CommanderDynamicFieldKind.Resource)] = "Food",
                    [Field(allocate, CommanderDynamicFieldKind.SourceKind)] = "Sheep" },
                    new JObject { [Input(allocate, CommanderDynamicResultKind.WorkerSet)] = "first" },
                    new JArray("first"))
            });
        }

        private static JObject ExactProducersExample()
        {
            CommanderDynamicPrimitive location = Primitive(CommanderDynamicMechanic.ResolveLocation);
            CommanderDynamicPrimitive build = Primitive(CommanderDynamicMechanic.Build);
            CommanderDynamicPrimitive produce = Primitive(CommanderDynamicMechanic.Produce);
            return Envelope(new JArray
            {
                Node("site", location, new JObject { [Field(location, CommanderDynamicFieldKind.Anchor)] = "MyTownCenter",
                    [Field(location, CommanderDynamicFieldKind.Relation)] = "Near",
                    [Field(location, CommanderDynamicFieldKind.ClearGapTiles)] = 1 }),
                Node("barracks", build, new JObject { [Field(build, CommanderDynamicFieldKind.BuildingId)] = "building:Barracks",
                    [Field(build, CommanderDynamicFieldKind.Count)] = 2 },
                    new JObject { [Input(build, CommanderDynamicResultKind.LocationIntent)] = "site" },
                    new JArray("site")),
                Node("spears", produce, new JObject { [Field(produce, CommanderDynamicFieldKind.UnitId)] = "unit:1",
                    [Field(produce, CommanderDynamicFieldKind.Count)] = 10,
                    [Field(produce, CommanderDynamicFieldKind.QuantityMode)] = "New" },
                    new JObject { [Input(produce, CommanderDynamicResultKind.StructureSet)] = "barracks" },
                    new JArray("barracks"))
            });
        }

        private static JObject Envelope(JArray nodes) => new JObject
        {
            ["outcome"] = "DynamicPlan", ["version"] = 1, ["nodes"] = nodes
        };

        private static JObject Node(string id, CommanderDynamicPrimitive primitive, JObject parameters,
            JObject inputs = null, JArray dependsOn = null) => new JObject
        {
            ["id"] = id, ["mechanic"] = primitive.Id, ["parameters"] = parameters,
            ["inputs"] = inputs ?? new JObject(), ["dependsOn"] = dependsOn ?? new JArray()
        };

        private static CommanderDynamicPrimitive Primitive(CommanderDynamicMechanic mechanic)
        {
            foreach (CommanderDynamicPrimitive primitive in CommanderDynamicPrimitiveRegistry.All)
                if (primitive.Mechanic == mechanic) return primitive;
            throw new InvalidOperationException("Missing dynamic primitive metadata.");
        }

        private static string Field(CommanderDynamicPrimitive primitive, CommanderDynamicFieldKind kind)
        {
            foreach (CommanderDynamicField field in primitive.Parameters)
                if (field.Kind == kind) return field.Name;
            throw new InvalidOperationException("Missing dynamic field metadata.");
        }

        private static string Input(CommanderDynamicPrimitive primitive, CommanderDynamicResultKind kind)
        {
            foreach (CommanderDynamicInput input in primitive.Inputs)
                if (input.Accepts == kind) return input.Role;
            throw new InvalidOperationException("Missing dynamic input metadata.");
        }
    }
}
