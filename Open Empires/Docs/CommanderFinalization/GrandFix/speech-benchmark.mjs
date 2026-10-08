import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

// Offline measurement only: never changes a transcript or calls any speech/semantic provider.
const numberWords = new Map(['zero','one','two','three','four','five','six','seven','eight','nine','ten',
  'eleven','twelve','thirteen','fourteen','fifteen','sixteen','seventeen','eighteen','nineteen','twenty'].map((word,n)=>[String(n),word]));
numberWords.set('40','forty');
function tokens(text) { return text.toLowerCase().match(/[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*/gu) ?? []; }
function boundedText(text, maximum) { if (typeof text !== 'string' || text.length > maximum) throw new Error('Invalid bounded benchmark text.'); }

export function wordEdits(reference, hypothesis) {
  boundedText(reference,1024); boundedText(hypothesis,4096);
  const wanted=tokens(reference), actual=tokens(hypothesis), width=wanted.length+1;
  const operations=new Uint8Array(width*(actual.length+1));
  let previous=Uint16Array.from({length:width},(_,i)=>i);
  for (let j=1;j<width;j++) operations[j]=2;
  for (let i=1;i<=actual.length;i++) {
    const current=new Uint16Array(width); current[0]=i; operations[i*width]=3;
    for (let j=1;j<width;j++) {
      const mismatch=actual[i-1]!==wanted[j-1];
      let cost=previous[j-1]+(mismatch?1:0), operation=mismatch?1:0;
      if (current[j-1]+1<cost) { cost=current[j-1]+1; operation=2; }
      if (previous[j]+1<cost) { cost=previous[j]+1; operation=3; }
      current[j]=cost; operations[i*width+j]=operation;
    }
    previous=current;
  }
  let i=actual.length,j=wanted.length,substitutions=0,deletions=0,insertions=0;
  while(i||j) {
    const op=operations[i*width+j];
    if(op===2) { deletions++; j--; } else if(op===3) { insertions++; i--; }
    else { if(op===1) substitutions++; i--; j--; }
  }
  return {referenceWords:wanted.length,substitutions,deletions,insertions,errors:substitutions+deletions+insertions};
}

export function criticalPreserved(text, phrases) {
  boundedText(text,4096);
  if(!Array.isArray(phrases)||phrases.length>16) throw new Error('Invalid critical annotation.');
  const actual=tokens(text).map(t=>numberWords.get(t)??t);
  return phrases.every(phrase=>{
    boundedText(phrase,128); const wanted=tokens(phrase).map(t=>numberWords.get(t)??t);
    return wanted.length>0 && actual.some((_,start)=>wanted.every((word,offset)=>actual[start+offset]===word));
  });
}
function timing(values) {
  if(!values.length) return null;
  const sorted=values.toSorted((a,b)=>a-b), n=sorted.length;
  return {samples:n,medianMs:n%2?sorted[(n-1)/2]:(sorted[n/2-1]+sorted[n/2])/2,p95Ms:sorted[Math.ceil(0.95*n)-1]};
}

export function evaluate(corpus, outputs) {
  if(!Array.isArray(corpus)||corpus.length<1||corpus.length>128||!Array.isArray(outputs)||outputs.length>1024)
    throw new Error('Benchmark record bounds exceeded.');
  const cases=new Map();
  for(const clip of corpus) {
    if(!clip||typeof clip.id!=='string'||!/^[a-zA-Z0-9_-]{1,64}$/.test(clip.id)||cases.has(clip.id)
      ||!['speech','silence','noise','music','cancel'].includes(clip.kind)||!['held-out','tuning'].includes(clip.split)
      ||!['human','synthetic','unknown'].includes(clip.source)) throw new Error('Invalid corpus identity/provenance.');
    boundedText(clip.reference,1024);
    if(!criticalPreserved(clip.reference,clip.criticalPhrases)) throw new Error('Critical annotation absent from reference.');
    if(clip.safetyNegation===true&&(!Array.isArray(clip.negationPhrases)||!clip.negationPhrases.length
      ||!criticalPreserved(clip.reference,clip.negationPhrases))) throw new Error('Designated negation annotation required.');
    if(clip.kind!=='speech'&&clip.reference.length!==0) throw new Error('No-speech reference must be empty.');
    cases.set(clip.id,clip);
  }
  const engines=new Map();
  for(const output of outputs) {
    if(!output||typeof output.engine!=='string'||!/^[a-zA-Z0-9_.:-]{1,128}$/.test(output.engine)||!cases.has(output.id))
      throw new Error('Output lacks a corpus/engine identity.');
    boundedText(output.text,4096);
    if(output.releaseToReviewMs!==undefined&&(!Number.isFinite(output.releaseToReviewMs)||output.releaseToReviewMs<0))
      throw new Error('Invalid timing measurement.');
    if(!engines.has(output.engine)) engines.set(output.engine,new Map());
    const byId=engines.get(output.engine);
    if(byId.has(output.id)) throw new Error('Duplicate attempt: retain it as a separate engine/run, never cherry-pick.');
    byId.set(output.id,output);
  }
  if(!engines.size||engines.size>8) throw new Error('No engine results or engine bound exceeded.');
  const summaries=[];
  for(const [engine,byId] of [...engines].toSorted(([a],[b])=>a<b?-1:a>b?1:0)) {
    if(byId.size!==cases.size) throw new Error('All engines must cover the identical corpus, including failures.');
    const summary={engine,samples:cases.size,referenceWords:0,speechWordErrors:0,criticalExact:0,criticalSamples:0,
      safetyNegationFailures:0,noSpeechSamples:0,noSpeechFalseTranscripts:0,submittedNoSpeechOrCancel:0,
      heldOutHumanSpeechSamples:0,wer:null,criticalRate:null,warmReleaseToReview:null,
      qualityClaimEligible:false,physicalOperationallyVerified:false};
    const warm=[];
    for(const [id,clip] of cases) {
      const output=byId.get(id);
      if(output.warm===true&&output.releaseToReviewMs!==undefined) warm.push(output.releaseToReviewMs);
      if(clip.kind==='speech') {
        // Tuning and held-out data are never merged into one apparent held-out score.
        if(clip.split!=='held-out') continue;
        const edits=wordEdits(clip.reference,output.text);
        summary.referenceWords+=edits.referenceWords; summary.speechWordErrors+=edits.errors;
        if(clip.source==='human') summary.heldOutHumanSpeechSamples++;
        const critical=criticalPreserved(output.text,clip.criticalPhrases);
        if(clip.criticalPhrases.length) { summary.criticalSamples++; if(critical) summary.criticalExact++; }
        if(clip.safetyNegation===true&&!criticalPreserved(output.text,clip.negationPhrases)) summary.safetyNegationFailures++;
      } else {
        summary.noSpeechSamples++;
        if(tokens(output.text).length) summary.noSpeechFalseTranscripts++;
        if(output.submitted===true) summary.submittedNoSpeechOrCancel++;
      }
    }
    summary.wer=summary.referenceWords?summary.speechWordErrors/summary.referenceWords:null;
    summary.criticalRate=summary.criticalSamples?summary.criticalExact/summary.criticalSamples:null;
    summary.warmReleaseToReview=timing(warm);
    // Corpus size only permits considering a claim; source consent/version/hardware and comparisons need independent evidence.
    summary.qualityClaimEligible=summary.heldOutHumanSpeechSamples>=40;
    summaries.push(summary);
  }
  return {schema:'openempires-speech-benchmark@1',scoringOnly:true,
    notice:'Not an ASR run, gameplay test, model ranking, physical microphone proof or release certificate. Input measurement provenance must be verified independently.',engines:summaries};
}

function readBounded(file) {
  if(fs.statSync(file).size>8*1024*1024) throw new Error('Benchmark input file exceeds8MiB.');
  return JSON.parse(fs.readFileSync(file,'utf8'));
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href) {
  const options=new Map();
  for(let i=2;i<process.argv.length;i+=2) {
    if(!['--corpus','--results','--out'].includes(process.argv[i])||!process.argv[i+1]||options.has(process.argv[i]))
      throw new Error('Use --corpus FILE --results FILE [--out FILE].');
    options.set(process.argv[i],process.argv[i+1]);
  }
  if(!options.has('--corpus')||!options.has('--results')) throw new Error('Corpus and result files required.');
  const report=evaluate(readBounded(options.get('--corpus')),readBounded(options.get('--results')));
  const serialized=JSON.stringify(report,null,2)+'\n';
  if(options.has('--out')) fs.writeFileSync(options.get('--out'),serialized,'utf8');
  else process.stdout.write(serialized);
}
