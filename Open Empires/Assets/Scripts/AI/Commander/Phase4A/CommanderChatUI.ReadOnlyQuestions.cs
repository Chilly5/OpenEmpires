using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private async Task<CommanderAIChatSubmission> SubmitReadOnlyQuestionAsync(string message)
        {
            var sim=semanticSimulation;var manager=semanticGoalManager;var provider=semanticProvider;
            if(sim==null||manager==null||manager.IsDisposed||Conversation==null||Conversation.PlayerId!=manager.PlayerId)
            {AppendLine("Commander","Commander observations are not available in this runtime.",false);return null;}
            int generation=runtimeGeneration;
            bool Current()=>this!=null&&runtimeGeneration==generation&&!lifetime.IsCancellationRequested
                &&ReferenceEquals(semanticSimulation,sim)&&ReferenceEquals(semanticGoalManager,manager)
                &&ReferenceEquals(semanticProvider,provider)&&!manager.IsDisposed;
            bool statusQuestion=CommanderTacticalStatusProjection.IsStatusQuestion(message);
            var status=statusQuestion?CommanderTacticalStatusProjection.Observe(sim,manager,message):null;
            TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message),out string facts);
            void LocalFallback(string unavailable)
            {
                if(!Current())return;
                string answer=(statusQuestion?CommanderTacticalStatusProjection.Observe(sim,manager,message).Answer():facts)??unavailable;
                AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
            }
            AppendLine("Player",message,false);
            if(submitting||provider==null)
            {
                string local=status?.Answer()??facts??"Another interpretation is pending; no information request started gameplay.";
                AppendLine("Commander",local,false);Conversation.Memory.RecordExplanation(local);return null;
            }
            // No request ticket, admission, approval, strategy evaluation, planner or
            // cancellation of pending gameplay previews occurs on this branch.
            submitting=true;
            using(var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                cancellation.CancelAfter(semanticProviderTimeout);
                try
                {
                    var request=new CommanderSemanticProviderRequest(message,new CommanderContextBuilder().Build(sim,manager),
                        Conversation.SemanticMemory.Snapshot(),facts,tacticalStatus:status,readOnlyQuestion:true);
                    var task=RequestSemanticTranslation(provider,request,cancellation.Token);
                    _=task.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                    if(await Task.WhenAny(task,Task.Delay(semanticProviderTimeout,cancellation.Token))!=task)
                    {cancellation.Cancel();LocalFallback("The information request timed out; gameplay requests were unchanged.");return null;}
                    var result=await task;
                    if(!Current()||cancellation.IsCancellationRequested)return null;
                    // Reobserve after latency. The model cannot fabricate a tactical
                    // blocker or use an executable answer to acquire authority.
                    if(statusQuestion)status=CommanderTacticalStatusProjection.Observe(sim,manager,message);
                    string answer=status?.Answer()??facts;
                    if(answer==null)
                        answer=result?.IsValid==true&&(result.Outcome==CommanderSemanticOutcome.Answer||result.Outcome==CommanderSemanticOutcome.Clarify||result.Outcome==CommanderSemanticOutcome.Unsupported)
                            ?result.SafeExplanation:"You asked for information; no gameplay order was submitted.";
                    AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
                }
                catch(OperationCanceledException){LocalFallback("The information request was cancelled; gameplay requests were unchanged.");}
                catch(Exception){LocalFallback("I could not observe that safely; no gameplay requests changed.");}
                finally{if(this!=null&&generation==runtimeGeneration)submitting=false;}
            }
            return null;
        }
    }
}
