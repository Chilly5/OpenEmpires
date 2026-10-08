mergeInto(LibraryManager.library,{
  $CommanderMicRuntime:{
    job:null,context:null,owner:null,granted:false,rawPending:0,devices:[],handlers:null,
    signal:function(job,stage,code){if(job.cancelled&&stage!=='cancelled')return;
      SendMessage(job.receiver,'OnCommanderMicrophoneState',JSON.stringify({jobId:job.id,stage:stage,code:code||''}));},
    stopTracks:function(stream){if(stream&&stream.getTracks)stream.getTracks().forEach(function(track){track.onended=null;track.stop();});},
    closeContext:function(){var state=CommanderMicRuntime,ctx=state.context;state.context=null;
      if(ctx){ctx.onstatechange=null;try{ctx.close().catch(function(){});}catch(_){}}},
    contextFor:function(owner){var state=CommanderMicRuntime;state.owner=owner;
      if(!state.context||state.context.state==='closed'){var Type=window.AudioContext||window.webkitAudioContext;
        if(!Type)throw new Error('MIC_UNSUPPORTED');state.context=new Type();}
      // This call occurs at the deliberate activation/record action, before awaits.
      try{state.context.resume().catch(function(){});}catch(_){}
      state.attach();return state.context;},
    attach:function(){var state=CommanderMicRuntime;if(state.handlers)return;
      var lost=function(){if(state.job)state.cancel(state.job.id,'MIC_FOCUS_LOST');};
      var visible=function(){if(document.hidden)lost();};
      var up=function(event){var job=state.job;if(job&&!job.permission&&job.key&&event.code===job.key)state.stop(job.id);};
      var gesture=function(event){if(event.isTrusted!==false&&state.granted&&state.context&&state.context.state==='suspended')
        state.context.resume().catch(function(){});};
      state.handlers={blur:lost,pagehide:lost,visibilitychange:visible,keyup:up,keydown:gesture,pointerdown:gesture};
      Object.keys(state.handlers).forEach(function(name){(name==='visibilitychange'?document:window).addEventListener(name,state.handlers[name]);});},
    detach:function(){var state=CommanderMicRuntime;if(!state.handlers)return;
      Object.keys(state.handlers).forEach(function(name){(name==='visibilitychange'?document:window).removeEventListener(name,state.handlers[name]);});state.handlers=null;},
    make:function(receiver,id,permission){var state=CommanderMicRuntime;
      if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)||receiver.length>128)return null;
      var job={receiver:receiver,id:id,permission:permission,cancelled:false,stopping:false,listening:false,done:false,code:'',
        stream:null,node:null,source:null,gain:null,chunks:[],frames:0,sequence:0,rate:0,maximum:0,timer:null};
      if(state.job||state.rawPending){state.signal(job,'error','MIC_BUSY');return null;}state.job=job;return job;},
    permission:function(receiver,id){var state=CommanderMicRuntime,job=state.make(receiver,id,true);if(!job)return;
      if(!navigator.mediaDevices||!navigator.mediaDevices.getUserMedia){state.signal(job,'error','MIC_UNSUPPORTED');state.job=null;return;}
      try{state.contextFor(receiver);}catch(_){state.signal(job,'error','MIC_UNSUPPORTED');state.job=null;return;}
      state.rawPending++;
      navigator.mediaDevices.getUserMedia({audio:true,video:false}).then(function(stream){
        state.stopTracks(stream);if(job.cancelled||state.job!==job)return;
        state.granted=true;return navigator.mediaDevices.enumerateDevices().then(function(devices){
          if(job.cancelled||state.job!==job)return;state.devices=devices.filter(function(d){return d.kind==='audioinput';}).slice(0,16);
          state.signal(job,'permission','');state.job=null;});
      }).catch(function(){if(!job.cancelled){state.granted=false;state.signal(job,'error','MIC_PERMISSION_DENIED');state.job=null;}})
        .finally(function(){state.rawPending--;});},
    cleanupNodes:function(job){var state=CommanderMicRuntime;state.stopTracks(job.stream);job.stream=null;
      [job.source,job.node,job.gain].forEach(function(node){if(node){try{node.disconnect();}catch(_){}}});
      if(job.node){job.node.port.onmessage=null;try{job.node.port.close();}catch(_){}}
      job.source=job.node=job.gain=null;clearTimeout(job.timer);state.closeContext();},
    cancel:function(id,code){var state=CommanderMicRuntime,job=state.job;if(!job||job.id!==id)return;
      job.cancelled=true;job.code=code||'MIC_CANCELLED';state.cleanupNodes(job);job.chunks=[];state.signal(job,'cancelled',job.code);state.job=null;},
    stop:function(id){var state=CommanderMicRuntime,job=state.job;if(!job||job.id!==id||job.done||job.stopping)return;
      if(!job.listening){state.cancel(id,'MIC_NOT_READY');return;}
      job.stopping=true;job.listening=false;job.node.port.postMessage({op:'stop'});state.stopTracks(job.stream);job.stream=null;
      clearTimeout(job.timer);job.timer=setTimeout(function(){state.cancel(id,'MIC_FINALIZE_TIMEOUT');},5000);},
    begin:function(receiver,id,moduleUrl,device,key,seconds){var state=CommanderMicRuntime,job=state.make(receiver,id,false);if(!job)return;
      if(!state.granted){state.signal(job,'error','MIC_PERMISSION_REQUIRED');state.job=null;return;}
      var url;try{url=new URL(moduleUrl,location.href);if(url.origin!==new URL(location.href).origin||!url.pathname.endsWith('/CommanderVoice/commander-pcm-worklet.js')
        ||!Number.isFinite(seconds)||seconds<1||seconds>60||document.hidden||!document.hasFocus()
        ||document.activeElement&&/^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement.tagName))throw new Error('setup');}
      catch(_){state.signal(job,'error','MIC_SETUP_REQUIRED');state.job=null;return;}
      var ctx;try{ctx=state.contextFor(receiver);if(!ctx.audioWorklet||!window.AudioWorkletNode)throw new Error('unsupported');}
      catch(_){state.signal(job,'error','MIC_UNSUPPORTED');state.job=null;return;}
      job.rate=ctx.sampleRate;if(!Number.isInteger(job.rate)||job.rate<8000||job.rate>96000){state.cancel(id,'MIC_FORMAT_UNSUPPORTED');return;}
      job.maximum=Math.floor(job.rate*seconds);job.key=key;
      var audio={channelCount:{ideal:1}};
      if(device){var match=state.devices.find(function(d){return d.label===device;});if(!match){state.cancel(id,'MIC_DEVICE_UNAVAILABLE');return;}audio.deviceId={exact:match.deviceId};}
      state.rawPending++;state.signal(job,'opening','');
      (async function(){var stream;
        try{
          stream=await navigator.mediaDevices.getUserMedia({audio:audio,video:false});
          if(job.cancelled||state.job!==job){state.stopTracks(stream);return;}job.stream=stream;
          await Promise.all([ctx.resume(),ctx.audioWorklet.addModule(url.href)]);
          if(job.cancelled||state.job!==job){state.stopTracks(stream);return;}
          job.node=new window.AudioWorkletNode(ctx,'commander-pcm',{numberOfInputs:1,numberOfOutputs:1,outputChannelCount:[1],
            processorOptions:{maximumFrames:job.maximum,chunkFrames:2048}});
          job.node.port.onmessage=function(event){if(job.cancelled||state.job!==job)return;var data=event.data;
            if(data&&data.kind==='pcm'){
              if(!(data.samples instanceof Float32Array)||data.samples.length<1||data.samples.length>2048||data.sequence!==job.sequence++
                ||job.frames+data.samples.length>job.maximum||job.chunks.length>=4096){state.cancel(id,'MIC_INVALID_DATA');return;}
              for(var i=0;i<data.samples.length;i++)if(!Number.isFinite(data.samples[i])){state.cancel(id,'MIC_INVALID_DATA');return;}
              job.frames+=data.samples.length;job.chunks.push(data.samples);
            }else if(data&&data.kind==='done'){
              if(data.frames!==job.frames){state.cancel(id,'MIC_INVALID_DATA');return;}
              job.done=true;job.stopping=true;job.listening=false;state.cleanupNodes(job);state.signal(job,'done','');
            }else state.cancel(id,'MIC_INVALID_DATA');};
          job.source=ctx.createMediaStreamSource(stream);job.gain=ctx.createGain();job.gain.gain.value=0;
          job.source.connect(job.node);job.node.connect(job.gain);job.gain.connect(ctx.destination);
          stream.getAudioTracks().forEach(function(track){track.onended=function(){if(!job.stopping)state.cancel(id,'MIC_DISCONNECTED');};});
          ctx.onstatechange=function(){if(job.listening&&!job.stopping&&ctx.state!=='running')state.cancel(id,'MIC_SUSPENDED');};
          job.listening=true;state.signal(job,'listening','');job.timer=setTimeout(function(){state.stop(id);},seconds*1000);
        }catch(_){state.stopTracks(stream);if(!job.cancelled)state.cancel(id,'MIC_OPEN_FAILED');}
        finally{state.rawPending--;}
      })();},
    dispose:function(owner){var state=CommanderMicRuntime;if(state.job&&state.job.receiver===owner)state.cancel(state.job.id,'MIC_CANCELLED');
      if(state.owner===owner){state.granted=false;state.devices=[];state.closeContext();state.detach();state.owner=null;}}
  },
  CommanderMic_Permission__deps:['$CommanderMicRuntime'],CommanderMic_Permission:function(receiver,id){CommanderMicRuntime.permission(UTF8ToString(receiver),UTF8ToString(id));},
  CommanderMic_Begin__deps:['$CommanderMicRuntime'],CommanderMic_Begin:function(receiver,id,url,device,key,seconds){CommanderMicRuntime.begin(UTF8ToString(receiver),UTF8ToString(id),UTF8ToString(url),UTF8ToString(device),UTF8ToString(key),seconds);},
  CommanderMic_Stop__deps:['$CommanderMicRuntime'],CommanderMic_Stop:function(id){CommanderMicRuntime.stop(UTF8ToString(id));},
  CommanderMic_Cancel__deps:['$CommanderMicRuntime'],CommanderMic_Cancel:function(id){CommanderMicRuntime.cancel(UTF8ToString(id));},
  CommanderMic_Dispose__deps:['$CommanderMicRuntime'],CommanderMic_Dispose:function(owner){CommanderMicRuntime.dispose(UTF8ToString(owner));},
  CommanderMic_PermissionReady__deps:['$CommanderMicRuntime'],CommanderMic_PermissionReady:function(){return CommanderMicRuntime.granted?1:0;},
  CommanderMic_IsListening__deps:['$CommanderMicRuntime'],CommanderMic_IsListening:function(id){var j=CommanderMicRuntime.job;return j&&j.id===UTF8ToString(id)&&j.listening?1:0;},
  CommanderMic_FrameCount__deps:['$CommanderMicRuntime'],CommanderMic_FrameCount:function(id){var j=CommanderMicRuntime.job;return j&&j.id===UTF8ToString(id)&&j.done?j.frames:-1;},
  CommanderMic_Rate__deps:['$CommanderMicRuntime'],CommanderMic_Rate:function(id){var j=CommanderMicRuntime.job;return j&&j.id===UTF8ToString(id)?j.rate:0;},
  CommanderMic_Read__deps:['$CommanderMicRuntime'],CommanderMic_Read:function(id,pointer,capacity){var s=CommanderMicRuntime,j=s.job;
    if(!j||j.id!==UTF8ToString(id)||!j.done||j.frames>capacity)return -1;
    var offset=pointer>>>2;for(var i=0;i<j.chunks.length;i++){HEAPF32.set(j.chunks[i],offset);offset+=j.chunks[i].length;}
    var frames=j.frames;j.chunks=[];s.job=null;return frames;}
});
