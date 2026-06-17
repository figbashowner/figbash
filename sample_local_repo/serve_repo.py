"""Serve this sample repository over HTTP for Unity repo-loading tests.

Usage:
    python serve_repo.py
    python serve_repo.py --port 8000
    python serve_repo.py --port 0

The Unity app should be pointed at the catalog URL printed by the script,
for example:
    http://127.0.0.1:8000/catalog.json
"""

from __future__ import annotations

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Serve the sample_local_repo folder over HTTP."
    )
    parser.add_argument(
        "--host",
        default="127.0.0.1",
        help="Interface to bind to. Defaults to 127.0.0.1.",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8000,
        help="Port to bind to. Use 0 to pick a free port automatically.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    root = Path(__file__).resolve().parent

    handler = partial(SimpleHTTPRequestHandler, directory=str(root))
    server = ThreadingHTTPServer((args.host, args.port), handler)
    actual_host, actual_port = server.server_address[:2]
    catalog_url = f"http://{actual_host}:{actual_port}/catalog.json"

    print(f"Serving {root}")
    print(f"Catalog URL: {catalog_url}")
    print("Press Ctrl+C to stop.")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping server.")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
