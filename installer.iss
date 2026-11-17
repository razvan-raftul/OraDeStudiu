; Program de instalare pentru Ora de Studiu (Inno Setup 6)
; Se instalează doar pentru utilizatorul curent, fără drepturi de administrator.

#define AppName "Ora de Studiu"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{3F7A2C91-5D4E-4B8A-A1C6-9E2D7B5F0C38}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Răzvan
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.
OutputBaseFilename=OraDeStudiu-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\OraDeStudiu.exe
UninstallDisplayName={#AppName}
CloseApplications=yes

[Languages]
#if FileExists(AddBackslash(SourcePath) + "Romanian.isl")
Name: "ro"; MessagesFile: "Romanian.isl"
#else
Name: "en"; MessagesFile: "compiler:Default.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "Pune o scurtătură pe desktop"; GroupDescription: "Scurtături:"

[Files]
Source: "out\OraDeStudiu.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "out\OraDeStudiu.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\OraDeStudiu.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\OraDeStudiu.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\OraDeStudiu.exe"; Description: "Pornește Ora de Studiu acum"; Flags: nowait postinstall skipifsilent
; la actualizarea automată (instalare fără ferestre) aplicația se redeschide singură
Filename: "{app}\OraDeStudiu.exe"; Flags: nowait; Check: IsAutoUpdate

[Code]
function IsAutoUpdate: Boolean;
begin
  Result := Pos('/AUTOUPDATE', Uppercase(GetCmdTail)) > 0;
end;
