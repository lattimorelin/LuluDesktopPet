#define MyAppName "噜噜桌宠"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "噜噜桌宠"
#define MyAppExeName "LuluDesktopPet.exe"

[Setup]
AppId={{8F9FC35E-1378-4C4F-B94B-9B362F969950}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序
VersionInfoProductName={#MyAppName}
DefaultDirName={localappdata}\Programs\LuluDesktopPet
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\installer-output
OutputBaseFilename=LuluDesktopPet-Setup-{#MyAppVersion}
SetupIconFile=..\assets\lulu.ico
UninstallDisplayIcon={app}\assets\lulu.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=LuluDesktopPet.SingleInstance
MinVersion=10.0

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked
Name: "startup"; Description: "登录 Windows 时自动启动噜噜"; GroupDescription: "启动选项："; Flags: unchecked

[Files]
Source: "..\dist\LuluDesktopPet.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\assets\*"; DestDir: "{app}\assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\噜噜桌宠"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\卸载噜噜桌宠"; Filename: "{uninstallexe}"
Name: "{autodesktop}\噜噜桌宠"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\噜噜桌宠"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动噜噜桌宠"; Flags: nowait postinstall skipifsilent
