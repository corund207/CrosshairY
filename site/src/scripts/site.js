import data from '../data/patterns.json';

const REPO = 'corund207/CrosshairY';
const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (s, r = document) => r.querySelector(s);
const el = (tag, attrs = {}, parent) => {
  const n = document.createElementNS(SVGNS, tag);
  for (const k in attrs) n.setAttribute(k, attrs[k]);
  if (parent) parent.appendChild(n);
  return n;
};

/* ------------------------------------------------------------------ sheet tabs + keys 1–6 */

const tabs = [...document.querySelectorAll('.tabs a')];
const sheets = tabs.map((t) => document.getElementById(t.dataset.sheet));
const io = new IntersectionObserver(
  (entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue;
      tabs.forEach((t) => t.setAttribute('aria-current', String(t.dataset.sheet === e.target.id)));
    }
  },
  { rootMargin: '-45% 0px -50% 0px' }
);
sheets.forEach((s) => s && io.observe(s));
addEventListener('keydown', (e) => {
  if (e.ctrlKey || e.metaKey || e.altKey || /input|textarea|select/i.test(e.target.tagName)) return;
  const n = Number(e.key);
  if (n >= 1 && n <= sheets.length) {
    sheets[n - 1].scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    tabs[n - 1].focus({ preventScroll: true });
  }
});

/* ------------------------------------------------------------------ CAD cursor (fine pointers only) */

const cad = $('#cad');
if (cad && matchMedia('(hover: hover) and (pointer: fine)').matches) {
  const h = $('.h', cad), v = $('.v', cad), box = $('.box', cad), xy = $('.xy', cad);
  let raf = 0, mx = -100, my = -100, over = false;
  const paint = () => {
    raf = 0;
    h.style.transform = `translateY(${my}px)`;
    v.style.transform = `translateX(${mx}px)`;
    box.style.transform = `translate(${mx}px, ${my}px)`;
    const cx = Math.round(mx - innerWidth / 2), cy = Math.round(my - innerHeight / 2);
    xy.textContent = `X ${cx >= 0 ? '+' : '−'}${Math.abs(cx)}  Y ${cy >= 0 ? '+' : '−'}${Math.abs(cy)}`;
    const flipX = mx > innerWidth - 150, flipY = my > innerHeight - 40;
    xy.style.transform = `translate(${mx + (flipX ? -12 - xy.offsetWidth : 14)}px, ${my + (flipY ? -30 : 12)}px)`;
    cad.classList.toggle('over', over);
  };
  addEventListener('pointermove', (e) => {
    if (e.pointerType !== 'mouse') return;
    mx = e.clientX; my = e.clientY;
    over = !!e.target.closest('a, button');
    document.body.classList.add('cad-on');
    if (!raf) raf = requestAnimationFrame(paint);
  }, { passive: true });
  document.documentElement.addEventListener('mouseleave', () => { document.body.classList.remove('cad-on'); mx = my = -100; paint(); });
}

/* ------------------------------------------------------------------ kinematic study: the spray demo */

