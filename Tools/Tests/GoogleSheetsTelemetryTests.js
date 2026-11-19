/* Run with Node: node Tools/Tests/GoogleSheetsTelemetryTests.js
   No network or live spreadsheet: executes the actual receiver with Google service doubles. */
function runReceiverTests(source) {
  let checks=0;
  const check=(condition,message)=>{if(!condition)throw new Error(message);checks++;};
  const key='test-only-ingest-key-01234567890123456789';
  const state={rows:[],grid:1000,props:{},busy:false,failAfterWrite:false,opens:0};
  const properties={getProperty:k=>state.props[k]||null,setProperty:(k,v)=>{state.props[k]=v;},
    setProperties:p=>Object.assign(state.props,p)};
  const sheet={getLastRow:()=>state.rows.length,getMaxRows:()=>state.grid,
    insertRowsAfter:(row,n)=>{state.grid+=n;},
    getRange:(row,col,n,m)=>({
      setNumberFormat(){return this;},
      getValues:()=>Array.from({length:n},(_,i)=>Array.from({length:m},(_,j)=>(state.rows[row-1+i]||[])[col-1+j])),
      setValues:values=>{if(row+n-1>state.grid)throw new Error('Grid exceeded');
        values.forEach((r,i)=>{state.rows[row-1+i]=r.slice();});},
      createTextFinder:pattern=>{const finder={useRegularExpression:()=>finder,matchEntireCell:()=>finder,
        findAll:()=>state.rows.slice(row-1,row-1+n).filter(r=>new RegExp(pattern).test(String(r[col-1])))
          .map(r=>({getValue:()=>r[col-1]}))};return finder;}
    })};
  const env={
    PropertiesService:{getScriptProperties:()=>properties},
    LockService:{getScriptLock:()=>({tryLock:()=>!state.busy,releaseLock:()=>{}})},
    SpreadsheetApp:{openById:()=>{state.opens++;return {getSheetByName:()=>sheet};},
      flush:()=>{if(state.failAfterWrite){state.failAfterWrite=false;throw new Error('After-write failure');}}},
    ContentService:{MimeType:{JSON:'application/json'},createTextOutput:text=>({text,setMimeType(){return this;}})}
  };
  const api=new Function(...Object.keys(env), source+'\nreturn {doPost,doGet,makeRow_,buildSummary_,HEADERS};')(...Object.values(env));
  function reset(){state.rows=[api.HEADERS.slice()];state.grid=1000;state.busy=false;state.failAfterWrite=false;state.opens=0;
    state.props={INGEST_KEY:key,SPREADSHEET_ID:'test-sheet'};}
  const id=n=>n.toString(16).padStart(32,'0');
  const run=n=>({sessionId:id(n),installationId:id(100),applicationSessionId:id(200),schemaVersion:2,
    version:'1.0',platform:'WindowsPlayer',level:'Lab',playerClass:'Normal',score:50,survivalSeconds:20,
    startedAtUtc:'2026-11-19T12:00:00.000Z',endedAtUtc:'2026-11-19T12:00:20.000Z',endReason:'defeat',
    endContext:{source:'anomaly_contact',arena:'Lab',enemyState:'BasePursuit',playerX:2,playerY:3},
    scorePickups:2,powerups:1,parries:3,firewallBursts:0,breaches:0,contractsCompleted:0,upgradesChosen:0});
  const post=body=>JSON.parse(api.doPost({postData:{contents:JSON.stringify(body)}}).text);
  const batch=runs=>({protocolVersion:1,ingestKey:key,runs});
  reset();
  let reply=post({...batch([run(1)]),ingestKey:'wrong'});
  check(!reply.ok && state.opens===0,'Wrong key cannot access the sheet.');
  reply=JSON.parse(api.doPost({postData:{contents:'{'}}).text);
  check(reply.error==='invalid_json','Malformed JSON rejected.');
  reply=post(null);check(reply.error==='invalid_batch','Null JSON body rejected without touching the sheet.');
  reply=JSON.parse(api.doPost({postData:{contents:'x'.repeat(65537)}}).text);
  check(reply.error==='invalid_payload','Oversize body rejected.');
  reply=post(batch(Array.from({length:6},(_,i)=>run(i+1))));
  check(reply.error==='invalid_batch','Hard batch limit enforced.');
  reply=post(batch([run(1)]));
  check(reply.ok && reply.acceptedIds[0]===id(1) && state.rows.length===2,'Valid run creates one durable row.');
  post(batch([run(1)]));check(state.rows.length===2,'Retry after lost acknowledgement is deduplicated.');
  post(batch([run(2),run(2)]));check(state.rows.length===3,'Duplicates inside a batch are deduplicated.');
  state.failAfterWrite=true;reply=post(batch([run(3)]));
  check(!reply.ok && state.rows.length===4,'Failure after durable write does not pretend to acknowledge.');
  reply=post(batch([run(3)]));check(reply.ok && state.rows.length===4,'Retry after post-write failure does not duplicate.');
  const invalid=run(4);invalid.score=-1;reply=post(batch([run(5),invalid]));
  check(reply.ok && reply.rejectedIds[0]===id(4) && reply.acceptedIds[0]===id(5),'Mixed valid/invalid batch acknowledged precisely.');
  const formula=run(6);formula.playerClass='=IMPORTXML("bad")';post(batch([formula]));
  check(state.rows[state.rows.length-1][9].startsWith("'="),'External text cannot execute as a sheet formula.');
  const nan=run(7);nan.survivalSeconds=NaN;reply=post(batch([nan]));
  check(reply.rejectedIds[0]===id(7),'Nonfinite numbers rejected.');
  state.busy=true;const count=state.rows.length;reply=post(batch([run(8)]));
  check(reply.retryable && state.rows.length===count,'Concurrent lock contention does not write.');state.busy=false;
  state.grid=state.rows.length;post(batch([run(8)]));
  check(state.grid>state.rows.length,'Grid expands before a rectangular append.');
  state.props.INGEST_DAY=new Date().toISOString().slice(0,10);state.props.INGEST_COUNT='2500';
  reply=post(batch([run(9)]));check(reply.error==='capacity','Daily ingestion limit enforced.');
  reply=post(batch([run(1)]));check(reply.ok,'Already stored run can be acknowledged even at capacity.');
  const reversed=run(10);reversed.endedAtUtc='2026-11-18T00:00:00Z';
  reply=post(batch([reversed]));check(reply.rejectedIds[0]===id(10),'Impossible time order rejected.');
  const info=JSON.parse(api.doGet().text);check(!('runs' in info) && !('key' in info),'GET discloses no records or key.');
  const dashboard=api.buildSummary_(state.rows.slice(1));
  check(dashboard.every(row=>row.length===4),'Dashboard is a valid rectangular write.');
  check(dashboard.some(row=>row[0]==='Partidas recibidas' && row[1]===state.rows.length-1),'Dashboard counts real received rows.');
  reset();state.rows[0][0]='different';reply=post(batch([run(1)]));
  check(!reply.ok && state.rows.length===1,'Unexpected headers fail closed without overwriting data.');
  return 'Google Sheets receiver: '+checks+' checks passed.';
}
if(typeof module!=='undefined' && module.exports){
  module.exports={runReceiverTests};
  if(require.main===module){const fs=require('fs'),path=require('path');
    console.log(runReceiverTests(fs.readFileSync(path.join(__dirname,'../GoogleSheetsTelemetry/Code.gs'),'utf8')));}
}
