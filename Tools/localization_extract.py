#!/usr/bin/env python3
"""Extrae textos de la UI a Resources/Localization/es.json (F7-06).

Uso:
  python3 Tools/localization_extract.py          # aplica y fusiona la tabla
  python3 Tools/localization_extract.py --check  # solo valida, no escribe

Convención:
  - UXML: text="..." -> text="{loc:ui.<pantalla>.<slug>}"
  - C#:   text = "..." -> text = LocalizationService.T("ui.code.<archivo>.<slug>")
  - Los valores españoles viven en es.json; el runtime los resuelve.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UI_DIR = ROOT / "Assets/_TacticalFive/UI"
CS_DIR = ROOT / "Assets/_TacticalFive/Scripts/UI"
CS_EXTRA = [ROOT / "Assets/_TacticalFive/Scripts/Core/UIScreenController.cs"]
JSON_PATH = ROOT / "Assets/_TacticalFive/Resources/Localization/es.json"

TAG_RE = re.compile(r"<(?P<tag>[A-Za-z0-9_:]+)(?P<attrs>[^>]*)>", re.S)
ATTR_RE = re.compile(r'([A-Za-z0-9_:-]+)="([^"]*)"')
TOKEN_RE = re.compile(r"\{loc:([A-Za-z0-9_.-]+)\}")
LOCALIZABLE_ATTRS = ("text", "placeholder", "placeholder-text")
CS_TEXT_RE = re.compile(r'(?P<prefix>\btext\s*=\s*)"(?P<value>(?:\\.|[^"\\])*)"')
CS_LABEL_RE = re.compile(r'new\s+Label\(\s*"(?P<value>(?:\\.|[^"\\])*)"\s*\)')
CS_T_RE = re.compile(r'LocalizationService\.T\("([^"]+)"')


def has_letter(value: str) -> bool:
    return any(unicodedata.category(ch).startswith("L") for ch in value)


def translatable(value: str) -> bool:
    value = (value or "").strip()
    if not value or value.startswith("{loc:"):
        return False
    return has_letter(value)


def ascii_slug(value: str, max_len: int = 40) -> str:
    value = unicodedata.normalize("NFKD", value or "")
    value = "".join(ch for ch in value if not unicodedata.combining(ch))
    value = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", value)
    value = re.sub(r"[^A-Za-z0-9]+", "_", value).strip("_").lower()
    if not value:
        value = "text"
    if len(value) > max_len:
        suffix = hashlib.sha1(value.encode("utf-8")).hexdigest()[:8]
        value = value[:max_len].rstrip("_") + "_" + suffix
    return value


def decode_csharp(raw: str) -> str:
    out = []
    i = 0
    simple = {
        "n": "\n", "r": "\r", "t": "\t", "0": "\0", "\\": "\\",
        '"': '"', "'": "'", "a": "\a", "b": "\b", "f": "\f", "v": "\v",
    }
    while i < len(raw):
        ch = raw[i]
        if ch != "\\":
            out.append(ch)
            i += 1
            continue
        i += 1
        if i >= len(raw):
            out.append("\\")
            break
        esc = raw[i]
        i += 1
        if esc == "u" and i + 4 <= len(raw):
            out.append(chr(int(raw[i:i + 4], 16)))
            i += 4
        elif esc == "U" and i + 8 <= len(raw):
            out.append(chr(int(raw[i:i + 8], 16)))
            i += 8
        elif esc == "x":
            start = i
            while i < len(raw) and i - start < 4 and raw[i] in "0123456789abcdefABCDEF":
                i += 1
            if i > start:
                out.append(chr(int(raw[start:i], 16)))
            else:
                out.append("\\x")
        elif esc in simple:
            out.append(simple[esc])
        else:
            out.append("\\" + esc)
    return "".join(out)


def screen_key(path: Path) -> str:
    rel = path.relative_to(UI_DIR)
    parts = rel.parts
    if len(parts) > 1 and parts[0] == "Screens":
        return ascii_slug(parts[1])
    if "Core" in parts:
        return "core_" + ascii_slug(path.stem)
    return ascii_slug(path.stem)


def make_key(prefix: str, base: str, value: str, entries: dict[str, str], used: dict[str, str]) -> str:
    candidate = f"{prefix}.{base}"
    n = 2
    while candidate in used and used[candidate] != value:
        candidate = f"{prefix}.{base}_{n}"
        n += 1
    used[candidate] = value
    entries.setdefault(candidate, value)
    return candidate


def load_existing() -> dict[str, str]:
    if not JSON_PATH.exists():
        return {}
    try:
        data = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    except Exception:
        return {}
    result = {}
    for entry in data.get("entries", []):
        key = entry.get("key")
        if key:
            result[key] = entry.get("value", "")
    return result


def write_table(entries: dict[str, str]) -> None:
    JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "language": "es",
        "entries": [
            {"key": key, "value": entries[key]}
            for key in sorted(entries)
        ],
    }
    JSON_PATH.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def process_uxml(path: Path, entries: dict[str, str], used: dict[str, str]) -> str:
    source = path.read_text(encoding="utf-8")
    prefix = f"ui.{screen_key(path)}"

    def process_tag(match: re.Match) -> str:
        tag = match.group(0)
        attrs = dict(ATTR_RE.findall(match.group("attrs")))
        updated = tag
        for attr in LOCALIZABLE_ATTRS:
            value = attrs.get(attr)
            if value is None or not translatable(value):
                continue
            decoded = html.unescape(value)
            if not translatable(decoded):
                continue
            base = ascii_slug(attrs.get("name") or decoded)
            key = make_key(prefix, base, decoded, entries, used)
            updated = re.sub(
                rf'\b{re.escape(attr)}="[^"]*"',
                f'{attr}="{{loc:{key}}}"',
                updated,
                count=1,
            )
        return updated

    return TAG_RE.sub(process_tag, source)


def is_commented(source: str, pos: int) -> bool:
    line_start = source.rfind("\n", 0, pos) + 1
    line = source[line_start:pos].lstrip()
    return line.startswith("//") or line.startswith("*")


def process_cs(path: Path, entries: dict[str, str], used: dict[str, str]) -> str:
    source = path.read_text(encoding="utf-8")
    file_slug = ascii_slug(path.stem)
    prefix = f"ui.code.{file_slug}"

    def replace_text(match: re.Match) -> str:
        if is_commented(source, match.start()):
            return match.group(0)
        raw = match.group("value")
        decoded = decode_csharp(raw)
        if not translatable(decoded):
            return match.group(0)
        key = make_key(prefix, ascii_slug(decoded), decoded, entries, used)
        return f'{match.group("prefix")}LocalizationService.T("{key}")'

    def replace_label(match: re.Match) -> str:
        if is_commented(source, match.start()):
            return match.group(0)
        raw = match.group("value")
        decoded = decode_csharp(raw)
        if not translatable(decoded):
            return match.group(0)
        key = make_key(prefix, ascii_slug(decoded), decoded, entries, used)
        return f'new Label(LocalizationService.T("{key}"))'

    source = CS_TEXT_RE.sub(replace_text, source)
    source = CS_LABEL_RE.sub(replace_label, source)
    return source


def collect_tokens() -> set[str]:
    tokens = set()
    for path in UI_DIR.rglob("*.uxml"):
        tokens.update(TOKEN_RE.findall(path.read_text(encoding="utf-8")))
    for path in list(CS_DIR.rglob("*.cs")) + [p for p in CS_EXTRA if p.exists()]:
        source = path.read_text(encoding="utf-8")
        tokens.update(TOKEN_RE.findall(source))
        tokens.update(CS_T_RE.findall(source))
    # Placeholders de documentación/comentarios, no claves reales.
    tokens.discard("...")
    tokens.discard("clave")
    return tokens


def check(entries: dict[str, str]) -> int:
    problems = []
    for path in UI_DIR.rglob("*.uxml"):
        source = path.read_text(encoding="utf-8")
        for match in TAG_RE.finditer(source):
            attrs = dict(ATTR_RE.findall(match.group("attrs")))
            for attr in LOCALIZABLE_ATTRS:
                value = attrs.get(attr)
                if value is not None and translatable(value) and not value.startswith("{loc:"):
                    problems.append(f"{path.relative_to(ROOT)}: {attr} sin token: {value[:80]!r}")
    for path in sorted(list(CS_DIR.rglob("*.cs")) + [p for p in CS_EXTRA if p.exists()]):
        source = path.read_text(encoding="utf-8")
        for match in CS_TEXT_RE.finditer(source):
            if is_commented(source, match.start()):
                continue
            value = decode_csharp(match.group("value"))
            if translatable(value):
                problems.append(f"{path.relative_to(ROOT)}: text sin clave: {value[:80]!r}")
        for match in CS_LABEL_RE.finditer(source):
            if is_commented(source, match.start()):
                continue
            value = decode_csharp(match.group("value"))
            if translatable(value):
                problems.append(f"{path.relative_to(ROOT)}: new Label sin clave: {value[:80]!r}")
    for token in sorted(collect_tokens()):
        if token not in entries:
            problems.append(f"Token sin entrada en es.json: {token}")
    if problems:
        print("\n".join(problems), file=sys.stderr)
        print(f"\n{len(problems)} problemas de localización.", file=sys.stderr)
        return 1
    print(f"Localización OK: {len(collect_tokens())} tokens, {len(entries)} entradas.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="solo valida, no modifica archivos")
    args = parser.parse_args()

    entries = load_existing()
    used = dict(entries)

    if args.check:
        # En modo check no se generan textos nuevos: solo se comparan tokens y UXML.
        return check(entries)

    for path in sorted(UI_DIR.rglob("*.uxml")):
        original = path.read_text(encoding="utf-8")
        updated = process_uxml(path, entries, used)
        if updated != original:
            path.write_text(updated, encoding="utf-8")

    for path in sorted(list(CS_DIR.rglob("*.cs")) + [p for p in CS_EXTRA if p.exists()]):
        original = path.read_text(encoding="utf-8")
        updated = process_cs(path, entries, used)
        if updated != original:
            path.write_text(updated, encoding="utf-8")

    write_table(entries)
    print(f"Localización aplicada: {len(entries)} entradas en {JSON_PATH.relative_to(ROOT)}.")
    return check(entries)


if __name__ == "__main__":
    raise SystemExit(main())
