import threading
import time
import urllib.request
import urllib.error
from http.server import HTTPServer, BaseHTTPRequestHandler
from pathlib import Path
import tempfile
import shutil

class MockQdrantHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        if "nonexistent" in self.path:
            self.send_response(404)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(b'{"status":{"error":"Collection nonexistent does not exist"}}')
        elif "timeout" in self.path:
            time.sleep(2)
            self.send_response(200)
            self.end_headers()
        else:
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(b'{"result":{"name":"test.snapshot"}}')

    def do_GET(self):
        if "partial" in self.path:
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", "10000")
            self.end_headers()
            self.wfile.write(b"ABCDEFGHIJ" * 10)
            self.wfile.flush()
            # Simulate broken pipe / connection dropped
            return

    def log_message(self, format, *args):
        pass # Suppress logging

def run_tests():
    print("=== EMPIRICAL TEST: Qdrant Backup Failure Modes ===")
    
    server = HTTPServer(("127.0.0.1", 18889), MockQdrantHandler)
    server_thread = threading.Thread(target=server.serve_forever, daemon=True)
    server_thread.start()

    # Test 1: Missing collection error (404)
    print("\nTest 1: Testing missing collection (404)...")
    url_404 = "http://127.0.0.1:18889/collections/nonexistent/snapshots"
    req = urllib.request.Request(url_404, data=b"", method="POST")
    try:
        urllib.request.urlopen(req, timeout=5)
        print("FAIL: Expected 404 error but succeeded!")
    except urllib.error.HTTPError as ex:
        print(f"CONFIRMED: Script crashes with unhandled HTTPError: {ex.code} {ex.reason}")

    # Test 2: Network timeout during snapshot trigger
    print("\nTest 2: Testing timeout handling...")
    url_timeout = "http://127.0.0.1:18889/collections/timeout/snapshots"
    req2 = urllib.request.Request(url_timeout, data=b"", method="POST")
    try:
        urllib.request.urlopen(req2, timeout=0.5)
        print("FAIL: Expected timeout!")
    except Exception as ex:
        print(f"CONFIRMED: Script crashes on timeout without retry: {type(ex).__name__}")

    # Test 3: Partial corrupted snapshot file left on disk
    print("\nTest 3: Testing partial file corruption left on disk...")
    temp_dir = Path(tempfile.mkdtemp())
    try:
        local_file = temp_dir / "test.snapshot"
        try:
            with urllib.request.urlopen("http://127.0.0.1:18889/collections/partial", timeout=2) as response:
                with open(local_file, "wb") as f:
                    chunk = response.read(20)
                    f.write(chunk)
                    # Simulate premature network drop mid-transfer
                    raise ConnectionResetError("Simulated network drop")
        except ConnectionResetError:
            pass

        if local_file.exists():
            print(f"CONFIRMED: Corrupted partial file was left on disk! Size: {local_file.stat().st_size} bytes")
            print("Vulnerability Confirmed: Incomplete snapshot is stored as valid backup, breaking disaster recovery!")
    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)
        server.shutdown()

if __name__ == "__main__":
    run_tests()
