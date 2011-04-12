;
; SQLite.iss --
;
; Written by Joe Mistachkin.
; Released to the public domain, use at your own risk!
;

[Setup]
AllowNoIcons=true
ArchitecturesInstallIn64BitMode=x64
AlwaysShowComponentsList=false
AppCopyright=Public Domain
AppID={#AppId}
AppName=System.Data.SQLite
AppPublisher=System.Data.SQLite Team
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
AppVerName=System.Data.SQLite v{#AppVersion}
AppVersion={#AppVersion}
AppComments=The ADO.NET adapter for the SQLite database engine.
AppReadmeFile={app}\readme.htm
DefaultDirName={pf}\System.Data.SQLite
DefaultGroupName=System.Data.SQLite
OutputBaseFilename=System.Data.SQLite.Setup_{#AppProcessor}_{#Year}
SetupLogging=true
UninstallFilesDir={app}\uninstall
VersionInfoVersion={#AppVersion}
ExtraDiskSpaceRequired=2097152

[Code]
#include "CheckForNetFx.pas"
#include "InitializeSetup.pas"

[Components]
Name: Application; Description: System.Data.SQLite components.; Types: custom compact full
Name: Application\Core; Description: Core components.; Types: custom compact full
Name: Application\Core\MSIL; Description: Core managed components.; Types: custom compact full
Name: Application\Core\{#AppProcessor}; Description: Core native components.; Types: custom compact full
Name: Application\LINQ; Description: LINQ support components.; Types: custom compact full
Name: Application\Symbols; Description: Debugging symbol components.; Types: custom compact full
Name: Application\Documentation; Description: Documentation components.; Types: custom compact full
Name: Application\Test; Description: Test components.; Types: custom compact full

[Tasks]
Components: Application\Core\MSIL Or Application\LINQ; Name: GAC; Description: Install the assemblies into the global assembly cache.; Flags: unchecked; Check: CheckIsNetFx2Setup() or CheckIsNetFx4Setup()
Components: Application\Core\MSIL Or Application\LINQ; Name: NGEN; Description: Generate native images for the assemblies and install the images in the native image cache.; Check: CheckIsNetFx2Setup() or CheckIsNetFx4Setup()

[Run]
Components: Application\Core\MSIL; Tasks: NGEN; Filename: {code:GetNetFx2InstallRoot|Ngen.exe}; Parameters: "install ""{app}\bin\System.Data.SQLite.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx2Setup()
Components: Application\Core\MSIL; Tasks: NGEN; Filename: {code:GetNetFx4InstallRoot|Ngen.exe}; Parameters: "install ""{app}\bin\System.Data.SQLite.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx4Setup()
Components: Application\LINQ; Tasks: NGEN; Filename: {code:GetNetFx2InstallRoot|Ngen.exe}; Parameters: "install ""{app}\bin\System.Data.SQLite.Linq.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx2Setup() and CheckForNetFx35(1)
Components: Application\LINQ; Tasks: NGEN; Filename: {code:GetNetFx4InstallRoot|Ngen.exe}; Parameters: "install ""{app}\bin\System.Data.SQLite.Linq.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx4Setup()

[UninstallRun]
Components: Application\LINQ; Tasks: NGEN; Filename: {code:GetNetFx4InstallRoot|Ngen.exe}; Parameters: "uninstall ""{app}\bin\System.Data.SQLite.Linq.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx4Setup()
Components: Application\LINQ; Tasks: NGEN; Filename: {code:GetNetFx2InstallRoot|Ngen.exe}; Parameters: "uninstall ""{app}\bin\System.Data.SQLite.Linq.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx2Setup() and CheckForNetFx35(1)
Components: Application\Core\MSIL; Tasks: NGEN; Filename: {code:GetNetFx4InstallRoot|Ngen.exe}; Parameters: "uninstall ""{app}\bin\System.Data.SQLite.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx4Setup()
Components: Application\Core\MSIL; Tasks: NGEN; Filename: {code:GetNetFx2InstallRoot|Ngen.exe}; Parameters: "uninstall ""{app}\bin\System.Data.SQLite.dll"" /nologo"; Flags: skipifdoesntexist; Check: CheckIsNetFx2Setup()

[Dirs]
Name: {app}\bin
Name: {app}\doc
Name: {app}\GAC

[Files]
Components: Application\Core\{#AppProcessor}; Source: ..\Externals\MSVCPP\vcredist_{#AppProcessor}_{#VcRuntime}.exe; DestDir: {tmp}; Flags: dontcopy
Components: Application; Source: ..\readme.htm; DestDir: {app}; Flags: restartreplace uninsrestartdelete isreadme
Components: Application\Core\MSIL; Tasks: GAC; Source: ..\bin\Release\bin\System.Data.SQLite.dll; DestDir: {app}\GAC; StrongAssemblyName: "System.Data.SQLite, Version={#AppVersion}, Culture=neutral, PublicKeyToken={#AppPublicKey}, ProcessorArchitecture=MSIL"; Flags: restartreplace uninsrestartdelete uninsnosharedfileprompt sharedfile gacinstall
Components: Application\Core\MSIL; Source: ..\bin\Release\bin\System.Data.SQLite.dll; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\Core\MSIL and Application\Symbols; Source: ..\bin\Release\bin\System.Data.SQLite.pdb; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\LINQ; Tasks: GAC; Source: ..\bin\Release\bin\System.Data.SQLite.Linq.dll; DestDir: {app}\GAC; StrongAssemblyName: "System.Data.SQLite.Linq, Version={#AppVersion}, Culture=neutral, PublicKeyToken={#AppPublicKey}, ProcessorArchitecture=MSIL"; Flags: restartreplace uninsrestartdelete uninsnosharedfileprompt sharedfile gacinstall
Components: Application\LINQ; Source: ..\bin\Release\bin\System.Data.SQLite.Linq.dll; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\LINQ and Application\Symbols; Source: ..\bin\Release\bin\System.Data.SQLite.Linq.pdb; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\Core\{#AppProcessor}; Source: ..\bin\{#AppPlatform}\ReleaseNativeOnly\SQLite.Interop.dll; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\Core\{#AppProcessor} and Application\Symbols; Source: ..\bin\{#AppPlatform}\ReleaseNativeOnly\SQLite.Interop.pdb; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\Documentation; Source: ..\doc\SQLite.NET.chm; DestDir: {app}\doc; Flags: restartreplace uninsrestartdelete
Components: Application\Test; Source: ..\bin\Release\bin\test.exe; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\Test and Application\Symbols; Source: ..\bin\Release\bin\test.pdb; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete
Components: Application\Test; Source: ..\bin\Release\bin\test.exe.config; DestDir: {app}\bin; Flags: restartreplace uninsrestartdelete

[Icons]
Name: {group}\Test Application; Filename: {app}\bin\test.exe; WorkingDir: {app}\bin; IconFilename: {app}\bin\test.exe; Comment: Launch Test Application; IconIndex: 0; Flags: createonlyiffileexists
Name: {group}\Class Library Documentation; Filename: {app}\doc\SQLite.NET.chm; WorkingDir: {app}\doc; Comment: Launch Class Library Documentation; Flags: createonlyiffileexists
Name: {group}\README File; Filename: {app}\readme.htm; WorkingDir: {app}; Comment: View README File; Flags: createonlyiffileexists