#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{E1CC9728-7476-4FD7-A14F-9960F0C83A80}
AppName=My Game Launcher
AppVersion={#AppVersion}
AppPublisher=xasifsaeedx
AppPublisherURL=https://github.com/xasifsaeedx/Game-Launcher
AppSupportURL=https://github.com/xasifsaeedx/Game-Launcher/issues
DefaultDirName={localappdata}\Programs\My Game Launcher
DefaultGroupName=My Game Launcher
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=GameLauncher-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=My Game Launcher
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\My Game Launcher"; Filename: "{app}\GameLauncher.App.exe"
Name: "{autodesktop}\My Game Launcher"; Filename: "{app}\GameLauncher.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GameLauncher.App.exe"; Description: "Launch My Game Launcher"; Flags: nowait postinstall skipifsilent
