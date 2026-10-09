#!/usr/bin/env node
// The browser build on phones and tablets, headless: WebKit with an iPhone or iPad profile, or Chromium with an Android one
// (Playwright's device profiles: a touch screen, a coarse pointer, the phone's viewport and pixel ratio). It measures what
// would decide whether a phone's tab survives, and drives the game with real touch events through its on-screen controls.
//
//   node Tools/mobile-check.mjs --serve Builds/Pages --device "iPhone 15 landscape" [--play] [--out Logs/mobile/<name>]
//   node Tools/mobile-check.mjs --serve Builds/Pages --device desktop-chromium     (a desktop: the controls must stay hidden)
//
// Devices: any of Playwright's (WebKit for Apple's, Chromium otherwise), or desktop-chromium. Playwright is the blog's
// (Playwright 1.63; $PLAYWRIGHT_CORE to use another), WebKit its 2359 build ($WEBKIT_EXE), Chromium the newest in
// ~/.cache/ms-playwright ($CHROME). Nothing is installed.
//
// Measured: the time to the title ("[Title] show"), the peak of the WebAssembly heap, of what the page has given WebGL
// (textures, and whether they arrived compressed or as plain RGBA, buffers, render targets: counted from the calls), of the
// browser's content process's anonymous memory (RssAnon: the heap, the compiled code, the JavaScript heap and the decoded
// textures, without the shared libraries), the download, and the frame rate at the title and on the desk. iOS kills a tab
// well before a desktop runs out, at about 1 GB or less; this WebKit doesn't, so the numbers are the measure.
// In WebKit the page's sound is a silent stand-in (see WEBKIT_AUDIO): this machine's WebKit can't play or decode it.
// Phone and tablet profiles hide the desktop texture formats (S3TC/DXT, BPTC, RGTC) from the page, as a phone's GPU lacks them
// and this desktop GPU has them; --desktop-formats keeps them.
// --play goes on: taps through the title, picks up and turns an object with a drag, leans in with a pinch, and uses the
// on-screen buttons, with a screenshot at each step. Writes console.log, report.json and shots/ to --out.
import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const require = createRequire(import.meta.url);
const PW = process.env.PLAYWRIGHT_CORE ?? path.join(os.homedir(), "Sites/blog/node_modules/playwright-core");
const { webkit, chromium, devices } = require(PW);

const args = process.argv.slice(2);
const opt = { device: "iPhone 15 landscape", serve: null, url: null, port: 0, out: null, play: false, timeout: 300, query: "" };
for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (a === "--device") opt.device = args[++i];
  else if (a === "--serve") opt.serve = args[++i];
  else if (a === "--port") opt.port = Number(args[++i]);
  else if (a === "--out") opt.out = args[++i];
  else if (a === "--play") opt.play = true;
  else if (a === "--timeout") opt.timeout = Number(args[++i]);
  else if (a === "--query") opt.query = args[++i];
  else if (a === "--desktop-formats") opt.desktopFormats = true;
  else if (a === "--trace-touch") opt.traceTouch = true;
  else if (a === "-h" || a === "--help") { console.log(fs.readFileSync(fileURLToPath(import.meta.url), "utf8").split("\nimport")[0]); process.exit(0); }
  else if (!a.startsWith("--") && !opt.url) opt.url = a;
  else { console.error(`unknown option ${a}`); process.exit(2); }
}
if (!opt.url && !opt.serve) { console.error("give a URL, or --serve <folder>"); process.exit(2); }
const desktop = opt.device === "desktop-chromium";
const profile = desktop ? { viewport: { width: 1600, height: 900 }, deviceScaleFactor: 1, isMobile: false, hasTouch: false, defaultBrowserType: "chromium" }
                        : devices[opt.device];
if (!profile) { console.error(`no device "${opt.device}" in Playwright's list`); process.exit(2); }
const isWebKit = profile.defaultBrowserType === "webkit";
const OUT = path.resolve(opt.out ?? path.join(ROOT, "Logs", "mobile", opt.device.replace(/\W+/g, "-").toLowerCase()));
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(path.join(OUT, "shots"), { recursive: true });

const t0 = Date.now();
const secs = () => ((Date.now() - t0) / 1000).toFixed(1);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const logFile = fs.openSync(path.join(OUT, "console.log"), "w");
let logBytes = 0;
function note(line) {
  const l = `[${secs()}s] ${line}\n`;
  if (logBytes < 4_000_000) { fs.writeSync(logFile, l); logBytes += l.length; }
}
function say(line) { console.log(line); note(`### ${line}`); }

// ---------------------------------------------------------------- a static server like GitHub Pages (as check-pages.mjs)

const TYPES = { ".html": "text/html; charset=utf-8", ".js": "application/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
  ".wasm": "application/wasm", ".png": "image/png", ".ttf": "font/ttf", ".json": "application/json", ".unityweb": "application/octet-stream" };
