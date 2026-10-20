#define AppName "DocSoft"
#ifndef AppVersion
#define AppVersion "1.0.6"
#endif
#define AppPublisher "DemaNFox"
#ifndef TargetRuntime
#define TargetRuntime "win-x64"
#endif
#ifndef BuildConfiguration
#define BuildConfiguration "Release"
#endif
#define AppRoot "..\src\AppHost\DockLauncher.AppHost\bin\" + BuildConfiguration + "\net9.0-windows\" + TargetRuntime + "\publish"
#define LauncherRoot "..\src\AppHost\DockLauncher.Launcher\bin\" + BuildConfiguration + "\net9.0-windows\" + TargetRuntime + "\publish"

[Setup]
AppId={{8F6B9416-5690-48DA-A4B1-63D622044B55}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=DocSoft-Setup-{#AppVersion}-{#TargetRuntime}
SetupIconFile=..\src\AppHost\DockLauncher.AppHost\Resources\DocSoft.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
CloseApplicationsFilter=DockLauncher.exe
RestartApplications=no
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#LauncherRoot}\DockLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\DockLauncher.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DockLauncher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\DockLauncher.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
