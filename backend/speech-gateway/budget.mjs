import fs from 'node:fs';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

export function createBudget(options) {
  const file=path.resolve(options.ledgerPath),maximumOwners=128;
  let state={schema:'openempires-gateway-budget@1',audioSeconds:0,semanticRequests:0,estimatedUsd:0,owners:{},jobs:{}};
  if(fs.existsSync(file)) {
    if(fs.statSync(file).size>65536)throw new Error('BUDGET_STATE_INVALID');
    try{state=JSON.parse(fs.readFileSync(file,'utf8'));}catch{throw new Error('BUDGET_STATE_INVALID');}
    if(!state||state.schema!=='openempires-gateway-budget@1'||!Number.isSafeInteger(state.audioSeconds)||state.audioSeconds<0
      ||!Number.isInteger(state.semanticRequests)||state.semanticRequests<0||!Number.isFinite(state.estimatedUsd)||state.estimatedUsd<0
      ||!state.owners||typeof state.owners!=='object'||Array.isArray(state.owners)||Object.keys(state.owners).length>maximumOwners)throw new Error('BUDGET_STATE_INVALID');
    let audio=0,semantic=0;
    for(const [owner,entry]of Object.entries(state.owners)) {
      if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(owner)||!entry
        ||!Number.isSafeInteger(entry.audioSeconds)||entry.audioSeconds<0||!Number.isSafeInteger(entry.semanticRequests)||entry.semanticRequests<0)
        throw new Error('BUDGET_STATE_INVALID');
      audio+=entry.audioSeconds;semantic+=entry.semanticRequests;
    }
    if(audio!==state.audioSeconds||semantic!==state.semanticRequests)throw new Error('BUDGET_STATE_INVALID');
    state.jobs??={};
    if(typeof state.jobs!=='object'||Array.isArray(state.jobs)||Object.keys(state.jobs).length>options.maxJobRecords)throw new Error('BUDGET_STATE_INVALID');
    for(const [identity,time]of Object.entries(state.jobs)) {
      const [owner,kind,job]=identity.split(':');
      if(!state.owners[owner]||!['audio','semantic'].includes(kind)||!/^[0-9a-f-]{36}$/i.test(job??'')||identity.split(':').length!==3
        ||!Number.isSafeInteger(time)||time<0)throw new Error('BUDGET_STATE_INVALID');
    }
  }
  function save() {
    fs.mkdirSync(path.dirname(file),{recursive:true});
    const temporary=file+'.'+randomUUID()+'.tmp';let fd;
    try {
      fd=fs.openSync(temporary,'wx',0o600);fs.writeFileSync(fd,JSON.stringify(state),'utf8');fs.fsyncSync(fd);fs.closeSync(fd);fd=undefined;
      fs.renameSync(temporary,file);
    }finally{if(fd!==undefined)fs.closeSync(fd);if(fs.existsSync(temporary))fs.unlinkSync(temporary);}
  }
  function prune(now){for(const [identity,time]of Object.entries(state.jobs))if(now-time>=options.jobTtlMs)delete state.jobs[identity];}
  return {hasJob(identity,now){prune(now);return Object.hasOwn(state.jobs,identity);},reserve(owner,kind,audioSeconds=0,jobId,now=Date.now()) {
    prune(now);const identity=owner+':'+kind+':'+jobId;
    if(Object.hasOwn(state.jobs,identity))throw new Error('DUPLICATE_JOB');
    if(Object.keys(state.jobs).length>=options.maxJobRecords)throw new Error('CAPACITY');
    const cost=kind==='audio'?audioSeconds*options.audioUsdPerSecond:options.semanticUsdPerRequest;
    if(!Number.isFinite(cost)||cost<=0||!Number.isFinite(options.maxEstimatedUsd)||options.maxEstimatedUsd<=0)throw new Error('QUOTA_EXHAUSTED');
    let entry=state.owners[owner];
    if(!entry){if(Object.keys(state.owners).length>=maximumOwners)throw new Error('CAPACITY');entry={audioSeconds:0,semanticRequests:0};}
    if(state.estimatedUsd+cost>options.maxEstimatedUsd||kind==='audio'&&(state.audioSeconds+audioSeconds>options.maxAudioSeconds
      ||entry.audioSeconds+audioSeconds>options.maxPerOwnerAudioSeconds)
      ||kind==='semantic'&&(state.semanticRequests>=options.maxSemanticRequests||entry.semanticRequests>=options.maxPerOwnerSemanticRequests))
      throw new Error('QUOTA_EXHAUSTED');
    state.owners[owner]=entry;state.estimatedUsd+=cost;
    state.jobs[identity]=now;
    if(kind==='audio'){state.audioSeconds+=audioSeconds;entry.audioSeconds+=audioSeconds;}
    else {state.semanticRequests++;entry.semanticRequests++;}
    // Durable reservation BEFORE upstream attempt. Never refund an uncertain accepted
    // billable call, reset on restart, or store tokens/audio/transcript/provider secrets.
    save();
    return {audioSeconds:state.audioSeconds,semanticRequests:state.semanticRequests};
  }};
}