const plot = $('#plot');
if (plot) {
  const svg = $('#plot-svg');
  const readout = $('#plot-readout');
  const gamesEl = $('#games'), weaponsEl = $('#weapons'), specEl = $('#spec');
  const tbody = $('#coords tbody'), coordsWrap = $('.coords-wrap');
  const short = { 'VALORANT': 'Valorant', 'Counter-Strike 2': 'CS2', 'Rust': 'Rust', 'Apex Legends': 'Apex' };
  let game = 'VALORANT';
  let pat = data.patterns.find((p) => p.game === 'VALORANT' && p.name === 'Vandal') || data.patterns[0];
  let geo, tracker, hitLayer, rows = [], firing = false, t0 = 0, raf = 0, fired = 0;

  const shotMs = () => 60000 / pat.rpm;
  // bullets: the accurate shots land dead center, then one bullet per pattern point
  const bullets = () => {
    const list = [];
    for (let k = 0; k < pat.stable - 1; k++) list.push([0, 0]);
    for (const p of pat.points) list.push(p);
    return list;
  };
  // tracker offset at time t, matching the app's animation (startDelay, then one point per shot)
  const posAt = (t) => {
    const delay = (pat.stable - 1) * shotMs();
    if (t <= delay) return [0, 0];
    const u = (t - delay) / shotMs();
    const pts = pat.points, i = Math.min(pts.length - 1, Math.floor(u));
    if (i >= pts.length - 1) return pts[pts.length - 1];
    const f = u - i, a = pts[i], b = pts[i + 1];
    return [a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f];
  };

  function layout() {
    const pts = pat.points;
    const xs = pts.map((p) => p[0]).concat(0), ys = pts.map((p) => p[1]).concat(0);
    const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    const s = Math.min(6, 400 / Math.max(20, maxY - minY), 420 / Math.max(20, maxX - minX));
    return { s, ox: 300 - ((minX + maxX) / 2) * s, oy: 500 - maxY * s };
  }
  const P = (p) => [geo.ox + p[0] * geo.s, geo.oy + p[1] * geo.s];

  function draw() {
    stop(true);
    geo = layout();
    svg.textContent = '';
    const defs = el('defs', {}, svg);
    const mk = el('marker', { id: 'parr', viewBox: '0 0 10 10', refX: 9, refY: 5, markerWidth: 7, markerHeight: 7, orient: 'auto-start-reverse' }, defs);
    el('path', { d: 'M0 1 10 5 0 9z', fill: '#45C4D6' }, mk);
    el('line', { class: 'axis', x1: 20, y1: geo.oy, x2: 580, y2: geo.oy }, svg);
    el('line', { class: 'axis', x1: geo.ox, y1: 30, x2: geo.ox, y2: 580 }, svg);
    const path = pat.points.map(P);
    el('polyline', { class: 'path', points: path.map((p) => p.join(',')).join(' ') }, svg);
    path.forEach((p, i) => {
      el('circle', { class: 'pt', cx: p[0], cy: p[1], r: 1.6 }, svg);
      if (i > 0 && (i + 1) % 5 === 0) {
        const t = el('text', { class: 'pt-n', x: p[0] + 7, y: p[1] + 3 }, svg);
        t.textContent = String(i + pat.stable);
      }
    });
    // static reference crosshair at the origin
    const ref = el('g', { stroke: '#7c8389', 'stroke-width': 1.5 }, svg);
    [[0, -9, 0, -19], [0, 9, 0, 19], [-9, 0, -19, 0], [9, 0, 19, 0]].forEach(([a, b, c, d]) =>
      el('line', { x1: geo.ox + a, y1: geo.oy + b, x2: geo.ox + c, y2: geo.oy + d }, ref));
    // scale bar: 10 px at 1080p
    const sb = el('g', { class: 'scale-bar' }, svg);
    const len = 10 * geo.s;
    el('line', { x1: 24, y1: 572, x2: 24 + len, y2: 572 }, sb);
    el('line', { x1: 24, y1: 567, x2: 24, y2: 577 }, sb);
    el('line', { x1: 24 + len, y1: 567, x2: 24 + len, y2: 577 }, sb);
    const sbt = el('text', { x: 30 + len, y: 576 }, sb);
    sbt.textContent = '10 PX';
    // cyan dimension: total climb of the spray
    const top = Math.min(...pat.points.map((p) => p[1]));
    if (top < -4) {
      const dg = el('g', { class: 'dimln' }, svg);
      const yTop = geo.oy + top * geo.s, xd = 560;
      el('line', { x1: geo.ox + 26, y1: geo.oy + 0.5, x2: xd + 8, y2: geo.oy + 0.5 }, dg);
      el('line', { x1: geo.ox + 26, y1: yTop + 0.5, x2: xd + 8, y2: yTop + 0.5 }, dg);
      el('line', { x1: xd, y1: geo.oy - 3, x2: xd, y2: yTop + 3, 'marker-start': 'url(#parr)', 'marker-end': 'url(#parr)' }, dg);
      const tx = el('text', { x: xd - 8, y: (geo.oy + yTop) / 2, 'text-anchor': 'end' }, dg);
      tx.textContent = `CLIMB ${-top} PX`;
    }
    hitLayer = el('g', {}, svg);
    tracker = el('g', {}, svg);
    el('circle', { r: 7.5, fill: '#0e1012', stroke: '#0e1012', 'stroke-width': 3 }, tracker);
    el('circle', { r: 6, fill: '#F5A524' }, tracker);
    moveTracker([0, 0]);
    fillTable();
    fillSpec();
    setReadout();
  }

  function moveTracker(p) {
    const [x, y] = P(p);
    tracker.setAttribute('transform', `translate(${x} ${y})`);
  }

  function setReadout(extra) {
    readout.innerHTML = `<b>${pat.name.toUpperCase()}</b> · ${pat.game.toUpperCase()}<br>SCALE ${geo.s.toFixed(1)}:1${extra ? '<br>' + extra : ''}`;
  }

  function fillSpec() {
    const total = pat.stable - 1 + pat.points.length;
    specEl.innerHTML = `<div>Fire rate<b>${pat.rpm} rpm</b></div><div>Accurate<b>${pat.stable} shot${pat.stable > 1 ? 's' : ''}</b></div><div>Plotted<b>${total} rounds</b></div>`;
  }

  function fillTable() {
    tbody.textContent = '';
    rows = bullets().map((b, k) => {
      const tr = document.createElement('tr');
      if (k < pat.stable) tr.className = 'acc';
      tr.innerHTML = `<td>${String(k + 1).padStart(2, '0')}</td><td>${b[0]}</td><td>${b[1]}</td><td>${Math.round(k * shotMs())}</td>`;
      tbody.appendChild(tr);
      return tr;
    });
    coordsWrap.scrollTop = 0;
  }

  function addHit(k, b) {
    const [x, y] = P(b);
    el('circle', { class: 'hit', cx: x, cy: y, r: 3.4 }, hitLayer);
    if (k === 0 || (k + 1) % 5 === 0) {
      const t = el('text', { class: 'hit-n', x: x - 8, y: y + 4, 'text-anchor': 'end' }, hitLayer);
      t.textContent = String(k + 1);
    }
    rows.forEach((r) => r.classList.remove('on'));
    const row = rows[k];
    if (row) {
      row.classList.add('on');
      const top = row.offsetTop - coordsWrap.clientHeight / 2;
      coordsWrap.scrollTop = Math.max(0, top);
    }
  }

  function frame(now) {
    const t = now - t0;
    const list = bullets();
    while (fired < list.length && fired * shotMs() <= t) { addHit(fired, list[fired]); fired++; }
    moveTracker(posAt(t));
    setReadout(`ROUND ${Math.min(fired, list.length)} / ${list.length}`);
    if (fired < list.length || t < (list.length) * shotMs()) raf = requestAnimationFrame(frame);
  }

  function start() {
    if (firing) return;
    firing = true;
    plot.classList.add('firing');
    plot.setAttribute('aria-pressed', 'true');
    hitLayer.textContent = '';
    rows.forEach((r) => r.classList.remove('on'));
    fired = 0;
    t0 = performance.now();
    raf = requestAnimationFrame(frame);
  }

  function stop(silent) {
    if (!firing) return;
    firing = false;
    cancelAnimationFrame(raf);
    plot.classList.remove('firing');
    plot.setAttribute('aria-pressed', 'false');
    moveTracker([0, 0]);   // release: the tracker snaps back
    if (!silent) setReadout(`FIRED ${fired} · RELEASED`);
  }

  plot.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return;
    e.preventDefault();
    plot.setPointerCapture(e.pointerId);
    start();
  });
  plot.addEventListener('pointerup', () => stop());
  plot.addEventListener('pointercancel', () => stop());
  plot.addEventListener('lostpointercapture', () => stop());
  plot.addEventListener('contextmenu', (e) => e.preventDefault());
  plot.addEventListener('keydown', (e) => {
    if ((e.key === ' ' || e.key === 'Enter') && !e.repeat) { e.preventDefault(); start(); }
  });
  plot.addEventListener('keyup', (e) => {
    if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); stop(); }
  });
  plot.addEventListener('blur', () => stop());

  function renderGames() {
    gamesEl.textContent = '';
    for (const g of data.games) {
      const b = document.createElement('button');
      b.type = 'button';
      b.textContent = short[g] || g;
      b.setAttribute('aria-pressed', String(g === game));
      b.addEventListener('click', () => {
        game = g;
        pat = data.patterns.find((p) => p.game === g && p.handTuned) || data.patterns.find((p) => p.game === g);
        renderGames(); renderWeapons(); draw();
      });
      gamesEl.appendChild(b);
    }
  }

  function renderWeapons() {
    weaponsEl.textContent = '';
    for (const p of data.patterns.filter((x) => x.game === game)) {
      const b = document.createElement('button');
      b.type = 'button';
      b.innerHTML = p.name + (p.handTuned ? '<svg class="tuned" viewBox="0 0 10 10" role="img" aria-label="hand-tuned"><path d="M5 .8 9.2 5 5 9.2.8 5z"/></svg>' : '');
      b.title = `${p.category} · ${p.rpm} rpm`;
      b.setAttribute('aria-pressed', String(p === pat));
      b.addEventListener('click', () => {
        pat = p;
        weaponsEl.querySelectorAll('button').forEach((x) => x.setAttribute('aria-pressed', 'false'));
        b.setAttribute('aria-pressed', 'true');
        draw();
      });
      weaponsEl.appendChild(b);
    }
  }

  renderGames();
  renderWeapons();
  draw();
}

