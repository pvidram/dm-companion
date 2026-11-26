#!/usr/bin/env python3
"""Genera los iconos del host de escritorio desde las capas del icono de Android (fondo + primer plano).

Uso: python3 scripts/make-desktop-icons.py   (necesita Node con npx; usa @resvg/resvg-js-cli)

Se compone un SVG con el fondo y el primer plano apilados, se rasteriza a cada tamaño y se empaquetan
los PNG en un .ico (formato con PNG incrustados, válido desde Windows Vista).
"""
import pathlib
import re
import struct
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "src/DmCompanion.App/Resources/AppIcon"
TARGET = ROOT / "src/DmCompanion.Desktop/Assets"
SIZES = [16, 24, 32, 48, 64, 128, 256]


def inner(svg: pathlib.Path) -> str:
    """Contenido del <svg> sin la cabecera ni el cierre."""
    text = svg.read_text(encoding="utf-8")
    return re.search(r"<svg[^>]*>(.*)</svg>", text, re.S).group(1)


def composite_svg() -> str:
    body = inner(SOURCE / "appicon.svg") + inner(SOURCE / "appiconfg.svg")
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="456" height="456" viewBox="0 0 456 456">{body}</svg>'


def render(svg: pathlib.Path, size: int, out: pathlib.Path) -> bytes:
    subprocess.run(
        ["npx", "--yes", "@resvg/resvg-js-cli", "--fit-width", str(size), str(svg), str(out)],
        check=True,
    )
    return out.read_bytes()


def build_ico(images: list[tuple[int, bytes]]) -> bytes:
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, data = b"", b""
    for size, png in images:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset + len(data))
        data += png
    return header + entries + data


def main() -> None:
    TARGET.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        tmp_dir = pathlib.Path(tmp)
        svg = tmp_dir / "icon.svg"
        svg.write_text(composite_svg(), encoding="utf-8")
        images = [(s, render(svg, s, tmp_dir / f"{s}.png")) for s in SIZES]
    (TARGET / "dm-companion.png").write_bytes(dict(images)[256])
    (TARGET / "dm-companion.ico").write_bytes(build_ico(images))


if __name__ == "__main__":
    main()
