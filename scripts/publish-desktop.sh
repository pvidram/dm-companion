#!/usr/bin/env bash
# Publica DM Companion para Windows (x64) y genera dist/dm_companion_pc.zip.
set -euo pipefail
cd "$(dirname "$0")/.."
source ~/.dotnet-dm/env-dm-companion.sh
export TMPDIR="$HOME/.dotnet-dm/tmp"
mkdir -p "$TMPDIR"
out=dist/dm-companion-pc
rm -rf "$out" dist/dm_companion_pc.zip
dotnet publish src/DmCompanion.Desktop -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:NuGetAudit=false -o "$out"
rm -f "$out"/aspnetcorev2_inprocess.dll "$out"/web.config "$out"/*.pdb
cp -r samples/vault-demo "$out/ejemplo"
# El léeme es texto plano (el Bloc de notas no pinta Markdown), generado desde la guía.
python3 scripts/guide-to-text.py docs/guia-version-pc.md "$out/LEEME.txt"
# Los ficheros van en la raíz del zip: con «Extraer todo» Windows crea la carpeta con el nombre del zip.
# Sin `zip` instalado se usa Python (mismo resultado).
if command -v zip >/dev/null; then
  (cd "$out" && zip -qr ../dm_companion_pc.zip .)
else
  python3 - "$out" dist/dm_companion_pc.zip <<'PY'
import os, sys, zipfile
root, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as zf:
    for folder, _, files in os.walk(root):
        for name in sorted(files):
            path = os.path.join(folder, name)
            zf.write(path, os.path.relpath(path, root))
PY
fi
echo "Listo: dist/dm_companion_pc.zip"
