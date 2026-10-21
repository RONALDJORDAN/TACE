; =============================================================================
;  Plataforma Bot_RPA - Script do Instalador (Inno Setup 6)
;  Arquivo: controle/BotRPA.iss
;
;  Gera o instalador "InstaladorBotRPA_<versao>.exe" a partir do conteudo
;  ja pronto em BotRPA_Release\ (mesma base do pacote portatil PnP).
;
;  REGRAS:
;   - PROGRAMAS  -> {app}  (padrao C:\Program Files\Bot_RPA)
;   - DADOS      -> {commonappdata}\Bot_RPA  (variavel BOT_RPA_DATA_DIR)
;   - Atualizacao: reexecuta o instalador e PRESERVA os dados do usuario.
;   - Desinstalacao: remove os programas e PRESERVA os dados do usuario.
; =============================================================================

#define AppName "Bot_RPA"
#define AppNameLower "bot_rpa"
#define AppPublisher "Plataforma InnovTech"
#define AppExe "BotRPAui.exe"
#define AppVersion "20.10.2026.1843"
#define DataDirName "Bot_RPA"

[Setup]
AppId={{7C1B9E42-3A6D-4F51-9E77-2B8D4C0A5E31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir=..\controle
OutputBaseFilename=Instalador{#AppName}_{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
SetupIconFile=..\src\ui\Assets\Icons\BotRPAui.ico
; Marca o instalador como "de atualizacao" quando ja existe instalacao:
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

[Languages]
Name: "brazilian"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
brazilian.DataDirTitle=Pasta de dados do usuario
brazilian.DataDirLabel=Os dados do Bot_RPA (banco de aparelhos, credenciais e pacotes dos municipios) serao gravados nesta pasta.%n%nA pasta padrao e %1.%n%nOs PROGRAMAS (arquivos do sistema) serao instalados em:%n%2%n%nSe voce desinstalar o programa, os dados desta pasta serao PRESERVADOS.

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na &Area de Trabalho"; GroupDescription: "Atalhos adicionais:"

[Files]
; --- PROGRAMAS (sobrescritos a cada instalacao/Atualizacao) ---
Source: "..\BotRPA_Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Versao do build gravada na pasta de instalacao
Source: "..\config\version.txt"; DestDir: "{app}\config"; Flags: ignoreversion

; --- DADOS DO USUARIO (nunca sobrescritos) ---
; O seed do settings.json vai para a pasta de dados apenas na primeira instalacao.
Source: "..\BotRPA_Release\config\settings.json"; DestDir: "{commonappdata}\{#DataDirName}\config"; Flags: onlyifdoesntexist
Source: "..\BotRPA_Release\config\first_run.json"; DestDir: "{commonappdata}\{#DataDirName}\config"; Flags: onlyifdoesntexist
Source: "..\BotRPA_Release\config\municipios.csv"; DestDir: "{commonappdata}\{#DataDirName}\config"; Flags: onlyifdoesntexist
; Banco de dispositivos com o numero de patrimonio
Source: "..\BotRPA_Release\data\bot_database.db"; DestDir: "{commonappdata}\{#DataDirName}\data"; Flags: onlyifdoesntexist
; Pacotes dos municipios (APK / ZIP / Wallpaper)
Source: "..\BotRPA_Release\data\input\*"; DestDir: "{commonappdata}\{#DataDirName}\data\input"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs
Source: "..\BotRPA_Release\Arquivos\*"; DestDir: "{commonappdata}\{#DataDirName}\Arquivos"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs

[Dirs]
; Pastas de dados do usuario (criadas se nao existirem)
Name: "{commonappdata}\{#DataDirName}"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\config"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\data"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\data\input"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\data\input\municipios"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\data\input\apps"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\data\backups"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\Arquivos"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\logs"; Permissions: users-modify
Name: "{commonappdata}\{#DataDirName}\registros"; Permissions: users-modify
Name: "{group}"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Aponta a pasta de DADOS para o app (lido por PathHelper e por paths.c no motor C).
Root: HKLM; Subkey: "Environment"; ValueName: "BOT_RPA_DATA_DIR"; ValueData: "{commonappdata}\{#DataDirName}"; ValueType: string; Flags: uninsdeletevalue
; Caminho de instalacao (uso geral / terceiros)
Root: HKLM; Subkey: "SOFTWARE\{#AppName}"; ValueName: "InstallPath"; ValueData: "{app}"; ValueType: string; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#AppName}"; ValueName: "DataPath"; ValueData: "{commonappdata}\{#DataDirName}"; ValueType: string; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#AppName}"; ValueName: "Version"; ValueData: "{#AppVersion}"; ValueType: string; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "Iniciar o {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove apenas rastros do PROGRAMA. Os DADOS do usuario sao PRESERVADOS.
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\registros"
Type: filesandordirs; Name: "{app}\data"

[Code]
var
  BotRpaPath: String;

{ ---------------------------------------------------------------- }
{  Antes de instalar: garante que o app NAO esteja em execucao.        }
{ ---------------------------------------------------------------- }
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM BotRPAui.exe /T', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    { app encerrado }
  end;
end;

{ ---------------------------------------------------------------- }
{  Apos instalar: avisa onde os dados ficam e oferece abrir.         }
{ ---------------------------------------------------------------- }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    BotRpaPath := ExpandConstant('{commonappdata}') + '\{#DataDirName}';
    if MsgBox('Instalacao concluida!' + #13#10 + #13#10 +
              'Programas instalados em:' + #13#10 + ExpandConstant('{app}') + #13#10 + #13#10 +
              'Seus dados (banco de aparelhos, credenciais e pacotes dos municipios) estao em:' + #13#10 +
              BotRpaPath + #13#10 + #13#10 +
              'Ao atualizar o programa, seus dados sao PRESERVADOS.' + #13#10 + #13#10 +
              'Deseja iniciar o Bot_RPA agora?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', ExpandConstant('{app}') + '\{#AppExe}', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
  end;
end;

{ ---------------------------------------------------------------- }
{  Pergunta se o usuario quer apagar os DADOS na desinstalacao.     }
{ ---------------------------------------------------------------- }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataPath := ExpandConstant('{commonappdata}') + '\{#DataDirName}';
    if DirExists(DataPath) then
    begin
      if MsgBox('O programa foi removido.' + #13#10 + #13#10 +
                'Os seus dados continuam em:' + #13#10 + DataPath + #13#10 + #13#10 +
                'Deseja APAGAR tambem os seus dados?' + #13#10 +
                '(banco de aparelhos, credenciais, pacotes e historico)' + #13#10 + #13#10 +
                'Escolha "Nao" para manter seus dados para uma futura instalacao.',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataPath, True, True, True);
      end;
    end;
  end;
end;
