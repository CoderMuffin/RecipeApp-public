const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('electronAPI', {
    download: function() { ipcRenderer.send('download', ...arguments) },
    upload: () => ipcRenderer.send('upload', "")
});
