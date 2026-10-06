using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderEconomyClarification")]
    public sealed class CommanderEconomyClarificationTests
    {
        // Catches loss of count/state/source or conversion of an ordinary counted order into a total.
        [Test]
        public void SemanticMappings_PreserveWorkerAndSourceCriteria()
        {
            var fixtures = new[]
            {
                new[] { "SelectedCount", "Exact", "4", "Any", "", "Food", "Any" },
                new[] { "SelectedCount", "Exact", "4", "Idle", "", "Food", "Any" },
                new[] { "SelectedCount", "Exact", "4", "Idle", "", "Food", "Sheep" },
                new[] { "SelectedCount", "Exact", "3", "Gathering", "Wood", "Gold", "Any" },
                new[] { "SelectedCount", "AllMatching", "", "Idle", "", "Food", "Berries" },
                new[] { "Additional", "Exact", "4", "Any", "", "Food", "Any" },
                new[] { "TargetTotal", "Exact", "4", "Any", "", "Food", "Any" }
            };
            foreach (string[] fixture in fixtures)
            {
                var node = AllocationNode(fixture);
                var parsed = CommanderSemanticJson.Parse(Request(node));
                Assert.That(parsed.IsValid, Is.True, node);
                Assert.That(parsed.Nodes[0].Type.ToString(), Is.EqualTo("AllocateWorkers"));
                AssertAllocation(Read(parsed.Nodes[0], "WorkerAllocation"), fixture);
                Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], Context(),
                    out CommanderIntent intent, out string reason), Is.True, reason);
                Assert.That(intent.Type.ToString(), Is.EqualTo("AllocateWorkers"));
                AssertAllocation(Read(intent, "Allocation"), fixture);
                var dto = CommanderIntentDtoCodec.FromIntent(intent);
                var roundtrip = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.Serialize(dto), Context());
                Assert.That(roundtrip.Success, Is.True, roundtrip.Reason);
                AssertAllocation(Read(roundtrip.Intent, "Allocation"), fixture);
            }
        }

        // Catches relaxed fields, invalid combinations, clamping, or concrete provider authority.
        [Test]
        public void InvalidAllocationSchema_RejectsBoundsFieldsAndIncompatibleSources()
        {
            string[] validFixture = { "SelectedCount", "Exact", "4", "Idle", "", "Food", "Sheep" };
            var invalid = new List<string>();
            string valid = AllocationNode(validFixture);
            foreach (string count in new[] { "0", "201", "1.5", "\"four\"", "null" })
                invalid.Add(valid.Replace("\"count\":4", "\"count\":" + count));
            foreach (string field in new[] { "entityId", "workerIds", "coordinates", "producerFromNode", "resultFromNode" })
                invalid.Add(valid.Substring(0, valid.Length - 1) + ",\"" + field + "\":0}");
            invalid.Add(valid.Replace("\"state\":\"Idle\"", "\"state\":\"Idle\",\"unitId\":2"));
            invalid.Add(valid.Replace("\"resource\":\"Food\"", "\"resource\":\"Food\",\"tile\":2"));
            invalid.Add(valid.Replace("\"Sheep\"", "\"Tree\""));
            invalid.Add(valid.Replace("\"Food\"", "\"Gold\""));
            invalid.Add(valid.Replace("\"state\":\"Idle\"", "\"state\":\"Idle\",\"currentResource\":\"Wood\""));
            invalid.Add(valid.Replace("\"SelectedCount\"", "\"TargetTotal\""));
            invalid.Add(valid.Replace("\"Exact\"", "\"AllMatching\""));
            invalid.Add(AllocationNode(new[] { "SelectedCount", "AllMatching", "", "Any", "", "Food", "Any" }));
            foreach (string node in invalid)
                Assert.That(CommanderSemanticJson.Parse(Request(node)).IsValid, Is.False, node);
            string duplicate = Request(AllocationNode(validFixture)).Replace("\"count\":4", "\"count\":4,\"count\":5");
            Assert.That(CommanderSemanticJson.Parse(duplicate).IsValid, Is.False);
            var abovePopulation = CommanderSemanticJson.Parse(Request(AllocationNode(
                new[] { "SelectedCount", "Exact", "21", "Any", "", "Food", "Any" })));
            Assert.That(abovePopulation.IsValid, Is.True);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(abovePopulation.Nodes[0], Context(20), out _, out _), Is.False);
            Assert.That(CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"SetResourceAllocation\",\"resource\":\"Food\",\"count\":0}]}").IsValid, Is.True);
        }

        internal static string AllocationNode(string[] values)
        {
            return "{\"type\":\"AllocateWorkers\",\"mode\":\"" + values[0] + "\",\"countMode\":\"" + values[1]
                + "\"" + (values[2] == "" ? "" : ",\"count\":" + values[2])
                + ",\"workers\":{\"state\":\"" + values[3] + "\""
                + (values[4] == "" ? "" : ",\"currentResource\":\"" + values[4] + "\"")
                + "},\"destination\":{\"resource\":\"" + values[5] + "\",\"sourceKind\":\"" + values[6] + "\"}}";
        }

        internal static string Request(string node) => "{\"outcome\":\"Request\",\"nodes\":[" + node + "]}";
        // Reflection allows this test to fail behaviorally before the new projection types exist.
        private static object Read(object value, string property)
        {
            Assert.That(value, Is.Not.Null);
            PropertyInfo member = value.GetType().GetProperty(property);
            Assert.That(member, Is.Not.Null, property);
            return member.GetValue(value);
        }
        private static void AssertAllocation(object allocation, string[] expected)
        {
            Assert.That(Read(allocation, "Mode").ToString(), Is.EqualTo(expected[0]));
            Assert.That(Read(allocation, "CountMode").ToString(), Is.EqualTo(expected[1]));
            Assert.That(Read(allocation, "Count")?.ToString() ?? "", Is.EqualTo(expected[2]));
            object workers = Read(allocation, "Workers"), destination = Read(allocation, "Destination");
            Assert.That(Read(workers, "State").ToString(), Is.EqualTo(expected[3]));
            Assert.That(Read(workers, "CurrentResource")?.ToString() ?? "", Is.EqualTo(expected[4]));
            Assert.That(Read(destination, "Resource").ToString(), Is.EqualTo(expected[5]));
            Assert.That(Read(destination, "SourceKind").ToString(), Is.EqualTo(expected[6]));
        }
        internal static CommanderContext Context(int maximum = 200) => new CommanderContext(0, 0,
            new CommanderResourceSnapshot(0, 0, 0, 0), 0, maximum, maximum, 1, "English",
            new List<CommanderBuildingSnapshot>(), new List<CommanderUnitSnapshot>(),
            new List<CommanderBuildingSnapshot>(), new List<string>(), new List<CommanderGoalSnapshot>(),
            new List<CommanderVisibleResourceSnapshot>(), new List<CommanderUnitOptionSnapshot>(),
            new List<CommanderWorkerAllocationSnapshot>(), new List<CommanderVisibleEnemyMilitarySnapshot>());
    }
}
