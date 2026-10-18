@echo off
rem Ekran Kurtar - derleme betigi. Windows'un icindeki .NET Framework derleyicisini kullanir, ekstra kurulum gerekmez.
rem Sanal ekran surucusu (driver\package) varsa EkranKurtar.exe'nin icine gomulur; tek dosya olarak dagitilir.
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
if exist "%PKG%\EkranKurtarVDD.dll" if exist "%PKG%\EkranKurtarVDD.inf" if exist "%PKG%\EkranKurtarVDD.cat" if exist "%PKG%\EkranKurtarVDD.cer" (
  set DRV=/resource:%PKG%\EkranKurtarVDD.dll,EkranKurtar.driver.EkranKurtarVDD.dll /resource:%PKG%\EkranKurtarVDD.inf,EkranKurtar.driver.EkranKurtarVDD.inf /resource:%PKG%\EkranKurtarVDD.cat,EkranKurtar.driver.EkranKurtarVDD.cat /resource:%PKG%\EkranKurtarVDD.cer,EkranKurtar.driver.EkranKurtarVDD.cer
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
  /resource:src\app.ico,EkranKurtar.app.ico /resource:src\logo.png,EkranKurtar.logo.png %DRV% ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll ^
  /out:bin\EkranKurtar.exe src\*.cs
if errorlevel 1 (
  echo.
  echo DERLEME HATASI
  if "%1"=="" pause
  exit /b 1
)
echo.
echo TAMAM: bin\EkranKurtar.exe
if "%1"=="" pause
