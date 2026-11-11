#!/usr/bin/env bash
set -euo pipefail

case "$(uname -m)" in
  x86_64) rid='linux-x64'; arch='x64'; appimage_arch='x86_64' ;;
  aarch64|arm64) rid='linux-arm64'; arch='arm64'; appimage_arch='aarch64' ;;
  *) echo "Unsupported build architecture: $(uname -m). Use x86_64 or aarch64." >&2; exit 1 ;;
esac

project='native/PC.Troubleshoot.Desktop/PC.Troubleshoot.Desktop.csproj'
version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$project" | head -n 1)"
if [[ ! "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid SemVer version in $project: $version" >&2
  exit 1
fi

build_root='build'
runtime="$build_root/runtime/python/$rid"
publish="$build_root/publish/$rid"
python3 -m venv "$build_root/venv"
"$build_root/venv/bin/python" -m pip install --upgrade pip
"$build_root/venv/bin/python" -m pip install -r python/requirements.txt pyinstaller
dotnet publish "$project" --configuration Release --runtime "$rid" --self-contained true --output "$publish"
"$build_root/venv/bin/python" -m PyInstaller --clean --onefile --name probe \
  --distpath "$runtime" --workpath "$build_root/pyinstaller/$rid" python/probe.py

mkdir -p "$publish/runtime/python/$rid" dist
cp "$runtime/probe" "$publish/runtime/python/$rid/probe"
printf '%s' "${PC_RELEASE_REPOSITORY:-}" > "$publish/runtime/repository.txt"
cp LICENSE "$publish/LICENSE"

bundle="PC-Troubleshoot-$version-$arch"
bundle_dir="dist/$bundle"
rm -rf "$bundle_dir" "$build_root/appdir/$rid"
mkdir -p "$bundle_dir" "$build_root/appdir/$rid"
cp -a "$publish/." "$bundle_dir/"
tar -czf "dist/$bundle.tar.gz" -C dist "$bundle"

appdir="$build_root/appdir/$rid"
cp -a "$publish/." "$appdir/"
cp assets/icon.svg "$appdir/pc-troubleshoot.svg"
ln -sf pc-troubleshoot.svg "$appdir/.DirIcon"
cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/PC-Troubleshoot" "$@"
APPRUN
chmod +x "$appdir/AppRun" "$appdir/PC-Troubleshoot"
mkdir -p "$appdir/usr/share/applications"
cat > "$appdir/usr/share/applications/pc-troubleshoot.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=PC / SIGNAL
Comment=Local PC monitoring and troubleshooting
Exec=AppRun
Icon=pc-troubleshoot
Categories=System;Monitor;
Terminal=false
DESKTOP
cp "$appdir/usr/share/applications/pc-troubleshoot.desktop" "$appdir/pc-troubleshoot.desktop"

tool="$build_root/appimagetool-$appimage_arch.AppImage"
curl --fail --location --retry 3 \
  "https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-$appimage_arch.AppImage" \
  --output "$tool"
chmod +x "$tool"
APPIMAGE_EXTRACT_AND_RUN=1 ARCH="$appimage_arch" "$tool" "$appdir" "dist/PC-Troubleshoot-$version-$arch.AppImage"
