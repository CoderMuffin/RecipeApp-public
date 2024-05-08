@echo off
pushd %~dp0
rem call yank
electron-packager . Recipes --platform=win32 --arch=x64 --overwrite --icon icon.ico && 7z a -tzip recipes.zip Recipes-win32-x64 && Recipes-win32-x64\Recipes.exe
popd
