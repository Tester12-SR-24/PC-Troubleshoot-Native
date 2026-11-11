const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('pcApi', {
  snapshot: () => ipcRenderer.invoke('snapshot'),
  diagnostics: () => ipcRenderer.invoke('diagnostics'),
  runTool: (name) => ipcRenderer.invoke('run-tool', name),
  exportReport: () => ipcRenderer.invoke('export-report'),
  checkUpdates: () => ipcRenderer.invoke('check-updates'),
  openRelease: (url) => ipcRenderer.invoke('open-release', url)
});
