$ErrorActionPreference = 'Stop'
$rid = 'win-x64'
$project = 'native\PC.Troubleshoot.Desktop\PC.Troubleshoot.Desktop.csproj'
$buildRoot = Join-Path (Get-Location) 'build'
$runtime = Join-Path $buildRoot "runtime\python\$rid"
$publish = Join-Path $buildRoot "publish\$rid"
$venv = Join-Path $buildRoot 'venv'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version

if (-not $version) { throw 'Could not read the desktop app version from its project file.' }
if (-not (Test-Path $venv)) { py -m venv $venv }
if ($LASTEXITCODE -ne 0) { throw 'Could not create the packaging Python environment.' }
$python = Join-Path $venv 'Scripts\python.exe'
& $python -m pip install --upgrade pip
& $python -m pip install -r python\requirements.txt pyinstaller
if ($LASTEXITCODE -ne 0) { throw 'Could not install Python packaging dependencies.' }

dotnet publish $project --configuration Release --runtime $rid --self-contained true --output $publish
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the native desktop application.' }
& $python -m PyInstaller --clean --onefile --name probe --distpath $runtime `
  --workpath (Join-Path $buildRoot "pyinstaller\$rid") python\probe.py
if ($LASTEXITCODE -ne 0) { throw 'Could not bundle the Python system probe.' }

$runtimeTarget = Join-Path $publish "runtime\python\$rid"
New-Item -ItemType Directory -Force -Path $runtimeTarget | Out-Null
Copy-Item (Join-Path $runtime 'probe.exe') (Join-Path $runtimeTarget 'probe.exe') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $publish 'runtime') | Out-Null
$repository = if ($env:PC_RELEASE_REPOSITORY) { $env:PC_RELEASE_REPOSITORY } else { '' }
[System.IO.File]::WriteAllText((Join-Path $publish 'runtime\repository.txt'), $repository)
Copy-Item LICENSE $publish -Force
New-Item -ItemType Directory -Force -Path (Join-Path (Get-Location) 'dist') | Out-Null

$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
  $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
  if (-not (Test-Path $iscc)) {
    choco install innosetup --yes --no-progress
    if ($LASTEXITCODE -ne 0) { throw 'Could not install Inno Setup.' }
  }
}
$env:PC_APP_VERSION = $version
$env:PC_PUBLISH_DIR = $publish
& $iscc 'scripts\windows-installer.iss'
if ($LASTEXITCODE -ne 0) { throw 'Could not create the Windows installer.' }
