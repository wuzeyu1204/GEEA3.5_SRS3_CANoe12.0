@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul 2>&1
title SRS3 E2E Panel Plugin Installer

set "SCRIPT_DIR=%~dp0"
set "PROJECT_FILE=%SCRIPT_DIR%SRS3E2EPanel\SRS3E2EPanel.csproj"
set "PLUGIN_DLL=%SCRIPT_DIR%SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.dll"
set "CHECK_ONLY=0"
set "ELEVATED=0"
set "COPIED=0"

if /I "%~1"=="/check" set "CHECK_ONLY=1"
if /I "%~1"=="/elevated" set "ELEVATED=1"

echo.
echo ============================================================
echo   SRS3 E2E WPF Panel Plugin - CANoe 12 Installer
echo ============================================================
echo.

call :FindCANoe
if errorlevel 1 goto :Failure

call :CheckProcesses
if errorlevel 1 goto :Failure

if "%CHECK_ONLY%"=="0" if "%ELEVATED%"=="0" (
    fltmc >nul 2>&1
    if errorlevel 1 (
        echo Requesting administrator permission for Program Files...
        powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -ArgumentList '/elevated' -Verb RunAs"
        if errorlevel 1 (
            echo [ERROR] Administrator permission was not granted.
            goto :Failure
        )
        exit /b 0
    )
)

call :BuildPlugin
if errorlevel 1 goto :Failure

if not exist "%PLUGIN_DLL%" (
    echo [ERROR] Plugin DLL was not produced:
    echo         %PLUGIN_DLL%
    goto :Failure
)

