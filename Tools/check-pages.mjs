#!/usr/bin/env node
// Checks the browser build as it's served: headless Chromium (DevTools protocol) or Firefox (WebDriver BiDi), no npm
// packages, a fresh browser profile each run.
//
//   node Tools/check-pages.mjs https://nearbycoder.github.io/LostAndFound/          the live site, in Chromium
//   node Tools/check-pages.mjs --browser firefox <url>
//   node Tools/check-pages.mjs --serve Builds/Pages [--port 8791] [--full]           serve a folder as Pages does, at
//                                                                                   http://127.0.0.1:<port>/LostAndFound/
//
// By default it passes (exit 0) only when the page loads, the game reaches its title ("[Title] show" in the console)
// and nothing logs an error, fails to download or throws, within --timeout seconds. --full goes on to play:
//   keys:      arrows and Enter through the title to Settings: Fullscreen (the browser's), Graphics fidelity one step up;
//   audio:     before any input the page's AudioContext waits; after the first key it runs and sounds start;
//   fullscreen: the page's own button, by a real click;
//   reload:    the fidelity chosen is still there (the game's startup "[Quality]" line, and settings.json in IndexedDB);
//   play:      Begin, Monday's morning, turning, the bell, a click, the pause menu and back;
//   autopilot: the game plays Monday's first two claims itself (?lafAutopilot&lafQuitAfter=1.2), then a reload with
//              ?lafContinue picks the week up after them from the browser's save.
// Writes console.log, report.json and screenshots to --out (default Logs/check-pages/<browser>). Browsers: $CHROME or the
// newest Playwright Chromium in ~/.cache/ms-playwright, else google-chrome-stable/chromium; $FIREFOX or firefox.
import { spawn } from "node:child_process";
import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = { browser: "chromium", timeout: 180, full: false, port: 0, serve: null, out: null, url: null, size: "1600x900", keep: false };
for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (a === "--browser") opt.browser = args[++i];
  else if (a === "--timeout") opt.timeout = Number(args[++i]);
  else if (a === "--full") opt.full = true;
  else if (a === "--serve") opt.serve = args[++i];
  else if (a === "--port") opt.port = Number(args[++i]);
  else if (a === "--out") opt.out = args[++i];
  else if (a === "--size") opt.size = args[++i];
  else if (a === "--keep-profile") opt.keep = true;
  else if (a === "-h" || a === "--help") { console.log(fs.readFileSync(fileURLToPath(import.meta.url), "utf8").split("\nimport")[0]); process.exit(0); }
  else if (!a.startsWith("--") && !opt.url) opt.url = a;
  else { console.error(`unknown option ${a}`); process.exit(2); }
}
if (!["chromium", "firefox"].includes(opt.browser)) { console.error("--browser is chromium or firefox"); process.exit(2); }
if (!opt.url && !opt.serve) { console.error("give a URL, or --serve <folder>"); process.exit(2); }
const [W, H] = opt.size.split("x").map(Number);
const OUT = path.resolve(opt.out ?? path.join(ROOT, "Logs", "check-pages", opt.browser));
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

// ---------------------------------------------------------------- a static server like GitHub Pages: no Content-Encoding

const TYPES = { ".html": "text/html; charset=utf-8", ".js": "application/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
  ".wasm": "application/wasm", ".png": "image/png", ".jpg": "image/jpeg", ".ico": "image/x-icon", ".ttf": "font/ttf",
  ".json": "application/json", ".txt": "text/plain; charset=utf-8", ".unityweb": "application/octet-stream", ".data": "application/octet-stream" };
