import fs from 'node:fs';import fsp from 'node:fs/promises';import path from 'node:path';import {createHash,randomUUID} from 'node:crypto';import {fileURLToPath} from 'node:url';
const maximumModelBytes=1600000000,maximumCacheBytes=3*1024*1024*1024;
function error(code){return new Error(code);}
function validate(entry){if(!entry||!/^ggml-(tiny|base\.en|small\.en|medium\.en)\.bin$/.test(entry.file)||!Number.isSafeInteger(entry.bytes)||entry.bytes<1||entry.bytes>maximumModelBytes
 ||!/^\w{64}$/.test(entry.sha256)||!/^[a-f0-9]{64}$/.test(entry.sha256)||entry.repository!=='ggerganov/whisper.cpp'||!/^[a-f0-9]{40}$/.test(entry.revision))throw error('MODEL_MANIFEST_INVALID');}
function cancelled(signal){if(signal?.aborted)throw error('MODEL_CANCELLED');}
async function safeDirectory(directory){const absolute=path.resolve(directory);let current=path.parse(absolute).root;
 for(const part of absolute.slice(current.length).split(path.sep).filter(Boolean)){current=path.join(current,part);try{const s=await fsp.lstat(current);if(s.isSymbolicLink()||!s.isDirectory())throw error('MODEL_PATH_INVALID');}
 catch(e){if(e.code!=='ENOENT')throw e;await fsp.mkdir(current);}}return absolute;}
async function verify(file,entry,signal){const info=await fsp.lstat(file);if(info.isSymbolicLink()||!info.isFile())throw error('MODEL_PATH_INVALID');if(info.size!==entry.bytes)throw error('MODEL_SIZE_MISMATCH');
 const hash=createHash('sha256');let bytes=0;for await(const chunk of fs.createReadStream(file,{highWaterMark:65536})){cancelled(signal);bytes+=chunk.length;if(bytes>entry.bytes)throw error('MODEL_SIZE_MISMATCH');hash.update(chunk);}
 if(bytes!==entry.bytes)throw error('MODEL_SIZE_MISMATCH');if(hash.digest('hex')!==entry.sha256)throw error('MODEL_HASH_MISMATCH');}
async function storageBudget(root,needed){let used=0;const dirs=await fsp.readdir(root,{withFileTypes:true});if(dirs.length>32)throw error('MODEL_CACHE_LIMIT');
 for(const item of dirs){if(item.name==='.provision.lock')continue;if(!/^[a-f0-9]{64}$/.test(item.name)||item.isSymbolicLink()||!item.isDirectory())throw error('MODEL_PATH_INVALID');
  const files=await fsp.readdir(path.join(root,item.name));if(files.length>8)throw error('MODEL_CACHE_LIMIT');for(const name of files){const stat=await fsp.lstat(path.join(root,item.name,name));
   if(stat.isSymbolicLink()||!stat.isFile())throw error('MODEL_PATH_INVALID');used+=stat.size;}}
 if(used+needed>maximumCacheBytes)throw error('MODEL_CACHE_LIMIT');const disk=await fsp.statfs(root);if(disk.bavail*disk.bsize<needed+64*1024*1024)throw error('MODEL_DISK_FULL');}

