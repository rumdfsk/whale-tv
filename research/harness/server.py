"""Loopback-only diagnostic relay for the approved official HLS samples.

No account access, signature generation, recording or public broadcasting.
Only URLs from private/streams.json and same-origin playlist children are served.
"""
import hashlib
import json
import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urljoin, urlsplit

import requests

ROOT = Path(__file__).resolve().parent
CONFIG = ROOT.parent / "private" / "streams.json"
RESULTS = ROOT.parent / "results"
LOCK = threading.Lock()
RESOURCES = {}
COUNTERS = {"requests": 0, "upstream_errors": 0}


def register(index, url):
    key = hashlib.sha256((str(index) + url).encode()).hexdigest()[:24]
    with LOCK:
        RESOURCES[key] = (index, url)
    return "/media/" + key


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def send(self, status, body, content_type):
        if isinstance(body, str):
            body = body.encode()
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_GET(self):
        path = urlsplit(self.path).path
        items = json.loads(CONFIG.read_text())
        if path == "/api/channels":
            body = [{"name": item["name"], "source": register(i, item["url"])} for i, item in enumerate(items)]
            return self.send(200, json.dumps(body, ensure_ascii=False), "application/json")
        if path == "/api/counters":
            return self.send(200, json.dumps(COUNTERS), "application/json")
        if path.startswith("/media/"):
            with LOCK:
                resource = RESOURCES.get(path.rsplit("/", 1)[-1])
            if resource is None:
                return self.send(404, "Unknown sample", "text/plain")
            index, url = resource
            item = items[index]
            expected = urlsplit(item["url"])
            parsed = urlsplit(url)
            if parsed.scheme != "https" or parsed.netloc != expected.netloc:
                return self.send(403, "Outside approved CDN origin", "text/plain")
            try:
                with LOCK:
                    COUNTERS["requests"] += 1
                response = requests.get(url, headers=item.get("headers", {}), timeout=15, allow_redirects=False)
                if response.status_code != 200:
                    with LOCK:
                        COUNTERS["upstream_errors"] += 1
                    return self.send(502, "Upstream status " + str(response.status_code), "text/plain")
                if len(response.content) > 16 * 1024 * 1024:
                    return self.send(502, "Sample too large", "text/plain")
                if response.content.startswith(b"#EXTM3U"):
                    def child(value):
                        absolute = urljoin(url, value)
                        if urlsplit(absolute).netloc != expected.netloc or urlsplit(absolute).scheme != "https":
                            raise ValueError("Cross-origin child is outside this probe")
                        return register(index, absolute)
                    lines = []
                    for line in response.text.splitlines():
                        if line and not line.startswith("#"):
                            line = child(line.strip())
                        elif 'URI="' in line:
                            line = re.sub(r'URI="([^"]+)"', lambda m: 'URI="' + child(m[1]) + '"', line)
                        lines.append(line)
                    return self.send(200, "\n".join(lines) + "\n", "application/vnd.apple.mpegurl")
                return self.send(200, response.content, "video/mp2t")
            except Exception as exc:
                with LOCK:
                    COUNTERS["upstream_errors"] += 1
                return self.send(502, type(exc).__name__, "text/plain")
        files = {"/": ("index.html", "text/html; charset=utf-8"),
                 "/hls.min.js": ("vendor/hls.min.js", "application/javascript")}
        if path in files:
            filename, mime = files[path]
            return self.send(200, (ROOT / filename).read_bytes(), mime)
        self.send(404, "Not found", "text/plain")

    def do_POST(self):
        if self.path != "/api/results":
            return self.send(404, "Not found", "text/plain")
        # Same-origin diagnostic writes only; never an arbitrary local file path.
        if self.headers.get("Origin") != "http://127.0.0.1:8765":
            return self.send(403, "Origin required", "text/plain")
        size = int(self.headers.get("Content-Length", "0"))
        if not 0 < size <= 1024 * 1024:
            return self.send(413, "Invalid report size", "text/plain")
        try:
            body = json.loads(self.rfile.read(size))
            RESULTS.mkdir(exist_ok=True)
            (RESULTS / "browser-harness.json").write_text(json.dumps(body, ensure_ascii=False, indent=2))
            self.send(200, '{"saved":true}', "application/json")
        except (ValueError, OSError):
            self.send(400, "Invalid report", "text/plain")


if __name__ == "__main__":
    print("Diagnostic harness: http://127.0.0.1:8765 (loopback only)", flush=True)
    ThreadingHTTPServer(("127.0.0.1", 8765), Handler).serve_forever()
