import {test} from 'node:test';
import assert from 'node:assert/strict';
const enabled={COMMANDER_GATEWAY_ENABLED:'true',COMMANDER_AUTH_BASE_URL:'http://127.0.0.1:8080',
  COMMANDER_GATEWAY_ORIGINS:'https://game.example.invalid',COMMANDER_GATEWAY_OWNER_IDS:'11111111-1111-4111-8111-111111111111',
  COMMANDER_GATEWAY_LEDGER_PATH:'./private-state/budget.json',COMMANDER_GATEWAY_MAX_USD:'0.5',
  COMMANDER_AUDIO_USD_PER_SECOND:'0.001',COMMANDER_SEMANTIC_USD_PER_REQUEST:'0.05',
  COMMANDER_GATEWAY_POLICY_VERSION:'test-policy',COMMANDER_GATEWAY_OPERATOR:'Test operator',
  COMMANDER_UPSTREAM_RETENTION_DISCLOSURE:'Operator must verify vendor policy; not a zero-retention claim',
  COMMANDER_OPENAI_API_KEY:'fake-audio-key',COMMANDER_OPENROUTER_API_KEY:'fake-semantic-key'};
test('gateway is disabled by default and cannot enable paid routes with absent ceiling or keys',async()=>{
  const {loadConfiguration}=await import('./config.mjs');assert.deepEqual(loadConfiguration({}),{enabled:false});
  for(const key of ['COMMANDER_GATEWAY_MAX_USD','COMMANDER_AUDIO_USD_PER_SECOND','COMMANDER_GATEWAY_OWNER_IDS','COMMANDER_OPENAI_API_KEY','COMMANDER_OPENROUTER_API_KEY']){
    assert.throws(()=>loadConfiguration({...enabled,[key]:''}),{message:'GATEWAY_CONFIG_INVALID'});
  }
});
test('enabled configuration rejects insecure remote auth, wildcard origins and pricing that cannot cap spending',async()=>{
  const {loadConfiguration}=await import('./config.mjs');
  for(const changes of [{COMMANDER_AUTH_BASE_URL:'http://remote.example.invalid'},
    {COMMANDER_GATEWAY_ORIGINS:'*'},{COMMANDER_AUTH_BASE_URL:'https://user:password@example.invalid/'},
    {COMMANDER_AUDIO_USD_PER_SECOND:'0'},{COMMANDER_SEMANTIC_USD_PER_REQUEST:'NaN'},
    {COMMANDER_GATEWAY_MAX_AUDIO_SECONDS:'601'},{COMMANDER_GATEWAY_MAX_SEMANTIC_REQUESTS:'7'}]){
    assert.throws(()=>loadConfiguration({...enabled,...changes}),{message:'GATEWAY_CONFIG_INVALID'});
  }
  const config=loadConfiguration(enabled);assert.equal(config.maxAudioSeconds,600);assert.equal(config.maxSemanticRequests,6);
  assert.equal(config.policy.model,'gpt-transcribe');assert.equal(config.maxEstimatedUsd,0.5);
});
test('single-player standalone mode configures private sessions without any Rust auth URL',async()=>{
  const {loadConfiguration}=await import('./config.mjs');const values={...enabled,COMMANDER_AUTH_MODE:'standalone',COMMANDER_STANDALONE_SESSIONS_PATH:'./private-state/sessions.json'};
  delete values.COMMANDER_AUTH_BASE_URL;const config=loadConfiguration(values);
  assert.equal(config.authMode,'standalone');assert.equal(config.authBaseUrl,undefined);assert.match(config.sessionPath,/sessions\.json$/);
  assert.throws(()=>loadConfiguration({...values,COMMANDER_AUTH_MODE:'public'}),{message:'GATEWAY_CONFIG_INVALID'});
  assert.throws(()=>loadConfiguration({...values,COMMANDER_STANDALONE_SESSIONS_PATH:''}),{message:'GATEWAY_CONFIG_INVALID'});
});
