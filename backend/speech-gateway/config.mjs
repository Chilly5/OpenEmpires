import path from 'node:path';
import {validateAuthBase} from './upstream.mjs';
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function invalid(){throw new Error('GATEWAY_CONFIG_INVALID');}
export function loadConfiguration(values) {
  if(values.COMMANDER_GATEWAY_ENABLED!=='true')return {enabled:false};
  function text(key,max=512){const v=values[key];if(typeof v!=='string'||!v.trim()||v.length>max||/[\x00-\x1f]/.test(v))invalid();return v;}
  function positive(key,fallback,max=Infinity){const v=Number(values[key]??fallback);if(!Number.isFinite(v)||v<=0||v>max)invalid();return v;}
  function count(key,fallback,max){const v=positive(key,fallback,max);if(!Number.isInteger(v))invalid();return v;}
  const allowedOrigins=text('COMMANDER_GATEWAY_ORIGINS',4096).split(',');
  if(allowedOrigins.length>16)invalid();
  for(const value of allowedOrigins){if(validateAuthBase(value)!==value)invalid();}
  const authorizedOwners=text('COMMANDER_GATEWAY_OWNER_IDS',5000).split(',');
  if(authorizedOwners.length>128||authorizedOwners.some(id=>!uuid.test(id))||new Set(authorizedOwners).size!==authorizedOwners.length)invalid();
  const version=text('COMMANDER_GATEWAY_POLICY_VERSION',80);if(!/^[a-zA-Z0-9.-]+$/.test(version))invalid();
  const authMode=values.COMMANDER_AUTH_MODE??'backend';
  if(!['backend','standalone'].includes(authMode))invalid();
  const authBaseUrl=authMode==='backend'?validateAuthBase(text('COMMANDER_AUTH_BASE_URL',1024)):undefined;
  const sessionPath=authMode==='standalone'?path.resolve(text('COMMANDER_STANDALONE_SESSIONS_PATH',1024)):undefined;
  return {enabled:true,authMode,authBaseUrl,sessionPath,allowedOrigins,authorizedOwners,
    openaiKey:text('COMMANDER_OPENAI_API_KEY'),openrouterKey:text('COMMANDER_OPENROUTER_API_KEY'),
    ledgerPath:path.resolve(text('COMMANDER_GATEWAY_LEDGER_PATH',1024)),
    port:count('COMMANDER_GATEWAY_PORT',8082,65535),
    maxEstimatedUsd:positive('COMMANDER_GATEWAY_MAX_USD'),
    audioUsdPerSecond:positive('COMMANDER_AUDIO_USD_PER_SECOND'),
    semanticUsdPerRequest:positive('COMMANDER_SEMANTIC_USD_PER_REQUEST'),
    maxAudioSeconds:count('COMMANDER_GATEWAY_MAX_AUDIO_SECONDS',600,600),
    maxPerOwnerAudioSeconds:count('COMMANDER_GATEWAY_MAX_PER_OWNER_AUDIO_SECONDS',600,600),
    maxSemanticRequests:count('COMMANDER_GATEWAY_MAX_SEMANTIC_REQUESTS',6,6),maxPerOwnerSemanticRequests:6,
    policy:{version,operator:text('COMMANDER_GATEWAY_OPERATOR',128),provider:'OpenAI',model:'gpt-transcribe',
      retention:text('COMMANDER_UPSTREAM_RETENTION_DISCLOSURE',2048),
      purpose:'Audio is sent to the operator gateway and OpenAI for transcription only. Microphone permission is not upload consent.',
      applicationRetention:'No application audio or transcript retention. Durable quota counters only.'}
  };
}
