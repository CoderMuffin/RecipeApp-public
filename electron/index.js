const { app, BrowserWindow, ipcMain, shell } = require('electron');
const path = require("path");
const fs = require("fs");
const mime = require("mime-types");
const randomHex = size => [...Array(size)].map(() => Math.floor(Math.random() * 16).toString(16)).join('');

var currentFile;
var win;

ipcMain.on("download", async function (event, contenttype, data, recipeID) {
    let dir = app.getPath("userData");
    var location = path.join(dir, "temporary_recipe_download_" + randomHex(8) + "." + mime.extension(contenttype));
    fs.writeFileSync(location, Buffer.from(data));
    currentFile = location;
    shell.openPath(location);
    fs.watchFile(location, () => {
        win.webContents.executeJavaScript(`onElectronUploadComplete('${location}', '${fs.readFileSync(location, {encoding: 'base64'})}', '${recipeID}')`);
    })
});

app.whenReady().then(() => {
    win = new BrowserWindow({
        width: 800,
        height: 600,
        webPreferences: {
            preload: path.join(__dirname, "preload.js")
        }
    });
    win.setMenu(null);
    win.loadFile("index.html");
});
