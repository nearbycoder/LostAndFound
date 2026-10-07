#!/usr/bin/env python3
"""Play the WebGL build in Chrome on the real GPU (not the software renderer headless Chrome usually falls back to),
record the page's console, and take a screenshot whenever the game logs "[Shot] <name>".

By default Chrome runs headless with ANGLE on the system's EGL/GL driver, which draws on the GPU and paces frames at
60 Hz like an ordinary monitor. --headed opens a real window instead, but on a shared desktop that window may never
be shown, and then the compositor stops its frames (requestAnimationFrame doesn't fire at all).

    python3 Tools/serve_webgl.py Builds/WebGL &          # the build on http://127.0.0.1:8764
    python3 Tools/webgl_check.py --out Logs/webgl/smoke --profile Logs/webgl/profile \
        --url 'http://127.0.0.1:8764/?lafSmoke=smoke&lafSeconds=40' --until '\\[Smoke\\] done'

The game reads its -laf... options from the page's query string in a browser (Game.Arg). The window closes once a
console line matches --until (after --linger seconds, so the browser can finish writing saves to IndexedDB) or at
--timeout. Reuse --profile to keep the browser's storage (the save) between runs. Stdlib only: it speaks just enough
of the WebSocket protocol for Chrome's DevTools.
"""
import argparse, base64, json, os, re, socket, struct, subprocess, sys, time, urllib.request


IDB_LIST = """new Promise(res => { const r = indexedDB.open('/idbfs');
  r.onerror = () => res(['(no /idbfs database)']);
  r.onsuccess = () => { const db = r.result; if (!db.objectStoreNames.contains('FILE_DATA')) { res(['(no FILE_DATA store)']); return; }
    const out = []; db.transaction('FILE_DATA', 'readonly').objectStore('FILE_DATA').openCursor().onsuccess = e => {
      const c = e.target.result; if (!c) { res(out.length ? out : ['(empty)']); return; }
      const v = c.value; out.push(c.key + (v.contents ? ' ' + v.contents.length + ' bytes' : ' (folder)')); c.continue(); }; }; })"""


