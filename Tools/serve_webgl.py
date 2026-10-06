# Serves Builds/WebGL on http://127.0.0.1:8764 for testing (python3 Tools/serve_webgl.py Builds/WebGL).
# The Brotli files are sent with a br content encoding header, as a real host would have to.
import http.server, functools, sys
class H(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        p = self.path.split('?')[0]
        if p.endswith('.unityweb') or p.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        super().end_headers()
    def guess_type(self, path):
        if '.wasm' in path: return 'application/wasm'
        if '.js' in path: return 'application/javascript'
        if path.endswith('.data.br') or path.endswith('.data.unityweb'): return 'application/octet-stream'
        return super().guess_type(path)
http.server.ThreadingHTTPServer(('127.0.0.1', 8764), functools.partial(H, directory=sys.argv[1])).serve_forever()
