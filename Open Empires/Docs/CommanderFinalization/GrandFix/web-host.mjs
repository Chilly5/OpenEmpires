import http from 'node:http';import fs from 'node:fs';import path from 'node:path';import {randomBytes} from 'node:crypto';import {fileURLToPath} from 'node:url';
const mime={'.html':'text/html; charset=utf-8','.js':'application/javascript; charset=utf-8','.css':'text/css; charset=utf-8',
  '.wasm':'application/wasm','.data':'application/octet-stream','.bundle':'application/octet-stream','.json':'application/json',
  '.txt':'text/plain; charset=utf-8','.png':'image/png','.jpg':'image/jpeg','.jpeg':'image/jpeg','.gif':'image/gif','.svg':'image/svg+xml',
  '.ico':'image/x-icon','.ogg':'audio/ogg','.mp3':'audio/mpeg','.wav':'audio/wav'};
export function createWebHost({root,isolation=false,gatewayOrigins=[],iframeOrigins=[]}){
  const base=fs.realpathSync(path.resolve(root));if(!fs.statSync(base).isDirectory())throw new Error('HOST_CONFIG_INVALID');
  function origins(values){if(!Array.isArray(values)||values.length>16)throw new Error('HOST_CONFIG_INVALID');
    for(const value of values){let url;try{url=new URL(value);}catch{throw new Error('HOST_CONFIG_INVALID');}
      if(url.origin!==value||url.protocol!=='https:'&&!(url.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(url.hostname)))throw new Error('HOST_CONFIG_INVALID');}
    return values.join(' ');}
  const gateway=origins(gatewayOrigins),embed=origins(iframeOrigins);
  const server=http.createServer({maxHeaderSize:8192},(req,res)=>{
    const nonce=randomBytes(18).toString('base64');
    const headers={'X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer','Cache-Control':'no-store',
      'Permissions-Policy':'microphone=(self), camera=()',
      'Content-Security-Policy':"default-src 'self'; script-src 'self' 'nonce-"+nonce+"' 'wasm-unsafe-eval' blob:; style-src 'self' 'unsafe-inline'; img-src 'self' data:; media-src 'self' blob:; worker-src 'self' blob:; connect-src 'self' "+gateway+"; object-src 'none'; base-uri 'none'; frame-ancestors 'self' "+embed,
      ...(isolation?{'Cross-Origin-Opener-Policy':'same-origin','Cross-Origin-Embedder-Policy':'require-corp'}:{})};
    function fail(code){res.writeHead(code,headers);res.end('Web asset unavailable.');}
    try{
      if(!/^(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/.test(req.headers.host??'')){fail(403);return;}
      if(!['GET','HEAD'].includes(req.method)){fail(405);return;}
      let decoded;try{decoded=decodeURIComponent(req.url.split('?')[0]);}catch{fail(400);return;}
      if(decoded.length>2048||decoded.includes('\\')||decoded.includes('\0')||!decoded.startsWith('/')){fail(400);return;}
      const segments=decoded.split('/').filter(Boolean);if(segments.some(s=>s==='..'||s==='.'||s.startsWith('.')||/^(secrets?|credentials?|api[-_]?keys?|tokens?)([._-]|$)/i.test(s))){fail(403);return;}
      if(!segments.length)segments.push('index.html');
      let absolute=base;for(const segment of segments){absolute=path.join(absolute,segment);if(fs.lstatSync(absolute).isSymbolicLink()){fail(403);return;}}
      if(!absolute.startsWith(base+path.sep)||!fs.statSync(absolute).isFile()){fail(404);return;}
      const size=fs.statSync(absolute).size;if(size>1024*1024*1024){fail(413);return;}
      let format=absolute,encoding=null;
      if(format.endsWith('.gz')){encoding='gzip';format=format.slice(0,-3);}else if(format.endsWith('.br')){encoding='br';format=format.slice(0,-3);}
      const unityweb=format.endsWith('.unityweb');if(unityweb)format=format.slice(0,-9);
      const extension=path.extname(format).toLowerCase();if(!mime[extension]){fail(403);return;}
      headers['Content-Type']=unityweb?'application/octet-stream':mime[extension];if(encoding&&!unityweb)headers['Content-Encoding']=encoding;
      if(extension==='.html'){
        if(encoding||size>1024*1024){fail(413);return;}
        let html=fs.readFileSync(absolute,'utf8');html=html.replace(/<script\b(?![^>]*\bnonce=)([^>]*)>/gi,(_match,attributes)=>'<script nonce="'+nonce+'"'+attributes+'>');
        headers['Content-Length']=Buffer.byteLength(html);res.writeHead(200,headers);res.end(req.method==='HEAD'?undefined:html);return;
      }
      headers['Content-Length']=size;res.writeHead(200,headers);if(req.method==='HEAD'){res.end();return;}
      const stream=fs.createReadStream(absolute);stream.on('error',()=>res.destroy());res.on('close',()=>stream.destroy());stream.pipe(res);
    }catch{fail(404);}
  });server.maxConnections=32;server.headersTimeout=10000;server.requestTimeout=15000;server.keepAliveTimeout=5000;return server;
}
// Localhost development only. Production TLS/proxy deployment remains operator-owned.
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  try{const args=process.argv.slice(2),root=args.shift();if(!root)throw new Error('usage');let port=8088,isolation=false;const gatewayOrigins=[],iframeOrigins=[];
    while(args.length){const flag=args.shift();if(flag==='--port')port=Number(args.shift());else if(flag==='--gateway')gatewayOrigins.push(args.shift());
      else if(flag==='--iframe-origin')iframeOrigins.push(args.shift());else if(flag==='--isolation')isolation=true;else throw new Error('usage');}
    if(!Number.isInteger(port)||port<1||port>65535)throw new Error('usage');const server=createWebHost({root,isolation,gatewayOrigins,iframeOrigins});
    server.on('error',()=>{console.error('WEB_HOST_LISTEN_FAILED');process.exitCode=1;});server.listen(port,'127.0.0.1',()=>console.log('Unity Web local host ready on configured loopback port.'));
  }catch{console.error('WEB_HOST_CONFIG_INVALID: node web-host.mjs <build-root> [--port N] [--gateway HTTPS-origin] [--isolation]');process.exitCode=1;}
}
