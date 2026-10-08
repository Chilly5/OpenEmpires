import {test} from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import os from 'node:os';import path from 'node:path';
const entry={file:'ggml-tiny.bin',bytes:3,sha256:'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',repository:'ggerganov/whisper.cpp',revision:'5359861c739e955e79d9a303bcbc70fb988958b1'};
async function fixture(){const dir=fs.mkdtempSync(path.join(os.tmpdir(),'OpenEmpires-provision-'));return{dir,root:path.join(dir,'cache'),cleanup(){const p=path.resolve(dir);if(path.dirname(p)===path.resolve(os.tmpdir())&&path.basename(p).startsWith('OpenEmpires-provision-'))fs.rmSync(p,{recursive:true,force:true});}};}
test('manual import verifies trusted size/hash before atomic promotion and preserves source',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');const source=path.join(f.dir,'ggml-tiny.bin');fs.writeFileSync(source,'abc');
 const r=await provisionModel({entry,cacheRoot:f.root,sourcePath:source});assert.equal(fs.readFileSync(r.path,'utf8'),'abc');assert.equal(fs.readFileSync(source,'utf8'),'abc');assert.equal(r.status,'verified');
 assert.deepEqual(fs.readdirSync(path.dirname(r.path)),['ggml-tiny.bin']);
}finally{f.cleanup();}});
test('hash mismatch is rejected without installed model or partial leftover',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');const source=path.join(f.dir,'ggml-tiny.bin');fs.writeFileSync(source,'bad');
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,sourcePath:source}),/MODEL_HASH_MISMATCH/);
 assert.ok(!fs.existsSync(path.join(f.root,entry.sha256,entry.file)));assert.equal(fs.readFileSync(source,'utf8'),'bad');
 assert.ok(!fs.readdirSync(f.root).includes('.provision.lock'));assert.deepEqual(fs.readdirSync(path.join(f.root,entry.sha256)),[]);
}finally{f.cleanup();}});
test('existing verified model is preserved and no unnecessary request occurs',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');const source=path.join(f.dir,'ggml-tiny.bin');fs.writeFileSync(source,'abc');
 const first=await provisionModel({entry,cacheRoot:f.root,sourcePath:source});let calls=0;
 const second=await provisionModel({entry,cacheRoot:f.root,fetchImpl:async()=>{calls++;throw new Error('not called');}});
 assert.equal(calls,0);assert.equal(second.path,first.path);assert.equal(fs.readFileSync(first.path,'utf8'),'abc');
}finally{f.cleanup();}});
test('corrupt existing model is reported, not overwritten or silently accepted',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');fs.mkdirSync(path.join(f.root,entry.sha256),{recursive:true});const target=path.join(f.root,entry.sha256,entry.file);fs.writeFileSync(target,'bad');
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,fetchImpl:async()=>new Response('abc')}),/MODEL_HASH_MISMATCH/);assert.equal(fs.readFileSync(target,'utf8'),'bad');
}finally{f.cleanup();}});
test('chunked oversize is stopped and never promoted',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');let url;
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,fetchImpl:async value=>{url=value;return new Response(new ReadableStream({start(c){c.enqueue(new TextEncoder().encode('abc'));c.enqueue(new TextEncoder().encode('x'));c.close();}}));}}),/MODEL_SIZE_MISMATCH/);
 assert.equal(url,'https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-tiny.bin');assert.ok(!fs.existsSync(path.join(f.root,entry.sha256,entry.file)));
}finally{f.cleanup();}});
test('cancellation during streamed download cleans only owned temporary files',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');const cts=new AbortController();
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,signal:cts.signal,fetchImpl:async()=>new Response(new ReadableStream({start(c){c.enqueue(new TextEncoder().encode('a'));cts.abort();c.close();}}))}),/MODEL_CANCELLED/);
 assert.deepEqual(fs.readdirSync(path.join(f.root,entry.sha256)),[]);assert.ok(!fs.existsSync(path.join(f.root,'.provision.lock')));
}finally{f.cleanup();}});
test('active root lock rejects replacement rather than creating another acquisition',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');fs.mkdirSync(f.root,{recursive:true});fs.writeFileSync(path.join(f.root,'.provision.lock'),'fixture lock');
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,fetchImpl:async()=>new Response('abc')}),/MODEL_BUSY/);assert.equal(fs.readFileSync(path.join(f.root,'.provision.lock'),'utf8'),'fixture lock');
}finally{f.cleanup();}});
test('symlink/junction cache subdirectory cannot redirect writes outside the requested cache',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');fs.mkdirSync(f.root);const outside=path.join(f.dir,'outside');fs.mkdirSync(outside);fs.symlinkSync(outside,path.join(f.root,entry.sha256),'junction');
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,fetchImpl:async()=>new Response('abc')}),/MODEL_PATH_INVALID/);assert.deepEqual(fs.readdirSync(outside),[]);
}finally{f.cleanup();}});
test('model traversal, unpinned revision and unsupported repository are rejected before I/O',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');for(const patch of [{file:'../bad.bin'},{revision:'main'},{repository:'other/repo'}]){
 await assert.rejects(provisionModel({entry:{...entry,...patch},cacheRoot:f.root,fetchImpl:async()=>new Response('abc')}),/MODEL_MANIFEST_INVALID/);}
 assert.ok(!fs.existsSync(f.root));
}finally{f.cleanup();}});
test('transport failures retain a safe useful category without raw upstream errors',async()=>{const f=await fixture();try{
 const {provisionModel}=await import('./model-provision.mjs');
 await assert.rejects(provisionModel({entry,cacheRoot:f.root,fetchImpl:async()=>{throw Object.assign(new Error('fixture-only raw upstream detail'),{code:'ECONNRESET'});}}),/MODEL_NETWORK_FAILED/);
 assert.ok(!fs.existsSync(path.join(f.root,'.provision.lock')));
}finally{f.cleanup();}});