for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -Command "(Get-Item -LiteralPath '%PLUGIN_DLL%').VersionInfo.FileVersion"`) do set "PLUGIN_VERSION=%%V"
echo [OK] Plugin build: %PLUGIN_DLL%
echo [OK] File version: !PLUGIN_VERSION!

if "%CHECK_ONLY%"=="1" (
    echo [CHECK] Detection and build passed. No file was copied.
    goto :Success
)

call :CopyPlugin "%CANOE_ROOT%\Exec64\ControlLibraries"
if errorlevel 1 goto :Failure
call :CopyPlugin "%CANOE_ROOT%\Exec32\ControlLibraries"
if errorlevel 1 goto :Failure

if "%COPIED%"=="0" (
    echo [ERROR] No ControlLibraries directory was found under:
    echo         %CANOE_ROOT%
    goto :Failure
)

echo.
echo [OK] Installed to %COPIED% CANoe ControlLibraries folder(s).
echo [NEXT] Open Vector Panel Designer, select the SRS3 E2E library,
echo        then place "E2E Tx WPF Control" or "E2E Rx WPF Control" on the panel.
goto :Success

:FindCANoe
set "CANOE_ROOT="
if defined CANOE12_ROOT if exist "%CANOE12_ROOT%" set "CANOE_ROOT=%CANOE12_ROOT%"
for /f "tokens=2,*" %%A in ('reg query "HKLM\SOFTWARE\Vector\CANoe\12.0" /v Path 2^>nul ^| find /I "REG_SZ"') do set "CANOE_ROOT=%%B"

if not defined CANOE_ROOT if defined ProgramW6432 for /d %%D in ("%ProgramW6432%\Vector CANoe 12.0*") do if exist "%%~fD" set "CANOE_ROOT=%%~fD"
if not defined CANOE_ROOT for /d %%D in ("%ProgramFiles%\Vector CANoe 12.0*") do if exist "%%~fD" set "CANOE_ROOT=%%~fD"
if not defined CANOE_ROOT if defined ProgramFiles(x86) for /d %%D in ("%ProgramFiles(x86)%\Vector CANoe 12.0*") do if exist "%%~fD" set "CANOE_ROOT=%%~fD"

if not defined CANOE_ROOT (
    echo [ERROR] Vector CANoe 12.0 was not found.
    echo         Install CANoe 12.0 or set CANOE12_ROOT to its install folder.
    exit /b 1
)

if not exist "%CANOE_ROOT%" (
    echo [ERROR] CANoe registry path does not exist: %CANOE_ROOT%
    exit /b 1
)

set "CANOE_EXEC="
if exist "%CANOE_ROOT%\Exec64\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll" set "CANOE_EXEC=%CANOE_ROOT%\Exec64"
if not defined CANOE_EXEC if exist "%CANOE_ROOT%\Exec32\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll" set "CANOE_EXEC=%CANOE_ROOT%\Exec32"

if not defined CANOE_EXEC (
    echo [ERROR] Vector.PanelControlPlugin 1.2.0.0 was not found below:
    echo         %CANOE_ROOT%
    exit /b 1
)

set "PANEL_API=%CANOE_EXEC%\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll"
echo [OK] CANoe root: %CANOE_ROOT%
echo [OK] Panel API:  %PANEL_API%
exit /b 0

:CheckProcesses
set "APP_RUNNING=0"
for %%P in (PanelDesigner.exe CANoe64.exe CANoe32.exe CANoe.exe) do (
    tasklist /FI "IMAGENAME eq %%P" 2>nul | find /I "%%P" >nul
    if not errorlevel 1 (
        echo [BUSY] %%P is running.
        set "APP_RUNNING=1"
    )
)
if "%APP_RUNNING%"=="1" (
    echo [ERROR] Close CANoe and Vector Panel Designer, then run this installer again.
    exit /b 1
)
exit /b 0

:BuildPlugin
if not exist "%PROJECT_FILE%" (
    if exist "%PLUGIN_DLL%" (
        echo [INFO] Project source not found; using the packaged DLL.
        exit /b 0
    )
    echo [ERROR] Project source and packaged DLL are both missing.
    exit /b 1
)

set "MSBUILD_EXE="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" set "MSBUILD_EXE=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not defined MSBUILD_EXE if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" set "MSBUILD_EXE=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"

if not defined MSBUILD_EXE (
    if exist "%PLUGIN_DLL%" (
        echo [WARN] .NET Framework MSBuild was not found; using the packaged DLL.
        exit /b 0
    )
    echo [ERROR] .NET Framework 4.x MSBuild was not found.
    exit /b 1
)

echo [BUILD] Compiling Release plugin...
"%MSBUILD_EXE%" "%PROJECT_FILE%" /t:Rebuild /p:Configuration=Release /p:CanoePanelPluginPath="%PANEL_API%" /v:minimal
if errorlevel 1 (
    echo [ERROR] Plugin build failed.
    exit /b 1
)
exit /b 0

:CopyPlugin
set "TARGET_DIR=%~1"
if not exist "%TARGET_DIR%" exit /b 0

if exist "%TARGET_DIR%\SRS3.E2E.PanelControl.dll" (
    echo [INFO] Disabling legacy plugin filename in %TARGET_DIR%...
    move /Y "%TARGET_DIR%\SRS3.E2E.PanelControl.dll" "%TARGET_DIR%\SRS3.E2E.PanelControl.dll.disabled" >nul
    if errorlevel 1 (
        echo [ERROR] Could not disable the legacy plugin DLL.
        exit /b 1
    )
)

echo [COPY] %TARGET_DIR%
copy /B /Y "%PLUGIN_DLL%" "%TARGET_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll" >nul
if errorlevel 1 (
    echo [ERROR] Copy failed: %TARGET_DIR%
    exit /b 1
)

fc /B "%PLUGIN_DLL%" "%TARGET_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll" >nul
if errorlevel 1 (
    echo [ERROR] Binary verification failed: %TARGET_DIR%
    exit /b 1
)

set /a COPIED+=1
echo [OK] Verified: %TARGET_DIR%\SRS3_E2E_PanelControl_1_2_0_0.dll
exit /b 0

:Success
echo.
echo Completed successfully.
if not "%CHECK_ONLY%"=="1" pause
exit /b 0

:Failure
echo.
echo Installation did not complete. No CANoe configuration was opened or modified.
pause
exit /b 1