class DevTools:
    """A minimal WebSocket client for one DevTools page target."""

    def __init__(self, ws_url):
        host, path = re.match(r"ws://([^/]+)(/.*)", ws_url).groups()
        h, p = host.split(":")
        self.sock = socket.create_connection((h, int(p)), timeout=5)
        key = base64.b64encode(os.urandom(16)).decode()
        self.sock.sendall((f"GET {path} HTTP/1.1\r\nHost: {host}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                           f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n").encode())
        head = b""
        while b"\r\n\r\n" not in head:
            head += self.sock.recv(1)
        if b" 101 " not in head.split(b"\r\n")[0]:
            raise RuntimeError("DevTools refused the WebSocket: " + head.decode(errors="replace"))
        self.buf = b""
        self.next_id = 0

    def send(self, method, params=None):
        self.next_id += 1
        data = json.dumps({"id": self.next_id, "method": method, "params": params or {}}).encode()
        n = len(data)
        head = bytes([0x81]) + (bytes([0x80 | n]) if n < 126 else bytes([0x80 | 126]) + struct.pack(">H", n) if n < 65536
                                 else bytes([0x80 | 127]) + struct.pack(">Q", n))
        mask = os.urandom(4)
        self.sock.sendall(head + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(data)))
        return self.next_id

    def _read(self, n):
        while len(self.buf) < n:
            chunk = self.sock.recv(1 << 16)
            if not chunk:
                raise ConnectionError("DevTools closed the connection")
            self.buf += chunk
        out, self.buf = self.buf[:n], self.buf[n:]
        return out

    def recv(self, timeout):
        """The next message as a dict, or None if nothing arrives within timeout seconds."""
        self.sock.settimeout(timeout)
        payload = b""
        try:
            while True:
                b0, b1 = self._read(2)
                n = b1 & 0x7F
                if n == 126:
                    n = struct.unpack(">H", self._read(2))[0]
                elif n == 127:
                    n = struct.unpack(">Q", self._read(8))[0]
                data = self._read(n)
                op = b0 & 0x0F
                if op == 0x9:            # ping
                    continue
                if op == 0x8:
                    raise ConnectionError("DevTools closed the connection")
                payload += data
                if b0 & 0x80:
                    return json.loads(payload)
        except socket.timeout:
            return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--url", required=True)
    ap.add_argument("--out", required=True, help="folder for console.log and the screenshots")
    ap.add_argument("--profile", required=True, help="Chrome profile folder (its IndexedDB holds the game's save)")
    ap.add_argument("--until", default=r"\[Smoke\] done|\[Auto\] PASS|\[Auto\] FAIL|\[Auto\] quitting")
    ap.add_argument("--timeout", type=float, default=600)
    ap.add_argument("--linger", type=float, default=8, help="seconds to stay open after --until matches")
    ap.add_argument("--size", default="1600,900")
    ap.add_argument("--port", type=int, default=9337)
    ap.add_argument("--js", help="JavaScript to run in the page once it's up (e.g. to resize the canvas)")
    ap.add_argument("--headed", action="store_true", help="a real window (Wayland or X11) rather than headless")
    ap.add_argument("--chrome-flags", default="", help="extra Chrome flags, space-separated")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    os.makedirs(a.profile, exist_ok=True)
    chrome = ["google-chrome-stable", f"--user-data-dir={os.path.abspath(a.profile)}", "--no-first-run",
              "--no-default-browser-check", f"--remote-debugging-port={a.port}", f"--window-size={a.size}",
              "--autoplay-policy=no-user-gesture-required", "--ignore-gpu-blocklist", "--disable-background-timer-throttling",
              "--disable-renderer-backgrounding", "--disable-backgrounding-occluded-windows"]
    if not a.headed:
        chrome += ["--headless=new", "--use-gl=angle", "--use-angle=gl-egl"]
    elif os.environ.get("WAYLAND_DISPLAY"):
        chrome.append("--ozone-platform=wayland")
    chrome += a.chrome_flags.split()
    proc = subprocess.Popen(chrome + ["about:blank"], stdout=subprocess.DEVNULL, stderr=open(os.path.join(a.out, "chrome.log"), "w"))
    print(f"[webgl] chrome pid {proc.pid}", flush=True)
    log = open(os.path.join(a.out, "console.log"), "w")
    t0 = time.time()
    status = 1
    try:
        target = None
        while target is None and time.time() - t0 < 30:
            try:
                pages = json.load(urllib.request.urlopen(f"http://127.0.0.1:{a.port}/json/list", timeout=2))
                target = next((p for p in pages if p.get("type") == "page"), None)
            except OSError:
                time.sleep(0.5)
        if target is None:
            raise RuntimeError("Chrome's DevTools never came up")
        dt = DevTools(target["webSocketDebuggerUrl"])
        dt.send("Runtime.enable")
        probe = ("(()=>{const g=document.createElement('canvas').getContext('webgl2');if(!g)return 'no webgl2';"
                 "const e=g.getExtension('WEBGL_debug_renderer_info');return e?g.getParameter(e.UNMASKED_RENDERER_WEBGL):g.getParameter(g.RENDERER)})()")
        gpu_probe, probe_at = None, time.time() + 3
        dt.send("Page.navigate", {"url": a.url})
        shots = {}
        done_at = None
        while True:
            now = time.time()
            if done_at is not None and now >= done_at:
                status = 0
                break
            if now - t0 > a.timeout:
                print("[webgl] timed out", flush=True)
                log.write(f"[webgl] timed out after {a.timeout:.0f}s\n")
                break
            if probe_at is not None and now >= probe_at:   # once the page is up: which GPU WebGL is drawing with
                gpu_probe, probe_at = dt.send("Runtime.evaluate", {"expression": probe, "returnByValue": True}), None
                if a.js:
                    dt.send("Runtime.evaluate", {"expression": a.js})
            m = dt.recv(1.0)
            if m is None:
                continue
            if gpu_probe is not None and m.get("id") == gpu_probe:
                r = m.get("result", {}).get("result", {}).get("value") or json.dumps(m)[:300]
                print(f"[webgl] WebGL renderer: {r}", flush=True)
                log.write(f"[webgl] WebGL renderer: {r}\n")
            elif m.get("id") in shots:
                name = shots.pop(m["id"])
                data = m.get("result", {}).get("data")
                if data:
                    with open(os.path.join(a.out, name + ".png"), "wb") as f:
                        f.write(base64.b64decode(data))
            elif m.get("method") == "Runtime.consoleAPICalled":
                text = " ".join(str(x.get("value", x.get("description", ""))) for x in m["params"].get("args", []))
                log.write(f"{now - t0:7.1f} {m['params'].get('type', 'log'):7s} {text}\n")
                log.flush()
                for line in text.splitlines():
                    s = re.search(r"\[Shot\] (\S+)", line)
                    if s:
                        shots[dt.send("Page.captureScreenshot", {"format": "png"})] = s.group(1)
                    if done_at is None and re.search(a.until, line):
                        print(f"[webgl] {line.strip()}", flush=True)
                        done_at = now + a.linger
            elif m.get("method") == "Runtime.exceptionThrown":
                d = m["params"]["exceptionDetails"]
                log.write(f"{now - t0:7.1f} EXCEPTION {d.get('exception', {}).get('description', d.get('text'))}\n")
        # what the page has stored: the game's save and settings live in IndexedDB's "/idbfs" database
        listing = dt.send("Runtime.evaluate", {"awaitPromise": True, "returnByValue": True, "expression": IDB_LIST})
        end = time.time() + 10
        while time.time() < end:
            m = dt.recv(1.0)
            if m and m.get("id") == listing:
                for line in (m.get("result", {}).get("result", {}).get("value") or ["(nothing)"]):
                    log.write(f"[idbfs] {line}\n")
                    print(f"[webgl] idbfs: {line}", flush=True)
                break
        try:
            dt.send("Browser.close")
            proc.wait(10)
        except Exception:
            pass
    finally:
        log.close()
        if proc.poll() is None:
            proc.terminate()
            try:
                proc.wait(10)
            except subprocess.TimeoutExpired:
                proc.kill()
    print(f"[webgl] console log in {os.path.join(a.out, 'console.log')}", flush=True)
    sys.exit(status)


if __name__ == "__main__":
    main()
