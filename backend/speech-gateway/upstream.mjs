import {createStandaloneAuthenticator} from './standalone-auth.mjs';
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function validateAuthBase(value) {
  let url;try{url=new URL(value);}catch{throw new Error('GATEWAY_CONFIG_INVALID');}
  if(url.username||url.password||url.search||url.hash||url.pathname!=='/'
    ||url.protocol!=='https:'&&!(url.protocol==='http:'&&['127.0.0.1','localhost','[::1]'].includes(url.hostname)))throw new Error('GATEWAY_CONFIG_INVALID');
  return url.origin;
}
async function boundedJson(response,signal,limit=65536) {
  const declared=response.headers.get('content-length');
  if(declared&&(!/^\d+$/.test(declared)||Number(declared)>limit)){await response.body?.cancel();throw new Error('RESPONSE_TOO_LARGE');}
  if(!response.body)throw new Error('INVALID_UPSTREAM_RESPONSE');
  const reader=response.body.getReader(),chunks=[];let size=0;
  try {
    for(;;){signal.throwIfAborted();const {done,value}=await reader.read();if(done)break;
      size+=value.byteLength;if(size>limit)throw new Error('RESPONSE_TOO_LARGE');chunks.push(value);}
    signal.throwIfAborted();let json;
    try{json=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(Buffer.concat(chunks,size)));}
    catch{throw new Error('INVALID_UPSTREAM_RESPONSE');}return json;
  }finally{await reader.cancel().catch(()=>{});reader.releaseLock();}
}
export function createAdapters({authBaseUrl,authMode='backend',sessionPath,authorizedOwners,now,openaiKey,openrouterKey,fetchImpl=fetch}) {
  if(!['backend','standalone'].includes(authMode))throw new Error('GATEWAY_CONFIG_INVALID');
  const authenticateStandalone=authMode==='standalone'?createStandaloneAuthenticator({sessionPath,authorizedOwners,now}):null;
  const authBase=authMode==='backend'?validateAuthBase(authBaseUrl):null;
  async function send(url,options,limit) {
    const response=await fetchImpl(url,{...options,redirect:'error'});
    if(!response.ok){await response.body?.cancel();throw new Error(response.status===401||response.status===403?'UPSTREAM_AUTH':response.status===429?'UPSTREAM_QUOTA':'UPSTREAM_UNAVAILABLE');}
    return boundedJson(response,options.signal,limit);
  }
  return {
    async validateAuthentication(){if(authenticateStandalone)await authenticateStandalone.validateStore();},
    async authenticate(token,signal) {
      if(authenticateStandalone)return authenticateStandalone(token,signal);
      if(typeof token!=='string'||!uuid.test(token))return null;
      try{const body=await send(authBase+'/api/auth/session',{headers:{Authorization:'Bearer '+token},signal},4096);
        return typeof body?.player_id==='string'&&uuid.test(body.player_id)?body.player_id:null;
      }catch(error){if(error.message==='UPSTREAM_AUTH')return null;throw error;}
    },
    async transcribe({bytes,language,signal}) {
      if(!openaiKey)throw new Error('UPSTREAM_UNAVAILABLE');
      const form=new FormData();form.append('file',new Blob([bytes],{type:'audio/wav'}),'recording.wav');
      form.append('model','gpt-transcribe');form.append('language',language);form.append('response_format','json');
      const body=await send('https://api.openai.com/v1/audio/transcriptions',{method:'POST',headers:{Authorization:'Bearer '+openaiKey},body:form,signal});
      if(typeof body?.text!=='string'||body.text.length>4096)throw new Error('INVALID_UPSTREAM_RESPONSE');return body.text;
    },
    async semantic({body,signal}) {
      if(!openrouterKey)throw new Error('UPSTREAM_UNAVAILABLE');
      return send('https://openrouter.ai/api/v1/chat/completions',{method:'POST',headers:{Authorization:'Bearer '+openrouterKey,'Content-Type':'application/json'},body:JSON.stringify(body),signal});
    }
  };
}
