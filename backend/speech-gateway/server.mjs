import fs from 'node:fs';
import path from 'node:path';
import {loadConfiguration} from './config.mjs';
import {createAdapters} from './upstream.mjs';
import {createGateway} from './gateway.mjs';

// Only this explicitly started server process reads named operator configuration.
// No dotenv auto-loading, environment dumps, access logs or raw upstream errors.
try {
  const config=loadConfiguration(process.env);
  if(!config.enabled)console.log('Commander gateway disabled; no listener or provider calls.');
  else {
    const adapters=createAdapters(config);
    await adapters.validateAuthentication(); // Standalone registry/ACL/expiry checked before listener or lock.
    fs.mkdirSync(path.dirname(config.ledgerPath),{recursive:true,mode:0o700});
    const lock=config.ledgerPath+'.lock';const fd=fs.openSync(lock,'wx',0o600);
    fs.writeFileSync(fd,JSON.stringify({schema:'gateway-process-lock@1',pid:process.pid}));fs.fsyncSync(fd);fs.closeSync(fd);
    // Process-lifetime lock deliberately survives an unclean exit. An operator
    // may remove ONLY this lock after verifying its process is gone; never the
    // budget. Do not run multiple replicas or distinct ledgers for one budget.
    const server=createGateway({...config,...adapters});
    server.on('error',()=>{console.error('GATEWAY_LISTEN_FAILED');process.exitCode=1;});
    server.listen(config.port,'127.0.0.1',()=>console.log('Commander gateway ready on configured loopback port.'));
  }
}catch{console.error('GATEWAY_STARTUP_FAILED');process.exitCode=1;}
