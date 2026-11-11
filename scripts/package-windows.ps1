$ErrorActionPreference = 'Stop'
$architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$rid = "win-$architecture"
$runtime = Join-Path (Get-Location) 'build\runtime'
$venv = Join-Path (Get-Location) 'build\venv'

$basePython = if ($env:PC_TROUBLESHOOT_PYTHON) { $env:PC_TROUBLESHOOT_PYTHON } else { 'py' }
if ($basePython -eq 'py') { & py -m venv $venv } else { & $basePython -m venv $venv }
if ($LASTEXITCODE -ne 0) { throw 'Could not create the packaging Python environment.' }
$python = Join-Path $venv 'Scripts\python.exe'
& $python -m pip install -r python\requirements.txt pyinstaller
if ($LASTEXITCODE -ne 0) { throw 'Could not install Python packaging dependencies.' }
dotnet publish service\PcTroubleshoot.Service.csproj `
  --configuration Release --runtime $rid --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  --output (Join-Path $runtime "service\$rid")
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the self-contained C# service.' }
& $python -m PyInstaller --clean --onefile --name probe `
  --distpath (Join-Path $runtime "python\$rid") `
  --workpath (Join-Path (Get-Location) "build\pyinstaller\$rid") python\probe.py
if ($LASTEXITCODE -ne 0) { throw 'Could not bundle the Python probe.' }
$releaseRepository = if ($env:PC_RELEASE_REPOSITORY) { $env:PC_RELEASE_REPOSITORY } else { '' }
[System.IO.File]::WriteAllText((Join-Path $runtime 'repository.txt'), $releaseRepository)
npx electron-builder --win nsis --$architecture
if ($LASTEXITCODE -ne 0) { throw 'Could not build the Windows installer.' }
