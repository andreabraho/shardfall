@echo off
setlocal enabledelayedexpansion

rem ---------------------------------------------------------------------------
rem  Launches the game directly (no editor). Double-click this file.
rem
rem  Uses the console build of Godot on purpose, so a second window shows the
rem  log output ([content] loaded..., [nav] baked..., and any errors). That is
rem  what you want while testing; it is not what ships to players.
rem
rem  IMPORTANT: this resolves the REAL Godot executable, not the winget shim at
rem  WinGet\Links\godot.exe. Godot looks for its C# API assemblies next to the
rem  executable, and the shim folder has no GodotSharp\ beside it, so launching
rem  through the shim fails with ".NET: Assemblies not found" and crashes.
rem ---------------------------------------------------------------------------

if defined GODOT_EXE (
    set "GODOT=%GODOT_EXE%"
    goto :check
)

set "PKGDIR=%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe"

for /d %%D in ("%PKGDIR%\Godot_v*_mono_win64") do (
    for %%F in ("%%D\Godot_v*_mono_win64_console.exe") do set "GODOT=%%F"
)

:check
if not defined GODOT goto :notfound
if not exist "%GODOT%" goto :notfound

rem Build first so the game can never run a stale assembly — otherwise a C#
rem change appears to do nothing, which is a miserable thing to debug.
echo Building C# ...
dotnet build "%~dp0game\Kiln.Game.csproj" --nologo -v q
if errorlevel 1 (
    echo.
    echo  BUILD FAILED - fix the errors above before running.
    pause
    exit /b 1
)

echo.
echo Launching: %GODOT%
echo.
"%GODOT%" --path "%~dp0game" %*
set "CODE=%ERRORLEVEL%"

if not "%CODE%"=="0" (
    echo.
    echo Game exited with code %CODE%.
    pause
)
exit /b %CODE%

:notfound
echo.
echo  Could not find the Godot mono executable.
echo.
echo  Looked in:
echo    %PKGDIR%
echo.
echo  Fix: set GODOT_EXE to your Godot executable, for example
echo    set GODOT_EXE=C:\tools\godot\Godot_v4.7.2-stable_mono_win64_console.exe
echo  then run this file again.
echo.
pause
exit /b 1
