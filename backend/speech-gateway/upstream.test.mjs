import {test} from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';

const owner='11111111-1111-4111-8111-111111111111';
const token='22222222-2222-4222-8222-222222222222';
async function fixture(handler) {
  const {createAdapters}=await import('./upstream.mjs');
  const server=http.createServer(handler);await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const base='http://127.0.0.1:'+server.address().port,targets=[];
  const adapters=createAdapters({authBaseUrl:base,openaiKey:'fake-server-audio-key',openrouterKey:'fake-server-semantic-key',
    fetchImpl:(url,options)=>{targets.push({url,options});return fetch(base+new URL(url).pathname,options);}});
  return {adapters,targets,close:()=>new Promise(resolve=>server.close(resolve))};
}
test('fixed audio endpoint sends server-owned multipart settings and admits only bounded plain text',async()=>{
  const f=await fixture(async(req,res)=>{
    const chunks=[];for await(const c of req)chunks.push(c);
    assert.equal(req.url,'/v1/audio/transcriptions');assert.equal(req.headers.authorization,'Bearer fake-server-audio-key');
    const body=Buffer.concat(chunks).toString();assert.match(body,/gpt-transcribe/);assert.match(body,/audio\/wav/);
    assert.match(body,/name="language"\r\n\r\nen/);res.end(JSON.stringify({text:'four spearmen',unused:'ignored'}));
  });try{
    assert.equal(await f.adapters.transcribe({bytes:Buffer.from('fixture'),language:'en',signal:AbortSignal.timeout(2000)}),'four spearmen');
    assert.equal(f.targets[0].url,'https://api.openai.com/v1/audio/transcriptions');assert.equal(f.targets[0].options.redirect,'error');
  }finally{await f.close();}
});
test('authentication verifies backend session and never treats an origin as identity',async()=>{
  const f=await fixture((req,res)=>{assert.equal(req.url,'/api/auth/session');assert.equal(req.headers.authorization,'Bearer '+token);res.end(JSON.stringify({player_id:owner}));});
  try{assert.equal(await f.adapters.authenticate(token,AbortSignal.timeout(2000)),owner);
    assert.equal(await f.adapters.authenticate('not-a-session-token',AbortSignal.timeout(2000)),null);assert.equal(f.targets.length,1);
  }finally{await f.close();}
});
test('upstream chunked/error bodies are bounded and raw provider failures are never surfaced',async()=>{
  const f=await fixture((req,res)=>{res.writeHead(401,{'Content-Type':'application/json'});res.write('secret-like-upstream-error');res.end('x'.repeat(70000));});
  try{await assert.rejects(f.adapters.semantic({body:{model:'openai/gpt-6-luna'},signal:AbortSignal.timeout(2000)}),{message:'UPSTREAM_AUTH'});
    assert.equal(f.targets[0].url,'https://openrouter.ai/api/v1/chat/completions');
  }finally{await f.close();}
});
test('chunked success exceeding decoded response cap is rejected before JSON materialization',async()=>{
  const f=await fixture((req,res)=>{res.writeHead(200,{'Content-Type':'application/json'});res.write('{"text":"');res.end('x'.repeat(70000)+'"}');});
  try{await assert.rejects(f.adapters.transcribe({bytes:Buffer.from('fixture'),language:'en',signal:AbortSignal.timeout(2000)}),{message:'RESPONSE_TOO_LARGE'});
  }finally{await f.close();}
});
test('absent server credential is fail-closed without an upstream request',async()=>{
  const {createAdapters}=await import('./upstream.mjs');let called=false;
  const adapters=createAdapters({authBaseUrl:'http://127.0.0.1:8080',fetchImpl:()=>{called=true;throw new Error('unexpected');}});
  await assert.rejects(adapters.transcribe({bytes:Buffer.from('fixture'),language:'en',signal:AbortSignal.timeout(1000)}),{message:'UPSTREAM_UNAVAILABLE'});assert.equal(called,false);
});
