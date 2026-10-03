; Setup program for Beacon Worship. Compiled by publish.ps1 into one Setup.exe in dist\.
; The app is installed into Program Files like any normal Windows program. Songs, sets and settings are
; kept in the user's own folders (see README), so uninstalling never deletes them.

#define AppName "Beacon Worship"
#define AppVersion "1.0.0"
#define AppPublisher "Blessingkeyz"
#define AppExe "BiblePresenter.App.exe"

[Setup]
; Fixed ID: Windows uses it to recognise this program on upgrades and in Apps & features. Do not change it.
AppId={{4812113D-5D09-4750-B96F-2C8E8896005C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Program Files needs administrator rights, so Windows asks for permission once at the start of setup.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; If the app is running during an upgrade, setup offers to close it rather than failing.
CloseApplications=yes
RestartApplications=no
OutputDir=..\dist
OutputBaseFilename=BeaconWorship-Setup
SetupIconFile=..\BiblePresenter.App\Resources\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\BeaconWorship\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\BeaconWorship\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
