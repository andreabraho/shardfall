@echo off
setlocal enabledelayedexpansion

rem ---------------------------------------------------------------------------
rem  Opens the project in the Godot editor. Double-click this file.
rem  Press F5 inside the editor to run the game.
rem
rem  Resolves the REAL Godot executable rather than the winget shim — see the
rem  comment in play.bat for why the shim crashes.
rem ---------------------------------------------------------------------------

if defined GODOT_EXE (
    set "GODOT=%GODOT_EXE%"
    goto :check
)

set "PKGDIR=%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe"

rem The non-console build here: the editor has its own output panel.
for /d %%D in ("%PKGDIR%\Godot_v*_mono_win64") do (
    for %%F in ("%%D\Godot_v*_mono_win64.exe") do set "GODOT=%%F"
)

:check
if not defined GODOT goto :notfound
if not exist "%GODOT%" goto :notfound

echo Opening editor: %GODOT%
start "" "%GODOT%" --editor --path "%~dp0game"
exit /b 0

:notfound
echo.
echo  Could not find the Godot mono executable.
echo.
echo  Looked in:
echo    %PKGDIR%
echo.
echo  Fix: set GODOT_EXE to your Godot executable, then run this file again.
echo.
pause
exit /b 1
