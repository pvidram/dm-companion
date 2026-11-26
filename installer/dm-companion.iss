; Instalador por usuario de DM Companion (sin administrador). Lo compila el workflow de release:
;   iscc installer\dm-companion.iss /DAppVersion=0.2.0 /DSourceDir=...\dist\win-x64\app /DOutputDir=...\dist
#ifndef AppVersion
  #error Falta /DAppVersion
#endif
#ifndef SourceDir
  #error Falta /DSourceDir
#endif
#ifndef OutputDir
  #error Falta /DOutputDir
#endif

[Setup]
AppId={{6F0C9B7E-2D4A-4C1E-9E3B-5A7D2C8F1B40}
AppName=DM Companion
AppVersion={#AppVersion}
AppPublisher=DM Companion
DefaultDirName={localappdata}\Programs\DM Companion
DefaultGroupName=DM Companion
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=dm-companion-{#AppVersion}-win-x64-setup
SetupIconFile={#SourceDir}\Assets\dm-companion.ico
UninstallDisplayIcon={app}\DMCompanion.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\DM Companion"; Filename: "{app}\DMCompanion.exe"
Name: "{autodesktop}\DM Companion"; Filename: "{app}\DMCompanion.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DMCompanion.exe"; Description: "{cm:LaunchProgram,DM Companion}"; Flags: nowait postinstall skipifsilent

; Los datos del usuario (%APPDATA%\DmCompanion, %LOCALAPPDATA%\DmCompanion) y el vault no se tocan: el desinstalador
; solo borra lo que instaló en {app}.
