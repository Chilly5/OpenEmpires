import http from 'node:http';
import {createBudget} from './budget.mjs';
import {inspectWav,maximumAudioBytes} from './wav.mjs';

const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function deny(code,status=400){const error=new Error(code);error.gatewayCode=code;error.status=status;return error;}
async function untilAborted(task,signal) {
  signal.throwIfAborted();let cancel;
  const interrupted=new Promise((_,reject)=>{cancel=()=>reject(signal.reason);signal.addEventListener('abort',cancel,{once:true});});
  try{return await Promise.race([task,interrupted]);}finally{signal.removeEventListener('abort',cancel);}
}
async function receive(request,limit) {
  const declared=request.headers['content-length'];
  if(declared!==undefined&&(!/^\d+$/.test(declared)||Number(declared)>limit))throw deny('UPLOAD_TOO_LARGE',413);
  let size=0;const chunks=[];
  for await(const chunk of request){size+=chunk.length;if(size>limit)throw deny('UPLOAD_TOO_LARGE',413);chunks.push(chunk);}
  return Buffer.concat(chunks,size);
}
function semanticRequest(body) {
  if(!body||typeof body!=='object'||Array.isArray(body)||Object.keys(body).some(k=>!['model','messages','max_tokens','reasoning'].includes(k))
    ||body.model!=='openai/gpt-6-luna'||!Number.isInteger(body.max_tokens)||body.max_tokens<1||body.max_tokens>4096
    ||!Array.isArray(body.messages)||body.messages.length<1||body.messages.length>16)throw deny('INVALID_SEMANTIC_REQUEST');
  let length=0;
  for(const m of body.messages) {
    if(!m||Object.keys(m).some(k=>!['role','content'].includes(k))||!['system','user','assistant'].includes(m.role)||typeof m.content!=='string')throw deny('INVALID_SEMANTIC_REQUEST');
    length+=m.content.length;
  }
  if(length>24576||body.reasoning!==undefined&&(Object.keys(body.reasoning).length!==1||body.reasoning.effort!=='none'))throw deny('INVALID_SEMANTIC_REQUEST');
  return body;
}
export function createGateway(configuration) {
  const options={maxConcurrent:2,maxPerOwnerConcurrent:1,maxPerOwnerSemanticRequests:6,requestTimeoutMs:30000,
    maxJobRecords:256,jobTtlMs:120000,...configuration};
  if(typeof options.authenticate!=='function'||typeof options.transcribe!=='function'||typeof options.semantic!=='function'
    ||!options.policy?.version||!Array.isArray(options.allowedOrigins)||!Array.isArray(options.authorizedOwners)||!options.ledgerPath)throw new Error('GATEWAY_CONFIG_INVALID');
  const budget=createBudget(options),ownerActive=new Map();let active=0;
  const server=http.createServer({maxHeaderSize:8192},async(request,response)=>{
    const abort=new AbortController();let admitted=false,owner,owned=false,pending,settled=true;
    async function wait(task) {
      settled=false;pending=Promise.resolve(task).then(value=>{settled=true;return value;},error=>{settled=true;throw error;});
      pending.catch(()=>{});return untilAborted(pending,abort.signal);
    }
    function release() {
      if(!admitted)return;admitted=false;active--;
      if(owned){const remaining=(ownerActive.get(owner)??1)-1;if(remaining)ownerActive.set(owner,remaining);else ownerActive.delete(owner);}
    }
    const expire=setTimeout(()=>abort.abort(),options.requestTimeoutMs);expire.unref();
    request.on('aborted',()=>abort.abort());response.on('close',()=>{if(!response.writableEnded)abort.abort();});
    function reply(status,body) {
      if(response.destroyed)return;
      const serialized=JSON.stringify(body);if(Buffer.byteLength(serialized)>65536){status=502;body={error:'RESPONSE_TOO_LARGE'};}
      response.writeHead(status,{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store',
        'X-Content-Type-Options':'nosniff',...(options.allowedOrigins.includes(request.headers.origin)?{'Access-Control-Allow-Origin':request.headers.origin,'Vary':'Origin'}:{})});
      response.end(JSON.stringify(body));
      if(abort.signal.aborted&&!request.complete)response.once('finish',()=>request.destroy());
    }
    try {
      const origin=request.headers.origin;
      if(origin!==undefined&&!options.allowedOrigins.includes(origin))throw deny('ORIGIN_REJECTED',403);
      if(request.method==='OPTIONS') {
        if(!origin||!options.allowedOrigins.includes(origin))throw deny('ORIGIN_REJECTED',403);
        response.writeHead(204,{'Access-Control-Allow-Origin':origin,'Vary':'Origin','Access-Control-Allow-Methods':'POST, GET, OPTIONS',
          'Access-Control-Allow-Headers':'Authorization, Content-Type, X-Voice-Job, X-Voice-Consent, X-Voice-Language, X-Commander-Job','Access-Control-Max-Age':'300'});
        response.end();return;
      }
      if(request.url==='/api/commander/voice-policy'&&request.method==='GET'){reply(200,{...options.policy,maxDurationSeconds:60,audioFormat:'pcm16-wav-mono16k'});return;}
      const audio=request.url==='/api/commander/stt',semantic=request.url==='/api/commander/semantic';
      if(request.method!=='POST'||!audio&&!semantic)throw deny('NOT_FOUND',404);
      const bearer=request.headers.authorization;
      if(typeof bearer!=='string'||!bearer.startsWith('Bearer ')||bearer.length>512)throw deny('UNAUTHENTICATED',401);
      if(active>=options.maxConcurrent)throw deny('BUSY',429);
      admitted=true;active++;
      owner=await wait(options.authenticate(bearer.slice(7),abort.signal));
      if(typeof owner!=='string'||!uuid.test(owner))throw deny('UNAUTHENTICATED',401);
      if(!options.authorizedOwners.includes(owner))throw deny('SESSION_NOT_ENABLED',403);
      if((ownerActive.get(owner)??0)>=options.maxPerOwnerConcurrent)throw deny('BUSY',429);
      owned=true;ownerActive.set(owner,(ownerActive.get(owner)??0)+1);
      if(audio&&request.headers['x-voice-consent']!==options.policy.version)throw deny('CONSENT_REQUIRED',409);
      const job=request.headers[audio?'x-voice-job':'x-commander-job'];if(typeof job!=='string'||!uuid.test(job))throw deny('INVALID_JOB');
      const now=Date.now();
      const identity=owner+':'+(audio?'audio:':'semantic:')+job;
      if(budget.hasJob(identity,now))throw deny('DUPLICATE_JOB',409);
      const content=request.headers['content-type'];
      if(audio&&content!=='audio/wav'||semantic&&!/^application\/json(?:;\s*charset=utf-8)?$/i.test(content??''))throw deny('INVALID_CONTENT_TYPE');
      const bytes=await wait(receive(request,audio?maximumAudioBytes:65536));abort.signal.throwIfAborted();
      let parsed;
      if(audio)parsed=inspectWav(bytes);
      else {try{parsed=semanticRequest(JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes)));}catch{throw deny('INVALID_SEMANTIC_REQUEST');}}
      const language=request.headers['x-voice-language']??'en';if(audio&&!['en','fr','de','es','it','pt','ja','ko','zh'].includes(language))throw deny('INVALID_LANGUAGE');
      budget.reserve(owner,audio?'audio':'semantic',audio?parsed.billableSeconds:0,job,now);
      if(audio) {
        const text=await wait(options.transcribe({bytes,language,jobId:job,owner,signal:abort.signal}));abort.signal.throwIfAborted();
        if(typeof text!=='string'||text.length>4096)throw deny('INVALID_TRANSCRIPT',502);
        reply(200,{jobId:job,policyVersion:options.policy.version,text});
      }else {
        const result=await wait(options.semantic({body:parsed,jobId:job,owner,signal:abort.signal}));abort.signal.throwIfAborted();reply(200,result);
      }
    }catch(error) {
      const safe=['INVALID_AUDIO','DUPLICATE_JOB','QUOTA_EXHAUSTED','CAPACITY','UPSTREAM_AUTH','UPSTREAM_QUOTA','RESPONSE_TOO_LARGE','INVALID_UPSTREAM_RESPONSE'];
      const code=abort.signal.aborted?'TIMEOUT_OR_CANCELLED':error?.gatewayCode??(safe.includes(error?.message)?error.message:'UPSTREAM_UNAVAILABLE');
      reply(abort.signal.aborted?504:error?.gatewayCode?error.status:code==='DUPLICATE_JOB'?409:code==='QUOTA_EXHAUSTED'||code==='CAPACITY'||code==='UPSTREAM_QUOTA'?429:code==='INVALID_AUDIO'?400:502,{error:code});
    }finally {
      clearTimeout(expire);if(pending&&!settled)pending.then(release,release);else release();
    }
  });
  server.requestTimeout=options.requestTimeoutMs;server.headersTimeout=Math.min(10000,options.requestTimeoutMs);server.keepAliveTimeout=5000;
  server.maxConnections=64;
  server.on('clientError',(_error,socket)=>{socket.end('HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n');});
  return server;
}
