@echo off
rem Broken Screen Resizer - derleme betigi. Windows'un icindeki .NET Framework derleyicisini kullanir, ekstra kurulum gerekmez.
rem Sanal ekran surucusu (driver\package) varsa BrokenScreenResizer.exe'nin icine gomulur; tek dosya olarak dagitilir.
rem Surucuyu yeniden derlemek icin: driver\build_driver.bat (Visual Studio + WDK gerekir, sadece gelistirici icin)
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo .NET Framework 4 derleyicisi bulunamadi.
  if "%1"=="" pause
  exit /b 1
)

set DRV=
set PKG=driver\package
if exist "%PKG%\BrokenScreenResizerVDD.dll" if exist "%PKG%\BrokenScreenResizerVDD.inf" if exist "%PKG%\BrokenScreenResizerVDD.cat" if exist "%PKG%\BrokenScreenResizerVDD.cer" (
  set DRV=/resource:%PKG%\BrokenScreenResizerVDD.dll,BrokenScreenResizer.driver.BrokenScreenResizerVDD.dll /resource:%PKG%\BrokenScreenResizerVDD.inf,BrokenScreenResizer.driver.BrokenScreenResizerVDD.inf /resource:%PKG%\BrokenScreenResizerVDD.cat,BrokenScreenResizer.driver.BrokenScreenResizerVDD.cat /resource:%PKG%\BrokenScreenResizerVDD.cer,BrokenScreenResizer.driver.BrokenScreenResizerVDD.cer
)
if "%DRV%"=="" (
  echo UYARI: driver\package bulunamadi; sanal monitor yontemi bu exe'de calismaz.
  echo        Once driver\build_driver.bat calistir.
) else (
  echo Sanal ekran surucusu exe'ye gomuluyor.
)

if not exist bin mkdir bin
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu ^
  /win32icon:src\app.ico /win32manifest:src\app.manifest ^
  /resource:src\app.ico,BrokenScreenResizer.app.ico /resource:src\logo.png,BrokenScreenResizer.logo.png %DRV% ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll ^
  /out:bin\BrokenScreenResizer.exe src\*.cs
if errorlevel 1 (
  echo.
  echo DERLEME HATASI
  if "%1"=="" pause
  exit /b 1
)
rem Kaldirma betigi exe'nin yaninda dagitilir
copy /y Uninstall.bat bin\ >nul
echo.
echo TAMAM: bin\BrokenScreenResizer.exe + bin\Uninstall.bat
if "%1"=="" pause
