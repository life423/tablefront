; Compile with BuildInstaller.ps1. The Unity player needs every runtime folder.
#ifndef BuildDirectory
  #error BuildDirectory must point to the complete Windows player build.
#endif
#ifndef OutputDirectory
  #error OutputDirectory must be supplied.
#endif
#ifndef AppVersion
  #define AppVersion "0.3.2"
#endif

[Setup]
AppId={{6EBC15BC-6E51-4390-B9A5-8A20F5A904BA}
AppName=Tablefront
AppVersion={#AppVersion}
AppVerName=Tablefront {#AppVersion}
AppPublisher=Lumen Workshop
AppPublisherURL=https://github.com/life423/tablefront
AppSupportURL=https://github.com/life423/tablefront/issues
AppUpdatesURL=https://github.com/life423/tablefront/releases/latest
DefaultDirName={localappdata}\Programs\Tablefront
DefaultGroupName=Tablefront
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDirectory}
OutputBaseFilename=Tablefront-Setup-v{#AppVersion}
SetupIconFile=..\Assets\Branding\Tablefront.ico
UninstallDisplayIcon={app}\StartTablefront.exe
WizardStyle=modern dynamic
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayName=Tablefront
VersionInfoVersion={#AppVersion}.0

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Shortcuts:"
Name: "startmenuicon"; Description: "Create a &Start menu shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#BuildDirectory}\StartTablefront.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDirectory}\Tablefront.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDirectory}\UnityPlayer.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDirectory}\UnityCrashHandler64.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDirectory}\dstorage.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDirectory}\dstoragecore.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDirectory}\D3D12\*"; DestDir: "{app}\D3D12"; Excludes: "*.pdb,*.mdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BuildDirectory}\MonoBleedingEdge\*"; DestDir: "{app}\MonoBleedingEdge"; Excludes: "*.pdb,*.mdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BuildDirectory}\Tablefront_Data\*"; DestDir: "{app}\Tablefront_Data"; Excludes: "*.pdb,*.mdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Always use the startup-fix launcher, including the finish-page launch below.
Name: "{userdesktop}\Tablefront"; Filename: "{app}\StartTablefront.exe"; WorkingDir: "{app}"; AppUserModelID: "LumenWorkshop.Tablefront"; Tasks: desktopicon
Name: "{group}\Tablefront"; Filename: "{app}\StartTablefront.exe"; WorkingDir: "{app}"; AppUserModelID: "LumenWorkshop.Tablefront"; Tasks: startmenuicon

[Run]
Filename: "{app}\StartTablefront.exe"; WorkingDir: "{app}"; Description: "Play Tablefront"; Flags: nowait postinstall skipifsilent
