import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
const plugin='Assets/Plugins/WebGL/CommanderGateway.jslib';
const job='22222222-2222-4222-8222-222222222222';
function fixture(fetchImpl) {
  const strings={1:'receiver-fixture',2:job,3:'https://game.example.invalid',4:'11111111-1111-4111-8111-111111111111',5:'policy-v1',6:'en'};
  const library={},messages=[],timers=[];const heap=new Uint8Array(3*1024*1024);
  const context={LibraryManager:{library},mergeInto:(target,values)=>Object.assign(target,values),
    UTF8ToString:pointer=>strings[pointer]??'',HEAPU8:heap,SendMessage:(...args)=>messages.push(args),
    fetch:fetchImpl,URL,AbortController,Uint8Array,Date,Promise,TextDecoder,
    setTimeout:callback=>{timers.push(callback);return timers.length;},clearTimeout:()=>{}};
  vm.runInNewContext(fs.readFileSync(plugin,'utf8'),context);
  // Emscripten dependency injection publishes $ helpers as bare globals.
  for(const [key,value]of Object.entries(library))if(key.startsWith('$'))context[key.slice(1)]=value;
  return {library,messages,timers,heap,strings,
    start:()=>library.CommanderGateway_Begin(1,2,3,4,5,6,100,44,1)};
}
async function settle(){for(let i=0;i<8;i++)await new Promise(resolve=>setImmediate(resolve));}
test('browser bridge streams a bounded response and signals metadata only to the fixed callback',async()=>{
  let requested;const f=fixture(async(url,options)=>{requested={url,options};return new Response('{"text":"four spearmen"}',{status:200});});
  f.start();await settle();assert.equal(requested.url,'https://game.example.invalid/api/commander/stt');
  assert.equal(requested.options.headers['X-Voice-Consent'],'policy-v1');assert.equal(requested.options.redirect,'error');
  assert.equal(f.messages.length,1);assert.equal(f.messages[0][1],'OnCommanderGatewayResponse');
  const meta=JSON.parse(f.messages[0][2]);assert.equal(meta.jobId,job);assert.equal(meta.status,200);
  assert.ok(!f.messages[0][2].includes('spearmen'));
  const size=f.library.CommanderGateway_ResponseSize(2);assert.equal(size,24);
  assert.equal(f.library.CommanderGateway_ReadResponse(2,200,size),size);
  assert.equal(new TextDecoder().decode(f.heap.slice(200,200+size)),'{"text":"four spearmen"}');
  assert.equal(f.library.CommanderGateway_ResponseSize(2),-1);
});
test('chunked oversized response is aborted before retaining the full body or notifying raw text',async()=>{
  const f=fixture(async()=>new Response(new ReadableStream({start(c){c.enqueue(new Uint8Array(40000));c.enqueue(new Uint8Array(40000));c.close();}})));
  f.start();await settle();assert.equal(f.messages.length,1);
  assert.equal(JSON.parse(f.messages[0][2]).error,'RESPONSE_TOO_LARGE');assert.equal(f.library.CommanderGateway_ResponseSize(2),-1);
});
test('cancelled late fetch cannot call an old Unity receiver',async()=>{
  let finish;const f=fixture(()=>new Promise(resolve=>{finish=resolve;}));f.start();f.library.CommanderGateway_Cancel(2);
  finish(new Response('{"text":"stale"}'));await settle();assert.equal(f.messages.length,0);assert.equal(f.library.CommanderGateway_ResponseSize(2),-1);
});
test('insecure remote gateway is rejected before token or audio transfer',async()=>{
  let calls=0;const f=fixture(()=>{calls++;return Promise.resolve(new Response('{}'));});f.strings[3]='http://remote.example.invalid';
  f.start();await settle();assert.equal(calls,0);assert.equal(JSON.parse(f.messages[0][2]).error,'GATEWAY_SETUP_REQUIRED');
});
test('declared oversized body releases its unread browser stream',async()=>{
  let cancelled=0;const f=fixture(async()=>new Response(new ReadableStream({cancel(){cancelled++;}}),{headers:{'Content-Length':'70000'}}));
  f.start();await settle();assert.equal(JSON.parse(f.messages[0][2]).error,'RESPONSE_TOO_LARGE');assert.equal(cancelled,1);
});
