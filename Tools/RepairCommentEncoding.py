#!/usr/bin/env python3
"""Scan C# comments for encoding damage and restore what can be restored.

Usage (from repo root):
    python Tools/RepairCommentEncoding.py
    python Tools/RepairCommentEncoding.py --apply

Recoverable:
    - whole file saved as GBK
    - UTF-8 bytes mis-decoded as Latin-1 / GBK (mojibake)
Not recoverable without history:
    - Chinese replaced by ???  (tries git log of the same line)
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

SCAN_ROOTS = ("Assets/Scripts", "Assets/Editor")
SKIP_PARTS = ("/Plugins/", "/Config/Luban/", "/PackageCache/")
GIT_DEPTH = 25
MAX_BYTES = 2 * 1024 * 1024
CJK_RE = re.compile(r"[\u3400-\u9FFF\u3000-\u303F\uFF00-\uFFEF]")
GARBLED_ASSIGN_RE = re.compile(
    r'(DisplayName|ReferenceNote|Tooltip|Header)\s*=\s*"'
)


def is_cjk(ch: str) -> bool:
    o = ord(ch)
    return 0x3400 <= o <= 0x9FFF or 0x3000 <= o <= 0x303F or 0xFF00 <= o <= 0xFFEF


def count_cjk(text: str) -> int:
    return sum(1 for ch in text if is_cjk(ch))


def ascii_skeleton(line: str) -> str:
    return "".join(
        ch
        for ch in line
        if ch not in "?\ufffd" and not ch.isspace() and not is_cjk(ch)
    )


def should_skip(path: Path) -> bool:
    posix = path.as_posix()
    return any(part in posix for part in SKIP_PARTS)


def decode_bytes(raw: bytes) -> tuple[str, str, bool]:
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw[3:].decode("utf-8"), "UTF-8 BOM", False
    try:
        return raw.decode("utf-8"), "UTF-8", False
    except UnicodeDecodeError:
        pass
    try:
        text = raw.decode("gbk")
        if count_cjk(text) > 0:
            return text, "GBK", True
    except UnicodeDecodeError:
        pass
    return raw.decode("utf-8", errors="replace"), "UTF-8(lossy)", False


def looks_like_mojibake(text: str) -> bool:
    if "锟斤拷" in text:
        return True
    sample = text[:20000]
    latin1 = sum(1 for ch in sample if "\u00c0" <= ch <= "\u00ff")
    cjk = count_cjk(sample)
    return latin1 >= 8 and latin1 > cjk


def try_mojibake(text: str) -> str:
    best, best_cjk = text, count_cjk(text)
    for wrong in ("latin-1", "cp1252", "gbk"):
        try:
            candidate = text.encode(wrong).decode("utf-8")
        except (UnicodeEncodeError, UnicodeDecodeError):
            continue
        if "\ufffd" in candidate:
            continue
        cjk = count_cjk(candidate)
        if cjk > best_cjk + 2:
            best, best_cjk = candidate, cjk
    return best


def has_replacement_question_run(line: str) -> bool:
    """Consecutive ?? / ??? counts as lost text. A single ? does not."""
    masked = re.sub(r"\?\.", "", line)
    masked = re.sub(r'(?<=[\w)\]])(\s*)\?\?(\s*)(?=[\w(!])', r'\1\2', masked)
    return re.search(r"\?{2,}", masked) is not None


def line_looks_garbled(line: str) -> bool:
    t = line.lstrip()
    is_comment = t.startswith("//") or t.startswith("*")
    is_doc_string = bool(GARBLED_ASSIGN_RE.search(line))
    if not is_comment and not is_doc_string:
        return False
    return has_replacement_question_run(line)


def git_history(root: Path, rel: str) -> list[list[str]]:
    try:
        log = subprocess.check_output(
            ["git", "log", "-n", str(GIT_DEPTH), "--pretty=format:%H", "--", rel],
            cwd=root,
            stderr=subprocess.DEVNULL,
        )
    except (subprocess.CalledProcessError, FileNotFoundError):
        return []
    history: list[list[str]] = []
    for commit in log.decode("utf-8", errors="replace").splitlines():
        commit = commit.strip()
        if not commit:
            continue
        try:
            blob = subprocess.check_output(
                ["git", "show", f"{commit}:{rel}"],
                cwd=root,
                stderr=subprocess.DEVNULL,
            )
        except subprocess.CalledProcessError:
            continue
        text, _, _ = decode_bytes(blob)
        history.append(text.splitlines(keepends=True) or [text])
    return history


def restore_from_git(lines: list[str], history: list[list[str]]) -> bool:
    changed = False
    for i, line in enumerate(lines):
        if not line_looks_garbled(line):
            continue
        ending = ""
        body = line
        if body.endswith("\r\n"):
            body, ending = body[:-2], "\r\n"
        elif body.endswith("\n"):
            body, ending = body[:-1], "\n"
        sk = ascii_skeleton(body)
        if len(sk) < 8:
            continue
        best, best_cjk = None, count_cjk(body)
        for old_lines in history:
            start = max(0, i - 8)
            end = min(len(old_lines) - 1, i + 8)
            for j in range(start, end + 1):
                old = old_lines[j].rstrip("\r\n")
                if ascii_skeleton(old) != sk:
                    continue
                cjk = count_cjk(old)
                if cjk > best_cjk:
                    best, best_cjk = old, cjk
        if best:
            lines[i] = best + ending
            changed = True
    return changed


def process_file(root: Path, path: Path, apply: bool, git_cache: dict) -> str | None:
    raw = path.read_bytes()
    if not raw or len(raw) > MAX_BYTES:
        return None
    rel = path.relative_to(root).as_posix()
    text, enc_name, used_fallback = decode_bytes(raw)
    original = text
    changed = used_fallback
    if looks_like_mojibake(text):
        fixed = try_mojibake(text)
        if count_cjk(fixed) > count_cjk(text):
            text = fixed
            changed = True
    lines = text.splitlines(keepends=True) or [text]
    git_restored = False
    if any(line_looks_garbled(x) for x in lines):
        if rel not in git_cache:
            git_cache[rel] = git_history(root, rel)
        git_restored = restore_from_git(lines, git_cache[rel])
        changed = changed or git_restored
    new_text = "".join(lines)
    if changed and (new_text != original or used_fallback):
        if apply:
            path.write_text(new_text, encoding="utf-8-sig")
        kind = []
        if used_fallback:
            kind.append(f"decode {enc_name}->UTF-8")
        if git_restored:
            kind.append("git restore ???")
        if not kind:
            kind.append("mojibake")
        return f"FIX  {rel}  ({', '.join(kind)})"
    if any(line_looks_garbled(x) for x in lines):
        return f"NEED {rel}  (??? and git did not match)"
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Repair garbled Chinese comments in C#.")
    parser.add_argument("--apply", action="store_true", help="Write files. Default is dry-run.")
    parser.add_argument("--root", default=None, help="Repo root. Default: parent of Tools/")
    args = parser.parse_args()
    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parents[1]

    reports: list[str] = []
    git_cache: dict = {}
    scanned = 0
    for scan in SCAN_ROOTS:
        abs_root = root / scan
        if not abs_root.is_dir():
            continue
        for path in abs_root.rglob("*.cs"):
            if should_skip(path):
                continue
            scanned += 1
            msg = process_file(root, path, args.apply, git_cache)
            if msg:
                reports.append(msg)

    mode = "APPLY" if args.apply else "DRY-RUN"
    print(f"[{mode}] scanned {scanned} files, {len(reports)} findings")
    for line in reports:
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
