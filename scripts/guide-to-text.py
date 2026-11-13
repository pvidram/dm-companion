#!/usr/bin/env python3
"""Convierte la guía en Markdown a texto plano legible en el Bloc de notas (UTF-8 con BOM, saltos CRLF)."""
import re
import sys
import textwrap

WIDTH = 78
# Un nombre como dm-companion-AAAAMMDD.log no debe partirse por el guion.
NO_HYPHEN_BREAKS = {"break_on_hyphens": False, "break_long_words": False}


def clean(text: str) -> str:
    text = text.replace("**", "")
    return re.sub(r"`([^`]*)`", r"\1", text)


def convert(markdown: str) -> str:
    out: list[str] = []
    for raw in markdown.splitlines():
        line = clean(raw.rstrip())
        if line.startswith("# "):
            title = line[2:]
            out += [title, "=" * len(title), ""]
        elif line.startswith("## "):
            if out and out[-1] != "":
                out.append("")
            title = line[3:]
            out += [title, "-" * len(title), ""]
        elif (m := re.match(r"^(\s*(?:[-*]|\d+\.)\s+)(.*)$", line)):
            marker = m.group(1)
            out += textwrap.wrap(m.group(2), WIDTH, initial_indent=marker, subsequent_indent=" " * len(marker), **NO_HYPHEN_BREAKS) or [marker]
        elif line == "":
            if out and out[-1] != "":
                out.append("")
        else:
            out += textwrap.wrap(line, WIDTH, **NO_HYPHEN_BREAKS)
    return "\r\n".join(out).rstrip("\r\n") + "\r\n"


if __name__ == "__main__":
    with open(sys.argv[1], encoding="utf-8") as src, open(sys.argv[2], "w", encoding="utf-8-sig", newline="") as dst:
        dst.write(convert(src.read()))
