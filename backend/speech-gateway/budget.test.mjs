import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createBudget} from './budget.mjs';
function withState(state,run){const directory=fs.mkdtempSync(path.join(os.tmpdir(),'OpenEmpires-budget-'));
  try{const ledgerPath=path.join(directory,'budget.json');fs.writeFileSync(ledgerPath,JSON.stringify(state));run(ledgerPath);}
  finally{const resolved=path.resolve(directory);if(path.dirname(resolved)===path.resolve(os.tmpdir())&&path.basename(resolved).startsWith('OpenEmpires-budget-'))fs.rmSync(resolved,{recursive:true,force:true});}}
const owner='11111111-1111-4111-8111-111111111111';
test('corrupt owner counters never bypass a persistent global or owner quota',()=>{
  for(const entry of [{audioSeconds:-100,semanticRequests:0},{audioSeconds:'0',semanticRequests:0},{audioSeconds:0,semanticRequests:-1}]){
    withState({schema:'openempires-gateway-budget@1',audioSeconds:0,semanticRequests:0,estimatedUsd:0,owners:{[owner]:entry}},ledgerPath=>{
      assert.throws(()=>createBudget({ledgerPath}),{message:'BUDGET_STATE_INVALID'});
    });
  }
});
test('lost owner accounting cannot reset the per-owner budget on restart',()=>{
  withState({schema:'openempires-gateway-budget@1',audioSeconds:5,semanticRequests:1,estimatedUsd:0.05,owners:{}},ledgerPath=>{
    assert.throws(()=>createBudget({ledgerPath}),{message:'BUDGET_STATE_INVALID'});
  });
});
