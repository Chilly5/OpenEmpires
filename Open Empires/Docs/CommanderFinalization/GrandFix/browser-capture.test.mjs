import {test} from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import vm from 'node:vm';
const mic='Assets/Plugins/WebGL/CommanderMicrophone.jslib',worklet='Assets/StreamingAssets/CommanderVoice/commander-pcm-worklet.js';
const id='22222222-2222-4222-8222-222222222222';
function fixture(getMedia){const library={},messages=[],listeners={},tracks={stops:0,stop(){this.stops++;},getSettings(){return {sampleRate:48000,channelCount:1};}};
  class Context{constructor(){this.state='running';this.sampleRate=48000;this.audioWorklet={addModule:async()=>{}};this.destination={};}resume(){this.state='running';return Promise.resolve();}close(){this.state='closed';return Promise.resolve();}createMediaStreamSource(){return {connect(){},disconnect(){}};}createGain(){return {gain:{value:1},connect(){},disconnect(){}};}}
  class WorkletNode{constructor(){this.port={onmessage:null,postMessage(){},close(){}};}connect(){}disconnect(){}}
  const strings={1:'receiver',2:id,3:'https://game.example.invalid/StreamingAssets/CommanderVoice/commander-pcm-worklet.js',4:'',5:'KeyV'};
  const document={hidden:false,activeElement:null,hasFocus:()=>true,addEventListener:(n,f)=>{listeners[n]=f;},removeEventListener(){}};
  const context={LibraryManager:{library},mergeInto:(t,v)=>Object.assign(t,v),UTF8ToString:p=>strings[p]??'',SendMessage:(...a)=>messages.push(a),
    navigator:{mediaDevices:{getUserMedia:getMedia,enumerateDevices:async()=>[]}},window:{AudioContext:Context,AudioWorkletNode:WorkletNode,addEventListener:(n,f)=>{listeners[n]=f;},removeEventListener(){}},
    document,location:{href:'https://game.example.invalid/'},URL,Float32Array,Number,Math,Date,Promise,HEAPF32:new Float32Array(6000000),setTimeout:()=>1,clearTimeout(){}};
  vm.runInNewContext(fs.readFileSync(mic,'utf8'),context);for(const[k,v]of Object.entries(library))if(k.startsWith('$'))context[k.slice(1)]=v;
  return {library,messages,listeners,tracks,stream:{getTracks:()=>[tracks],getAudioTracks:()=>[tracks]},context};}
async function settle(){for(let i=0;i<6;i++)await new Promise(r=>setImmediate(r));}
test('permission grants stop temporary tracks and never start recording',async()=>{let stream;const f=fixture(async()=>stream);stream=f.stream;
  f.library.CommanderMic_Permission(1,2);await settle();assert.equal(f.tracks.stops,1);assert.equal(JSON.parse(f.messages.at(-1)[2]).stage,'permission');
  assert.equal(f.library.CommanderMic_IsListening(2),0);});
test('cancelled permission late grant stops tracks without a ready callback',async()=>{let finish;const f=fixture(()=>new Promise(r=>{finish=r;}));
  f.library.CommanderMic_Permission(1,2);f.library.CommanderMic_Cancel(2);finish(f.stream);await settle();assert.equal(f.tracks.stops,1);
  assert.equal(f.messages.some(m=>JSON.parse(m[2]).stage==='permission'),false);});
test('recording cannot start without prior explicit microphone activation',async()=>{let calls=0;const f=fixture(async()=>{calls++;return f.stream;});
  f.library.CommanderMic_Begin(1,2,3,4,5,15);await settle();assert.equal(calls,0);assert.equal(JSON.parse(f.messages.at(-1)[2]).code,'MIC_PERMISSION_REQUIRED');});
test('late capture stream after release is stopped instead of starting a worklet',async()=>{let stream,finish;let calls=0;const f=fixture(()=>++calls===1?Promise.resolve(stream):new Promise(r=>{finish=r;}));stream=f.stream;
  f.library.CommanderMic_Permission(1,2);await settle();f.library.CommanderMic_Cancel(2); // retire only permission record; grant remains
  f.library.CommanderMic_Begin(1,2,3,4,5,15);f.library.CommanderMic_Stop(2);finish(stream);await settle();assert.equal(f.tracks.stops,2);
  assert.equal(f.library.CommanderMic_IsListening(2),0);});
test('worklet flush preserves partial final chunk and enforces exact frame ceiling',()=>{
  let ctor;class Base{constructor(){this.port={sent:[],postMessage(m){this.sent.push(m);},onmessage:null};}}
  vm.runInNewContext(fs.readFileSync(worklet,'utf8'),{AudioWorkletProcessor:Base,registerProcessor:(name,type)=>{assert.equal(name,'commander-pcm');ctor=type;},Float32Array,Number,Math,sampleRate:16000});
  const worker=new ctor({processorOptions:{maximumFrames:200,chunkFrames:128}});const first=new Float32Array(128).fill(.25),last=new Float32Array(128).fill(.5);
  worker.process([[first]],[]);worker.process([[last]],[]);const pcm=worker.port.sent.filter(x=>x.kind==='pcm');
  assert.equal(pcm.length,2);assert.equal(pcm[0].samples.length,128);assert.equal(pcm[1].samples.length,72);assert.equal(pcm[1].samples[71],.5);
  assert.equal(worker.port.sent.at(-1).frames,200);assert.equal(worker.process([[last]],[]),false);
  const partial=new ctor({processorOptions:{maximumFrames:1000,chunkFrames:128}});partial.process([[new Float32Array(31).fill(.75)]],[]);
  partial.port.onmessage({data:{op:'stop'}});assert.equal(partial.port.sent[0].samples.length,31);assert.equal(partial.port.sent[0].samples[30],.75);
});
