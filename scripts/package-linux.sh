#!/usr/bin/env bash
set -euo pipefail

case "$(uname -m)" in
  x86_64) rid="linux-x64" ;;
  aarch64|arm64) rid="linux-arm64" ;;
  *) echo "Unsupported build architecture: $(uname -m). Use x86_64 or aarch64." >&2; exit 1 ;;
esac

runtime="build/runtime"
python3 -m venv build/venv
build/venv/bin/python -m pip install -r python/requirements.txt pyinstaller
dotnet publish service/PcTroubleshoot.Service.csproj \
  --configuration Release --runtime "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  --output "$runtime/service/$rid"
build/venv/bin/python -m PyInstaller --clean --onefile --name probe \
  --distpath "$runtime/python/$rid" --workpath "build/pyinstaller/$rid" python/probe.py
printf '%s' "${PC_RELEASE_REPOSITORY:-}" > "$runtime/repository.txt"
npx electron-builder --linux AppImage tar.gz
