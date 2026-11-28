#ifndef PublishDir
  #error PublishDir must point to the self-contained Windows build.
#endif
#ifndef AppVersion
  #error AppVersion must be supplied by the build script.
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by the build script.
#endif

[Setup]
AppId={{362E3AB8-C8E0-478A-B08D-5B9B4361ED3E}
AppName=CydLan
AppVersion={#AppVersion}
AppPublisher=CydLan
DefaultDirName={localappdata}\Programs\CydLan
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
DisableProgramGroupPage=yes
DisableWelcomePage=yes
LicenseFile=..\LICENSE
SetupIconFile=..\assets\brand\cydlan.ico
UninstallDisplayIcon={app}\CydLan.exe
OutputDir={#OutputDir}
OutputBaseFilename=CydLan-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
AppMutex=Local\CydLan.Desktop
CloseApplications=no
RestartApplications=no
UsePreviousTasks=yes

[Tasks]
Name: "desktopicon"; Description: "Desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\CydLan"; Filename: "{app}\CydLan.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\CydLan"; Filename: "{app}\CydLan.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\CydLan.exe"; Description: "Open CydLan"; Flags: nowait postinstall skipifsilent unchecked

[UninstallRun]
Filename: "{app}\CydLan.exe"; Parameters: "--reset-stats"; RunOnceId: "ResetPetStats"; Flags: runhidden waituntilterminated skipifdoesntexist
