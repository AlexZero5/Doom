@echo off
rem Запуск FunDoom в Windows Terminal (обходник системного терминала по умолчанию).
rem В Rider: Run - Edit Configurations - + - Shell Script:
rem   Script path: этот файл. Или просто запусти двойным кликом.

set "DOTNET=C:\Users\alex1\.dotnet\dotnet.exe"
set "GAMEDIR=%~dp0Doom"

start "" wt -d "%GAMEDIR%" -- "%DOTNET%" "bin\Debug\net10.0\Doom.dll"
