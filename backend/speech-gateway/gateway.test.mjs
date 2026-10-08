import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const owner='11111111-1111-4111-8111-111111111111';
const job='22222222-2222-4222-8222-222222222222';
function wav(seconds=1) {
  const bytes=Buffer.alloc(44+Math.round(16000*seconds)*2);
  bytes.write('RIFF');bytes.writeUInt32LE(bytes.length-8,4);bytes.write('WAVEfmt ',8);
  bytes.writeUInt32LE(16,16);bytes.writeUInt16LE(1,20);bytes.writeUInt16LE(1,22);bytes.writeUInt32LE(16000,24);
  bytes.writeUInt32LE(32000,28);bytes.writeUInt16LE(2,32);bytes.writeUInt16LE(16,34);bytes.write('data',36);bytes.writeUInt32LE(bytes.length-44,40);
  return bytes;
}
async function fixture(overrides={}) {
  const {createGateway}=await import('./gateway.mjs');
  const directory=overrides.directory??fs.mkdtempSync(path.join(os.tmpdir(),'OpenEmpires-gateway-'));
  let calls=0;
  const service=createGateway({
    authenticate:async token=>token==='fixture-token'?owner:null,
    transcribe:async()=>{calls++;return 'make four spearmen';},
    semantic:async()=>{calls++;return {choices:[{finish_reason:'stop',message:{content:'{"outcome":"Answer","message":"fixture"}'}}]};},
    policy:{version:'fixture-policy',provider:'fixture',model:'fixture-model',retention:'Test adapter; no real vendor processing'},
    allowedOrigins:['https://game.example.invalid'],
    authorizedOwners:[owner],maxAudioSeconds:10,maxSemanticRequests:6,maxPerOwnerAudioSeconds:5,
    maxEstimatedUsd:1,audioUsdPerSecond:0.000075,semanticUsdPerRequest:0.01,
    ledgerPath:path.join(directory,'budget.json'),...overrides
  });
  await new Promise(resolve=>service.listen(0,'127.0.0.1',resolve));
  const base='http://127.0.0.1:'+service.address().port;
  return {base,calls:()=>calls,service,directory,async close(){await new Promise(resolve=>service.close(resolve));
    const resolved=path.resolve(directory);if(path.dirname(resolved)===path.resolve(os.tmpdir())&&path.basename(resolved).startsWith('OpenEmpires-gateway-'))fs.rmSync(resolved,{recursive:true,force:true});}};
}
const headers={Authorization:'Bearer fixture-token',Origin:'https://game.example.invalid','Content-Type':'audio/wav',
  'X-Voice-Job':job,'X-Voice-Consent':'fixture-policy','X-Voice-Language':'en'};
