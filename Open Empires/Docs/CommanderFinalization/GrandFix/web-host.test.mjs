import {test} from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import path from 'node:path';import os from 'node:os';import zlib from 'node:zlib';import http from 'node:http';
async function fixture(options={}){const {createWebHost}=await import('./web-host.mjs');const directory=fs.mkdtempSync(path.join(os.tmpdir(),'OpenEmpires-host-'));
  fs.mkdirSync(path.join(directory,'Build'));fs.writeFileSync(path.join(directory,'index.html'),'<canvas></canvas><script>window.fixture=1;</script>');
  fs.writeFileSync(path.join(directory,'Build','fixture.framework.js.gz'),zlib.gzipSync('fixture JS'));
  fs.writeFileSync(path.join(directory,'Build','fixture.wasm.br'),zlib.brotliCompressSync(Buffer.from([0,97,115,109])));
  fs.writeFileSync(path.join(directory,'Build','fixture.data.unityweb'),Buffer.from('loader-managed compressed bytes'));
  fs.writeFileSync(path.join(directory,'.env'),'fixture-only-secret');fs.writeFileSync(path.join(directory,'ggml-tiny.bin'),'not a browser model');
  const server=createWebHost({root:directory,...options});await new Promise(r=>server.listen(0,'127.0.0.1',r));
  return {directory,server,url:'http://127.0.0.1:'+server.address().port,async close(){await new Promise(r=>server.close(r));const resolved=path.resolve(directory);
    if(path.dirname(resolved)===path.resolve(os.tmpdir())&&path.basename(resolved).startsWith('OpenEmpires-host-'))fs.rmSync(resolved,{recursive:true,force:true});}};}
test('HTML uses response-specific nonces, narrow CSP and no default cross-origin isolation',async()=>{const f=await fixture();try{
  const response=await fetch(f.url+'/');const html=await response.text(),csp=response.headers.get('Content-Security-Policy');const nonce=html.match(/nonce="([^"]+)"/)[1];
  const script=csp.split(';').find(value=>value.trim().startsWith('script-src '));
  assert.ok(script.includes("'nonce-"+nonce+"'"));assert.ok(!script.includes("'unsafe-inline'"));assert.ok(!script.includes("'unsafe-eval'"));
  assert.equal(response.headers.get('Permissions-Policy'),'microphone=(self), camera=()');assert.equal(response.headers.get('Cross-Origin-Embedder-Policy'),null);
  const second=await(await fetch(f.url+'/')).text();assert.notEqual(second.match(/nonce="([^"]+)"/)[1],nonce);
}finally{await f.close();}});
test('gzip and brotli assets preserve compression and actual decoded MIME; unityweb stays loader-managed',async()=>{const f=await fixture();try{
  let r=await fetch(f.url+'/Build/fixture.framework.js.gz');assert.equal(r.headers.get('Content-Encoding'),'gzip');assert.match(r.headers.get('Content-Type'),/javascript/);assert.equal(await r.text(),'fixture JS');
  r=await fetch(f.url+'/Build/fixture.wasm.br');assert.equal(r.headers.get('Content-Encoding'),'br');assert.equal(r.headers.get('Content-Type'),'application/wasm');assert.deepEqual([...new Uint8Array(await r.arrayBuffer())],[0,97,115,109]);
  r=await fetch(f.url+'/Build/fixture.data.unityweb');assert.equal(r.headers.get('Content-Encoding'),null);assert.equal(await r.text(),'loader-managed compressed bytes');
}finally{await f.close();}});
test('private/config/model files and encoded traversal are denied before any data disclosure',async()=>{const f=await fixture();try{
  for(const suffix of ['/.env','/ggml-tiny.bin','/%2e%2e%2f.env','/Build/%2e%2e%5c.env']){
    const r=await fetch(f.url+suffix);assert.ok([400,403,404].includes(r.status));assert.ok(!(await r.text()).includes('fixture-only-secret'));
  }
}finally{await f.close();}});
test('symlink escape is refused and unknown Host cannot turn loopback server into an exposed origin',async()=>{const f=await fixture();try{
  const outside=path.join(os.tmpdir(),'OpenEmpires-outside-'+path.basename(f.directory));fs.mkdirSync(outside);fs.writeFileSync(path.join(outside,'secret.txt'),'outside fixture');
  try{fs.symlinkSync(outside,path.join(f.directory,'escape'),'junction');const r=await fetch(f.url+'/escape/secret.txt');assert.equal(r.status,403);
    const result=await new Promise((resolve,reject)=>{const req=http.get(f.url+'/',{headers:{Host:'evil.example.invalid'}},res=>{res.resume();res.on('end',()=>resolve(res.statusCode));});req.on('error',reject);});assert.equal(result,403);
  }finally{const resolved=path.resolve(outside);if(path.dirname(resolved)===path.resolve(os.tmpdir())&&path.basename(resolved).startsWith('OpenEmpires-outside-OpenEmpires-host-'))fs.rmSync(resolved,{recursive:true,force:true});}
}finally{await f.close();}});
test('isolation and external connect origins are enabled only by explicit supported configuration',async()=>{const f=await fixture({isolation:true,gatewayOrigins:['https://api.example.invalid']});try{
  const r=await fetch(f.url+'/');assert.equal(r.headers.get('Cross-Origin-Opener-Policy'),'same-origin');assert.equal(r.headers.get('Cross-Origin-Embedder-Policy'),'require-corp');
  assert.ok(r.headers.get('Content-Security-Policy').includes('https://api.example.invalid'));
}finally{await f.close();}});
