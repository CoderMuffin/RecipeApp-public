@echo off
%SYSTEMROOT%\System32\inetsrv\appcmd stop apppool /apppool.name:"recipes.rectanglered.com"
cd %~dp0
dotnet publish -c Release -o C:\Working\recipes.rectanglered.com\api && del C:\Working\recipes.rectanglered.com\api\web.config
%SYSTEMROOT%\System32\inetsrv\appcmd start apppool /apppool.name:"recipes.rectanglered.com"
pause
