namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// The offline viewer's page, looking and navigating like Google Sheets, with its placeholders: the title, the sheet's
/// cell styles, a template per tab, the tabs and the meta (filled by <see cref="ViewerWriting"/>).
/// </summary>
public static class ViewerTemplate
{
    public const string Page = """
<!doctype html>
<html lang="en"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>/*__TITLE__*/</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<!-- font loads in the background: a slow/blocked fonts.googleapis.com must not stall the page -->
<link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap" rel="stylesheet" media="print" onload="this.media='all'">
<style>
:root{--chrome:#f9fbfd;--line:#c4c7c5;--text:#1f1f1f;--muted:#5f6368;--accent:#0b57d0;--green:#188038;--hdr:#f8f9fa;--hdr-line:#c0c0c0;--sheet:#fff}
*{box-sizing:border-box}
html,body{height:100%;margin:0}
body{display:flex;flex-direction:column;background:var(--chrome);color:var(--text);font:14px Roboto,Arial,sans-serif;overflow:hidden}
header{display:flex;align-items:center;gap:12px;padding:8px 12px;border-bottom:1px solid var(--line);background:var(--chrome);flex-wrap:wrap}
.logo{width:28px;height:28px;flex:none}
.title{display:flex;flex-direction:column;min-width:0;flex:1}
.title h1{font-size:18px;font-weight:400;margin:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.title small{color:var(--muted);font-size:12px}
.title small a{color:var(--muted)}
.search{position:relative;flex:0 1 360px;min-width:200px}
.search input{width:100%;padding:8px 12px 8px 34px;border:1px solid transparent;border-radius:20px;background:#e9eef6;font:inherit;outline:none}
.search input:focus{background:#fff;border-color:var(--accent)}
.search svg{position:absolute;left:10px;top:9px;width:16px;height:16px;fill:var(--muted)}
#results{position:absolute;right:0;top:42px;width:min(560px,92vw);max-height:70vh;overflow:auto;background:#fff;border:1px solid var(--line);border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,.18);z-index:50;display:none}
#results.open{display:block}
#results .hit{display:block;width:100%;text-align:left;border:0;border-bottom:1px solid #eee;background:none;padding:8px 12px;cursor:pointer;font:13px Roboto,Arial}
#results .hit:hover,#results .hit.sel{background:#e8f0fe}
#results .tab{display:inline-block;font-size:11px;color:var(--green);font-weight:500;margin-right:6px}
#results .row{color:var(--muted);font-size:11px}
#results mark{background:#fde293;padding:0}
#results .info{padding:8px 12px;color:var(--muted);font-size:12px}
main{flex:1;position:relative;min-height:0;background:var(--sheet)}
.pane{position:absolute;inset:0;overflow:auto;display:none}
.pane.active{display:block}
footer{display:flex;align-items:stretch;border-top:1px solid var(--line);background:var(--chrome);height:40px;flex:none}
.tbtn{border:0;background:none;padding:0 12px;cursor:pointer;color:var(--muted);display:flex;align-items:center}
.tbtn:hover{background:#e9eef6}
.tbtn svg{width:20px;height:20px;fill:currentColor}
#tabs{display:flex;overflow-x:auto;scrollbar-width:thin;flex:1}
#tabs button{flex:none;border:0;background:none;padding:0 16px;font:500 13px Roboto,Arial;color:#444746;cursor:pointer;white-space:nowrap;border-bottom:3px solid transparent}
#tabs button:hover{background:#e9eef6}
#tabs button.active{background:#e1e9f7;color:var(--accent);border-bottom-color:var(--accent)}
#tabs button.old{color:#80868b}
#menu{position:absolute;left:8px;bottom:46px;background:#fff;border:1px solid var(--line);border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,.18);max-height:70vh;overflow:auto;z-index:60;display:none;padding:6px 0}
#menu.open{display:block}
#menu button{display:block;width:100%;text-align:left;border:0;background:none;padding:8px 20px;font:13px Roboto,Arial;cursor:pointer}
#menu button:hover{background:#e8f0fe}
#menu button.active{color:var(--accent);font-weight:500}
#toast{position:fixed;left:50%;bottom:56px;transform:translateX(-50%);background:#323232;color:#fff;padding:10px 16px;border-radius:6px;font-size:13px;opacity:0;transition:opacity .2s;pointer-events:none;z-index:70}
#toast.show{opacity:1}
.missing{padding:24px;color:var(--muted)}

/* ---- Google Sheets "waffle" grid base ---- */
.ritz{display:inline-block;min-width:100%}
.ritz .waffle{border-collapse:separate;border-spacing:0;table-layout:fixed;width:0;font-family:Arial,sans-serif;font-size:10pt;user-select:text}
.ritz .waffle td{overflow:hidden;padding:2px 3px;vertical-align:bottom;white-space:nowrap;direction:ltr;color:#000;font-size:10pt;font-family:Arial,sans-serif}
.ritz .waffle a{color:inherit}
.ritz .waffle td a{cursor:pointer}
.ritz .waffle .softmerge{overflow:visible}
.ritz .waffle .softmerge-inner{overflow:hidden;position:relative;text-overflow:clip;white-space:nowrap}
.ritz .waffle thead th{position:sticky;top:0;z-index:3;height:20px;background:var(--hdr);color:var(--muted);font:11px Roboto,Arial;font-weight:400;text-align:center;border-right:1px solid var(--hdr-line);border-bottom:1px solid var(--hdr-line);padding:0;overflow:hidden}
.ritz .waffle th.row-header-shim{width:46px;min-width:46px}
.ritz .waffle thead th.row-header-shim{left:0;z-index:5}
.ritz .waffle tbody th.row-headers-background{position:sticky;left:0;z-index:2;background:var(--hdr);color:var(--muted);font:11px Roboto,Arial;text-align:center;border-right:1px solid var(--hdr-line);border-bottom:1px solid var(--hdr-line);padding:0}
tr.frz > td{background-color:#fff}  /* low specificity: the sheet's own cell colours win */
.ritz .waffle tr.frz > th{z-index:4}
.ritz .waffle .freezebar-cell{background:#c4c7c5 !important;padding:0;height:4px;border:0}
.ritz .waffle tbody th.freezebar-cell{position:sticky;left:0}
.ritz .waffle td.hl{outline:3px solid #1a73e8;outline-offset:-3px;animation:hl 2.4s ease-out}
@keyframes hl{0%{box-shadow:inset 0 0 0 999px rgba(26,115,232,.35)}100%{box-shadow:inset 0 0 0 999px rgba(26,115,232,0)}}

/* ---- per-tab cell styles extracted from Google ---- */
/*__SHEET_CSS__*/
</style>
</head>
<body>
<header>
  <svg class="logo" viewBox="0 0 48 48" aria-hidden="true"><path fill="#0f9d58" d="M29 4H11a3 3 0 0 0-3 3v34a3 3 0 0 0 3 3h26a3 3 0 0 0 3-3V15z"/><path fill="#87ceac" d="M29 4v11h11z"/><path fill="#f1f1f1" d="M15 21h18v15H15zm2 2v3h6v-3zm8 0v3h6v-3zm-8 5v3h6v-3zm8 0v3h6v-3z"/></svg>
  <div class="title"><h1 id="docTitle"></h1><small id="docMeta"></small></div>
  <div class="search">
    <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14"/></svg>
    <input id="q" type="search" placeholder="Search all tabs  (Ctrl+K)" autocomplete="off">
    <div id="results"></div>
  </div>
</header>
<main id="main"></main>
<footer>
  <button class="tbtn" id="allBtn" title="All sheets"><svg viewBox="0 0 24 24"><path d="M3 18h18v-2H3zm0-5h18v-2H3zm0-7v2h18V6z"/></svg></button>
  <nav id="tabs"></nav>
</footer>
<div id="menu"></div>
<div id="toast"></div>

<!--__TEMPLATES__-->

<script>
// if anything below throws, say so on the page instead of showing a blank viewer
window.addEventListener('error', e => {
  if (!e.message) return;  // resource load errors (images) have no message
  const m = document.getElementById('main');
  if (m && !m.querySelector('.pane')) m.innerHTML = '<p class="missing">Viewer error: ' +
    String(e.message).replace(/</g, '&lt;') + ' (line ' + e.lineno + '). Press F12 → Console for details.</p>';
});
</script>
<script>
const TABS = /*__TABS__*/[];
const META = /*__META__*/{};
const byGid = Object.fromEntries(TABS.map(t => [t.gid, t]));
const $ = s => document.querySelector(s);
let current = null;

document.title = (META.title || document.title) + (META.extracted_at ? ' (' + META.extracted_at + ')' : '');
$('#docTitle').textContent = (META.title || '') + (META.extracted_at ? '  (' + META.extracted_at + ')' : '');
$('#docMeta').innerHTML = 'Offline copy · snapshot ' + (META.extracted_at || META.extracted || '') +
  (META.url ? ' · <a href="' + META.url + '" target="_blank" rel="noopener">open original</a>' : '');

function colName(n){let s='';n++;while(n>0){const m=(n-1)%26;s=String.fromCharCode(65+m)+s;n=Math.floor((n-1)/26);}return s;}

function toast(msg){const t=$('#toast');t.textContent=msg;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(()=>t.classList.remove('show'),2600);}

function pane(gid){
  let p = document.getElementById('pane-' + gid);
  if (p) return p;
  p = document.createElement('div');
  p.className = 'pane'; p.id = 'pane-' + gid;
  const wrap = document.createElement('div'); wrap.className = 'ritz';
  const tpl = document.getElementById('tpl-' + gid);
  if (tpl) wrap.appendChild(tpl.content.cloneNode(true));
  p.appendChild(wrap);
  $('#main').appendChild(p);
  const table = wrap.querySelector('table');
  if (table) decorate(table);
  return p;
}

function decorate(table){
  // column letters in the header shims
  let c = 0;
  table.querySelectorAll('thead th.header-shim').forEach(th => {
    if (th.classList.contains('row-header-shim')) return;
    th.textContent = colName(c++);
  });
  // frozen rows: everything above the horizontal freeze bar sticks to the top
  const rows = [...table.tBodies[0]?.rows || []];
  const fi = rows.findIndex(r => r.querySelector('.freezebar-horizontal-handle'));
  if (fi >= 0 && fi < 30) {
    requestAnimationFrame(() => {
      let top = table.tHead ? table.tHead.getBoundingClientRect().height : 0;
      for (let i = 0; i <= fi; i++) {
        const r = rows[i]; r.classList.add('frz');
        for (const cell of r.cells) { cell.style.position = 'sticky'; cell.style.top = top + 'px';
          if (cell.tagName === 'TH') cell.style.left = '0'; }
        top += r.getBoundingClientRect().height;
      }
    });
  }
}

function show(gid, push = true){
  if (!byGid[gid]) gid = TABS[0].gid;
  const p = pane(gid);
  document.querySelectorAll('.pane.active').forEach(x => x.classList.remove('active'));
  p.classList.add('active');
  current = gid;
  document.querySelectorAll('#tabs button, #menu button').forEach(b => b.classList.toggle('active', b.dataset.gid === gid));
  const btn = document.querySelector('#tabs button[data-gid="' + gid + '"]');
  if (btn) btn.scrollIntoView({block: 'nearest', inline: 'nearest'});
  if (push && location.hash !== '#gid=' + gid) history.replaceState(null, '', '#gid=' + gid);
  document.title = byGid[gid].name + ' – ' + (META.title || '') + (META.extracted_at ? ' (' + META.extracted_at + ')' : '');
  return p;
}

function flash(td){
  if (!td) return;
  td.scrollIntoView({block: 'center', inline: 'center'});
  td.classList.remove('hl'); void td.offsetWidth; td.classList.add('hl');
  setTimeout(() => td.classList.remove('hl'), 2500);
}

function gotoRow(gid, rowNum){
  const p = show(gid);
  const th = document.getElementById(gid + 'R' + (rowNum - 1));
  if (th) flash(th.parentElement.querySelector('td') || th);
  else p.scrollTop = 0;
}

// --- images that fail to load: show an empty cell, like the live sheet does
document.addEventListener('error', e => {
  if (e.target.tagName === 'IMG') e.target.style.visibility = 'hidden';
}, true);

// --- tab bar + menu
const tabsEl = $('#tabs'), menu = $('#menu');
TABS.forEach(t => {
  const b = document.createElement('button');
  b.textContent = t.name; b.dataset.gid = t.gid; b.title = t.file;
  if (/^old\b/i.test(t.name)) b.classList.add('old');
  b.onclick = () => show(t.gid);
  tabsEl.appendChild(b);
  const m = b.cloneNode(true); m.onclick = () => { show(t.gid); menu.classList.remove('open'); };
  menu.appendChild(m);
});
$('#allBtn').onclick = e => { e.stopPropagation(); menu.classList.toggle('open'); };

// --- text index for search and named-range resolution (built lazily from templates)
let INDEX = null;
function buildIndex(){
  if (INDEX) return INDEX;
  INDEX = [];
  TABS.forEach(t => {
    const tpl = document.getElementById('tpl-' + t.gid); if (!tpl) return;
    tpl.content.querySelectorAll('td').forEach((td, k) => {
      const text = td.textContent.replace(/\s+/g, ' ').trim();
      if (!text) return;
      const th = td.parentElement.querySelector('th[id]');
      const row = th ? (+th.id.split('R').pop() + 1) : null;
      INDEX.push({gid: t.gid, k, row, text, lower: text.toLowerCase(), isLink: !!td.querySelector('a')});
    });
  });
  return INDEX;
}
function cellOf(gid, k){ show(gid); return document.getElementById('pane-' + gid).querySelectorAll('td')[k]; }

// --- links inside the sheet
document.addEventListener('click', e => {
  if (!e.target.closest('#menu, #allBtn')) menu.classList.remove('open');
  if (!e.target.closest('.search')) $('#results').classList.remove('open');
  const a = e.target.closest('.pane a[href^="#"]');
  if (!a) return;
  e.preventDefault();
  const h = a.getAttribute('href');
  let m = h.match(/^#gid=(-?\d+)(?:&range=([A-Z]+)(\d+))?/);
  if (m) { if (!byGid[m[1]]) return toast('That tab is not part of this copy.');
           return m[3] ? gotoRow(m[1], +m[3]) : (show(m[1]).scrollTop = 0); }
  if (/^#rangeid=/.test(h)) {
    const label = a.textContent.replace(/\s+/g, ' ').trim();
    const p = document.getElementById('pane-' + current);
    if (/back to the top/i.test(label)) { p.scrollTop = 0; p.scrollLeft = 0; return; }
    // named ranges aren't exported; jump to the matching heading/cell instead
    const idx = buildIndex(), want = label.toLowerCase();
    const srcTd = a.closest('td'), srcK = [...p.querySelectorAll('td')].indexOf(srcTd);
    const cands = idx.filter(x => !x.isLink && (x.lower === want || x.lower.startsWith(want)));
    const pick = cands.find(x => x.gid === current && x.k !== srcK && x.lower === want)
              || cands.find(x => x.gid === current && x.k !== srcK)
              || cands.find(x => x.lower === want) || cands[0];
    if (pick) return flash(cellOf(pick.gid, pick.k));
    toast('Couldn’t find the target of “' + label + '” in this copy.');
  }
});

// --- search
const q = $('#q'), res = $('#results');
let hits = [], sel = -1;
function esc(s){return s.replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));}
function snippet(text, term){
  const i = text.toLowerCase().indexOf(term); const start = Math.max(0, i - 50);
  const s = (start ? '…' : '') + text.slice(start, i + term.length + 110) + (i + term.length + 110 < text.length ? '…' : '');
  return esc(s).replace(new RegExp(term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'ig'), m => '<mark>' + m + '</mark>');
}
function runSearch(){
  const term = q.value.trim().toLowerCase();
  if (term.length < 2) { res.classList.remove('open'); return; }
  const all = buildIndex().filter(x => x.lower.includes(term));
  // name-like cells (start with the term) first
  all.sort((a, b) => (b.lower.startsWith(term) - a.lower.startsWith(term)) || (a.text.length - b.text.length));
  hits = all.slice(0, 200); sel = -1;
  res.innerHTML = '<div class="info">' + all.length + ' match' + (all.length === 1 ? '' : 'es') + (all.length > 200 ? ' · showing 200' : '') + '</div>' +
    hits.map((h, i) => '<button class="hit" data-i="' + i + '"><span class="tab">' + esc(byGid[h.gid].name) + '</span><span class="row">' + (h.row ? 'row ' + h.row : '') + '</span><br>' + snippet(h.text, term) + '</button>').join('');
  res.classList.add('open');
}
let timer; q.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(runSearch, 150); });
q.addEventListener('focus', () => { if (q.value.trim().length >= 2) runSearch(); });
res.addEventListener('click', e => { const b = e.target.closest('.hit'); if (b) open(+b.dataset.i); });
function open(i){ const h = hits[i]; if (!h) return; res.classList.remove('open'); flash(cellOf(h.gid, h.k)); }
q.addEventListener('keydown', e => {
  const btns = res.querySelectorAll('.hit');
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault(); sel = Math.max(0, Math.min(btns.length - 1, sel + (e.key === 'ArrowDown' ? 1 : -1)));
    btns.forEach((b, i) => b.classList.toggle('sel', i === sel)); btns[sel]?.scrollIntoView({block: 'nearest'});
  } else if (e.key === 'Enter') { open(sel >= 0 ? sel : 0); }
  else if (e.key === 'Escape') { res.classList.remove('open'); q.blur(); }
});
document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); q.focus(); q.select(); }
  if (e.altKey && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {   // Sheets: Alt+↑/↓ switches tabs
    e.preventDefault(); const i = TABS.findIndex(t => t.gid === current);
    show(TABS[(i + (e.key === 'ArrowDown' ? 1 : TABS.length - 1)) % TABS.length].gid);
  }
});

window.addEventListener('hashchange', () => { const m = location.hash.match(/gid=(-?\d+)/); if (m && m[1] !== current) show(m[1], false); });
const m0 = location.hash.match(/gid=(-?\d+)/);
show(m0 ? m0[1] : TABS[0].gid);
</script>
</body></html>

""";
}
