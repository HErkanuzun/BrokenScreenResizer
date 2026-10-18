@echo off
rem Broken Screen Resizer - tam kaldirma / full uninstall
rem Programin bilgisayarda biraktigi her seyi geri alir ve siler: ayarlar, "Windows ile baslat" kaydi,
rem ekran degisiklikleri, sanal ekran surucusu ve sertifikasi, Windows'un sanal monitor kayitlari
rem (eski "Ekran Kurtar" kalintilari dahil). Surucu kuruluysa Windows yonetici izni ister.
rem Program kurulum gerektirmez; en sonda bu klasoru kendin silebilirsin.
rem Bu dosya BrokenScreenResizer.exe ile ayni klasorde olmali.
cd /d "%~dp0"
if not exist "BrokenScreenResizer.exe" (
  echo BrokenScreenResizer.exe bu klasorde bulunamadi. Uninstall.bat dosyasini programla ayni klasore koy.
  echo BrokenScreenResizer.exe was not found in this folder. Keep Uninstall.bat next to the program.
  pause
  exit /b 1
)
start "" /wait "BrokenScreenResizer.exe" --uninstall
