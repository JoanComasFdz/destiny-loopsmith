// The rules page's behaviour: the tree folds, shows or hides and selects (below), and each flow's wires are drawn after layout from
// where its boxes landed (again on resize and when the theme changes; the arrowhead is the page's one marker): the triggers meet in one arrow into the first
// condition, the conditions are joined by "and", and from the last condition one line splits into an arrow per result.
const curve = (x1, y1, x2, y2) => { const k = (x2 - x1) / 2; return `M${x1},${y1} C${x1 + k},${y1} ${x2 - k},${y2} ${x2},${y2}`; };

// An odd number of results lines its middle one up with the last condition, so the middle arrow runs straight: the
// column gets a margin on one side (the flow centres it, so a margin of 2·d moves it by d).
function alignResults(flow){
  const col = flow.querySelector('.rcol'), boxes = col.querySelectorAll('.rbox'), cond = flow.querySelectorAll('.ccol .rbox');
  col.style.marginTop = col.style.marginBottom = '0px';
  if (boxes.length % 2 === 0 || !cond.length) return;
  const mid = boxes[(boxes.length - 1) / 2].getBoundingClientRect(), last = cond[cond.length - 1].getBoundingClientRect();
  const d = (last.top + last.bottom) / 2 - (mid.top + mid.bottom) / 2;
  if (d > 0) col.style.marginTop = `${2 * d}px`; else col.style.marginBottom = `${-2 * d}px`;
}

function wireFlow(flow){
  const svg = flow.querySelector('.wires'), o = flow.getBoundingClientRect();
  const box = el => { const r = el.getBoundingClientRect(); return {l: r.left - o.left, r: r.right - o.left, m: (r.top + r.bottom) / 2 - o.top}; };
  const trig = [...flow.querySelectorAll('.tcol .rbox')].map(box), cond = [...flow.querySelectorAll('.ccol .rbox')].map(box), res = [...flow.querySelectorAll('.rcol .rbox')].map(box);
  svg.setAttribute('width', flow.scrollWidth); svg.setAttribute('height', flow.offsetHeight);
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
  svg.innerHTML = s;
}
// Only the flows that are shown (a hidden one has no layout); a flow shown again is wired then.
function wireFlows(){
  const flows = [...document.querySelectorAll('.flow')].filter(flow => flow.offsetParent !== null);
  flows.forEach(alignResults);
  flows.forEach(wireFlow);
}

// The tree, like a folder explorer: an item's arrow folds it; its checkbox shows or hides its flows (and ticks or unticks
// everything under it; a folder with some ticked shows a dash); its name selects it and brings its flows into view.
const items = [...document.querySelectorAll('.titem')];
const section = item => document.querySelector(`.rflows [data-item="${CSS.escape(item.dataset.item)}"]`);
const childItems = item => [...item.querySelectorAll(':scope > ul > .titem')];
const box = item => item.querySelector(':scope > .trow > input');

// A rule's flows show when it is ticked; an element's or a file's part shows while any rule in it does.
function showTicked(){
  items.filter(item => item.classList.contains('rule')).forEach(item => { const part = section(item); if (part) part.hidden = !box(item).checked; });
  document.querySelectorAll('.rflows .relem, .rflows .rfile').forEach(part => part.hidden = !part.querySelector('.rrule:not([hidden])'));
  requestAnimationFrame(wireFlows);
}

// A folder, file or element is ticked when all under it are, unticked when none are, and shows a dash in between.
function settle(item){
  const kids = childItems(item);
  if (!kids.length) return;
  kids.forEach(settle);
  const ticked = kids.filter(kid => box(kid).checked && !box(kid).indeterminate).length, partly = kids.some(kid => box(kid).indeterminate);
  box(item).checked = ticked === kids.length;
  box(item).indeterminate = partly || (ticked > 0 && ticked < kids.length);
}

function select(item){
  items.forEach(other => other.setAttribute('aria-selected', other === item));
  document.querySelectorAll('.rflows .hit').forEach(part => part.classList.remove('hit'));
  // A folder has no part of its own: its first file's stands for it.
  const part = section(item) ?? section(item.querySelector('.titem.file') ?? item);
  if (!part || part.hidden) return;
  part.classList.add('hit');
  part.scrollIntoView({behavior: 'smooth', block: 'start'});
}

items.forEach(item => {
  item.querySelector(':scope > .trow > .twisty:not(.none)')?.addEventListener('click', () =>
    item.setAttribute('aria-expanded', item.getAttribute('aria-expanded') !== 'true'));
  box(item).addEventListener('change', () => {
    item.querySelectorAll('input').forEach(input => { input.checked = box(item).checked; input.indeterminate = false; });
    if (items.length) settle(items[0]);
    showTicked();
  });
  item.querySelector(':scope > .trow > .tname').addEventListener('click', () => select(item));
});

addEventListener('resize', wireFlows);
addEventListener('load', wireFlows);
document.fonts?.ready.then(wireFlows);
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', wireFlows);
new MutationObserver(wireFlows).observe(document.documentElement, {attributes: true, attributeFilter: ['data-theme']});
requestAnimationFrame(wireFlows);
