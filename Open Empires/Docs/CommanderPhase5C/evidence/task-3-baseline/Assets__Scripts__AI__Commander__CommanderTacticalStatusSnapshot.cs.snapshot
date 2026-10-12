using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Detached observation only: no runtime objects, commands, delegates or IDs.
    public sealed class CommanderTacticalStepSnapshot
    {
        public string Label { get; }
        public string State { get; }
        public string Reason { get; }
        public int Requested { get; }
        public int? RequestedNew { get; }
        public int Owned { get; }
        public int Produced { get; }
        public int Queued { get; }
        public int AttributedQueued { get; }
        public int Pending { get; }
        public int ProducerReady { get; }
        public int ProducerUnfinished { get; }
        public int UnmetDependencies { get; }
        public bool FailedDependency { get; }
        public bool HumanOverride { get; }
        public bool? TargetAvailable { get; }
        public KnowledgeCost Deficits { get; }
        public int ReservedWorkers { get; }
        public int ProtectedWorkers { get; }
        public int LastPlannerTick { get; }

        internal CommanderTacticalStepSnapshot(string label,string state,string reason,int requested,int? requestedNew,
            int owned,int produced,int queued,int attributedQueued,int pending,int ready,int unfinished,
            int dependencies,bool failed,bool takeover,bool? target,KnowledgeCost deficits,int reserved,int protectedWorkers,int lastTick)
        { Label=label;State=state;Reason=reason;Requested=requested;RequestedNew=requestedNew;Owned=owned;Produced=produced;
            Queued=queued;AttributedQueued=attributedQueued;Pending=pending;ProducerReady=ready;ProducerUnfinished=unfinished;
            UnmetDependencies=dependencies;FailedDependency=failed;HumanOverride=takeover;TargetAvailable=target;Deficits=deficits;
            ReservedWorkers=reserved;ProtectedWorkers=protectedWorkers;LastPlannerTick=lastTick; }

        internal JObject Json() => new JObject {
            ["label"]=Label,["state"]=State,["reason"]=Reason,["requested"]=Requested,["requestedNew"]=RequestedNew,
            ["owned"]=Owned,["produced"]=Produced,["queued"]=Queued,["attributableQueued"]=AttributedQueued,["pending"]=Pending,
            ["producerReady"]=ProducerReady,["producerUnfinished"]=ProducerUnfinished,["unmetDependencies"]=UnmetDependencies,
            ["failedDependency"]=FailedDependency,["humanOverride"]=HumanOverride,["targetAvailable"]=TargetAvailable,
            ["deficits"]=Deficits==null?JValue.CreateNull():JObject.FromObject(Deficits),["reservedWorkers"]=ReservedWorkers,
            ["protectedWorkers"]=ProtectedWorkers,["lastPlannerTick"]=LastPlannerTick };
    }

    public sealed class CommanderTacticalRequestSnapshot
    {
        public string Label { get; }
        public IReadOnlyList<CommanderTacticalStepSnapshot> Steps { get; }
        internal CommanderTacticalRequestSnapshot(string label,IEnumerable<CommanderTacticalStepSnapshot> steps)
        { Label=label;Steps=Array.AsReadOnly(steps.ToArray()); }
    }

    public sealed class CommanderTacticalStatusSnapshot
    {
        public const int MaximumCharacters=1536;
        public string Selection { get; }
        public int Tick { get; }
        public int Population { get; }
        public int PopulationCap { get; }
        public int QueuedPopulation { get; }
        public int MaximumPopulation { get; }
        public bool Truncated { get; }
        public IReadOnlyList<CommanderTacticalRequestSnapshot> Requests { get; }
        internal CommanderTacticalStatusSnapshot(int tick,int population,int cap,int queued,int max,string selection,
            IEnumerable<CommanderTacticalRequestSnapshot> requests,bool truncated)
        { Tick=tick;Population=population;PopulationCap=cap;QueuedPopulation=queued;MaximumPopulation=max;Selection=selection;
            Requests=Array.AsReadOnly(requests.ToArray());Truncated=truncated; }

        internal JObject Json()
        {
            var root=new JObject{["selection"]=Selection,["tick"]=Tick,["population"]=Population,["populationCap"]=PopulationCap,
                ["queuedPopulation"]=QueuedPopulation,["maximumPopulation"]=MaximumPopulation,["truncated"]=Truncated,["requests"]=new JArray()};
            var requests=(JArray)root["requests"];
            foreach(var request in Requests)
            {
                var item=new JObject{["label"]=request.Label,["steps"]=new JArray()};requests.Add(item);
                foreach(var step in request.Steps)
                {
                    var steps=(JArray)item["steps"];steps.Add(step.Json());
                    if(root.ToString(Formatting.None).Length>MaximumCharacters)
                    { steps.RemoveAt(steps.Count-1);root["truncated"]=true;break; }
                }
                if(((JArray)item["steps"]).Count==0)
                {requests.RemoveAt(requests.Count-1);root["truncated"]=true;continue;}
                if(root.ToString(Formatting.None).Length>MaximumCharacters)
                { requests.RemoveAt(requests.Count-1);root["truncated"]=true;break; }
            }
            return root;
        }

        internal string Answer()
        {
            if(Selection=="None")return "No matching accepted Commander request is recorded. An unapproved preview has not started.";
            if(Selection=="Ambiguous")return "Which request do you mean: "+string.Join("; ",Requests.Select(r=>r.Label))
                +"? Ask about the unit or building and count. No orders were changed."+(Truncated?" More requests are omitted.":"");
            var request=Requests[0];var step=request.Steps[0];
            string counts=step.RequestedNew.HasValue?$"{step.Produced}/{step.RequestedNew.Value} new completed, {step.AttributedQueued+step.Pending} attributable queued or in flight. "
                : step.Requested>0?$"Owned {step.Owned}; requested {step.Requested} total/count; queued {step.Queued}. ":"";
            string reason=string.IsNullOrEmpty(step.Reason)?"No status reason has been recorded yet.":"Reported status reason: "+step.Reason;
            return request.Label+": "+counts+reason+(step.HumanOverride?" Human control is protected.":"")
                +(Truncated?" Some steps are omitted.":"");
        }
    }

    internal static class CommanderTacticalStatusProjection
    {
        internal static bool IsStatusQuestion(string input) => Regex.IsMatch(input??"",
            @"\b(status|progress|progressing|doing|waiting|finished|ready|blocked|stuck|request|army)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        internal static bool IsInformationQuestion(string input)
        {
            string text=(input??"").Trim();
            if(Regex.IsMatch(text,@"^(why|what|how|when|where|which|who|is|are|did|does|have|has)\b|^(tell me|explain|show me|report|i want to know|can my civilization|status|progress|current strategy status|strategy status)\b",
                RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
            if(Regex.IsMatch(text,@"^can (i|we) (make|train|build) (?:a |an )?[a-z]+\s*[?.!]*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
            if(Regex.IsMatch(text,@"^(can|could|would|will) you (?:(tell|show|remind) me\b|(?:explain|report)\b)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
            return text.EndsWith("?",StringComparison.Ordinal)
                && !Regex.IsMatch(text,@"^(can|could|would|will) (you|we|i)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
                && !SimpleTextIntentParser.IsExplicitOrderForm(text);
        }

        internal static CommanderTacticalStatusSnapshot Observe(GameSimulation sim,CommanderGoalManager manager,string query)
        {
            var catalog=GameKnowledgeCatalog.Build(sim);
            var all=manager.ActiveGoals.Concat(manager.ArchivedGoals.Reverse().Take(CommanderGoalManager.MaxArchivedGoals))
                .Where(g=>g.PlayerId==manager.PlayerId&&ReferenceEquals(g.RuntimeOwner,manager)).ToList();
            bool HasMention(CommanderGoal goal)
            {
                if(goal is EnsureUnitCountGoal unit)return UnitMention(unit.RequestedUnitType);
                if(goal is BuildStructureGoal build)return BuildingMention(build.StructureType);
                if(goal is CommanderCapabilityGoal action)
                    return action.Action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType&&UnitMention(action.Action.UnitSelector.UnitType)
                        || action.Action.TargetSelector.HasValue&&(action.Action.TargetSelector.Value.Kind==CommanderTargetSelectorKind.UnitType
                            ?UnitMention(action.Action.TargetSelector.Value.UnitType):BuildingMention(action.Action.TargetSelector.Value.StructureType.Value));
                return false;
            }
            bool UnitMention(int type)
            {
                var record=catalog.FindUnitByType(type);var actual=catalog.FindUnitByType(sim.ResolveCivUnitType(manager.PlayerId,type));
                return Mention(query,CommanderIntentCatalog.GetUnitDisplayName(type))||Mention(query,CommanderIntentCatalog.GetUnitDisplayName(type,true))
                    || record!=null&&(Mention(query,record.DisplayName)||record.Aliases.Any(a=>Mention(query,a)))
                    || actual!=null&&(Mention(query,actual.DisplayName)||actual.Aliases.Any(a=>Mention(query,a)));
            }
            bool BuildingMention(BuildingType type)
            { var record=catalog.FindBuilding(type.ToString());return Mention(query,type.ToString())||record!=null&&(Mention(query,record.DisplayName)||record.Aliases.Any(a=>Mention(query,a))); }
            // Detect an explicit content reference independently of current goals;
            // absence is not permission to answer about a different request.
            bool specific=catalog.Units.Any(u=>UnitMention(u.UnitType))||catalog.Buildings.Any(b=>BuildingMention(b.BuildingType));
            var groups=all.GroupBy(g=>(object)g.RequestAuthority??g).Where(g=>!specific||g.Any(HasMention)).ToList();
            // A count attached to a named subject disambiguates otherwise identical
            // owned requests. This is selection for observation, never authority.
            var counts=Regex.Matches(query??"",@"\b[0-9]{1,3}\b").Cast<Match>()
                .Select(m=>int.Parse(m.Value,System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            if(specific&&counts.Count>0)
                groups=groups.Where(g=>g.Any(s=>HasMention(s)&&(s is EnsureUnitCountGoal u
                    ?counts.Contains(u.IsExplicitNewProduction?u.RequiredNewProductionCount:u.TargetTotal)
                    :s is BuildStructureGoal b?counts.Contains(b.Count):s is CommanderCapabilityGoal c&&counts.Contains(c.Action.UnitSelector.Count)))).ToList();
            var active=groups.Where(g=>g.Any(s=>!s.IsTerminal)).ToList();
            if(active.Count>0)groups=active;else groups=groups.OrderByDescending(g=>g.Max(s=>s.CreatedTick)).Take(1).ToList();
            var selected=groups.OrderBy(g=>g.Min(s=>s.CreatedTick)).ThenBy(g=>g.Min(s=>s.GoalId)).Take(3).ToList();
            var requests=new List<CommanderTacticalRequestSnapshot>();bool truncated=groups.Count>selected.Count;
            foreach(var group in selected)
            {
                var ordered=group.OrderBy(g=>specific&&HasMention(g)?0:1)
                    .ThenBy(g=>g.IsTerminal?2:g.Status==CommanderGoalStatus.Blocked||g.Status==CommanderGoalStatus.Failed?0:1)
                    .ThenBy(g=>g.RequestNodeIndex).ThenBy(g=>g.GoalId).ToList();
                var steps=ordered.Take(3).Select(g=>Step(sim,manager,g)).ToArray();
                truncated|=ordered.Count>steps.Length;
                requests.Add(new CommanderTacticalRequestSnapshot(string.Join(" / ",steps.Select(s=>s.Label).Distinct()).Substring(0,
                    Math.Min(120,string.Join(" / ",steps.Select(s=>s.Label).Distinct()).Length)),steps));
            }
            int queued=sim.BuildingRegistry.GetAllBuildings().Where(b=>b.PlayerId==manager.PlayerId&&!b.IsDestroyed).Sum(b=>b.TrainingQueue.Count)
                +sim.CountPendingTrainingOrders(playerId:manager.PlayerId);
            return new CommanderTacticalStatusSnapshot(sim.CurrentTick,sim.GetPopulation(manager.PlayerId),sim.GetPopulationCap(manager.PlayerId),queued,
                sim.Config.MaxPopulation,groups.Count==0?"None":groups.Count==1?"One":"Ambiguous",requests,truncated);
        }

        private static bool Mention(string input,string term)=>!string.IsNullOrWhiteSpace(term)&&Regex.IsMatch(input??"",@"(?<!\w)"+Regex.Escape(term)+@"(?!\w)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        private static string Reason(string value)
        {
            string text=Regex.Replace(value??"",@"#\d+|\b(?:goal|building|unit|worker|producer)\s+(?:id\s*)?\d+\b","the resolved entity",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            text=Regex.Replace(text,@"\(\s*-?\d+\s*,\s*-?\d+\s*\)","the resolved location");
            text=Regex.Replace(text,@"\s+"," ").Trim();return text.Length<=160?text:text.Substring(0,157)+"...";
        }
        private static CommanderTacticalStepSnapshot Step(GameSimulation sim,CommanderGoalManager manager,CommanderGoal goal)
        {
            int owned=goal.LastObservedOwnedCount,produced=0,queued=goal.LastObservedQueuedCount,attributed=0,pending=0,ready=0,unfinished=0,requested=0;
            int? newCount=null;KnowledgeCost deficit=null;bool takeover=goal.FrozenWorkerHumanOverride;bool? target=null;
            string label=goal.GoalType.ToString(),state=goal.Status.ToString(),reason=Reason(goal.StatusReason);
            if(goal is EnsureUnitCountGoal units)
            {
                requested=units.TargetTotal;newCount=units.IsExplicitNewProduction||units.HasResultConsumer?units.RequiredNewProductionCount:(int?)null;
                int resolved=sim.ResolveCivUnitType(goal.PlayerId,units.RequestedUnitType);
                label=(units.IsExplicitNewProduction?units.RequiredNewProductionCount+" new ":units.TargetTotal+" total ")+KeybindManager.GetUnitTypeDisplayName(resolved);
                owned=CommanderProductionProjection.LivingOwnedIds(sim,goal.PlayerId,resolved).Count;
                produced=units.AttributedUnitIds.Count(id=>sim.UnitRegistry.GetUnit(id) is UnitData u&&u.PlayerId==goal.PlayerId&&u.CurrentHealth>0&&u.State!=UnitState.Dead);
                pending=sim.CountPendingTrainingOrders(issuer:goal);
                attributed=units.TrackedTrainingOrders.Count(r=>r.IsQueued&&!r.IsCancelled);
                var buildings=sim.BuildingRegistry.GetAllBuildings().Where(b=>b.PlayerId==goal.PlayerId&&!b.IsDestroyed).ToList();
                queued=buildings.Sum(b=>b.TrainingQueue.Count(t=>t==resolved))+sim.CountPendingTrainingOrders(playerId:goal.PlayerId,resolvedType:resolved);
                var exactProducerIds=units.RequiredProducerGoal==null?null:new HashSet<int>(units.RequiredProducerGoal.ResultBuildingIds);
                if(exactProducerIds!=null)
                {exactProducerIds.UnionWith(units.RequiredProducerGoal.AttributedBuildingIds);if(units.RequiredProducerGoal.PlacedBuildingId>=0)exactProducerIds.Add(units.RequiredProducerGoal.PlacedBuildingId);}
                var producers=buildings.Where(b=>sim.IsCompatibleProductionBuilding(goal.PlayerId,b,units.RequestedUnitType)
                    &&(exactProducerIds==null||exactProducerIds.Contains(b.Id))
                    &&(units.BoundProducerBuildingIds==null||units.BoundProducerBuildingIds.Contains(b.Id))).OrderBy(b=>b.Id).ToList();
                ready=producers.Count(b=>!b.IsUnderConstruction);unfinished=producers.Count-ready;
                var producer=producers.FirstOrDefault(b=>!b.IsUnderConstruction);
                if(producer!=null)
                { sim.GetUnitTrainingCosts(producer,units.RequestedUnitType,out int food,out int wood,out int gold);var stock=sim.ResourceManager.GetPlayerResources(goal.PlayerId);
                    deficit=new KnowledgeCost(Math.Max(0,food-stock.Food),Math.Max(0,wood-stock.Wood),Math.Max(0,gold-stock.Gold)); }
            }
            else if(goal is BuildStructureGoal building)
            { requested=building.Count;label="Build "+building.Count+" "+CommanderIntentCatalog.GetStructureDisplayName(building.StructureType);
                produced=building.AttributedBuildingIds.Count(id=>sim.BuildingRegistry.GetBuilding(id) is BuildingData b&&b.PlayerId==goal.PlayerId&&!b.IsDestroyed&&!b.IsUnderConstruction); }
            else if(goal is AllocateWorkersGoal workers){label="Workers to "+workers.Allocation.Destination.Resource;requested=workers.Allocation.Count??workers.SelectedWorkerIds.Count;takeover|=workers.HumanInterrupted;}
            else if(goal is ResourceAllocationGoal allocation){label="Workers to "+allocation.Resource;requested=allocation.TargetWorkers;}
            else if(goal is ReachAgeGoal age){label="Reach age "+age.TargetAge;requested=age.TargetAge;}
            else if(goal is CommanderCapabilityGoal capability)
            {
                label=capability.Action.ActionType.ToString();requested=capability.Action.UnitSelector.Count;takeover|=capability.ResultHumanOverride;
                if(capability.Action.ActionType==CommanderCapabilityActionType.AttackTarget)
                {
                    UnitData enemy=capability.TargetBinding?.Unit;
                    BuildingData enemyBuilding=capability.TargetBinding?.Building;
                    if(enemy==null&&capability.IssuedCommand is AttackUnitCommand attack)enemy=sim.UnitRegistry.GetUnit(attack.TargetUnitId);
                    if(enemyBuilding==null&&capability.IssuedCommand is AttackBuildingCommand attackBuilding)enemyBuilding=sim.BuildingRegistry.GetBuilding(attackBuilding.TargetBuildingId);
                    bool observed=false;
                    if(enemy!=null){var tile=sim.MapData.WorldToTile(enemy.SimPosition);observed=sim.FogOfWar.GetVisibility(goal.PlayerId,tile.x,tile.y)==TileVisibility.Visible;}
                    else if(enemyBuilding!=null)observed=sim.FogOfWar.GetVisibility(goal.PlayerId,enemyBuilding.OriginTileX,enemyBuilding.OriginTileZ)==TileVisibility.Visible;
                    if(!observed){target=false;reason="The requested enemy target is not currently observable.";state="TargetUnavailable";}
                }
                if(capability.TargetBinding!=null)
                {
                    var binding=capability.TargetBinding;
                    if(binding.Unit!=null)
                    { var tile=sim.MapData.WorldToTile(binding.Unit.SimPosition);if(sim.FogOfWar.GetVisibility(goal.PlayerId,tile.x,tile.y)!=TileVisibility.Visible)
                        {target=false;reason="The requested enemy target is not currently visible.";state="TargetUnavailable";} }
                    if(!target.HasValue)target=new CommanderCapabilityExecutor(sim).IsTargetCurrent(capability.Action,binding);
                }
            }
            if(manager.ObserveGoalSuspended(goal.GoalId))
            {state="Paused";reason="Execution of this accepted request is suspended; no planning occurs while suspended.";}
            int reserved=0,protectedWorkers=0;
            foreach(var unit in sim.UnitRegistry.GetAllUnits())if(unit.PlayerId==goal.PlayerId&&unit.IsVillager&&unit.CurrentHealth>0&&unit.State!=UnitState.Dead)
            { if(manager.GetWorkerReservation(unit.Id)?.GoalId==goal.GoalId)reserved++;if(manager.ObserveHumanProtection(unit.Id))protectedWorkers++; }
            return new CommanderTacticalStepSnapshot(label,state,reason,requested,newCount,owned,produced,queued,attributed,pending,ready,unfinished,
                goal.Dependencies.Count(g=>g.Status!=CommanderGoalStatus.Completed),goal.Dependencies.Any(g=>g.Status==CommanderGoalStatus.Failed||g.Status==CommanderGoalStatus.Cancelled),
                takeover,target,deficit,reserved,protectedWorkers,goal.LastPlannerObservationTick);
        }
    }
}
