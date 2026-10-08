import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
const owner='11111111-1111-4111-8111-111111111111',token='22222222-2222-4222-8222-222222222222';
const now=1700000000000;
async function fixture(){const root=fs.mkdtempSync(path.join(os.tmpdir(),'oe-gateway-auth-')),dir=path.join(root,'service');
  const {issueStandaloneSession}=await api();issueStandaloneSession(dir,owner,{now});const file=path.join(dir,'sessions.json');
  function write(sessions){fs.writeFileSync(file,JSON.stringify({schema:'single-player-gateway-sessions@1',sessions}),{mode:0o600});}
  function entry(changes={}){return {token_sha256:createHash('sha256').update(token).digest('hex'),owner_id:owner,expires_at_unix_ms:now+3600000,...changes};}
  write([entry()]);return {dir,root,file,write,entry,close:()=>fs.rmSync(root,{recursive:true,force:true})};}
async function api(){const module=await import('./standalone-auth.mjs');assert.equal(typeof module.createStandaloneAuthenticator,'function');return module;}
test('standalone session authenticates without Rust or network and revocation is immediate',async()=>{
  const f=await fixture();try{const {createStandaloneAuthenticator}=await api();const authenticate=createStandaloneAuthenticator({sessionPath:f.file,authorizedOwners:[owner],now:()=>now});
    assert.equal(await authenticate(token,new AbortController().signal),owner);f.write([]);assert.equal(await authenticate(token,new AbortController().signal),null);
    assert.equal(await authenticate(' '+token,new AbortController().signal),null);
  }finally{f.close();}
});
test('expired foreign duplicate malformed and excessive-lifetime registries fail closed',async()=>{
  const f=await fixture();try{const {createStandaloneAuthenticator}=await api();const authenticate=createStandaloneAuthenticator({sessionPath:f.file,authorizedOwners:[owner],now:()=>now});
    for(const entries of [[f.entry({expires_at_unix_ms:now})],[f.entry({owner_id:'33333333-3333-4333-8333-333333333333'})],
      [f.entry(),f.entry()],[f.entry({token_sha256:'bad'})],[f.entry({expires_at_unix_ms:now+86400001})]]){
      f.write(entries);assert.equal(await authenticate(token,new AbortController().signal),null);
    }
    fs.writeFileSync(f.file,'x'.repeat(32769));assert.equal(await authenticate(token,new AbortController().signal),null);
    fs.writeFileSync(f.file,Buffer.from([0xff]));assert.equal(await authenticate(token,new AbortController().signal),null);
  }finally{f.close();}
});
test('standalone adapter mode never calls the backend identity endpoint',async()=>{
  const f=await fixture();try{const {createAdapters}=await import('./upstream.mjs');let calls=0;
    const adapters=createAdapters({authMode:'standalone',sessionPath:f.file,authorizedOwners:[owner],now:()=>now,fetchImpl:()=>{calls++;throw new Error('must not call');}});
    assert.equal(await adapters.authenticate(token,new AbortController().signal),owner);assert.equal(calls,0);
  }finally{f.close();}
});
test('offline issuer writes only private files, exposes no token in result and refuses overwrite',async()=>{
  const f=await fixture();try{const {issueStandaloneSession}=await api();const issueDir=path.join(f.root,'issued');
    const issued=issueStandaloneSession(issueDir,owner,{now,ttlMs:3600000});assert.equal(issued.ownerId,owner);assert.equal(issued.token,undefined);
    const privateToken=fs.readFileSync(issued.tokenPath,'utf8').trim();const authenticate=(await api()).createStandaloneAuthenticator({sessionPath:issued.registryPath,authorizedOwners:[owner],now:()=>now});
    assert.equal(await authenticate(privateToken,new AbortController().signal),owner);
    assert.throws(()=>issueStandaloneSession(issueDir,owner,{now}),{message:'SESSION_ISSUE_FAILED'});
    assert.equal(fs.readFileSync(issued.tokenPath,'utf8').trim(),privateToken);
  }finally{f.close();}
});
test('real loopback gateway enforces standalone auth, revocation and unchanged owner budget',async()=>{
  const f=await fixture();let server;try{
    const {createStandaloneAuthenticator}=await api(),{createGateway}=await import('./gateway.mjs');let calls=0;
    server=createGateway({authenticate:createStandaloneAuthenticator({sessionPath:f.file,authorizedOwners:[owner],now:()=>now}),
      transcribe:async()=>{throw new Error('unexpected');},semantic:async()=>{calls++;return {choices:[{finish_reason:'stop',message:{content:'{"outcome":"Answer","message":"fixture"}'}}]};},
      policy:{version:'fixture',provider:'fixture',model:'fixture',retention:'fixture'},allowedOrigins:['https://game.example.invalid'],authorizedOwners:[owner],
      ledgerPath:path.join(f.dir,'budget.json'),maxAudioSeconds:600,maxPerOwnerAudioSeconds:600,maxSemanticRequests:6,maxEstimatedUsd:1,audioUsdPerSecond:0.001,semanticUsdPerRequest:0.01});
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const base='http://127.0.0.1:'+server.address().port;
    const body=JSON.stringify({model:'openai/gpt-6-luna',messages:[{role:'user',content:'fixture'}],max_tokens:1});
    const headers={Authorization:'Bearer '+token,Origin:'https://game.example.invalid','Content-Type':'application/json','X-Commander-Job':'44444444-4444-4444-8444-444444444444'};
    assert.equal((await fetch(base+'/api/commander/semantic',{method:'POST',headers,body})).status,200);
    f.write([]);assert.equal((await fetch(base+'/api/commander/semantic',{method:'POST',headers:{...headers,'X-Commander-Job':'55555555-5555-4555-8555-555555555555'},body})).status,401);
    assert.equal(calls,1);const budget=fs.readFileSync(path.join(f.dir,'budget.json'),'utf8');assert.ok(!budget.includes(token));assert.ok(budget.includes(owner));
  }finally{if(server)await new Promise(resolve=>server.close(resolve));f.close();}
});
test('startup validation rejects missing malformed and expired session stores',async()=>{
  const f=await fixture();try{const {createAdapters}=await import('./upstream.mjs');const adapters=createAdapters({authMode:'standalone',sessionPath:f.file,authorizedOwners:[owner],now:()=>now});
    assert.equal(typeof adapters.validateAuthentication,'function');await adapters.validateAuthentication();
    for(const contents of ['invalid',JSON.stringify({schema:'single-player-gateway-sessions@1',sessions:[f.entry({expires_at_unix_ms:now})]})]){
      fs.writeFileSync(f.file,contents);await assert.rejects(adapters.validateAuthentication(),{message:'GATEWAY_CONFIG_INVALID'});
    }
    fs.unlinkSync(f.file);await assert.rejects(adapters.validateAuthentication(),{message:'GATEWAY_CONFIG_INVALID'});
  }finally{f.close();}
});
test('Windows refuses an existing public issuer directory and a publicly readable registry',{skip:process.platform!=='win32'},async()=>{
  const f=await fixture();try{const {issueStandaloneSession,createStandaloneAuthenticator}=await api();const publicDir=path.join(f.dir,'public');fs.mkdirSync(publicDir);
    const grant=spawnSync('icacls.exe',[publicDir,'/grant','*S-1-1-0:(OI)(CI)R'],{windowsHide:true,stdio:'pipe'});assert.equal(grant.status,0);
    assert.throws(()=>issueStandaloneSession(publicDir,owner,{now}),{message:'SESSION_ISSUE_FAILED'});
    assert.equal(fs.existsSync(path.join(publicDir,'session-token.txt')),false);
    const registryGrant=spawnSync('icacls.exe',[f.file,'/grant','*S-1-1-0:R'],{windowsHide:true,stdio:'pipe'});assert.equal(registryGrant.status,0);
    assert.equal(await createStandaloneAuthenticator({sessionPath:f.file,authorizedOwners:[owner],now:()=>now})(token,new AbortController().signal),null);
  }finally{f.close();}
});
