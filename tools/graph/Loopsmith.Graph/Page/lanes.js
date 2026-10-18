// The lanes page's behaviour: the scope chips show one scope's lanes, and each lane's wires are drawn after layout from
// where its boxes landed (again on resize and when the theme changes): the triggers meet in one arrow into the first
// condition, the conditions are joined by "and", and from the last condition one line splits into an arrow per result.
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const head = () => `<defs><marker id="lhead" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" markerUnits="userSpaceOnUse" orient="auto"><path d="M0,0 L10,5 L0,10 L3,5 z" fill="${css('--fg')}"/></marker></defs>`;
const curve = (x1, y1, x2, y2) => { const k = (x2 - x1) / 2; return `M${x1},${y1} C${x1 + k},${y1} ${x2 - k},${y2} ${x2},${y2}`; };

// An odd number of results lines its middle one up with the last condition, so the middle arrow runs straight: the
// column gets a margin on one side (the lane centres it, so a margin of 2·d moves it by d).
function alignResults(lane){
  const col = lane.querySelector('.rcol'), boxes = col.querySelectorAll('.lbox'), cond = lane.querySelectorAll('.ccol .lbox');
  col.style.marginTop = col.style.marginBottom = '0px';
  if (boxes.length % 2 === 0 || !cond.length) return;
  const mid = boxes[(boxes.length - 1) / 2].getBoundingClientRect(), last = cond[cond.length - 1].getBoundingClientRect();
  const d = (last.top + last.bottom) / 2 - (mid.top + mid.bottom) / 2;
  if (d > 0) col.style.marginTop = `${2 * d}px`; else col.style.marginBottom = `${-2 * d}px`;
}

function wireLane(lane){
  const svg = lane.querySelector('.wires'), o = lane.getBoundingClientRect();
  const box = el => { const r = el.getBoundingClientRect(); return {l: r.left - o.left, r: r.right - o.left, m: (r.top + r.bottom) / 2 - o.top}; };
  const trig = [...lane.querySelectorAll('.tcol .lbox')].map(box), cond = [...lane.querySelectorAll('.ccol .lbox')].map(box), res = [...lane.querySelectorAll('.rcol .lbox')].map(box);
  svg.setAttribute('width', lane.scrollWidth); svg.setAttribute('height', lane.offsetHeight);
  let s = '';
  // Nothing in place but the trigger: one line from it splits into the results.
  const from = cond.length ? cond[cond.length - 1] : trig[0];
  if (cond.length){
    const first = cond[0], mx = first.l - 16;
    trig.forEach(t => s += `<path class="w" d="${curve(t.r, t.m, mx, first.m)}"/>`);
    s += `<path class="w arrow" d="M${mx},${first.m} L${first.l - 2},${first.m}"/>`;
    for (let i = 1; i < cond.length; i++){ const a = cond[i - 1], b = cond[i], x = (a.r + b.l) / 2;
      s += `<path class="w and" d="M${a.r},${a.m} L${b.l},${b.m}"/><text class="wl" x="${x}" y="${a.m - 6}" text-anchor="middle">and</text>`; }
  }
  const sx = from.r + 16;
  s += `<path class="w" d="M${from.r},${from.m} L${sx},${from.m}"/>`;
  res.forEach(r => s += `<path class="w arrow" d="${curve(sx, from.m, r.l - 2, r.m)}"/>`);
  svg.innerHTML = head() + s;
}

function wireLanes(){
  const lanes = document.querySelectorAll('.lscope:not([hidden]) .lane3');
  lanes.forEach(alignResults);
  lanes.forEach(wireLane);
}

function showScope(id){
  document.querySelectorAll('[data-scope-chip]').forEach(chip => chip.setAttribute('aria-pressed', chip.dataset.scopeChip === id));
  document.querySelectorAll('.lscope').forEach(scope => scope.hidden = scope.dataset.scope !== id);
  requestAnimationFrame(wireLanes);
}

document.querySelectorAll('[data-scope-chip]').forEach(chip => chip.onclick = () => showScope(chip.dataset.scopeChip));
addEventListener('resize', wireLanes);
addEventListener('load', wireLanes);
document.fonts?.ready.then(wireLanes);
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', wireLanes);
new MutationObserver(wireLanes).observe(document.documentElement, {attributes: true, attributeFilter: ['data-theme']});
showScope(document.querySelector('[data-scope-chip]').dataset.scopeChip);
