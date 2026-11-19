// Bound Google Apps Script, V8 runtime. Deploy as a web app executing as its owner.
// The spreadsheet stays private. The web app accepts writes, never reads data publicly.
const RUN_SHEET = 'GLITCH_Runs';
const SUMMARY_SHEET = 'GLITCH_Resumen';
const MAX_ROWS = 50000;
const MAX_DAILY_ROWS = 2500;
const HEADERS = ['runId','receivedUtc','startedUtc','endedUtc','installationId','applicationSessionId',
  'version','platform','initialLevel','playerClass','score','survivalSeconds','endReason','deathCause',
  'finalArena','enemyState','playerX','playerY','pickups','powerups','parries','firewallBursts','breaches',
  'contracts','upgrades','schemaVersion'];

function onOpen() {
  SpreadsheetApp.getUi().createMenu('GLITCH')
    .addItem('Configurar telemetría', 'setupTelemetry')
    .addItem('Actualizar resumen', 'refreshTelemetrySummary').addToUi();
}

function setupTelemetry() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  if (!ss) throw new Error('Abrí este script desde Extensiones > Apps Script de la hoja.');
  const props = PropertiesService.getScriptProperties();
  props.setProperty('SPREADSHEET_ID', ss.getId());
  if (!props.getProperty('INGEST_KEY')) props.setProperty('INGEST_KEY',
    Utilities.getUuid().replace(/-/g, '') + Utilities.getUuid().replace(/-/g, ''));
  let sheet = ss.getSheetByName(RUN_SHEET);
  if (!sheet) sheet = ss.insertSheet(RUN_SHEET);
  if (sheet.getLastRow() === 0) sheet.getRange(1, 1, 1, HEADERS.length).setValues([HEADERS]);
  assertHeaders_(sheet);
  sheet.setFrozenRows(1);
  sheet.getRange(1, 1, 1, HEADERS.length).setBackground('#102333').setFontColor('#ddecf5').setFontWeight('bold');
  if (!ss.getSheetByName(SUMMARY_SHEET)) ss.insertSheet(SUMMARY_SHEET);
  SpreadsheetApp.flush();
  refreshTelemetrySummary();
}

function doGet() {
  return json_({service:'GLITCH telemetry', protocolVersion:1});
}

function doPost(e) {
  try {
    const raw = e && e.postData && e.postData.contents;
    if (typeof raw !== 'string' || raw.length > 65536) return json_({ok:false,error:'invalid_payload',retryable:false});
    let input;
    try { input = JSON.parse(raw); } catch (_) { return json_({ok:false,error:'invalid_json',retryable:false}); }
    if (!input || typeof input !== 'object') return json_({ok:false,error:'invalid_batch',retryable:false});
    const props = PropertiesService.getScriptProperties();
    const key = props.getProperty('INGEST_KEY');
    if (!key || key.length < 24 || input.ingestKey !== key)
      return json_({ok:false,error:'unauthorized',retryable:false});
    if (input.protocolVersion !== 1 || !Array.isArray(input.runs) || input.runs.length < 1 || input.runs.length > 5)
      return json_({ok:false,error:'invalid_batch',retryable:false});
    const received = new Date().toISOString();
    const valid = [], rejectedIds = [], seen = new Set();
    for (const run of input.runs) {
      if (!run || !id_(run.sessionId)) return json_({ok:false,error:'invalid_run_id',retryable:false});
      if (seen.has(run.sessionId)) continue;
      seen.add(run.sessionId);
      try { valid.push(makeRow_(run, received)); } catch (_) { rejectedIds.push(run.sessionId); }
    }
    const lock = LockService.getScriptLock();
    if (!lock.tryLock(2000)) return json_({ok:false,error:'busy',retryable:true});
    try {
      const ss = SpreadsheetApp.openById(props.getProperty('SPREADSHEET_ID'));
      const sheet = ss.getSheetByName(RUN_SHEET);
      assertHeaders_(sheet);
      const last = sheet.getLastRow();
      const ids = valid.map(row => row[0]);
      const existing = new Set();
      // One server-side ID search per batch, not a download of the whole table.
      if (last > 1 && ids.length) {
        const matches = sheet.getRange(2, 1, last - 1, 1).createTextFinder('^(?:' + ids.join('|') + ')$')
          .useRegularExpression(true).matchEntireCell(true).findAll();
        matches.forEach(cell => existing.add(String(cell.getValue())));
      }
      const fresh = valid.filter(row => !existing.has(row[0]));
      const day = received.slice(0,10);
      const daily = props.getProperty('INGEST_DAY') === day ? Number(props.getProperty('INGEST_COUNT') || 0) : 0;
      if (last - 1 + fresh.length > MAX_ROWS || daily + fresh.length > MAX_DAILY_ROWS)
        return json_({ok:false,error:'capacity',retryable:true,
          acceptedIds:Array.from(existing),rejectedIds:rejectedIds});
      if (fresh.length) {
        if (last + fresh.length > sheet.getMaxRows())
          sheet.insertRowsAfter(sheet.getMaxRows(), Math.max(1000,last + fresh.length - sheet.getMaxRows()));
        // Preserve numeric-looking GUIDs and UTC timestamps as literal strings.
        sheet.getRange(last + 1, 1, fresh.length, 10).setNumberFormat('@');
        // A single rectangular write. Retrying a lost acknowledgement finds runId in this same durable table.
        sheet.getRange(last + 1, 1, fresh.length, HEADERS.length).setValues(fresh);
        SpreadsheetApp.flush();
        props.setProperties({INGEST_DAY:day, INGEST_COUNT:String(daily + fresh.length)});
      }
      return json_({ok:true,acceptedIds:ids,rejectedIds:rejectedIds});
    } finally { lock.releaseLock(); }
  } catch (_) {
    // Never returns account, token, spreadsheet contents, exception paths or a stack trace.
    return json_({ok:false,error:'server_error',retryable:true});
  }
}

