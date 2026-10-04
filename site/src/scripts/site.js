import data from '../data/patterns.json';

const REPO = 'corund207/Reticly';
const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (s, r = document) => r.querySelector(s);
const el = (tag, attrs = {}, parent) => {
  const n = document.createElementNS(SVGNS, tag);
  for (const k in attrs) n.setAttribute(k, attrs[k]);
  if (parent) parent.appendChild(n);
  return n;
};
const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
const clamp = (v, a = 0, b = 1) => Math.min(b, Math.max(a, v));

/* ------------------------------------------------------------------ reveal on view */

const io = new IntersectionObserver((entries) => {
  for (const e of entries) if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
}, { rootMargin: '0px 0px -12% 0px' });
document.querySelectorAll('.reveal').forEach((n) => io.observe(n));

/* ------------------------------------------------------------------ count-up numbers */

const counter = new IntersectionObserver((entries) => {
  for (const e of entries) {
    if (!e.isIntersecting) continue;
    counter.unobserve(e.target);
    const n = e.target, to = Number(n.dataset.count), suffix = n.dataset.suffix || '';
    if (reduce || to === 0) { n.textContent = to + suffix; continue; }
    const t0 = performance.now(), dur = 1400;
    const step = (now) => {
      const k = clamp((now - t0) / dur), eased = 1 - Math.pow(1 - k, 4);
      n.textContent = Math.round(to * eased) + suffix;
      if (k < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }
}, { threshold: 0.6 });
document.querySelectorAll('[data-count]').forEach((n) => counter.observe(n));

/* ------------------------------------------------------------------ spray helpers (shared) */

const vandal = data.patterns.find((p) => p.game === 'VALORANT' && p.name === 'Vandal') || data.patterns[0];
const bulletsOf = (pat) => {
  const list = [];
  for (let k = 0; k < pat.stable - 1; k++) list.push([0, 0]);
  for (const p of pat.points) list.push(p);
  return list;
};
// tracker offset at time t (ms), matching the app's animation: hold for the accurate shots, then one point per shot
const posAt = (pat, t) => {
  const shot = 60000 / pat.rpm, delay = (pat.stable - 1) * shot;
  if (t <= delay) return [0, 0];
  const u = (t - delay) / shot, pts = pat.points, i = Math.min(pts.length - 1, Math.floor(u));
  if (i >= pts.length - 1) return pts[pts.length - 1];
  const f = u - i, a = pts[i], b = pts[i + 1];
  return [a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f];
};
const fit = (pat, box = 420, bottom = 500) => {
  const xs = pat.points.map((p) => p[0]).concat(0), ys = pat.points.map((p) => p[1]).concat(0);
  const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
  const s = Math.min(6, (box - 20) / Math.max(20, maxY - minY), box / Math.max(20, maxX - minX));
  return { s, ox: 300 - ((minX + maxX) / 2) * s, oy: bottom - maxY * s };
};
function trackerGroup(parent) {
  const g = el('g', {}, parent);
  el('circle', { r: 9, fill: '#000' }, g);
  el('circle', { r: 7, fill: '#f5a524' }, g);
  return g;
}

/* ------------------------------------------------------------------ scroll-driven sections */

const scrollies = [...document.querySelectorAll('[data-scrolly]')];
const words = [...document.querySelectorAll('.words span')];
const hot = new Set(['exact', 'center', 'recoil,', 'bullet', 'bullet.']);
words.forEach((w) => { if (hot.has(w.textContent.trim().toLowerCase())) w.dataset.hot = '1'; });

// the recoil section: a pinned stage whose spray is scrubbed by scroll position
const spray = (() => {
  const svg = $('#spray-svg');
  if (!svg) return null;
  const geo = fit(vandal, 430, 520);
  const P = (p) => [geo.ox + p[0] * geo.s, geo.oy + p[1] * geo.s];
  el('line', { class: 'spray-axis', x1: 40, y1: geo.oy, x2: 560, y2: geo.oy }, svg);
  el('line', { class: 'spray-axis', x1: geo.ox, y1: 40, x2: geo.ox, y2: 570 }, svg);
  el('polyline', { class: 'spray-path', points: vandal.points.map(P).map((p) => p.join(',')).join(' ') }, svg);
  const ref = el('g', { class: 'spray-ref' }, svg);
  [[0, -12, 0, -26], [0, 12, 0, 26], [-12, 0, -26, 0], [12, 0, 26, 0]].forEach(([a, b, c, d]) =>
    el('line', { x1: geo.ox + a, y1: geo.oy + b, x2: geo.ox + c, y2: geo.oy + d }, ref));
  const hits = el('g', {}, svg);
  const list = bulletsOf(vandal);
  const marks = list.map((b) => { const [x, y] = P(b); const c = el('circle', { class: 'spray-hit', cx: x, cy: y, r: 4.5 }, hits); c.style.opacity = 0; return c; });
  const tracker = trackerGroup(svg);
  const roundEl = $('#spray-round b'), totalEl = $('#spray-round span');
  totalEl.textContent = '/ ' + list.length;
  const caps = [...document.querySelectorAll('.cap')];
  const shot = 60000 / vandal.rpm, total = list.length * shot;
  return (p) => {
    // 0–0.86 of the section fires the whole spray, the rest is the release
    const firing = p < 0.88;
    const t = clamp(p / 0.86) * total;
    const fired = firing ? Math.min(list.length, Math.floor(t / shot) + (p > 0.02 ? 1 : 0)) : list.length;
    marks.forEach((m, k) => { m.style.opacity = k < fired ? (firing ? 1 : 0.35) : 0; });
    const [x, y] = firing ? P(posAt(vandal, t)) : P([0, 0]);
    tracker.setAttribute('transform', `translate(${x} ${y})`);
    roundEl.textContent = String(firing ? fired : 0);
    const ci = !firing ? 3 : p < 0.22 ? 0 : p < 0.6 ? 1 : 2;
    caps.forEach((c, i) => c.classList.toggle('on', i === ci));
  };
})();

function update() {
  const vh = innerHeight;
  for (const s of scrollies) {
    const r = s.getBoundingClientRect();
    const span = Math.max(1, r.height - vh);
    const p = clamp(-r.top / span);
    s.style.setProperty('--p', p.toFixed(4));
    const kind = s.dataset.scrolly;
    if (kind === 'statement') {
      const lit = Math.round(p * 1.25 * words.length);
      words.forEach((w, i) => { w.classList.toggle('on', i < lit); w.classList.toggle('hot', i < lit && w.dataset.hot === '1'); });
    } else if (kind === 'recoil' && spray) spray(p);
  }
}
if (!reduce) {
  let raf = 0;
  addEventListener('scroll', () => { if (!raf) raf = requestAnimationFrame(() => { raf = 0; update(); }); }, { passive: true });
  addEventListener('resize', update);
  update();
} else {
  words.forEach((w) => w.classList.add('on'));
  if (spray) spray(0.5);
}

/* ------------------------------------------------------------------ try it: interactive hold-to-fire */

const plot = $('#plot');
if (plot) {
  const svg = $('#plot-svg'), readout = $('#plot-readout');
  const gamesEl = $('#games'), weaponsEl = $('#weapons'), specEl = $('#spec');
  const short = { 'VALORANT': 'Valorant', 'Counter-Strike 2': 'CS2', 'Rust': 'Rust', 'Apex Legends': 'Apex' };
  let game = 'VALORANT', pat = vandal;
  let geo, tracker, hitLayer, firing = false, t0 = 0, raf = 0, fired = 0;
  const shotMs = () => 60000 / pat.rpm;
  const P = (p) => [geo.ox + p[0] * geo.s, geo.oy + p[1] * geo.s];

  function draw() {
    stop(true);
    geo = fit(pat);
    svg.textContent = '';
    const defs = el('defs', {}, svg);
    const mk = el('marker', { id: 'parr', viewBox: '0 0 10 10', refX: 9, refY: 5, markerWidth: 7, markerHeight: 7, orient: 'auto-start-reverse' }, defs);
    el('path', { d: 'M0 1 10 5 0 9z', fill: '#45C4D6' }, mk);
    el('line', { class: 'axis', x1: 20, y1: geo.oy, x2: 580, y2: geo.oy }, svg);
    el('line', { class: 'axis', x1: geo.ox, y1: 30, x2: geo.ox, y2: 580 }, svg);
    const path = pat.points.map(P);
    el('polyline', { class: 'path', points: path.map((p) => p.join(',')).join(' ') }, svg);
    path.forEach((p, i) => {
      el('circle', { class: 'pt', cx: p[0], cy: p[1], r: 1.7 }, svg);
      if (i > 0 && (i + 1) % 5 === 0) { const t = el('text', { class: 'pt-n', x: p[0] + 8, y: p[1] + 4 }, svg); t.textContent = String(i + pat.stable); }
    });
    const top = Math.min(...pat.points.map((p) => p[1]));
    if (top < -4) {
      const dg = el('g', { class: 'dimln' }, svg), yTop = geo.oy + top * geo.s, xd = 556;
      el('line', { x1: geo.ox + 30, y1: geo.oy, x2: xd + 8, y2: geo.oy }, dg);
      el('line', { x1: geo.ox + 30, y1: yTop, x2: xd + 8, y2: yTop }, dg);
      el('line', { x1: xd, y1: geo.oy - 3, x2: xd, y2: yTop + 3, 'marker-start': 'url(#parr)', 'marker-end': 'url(#parr)' }, dg);
      const tx = el('text', { x: xd - 8, y: (geo.oy + yTop) / 2, 'text-anchor': 'end' }, dg);
      tx.textContent = `Climb ${-top} px`;
    }
    const sb = el('g', { class: 'scale-bar' }, svg), len = 10 * geo.s;
    el('line', { x1: 26, y1: 570, x2: 26 + len, y2: 570 }, sb);
    const st = el('text', { x: 32 + len, y: 574 }, sb); st.textContent = '10 px';
    hitLayer = el('g', {}, svg);
    tracker = trackerGroup(svg);
    move([0, 0]);
    const total = pat.stable - 1 + pat.points.length;
    specEl.innerHTML = `<div><dt>Fire rate</dt><dd>${pat.rpm} rpm</dd></div><div><dt>Accurate</dt><dd>${pat.stable}</dd></div><div><dt>Rounds</dt><dd>${total}</dd></div>`;
    say();
  }
  const move = (p) => { const [x, y] = P(p); tracker.setAttribute('transform', `translate(${x} ${y})`); };
  const say = (extra) => { readout.innerHTML = `<b>${pat.name}</b> · ${pat.game}${extra ? '<br>' + extra : ''}`; };

  function frame(now) {
    const t = now - t0, list = bulletsOf(pat);
    while (fired < list.length && fired * shotMs() <= t) {
      const [x, y] = P(list[fired]);
      el('circle', { class: 'hit', cx: x, cy: y, r: 3.6 }, hitLayer);
      if (fired === 0 || (fired + 1) % 5 === 0) { const n = el('text', { class: 'hit-n', x: x - 9, y: y + 4, 'text-anchor': 'end' }, hitLayer); n.textContent = String(fired + 1); }
      fired++;
    }
    move(posAt(pat, t));
    say(`Round ${Math.min(fired, list.length)} of ${list.length}`);
    if (fired < list.length || t < list.length * shotMs()) raf = requestAnimationFrame(frame);
  }
  function start() {
    if (firing) return;
    firing = true; plot.classList.add('firing'); plot.setAttribute('aria-pressed', 'true');
    hitLayer.textContent = ''; fired = 0; t0 = performance.now(); raf = requestAnimationFrame(frame);
  }
  function stop(silent) {
    if (!firing) return;
    firing = false; cancelAnimationFrame(raf); plot.classList.remove('firing'); plot.setAttribute('aria-pressed', 'false');
    move([0, 0]);
    if (!silent) say(`Fired ${fired}. Released, back to center.`);
  }
  plot.addEventListener('pointerdown', (e) => { if (e.button !== 0) return; e.preventDefault(); plot.setPointerCapture(e.pointerId); start(); });
  plot.addEventListener('pointerup', () => stop());
  plot.addEventListener('pointercancel', () => stop());
  plot.addEventListener('lostpointercapture', () => stop());
  plot.addEventListener('contextmenu', (e) => e.preventDefault());
  plot.addEventListener('keydown', (e) => { if ((e.key === ' ' || e.key === 'Enter') && !e.repeat) { e.preventDefault(); start(); } });
  plot.addEventListener('keyup', (e) => { if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); stop(); } });
  plot.addEventListener('blur', () => stop());

  function renderGames() {
    gamesEl.textContent = '';
    for (const g of data.games) {
      const b = document.createElement('button');
      b.type = 'button'; b.textContent = short[g] || g; b.setAttribute('aria-pressed', String(g === game));
      b.addEventListener('click', () => {
        game = g; pat = data.patterns.find((p) => p.game === g && p.handTuned) || data.patterns.find((p) => p.game === g);
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
  renderGames(); renderWeapons(); draw();
}

/* ------------------------------------------------------------------ carousel */

const rail = $('#rail');
document.querySelectorAll('[data-rail]').forEach((b) => b.addEventListener('click', () => {
  const slide = rail.querySelector('.slide');
  rail.scrollBy({ left: Number(b.dataset.rail) * (slide.offsetWidth + 22), behavior: reduce ? 'auto' : 'smooth' });
}));

/* ------------------------------------------------------------------ live release + gallery */

(async () => {
  try {
    const r = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: 'application/vnd.github+json' } });
    if (!r.ok) return;
    const j = await r.json();
    const a = (j.assets || []).find((x) => x.name === 'Reticly.exe' || x.name === 'CrosshairY.exe');
    if (a?.browser_download_url) document.querySelectorAll('[data-dl]').forEach((n) => (n.href = a.browser_download_url));
    const set = (k, v) => document.querySelectorAll(`[data-rel="${k}"]`).forEach((n) => { if (v) n.textContent = v; });
    set('version', String(j.tag_name || '').replace(/^v/, ''));
    if (a) {
      set('size', (a.size / 1048576).toFixed(1) + ' MB');
      if (a.digest) set('sha', String(a.digest).replace('sha256:', '').toUpperCase());
    }
  } catch {}
})();

(async () => {
  const wrap = $('#gallery-grid');
  if (!wrap) return;
  const status = $('#gallery-status');
  try {
    const r = await fetch(`https://raw.githubusercontent.com/${REPO}/main/gallery/gallery.json`, { cache: 'no-cache' });
    if (!r.ok) throw new Error(String(r.status));
    const list = (await r.json()).crosshairs || [];
    if (list.length === wrap.children.length) { status?.remove(); return; }
    wrap.textContent = '';
    for (const g of list) {
      const f = document.createElement('figure'); f.className = 'gtile';
      const box = document.createElement('div'); box.className = 'gimg';
      const img = new Image(492, 240);
      img.addEventListener('error', () => box.classList.add('missing'));
      img.loading = 'lazy';
      img.src = `https://raw.githubusercontent.com/${REPO}/main/gallery/previews/${encodeURIComponent(g.id)}.png`;
      img.alt = `${g.name} crosshair`;
      const cap = document.createElement('figcaption');
      const n = document.createElement('span'); n.className = 'gname'; n.textContent = g.name;
      const by = document.createElement('span'); by.className = 'gby'; by.textContent = g.author || '';
      cap.append(n, by); box.append(img); f.append(box, cap); wrap.appendChild(f);
    }
    status?.remove();
  } catch {
    if (status) status.textContent = "Couldn't load the gallery right now. It's in the app under Discover › Community gallery.";
  }
})();