/* ------------------------------------------------------------------ live release + gallery refresh */

(async () => {
  try {
    const r = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: 'application/vnd.github+json' } });
    if (!r.ok) return;
    const j = await r.json();
    const a = (j.assets || []).find((x) => x.name === 'CrosshairY.exe');
    const set = (k, v) => document.querySelectorAll(`[data-rel="${k}"]`).forEach((n) => { if (v) n.textContent = v; });
    set('version', String(j.tag_name || '').replace(/^v/, ''));
    set('date', String(j.published_at || '').slice(0, 10));
    if (a) {
      set('size', (a.size / 1048576).toFixed(1) + ' MB');
      if (a.digest) set('sha', String(a.digest).replace('sha256:', '').toUpperCase());
    }
  } catch {}
})();

(async () => {
  const wrap = $('#gallery');
  if (!wrap) return;
  const status = $('#gallery-status');
  try {
    const r = await fetch(`https://raw.githubusercontent.com/${REPO}/main/gallery/gallery.json`, { cache: 'no-cache' });
    if (!r.ok) throw new Error(String(r.status));
    const list = (await r.json()).crosshairs || [];
    if (list.length === wrap.children.length) { status?.remove(); return; }
    wrap.textContent = '';
    for (const g of list) {
      const f = document.createElement('figure');
      const box = document.createElement('div'); box.className = 'gimg';
      const img = new Image(492, 240);
      img.addEventListener('error', () => box.classList.add('missing'));
      img.loading = 'lazy';
      img.src = `https://raw.githubusercontent.com/${REPO}/main/gallery/previews/${encodeURIComponent(g.id)}.png`;
      img.alt = `${g.name} crosshair on a dark and a light background`;
      const cap = document.createElement('figcaption');
      const n = document.createElement('span'); n.className = 'gname'; n.textContent = g.name;
      const by = document.createElement('span'); by.className = 'gby'; by.textContent = g.author || '';
      cap.append(n, by);
      box.append(img);
      f.append(box, cap);
      wrap.appendChild(f);
    }
    status?.remove();
  } catch {
    if (status) status.textContent = "Couldn't load the gallery right now. It's in the app under Discover › Community gallery.";
  }
})();
