// Project-owned PCM collector; no inference, uploads or game authority.
class CommanderPcmProcessor extends AudioWorkletProcessor {
  constructor(options) {
    super();const setup=options.processorOptions||{};
    this.maximumFrames=Math.min(Math.floor(sampleRate*60),Math.max(1,setup.maximumFrames|0));
    this.chunkFrames=Math.min(2048,Math.max(128,setup.chunkFrames|0));
    this.chunk=new Float32Array(this.chunkFrames);this.used=0;this.total=0;this.sequence=0;this.closed=false;
    this.port.onmessage=event=>{if(event.data&&event.data.op==='stop')this.finish();};
  }
  flush(){if(!this.used)return;const samples=this.chunk.slice(0,this.used);
    this.port.postMessage({kind:'pcm',sequence:this.sequence++,samples},[samples.buffer]);this.used=0;}
  finish(){if(this.closed)return;this.closed=true;this.flush();this.port.postMessage({kind:'done',frames:this.total});}
  process(inputs){
    if(this.closed)return false;const channels=inputs[0];if(!channels||!channels.length)return true;
    const length=channels[0].length;
    for(let i=0;i<length&&this.total<this.maximumFrames;i++){
      let value=0;for(let ch=0;ch<channels.length;ch++)value+=(channels[ch][i]||0)/channels.length;
      this.chunk[this.used++]=value;this.total++;if(this.used===this.chunkFrames)this.flush();
    }
    if(this.total===this.maximumFrames)this.finish();return !this.closed;
  }
}
registerProcessor('commander-pcm',CommanderPcmProcessor);
