@echo off
cd /d "%~dp0"
start "" "http://127.0.0.1:17380"
node server.js
pause
