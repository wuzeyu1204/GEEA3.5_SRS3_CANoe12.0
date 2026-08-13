@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul 2>&1
title SRS3 E2E Panel - One Click Setup

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%.."
set "PROJECT_FILE=%SCRIPT_DIR%SRS3E2EPanel\SRS3E2EPanel.csproj"
set "BUILD_DLL=%SCRIPT_DIR%SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.dll"
set "DIST_DIR=%SCRIPT_DIR%dist\SRS3_E2E_Panel"
set "PACKAGE_DLL=%DIST_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll"
set "PANEL_TEMPLATE=%REPO_ROOT%\Panels\SRS3 E2E Test Console - Manual Import.xvp"
set "SYSVAR_IL=%REPO_ROOT%\SyaVar\01_CANoe_IL_SystemVariables.xml"
set "SYSVAR_E2E=%REPO_ROOT%\SyaVar\10_SRS3_E2E_Core_SystemVariables.xml"

echo.
echo ============================================================
echo   SRS3 E2E Panel - One Click Build, Check and Install
echo ============================================================
echo   This script NEVER starts CANoe or edits CFG/CAPL/SysVar files.
echo.

call :CheckProcesses
if errorlevel 1 goto :Failure
call :FindCANoeRoot
if errorlevel 1 goto :Failure
call :FindMSBuild
if errorlevel 1 goto :Failure
call :CleanWorkspace
if errorlevel 1 goto :Failure
call :BuildAndPackage
if errorlevel 1 goto :Failure
call :StaticAudit
if errorlevel 1 goto :Failure
call :FinalizeCleanup
if errorlevel 1 goto :Failure

if /I "%~1"=="/check" (
    echo [CHECK] Clean, build, package and static audit passed. Installation was skipped.
    goto :Success
)

fltmc >nul 2>&1
if errorlevel 1 (
    echo [INFO] Administrator permission is required only for the final DLL copy.
    echo [INFO] A Windows UAC prompt will open. Choose Yes to continue.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
      "$p=Start-Process -FilePath '%~f0' -ArgumentList '/install-only' -Verb RunAs -PassThru -Wait; exit $p.ExitCode"
    if errorlevel 1 goto :Failure
    echo [OK] Setup completed.
    goto :Success
)

call :InstallPackage
if errorlevel 1 goto :Failure
goto :Success

:CheckProcesses
for %%P in (PanelDesigner.exe CANoe64.exe CANoe32.exe CANoe.exe) do (
    tasklist /FI "IMAGENAME eq %%P" 2>nul | find /I "%%P" >nul
    if not errorlevel 1 (
        echo [STOP] %%P is running.
        echo        Close CANoe and Panel Designer, then double-click this script again.
        exit /b 1
    )
)
exit /b 0

:FindCANoeRoot
set "CANOE_ROOT="
if defined CANOE12_ROOT if exist "%CANOE12_ROOT%" set "CANOE_ROOT=%CANOE12_ROOT%"
if not defined CANOE_ROOT for /f "tokens=2,*" %%A in ('reg query "HKLM\SOFTWARE\Vector\CANoe\12.0" /v Path 2^>nul ^| find /I "REG_SZ"') do set "CANOE_ROOT=%%B"
if not defined CANOE_ROOT if defined ProgramW6432 for /d %%D in ("%ProgramW6432%\Vector CANoe 12.0*") do if exist "%%~fD" set "CANOE_ROOT=%%~fD"
if not defined CANOE_ROOT for /d %%D in ("%ProgramFiles%\Vector CANoe 12.0*") do if exist "%%~fD" set "CANOE_ROOT=%%~fD"
if not defined CANOE_ROOT if defined ProgramFiles(x86) for /d %%D in ("%ProgramFiles(x86)%\Vector CANoe 12.0*") do if exist "%%~fD" set "CANOE_ROOT=%%~fD"
if not defined CANOE_ROOT (
    echo [ERROR] CANoe 12 was not found. Set CANOE12_ROOT to its installation directory.
    exit /b 1
)
set "PANEL_API="
if exist "%CANOE_ROOT%\Exec64\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll" set "PANEL_API=%CANOE_ROOT%\Exec64\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll"
if not defined PANEL_API if exist "%CANOE_ROOT%\Exec32\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll" set "PANEL_API=%CANOE_ROOT%\Exec32\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll"
if not defined PANEL_API (
    echo [ERROR] Vector.PanelControlPlugin 1.2.0.0 was not found below %CANOE_ROOT%.
    exit /b 1
)
echo [OK] CANoe: %CANOE_ROOT%
exit /b 0

:FindMSBuild
set "MSBUILD_EXE="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" set "MSBUILD_EXE=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not defined MSBUILD_EXE if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" set "MSBUILD_EXE=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if not defined MSBUILD_EXE (
    echo [ERROR] .NET Framework MSBuild was not found.
    exit /b 1
)
exit /b 0

:CleanWorkspace
echo [1/4] Cleaning generated and legacy workspace files...
for %%D in ("%REPO_ROOT%\work" "%SCRIPT_DIR%SRS3E2EPanel\bin" "%SCRIPT_DIR%SRS3E2EPanel\obj") do (
    if exist "%%~fD" rmdir /S /Q "%%~fD"
    if exist "%%~fD" (
        echo [ERROR] Could not clean %%~fD
        exit /b 1
    )
)
if exist "%REPO_ROOT%\Reports" for %%F in ("%REPO_ROOT%\Reports\*") do if /I not "%%~nxF"==".gitkeep" del /F /Q "%%~fF" >nul 2>&1
exit /b 0

