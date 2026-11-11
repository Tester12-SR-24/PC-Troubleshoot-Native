#define AppName "PC Troubleshoot"
#define AppVersion GetEnv("PC_APP_VERSION")
#define PublishDir GetEnv("PC_PUBLISH_DIR")

[Setup]
AppId={{7AE14F17-BC0B-43DB-BD56-5CC145AF9308}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=PC Troubleshoot
DefaultDirName={localappdata}\Programs\PC Troubleshoot
DefaultGroupName=PC Troubleshoot
OutputDir=..\dist
OutputBaseFilename=PC-Troubleshoot-{#AppVersion}-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\PC-Troubleshoot.exe

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PC Troubleshoot"; Filename: "{app}\PC-Troubleshoot.exe"
Name: "{autodesktop}\PC Troubleshoot"; Filename: "{app}\PC-Troubleshoot.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\PC-Troubleshoot.exe"; Description: "Launch PC Troubleshoot"; Flags: nowait postinstall skipifsilent

