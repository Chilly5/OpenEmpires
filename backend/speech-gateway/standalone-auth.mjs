import fs from 'node:fs';
import path from 'node:path';
import {createHash,randomUUID,timingSafeEqual} from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const hashPattern=/^[0-9a-f]{64}$/;
const maximumBytes=32768,maximumSessions=128,maximumLifetimeMs=86400000;
const schema='single-player-gateway-sessions@1';
function digest(token){return createHash('sha256').update(token,'utf8').digest();}
function exact(object,fields){return object&&typeof object==='object'&&!Array.isArray(object)
  &&Object.keys(object).length===fields.length&&fields.every(field=>Object.hasOwn(object,field));}
function privateParent(file){const parent=fs.lstatSync(path.dirname(file));if(!parent.isDirectory()||parent.isSymbolicLink())throw new Error('SESSION_STORE_INVALID');}
function windowsPrivate(file,protect=false){
  const encodedPath=Buffer.from(file,'utf8').toString('base64');
  const script=`$ErrorActionPreference='Stop';$taskPath=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('${encodedPath}'));$taskSid=[Security.Principal.WindowsIdentity]::GetCurrent().User;
${protect?`$taskAcl=New-Object Security.AccessControl.DirectorySecurity;$taskAcl.SetAccessRuleProtection($true,$false);$taskRule=New-Object Security.AccessControl.FileSystemAccessRule($taskSid,'FullControl','ContainerInherit,ObjectInherit','None','Allow');$taskAcl.AddAccessRule($taskRule);[IO.Directory]::SetAccessControl($taskPath,$taskAcl);`:''}
$taskAcl=if([IO.Directory]::Exists($taskPath)){[IO.Directory]::GetAccessControl($taskPath)}else{[IO.File]::GetAccessControl($taskPath)};$taskSafe=@($taskSid.Value,'S-1-5-18','S-1-5-32-544');if($taskSafe -notcontains $taskAcl.GetOwner([Security.Principal.SecurityIdentifier]).Value){exit 2};foreach($taskRule in $taskAcl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){if($taskRule.AccessControlType -eq 'Allow' -and $taskRule.PropagationFlags -ne 'InheritOnly' -and $taskSafe -notcontains $taskRule.IdentityReference.Value){exit 2}};'PRIVATE';`;
  const executable=path.join(process.env.SystemRoot??'C:\\Windows','System32','WindowsPowerShell','v1.0','powershell.exe');
  const result=spawnSync(executable,['-NoProfile','-NonInteractive','-EncodedCommand',Buffer.from(script,'utf16le').toString('base64')],
    {windowsHide:true,encoding:'utf8',timeout:5000,maxBuffer:4096});
  return result.status===0&&result.stdout.trim()==='PRIVATE';
}

