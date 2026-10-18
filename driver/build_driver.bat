@echo off
rem ============================================================================
rem Ekran Kurtar sanal ekran surucusu - derleme + imzalama betigi
rem
rem Gerekenler (sadece gelistirici bilgisayarinda):
rem   - Visual Studio 2022/2026, "C++ ile masaustu gelistirme" bileseni
rem   - Windows SDK (Visual Studio ile gelir)
rem   - WDK: kurulu degilse betik Microsoft'un resmi NuGet paketini driver\.wdk icine indirir
rem
rem Cikti: driver\package\  (EkranKurtarVDD.dll, .inf, .cat, .cer)
rem Ana build.bat bu klasoru EkranKurtar.exe'nin icine gomer.
rem ============================================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set UMDF_VER=2.25
set IDDCX_VER=1.4
set WDK_NUGET_VER=10.0.26100.6584

rem ---------------------------------------------------------------- Visual Studio
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if not exist "%VSWHERE%" (
  echo Visual Studio bulunamadi. "C++ ile masaustu gelistirme" bileseniyle Visual Studio kur.
  goto :fail
)
set VSPATH=
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set VSPATH=%%i
if "%VSPATH%"=="" (
  echo Visual Studio C++ araclari bulunamadi.
  goto :fail
)
set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"
call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 goto :fail

rem ---------------------------------------------------------------- WDK
set WDKROOT=
set KITS=%ProgramFiles(x86)%\Windows Kits\10
if exist "%KITS%\Include\wdf\umdf\%UMDF_VER%\wdf.h" set WDKROOT=%KITS%
if "%WDKROOT%"=="" if exist ".wdk\c\Include\wdf\umdf\%UMDF_VER%\wdf.h" set WDKROOT=%~dp0.wdk\c
if "%WDKROOT%"=="" (
  echo WDK bulunamadi, resmi NuGet paketi indiriliyor ^(~110 MB, bir kez^)...
  if not exist .wdk mkdir .wdk
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue';" ^
    "Invoke-WebRequest -UseBasicParsing 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.wdk.x64/%WDK_NUGET_VER%/microsoft.windows.wdk.x64.%WDK_NUGET_VER%.nupkg' -OutFile '.wdk\wdk.zip';" ^
    "Expand-Archive -Force '.wdk\wdk.zip' '.wdk'; Remove-Item '.wdk\wdk.zip'"
  if errorlevel 1 (
    echo WDK indirilemedi.
    goto :fail
  )
  set WDKROOT=%~dp0.wdk\c
)

set WDKVER=
for /d %%v in ("%WDKROOT%\Include\10.*") do if exist "%%v\um\iddcx\%IDDCX_VER%\IddCx.h" set WDKVER=%%~nxv
if "%WDKVER%"=="" (
  echo WDK icinde IddCx %IDDCX_VER% basliklari bulunamadi: %WDKROOT%
  goto :fail
)
echo WDK: %WDKROOT% ^(%WDKVER%^)

set WDKINC=%WDKROOT%\Include\%WDKVER%
set WDKLIB=%WDKROOT%\Lib\%WDKVER%

rem ---------------------------------------------------------------- Derleme
if not exist out\obj mkdir out\obj
if not exist package mkdir package

cl /nologo /c /std:c++17 /EHsc /O2 /MT /W4 /WX- /wd4324 /GS /guard:cf /Zi /utf-8 ^
  /DUNICODE /D_UNICODE /DUMDF_USING_NTSTATUS ^
  /DUMDF_VERSION_MAJOR=2 /DUMDF_VERSION_MINOR=%UMDF_VER:~2% /DIDDCX_VERSION_MAJOR=1 /DIDDCX_VERSION_MINOR=%IDDCX_VER:~2% ^
  /D_WIN32_WINNT=0x0A00 /DWINVER=0x0A00 /DNTDDI_VERSION=0x0A000007 ^
  /I "%WDKINC%\um\iddcx\%IDDCX_VER%" /I "%WDKROOT%\Include\wdf\umdf\%UMDF_VER%" ^
  /I "%WDKINC%\um" /I "%WDKINC%\shared" ^
  /Fo"out\obj\\" /Fd"out\obj\\" Driver.cpp
if errorlevel 1 goto :fail

link /nologo /DLL /OUT:out\EkranKurtarVDD.dll /PDB:out\EkranKurtarVDD.pdb /DEBUG /OPT:REF /OPT:ICF ^
  /SUBSYSTEM:WINDOWS /DYNAMICBASE /NXCOMPAT /GUARD:CF /RELEASE ^
  out\obj\Driver.obj ^
  "%WDKROOT%\Lib\wdf\umdf\x64\%UMDF_VER%\WdfDriverStubUm.lib" ^
  "%WDKLIB%\um\x64\iddcx\%IDDCX_VER%\IddCxStub.lib" ^
  ntdll.lib d3d11.lib dxgi.lib avrt.lib kernel32.lib
if errorlevel 1 goto :fail

copy /y out\EkranKurtarVDD.dll package\ >nul
copy /y EkranKurtarVDD.inf package\ >nul
if exist package\EkranKurtarVDD.cat del package\EkranKurtarVDD.cat

rem ---------------------------------------------------------------- Imzalama
rem Gelistiricinin kendi bilgisayarinda bir kez kendinden imzali sertifika olusturulur
rem (CurrentUser\My). Ozel anahtar bu bilgisayardan cikmaz; pakete sadece acik sertifika (.cer) konur.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign_driver.ps1" -Package "%~dp0package" -WdkRoot "%WDKROOT%" -WdkVer "%WDKVER%"
if errorlevel 1 goto :fail

echo.
echo TAMAM: driver\package hazir. Simdi ana klasordeki build.bat'i calistir.
if "%1"=="" pause
exit /b 0

:fail
echo.
echo SURUCU DERLEME HATASI
if "%1"=="" pause
exit /b 1
