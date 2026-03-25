#!/usr/bin/env python3
"""Small standalone probe for Subdivx search flow.

Usage:
  python3 tools/subdivx_probe.py --query "Made in Abyss" \
    --cf-clearance "..." \
    --sdx "..." \
    --user-agent "Mozilla/5.0 ..."
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import urllib.parse
import urllib.request


BASE_URL = "https://subdivx.com"
VERSION_RE = re.compile(r"(?:index-min\.js|sdx-min\.css)\?v=([0-9.]+)", re.IGNORECASE)


def build_cookie_header(cf_clearance: str, sdx: str, cookie_header: str) -> str:
    if cookie_header.strip():
        return cookie_header.strip()

    parts = []
    if cf_clearance.strip():
        parts.append(f"cf_clearance={cf_clearance.strip()}")
    if sdx.strip():
        parts.append(f"sdx={sdx.strip()}")
    return "; ".join(parts)


def request_text(url: str, headers: dict[str, str]) -> str:
    req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.read().decode("utf-8", errors="replace")


def request_json(url: str, headers: dict[str, str]) -> object:
    return json.loads(request_text(url, headers))


def post_form_json(url: str, data: dict[str, str], headers: dict[str, str]) -> object:
    encoded = urllib.parse.urlencode(data).encode("utf-8")
    merged = dict(headers)
    merged["Content-Type"] = "application/x-www-form-urlencoded; charset=UTF-8"
    req = urllib.request.Request(url, data=encoded, headers=merged, method="POST")
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read().decode("utf-8", errors="replace"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--query", required=True)
    parser.add_argument("--cf-clearance", default="")
    parser.add_argument("--sdx", default="")
    parser.add_argument("--cookie-header", default="")
    parser.add_argument("--user-agent", required=True)
    args = parser.parse_args()

    cookie_header = build_cookie_header(args.cf_clearance, args.sdx, args.cookie_header)
    headers = {
        "Accept": "application/json, text/javascript, */*; q=0.1",
        "Origin": BASE_URL,
        "Referer": BASE_URL + "/",
        "User-Agent": args.user_agent,
        "X-Requested-With": "XMLHttpRequest",
    }
    if cookie_header:
        headers["Cookie"] = cookie_header

    html = request_text(BASE_URL + "/", headers)
    match = VERSION_RE.search(html)
    if not match:
        print("Could not determine Subdivx frontend version.", file=sys.stderr)
        return 2

    version_suffix = match.group(1).replace(".", "")
    token_payload = request_json(BASE_URL + "/inc/gt.php?gt=1", headers)
    token = token_payload.get("token") if isinstance(token_payload, dict) else None
    if not token:
        print(f"Token response invalid: {token_payload!r}", file=sys.stderr)
        return 3

    search_field = f"buscar{version_suffix}"
    payload = post_form_json(
        BASE_URL + "/inc/ajax.php",
        {
            "tabla": "resultados",
            "filtros": "",
            search_field: args.query,
            "token": token,
        },
        headers,
    )

    if not isinstance(payload, dict):
        print(f"Unexpected search payload: {payload!r}", file=sys.stderr)
        return 4

    items = payload.get("aaData") or []
    print(f"Version suffix: {version_suffix}")
    print(f"Token ok: yes")
    print(f"Query: {args.query}")
    print(f"Results: {len(items)}")
    for item in items[:10]:
        if not isinstance(item, dict):
            continue
        title = item.get("titulo")
        subtitle_id = item.get("id")
        downloads = item.get("descargas")
        uploader = item.get("nick")
        description = item.get("descripcion")
        print("-" * 40)
        print(f"id: {subtitle_id}")
        print(f"title: {title}")
        print(f"downloads: {downloads}")
        print(f"uploader: {uploader}")
        print(f"description: {description}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
