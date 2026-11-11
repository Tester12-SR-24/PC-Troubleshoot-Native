const { app, BrowserWindow, ipcMain, shell } = require('electron');
const { spawn } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');

const apiBase = 'http://127.0.0.1:47830';
let backend;
let windowRef;
let quitting = false;

function startBackend() {
  const root = app.isPackaged ? path.join(process.resourcesPath, 'runtime') : app.getAppPath();
  const rid = `${process.platform}-${process.arch}`;
  const executable = path.join(root, 'service', rid, process.platform === 'win32' ? 'PcTroubleshoot.Service.exe' : 'PcTroubleshoot.Service');
  const command = app.isPackaged ? executable : 'dotnet';
  const args = app.isPackaged ? [] : ['run', '--project', path.join(root, 'service', 'PcTroubleshoot.Service.csproj'), '--no-launch-profile'];
  backend = spawn(command, args, {
    cwd: app.getAppPath(),
    windowsHide: true,
    stdio: 'ignore',
    env: { ...process.env, ASPNETCORE_URLS: apiBase, PC_TROUBLESHOOT_ROOT: root }
  });
  backend.on('error', (error) => console.error('Could not start the local diagnostic service:', error));
}

async function api(pathname, options = {}) {
  const response = await fetch(`${apiBase}${pathname}`, {
    ...options,
    headers: { 'content-type': 'application/json', ...(options.headers || {}) }
  });
  const body = await response.json();
  if (!response.ok) throw new Error(body.detail || body.title || body.error || `Diagnostic service returned ${response.status}`);
  return body;
}

function getReleaseRepository() {
  const packagedConfig = path.join(process.resourcesPath, 'runtime', 'repository.txt');
  const configured = process.env.PC_RELEASE_REPOSITORY || (app.isPackaged && fs.existsSync(packagedConfig)
    ? fs.readFileSync(packagedConfig, 'utf8').trim()
    : '');
  const repo = configured.replace(/^https:\/\/github\.com\//i, '').replace(/\.git$/i, '').replace(/\/$/, '');
  if (!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repo)) {
    throw new Error('This build has no GitHub repository configured for release checks.');
  }
  return repo;
}

function parseVersion(value) {
  const match = /^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?$/.exec(value);
  if (!match) throw new Error(`Release tag “${value}” is not a supported SemVer version (major.minor.patch).`);
  return { major: Number(match[1]), minor: Number(match[2]), patch: Number(match[3]), prerelease: match[4] || '' };
}

function compareVersions(left, right) {
  for (const key of ['major', 'minor', 'patch']) {
    if (left[key] !== right[key]) return left[key] > right[key] ? 1 : -1;
  }
  if (!left.prerelease && right.prerelease) return 1;
  if (left.prerelease && !right.prerelease) return -1;
  return 0;
}

async function checkLatestRelease() {
  const repository = getReleaseRepository();
  const response = await fetch(`https://api.github.com/repos/${repository}/releases/latest`, {
    headers: { accept: 'application/vnd.github+json', 'user-agent': 'PC-Signal-Update-Check' },
    signal: AbortSignal.timeout(15000)
  });
  if (!response.ok) throw new Error(response.status === 404
    ? 'No published GitHub release was found for this repository.'
    : `GitHub release check failed (${response.status}).`);
  const release = await response.json();
  const currentVersion = app.getVersion();
  const latestVersion = release.tag_name || '';
  const available = compareVersions(parseVersion(latestVersion), parseVersion(currentVersion)) > 0;
  return {
    available,
    currentVersion,
    latestVersion: latestVersion.replace(/^v/, ''),
    releaseUrl: release.html_url,
    releaseName: release.name || latestVersion,
    releaseNotes: release.body || ''
  };
}

function createWindow() {
  windowRef = new BrowserWindow({
    width: 1440,
    height: 960,
    minWidth: 1080,
    minHeight: 720,
    backgroundColor: '#111923',
    title: 'PC / SIGNAL',
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });
  windowRef.loadFile(path.join(__dirname, '..', 'ui', 'index.html'));
  windowRef.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('https://')) shell.openExternal(url);
    return { action: 'deny' };
  });
}

app.whenReady().then(() => {
  startBackend();
  ipcMain.handle('snapshot', () => api('/api/snapshot'));
  ipcMain.handle('diagnostics', () => api('/api/diagnostics'));
  ipcMain.handle('run-tool', (_event, name) => api('/api/tools', {
    method: 'POST', body: JSON.stringify({ name })
  }));
  ipcMain.handle('export-report', () => api('/api/report'));
  ipcMain.handle('check-updates', () => checkLatestRelease());
  ipcMain.handle('open-release', (_event, url) => {
    if (typeof url !== 'string' || !url.startsWith('https://github.com/')) throw new Error('Invalid release URL.');
    return shell.openExternal(url);
  });
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});

app.on('before-quit', (event) => {
  if (backend && !backend.killed && !quitting) {
    event.preventDefault();
    quitting = true;
    fetch(`${apiBase}/api/shutdown`, { method: 'POST' })
      .catch(() => {})
      .finally(() => { setTimeout(() => { if (!backend.killed) backend.kill(); app.quit(); }, 800); });
  }
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
