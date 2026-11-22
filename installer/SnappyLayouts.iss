; ============================================================================
; Snappy Layouts — Inno Setup 6 Installer Script (C:\Program Files\Snappy Layouts)
; ============================================================================
#ifndef AppVersion
  #define AppVersion "1.10"
#endif

#define AppName "Snappy Layouts"
#define AppPublisher "YUSUF KILINÇ"
#define AppURL "https://github.com/yusufkilinc44/SnappyLayouts"
#define AppExeName "Snappy Layouts.exe"
#define PublishDir "..\publish\win-x64"

[Setup]
AppId={{8F4E2C1A-9B3D-4E7A-A1C5-6D8E9F0B2C4A}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\dist
OutputBaseFilename=SnappyLayouts-v{#AppVersion}-Setup
SetupIconFile=..\src\Snappy Layouts\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=no
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=SnappyLayouts_YusufKilinc_SingleInstance

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Start {#AppName} automatically when Windows starts"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  SPI_SETWINARRANGING = $0083;
  SPIF_UPDATEINIFILE = $0001;
  SPIF_SENDCHANGE = $0002;

function SystemParametersInfo(uiAction, uiParam: Cardinal; pvParam: LongInt; fWinIni: Cardinal): Boolean;
  external 'SystemParametersInfoW@user32.dll stdcall';

procedure RestoreWindowsNativeSnap();
begin
  RegWriteDWordValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced', 'EnableSnapAssistFlyout', 1);
  RegWriteDWordValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced', 'EnableSnapBar', 1);
  RegWriteStringValue(HKCU, 'Control Panel\Desktop', 'WindowArrangementActive', '1');
  SystemParametersInfo(SPI_SETWINARRANGING, 1, 0, SPIF_UPDATEINIFILE or SPIF_SENDCHANGE);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec('taskkill.exe', '/F /IM "Snappy Layouts.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Snappy Layouts');
    RestoreWindowsNativeSnap();
  end;
end;
