# PC / SIGNAL

**PC Insight & Troubleshooting — a native desktop app for Windows and Linux.**

A vibe-coded PC Insight & Troubleshoot project with a native C# Avalonia UI and a Python `psutil` probe. PC / SIGNAL shows live CPU, memory, storage, and network readings, runs read-only resource checks, opens selected system utilities, and exports a local report. It does not collect process names or IP addresses, and it does not make system changes.

## Download and install

Get the latest packages from [GitHub Releases](https://github.com/Tester12-SR-24/PC-Troubleshoot-Native/releases/latest).

### Windows x64

1. Download `PC-Troubleshoot-<version>-x64-Setup.exe`.
2. Run the installer and follow its prompts.
3. Launch **PC / SIGNAL** from the Start menu or the optional desktop shortcut.

### Linux x64 or ARM64

Choose `x64` for an Intel/AMD 64-bit PC and `arm64` for a 64-bit ARM computer.

**AppImage**

```bash
chmod +x PC-Troubleshoot-<version>-<arch>.AppImage
./PC-Troubleshoot-<version>-<arch>.AppImage
```

If your distribution cannot mount AppImages, use the `.tar.gz` package instead. The desktop session needs X11 or XWayland and the system libraries `libX11`, `libICE`, `libSM`, and `fontconfig`.

**tar.gz**

```bash
tar -xzf PC-Troubleshoot-<version>-<arch>.tar.gz
cd PC-Troubleshoot-<version>-<arch>
./PC-Troubleshoot
```

Release packages include the .NET runtime and Python probe; no SDK, Node.js, or Python installation is needed.

## Run from source

Install the .NET 10 SDK and Python 3.10 or later. Avalonia packages restore from NuGet on the first build.

### Windows

```powershell
py -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -r python\requirements.txt
dotnet run --project native\PC.Troubleshoot.Desktop\PC.Troubleshoot.Desktop.csproj
```

### Linux

Install the desktop dependencies (Debian/Ubuntu):

```bash
sudo apt install libx11-6 libice6 libsm6 libfontconfig1
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r python/requirements.txt
dotnet run --project native/PC.Troubleshoot.Desktop/PC.Troubleshoot.Desktop.csproj
```

The app starts the probe itself and communicates through a private stdin/stdout pipe. It does not open a local HTTP port.

## Build packages locally

Build on the target operating system and architecture so native Avalonia and AppImage components match.

**Windows x64** (requires Inno Setup 6):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1
```

**Linux x64 or ARM64** (requires .NET 10, Python 3, and `curl`):

```bash
bash scripts/package-linux.sh
```

Outputs are placed in `dist/`: a Windows installer, or Linux AppImage and `.tar.gz` packages.

## Publish a release

1. Set the version in `native/PC.Troubleshoot.Desktop/PC.Troubleshoot.Desktop.csproj` using `major.minor.patch` (for example, `0.2.0`).
2. Commit and push the version change.
3. Create and push the matching tag:

   ```bash
   git tag v0.2.0
   git push origin v0.2.0
   ```

The **Build and release** workflow packages Windows x64, Linux x64, and Linux ARM64, then attaches the installer files to a GitHub Release. It rejects tags that do not exactly match the project SemVer version.

**Check for updates** compares the installed version with the latest stable GitHub Release and opens that release page when a newer version is available.

## License

This project is released under [WTFPL](LICENSE).