// Developer acquisition/import API. CLI always selects from the bundled trusted
// manifest; injected entries/fetches support controlled offline tests, not user URLs.
export async function provisionModel({entry,cacheRoot,sourcePath=null,fetchImpl=fetch,signal=null,onProgress=()=>{}}){
 validate(entry);cancelled(signal);if(typeof cacheRoot!=='string'||!cacheRoot)throw error('MODEL_PATH_INVALID');
 const root=await safeDirectory(cacheRoot);let lock=null,temp=null,output=null;const lockPath=path.join(root,'.provision.lock');
 try{
  try{lock=await fsp.open(lockPath,'wx');}catch(e){if(e.code==='EEXIST')throw error('MODEL_BUSY');throw e;}
  await lock.writeFile(JSON.stringify({schema:'openempires-model-acquisition@1',started:new Date().toISOString()}));
  const directory=await safeDirectory(path.join(root,entry.sha256));const target=path.join(directory,entry.file);
  try{await fsp.lstat(target);await verify(target,entry,signal);return{status:'verified',path:target,bytes:entry.bytes,sha256:entry.sha256,existing:true};}
  catch(e){if(e.code!=='ENOENT')throw e;}
  await storageBudget(root,entry.bytes);cancelled(signal);let body;
  if(sourcePath){const source=path.resolve(sourcePath);if(path.extname(source).toLowerCase()!=='.bin')throw error('MODEL_PATH_INVALID');
   const stat=await fsp.lstat(source);if(stat.isSymbolicLink()||!stat.isFile())throw error('MODEL_PATH_INVALID');if(stat.size!==entry.bytes)throw error('MODEL_SIZE_MISMATCH');body=fs.createReadStream(source,{highWaterMark:65536});}
  else{
   const deadline=AbortSignal.timeout(15*60*1000);const operationSignal=signal?AbortSignal.any([signal,deadline]):deadline;
   const response=await fetchImpl('https://huggingface.co/'+entry.repository+'/resolve/'+entry.revision+'/'+entry.file,{signal:operationSignal,headers:{Accept:'application/octet-stream'}});
   if(!response.ok||!response.body)throw error('MODEL_DOWNLOAD_FAILED');const length=response.headers.get('content-length');
   if(length!==null&&(!/^\d+$/.test(length)||Number(length)!==entry.bytes))throw error('MODEL_SIZE_MISMATCH');body=response.body;
  }
  temp=path.join(directory,'.download-'+randomUUID()+'.partial');output=await fsp.open(temp,'wx');const hash=createHash('sha256');let bytes=0;
  for await(const raw of body){cancelled(signal);const chunk=Buffer.from(raw);bytes+=chunk.length;if(bytes>entry.bytes)throw error('MODEL_SIZE_MISMATCH');
   hash.update(chunk);let offset=0;while(offset<chunk.length){const result=await output.write(chunk,offset,chunk.length-offset);if(!result.bytesWritten)throw error('MODEL_WRITE_FAILED');offset+=result.bytesWritten;}
   onProgress({received:bytes,total:entry.bytes});}
  cancelled(signal);if(bytes!==entry.bytes)throw error('MODEL_SIZE_MISMATCH');if(hash.digest('hex')!==entry.sha256)throw error('MODEL_HASH_MISMATCH');
  await output.sync();await output.close();output=null;await verify(temp,entry,signal);cancelled(signal);
  // Same-filesystem exclusive promotion never replaces a working/corrupt target.
  await fsp.link(temp,target);await fsp.unlink(temp);temp=null;
  return{status:'verified',path:target,bytes:entry.bytes,sha256:entry.sha256,existing:false};
 }catch(e){if(signal?.aborted)throw error('MODEL_CANCELLED');if(/^MODEL_[A-Z_]+$/.test(e.message))throw e;
  if(e.name==='TimeoutError'||e.name==='AbortError')throw error('MODEL_TIMEOUT');
  if(e.code==='ENOSPC')throw error('MODEL_DISK_FULL');
  if(/^(ECONNRESET|ETIMEDOUT|EACCES|ENETUNREACH|ECONNREFUSED|UND_ERR_)/.test(e.code??e.cause?.code??''))throw error('MODEL_NETWORK_FAILED');
  throw error('MODEL_PROVISION_FAILED');}
 finally{if(output)await output.close().catch(()=>{});if(temp)await fsp.unlink(temp).catch(()=>{});if(lock){await lock.close().catch(()=>{});await fsp.unlink(lockPath).catch(()=>{});}}
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 try{const args=process.argv.slice(2),model=args.shift();let cacheRoot=path.resolve('LocalModels/Whisper'),sourcePath=null;
  while(args.length){const flag=args.shift();if(flag==='--root')cacheRoot=path.resolve(args.shift()??'');else if(flag==='--import')sourcePath=args.shift();else throw error('MODEL_ARGUMENT_INVALID');}
  const catalogPath=new URL('../../../Assets/Resources/CommanderVoice/whisper-models.json',import.meta.url);const catalog=JSON.parse(await fsp.readFile(catalogPath,'utf8'));
  if(catalog.schema!=='openempires-whisper-models@1'||!Array.isArray(catalog.models)||catalog.models.length>8)throw error('MODEL_MANIFEST_INVALID');
  const spec=catalog.models.find(x=>x.file===model);if(!spec)throw error('MODEL_UNKNOWN');const entry={...spec,repository:catalog.repository,revision:catalog.revision};
  const result=await provisionModel({entry,cacheRoot,sourcePath});console.log(JSON.stringify({status:result.status,model:entry.file,bytes:result.bytes,sha256:result.sha256,existing:result.existing}));
 }catch(e){console.error(/^MODEL_[A-Z_]+$/.test(e.message)?e.message:'MODEL_PROVISION_FAILED');process.exitCode=1;}
}