function assertHeaders_(sheet) {
  if (!sheet || sheet.getLastRow() < 1) throw new Error('Run setupTelemetry first.');
  const actual = sheet.getRange(1,1,1,HEADERS.length).getValues()[0];
  if (HEADERS.some((value,index) => actual[index] !== value)) throw new Error('Unexpected headers; preserve existing data.');
}
function id_(value) { return typeof value === 'string' && /^[a-f0-9]{32}$/.test(value); }
function number_(value, max, integer) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || value > max || (integer && !Number.isInteger(value)))
    throw new Error('Invalid number');
  return value;
}
function text_(value, length) {
  const result = typeof value === 'string' && value.trim() ? value.trim().slice(0,length || 64) : 'unknown';
  // All external text stays literal; no spreadsheet formula injection.
  return /^[=+\-@\t\r\n]/.test(result) ? "'" + result : result;
}
function date_(value) {
  if (typeof value !== 'string' || value.length > 40 || !/^\d{4}-\d{2}-\d{2}T/.test(value)) throw new Error('Invalid timestamp');
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) throw new Error('Invalid timestamp');
  return date.toISOString();
}
function makeRow_(r, received) {
  if (!id_(r.installationId) || !id_(r.applicationSessionId)) throw new Error('Invalid identity');
  const started = date_(r.startedAtUtc), ended = date_(r.endedAtUtc);
  if (ended < started) throw new Error('Invalid time order');
  const c = r.endContext || {};
  const coord = v => typeof v === 'number' && Number.isFinite(v) && Math.abs(v) <= 1000000 ? v : 0;
  return [r.sessionId,received,started,ended,r.installationId,r.applicationSessionId,text_(r.version),text_(r.platform),
    text_(r.level),text_(r.playerClass),number_(r.score,100000000,true),number_(r.survivalSeconds,604800,false),
    text_(r.endReason),text_(c.source),text_(c.arena),text_(c.enemyState),coord(c.playerX),coord(c.playerY),
    number_(r.scorePickups,1000000,true),number_(r.powerups,1000000,true),number_(r.parries,1000000,true),
    number_(r.firewallBursts,1000000,true),number_(r.breaches,1000000,true),number_(r.contractsCompleted,1000000,true),
    number_(r.upgradesChosen,1000000,true),number_(r.schemaVersion,100,true)];
}
function json_(value) { return ContentService.createTextOutput(JSON.stringify(value)).setMimeType(ContentService.MimeType.JSON); }