const served = { bytes: 0, files: {} };
function serve(dir, port) {
  const base = path.resolve(dir);
  const server = http.createServer((req, res) => {
    const u = new URL(req.url, "http://x");
    let p = decodeURIComponent(u.pathname);
    if (!p.startsWith("/LostAndFound/")) { res.writeHead(404); res.end(); return; }
    p = p.slice("/LostAndFound/".length);
    if (p === "" || p.endsWith("/")) p += "index.html";
    const file = path.join(base, p);
    if (!file.startsWith(base + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) { res.writeHead(404); res.end(); return; }
    const size = fs.statSync(file).size;
    res.writeHead(200, { "Content-Type": TYPES[path.extname(file)] ?? "application/octet-stream", "Content-Length": size, "Cache-Control": "no-cache" });
    served.bytes += size;
    served.files[p] = (served.files[p] ?? 0) + size;
    fs.createReadStream(file).pipe(res);
  });
  return new Promise((ok) => server.listen(port, "127.0.0.1", () => ok(server)));
}

/** $CHROME, or the newest Chromium already in Playwright's cache (as check-pages.mjs): nothing is downloaded. */
function findChromium() {
  if (process.env.CHROME) return process.env.CHROME;
  const pw = path.join(os.homedir(), ".cache", "ms-playwright");
  const dirs = fs.existsSync(pw) ? fs.readdirSync(pw).filter((d) => /^chromium-\d+$/.test(d)).sort((a, b) => b.split("-")[1] - a.split("-")[1]) : [];
  for (const d of dirs) { const c = path.join(pw, d, "chrome-linux64", "chrome"); if (fs.existsSync(c)) return c; }
  throw new Error("no Chromium in ~/.cache/ms-playwright (set $CHROME)");
}

// ---------------------------------------------------------------- memory: this run's browser processes, from /proc

function children() {
  const kids = new Map();
  for (const d of fs.readdirSync("/proc")) {
    if (!/^\d+$/.test(d)) continue;
    try {
      const st = fs.readFileSync(`/proc/${d}/stat`, "utf8");
      const ppid = Number(st.slice(st.lastIndexOf(")") + 2).split(" ")[1]);
      if (!kids.has(ppid)) kids.set(ppid, []);
      kids.get(ppid).push(Number(d));
    } catch {}
  }
  return kids;
}
function descendants(pid) {
  const kids = children(), out = [], todo = [pid];
  while (todo.length) for (const k of kids.get(todo.pop()) ?? []) { out.push(k); todo.push(k); }
  return out;
}
function kind(pid) {
  try {
    const cmd = fs.readFileSync(`/proc/${pid}/cmdline`, "utf8").replace(/\0/g, " ");
    if (/(WebKit|WPE)WebProcess/.test(cmd)) return "web";
    if (/(WebKit|WPE)GPUProcess/.test(cmd)) return "gpu";
    if (/--type=renderer/.test(cmd)) return "web";
    if (/--type=gpu-process/.test(cmd)) return "gpu";
    return null;
  } catch { return null; }
}
const mem = { web: 0, gpu: 0, at: {} };
function sampleMemory() {
  const now = { web: 0, gpu: 0 };
  for (const pid of descendants(process.pid)) {
    const k = kind(pid);
    if (!k) continue;
    try {
      const st = fs.readFileSync(`/proc/${pid}/status`, "utf8");
      const anon = Number(st.match(/RssAnon:\s+(\d+)/)?.[1] ?? 0) * 1024;
      now[k] = Math.max(now[k], anon);   // the biggest of its kind: the game's tab (a browser may keep a spare one)
    } catch {}
  }
  for (const k of ["web", "gpu"]) if (now[k] > mem[k]) { mem[k] = now[k]; mem.at[k] = +secs(); }
  mem.last = now;
  return now;
}

// ---------------------------------------------------------------- what the page is asked to count before anything runs

const HOOKS = `(() => {
  const w = window; if (w.__mob) return;
  const m = w.__mob = { heap: 0, heapPeak: 0, tex: 0, texPeak: 0, compressedTex: 0, plainTex: 0, buf: 0, bufPeak: 0, rb: 0, rbPeak: 0,
                        memories: [], touches: 0, audio: [] };
  const Mem = WebAssembly.Memory;
  WebAssembly.Memory = function (d) { const x = new Mem(d); m.memories.push(x); return x; };
  WebAssembly.Memory.prototype = Mem.prototype;
  for (const f of ['instantiate', 'instantiateStreaming']) {
    const o = WebAssembly[f]; if (!o) continue;
    WebAssembly[f] = function (...a) { return o.apply(this, a).then((r) => { const e = (r.instance || r).exports;
      for (const k in e) if (e[k] instanceof Mem && !m.memories.includes(e[k])) m.memories.push(e[k]); return r; }); };
  }
  setInterval(() => { m.heap = m.memories.reduce((s, x) => s + x.buffer.byteLength, 0); m.heapPeak = Math.max(m.heapPeak, m.heap); }, 100);
  for (const name of ['AudioContext', 'webkitAudioContext']) {
    const C = w[name]; if (!C) continue;
    w[name] = class extends C { constructor(...a) { super(...a); m.audio.push(this); } };
  }
  addEventListener('touchstart', () => m.touches++, true);
  // sound decoded to PCM (a float a sample, per channel): the browser holds it outside the WebAssembly heap
  m.audioDecoded = 0; m.audioClips = 0;
  const BAC = w.BaseAudioContext || w.AudioContext;
  if (BAC && BAC.prototype.decodeAudioData) {
    const dec = BAC.prototype.decodeAudioData;
    BAC.prototype.decodeAudioData = function (data, ok, err) {
      const count = (b) => { if (b) { m.audioDecoded += b.length * b.numberOfChannels * 4; m.audioClips++; } return b; };
      const p = dec.call(this, data, ok && ((b) => ok(count(b))), err);
      return p && p.then ? p.then((b) => (ok ? b : count(b))) : p;
    };
  }
  // WebGL: bytes per texel of the formats a Unity build asks for (sized internal formats, and the unsized ones of WebGL 1)
  const B = { 0x8058: 4, 0x8C43: 4, 0x1908: 4, 0x8051: 3, 0x8C41: 3, 0x1907: 3, 0x881A: 8, 0x8814: 16, 0x8C3A: 4, 0x8D62: 2, 0x8229: 1,
              0x822B: 2, 0x822D: 2, 0x822E: 4, 0x822F: 4, 0x8230: 8, 0x8232: 4, 0x8059: 4, 0x8056: 2, 0x8057: 2, 0x81A5: 2, 0x81A6: 3,
              0x88F0: 4, 0x8CAC: 4, 0x8CAD: 8, 0x8D48: 1, 0x1909: 1, 0x190A: 2, 0x1906: 1, 0x8C3D: 4, 0x881B: 6, 0x8815: 12, 0x8D7C: 4, 0x8D70: 16 };
  const size = new WeakMap();   // texture or buffer object: its bytes so far
  m.big = {};   // allocations of 256 KB or more: "call format WxH" -> [count, bytes]
  const big = (what, n) => { if (n < 262144) return; const e = m.big[what] || (m.big[what] = [0, 0]); e[0]++; e[1] += n; };
  const add = (o, n, key, peak) => { if (!o) return; size.set(o, (size.get(o) || 0) + n); m[key] += n; m[peak] = Math.max(m[peak], m[key]); };
  for (const Ctx of [w.WebGL2RenderingContext, w.WebGLRenderingContext]) {
    if (!Ctx) continue;
    const P = Ctx.prototype, bound = new WeakMap();   // per context: target -> object, per texture unit
    const state = (gl) => { let s = bound.get(gl); if (!s) bound.set(gl, s = { unit: 0, tex: {}, buf: {}, rb: null }); return s; };
    const texTarget = (t) => (t >= 0x8515 && t <= 0x851A) ? 0x8513 : t;   // cube faces: the cube map
    const wrap = (name, f) => { const o = P[name]; if (o) P[name] = function (...a) { try { f(this, a); } catch (e) {} return o.apply(this, a); }; };
    wrap('activeTexture', (gl, a) => { state(gl).unit = a[0]; });
    wrap('bindTexture', (gl, a) => { state(gl).tex[state(gl).unit + ':' + a[0]] = a[1]; });
    wrap('bindBuffer', (gl, a) => { state(gl).buf[a[0]] = a[1]; });
    wrap('bindRenderbuffer', (gl, a) => { state(gl).rb = a[1]; });
    const tex = (gl, target) => state(gl).tex[state(gl).unit + ':' + texTarget(target)];
    wrap('texStorage2D', (gl, a) => { let n = 0, wd = a[3], ht = a[4];
      for (let l = 0; l < a[1]; l++) { n += wd * ht * (B[a[2]] ?? 1); wd = Math.max(1, wd >> 1); ht = Math.max(1, ht >> 1); }
      const isCube = a[0] === 0x8513; if (B[a[2]] === undefined) { m.compressedTex++; n = n / 4; } else m.plainTex++;
      big('texStorage2D 0x' + a[2].toString(16) + ' ' + a[3] + 'x' + a[4] + (isCube ? ' cube' : ''), n * (isCube ? 6 : 1));
      add(tex(gl, a[0]), n * (isCube ? 6 : 1), 'tex', 'texPeak'); });
    wrap('texStorage3D', (gl, a) => { let n = 0, wd = a[3], ht = a[4];
      for (let l = 0; l < a[1]; l++) { n += wd * ht * a[5] * (B[a[2]] ?? 1); wd = Math.max(1, wd >> 1); ht = Math.max(1, ht >> 1); }
      add(tex(gl, a[0]), n, 'tex', 'texPeak'); });
    // texImage2D re-specifies a level (a font atlas that grows is uploaded again and again): what that level held is replaced
    const levels = new WeakMap();
    const level = (o, key, n) => { if (!o) return; let l = levels.get(o); if (!l) levels.set(o, l = {}); const old = l[key] || 0; l[key] = n;
      size.set(o, (size.get(o) || 0) - old); m.tex -= old; add(o, n, 'tex', 'texPeak'); return old === 0; };
    wrap('texImage2D', (gl, a) => { if (a.length < 8) return; const n = a[3] * a[4] * (B[a[2]] ?? 4);   // (target, level, ifmt, w, h, ...)
      if (level(tex(gl, a[0]), a[0] + ':' + a[1], n) && a[1] === 0) { m.plainTex++; big('texImage2D 0x' + a[2].toString(16) + ' ' + a[3] + 'x' + a[4], n); } });
    wrap('compressedTexImage2D', (gl, a) => { const d = a[6]; const n = typeof d === 'number' ? d : a.length >= 9 ? a[8] : (d && d.byteLength) || 0;
      if (level(tex(gl, a[0]), a[0] + ':' + a[1], n) && a[1] === 0) { m.compressedTex++; big('compressed 0x' + a[2].toString(16) + ' ' + a[3] + 'x' + a[4], n); } });
    wrap('deleteTexture', (gl, a) => { const n = size.get(a[0]) || 0; m.tex -= n; size.delete(a[0]); });
    wrap('bufferData', (gl, a) => { const b = state(gl).buf[a[0]]; if (!b) return; const old = size.get(b) || 0; m.buf -= old; size.set(b, 0);
      add(b, typeof a[1] === 'number' ? a[1] : a.length >= 5 ? a[4] : (a[1] && a[1].byteLength) || 0, 'buf', 'bufPeak'); });
    wrap('deleteBuffer', (gl, a) => { const n = size.get(a[0]) || 0; m.buf -= n; size.delete(a[0]); });
    wrap('renderbufferStorage', (gl, a) => { const r = state(gl).rb; if (!r) return; m.rb -= size.get(r) || 0; size.set(r, 0); add(r, a[2] * a[3] * (B[a[1]] ?? 4), 'rb', 'rbPeak'); });
    wrap('renderbufferStorageMultisample', (gl, a) => { const r = state(gl).rb; if (!r) return; m.rb -= size.get(r) || 0; size.set(r, 0);
      add(r, a[3] * a[4] * (B[a[2]] ?? 4) * Math.max(1, a[1]), 'rb', 'rbPeak'); });
    wrap('deleteRenderbuffer', (gl, a) => { const n = size.get(a[0]) || 0; m.rb -= n; size.delete(a[0]); });
  }
})();`;

// This WebKit (WPE, Playwright's 2359 build, on this machine) has no GStreamer audio sink ("autoaudiosink not found") and no
// MP4 demuxer for the game's AAC sound: an AudioContext that starts playing, or an <audio> element that loads a streamed clip,
// takes the whole content process down (SIGABRT) a second after the title. So in WebKit the page gets a stand-in: an
// OfflineAudioContext (no output) that starts suspended and runs once resume() is called after a user gesture, whose
// decodeAudioData() gives a second of silence, and <audio> elements that never load. It shows that the game asks for sound
// on the first touch, not that Safari plays it; Chromium's runs have real audio.
const WEBKIT_AUDIO = `(() => {
  const Off = window.OfflineAudioContext; if (!Off) return;
  function StandIn(opts) {
    const ctx = new Off({ numberOfChannels: 2, length: 44100, sampleRate: (opts && opts.sampleRate) || 44100 });
    let state = 'suspended';
    const to = (s) => { if (state !== s) { state = s; ctx.dispatchEvent(new Event('statechange')); } return Promise.resolve(); };
    Object.defineProperty(ctx, 'state', { get: () => state });
    Object.defineProperty(ctx, 'baseLatency', { get: () => 0.01 });
    Object.defineProperty(ctx, 'outputLatency', { get: () => 0.02 });
    ctx.resume = () => navigator.userActivation && !navigator.userActivation.hasBeenActive ? Promise.resolve() : to('running');
    ctx.suspend = () => to('suspended');
    ctx.close = () => to('closed');
    ctx.createMediaElementSource = (el) => { const g = ctx.createGain(); g.mediaElement = el; return g; };
    ctx.decodeAudioData = function (data, ok) { const b = ctx.createBuffer(2, ctx.sampleRate, ctx.sampleRate); if (ok) setTimeout(() => ok(b), 0); return Promise.resolve(b); };
    return ctx;
  }
  window.AudioContext = window.webkitAudioContext = StandIn;
  class StandInAudio extends EventTarget {
    constructor() { super(); Object.assign(this, { src: '', preload: '', autoplay: false, loop: false, currentTime: 0, duration: 1, paused: true, volume: 1,
                                                    muted: false, playbackRate: 1, readyState: 4, style: {} }); }
    play() { this.paused = false; return Promise.resolve(); }
    pause() { this.paused = true; }
    load() {}
    removeAttribute(n) { if (n === 'src') this.src = ''; }
    setAttribute(n, v) { this[n] = v; }
    canPlayType() { return 'maybe'; }
  }
  window.Audio = StandInAudio;
  const ce = Document.prototype.createElement;
  Document.prototype.createElement = function (n, ...a) { return /^audio$/i.test(n) ? new StandInAudio() : ce.call(this, n, ...a); };
})();`;

// --trace-touch: every touch and pointer event the page gets, in the console
const TRACE_TOUCH = `(() => {
  for (const t of ['touchstart', 'touchend', 'touchcancel', 'pointerdown', 'pointerup', 'click', 'mousedown'])
    addEventListener(t, (e) => console.log('[trace] ' + t + ' on ' + (e.target.id || e.target.tagName) + (e.pointerType ? ' ' + e.pointerType : '') +
      (e.targetTouches ? ' targetTouches ' + e.targetTouches.length : '') + (e.isTrusted ? '' : ' (untrusted)')), true);
})();`;

const NO_S3TC = `(() => {
  const hide = /s3tc|bptc|rgtc/i;
  for (const Ctx of [window.WebGL2RenderingContext, window.WebGLRenderingContext]) {
    if (!Ctx) continue;
    const P = Ctx.prototype, ge = P.getExtension, gs = P.getSupportedExtensions;
    P.getExtension = function (n) { return hide.test(n) ? null : ge.call(this, n); };
    P.getSupportedExtensions = function () { return (gs.call(this) || []).filter((n) => !hide.test(n)); };
  }
})();`;

// ---------------------------------------------------------------- touch: real touch events, from the browser's own protocol

let page, raw;
async function rawSession() {
  if (isWebKit) {
    // WebKit's Playwright protocol has multi-touch (Input.dispatchTouchEvent) that the public API only uses for a tap
    const impl = page._connection.toImpl(page);
    const d = impl.delegate ?? impl._delegate;
    const s = d.rawTouchscreen?._pageProxySession ?? d._pageProxySession;
    return { send: (m, p) => s.send(m, p) };
  }
  const cdp = await page.context().newCDPSession(page);
  return { send: (m, p) => cdp.send(m, p) };
}
let fingers = [];   // where the fingers are down now
/** Chromium's events carry the time they're given (a phone's own clock; a loaded machine would deliver them late). */
async function touch(type, points, at = null) {
  // WebKit's protocol lists the fingers that lift in a touchEnd; Chromium's lists none
  if ((type === "touchEnd" || type === "touchCancel") && isWebKit && points.length === 0) points = fingers;
  const touchPoints = points.map((p, i) => ({ x: Math.round(p[0]), y: Math.round(p[1]), id: i, ...(isWebKit ? {} : { radiusX: 6, radiusY: 6, force: 1 }) }));
  const timestamp = isWebKit ? {} : { timestamp: (at ?? Date.now()) / 1000 };
  await raw.send("Input.dispatchTouchEvent", { type, touchPoints, ...timestamp });
  fingers = type === "touchEnd" || type === "touchCancel" ? [] : points;
}
/** A tap as the browser makes one (touch events, then the click a page sees for a tap): for the page's buttons. */
async function tap(x, y) {
  await page.touchscreen.tap(Math.round(x), Math.round(y));
}
/** A finger down and up on the game: the touch events alone, as the canvas reads them. */
async function fingerTap(x, y, hold = 70) {
  // WebKit's protocol has no times for touches, and at this machine's few frames a second its events arrive far apart:
  // its own tap sends the touchstart and touchend together, as a quick finger does
  if (isWebKit) { await page.touchscreen.tap(Math.round(x), Math.round(y)); return; }
  const t = Date.now();
  await touch("touchStart", [[x, y]], t);
  await sleep(hold);
  await touch("touchEnd", [], t + hold);
}
async function drag(from, to, ms = 600, steps = 15) {
  await touch("touchStart", [from]);
  for (let i = 1; i <= steps; i++) {
    await sleep(ms / steps);
    await touch("touchMove", [[from[0] + (to[0] - from[0]) * i / steps, from[1] + (to[1] - from[1]) * i / steps]]);
  }
  await sleep(60);
  await touch("touchEnd", []);
}
async function pinch(center, d0, d1, ms = 700, steps = 14) {
  const pts = (d) => [[center[0] - d / 2, center[1]], [center[0] + d / 2, center[1]]];
  await touch("touchStart", pts(d0));
  for (let i = 1; i <= steps; i++) { await sleep(ms / steps); await touch("touchMove", pts(d0 + (d1 - d0) * i / steps)); }
  await sleep(60);
  await touch("touchEnd", []);
}

// ---------------------------------------------------------------- the run

const report = { device: opt.device, browser: isWebKit ? "webkit" : "chromium", steps: [], errors: [], consoleErrors: 0 };
const lines = [];
let shots = 0, ok = true;
function step(name, pass, detail = "") {
  report.steps.push({ name, ok: pass, detail });
  say(`${pass === null ? "note" : pass ? "ok  " : "FAIL"} ${name}${detail ? ": " + detail : ""}`);
  if (pass === false) ok = false;
  return pass;
}
async function shot(name) {
  shots++;
  const f = path.join(OUT, "shots", `${String(shots).padStart(2, "0")}_${name}.png`);
  await page.screenshot({ path: f, timeout: 30000 }).catch((e) => note(`screenshot ${name}: ${e.message}`));
}
async function waitFor(re, timeoutS, from = 0) {
  const end = Date.now() + timeoutS * 1000;
  for (;;) {
    for (let i = from; i < lines.length; i++) { const mm = lines[i].match(re); if (mm) return mm; }
    if (Date.now() > end) return null;
    if (page.isClosed()) return null;
    await sleep(200);
  }
}
const MB = (b) => +(b / 1048576).toFixed(1);
let lastCounters = null;
async function counters() {
  if (report.crashed) return null;
  const c = await page.evaluate(() => { const m = window.__mob; return m && { heap: m.heap, heapPeak: m.heapPeak, tex: m.tex, texPeak: m.texPeak,
    compressedTex: m.compressedTex, plainTex: m.plainTex, buf: m.buf, bufPeak: m.bufPeak, rb: m.rb, rbPeak: m.rbPeak,
    audioDecoded: m.audioDecoded, audioClips: m.audioClips,
    jsHeap: performance.memory ? performance.memory.usedJSHeapSize : null }; }).catch(() => null);
  if (c) lastCounters = c;
  return c;
}
async function fps(ms = 4000) {
  return page.evaluate((ms) => new Promise((res) => { let n = 0; const t = performance.now();
    const f = () => { n++; if (performance.now() - t < ms) requestAnimationFrame(f); else res(Math.round(n / (performance.now() - t) * 10000) / 10); };
    requestAnimationFrame(f); }), ms).catch(() => null);
}
/** A point on the game's 1920x1080 frame (the UI's reference), in the page's CSS pixels. */
async function at(x, y) {
  const r = await page.evaluate(() => { const b = document.getElementById("unity-canvas").getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; });
  return [r[0] + x / 1920 * r[2], r[1] + y / 1080 * r[3]];
}
/** The centre of a page element (an on-screen control), or null when it isn't shown. */
async function el(sel) {
  return page.evaluate((sel) => { const e = document.querySelector(sel); if (!e) return null; const r = e.getBoundingClientRect();
    const cs = getComputedStyle(e); if (r.width === 0 || cs.visibility === "hidden" || cs.display === "none" || +cs.opacity === 0) return null;
    return [r.x + r.width / 2, r.y + r.height / 2, r.width, r.height]; }, sel);
}
async function tapEl(sel, name) {
  const p = await el(sel);
  if (!p) { step(`tap ${name}`, false, `${sel} isn't shown`); return false; }
  await tap(p[0], p[1]);
  return true;
}

async function main() {
  let server, url = opt.url;
  if (opt.serve) {
    server = await serve(opt.serve, opt.port);
    url = `http://127.0.0.1:${server.address().port}/LostAndFound/`;
    say(`serving ${path.resolve(opt.serve)} at ${url}`);
  }
  if (opt.query) url += (url.includes("?") ? "&" : "?") + opt.query;
  report.url = url;
  const exe = isWebKit ? (process.env.WEBKIT_EXE ?? path.join(os.homedir(), ".cache/webkit-libs/webkit-2359/pw_run.sh")) : undefined;
  const browser = isWebKit
    ? await webkit.launch({ headless: true, executablePath: exe })
    : await chromium.launch({ headless: true, executablePath: findChromium(), args: ["--use-gl=angle", "--use-angle=gl-egl", "--ignore-gpu-blocklist", "--enable-unsafe-swiftshader",
        "--autoplay-policy=user-gesture-required", "--mute-audio"] });
  const sampler = setInterval(sampleMemory, 250);
  // a line a second in the log: the content process's anonymous memory and the WebAssembly heap
  let tracing = false;
  const trace = setInterval(async () => {
    const m = mem.last ?? { web: 0, gpu: 0 };
    note(`[mem] content ${MB(m.web)} MB${m.gpu ? `, gpu ${MB(m.gpu)} MB` : ""}`);
    if (tracing || !page || page.isClosed() || report.crashed) return;
    tracing = true;
    const c = await Promise.race([counters(), sleep(3000).then(() => "busy")]);
    tracing = false;
    note(c === "busy" ? "[page] busy: no answer in 3 s" : c ? `[page] heap ${MB(c.heap)} MB, textures ${MB(c.tex)} MB (${c.compressedTex} compressed, ` +
         `${c.plainTex} plain), buffers ${MB(c.buf)} MB, targets ${MB(c.rb)} MB; ${await page.evaluate(() => document.getElementById("status")?.textContent).catch(() => "")}` : "[page] -");
  }, 1000);
  try {
    const { defaultBrowserType, ...ctxOpts } = profile;
    const context = await browser.newContext(ctxOpts);
    page = await context.newPage();
    raw = desktop ? null : await rawSession();
    if (isWebKit) await page.addInitScript(WEBKIT_AUDIO);
    await page.addInitScript(HOOKS);
    if (!desktop && !opt.desktopFormats) await page.addInitScript(NO_S3TC);
    if (opt.traceTouch) await page.addInitScript(TRACE_TOUCH);
    page.on("console", (m) => {
      const t = m.text();
      lines.push(t);
      note(`[${m.type()}] ${t}`);
      if (m.type() === "error") { report.consoleErrors++; report.errors.push(t.slice(0, 400)); }
    });
    page.on("pageerror", (e) => { report.errors.push("exception: " + e.message); note(`[exception] ${e.message}`); });
    page.on("crash", () => { report.crashed = true; say("the page crashed"); });
    const start = Date.now();
    await page.goto(url, { waitUntil: "domcontentloaded" });
    const env = await page.evaluate(() => ({
      ua: navigator.userAgent, coarse: matchMedia("(pointer: coarse)").matches, anyFine: matchMedia("(any-pointer: fine)").matches,
      hover: matchMedia("(hover: hover)").matches, touchPoints: navigator.maxTouchPoints, dpr: devicePixelRatio, vw: innerWidth, vh: innerHeight,
      webgl2: (() => { const g = document.createElement("canvas").getContext("webgl2"); if (!g) return null;
        const d = g.getExtension("WEBGL_debug_renderer_info"); return { renderer: d ? g.getParameter(d.UNMASKED_RENDERER_WEBGL) : g.getParameter(g.RENDERER),
          s3tc: !!g.getExtension("WEBGL_compressed_texture_s3tc"), astc: !!g.getExtension("WEBGL_compressed_texture_astc"),
          etc: !!g.getExtension("WEBGL_compressed_texture_etc"), etc1: !!g.getExtension("WEBGL_compressed_texture_etc1"), bptc: !!g.getExtension("EXT_texture_compression_bptc") }; })(),
      webgpu: !!navigator.gpu }));
    report.env = env;
    say(`${opt.device} in ${isWebKit ? "WebKit" : "Chromium"}: ${env.vw}x${env.vh} CSS px at ${env.dpr}x, pointer ${env.coarse ? "coarse" : "fine"}` +
        `${env.anyFine ? " (a fine one too)" : ""}, ${env.touchPoints} touch points, WebGL 2 ${env.webgl2 ? `(${env.webgl2.renderer}; s3tc ${env.webgl2.s3tc}, astc ${env.webgl2.astc}, etc ${env.webgl2.etc}, bptc ${env.webgl2.bptc})` : "no"}, WebGPU ${env.webgpu}`);

    await sleep(1500);
    await shot("page");
    // a phone held upright: the page asks for it on its side; then it's turned (the viewport's sides swapped)
    if (await el("#rotate")) {
      await shot("rotate");
      step("held upright, the phone is asked to turn on its side", true, "screenshot rotate");
      const v = page.viewportSize();
      await page.setViewportSize({ width: v.height, height: v.width });
      await sleep(1000);
      step("turned, the prompt goes", !(await el("#rotate")), `${v.height}x${v.width}`);
      await shot("turned");
    }
    // a phone or tablet: a word first, and a tap (which the sound needs anyway)
    const anyway = await el("#anyway");
    if (anyway) {
      step("the page asks before loading on a phone or tablet", null, "tapping \"Load it anyway\"");
      await tap(anyway[0], anyway[1]);
    }
    const title = await waitFor(/\[Title\] show/, opt.timeout);
    report.titleSeconds = +((Date.now() - start) / 1000).toFixed(1);
    sampleMemory();
    const c1 = await counters();
    step("the game reaches its title", !!title && !report.crashed, title ? `${report.titleSeconds} s, load average ${os.loadavg()[0].toFixed(0)}`
      : `not within ${opt.timeout} s; the page says "${await page.evaluate(() => document.getElementById("status")?.textContent ?? "").catch(() => "?")}"`);
    if (title) {
      await sleep(4000);
      await shot("title");
      report.titleFps = await fps();
      const c = await counters();
      if (c) report.atTitle = { heapMB: MB(c.heap), texturesMB: MB(c.tex), buffersMB: MB(c.buf), renderTargetsMB: MB(c.rb), compressedTextures: c.compressedTex, plainTextures: c.plainTex,
                         decodedAudioMB: MB(c.audioDecoded), decodedClips: c.audioClips, jsHeapMB: c.jsHeap != null ? MB(c.jsHeap) : null, contentAnonMB: MB(sampleMemory().web) };
      step("at the title", null, `${report.titleFps} fps; ${JSON.stringify(report.atTitle)}`);
      report.controls = await page.evaluate(() => { const t = document.getElementById("touch"); return t ? { shown: !t.hidden && getComputedStyle(t).display !== "none",
        classes: document.documentElement.className } : null; });
      if (desktop) step("the touch controls stay hidden on a desktop", !report.controls || !report.controls.shown, JSON.stringify(report.controls));
    }
    if (title && opt.play) await play();
    await sleep(500);
    sampleMemory();
    const c = report.crashed ? lastCounters : await counters();
    const bigs = await page.evaluate(() => window.__mob && window.__mob.big).catch(() => null);
    if (bigs) {
      report.bigAllocations = Object.entries(bigs).sort((a, b) => b[1][1] - a[1][1]).slice(0, 40).map(([k, v]) => `${MB(v[1])} MB  ${v[0]}x ${k}`);
      note("[textures] the biggest allocations:\n  " + report.bigAllocations.join("\n  "));
    }
    if (c) report.peak = { heapMB: MB(c.heapPeak), texturesMB: MB(c.texPeak), buffersMB: MB(c.bufPeak), renderTargetsMB: MB(c.rbPeak),
                           compressedTextures: c.compressedTex, plainTextures: c.plainTex };
    report.peakContentAnonMB = MB(mem.web);
    report.peakGpuProcessAnonMB = MB(mem.gpu);
    report.downloadMB = +(served.bytes / 1e6).toFixed(1);
    step("peak memory", null, `content process ${report.peakContentAnonMB} MB anonymous (at ${mem.at.web} s)` +
         (mem.gpu ? `, GPU process ${report.peakGpuProcessAnonMB} MB` : "") + `; ${JSON.stringify(report.peak)}; download ${report.downloadMB} MB`);
    await context.close().catch(() => {});
  } catch (e) {
    step("the check ran", false, e.stack ?? e.message);
  } finally {
    clearInterval(sampler);
    clearInterval(trace);
    await browser.close().catch(() => {});
    if (server) { server.close(); server.closeAllConnections?.(); }
  }
  report.ok = ok && !report.crashed;
  report.seconds = +secs();
  fs.writeFileSync(path.join(OUT, "report.json"), JSON.stringify(report, null, 2));
  say(`${report.errors.length} error(s); log, report.json and screenshots in ${OUT}`);
  say(report.ok ? "PASS" : "FAIL");
  fs.closeSync(logFile);
  process.exit(report.ok ? 0 : 1);
}

// ---------------------------------------------------------------- --play: the main verbs by touch

// what the game tells the page (TouchInput.State, index.html's lafTouchFlags)
const F = { playing: 1, modal: 2, holding: 4, left: 8, right: 16, lamp: 32, claim: 64, back: 128, pad: 256, stamp: 512,
            paused: 1024, rules: 2048, slip: 4096, cabinet: 8192, shelf: 16384 };
const flags = () => page.evaluate(() => window.lafTouchFlags ?? -1).catch(() => -1);
async function until(test, seconds, every = 250) {
  const end = Date.now() + seconds * 1000;
  for (;;) { const f = await flags(); if (f >= 0 && test(f)) return f; if (Date.now() > end) return null; await sleep(every); }
}
const named = (f) => Object.entries(F).filter(([, b]) => f & b).map(([k]) => k).join(" ") || "-";

// places on the game's 1920x1080 frame (its UI's reference size), from screenshots of the desk on the first morning
const AT = {
  begin: [320, 415], toTitle: [960, 770], bell: [1250, 758], slipClose: [1000, 560],   // the slip as the view frames it with a stamp in hand
  greenStamp: [647, 790], middle: [960, 520],
  drawerA: [743, 455], wallet: [972, 486],
};

async function play() {
  const has = (re, from) => lines.slice(from).some((l) => re.test(l));
  let from = lines.length;
  step("the on-screen controls are shown", !!(await el("#touch")), JSON.stringify(report.controls));
  const visible = async () => page.evaluate(() => Array.from(document.querySelectorAll("#touch .tb")).filter((b) => !b.classList.contains("off")).map((b) => b.id).join(" "));
  const sizes = await page.evaluate(() => Array.from(document.querySelectorAll("#touch .tb")).map((b) => { const r = b.getBoundingClientRect(); return [b.id, Math.round(r.width), Math.round(r.height)]; }));
  note(`button sizes (CSS px, shown or not): ${JSON.stringify(sizes)}`);

  // the title's menu: Begin, by a tap on the game's own button
  let p = await at(...AT.begin);
  await fingerTap(p[0], p[1]);
  const dismissed = await waitFor(/\[Title\] dismissed/, 20, from);
  step("a tap on Begin starts the game", !!dismissed);
  if (!dismissed) return;
  await sleep(3000);
  await shot("begun");

  // the porter's word, then the bell for the first claimant: taps on the bell (a tap anywhere moves a conversation on)
  let f = -1;
  for (let i = 0; i < 40 && !((f = await flags()) & F.claim); i++) {
    p = await at(...AT.bell);
    await fingerTap(p[0], p[1]);
    await sleep(1500);
  }
  step("taps move the morning on and ring the bell: a claim at the window", !!(f & F.claim), `flags ${named(f)}; buttons: ${await visible()}`);
  await sleep(4000);
  await shot("claim");
  report.deskFps = await fps();
  step("frame rate on the desk", null, `${report.deskFps} fps, load average ${os.loadavg()[0].toFixed(0)}`);
  for (let i = 0; i < 6 && (await flags()) & F.modal; i++) { p = await at(...AT.middle); await fingerTap(p[0], p[1]); await sleep(1200); }

  // the slip, Agnes's rules and a nudge, by their buttons; Back closes each
  if (await tapEl("#t-slip", "Slip")) {
    f = await until((x) => x & F.slip, 4);
    await sleep(800); await shot("slip");
    step("Slip brings up the claim slip", !!f, f ? named(f) : "no slip flag");
    await tapEl("#t-back", "Back (leave the slip)");
    f = await until((x) => !(x & F.slip), 4);
    step("Back leaves the slip", f !== null);
  }
  if (await tapEl("#t-rules", "Rules")) {
    f = await until((x) => x & F.rules, 4);
    await sleep(800); await shot("rules");
    step("Rules opens Agnes's rules", !!f);
    await tapEl("#t-back", "Back (close the rules)");
    f = await until((x) => !(x & F.rules), 4);
    step("Back closes the rules", f !== null);
  }
  from = lines.length;
  if (await tapEl("#t-nudge", "Nudge")) {
    await sleep(1500); await shot("nudge");
    step("Nudge asks Agnes for a nudge", has(/\[Nudge\] case/, from), lines.slice(from).find((l) => /\[Nudge\]/.test(l))?.slice(0, 160) ?? "no [Nudge] line");
  }

  // touch and hold: what's there, without a click (the rules card's hint)
  p = await at(1303, 913);
  await touch("touchStart", [p]); await sleep(900);
  await shot("hold_rules_card");
  const hint = await flags();
  await touch("touchEnd", []); await sleep(600);
  step("touching and holding doesn't click", !((await flags()) & F.rules), `flags while held ${named(hint)}`);

  // turn to the drawers and open drawer A, pick up the wallet
  if (await tapEl("#t-left", "turn left")) {
    f = await until((x) => x & F.cabinet, 5);
    await sleep(1500); await shot("drawers");
    step("the left arrow turns to the drawers", !!f, f ? named(f) : "");
  }
  if (AT.drawerA) {
    p = await at(...AT.drawerA); await fingerTap(p[0], p[1]); await sleep(1800); await shot("drawer_open");
    if (AT.wallet) { p = await at(...AT.wallet); await fingerTap(p[0], p[1]); }
    f = await until((x) => x & F.holding, 5);
    step("taps open drawer A and pick up the wallet", !!f, f ? named(f) : "not holding");
  }
  if (!((await flags()) & F.holding)) {
    // without the drawer's place, back to the desk
    if ((await flags()) & F.cabinet) { await tapEl("#t-right", "turn right"); await until((x) => !(x & F.cabinet), 5); }
  }
  if ((await flags()) & F.holding) {
    await sleep(1200); await shot("holding");
    step("holding: Tray, Back and (with the lamp) Lamp are shown", (await visible()).includes("t-tray"), await visible());
    const c = await at(960, 540);
    await drag([c[0] - 70, c[1]], [c[0] + 90, c[1] + 25]);
    await sleep(700); await shot("dragged");
    await pinch(c, 60, 220);
    await sleep(900); await shot("pinched_in");
    await pinch(c, 220, 60);
    await sleep(900);
    await tapEl("#t-tray", "Tray");
    f = await until((x) => !(x & F.holding), 5);
    await sleep(1500); await shot("on_tray");
    step("Tray puts it on the counter tray", f !== null);
    if ((await flags()) & F.cabinet) { await tapEl("#t-right", "turn right"); await until((x) => !(x & F.cabinet), 5); await sleep(1200); }
    // the green RETURN stamp: a tap picks it up, a tap on the slip shows what it would do, a second tap there stamps
    p = await at(...AT.greenStamp); await fingerTap(p[0], p[1]);
    f = await until((x) => x & F.stamp, 4);
    step("a tap picks up the green stamp", !!f);
    if (f) {
      from = lines.length;
      p = await at(...AT.slipClose); await fingerTap(p[0], p[1]); await sleep(1200); await shot("stamp_aimed");
      const aimed = await flags();
      step("the first tap on the slip only aims (the hint says what it would do)", !!(aimed & F.stamp), named(aimed));
      await fingerTap(p[0], p[1]); await sleep(2500); await shot("stamped");
      f = await until((x) => !(x & F.stamp), 6);
      const said = lines.slice(from).filter((l) => /\[(Stamp|Verdict|Case|Day|Resolve)\]/.test(l));
      step("the second tap on the same spot stamps", f !== null && said.some((l) => /\[Stamp\]/.test(l)), said.slice(0, 3).join(" | ") || "no [Stamp] line");
    }
  }
  // the pause menu, and back
  await sleep(2000);
  for (let i = 0; i < 6 && (await flags()) & F.modal && !((await flags()) & F.paused); i++) { p = await at(...AT.middle); await fingerTap(p[0], p[1]); await sleep(1200); }
  if (await tapEl("#t-menu", "Menu")) {
    f = await until((x) => x & F.paused, 4);
    await sleep(900); await shot("pause_menu");
    step("Menu pauses", !!f);
    await tapEl("#t-menu", "Menu (carry on)");
    f = await until((x) => !(x & F.paused), 4);
    step("Menu again carries on", f !== null);
  }
  // back to the title (the game is rebuilt in place) and into the week again: touch carries on
  if (await tapEl("#t-menu", "Menu")) {
    await until((x) => x & F.paused, 4);
    await sleep(800);
    from = lines.length;
    p = await at(...AT.toTitle); await fingerTap(p[0], p[1]);
    const back = await waitFor(/\[Title\] show/, 30, from);
    await sleep(2500);
    p = await at(...AT.begin); await fingerTap(p[0], p[1]);
    const again = await waitFor(/\[Title\] dismissed/, 20, from);
    step("back to the title and in again, by touch", !!back && !!again, back ? (again ? "title, then Begin" : "the title, but Begin didn't take") : "no title");
    await sleep(2500); await shot("in_again");
  }
  // a key hands the game to the keyboard (the controls go), and a touch brings them back
  from = lines.length;
  await page.keyboard.press("Shift");
  await sleep(1200);
  const afterKey = await page.evaluate(() => ({ touch: document.documentElement.classList.contains("touch"), hidden: document.getElementById("touch").hidden }));
  step("a key press hides the controls", !afterKey.touch && afterKey.hidden && has(/\[Touch\] mouse and keyboard in control/, from), JSON.stringify(afterKey));
  p = await at(1700, 120);   // the window's top right: nothing there to click
  await fingerTap(p[0], p[1]);
  await sleep(1200);
  const afterTouch = await page.evaluate(() => ({ touch: document.documentElement.classList.contains("touch"), hidden: document.getElementById("touch").hidden }));
  step("a touch brings them back", afterTouch.touch && !afterTouch.hidden && has(/\[Touch\] touch in control/, from), JSON.stringify(afterTouch));
  await shot("controls_back");
  const a = await page.evaluate(() => window.__mob.audio.map((x) => x.state));
  step("the sound runs after the first touch", a.length > 0 && a.some((s) => s === "running"), `AudioContext ${a.join(", ")}`);
  await shot("end");
}

main();