:BuildAndPackage
echo [2/4] Building the Panel plugin...
"%MSBUILD_EXE%" "%PROJECT_FILE%" /t:Rebuild /p:Configuration=Release /p:CanoePanelPluginPath="%PANEL_API%" /v:minimal
if errorlevel 1 exit /b 1
if not exist "%BUILD_DLL%" (
    echo [ERROR] Build did not produce %BUILD_DLL%
    exit /b 1
)
if exist "%DIST_DIR%" rmdir /S /Q "%DIST_DIR%"
if exist "%DIST_DIR%" exit /b 1
mkdir "%DIST_DIR%" || exit /b 1
copy /B /Y "%BUILD_DLL%" "%PACKAGE_DLL%" >nul || exit /b 1
if exist "%SCRIPT_DIR%SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.pdb" copy /B /Y "%SCRIPT_DIR%SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.pdb" "%DIST_DIR%\SRS3_E2E_PanelControl_1_2_0_0.pdb" >nul
copy /Y "%PANEL_TEMPLATE%" "%DIST_DIR%\SRS3 E2E Test Console - Manual Import.xvp" >nul || exit /b 1
copy /Y "%SYSVAR_IL%" "%DIST_DIR%\01_CANoe_IL_SystemVariables.xml" >nul || exit /b 1
copy /Y "%SYSVAR_E2E%" "%DIST_DIR%\10_SRS3_E2E_Core_SystemVariables.xml" >nul || exit /b 1
copy /Y "%SCRIPT_DIR%README.md" "%DIST_DIR%\README.md" >nul || exit /b 1
copy /Y "%SCRIPT_DIR%Bridge_Contract.md" "%DIST_DIR%\Bridge_Contract.md" >nul || exit /b 1
exit /b 0

:StaticAudit
echo [3/4] Running offline static checks...
findstr /C:"E2EConsoleControl" "%PANEL_TEMPLATE%" >nul || exit /b 1
findstr /C:"PanelBridge" "%PANEL_TEMPLATE%" >nul || exit /b 1
findstr /C:"arrayLength=" "%SYSVAR_E2E%" | findstr /C:"320" >nul || exit /b 1
for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -Command "(Get-Item -LiteralPath '%PACKAGE_DLL%').VersionInfo.FileVersion"`) do set "PLUGIN_VERSION=%%V"
for /f "usebackq delims=" %%H in (`powershell.exe -NoProfile -Command "(Get-FileHash -Algorithm SHA256 -LiteralPath '%PACKAGE_DLL%').Hash"`) do set "PLUGIN_HASH=%%H"
if not defined PLUGIN_VERSION exit /b 1
if not defined PLUGIN_HASH exit /b 1
(
    echo FileVersion=!PLUGIN_VERSION!
    echo SHA256=!PLUGIN_HASH!
    echo PanelApi=%PANEL_API%
) > "%DIST_DIR%\BUILD_INFO.txt"
echo [OK] Version !PLUGIN_VERSION!, SHA256 !PLUGIN_HASH!
exit /b 0

:FinalizeCleanup
for %%D in ("%SCRIPT_DIR%SRS3E2EPanel\bin" "%SCRIPT_DIR%SRS3E2EPanel\obj") do (
    if exist "%%~fD" rmdir /S /Q "%%~fD"
    if exist "%%~fD" exit /b 1
)
exit /b 0

:InstallPackage
echo [4/4] Removing legacy copies and installing one verified DLL...
set "COPIED=0"
call :InstallOne "%CANOE_ROOT%\Exec64\ControlLibraries" || exit /b 1
call :InstallOne "%CANOE_ROOT%\Exec32\ControlLibraries" || exit /b 1
if "!COPIED!"=="0" (
    echo [ERROR] No CANoe ControlLibraries directory was found.
    exit /b 1
)
exit /b 0

:InstallOne
set "TARGET_DIR=%~1"
if not exist "%TARGET_DIR%" exit /b 0
for %%F in ("%TARGET_DIR%\SRS3_E2E_PanelControl*.dll" "%TARGET_DIR%\SRS3_E2E_PanelControl*.dll.bak") do if exist "%%~fF" del /F /Q "%%~fF"
copy /B /Y "%PACKAGE_DLL%" "%TARGET_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll" >nul || exit /b 1
fc /B "%PACKAGE_DLL%" "%TARGET_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll" >nul || exit /b 1
set /a COPIED+=1
echo [OK] %TARGET_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll
exit /b 0

:Success
echo.
if /I "%~1"=="/check" (
    echo [PASS] Offline package is ready and statically verified.
) else (
    echo [PASS] One plugin DLL is installed and byte-verified.
)
echo [SAFE] No CANoe configuration, CAPL node or system-variable definition was edited.
echo [NEXT] Open CANoe yourself. Load only the packaged 01/10 SysVar files, then import the manual XVP.
if /I not "%~1"=="/install-only" pause
exit /b 0

:Failure
echo.
echo [FAILED] Nothing in the CANoe configuration was changed.
if /I not "%~1"=="/install-only" pause
exit /b 1
