#!/usr/bin/env bash
# Publica DM Companion para escritorio.
# Uso: scripts/publish-desktop.sh <win-x64|linux-x64> <versión>
#   win-x64   → dist/win-x64/app/ y dist/dm-companion-<versión>-win-x64.zip (el instalador lo compila iscc en Windows)
#   linux-x64 → dist/linux-x64/app/ y dist/dm-companion-<versión>-x86_64.AppImage (necesita appimagetool)
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:?Falta la plataforma: win-x64 o linux-x64}"
version="${2:?Falta la versión, p. ej. 0.2.0}"
case "$rid" in win-x64|linux-x64) ;; *) echo "Plataforma no soportada: $rid" >&2; exit 2 ;; esac

out="dist/$rid/app"
rm -rf "dist/$rid"
dotnet publish src/DmCompanion.Desktop -c Release -r "$rid" --self-contained \
  -p:Version="$version" -p:NuGetAudit=false -o "$out"
rm -f "$out"/aspnetcorev2_inprocess.dll "$out"/web.config "$out"/*.pdb
cp -r samples/vault-demo "$out/ejemplo"

if [ "$rid" = win-x64 ]; then
  python3 scripts/guide-to-text.py docs/guia-version-pc.md "$out/LEEME.txt"
  zip_path="dist/dm-companion-$version-win-x64.zip"
  rm -f "$zip_path"
  # Los ficheros van en la raíz del zip: con «Extraer todo» Windows crea la carpeta con el nombre del zip.
  python3 - "$out" "$zip_path" <<'PY'
import os, sys, zipfile
root, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as zf:
    for folder, _, files in os.walk(root):
        for name in sorted(files):
            path = os.path.join(folder, name)
            zf.write(path, os.path.relpath(path, root))
PY
  echo "Listo: $zip_path"
else
  appdir="dist/$rid/AppDir"
  mkdir -p "$appdir/usr/lib/dm-companion"
  cp -r "$out"/. "$appdir/usr/lib/dm-companion/"
  install -m 755 packaging/linux/AppRun "$appdir/AppRun"
  cp packaging/linux/dm-companion.desktop "$appdir/"
  cp src/DmCompanion.Desktop/Assets/dm-companion.png "$appdir/dm-companion.png"
  image="dist/dm-companion-$version-x86_64.AppImage"
  # --appimage-extract-and-run evita depender de FUSE (appimagetool es a su vez un AppImage).
  ARCH=x86_64 "${APPIMAGETOOL:-appimagetool}" --appimage-extract-and-run "$appdir" "$image"
  echo "Listo: $image"
fi
