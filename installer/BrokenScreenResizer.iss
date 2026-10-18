; Broken Screen Resizer - kurulum paketi (Inno Setup 6)
; Derleme: ISCC.exe /DAppVersion=1.01 installer\BrokenScreenResizer.iss   (önce build.bat)
;
; - Yönetici izni istemez; kullanıcının kendi Programlar klasörüne kurar (%LocalAppData%\Programs).
; - Program zaten kuruluysa aynı yere üzerine kurar (güncelleme); çalışan eski sürüm önce düzgünce kapatılır.
; - Windows "Uygulamalar" listesinde sürümüyle görünür; oradan kaldırılınca programın bilgisayarda
;   yaptığı her değişiklik de geri alınır (BrokenScreenResizer.exe --uninstall --silent).

#ifndef AppVersion
  #define AppVersion "1.01"
#endif
#define AppName "Broken Screen Resizer"
#define AppExe "BrokenScreenResizer.exe"
#define AppUrl "https://github.com/HErkanuzun/BrokenScreenResizer"

[Setup]
; AppId hiç değişmemeli: güncellemenin eski kurulumu bulması buna bağlı
AppId={{6E0B7C1D-4E52-4B2B-9F0A-3C8A51D7B2E4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=HErkanuzun
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 HErkanuzun. MIT License.
VersionInfoVersion={#AppVersion}.0.0
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
LicenseFile=..\LICENSE
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename=BrokenScreenResizer-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ekran değişikliklerini, sanal ekran sürücüsünü, sertifikayı, başlangıç kaydını ve ayarları temizler
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall --silent"; Flags: waituntilterminated; RunOnceId: "FullCleanup"

[Code]
// Dosyalar değiştirilmeden önce çalışan kopyalar (eski sürümler ve taşınabilir kopyalar dahil) düzgünce kapatılır:
// yeni exe geçici klasörden "--exit" ile çalıştırılır; etkin ayar yeni sürüm açılınca yeniden kurulur.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile('{#AppExe}');
  Exec(ExpandConstant('{tmp}\{#AppExe}'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