test('authenticated consented bounded WAV returns only correlated transcript data',async()=>{
  const f=await fixture();try{
    const response=await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body:wav()});
    assert.equal(response.status,200);const body=await response.json();
    assert.deepEqual(body,{jobId:job,policyVersion:'fixture-policy',text:'make four spearmen'});assert.equal(f.calls(),1);
  }finally{await f.close();}
});
test('no auth, wrong origin and missing/old consent reject before transcription',async()=>{
  const f=await fixture();try{
    for(const change of [{Authorization:''},{Origin:'https://evil.example.invalid'},{'X-Voice-Consent':''},{'X-Voice-Consent':'old'}]){
      const response=await fetch(f.base+'/api/commander/stt',{method:'POST',headers:{...headers,...change},body:wav()});
      assert.ok([401,403,409].includes(response.status));
    }assert.equal(f.calls(),0);
  }finally{await f.close();}
});
test('malformed container, invalid decoded duration and oversized upload never reach provider',async()=>{
  const f=await fixture();try{
    for(const body of [Buffer.from('not WAV'),wav(61),Buffer.alloc(2*1024*1024+1)]){
      const response=await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body});
      assert.ok([400,413].includes(response.status));
    }assert.equal(f.calls(),0);
  }finally{await f.close();}
});
test('duplicate job is single-use by authenticated owner and does not retranscribe',async()=>{
  const f=await fixture();try{
    assert.equal((await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body:wav()})).status,200);
    const duplicate=await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body:wav()});
    assert.equal(duplicate.status,409);assert.equal(f.calls(),1);
    const text=fs.readFileSync(path.join(f.directory,'budget.json'),'utf8');
    assert.ok(!text.includes('fixture-token'));assert.ok(!text.includes('make four spearmen'));
  }finally{await f.close();}
});
test('restart preserves short-lived job identity, without retaining its transcript',async()=>{
  const f=await fixture();let restarted;try{
    assert.equal((await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body:wav()})).status,200);
    await new Promise(resolve=>f.service.close(resolve));restarted=await fixture({directory:f.directory});
    const duplicate=await fetch(restarted.base+'/api/commander/stt',{method:'POST',headers,body:wav()});
    assert.equal(duplicate.status,409);assert.equal(restarted.calls(),0);
    const persisted=fs.readFileSync(path.join(f.directory,'budget.json'),'utf8');assert.ok(!persisted.includes('make four spearmen'));
  }finally{if(restarted)await new Promise(resolve=>restarted.service.close(resolve));await f.close();}
});
test('owner/global quota persisted across restart; no reset or unbounded new owner admission',async()=>{
  const f=await fixture({maxAudioSeconds:1,maxPerOwnerAudioSeconds:1});try{
    assert.equal((await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body:wav()})).status,200);
    await new Promise(resolve=>f.service.close(resolve));
    const restarted=await fixture({directory:f.directory,maxAudioSeconds:1,maxPerOwnerAudioSeconds:1});
    const response=await fetch(restarted.base+'/api/commander/stt',{method:'POST',headers:{...headers,'X-Voice-Job':'33333333-3333-4333-8333-333333333333'},body:wav()});
    await new Promise(resolve=>restarted.service.close(resolve));
    assert.equal(response.status,429);assert.equal(f.calls(),1);
    const ledger=JSON.parse(fs.readFileSync(path.join(f.directory,'budget.json'),'utf8'));assert.equal(ledger.audioSeconds,1);
  }finally{await f.close();}
});
test('rejected origin never receives permission to read a gateway response',async()=>{
  const f=await fixture();try{
    const r=await fetch(f.base+'/api/commander/stt',{method:'POST',headers:{...headers,Origin:'https://evil.example.invalid'},body:wav()});
    assert.equal(r.status,403);assert.equal(r.headers.get('Access-Control-Allow-Origin'),null);
  }finally{await f.close();}
});
test('deadline answers promptly but reserves capacity until ignoring provider actually stops',async()=>{
  let finish;const raw=new Promise(resolve=>{finish=resolve;});
  const f=await fixture({requestTimeoutMs:80,maxConcurrent:1,transcribe:()=>raw});try{
    const r=await fetch(f.base+'/api/commander/stt',{method:'POST',headers,body:wav(),signal:AbortSignal.timeout(1000)});
    assert.equal(r.status,504);assert.deepEqual(await r.json(),{error:'TIMEOUT_OR_CANCELLED'});
    const busy=await fetch(f.base+'/api/commander/stt',{method:'POST',headers:{...headers,'X-Voice-Job':'33333333-3333-4333-8333-333333333333'},body:wav()});
    assert.equal(busy.status,429);
  }finally{finish('late inert text');await raw;await f.close();}
});
test('semantic route rejects caller endpoint/model and bounds envelope while preserving Luna',async()=>{
  const f=await fixture();try{
    const semanticHeaders={...headers,'Content-Type':'application/json','X-Commander-Job':job};
    const body={model:'openai/gpt-6-luna',messages:[{role:'user',content:'fixture text'}],max_tokens:4096,reasoning:{effort:'none'}};
    assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:semanticHeaders,body:JSON.stringify(body)})).status,200);
    assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'33333333-3333-4333-8333-333333333333'},body:JSON.stringify({...body,model:'other'})})).status,400);
    assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'44444444-4444-4444-8444-444444444444'},body:JSON.stringify({...body,url:'https://evil.example.invalid'})})).status,400);
  }finally{await f.close();}
});
