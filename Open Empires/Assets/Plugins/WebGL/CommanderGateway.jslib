mergeInto(LibraryManager.library, {
  $CommanderGatewayRuntime: {
    jobs: {}, active: 0,
    notify: function (entry, status, error) {
      if (entry.cancelled || entry.notified) return;
      entry.notified = true;
      SendMessage(entry.receiver, 'OnCommanderGatewayResponse',
        JSON.stringify({jobId:entry.id,status:status,error:error || ''}));
    },
    cancel: function (id) {
      var entry=CommanderGatewayRuntime.jobs[id];if(!entry)return;
      entry.cancelled=true;entry.controller.abort();clearTimeout(entry.timer);
      entry.bytes=null;delete CommanderGatewayRuntime.jobs[id];
    },
    begin: function(receiver,id,base,token,policy,language,pointer,length,kind) {
      var state=CommanderGatewayRuntime;
      var entry={receiver:receiver,id:id,controller:new AbortController(),cancelled:false,notified:false,bytes:null};
      var uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
      var url;
      try {
        url=new URL(base);
        if(!uuid.test(id)||receiver.length>128||url.username||url.password||url.search||url.hash||url.pathname!=='/'
          ||url.protocol!=='https:'&&!(url.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].indexOf(url.hostname)>=0)
          ||[0,1,2].indexOf(kind)<0||!Number.isInteger(length)||length<0||length>(kind===1?2097152:65536)
          ||kind!==0&&(!uuid.test(token)||policy.length>80||language.length>8))throw new Error('setup');
      }catch(_){state.notify(entry,0,'GATEWAY_SETUP_REQUIRED');return;}
      if(state.jobs[id]){state.notify(entry,0,'DUPLICATE_JOB');return;}
      if(state.active>=2||Object.keys(state.jobs).length>=2){state.notify(entry,0,'STT_BUSY');return;}
      state.jobs[id]=entry;state.active++;
      var path=kind===0?'/api/commander/voice-policy':kind===1?'/api/commander/stt':'/api/commander/semantic';
      var headers={},body;
      if(kind!==0){headers.Authorization='Bearer '+token;body=new Uint8Array(HEAPU8.subarray(pointer,pointer+length));}
      if(kind===1){headers['Content-Type']='audio/wav';headers['X-Voice-Job']=id;headers['X-Voice-Consent']=policy;headers['X-Voice-Language']=language;}
      if(kind===2){headers['Content-Type']='application/json';headers['X-Commander-Job']=id;}
      entry.timer=setTimeout(function(){entry.controller.abort();state.notify(entry,0,'TIMEOUT_OR_CANCELLED');
        entry.bytes=null;delete state.jobs[id];},30000);
      // Actual work owns its slot until finally, even after logical cancellation.
      (async function(){
        var reader,response;
        try {
          response=await fetch(url.origin+path,{method:kind===0?'GET':'POST',headers:headers,body:body,
            signal:entry.controller.signal,redirect:'error',credentials:'omit',cache:'no-store'});
          if(entry.cancelled||entry.controller.signal.aborted)return;
          var declared=response.headers.get('content-length');
          if(declared&&(!/^\d+$/.test(declared)||Number(declared)>65536))throw new Error('RESPONSE_TOO_LARGE');
          if(!response.body||!response.body.getReader)throw new Error('TRANSPORT_UNAVAILABLE');
          reader=response.body.getReader();var chunks=[],size=0;
          for(;;){var result=await reader.read();if(entry.cancelled||entry.controller.signal.aborted)return;
            if(result.done)break;size+=result.value.byteLength;if(size>65536)throw new Error('RESPONSE_TOO_LARGE');
            chunks.push(result.value);if(chunks.length>1024)throw new Error('RESPONSE_TOO_LARGE');}
          var bytes=new Uint8Array(size),offset=0;for(var i=0;i<chunks.length;i++){bytes.set(chunks[i],offset);offset+=chunks[i].byteLength;}
          entry.bytes=bytes;state.notify(entry,response.status,'');
        }catch(error){
          entry.bytes=null;
          var reason=error&&error.message==='RESPONSE_TOO_LARGE'?'RESPONSE_TOO_LARGE':entry.controller.signal.aborted?'TIMEOUT_OR_CANCELLED':'GATEWAY_NETWORK_ERROR';
          entry.controller.abort();state.notify(entry,0,reason);
          delete state.jobs[id];
        }finally{
          if(reader){try{await reader.cancel();}catch(_){}reader.releaseLock();}
          else if(response&&response.body){try{await response.body.cancel();}catch(_){}}
          if(entry.cancelled||entry.controller.signal.aborted)delete state.jobs[id];
          clearTimeout(entry.timer);body=null;state.active--;
        }
      })();
    }
  },
  CommanderGateway_Begin__deps:['$CommanderGatewayRuntime'],
  CommanderGateway_Begin:function(receiver,id,base,token,policy,language,pointer,length,kind){
    CommanderGatewayRuntime.begin(UTF8ToString(receiver),UTF8ToString(id),UTF8ToString(base),UTF8ToString(token),
      UTF8ToString(policy),UTF8ToString(language),pointer,length,kind);
  },
  CommanderGateway_Cancel__deps:['$CommanderGatewayRuntime'],
  CommanderGateway_Cancel:function(id){CommanderGatewayRuntime.cancel(UTF8ToString(id));},
  CommanderGateway_Dispose__deps:['$CommanderGatewayRuntime'],
  CommanderGateway_Dispose:function(receiver){var name=UTF8ToString(receiver);for(var id in CommanderGatewayRuntime.jobs)
    if(CommanderGatewayRuntime.jobs[id].receiver===name)CommanderGatewayRuntime.cancel(id);},
  CommanderGateway_ResponseSize__deps:['$CommanderGatewayRuntime'],
  CommanderGateway_ResponseSize:function(id){var entry=CommanderGatewayRuntime.jobs[UTF8ToString(id)];return entry&&entry.bytes?entry.bytes.length:-1;},
  CommanderGateway_ReadResponse__deps:['$CommanderGatewayRuntime'],
  CommanderGateway_ReadResponse:function(id,pointer,capacity){var key=UTF8ToString(id),entry=CommanderGatewayRuntime.jobs[key];
    if(!entry||!entry.bytes||capacity<entry.bytes.length)return -1;
    var length=entry.bytes.length;HEAPU8.set(entry.bytes,pointer);entry.bytes=null;delete CommanderGatewayRuntime.jobs[key];return length;}
});
