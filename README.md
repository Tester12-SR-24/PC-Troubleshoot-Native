# PC Insight & Troubleshoot

A vibe-coded PC Insight & Troubleshoot project. It combines an Electron UI, an ASP.NET Core service in C#, and a Python `psutil` probe.

The desktop app provides live system readings, straightforward resource checks, quick links to local troubleshooting tools, and a report export. Monitoring stays on this computer; process names and IP addresses are not collected.

The project is licensed under [WTFPL](LICENSE).

## Install a release

Download the package that matches your operating system and processor from the project’s **Releases** page.

### Windows

1. Download `PC-Troubleshoot-<version>-x64-Setup.exe`.
2. Open the installer and follow the prompts.
3. Start **PC / SIGNAL** from the Start menu or desktop shortcut.

The installer includes the C# service and Python probe. Development tools are not needed to run a release.

### Linux x64 or ARM64

Choose an `x64` build for a typical Intel or AMD PC. Choose `arm64` for a 64-bit ARM computer.

**AppImage**

```bash
chmod +x PC-Troubleshoot-<version>-<arch>.AppImage
./PC-Troubleshoot-<version>-<arch>.AppImage
```

**tar.gz**

```bash
tar -xzf PC-Troubleshoot-<version>-<arch>.tar.gz
cd PC-Troubleshoot-<version>-<arch>
./pc-troubleshoot
```

The archive contains the desktop app and its local service and probe. No .NET SDK or Python installation is required for release builds.

## Run from source

Install Node.js 22+, the .NET 8 SDK, and Python 3.10+.

### Windows

```powershell
npm ci
py -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -r python\requirements.txt
npm start
```

### Linux

```bash
npm ci
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r python/requirements.txt
PC_TROUBLESHOOT_PYTHON="$PWD/.venv/bin/python" npm start
```

Linux quick actions use available desktop utilities (GNOME, KDE, XFCE, or MATE system monitor; GNOME Disks and Settings). Resource monitoring works without those optional utilities.

## Build installers locally

Install Node.js, Python 3.10+, and the .NET 8 SDK, then run `npm ci`. The packaging script creates an isolated Python environment and bundles the probe. Build on the target operating system and architecture so its native components match.

```bash
npm ci
npm run package:linux
```

On Linux, `package:linux` creates both `.AppImage` and `.tar.gz` packages for the host architecture. Run it on an x64 Linux host for x64 outputs and an ARM64 Linux host for ARM64 outputs.

On Windows, run `npm run package:windows` in PowerShell to create the NSIS installer for the host architecture.

## Publish a GitHub release

Push a version tag to a GitHub repository with Actions enabled:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The release workflow builds a Windows x64 installer, Linux x64 AppImage and tar.gz packages, and Linux ARM64 AppImage and tar.gz packages, then attaches them to a GitHub Release. You can also run **Build and release** manually from the Actions tab to build downloadable artifacts without publishing a release.

Release builds embed the repository name from GitHub Actions. To check updates in a local development build, set `PC_RELEASE_REPOSITORY` to `owner/repository` before starting the app. **Check for updates** compares the installed SemVer version with the latest stable GitHub Release and opens that release page when a newer version is available.

Versions use `major.minor.patch` (for example, `0.1.0`). Release tags must be `v` followed by that exact version; build metadata such as `+123` is rejected.

## Project layout

- `electron/`: desktop lifecycle, secure preload bridge, and packaged-service startup.
- `service/`: local ASP.NET Core API and platform-specific allowlisted tool launcher.
- `python/`: system sampler and diagnostics.
- `ui/`: Signal Monitor desktop interface.
- `scripts/`: native packaging commands for Windows and Linux.
- `.github/workflows/release.yml`: tagged release build and upload.

