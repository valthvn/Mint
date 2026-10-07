#ifndef AppVersion
  #error AppVersion must be supplied by Build-Installer.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by Build-Installer.ps1
#endif
#ifndef InstallerDir
  #error InstallerDir must be supplied by Build-Installer.ps1
#endif

[Setup]
AppId={{931D90F9-70EB-477B-BC08-2FE1D8DBBF94}
AppName=Mint
AppVersion={#AppVersion}
AppPublisher=Mint contributors
AppPublisherURL=https://github.com/valthvn/Mint-source
AppSupportURL=https://github.com/valthvn/Mint-source/issues
DefaultDirName={localappdata}\Programs\Mint
DefaultGroupName=Mint
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19045
AppMutex=Mint.App
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\Mint.exe
SetupIconFile=..\src\Mint\Assets\Mint.ico
LicenseFile=..\LICENSE
OutputDir={#InstallerDir}
OutputBaseFilename=Mint-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
Uninstallable=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
french.DesktopShortcut=Créer un raccourci sur le bureau
english.StartWithWindows=Start Mint when I sign in to Windows
french.StartWithWindows=Démarrer Mint à l'ouverture de session Windows
english.LaunchMint=Launch Mint
french.LaunchMint=Lancer Mint
english.UninstallNote=Disable cooling in Mint before uninstalling if you want to restore your power plan. Preferences and recovery backups will be kept. Continue uninstalling?
french.UninstallNote=Désactivez le refroidissement dans Mint avant de désinstaller pour restaurer votre profil d'alimentation. Les préférences et sauvegardes seront conservées. Continuer la désinstallation ?

[Messages]
english.FinishedLabel=Mint has been installed.%n%nClick the leaf icon in the system tray to open it. CPU temperatures require the PawnIO driver and may require running Mint as administrator.
french.FinishedLabel=Mint est installé.%n%nCliquez sur la feuille dans la zone de notification pour l'ouvrir. Les températures CPU nécessitent le pilote PawnIO et peuvent demander de lancer Mint en administrateur.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Mint"; Filename: "{app}\Mint.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Mint"; Filename: "{app}\Mint.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\Mint"; Filename: "{app}\Mint.exe"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\Mint.exe"; Description: "{cm:LaunchMint}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function InitializeUninstall(): Boolean;
begin
  Result := True;
  if not UninstallSilent then
    Result := MsgBox(CustomMessage('UninstallNote'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;
