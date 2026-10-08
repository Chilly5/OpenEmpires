export const maximumAudioBytes=2*1024*1024;
export function inspectWav(bytes) {
  if(!Buffer.isBuffer(bytes)||bytes.length<44||bytes.length>maximumAudioBytes)throw new Error('INVALID_AUDIO');
  if(bytes.toString('ascii',0,4)!=='RIFF'||bytes.toString('ascii',8,12)!=='WAVE'||bytes.readUInt32LE(4)!==bytes.length-8)
    throw new Error('INVALID_AUDIO');
  let format=null,data=null,offset=12,chunks=0;
  while(offset<bytes.length) {
    if(++chunks>16||offset+8>bytes.length)throw new Error('INVALID_AUDIO');
    const id=bytes.toString('ascii',offset,offset+4),size=bytes.readUInt32LE(offset+4),start=offset+8,end=start+size;
    if(end>bytes.length)throw new Error('INVALID_AUDIO');
    if(id==='fmt ') {
      if(format||size!==16)throw new Error('INVALID_AUDIO');
      format={encoding:bytes.readUInt16LE(start),channels:bytes.readUInt16LE(start+2),rate:bytes.readUInt32LE(start+4),
        byteRate:bytes.readUInt32LE(start+8),align:bytes.readUInt16LE(start+12),bits:bytes.readUInt16LE(start+14)};
    }else if(id==='data'){if(data)throw new Error('INVALID_AUDIO');data={start,size};}
    else if(!['JUNK','LIST'].includes(id))throw new Error('INVALID_AUDIO');
    offset=end+(size%2);
  }
  if(offset!==bytes.length||!format||!data||format.encoding!==1||format.channels!==1||format.rate!==16000
    ||format.bits!==16||format.align!==2||format.byteRate!==32000||data.size%2!==0)throw new Error('INVALID_AUDIO');
  const seconds=data.size/32000;
  if(seconds<0.1||seconds>60)throw new Error('INVALID_AUDIO');
  return {seconds,billableSeconds:Math.ceil(seconds),rate:16000,channels:1,format:'pcm16-wav'};
}
