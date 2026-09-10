; =============================================================================
; MailLoadTester – Inno Setup skript (autoinstalátor pro Windows)
; =============================================================================
; Před sestavením tohoto skriptu MUSÍ existovat:
;   ..\publish\MailLoadTester.exe
; (vytvoříte příkazem dotnet publish – viz JAK-SESTAVIT-A-INSTALOVAT.txt)
;
; Otevřete tento soubor v „Inno Setup Compiler“ a zvolte Build → Compile.
; =============================================================================

#define MyAppName        "MailLoadTester"
#define MyAppVersion     "2.8.10"
#define MyAppPublisher   "MailLoadTester"
#define MyAppExeName     "MailLoadTester.exe"
; Cesta k EXE relativně ke složce installer\
#define MyAppSource      "..\publish\MailLoadTester.exe"

[Setup]
AppId={{8F3C2A1B-6D4E-4A9F-9B2C-1E7D5A0F3B8C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Instalátor pro 64bit Windows
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
; Výstup
OutputDir=Output
OutputBaseFilename=Setup-MailLoadTester
; Komprese
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Práva: Program Files obvykle vyžaduje správce
PrivilegesRequired=admin
; Odinstalace
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=
; Informace
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=SMTP load / delivery tester
VersionInfoProductName={#MyAppName}

[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Vytvořit zástupce na ploše"; GroupDescription: "Další volby:"; Flags: unchecked

[Files]
; Hlavní program (self-contained single-file EXE)
Source: "{#MyAppSource}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Odinstalovat {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Spustit {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Jednoduchá kontrola: 64bit Windows
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
