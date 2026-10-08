using System;
using System.IO;
using UnityEngine;

namespace OpenEmpires.TestSupport
{
    public static class EvidenceLivePolicy
    {
        public const string OptInVariable="OPENEMPIRES_GRAND_FIX_LIVE_EVIDENCE";
        public const string ApprovedRun="d1000de8-7e43-44d3-8668-52621bb83f2c";
        public static bool AllowsLive(string value)=>value=="1";
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return AllowsLive(Environment.GetEnvironmentVariable(OptInVariable));
#else
                return false; // The Editor's persistent paid lane is not a player/browser lane.
#endif
            }
        }
        public static EvidenceBudgetJournal OpenApprovedJournal()
        {
            if(!Enabled)throw new InvalidOperationException("Live evidence is not explicitly enabled.");
            string root=Path.GetDirectoryName(Application.dataPath);
            return new EvidenceBudgetJournal(Path.Combine(root,"Docs","CommanderFinalization","GrandFix","paid-usage-ledger.json"),ApprovedRun,false);
        }
        public static BudgetedSpeechTransport OpenApprovedSpeechBenchmark(ICommanderGatewayTransport inner,double minimumSeconds,double quantumSeconds,string verifiedBillingBasis)
        {
            // The production client is not altered. Benchmarks explicitly inject
            // this into the existing consented speech provider's transport slot.
            if(verifiedBillingBasis==null||verifiedBillingBasis.StartsWith("mock-"))
                throw new InvalidOperationException("A real verified operator billing basis is required.");
            return new BudgetedSpeechTransport(inner,OpenApprovedJournal(),minimumSeconds,quantumSeconds,verifiedBillingBasis);
        }
    }
}
