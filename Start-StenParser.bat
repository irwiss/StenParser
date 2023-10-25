@echo off

rem Starts the server, waits 5 seconds and starts a browser
rem This script assumes port is set to 8080

cd /d win-x64\publish
start StenParser.exe

timeout /t 5
start http://localhost:8080/