// Service capability owner selects quotas only, never Unity identity/authority.
export function createStandaloneAuthenticator({sessionPath,authorizedOwners,now=Date.now}) {
  if(typeof sessionPath!=='string'||!sessionPath||!Array.isArray(authorizedOwners)||authorizedOwners.length>maximumSessions
    ||authorizedOwners.some(owner=>typeof owner!=='string'||!uuid.test(owner)))throw new Error('GATEWAY_CONFIG_INVALID');
  const file=path.resolve(sessionPath),owners=new Set(authorizedOwners);
  async function inspect(token,signal) {
    signal?.throwIfAborted();
    let handle;
    try {
      privateParent(file);const before=fs.lstatSync(file);if(!before.isFile()||before.isSymbolicLink())return null;
      if(process.platform==='win32'&&!windowsPrivate(file))return null;
      handle=await fs.promises.open(file,fs.constants.O_RDONLY|(fs.constants.O_NOFOLLOW??0));
      const stat=await handle.stat();
      if(!stat.isFile()||stat.nlink!==1||stat.size>maximumBytes||stat.dev!==before.dev||stat.ino!==before.ino
        ||process.platform!=='win32'&&(stat.mode&0o077)!==0)return null;
      const buffer=Buffer.alloc(maximumBytes+1);let bytes=0;
      while(bytes<buffer.length){signal?.throwIfAborted();const read=await handle.read(buffer,bytes,buffer.length-bytes,null);if(read.bytesRead===0)break;bytes+=read.bytesRead;}
      if(bytes>maximumBytes)return null;
      const data=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(buffer.subarray(0,bytes))),time=now();
      if(!Number.isSafeInteger(time)||!exact(data,['schema','sessions'])||data.schema!==schema
        ||!Array.isArray(data.sessions)||data.sessions.length>maximumSessions)return null;
      const seen=new Set(),candidate=token===null?null:digest(token);let owner=null,activeCount=0;
      for(const entry of data.sessions){
        if(!exact(entry,['token_sha256','owner_id','expires_at_unix_ms'])||!hashPattern.test(entry.token_sha256)
          ||!owners.has(entry.owner_id)||!Number.isSafeInteger(entry.expires_at_unix_ms)
          ||entry.expires_at_unix_ms>time+maximumLifetimeMs||seen.has(entry.token_sha256))return null;
        seen.add(entry.token_sha256);
        if(entry.expires_at_unix_ms>time){activeCount++;if(candidate&&timingSafeEqual(candidate,Buffer.from(entry.token_sha256,'hex')))owner=entry.owner_id;}
      }
      signal?.throwIfAborted();return {owner,activeCount};
    }catch{signal?.throwIfAborted();return null;}
    finally{await handle?.close().catch(()=>{});}
  }
  const authenticate=async(token,signal)=>{
    signal?.throwIfAborted();if(typeof token!=='string'||token.length!==36||!uuid.test(token))return null;
    return (await inspect(token,signal))?.owner??null;
  };
  authenticate.validateStore=async()=>{const result=await inspect(null);if(!result||result.activeCount===0)throw new Error('GATEWAY_CONFIG_INVALID');};
  return authenticate;
}

// Explicit offline operator action; exclusive private files, no public enrollment,
// listener/vendor calls or token in console/results. Never resets a spending ledger.
export function issueStandaloneSession(directory,owner,{now=Date.now(),ttlMs=3600000}={}) {
  let registryCreated=false,tokenCreated=false,registryPath,tokenPath;
  try {
    if(typeof directory!=='string'||!directory||typeof owner!=='string'||!uuid.test(owner)
      ||!Number.isSafeInteger(now)||!Number.isSafeInteger(ttlMs)||ttlMs<=0||ttlMs>maximumLifetimeMs)throw new Error();
    const root=path.resolve(directory),created=!fs.existsSync(root);if(root===path.parse(root).root)throw new Error();
    fs.mkdirSync(root,{recursive:true,mode:0o700});
    registryPath=path.join(root,'sessions.json');tokenPath=path.join(root,'session-token.txt');privateParent(registryPath);
    if(process.platform==='win32'){if(!windowsPrivate(root,created))throw new Error();}
    else if((fs.statSync(root).mode&0o077)!==0)throw new Error();
    if(fs.existsSync(registryPath)||fs.existsSync(tokenPath))throw new Error();
    const token=randomUUID(),expiresAt=now+ttlMs;if(!Number.isSafeInteger(expiresAt))throw new Error();
    const registry={schema,sessions:[{token_sha256:digest(token).toString('hex'),owner_id:owner,expires_at_unix_ms:expiresAt}]};
    let fd=fs.openSync(registryPath,'wx',0o600);registryCreated=true;
    try{fs.writeFileSync(fd,JSON.stringify(registry));fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
    fd=fs.openSync(tokenPath,'wx',0o600);tokenCreated=true;
    try{fs.writeFileSync(fd,token+'\n');fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
    if(process.platform==='win32'&&(!windowsPrivate(registryPath)||!windowsPrivate(tokenPath)))throw new Error();
    return {ownerId:owner,expiresAt,registryPath,tokenPath};
  }catch{
    if(tokenCreated)try{fs.unlinkSync(tokenPath);}catch{}
    if(registryCreated)try{fs.unlinkSync(registryPath);}catch{}
    throw new Error('SESSION_ISSUE_FAILED');
  }
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  try{if(process.argv.length!==4)throw new Error();issueStandaloneSession(process.argv[2],process.argv[3]);
    console.log('Session issued privately. Copy session-token.txt into masked Service session entry; no token is printed.');
  }catch{console.error('SESSION_ISSUE_FAILED');process.exitCode=1;}
}
