import { test } from 'node:test';
import assert from 'node:assert/strict';

async function scorer() { return import('./speech-benchmark.mjs'); }
test('word edits count substitutions, deletions and insertions independently', async () => {
  const { wordEdits } = await scorer();
  assert.deepEqual(wordEdits('make four spearmen', 'make fourteen archers'), { referenceWords:3, substitutions:2, deletions:0, insertions:0, errors:2 });
  assert.deepEqual(wordEdits('make four spearmen', 'make spearmen'), { referenceWords:3, substitutions:0, deletions:1, insertions:0, errors:1 });
  assert.deepEqual(wordEdits('make four spearmen', 'make four new spearmen'), { referenceWords:3, substitutions:0, deletions:0, insertions:1, errors:1 });
});
test('critical count and negation scoring never repairs recognition text', async () => {
  const { criticalPreserved } = await scorer();
  assert.equal(criticalPreserved('make 4 more spearmen; do not build', ['four more','do not']), true);
  assert.equal(criticalPreserved('make forty spearmen and build', ['four','do not']), false);
  assert.equal(criticalPreserved('gather wood', ['food']), false);
  assert.equal(criticalPreserved('put fourteen villagers on gold', ['four','gold']), false);
});
test('weighted WER, safety losses and silence transcripts are explicit', async () => {
  const { evaluate } = await scorer();
  const corpus = [
    {id:'a',reference:'make four spearmen',criticalPhrases:['four','spearmen'],kind:'speech',split:'held-out',source:'synthetic'},
    {id:'b',reference:'do not build',criticalPhrases:['do not'],negationPhrases:['do not'],kind:'speech',split:'held-out',source:'synthetic',safetyNegation:true},
    {id:'c',reference:'',criticalPhrases:[],kind:'silence',split:'held-out',source:'synthetic'}
  ];
  const output = corpus.map((c,i)=>({id:c.id,engine:'tiny-fixture',text:['make fourteen spearmen','build','make four spearmen'][i],warm:true,releaseToReviewMs:[100,200,500][i]}));
  const report = evaluate(corpus,output).engines[0];
  assert.equal(report.speechWordErrors,3); assert.equal(report.referenceWords,6); assert.equal(report.wer,0.5);
  assert.equal(report.criticalExact,0); assert.equal(report.criticalSamples,2);
  assert.equal(report.safetyNegationFailures,1); assert.equal(report.noSpeechFalseTranscripts,1);
  assert.equal(report.warmReleaseToReview.medianMs,200); assert.equal(report.warmReleaseToReview.p95Ms,500);
  assert.equal(report.qualityClaimEligible,false); assert.equal(report.physicalOperationallyVerified,false);
});
test('engine comparisons require exactly the same corpus, not cherry-picked successful attempts', async () => {
  const { evaluate } = await scorer();
  const corpus=[{id:'one',reference:'food',criticalPhrases:['food'],kind:'speech',split:'held-out',source:'human'}];
  assert.throws(()=>evaluate(corpus,[{id:'one',engine:'baseline',text:'food'},{id:'missing',engine:'candidate',text:'food'}]));
  assert.throws(()=>evaluate(corpus,[{id:'one',engine:'baseline',text:'food'},{id:'one',engine:'baseline',text:'food'}]));
});
test('small corpus and absent timing remain unverified rather than zero-latency success', async () => {
  const { evaluate } = await scorer();
  const report=evaluate([{id:'one',reference:'gold',criticalPhrases:['gold'],kind:'speech',split:'held-out',source:'human'}],
    [{id:'one',engine:'candidate',text:'gold'}]).engines[0];
  assert.equal(report.wer,0); assert.equal(report.warmReleaseToReview,null);
  assert.equal(report.qualityClaimEligible,false); assert.equal(report.physicalOperationallyVerified,false);
});
test('unbounded or malformed metadata is rejected before scoring', async () => {
  const { evaluate } = await scorer();
  assert.throws(()=>evaluate([{id:'one',reference:'x'.repeat(1025),criticalPhrases:[],kind:'speech',split:'held-out',source:'human'}],
    [{id:'one',engine:'candidate',text:'gold'}]));
});

test('missing unit is not falsely reported as a dropped negation', async () => {
  const { evaluate } = await scorer();
  const report=evaluate([{id:'one',reference:'do not build house',criticalPhrases:['do not','house'],
    negationPhrases:['do not'],safetyNegation:true,kind:'speech',split:'held-out',source:'synthetic'}],
    [{id:'one',engine:'candidate',text:'do not build barracks'}]).engines[0];
  assert.equal(report.criticalExact,0); assert.equal(report.safetyNegationFailures,0);
});
test('critical annotations must occur in reference and designated negations must be explicit', async () => {
  const { evaluate } = await scorer();
  const base={id:'one',reference:'do not build house',criticalPhrases:['do not','house'],kind:'speech',split:'held-out',source:'synthetic'};
  const output=[{id:'one',engine:'candidate',text:'do not build house'}];
  assert.throws(()=>evaluate([{...base,criticalPhrases:['archers']}],output));
  assert.throws(()=>evaluate([{...base,safetyNegation:true}],output));
});
