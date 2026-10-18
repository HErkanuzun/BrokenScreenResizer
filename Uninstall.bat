@echo off
rem Broken Screen Resizer - tam kaldirma
rem Programin bilgisayarda biraktigi her seyi siler: ayarlar, "Windows ile baslat" kaydi, ekran degisiklikleri,
rem sanal ekran surucusu ve sertifikasi, Windows'un sanal monitor kayitlari (eski "Ekran Kurtar" kalintilari dahil).
rem Surucu kuruluysa Windows yonetici izni ister. Sonunda istersen programin kendisi ve bu dosya da silinir.
rem Bu dosya BrokenScreenResizer.exe ile ayni klasorde olmali.
cd /d "%~dp0"
if not exist "BrokenScreenResizer.exe" (
  echo BrokenScreenResizer.exe bu klasorde bulunamadi. Uninstall.bat dosyasini programla ayni klasore koy.
  pause
  exit /b 1
)
start "" /wait "BrokenScreenResizer.exe" --uninstall
