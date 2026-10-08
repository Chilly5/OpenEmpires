using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenEmpires
{
    // Presentation-only adapters over the already admitted typed plan. No world
    // reads, selection, mutation, authorization or serialization lives here.
    internal static partial class CommanderPlanPreview
    {
        private static string Destination(ResourceType resource,ResourceSourceKind source)
            => resource+(source==ResourceSourceKind.Any?"":" only from "+Words(source.ToString()));
        private static string Words(string value)
            => System.Text.RegularExpressions.Regex.Replace(value??"",@"(?<=[a-z])(?=[A-Z])"," ");
        private static string WorkerDescription(string state,string resource)
            => "eligible "+(state=="Idle"?"idle ":state=="Gathering"?"gathering ":"")+"villagers"
                +(resource!=null?" currently gathering "+resource:"");
        private static string ConstraintDescription(CommanderConstraint constraint)
        {
            if(constraint is ProtectedResourceConstraint floor)
                return "keep at least "+(floor.MinimumWorkers?.ToString()??"the current number of")+" villagers gathering "+floor.Resource;
            if(constraint is PreferredWorkersConstraint workers&&workers.WorkerSource==CommanderPreferredWorkerSource.IdleOnly)
                return "use only idle villagers";
            if(constraint is MaximumQueueConstraint queue)return "at most "+queue.MaximumQueue+" queued units per producer";
            if(constraint is NoConstructionConstraint)return "construction forbidden (new and resumed)";
            if(constraint is ResourceSourceConstraint source)return "preparation source limit: "+Destination(source.Resource,source.SourceKind)+" (when needed)";
            throw new ArgumentException("Unsupported preview constraint.");
        }
        private static string Relation(string value)
            => value=="MapWest"?"map-west (left) of":value=="MapEast"?"map-east (right) of":value=="Near"?"Near":throw new ArgumentException("Unsupported placement relation.");
        private static string Anchor(string value)
            => value=="MyTownCenter"?"your Town Center":value=="MyBarracks"?"your Barracks":value=="WorkedResource"?"your worked resource":throw new ArgumentException("Unsupported placement anchor.");
        private static string UnitName(string canonicalId)
            => KeybindManager.GetUnitTypeDisplayName(int.Parse(canonicalId.Substring("unit:".Length),CultureInfo.InvariantCulture));
        private static string BuildingName(string canonicalId)
            => Words(CommanderIntentCatalog.GetStructureDisplayName((BuildingType)Enum.Parse(typeof(BuildingType),canonicalId.Substring("building:".Length))));
        private static string UnitKind(string kind)
        {
            switch(kind)
            {
                case "Military":return "military units";
                case "DamagedMilitary":return "damaged military units";
                case "Villagers":return "villagers";
                case "Scout":return "Scout";
                case "Spearman":return "Spearman";
                case "Archer":return "Archer";
                case "Knight":return "Knight";
                default:throw new ArgumentException("Unsupported unit selector.");
            }
        }
        private static string Location(CommanderLocationSelector selector)
        {
            switch(selector.Kind)
            {
                case CommanderLocationSelectorKind.PlayerBase:return "a point at your base";
                case CommanderLocationSelectorKind.WorkedResource:return "a point at your worked "+selector.ResourceType+" resource";
                case CommanderLocationSelectorKind.VisibleResource:return "a visible "+selector.ResourceType+" resource point";
                case CommanderLocationSelectorKind.VisibleEnemy:return "a currently visible enemy target";
                case CommanderLocationSelectorKind.RelativeToSelectedUnits:return "the center of the selected units";
                default:throw new ArgumentException("Unsupported action location.");
            }
        }
        private static string DescribeAction(CapabilityActionIntent action,string resultBuildingName)
        {
            string actors=action.UnitSelector.Count+" owned "+(action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType
                ?CommanderIntentCatalog.GetUnitDisplayName(action.UnitSelector.UnitType):UnitKind(action.UnitSelector.Kind.ToString()));
            string location=Location(action.LocationSelector);
            string target=action.TargetSelector.HasValue
                ?action.TargetSelector.Value.Kind==CommanderTargetSelectorKind.UnitType
                    ?"a visible enemy "+CommanderIntentCatalog.GetUnitDisplayName(action.TargetSelector.Value.UnitType)
                    :(action.ActionType==CommanderCapabilityActionType.RepairTarget?"your ":"a visible enemy ")+Words(CommanderIntentCatalog.GetStructureDisplayName(action.TargetSelector.Value.StructureType.Value))
                :action.ActionType==CommanderCapabilityActionType.RepairTarget?"an eligible damaged owned building":location;
            string effect;
            switch(action.ActionType)
            {
                case CommanderCapabilityActionType.MoveUnits:effect="Move "+actors+" to "+location;break;
                case CommanderCapabilityActionType.ScoutArea:effect="Scout by moving "+actors+" to "+location;break;
                case CommanderCapabilityActionType.PatrolArea:effect="Patrol with "+actors+"; point patrol from each starting position to "+location;break;
                case CommanderCapabilityActionType.DefendArea:effect="Move "+actors+" to "+location+" to help defend";break;
                case CommanderCapabilityActionType.RetreatUnits:effect="Retreat "+actors+" to "+location;break;
                case CommanderCapabilityActionType.AttackTarget:effect="Attack "+target+" with "+actors;break;
                case CommanderCapabilityActionType.RepairTarget:effect="Repair "+target+" with "+actors;break;
                case CommanderCapabilityActionType.SetRallyPoint:effect="Set the rally point of "+(resultBuildingName!=null?"the exact "+resultBuildingName:"your "+Words(CommanderIntentCatalog.GetStructureDisplayName(action.StructureType??BuildingType.Barracks)))+" at "+location;break;
                case CommanderCapabilityActionType.ResearchTechnology:effect="Research "+Words(action.Technology?.ToString())+" using an eligible completed research building";break;
                default:throw new ArgumentException("Unsupported preview effect.");
            }
            if(action.TargetSelector.HasValue)effect+="; no unrelated target substitutes (normal gameplay may retarget after dispatch)";
            if(action.LocationSelector.RadiusTiles.HasValue)effect+="; unsupported radius constraint "+action.LocationSelector.RadiusTiles.Value;
            return effect;
        }
        private static string ProducerDescription(CommanderSemanticGraphPlan graph,int index)
        {
            if(!(graph.Nodes[index].Intent is BuildStructureIntent build))throw new ArgumentException("Unsupported producer preview binding.");
            return "only the "+Words(CommanderIntentCatalog.GetStructureDisplayName(build.StructureType))+" built in step "+(index+1)
                +(build.Count>1?" (all "+build.Count+" buildings from that step)":"")+"; no other producer substitutes";
        }
        private static string ResultDescription(CommanderSemanticGraphPlan graph,int index)
        {
            var source=graph.Nodes[index].Intent;
            if(source is EnsureUnitCountIntent units)
            {
                int count=units.NewProductionCount??graph.ProductionExpectations.First(q=>q.NodeIndex==index).NewCount;
                return "only the "+count+" new "+CommanderIntentCatalog.GetUnitDisplayName(units.UnitType)+" produced in step "+(index+1)+"; no existing or human-produced units substitute";
            }
            if(source is BuildStructureIntent build)
                return "only the exact "+Words(CommanderIntentCatalog.GetStructureDisplayName(build.StructureType))+" built in step "+(index+1)+"; no existing building substitutes";
            throw new ArgumentException("Unsupported result preview binding.");
        }
        private static CommanderDynamicNode Source(CommanderSemanticGraphPlan graph,string id)
            => graph.DynamicProgram.Nodes.First(n=>n.Id==id);
        private static string Label(CommanderSemanticGraphPlan graph,string id)
        {
            var effect=graph.Nodes.FirstOrDefault(n=>n.DynamicNodeId==id);
            if(effect!=null)return "step "+(effect.Index+1);
            int index=graph.DynamicProgram.Nodes.ToList().FindIndex(n=>n.Id==id);
            if(index<0)throw new ArgumentException("Unknown preview reference.");
            return (graph.DynamicProgram.Nodes[index].Primitive.Mechanic==CommanderDynamicMechanic.ResolveLocation?"location ":"selection ")+(index+1);
        }
        private static void AppendEffectBindings(StringBuilder text,CommanderSemanticGraphPlan graph,CommanderDynamicNode node)
        {
            if(node.Inputs.TryGetValue("workers",out string workers))
                text.Append("; use only the frozen villagers from ").Append(Label(graph,workers));
            if(node.Inputs.TryGetValue("location",out string location))
                text.Append("; at ").Append(Label(graph,location));
            if(node.Inputs.TryGetValue("producers",out string producers)&&Source(graph,producers).Primitive.Mechanic==CommanderDynamicMechanic.SelectStructures)
                text.Append("; use only the ").Append(BuildingName(Source(graph,producers).Parameter<string>("building")))
                    .Append(" chosen in ").Append(Label(graph,producers)).Append("; no other producer substitutes");
            if(node.DependsOn.Count>0)
                text.Append("; requires ").Append(string.Join(", ",node.DependsOn.Select(id=>Label(graph,id))));
        }
        private static void AppendSelections(StringBuilder text,CommanderSemanticGraphPlan graph)
        {
            text.Append("Selections and placement (fixed for this plan):\n");
            foreach(var node in graph.DynamicProgram.Nodes)
            {
                string description;
                switch(node.Primitive.Mechanic)
                {
                    case CommanderDynamicMechanic.SelectWorkers:
                        string state=node.Parameters.TryGetValue("state",out object s)?(string)s:"Any";
                        string resource=node.Parameters.TryGetValue("currentResource",out object r)?(string)r:null;
                        description="Choose "+node.Parameter<int>("count")+" "+WorkerDescription(state,resource)+" once";break;
                    case CommanderDynamicMechanic.PartitionWorkers:
                        int first=node.Parameter<int>("offset")+1,last=first+node.Parameter<int>("count")-1;
                        description="Use "+(first==last?"villager "+first:"villagers "+first+"–"+last)+" from "+Label(graph,node.Inputs["workers"])+" as a fixed group";break;
                    case CommanderDynamicMechanic.SelectUnits:
                        description="Choose "+node.Parameter<int>("count")+" visible owned "+(node.Parameters.TryGetValue("unit",out object unit)?UnitName((string)unit):UnitKind(node.Parameter<string>("kind")))+" once";break;
                    case CommanderDynamicMechanic.SelectStructures:
                        description="Choose "+node.Parameter<int>("count")+" visible completed owned "+BuildingName(node.Parameter<string>("building"))+" once";break;
                    case CommanderDynamicMechanic.SelectResources:
                        description="Choose "+node.Parameter<int>("count")+" "+(node.Parameter<string>("mode")=="Worked"?"currently worked and visible":"visible")+" "+Destination((ResourceType)Enum.Parse(typeof(ResourceType),node.Parameter<string>("resource")),(ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),node.Parameter<string>("sourceKind")))+" resource anchor(s) once";break;
                    case CommanderDynamicMechanic.ResolveLocation:
                        string anchor=node.Parameters.TryGetValue("anchor",out object a)?Anchor((string)a)
                            :node.Inputs.TryGetValue("structures",out string structure)?"the exact building from "+Label(graph,structure)
                            :"the exact resource from "+Label(graph,node.Inputs["resources"]);
                        description=Relation(node.Parameter<string>("relation"))+" "+anchor+"; "+node.Parameter<int>("clearGapTiles")+"-tile clear gap between footprints within bounded tolerance; report a blocker if no valid site exists";break;
                    case CommanderDynamicMechanic.Build:
                    case CommanderDynamicMechanic.AllocateWorkers:
                    case CommanderDynamicMechanic.Produce:continue; // Fully disclosed by effect lines and input bindings.
                    default:throw new ArgumentException("Unsupported preview selection.");
                }
                text.Append("  ").Append(Label(graph,node.Id)).Append(": ").Append(description);
                if(node.DependsOn.Count>0)text.Append("; requires ").Append(string.Join(", ",node.DependsOn.Select(id=>Label(graph,id))));
                text.Append('\n');
            }
        }
    }
}
