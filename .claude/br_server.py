import http.server
import sys

class BrotliAwareHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        if self.path.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
            if self.path.endswith('.js.br'):
                self.send_header('Content-Type', 'application/javascript')
            elif self.path.endswith('.wasm.br'):
                self.send_header('Content-Type', 'application/wasm')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def send_head(self):
        # Strip conditional-GET headers so every request gets a full fresh
        # 200 response, never a 304 that reuses a client-cached body from
        # before Content-Encoding was being sent correctly.
        for h in ('If-Modified-Since', 'If-None-Match'):
            if h in self.headers:
                del self.headers[h]
        return super().send_head()

if __name__ == '__main__':
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8791
    directory = sys.argv[2] if len(sys.argv) > 2 else '.'
    handler = lambda *args, **kwargs: BrotliAwareHandler(*args, directory=directory, **kwargs)
    http.server.ThreadingHTTPServer(('', port), handler).serve_forever()