// Developer-requested refresh only: no dashboard reads/rebuilds for every incoming run.
function refreshTelemetrySummary() {
  const props = PropertiesService.getScriptProperties();
  const ss = SpreadsheetApp.openById(props.getProperty('SPREADSHEET_ID'));
  const raw = ss.getSheetByName(RUN_SHEET);
  assertHeaders_(raw);
  const rows = raw.getLastRow() > 1 ? raw.getRange(2,1,raw.getLastRow()-1,HEADERS.length).getValues() : [];
  let sheet = ss.getSheetByName(SUMMARY_SHEET);
  if (!sheet) sheet = ss.insertSheet(SUMMARY_SHEET);
  const report = buildSummary_(rows);
  // Only the dedicated derived sheet is replaced, never the raw run table.
  sheet.clearContents();
  sheet.getRange(1,1,report.length,4).setValues(report);
  sheet.setFrozenRows(1);
  sheet.getRange(1,1,1,4).setBackground('#102333').setFontColor('#ddecf5').setFontWeight('bold');
  sheet.autoResizeColumns(1,4);
}
function buildSummary_(rows) {
  const report = [['GLITCH - Resumen','Valor','Detalle','Actualizado ' + new Date().toISOString()]];
  const count = rows.length, sum = col => rows.reduce((n,r) => n + Number(r[col] || 0),0);
  const average = col => count ? Math.round(sum(col)/count*100)/100 : 0;
  const installations = new Map(), sessions = new Set();
  rows.forEach(r => { if (!installations.has(r[4])) installations.set(r[4],new Set());
    installations.get(r[4]).add(String(r[3]).slice(0,10)); sessions.add(r[5]); });
  const deaths = rows.filter(r => r[12] === 'defeat');
  const returned = Array.from(installations.values()).filter(days => days.size > 1).length;
  const durations = rows.map(r => Number(r[11])).sort((a,b)=>a-b);
  const mid = Math.floor(count/2), median = count ? count%2 ? durations[mid] : (durations[mid-1]+durations[mid])/2 : 0;
  const metric = (name,value,detail) => report.push([name,value,detail || '', '']);
  metric('Partidas recibidas',count);
  metric('Instalaciones distintas',installations.size,'No equivale a personas: reinstalar o revocar reinicia el identificador.');
  metric('Sesiones de aplicación',sessions.size);
  metric('Puntaje promedio',average(10)); metric('Supervivencia promedio (s)',average(11));
  metric('Supervivencia mediana (s)',median);
  metric('Abandonos (%)',count ? Math.round(rows.filter(r=>r[12]==='abandoned').length/count*10000)/100 : 0);
  metric('Muertes antes de 60 s (%)',deaths.length ? Math.round(deaths.filter(r=>r[11]<60).length/deaths.length*10000)/100 : 0);
  metric('Parrys por minuto',sum(11) ? Math.round(sum(20)/(sum(11)/60)*100)/100 : 0);
  metric('Instalaciones vistas en más de un día (%)',installations.size ? Math.round(returned/installations.size*10000)/100 : 0,
    'Indicador aproximado de retorno, no retención D1/D7 ni satisfacción.');
  const groups = (title, columns) => {
    report.push(['','','',''],[title,'Partidas','Promedio de tiempo (s)','Promedio de puntos']);
    const map = new Map();
    rows.forEach(r => { const key=columns.map(c=>String(r[c])).join(' / ');
      const item=map.get(key)||[0,0,0]; item[0]++; item[1]+=Number(r[11]); item[2]+=Number(r[10]); map.set(key,item); });
    Array.from(map.entries()).sort((a,b)=>b[1][0]-a[1][0]).forEach(([key,v]) =>
      report.push([text_(key,180),v[0],Math.round(v[1]/v[0]*100)/100,Math.round(v[2]/v[0]*100)/100]));
  };
  groups('Final por arena / causa',[14,13]); groups('Estado del jefe al finalizar',[15]);
  groups('Clase / estilo',[9]); groups('Versión / plataforma',[6,7]);
  return report;
}
