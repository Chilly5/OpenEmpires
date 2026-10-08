using System.Linq;

namespace OpenEmpires.TestSupport
{
    public static class EvidenceSemanticGate
    {
        public static bool HasOnlyStructureEffect(CommanderSemanticResult result,BuildingType type,int count)
        {
            if(result==null||!result.IsValid||count<1)return false;
            if(result.Outcome==CommanderSemanticOutcome.Request)
                return result.Nodes.Count==1&&result.Nodes[0].Type==CommanderSemanticNodeType.BuildStructure
                    &&result.Nodes[0].BuildingType==type&&result.Nodes[0].Count==count;
            if(result.Outcome!=CommanderSemanticOutcome.DynamicPlan||result.DynamicPlan==null)return false;
            var effects=result.DynamicPlan.Nodes.Where(n=>n.Primitive.Effect==CommanderDynamicEffectClass.Construction
                ||n.Primitive.Effect==CommanderDynamicEffectClass.Allocation||n.Primitive.Effect==CommanderDynamicEffectClass.Production).ToArray();
            return effects.Length==1&&effects[0].Primitive.Mechanic==CommanderDynamicMechanic.Build
                &&effects[0].Parameter<string>("building")=="building:"+type&&effects[0].Parameter<int>("count")==count;
        }
    }
}