let served = { bytes: 0, requests: [] };
function serve(dir, port) {
  const base = path.resolve(dir);
  const server = http.createServer((req, res) => {
    const u = new URL(req.url, "http://x");
    let p = decodeURIComponent(u.pathname);
    if (p === "/LostAndFound") { res.writeHead(301, { Location: "/LostAndFound/" + u.search }); res.end(); return; }
    if (!p.startsWith("/LostAndFound/")) { res.writeHead(404); res.end("not found"); return; }   // case-sensitive, as Pages is
    p = p.slice("/LostAndFound/".length);
    if (p === "" || p.endsWith("/")) p += "index.html";
    const file = path.join(base, p);
    if (!file.startsWith(base + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
      served.requests.push(`404 ${u.pathname}`); res.writeHead(404); res.end("not found"); return;
    }
    const st = fs.statSync(file);
    const size = st.size;
    const etag = `"${st.mtimeMs.toString(16)}-${size.toString(16)}"`;
    const head = { "Content-Type": TYPES[path.extname(file)] ?? "application/octet-stream", "Cache-Control": "max-age=600",
                   "ETag": etag, "Last-Modified": st.mtime.toUTCString() };
    const since = Date.parse(req.headers["if-modified-since"] ?? "");
    if (req.headers["if-none-match"] === etag || (!req.headers["if-none-match"] && since >= Math.floor(st.mtimeMs / 1000) * 1000)) {
      served.requests.push(`304 ${u.pathname}`); res.writeHead(304, head); res.end(); return;
    }
    res.writeHead(200, { ...head, "Content-Length": size });
    served.bytes += size;
    served.requests.push(`200 ${size} ${u.pathname}`);
    fs.createReadStream(file).pipe(res);
  });
  return new Promise((ok) => server.listen(port, "127.0.0.1", () => ok(server)));
}

// ---------------------------------------------------------------- browsers

function findChromium() {
  if (process.env.CHROME) return process.env.CHROME;
  const pw = path.join(os.homedir(), ".cache", "ms-playwright");
  const dirs = fs.existsSync(pw) ? fs.readdirSync(pw).filter((d) => /^chromium-\d+$/.test(d)).sort((a, b) => b.split("-")[1] - a.split("-")[1]) : [];
  for (const d of dirs) { const c = path.join(pw, d, "chrome-linux64", "chrome"); if (fs.existsSync(c)) return c; }
  for (const c of ["google-chrome-stable", "chromium", "chromium-browser", "google-chrome"]) {
    for (const dir of (process.env.PATH ?? "").split(":")) if (fs.existsSync(path.join(dir, c))) return path.join(dir, c);
  }
  throw new Error("no Chromium found (set $CHROME)");
}

function launch(cmd, argv, pattern) {
  const proc = spawn(cmd, argv, { stdio: ["ignore", "pipe", "pipe"], env: { ...process.env } });
  let errText = "";
  const ws = new Promise((ok, fail) => {
    const onData = (d) => {
      errText += d.toString();
      if (errText.length > 200_000) errText = errText.slice(-100_000);
      const m = errText.match(pattern);
      if (m) ok(m[1]);
    };
    proc.stdout.on("data", onData);
    proc.stderr.on("data", onData);
    proc.on("exit", (c) => fail(new Error(`${path.basename(cmd)} exited (${c}) before it was ready:\n${errText.slice(-2000)}`)));
    setTimeout(() => fail(new Error(`${path.basename(cmd)} didn't start in 60 s:\n${errText.slice(-2000)}`)), 60_000);
  });
  return { proc, ws, stderr: () => errText };
}

class Socket {
  constructor(url) {
    this.ws = new WebSocket(url);
    this.id = 0;
    this.pending = new Map();
    this.handlers = [];
    this.open = new Promise((ok, fail) => { this.ws.onopen = ok; this.ws.onerror = (e) => fail(new Error("WebSocket: " + (e.message ?? "error"))); });
    this.ws.onmessage = (m) => {
      const msg = JSON.parse(m.data);
      if (msg.id !== undefined && this.pending.has(msg.id)) {
        const { ok, fail, method } = this.pending.get(msg.id);
        this.pending.delete(msg.id);
        if (msg.error) fail(new Error(`${method}: ${typeof msg.error === "string" ? msg.error + " " + (msg.message ?? "") : JSON.stringify(msg.error)}`));
        else ok(msg.result);
      } else for (const h of this.handlers) h(msg);
    };
  }
  send(method, params = {}, extra = {}) {
    const id = ++this.id;
    this.ws.send(JSON.stringify({ id, method, params, ...extra }));
    return new Promise((ok, fail) => {
      this.pending.set(id, { ok, fail, method });
      setTimeout(() => { if (this.pending.has(id)) { this.pending.delete(id); fail(new Error(`${method}: no answer in 60 s`)); } }, 60_000);
    });
  }
}

// What the page is asked to keep track of before anything else runs: the AudioContexts it makes and the sounds they start,
// and whether a key or click has happened yet.
const PRELOAD = `(() => {
  const w = window; if (w.__laf) return;
  w.__laf = { contexts: [], starts: 0, startsBeforeInput: 0, input: false };
  const mark = () => { w.__laf.input = true; };
  addEventListener('pointerdown', mark, true); addEventListener('keydown', mark, true);
  for (const name of ['AudioContext', 'webkitAudioContext']) {
    const C = w[name]; if (!C) continue;
    w[name] = class extends C { constructor(...a) { super(...a); w.__laf.contexts.push(this); } };
  }
  // every kind of source that has its own start (AudioBufferSourceNode overrides the base's)
  for (const name of ['AudioScheduledSourceNode', 'AudioBufferSourceNode', 'OscillatorNode', 'ConstantSourceNode']) {
    const P = w[name] && w[name].prototype;
    if (!P || !Object.prototype.hasOwnProperty.call(P, 'start')) continue;
    const start = P.start;
    P.start = function (...a) { w.__laf.starts++; if (!w.__laf.input) w.__laf.startsBeforeInput++; return start.apply(this, a); };
  }
})();`;

const KEYS = {   // name: [key, code, Windows key code, WebDriver key]
  ArrowDown: ["ArrowDown", "ArrowDown", 40, "\uE015"], ArrowUp: ["ArrowUp", "ArrowUp", 38, "\uE013"],
  ArrowLeft: ["ArrowLeft", "ArrowLeft", 37, "\uE012"], ArrowRight: ["ArrowRight", "ArrowRight", 39, "\uE014"],
  Enter: ["Enter", "Enter", 13, "\uE007"], Escape: ["Escape", "Escape", 27, "\uE00C"], Space: [" ", "Space", 32, " "],
  KeyA: ["a", "KeyA", 65, "a"], KeyD: ["d", "KeyD", 68, "d"],
};

class Chromium {
  async start(profile) {
    const exe = findChromium();
    this.name = `Chromium (${exe})`;
    this.b = launch(exe, [
      "--headless=new", `--user-data-dir=${profile}`, "--remote-debugging-port=0", "--no-first-run", "--no-default-browser-check",
      `--window-size=${W},${H}`, "--autoplay-policy=user-gesture-required", "--mute-audio",
      // the real GPU through ANGLE on EGL, not the software renderer headless Chrome otherwise falls back to
      "--use-gl=angle", "--use-angle=gl-egl", "--ignore-gpu-blocklist", "--enable-unsafe-swiftshader",
      "--disable-background-timer-throttling", "--disable-renderer-backgrounding", "--disable-backgrounding-occluded-windows",
      "about:blank"], /DevTools listening on (ws:\/\/\S+)/);
    this.s = new Socket(await this.b.ws);
    await this.s.open;
    const { targetId } = await this.s.send("Target.createTarget", { url: "about:blank" });
    const { sessionId } = await this.s.send("Target.attachToTarget", { targetId, flatten: true });
    this.sid = sessionId;
    this.reqs = new Map();
    this.s.handlers.push((m) => {
      if (m.sessionId !== this.sid) return;
      const p = m.params;
      if (m.method === "Runtime.consoleAPICalled") {
        const text = p.args.map((a) => a.value ?? a.description ?? a.unserializableValue ?? "").join(" ");
        this.onConsole(p.type === "warning" ? "warn" : p.type, text);
      } else if (m.method === "Runtime.exceptionThrown") {
        this.onError(`exception: ${p.exceptionDetails.exception?.description ?? p.exceptionDetails.text}`);
      } else if (m.method === "Log.entryAdded") {
        if (p.entry.level === "error") this.onError(`browser: ${p.entry.text} ${p.entry.url ?? ""}`);
        else note(`[browser ${p.entry.level}] ${p.entry.text}`);
      } else if (m.method === "Network.responseReceived") {
        this.reqs.set(p.requestId, p.response.url);
        if (p.response.status >= 400) this.onError(`HTTP ${p.response.status} ${p.response.url}`);
      } else if (m.method === "Network.loadingFinished") {
        this.onBytes(this.reqs.get(p.requestId), p.encodedDataLength);
      } else if (m.method === "Network.loadingFailed" && !p.canceled) {
        this.onError(`download failed: ${this.reqs.get(p.requestId) ?? p.requestId} ${p.errorText}`);
      }
    });
    for (const m of ["Runtime.enable", "Log.enable", "Network.enable", "Page.enable"]) await this.send(m);
    await this.send("Page.addScriptToEvaluateOnNewDocument", { source: PRELOAD });
    await this.send("Network.setCacheDisabled", { cacheDisabled: false });
  }
  send(method, params) { return this.s.send(method, params, { sessionId: this.sid }); }
  async canFullscreen() { return "yes"; }
  async goto(url) { await this.send("Page.navigate", { url }); }
  async reload() { await this.send("Page.reload", {}); }
  async eval(expr) {
    const r = await this.send("Runtime.evaluate", { expression: expr, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error("eval: " + (r.exceptionDetails.exception?.description ?? r.exceptionDetails.text));
    return r.result.value;
  }
  async key(name) {
    const [key, code, vk] = KEYS[name];
    const text = key.length === 1 ? key : name === "Enter" ? "\r" : undefined;
    await this.send("Input.dispatchKeyEvent", { type: text ? "keyDown" : "rawKeyDown", key, code, windowsVirtualKeyCode: vk, text });
    await sleep(60);
    await this.send("Input.dispatchKeyEvent", { type: "keyUp", key, code, windowsVirtualKeyCode: vk });
  }
  async click(x, y) {
    await this.send("Input.dispatchMouseEvent", { type: "mouseMoved", x, y });
    await sleep(50);
    await this.send("Input.dispatchMouseEvent", { type: "mousePressed", x, y, button: "left", clickCount: 1 });
    await sleep(60);
    await this.send("Input.dispatchMouseEvent", { type: "mouseReleased", x, y, button: "left", clickCount: 1 });
  }
  async shot(file) {
    const { data } = await this.send("Page.captureScreenshot", { format: "png" });
    fs.writeFileSync(file, Buffer.from(data, "base64"));
  }
  async close() {
    try { await this.s.send("Browser.close"); } catch {}
    await sleep(500);
    if (this.b.proc.exitCode === null) this.b.proc.kill("SIGKILL");
  }
}

class Firefox {
  async start(profile) {
    const exe = process.env.FIREFOX ?? "firefox";
    this.name = `Firefox (${exe})`;
    fs.writeFileSync(path.join(profile, "user.js"), [
      'user_pref("browser.shell.checkDefaultBrowser", false);',
      'user_pref("browser.startup.homepage_override.mstone", "ignore");',
      'user_pref("datareporting.policy.dataSubmissionEnabled", false);',
      'user_pref("toolkit.telemetry.reportingpolicy.firstRun", false);',
      'user_pref("browser.aboutwelcome.enabled", false);',
      'user_pref("media.autoplay.default", 1);',            // sound waits for a click or key, as for a visitor
      'user_pref("media.autoplay.block-webaudio", true);',
      'user_pref("media.volume_scale", "0.0");',            // silent on this machine's speakers
      ""].join("\n"));
    this.b = launch(exe, ["--headless", "--no-remote", "--profile", profile, "--remote-debugging-port", "0",
                          "--width", String(W), "--height", String(H)], /WebDriver BiDi listening on (ws:\/\/\S+)/);
    this.s = new Socket((await this.b.ws) + "/session");
    await this.s.open;
    await this.s.send("session.new", { capabilities: { alwaysMatch: { acceptInsecureCerts: true } } });
    await this.s.send("session.subscribe", { events: ["log.entryAdded", "network.responseCompleted", "network.fetchError"] });
    const { contexts } = await this.s.send("browsingContext.getTree", {});
    this.ctx = contexts[0].context;
    // --width/--height size the window; setting the viewport as well needs more access than this asks for
    await this.s.send("browsingContext.setViewport", { context: this.ctx, viewport: { width: W, height: H } }).catch(() => {});
    await this.s.send("script.addPreloadScript", { functionDeclaration: `() => { ${PRELOAD} }` });
    this.s.handlers.push((m) => {
      const p = m.params;
      if (m.method === "log.entryAdded") {
        if (p.type === "javascript") this.onError(`exception: ${p.text}`);
        else this.onConsole(p.level === "warn" ? "warn" : p.level, p.text ?? (p.args ?? []).map((a) => a.value ?? "").join(" "));
      } else if (m.method === "network.responseCompleted") {
        const r = p.response;
        if (r.status >= 400) this.onError(`HTTP ${r.status} ${r.url}`);
        this.onBytes(r.url, r.bytesReceived ?? r.content?.size ?? 0);
      } else if (m.method === "network.fetchError") {
        if (!/NS_BINDING_ABORTED/.test(p.errorText)) this.onError(`download failed: ${p.request.url} ${p.errorText}`);
      }
    });
  }
  async goto(url) { await this.s.send("browsingContext.navigate", { context: this.ctx, url, wait: "none" }); }
  async reload() { await this.s.send("browsingContext.reload", { context: this.ctx, wait: "none" }); }
  async eval(expr) {
    const r = await this.s.send("script.evaluate", { expression: `(async () => JSON.stringify(await (${expr})))()`, target: { context: this.ctx },
                                                     awaitPromise: true, resultOwnership: "none", userActivation: false });
    if (r.type === "exception") throw new Error("eval: " + r.exceptionDetails.text);
    return r.result.value === undefined ? undefined : JSON.parse(r.result.value);
  }
  /** Headless Firefox refuses fullscreen outright; ask once, with WebDriver's own user activation, to know. */
  async canFullscreen() {
    const r = await this.s.send("script.evaluate", { expression: `(() => { const d = document.body.appendChild(document.createElement('div'));
      return d.requestFullscreen().then(() => document.exitFullscreen().then(() => { d.remove(); return 'yes'; }), e => { d.remove(); return e.message; }); })()`,
      target: { context: this.ctx }, awaitPromise: true, resultOwnership: "none", userActivation: true });
    return r.result?.value ?? "?";
  }
  async key(name) {
    const v = KEYS[name][3];
    await this.s.send("input.performActions", { context: this.ctx, actions: [{ type: "key", id: "kb", actions: [
      { type: "keyDown", value: v }, { type: "pause", duration: 60 }, { type: "keyUp", value: v }] }] });
  }
  async click(x, y) {
    await this.s.send("input.performActions", { context: this.ctx, actions: [{ type: "pointer", id: "mouse", parameters: { pointerType: "mouse" },
      actions: [{ type: "pointerMove", x: Math.round(x), y: Math.round(y) }, { type: "pause", duration: 50 },
                { type: "pointerDown", button: 0 }, { type: "pause", duration: 60 }, { type: "pointerUp", button: 0 }] }] });
  }
  async shot(file) {
    const { data } = await this.s.send("browsingContext.captureScreenshot", { context: this.ctx });
    fs.writeFileSync(file, Buffer.from(data, "base64"));
  }
  async close() {
    try { await this.s.send("browser.close", {}); } catch {}
    for (let i = 0; i < 20 && this.b.proc.exitCode === null; i++) await sleep(250);
    if (this.b.proc.exitCode === null) this.b.proc.kill("SIGKILL");
  }
}

// ---------------------------------------------------------------- the checks

const report = { browser: opt.browser, url: null, steps: [], errors: [], warnings: 0, bytes: 0, files: {} };
const lines = [];   // the game's and the page's console lines, in order
let errorsSeen = 0;
let shotQueue = Promise.resolve();
let shots = 0;
let browser;

function step(name, ok, detail = "") {
  report.steps.push({ name, ok, detail });
  say(`${ok ? "ok  " : "FAIL"} ${name}${detail ? ": " + detail : ""}`);
  return ok;
}
/** A check this browser can't make headless: said, not counted. */
function skip(name, why) {
  report.steps.push({ name, ok: null, detail: why });
  say(`skip ${name}: ${why}`);
  return true;
}

let lastPageCheck = 0;
async function waitFor(re, timeoutS, from = 0) {
  const end = Date.now() + timeoutS * 1000;
  for (;;) {
    for (let i = from; i < lines.length; i++) { const m = lines[i].match(re); if (m) return m; }
    if (Date.now() > end) return null;
    // the page itself says it can't go on (no WebGL 2, files missing, the game failed to start): no point waiting
    if (Date.now() - lastPageCheck > 1000) {
      lastPageCheck = Date.now();
      if (await browser.eval(`!!document.querySelector('#loader.failed')`).catch(() => false)) return null;
    }
    await sleep(200);
  }
}

async function shot(name) {
  if (shots >= 60) return;
  shots++;
  shotQueue = shotQueue.then(() => browser.shot(path.join(OUT, "shots", `${String(shots).padStart(2, "0")}_${name}.png`)).catch((e) => note(`screenshot ${name}: ${e.message}`)));
  await shotQueue;
}

async function main() {
  let server;
  let url = opt.url;
  if (opt.serve) {
    server = await serve(opt.serve, opt.port);
    url = `http://127.0.0.1:${server.address().port}/LostAndFound/`;
    say(`serving ${path.resolve(opt.serve)} at ${url} (pid ${process.pid})`);
  }
  report.url = url;
  const profile = path.join(OUT, "profile");
  fs.mkdirSync(profile, { recursive: true });
  browser = opt.browser === "firefox" ? new Firefox() : new Chromium();
  browser.onConsole = (level, text) => {
    lines.push(text);
    note(`[${level}] ${text}`);
    if (level === "error" || level === "assert") { errorsSeen++; report.errors.push(text.slice(0, 500)); say(`error in the console: ${text.slice(0, 300)}`); }
    else if (level === "warn") report.warnings++;
    const s = text.match(/^\[Shot\] (\S+)/);
    if (s) shot(s[1]);
  };
  browser.onError = (text) => {
    if (/\/favicon\.ico\b/.test(text)) { note(`[ignored] ${text}`); return; }   // the browser's own guess, not the page's
    errorsSeen++; report.errors.push(text.slice(0, 500)); note(`[ERROR] ${text}`); say(`error: ${text.slice(0, 300)}`);
  };
  browser.onBytes = (u, n) => {
    if (!u || u.startsWith("data:")) return;
    report.bytes += n;
    const f = u.split("?")[0].split("/").pop() || "index.html";
    report.files[f] = (report.files[f] ?? 0) + n;
  };

  let ok = true;
  try {
    await browser.start(profile);
    say(`browser: ${browser.name}, ${W}x${H}, headless`);
    const loadStart = Date.now();
    await browser.goto(url);
    const title = await waitFor(/\[Title\] show/, opt.timeout);
    const loadS = (Date.now() - loadStart) / 1000;
    report.loadSeconds = loadS;
    const gl = await browser.eval(`(() => { const c = document.createElement('canvas').getContext('webgl2'); if (!c) return 'no WebGL 2';
      const d = c.getExtension('WEBGL_debug_renderer_info'); return d ? c.getParameter(d.UNMASKED_RENDERER_WEBGL) : c.getParameter(c.RENDERER); })()`).catch((e) => e.message);
    report.renderer = gl;
    note(`WebGL renderer: ${gl}`);
    const pageText = await browser.eval(`document.getElementById('status') ? document.getElementById('status').textContent : ''`).catch(() => "");
    ok = step("the game reaches its title", !!title, title ? `${loadS.toFixed(1)} s after the page was asked for, renderer ${gl}` : `not within ${opt.timeout} s; the page says "${pageText}"`) && ok;
    if (title) {
      await sleep(3000);   // the title fades in; anything it logs in that time counts
      await shot("title");
      report.titleFps = await browser.eval(`new Promise(res => { let n = 0; const t = performance.now();
        const f = () => { n++; if (performance.now() - t < 3000) requestAnimationFrame(f); else res(Math.round(n / (performance.now() - t) * 10000) / 10); };
        requestAnimationFrame(f); })`).catch(() => null);
      note(`page frame rate at the title: ${report.titleFps} fps (requestAnimationFrame), load average ${os.loadavg().map((x) => x.toFixed(0)).join(" ")}`);
    }
    ok = step("no errors on the way", errorsSeen === 0, errorsSeen ? report.errors.slice(0, 3).join(" | ") : `${report.warnings} warning(s)`) && ok;
    report.downloadMB = +(report.bytes / 1e6).toFixed(1);
    if (title && opt.full) ok = (await full(url)) && ok;
  } catch (e) {
    ok = step("the check ran", false, e.stack ?? e.message);
  } finally {
    await browser.close().catch(() => {});
    if (server) { server.close(); server.closeAllConnections?.(); }
    if (!opt.keep) fs.rmSync(profile, { recursive: true, force: true });
  }
  if (opt.serve) { report.served = { bytes: served.bytes, requests: served.requests }; }
  report.ok = ok && errorsSeen === 0;
  report.seconds = +secs();
  fs.writeFileSync(path.join(OUT, "report.json"), JSON.stringify(report, null, 2));
  if (report.titleFps != null) say(`frame rate at the title: ${report.titleFps} fps; load average ${os.loadavg().map((x) => x.toFixed(0)).join(" ")}`);
  say(`download ${(report.bytes / 1e6).toFixed(1)} MB as the browser counted it${opt.serve ? `, ${(served.bytes / 1e6).toFixed(1)} MB served` : ""}; ` +
      `${report.errors.length} error(s), ${report.warnings} warning(s); log and screenshots in ${OUT}`);
  say(report.ok ? "PASS" : "FAIL");
  fs.closeSync(logFile);
  process.exit(report.ok ? 0 : 1);
}

async function full(url) {
  let ok = true;
  const audio = () => browser.eval(`({ contexts: (window.__laf ? __laf.contexts : []).map(c => c.state + ' ' + c.currentTime.toFixed(2)), starts: window.__laf ? __laf.starts : -1 })`);
  const fullscreen = () => browser.eval(`document.fullscreenElement ? (document.fullscreenElement.id || document.fullscreenElement.tagName) : ''`);
  const press = async (k, n = 1, gap = 450) => { for (let i = 0; i < n; i++) { await browser.key(k); await sleep(gap); } };

  // audio before any input: the context waits (a browser won't let a page make sound until it's clicked or typed in)
  const a0 = await audio();
  const waiting = a0.contexts.length > 0 && a0.contexts.every((c) => !c.startsWith("running"));
  if (opt.browser === "chromium" && !waiting) skip("before any input the audio waits", `headless Chromium lets audio start without a gesture (AudioContext ${a0.contexts.join(", ")})`);
  else ok = step("before any input the audio waits", waiting, `AudioContext ${a0.contexts.join(", ") || "none"}`) && ok;
  const canFs = await browser.canFullscreen().catch((e) => e.message);
  const noFs = canFs === "yes" ? null : `this headless browser refuses fullscreen even to WebDriver ("${canFs}")`;

  // keys: the title's menu to Settings, the browser's fullscreen from the card, the fidelity one step up
  let from = lines.length;
  await press("ArrowDown");          // the first arrow focuses Begin
  await sleep(1500);
  const a1 = await audio();
  await sleep(1500);
  const a2 = await audio();
  const running = a2.contexts.some((c) => c.startsWith("running")) && a2.contexts.some((c, i) => parseFloat(c.split(" ")[1]) > parseFloat((a1.contexts[i] ?? "x 0").split(" ")[1]));
  ok = step("after the first key the audio runs and sounds start", running && a2.starts > 0,
            `AudioContext ${a2.contexts.join(", ")}; ${a2.starts} sound(s) started, ${a0.starts} of them before any input`) && ok;
  await press("ArrowDown", 3);       // Begin, Curiosities, Controls, Settings
  await press("Enter");
  await sleep(1200);
  await shot("settings");
  await press("ArrowDown", 12);      // Volume ... Film effects, to Fullscreen
  await press("Enter");
  await sleep(1500);
  const fs1 = await fullscreen();
  if (noFs) skip("Settings > Fullscreen makes the page fullscreen", noFs);
  else ok = step("Settings > Fullscreen makes the page fullscreen", !!fs1, fs1 ? `fullscreen element: ${fs1}` : "document.fullscreenElement stayed empty") && ok;
  if (fs1) await shot("fullscreen_from_settings");
  await browser.eval(`document.fullscreenElement ? document.exitFullscreen().then(() => 1) : 0`).catch(() => {});
  await sleep(800);
  await press("ArrowDown");          // Graphics fidelity
  from = lines.length;
  await press("ArrowRight");         // one step up from the web's Medium
  const q = await waitFor(/\[Quality\] (\w+):/, 8, from);
  ok = step("the fidelity slider changes the graphics step", !!q && q[1] === "High", q ? `now ${q[1]}` : "no [Quality] line") && ok;
  await sleep(1000);
  await shot("settings_high");
  await press("Escape");
  await sleep(1200);

  // the page's own fullscreen button, by a real click
  const r = await browser.eval(`(() => { const b = document.getElementById('fullscreen'); if (!b || b.hidden) return null; const r = b.getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; })()`);
  if (r) {
    await browser.click(r[0], r[1]);
    await sleep(1500);
    const fs2 = await fullscreen();
    if (noFs) skip("the page's fullscreen button works", noFs);
    else ok = step("the page's fullscreen button works", !!fs2, fs2 ? `fullscreen element: ${fs2}` : "document.fullscreenElement stayed empty") && ok;
    await browser.eval(`document.fullscreenElement ? document.exitFullscreen().then(() => 1) : 0`).catch(() => {});
    await sleep(1000);
  } else ok = step("the page's fullscreen button works", false, "no visible #fullscreen button") && ok;

  // reload: the fidelity is still High (the game's startup line), and settings.json is in IndexedDB
  await sleep(1500);   // the settings flush to IndexedDB runs in the background
  from = lines.length;
  await browser.reload();
  const t2 = await waitFor(/\[Title\] show/, opt.timeout, from);
  let qStart = null;
  for (let i = from; i < lines.length; i++) { const m = lines[i].match(/\[Quality\] (\w+):/); if (m) qStart = m[1]; }
  const idb = await browser.eval(`new Promise(res => { const r = indexedDB.open('/idbfs'); r.onerror = () => res('no /idbfs');
    r.onsuccess = () => { const db = r.result; if (!db.objectStoreNames.contains('FILE_DATA')) { res('no FILE_DATA'); return; }
      const out = []; db.transaction('FILE_DATA', 'readonly').objectStore('FILE_DATA').openCursor().onsuccess = e => { const c = e.target.result;
        if (!c) { res(out.join('; ')); return; } const v = c.value;
        if (v.contents && /settings\\.json$/.test(c.key)) { const t = new TextDecoder().decode(v.contents); const m = t.match(/"quality",\\s*"value":\\s*([0-9.]+)/); out.push(c.key + ' quality=' + (m ? m[1] : '?')); }
        else if (v.contents) out.push(c.key); c.continue(); }; }; })`).catch((e) => e.message);
  ok = step("a setting survives a reload", !!t2 && qStart === "High", `after the reload the game started at ${qStart ?? "?"}; IndexedDB: ${idb}`) && ok;
  await sleep(2500);

  // play with keys and the mouse: Begin, Monday morning, turn to the drawers and back, the bell, a click, pause and back
  from = lines.length;
  await press("ArrowDown");
  await press("Enter");
  const day = await waitFor(/\[Day\] 1: morning/, 30, from);
  ok = step("Begin starts Monday", !!day) && ok;
  await sleep(5000);
  await shot("monday");
  await press("KeyD"); await sleep(1500); await shot("drawers");
  await press("KeyA"); await sleep(1500);
  for (let i = 0; i < 4; i++) { await press("Space"); await sleep(1500); }
  await shot("claim");
  const c = await browser.eval(`(() => { const r = document.getElementById('unity-canvas').getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height * 0.55]; })()`);
  await browser.click(c[0], c[1]);
  await sleep(1500);
  await press("Escape"); await sleep(800);   // put down whatever the click picked up, if anything
  await press("Escape"); await sleep(1500);
  await shot("pause_or_desk");
  await press("Escape"); await sleep(1500);
  ok = step("a short session with keys and the mouse", errorsSeen === 0, errorsSeen ? "errors above" : "no errors") && ok;

  // the game's own AutoPilot through Monday's first two claims, then the save picked up again after a reload
  from = lines.length;
  const sep = url.includes("?") ? "&" : "?";
  await browser.goto(`${url}${sep}lafAutopilot=x&lafQuitAfter=1.2&lafSpeed=3`);
  const quit = await waitFor(/\[Auto\] quitting after case (\S+)/, 300, from);
  const decided = lines.slice(from).filter((l) => /^\[Auto\] day 1 case/.test(l));
  ok = step("the AutoPilot plays Monday's first claims", !!quit && decided.length >= 2, decided.map((l) => l.replace(/^\[Auto\] /, "")).join("; ") || "nothing decided") && ok;
  await sleep(4000);   // the save's flush to IndexedDB
  from = lines.length;
  await browser.goto(`${url}${sep}lafAutopilot=x&lafContinue&lafQuitAfter=1.3&lafSpeed=3`);
  const resumed = await waitFor(/\[Day\] 1: resuming after (\d+) case/, 120, from);
  ok = step("the save survives a reload", !!resumed && Number(resumed[1]) >= 2, resumed ? resumed[0] : "no resume line") && ok;
  await waitFor(/\[Auto\] quitting/, 200, from);
  await sleep(1500);
  return ok;
}

main();
