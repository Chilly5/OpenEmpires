import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
const source=fs.readFileSync(new URL('../../../Assets/Plugins/WebGL/WebSocket.jslib',import.meta.url),'utf8');
function fixture(){
  const sockets=[],messages=[],library={};
  class Socket{static OPEN=1;constructor(){this.readyState=1;sockets.push(this);}close(){this.readyState=3;}send(){}}
  const context={LibraryManager:{library},mergeInto:(a,b)=>Object.assign(a,b),UTF8ToString:x=>x,window:{},WebSocket:Socket,
    SendMessage:(...args)=>messages.push(args),document:{addEventListener(){},removeEventListener(){}},setInterval:()=>1,clearInterval(){}};
  vm.runInNewContext(source,context);return {sockets,messages,library,context};
}
test('replaced socket callbacks cannot affect the current connection',()=>{
  const f=fixture();f.library.WebSocketConnect('ws://localhost/one','fixture');const old=f.sockets[0];
  f.library.WebSocketConnect('ws://localhost/two','fixture');const current=f.sockets[1];
  old.onopen();old.onmessage({data:'stale'});old.onerror();old.onclose({reason:'stale'});
  assert.equal(f.messages.length,0);assert.equal(f.context.window._oeWebSocket,current);
  current.onopen();assert.equal(f.messages.length,1);
});
test('closed socket callbacks cannot reopen or deliver late messages',()=>{
  const f=fixture();f.library.WebSocketConnect('ws://localhost','fixture');const old=f.sockets[0];f.library.WebSocketClose();
  old.onopen();old.onmessage({data:'stale'});old.onerror();old.onclose({reason:'stale'});assert.equal(f.messages.length,0);
});
