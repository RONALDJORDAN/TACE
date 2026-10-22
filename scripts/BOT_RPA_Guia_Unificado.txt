@echo off
:: =============================================================================
::  Bot_RPA - Guia de Comando Unificado (scenarios rapidos)
::  Arquivo: scripts\unified.bat
:: =============================================================================
::
::  Este guia lista todos os modos disponiveis e exemplos de uso.
::
::  Modos Principais (scripts\unified.bat <modo>):
::     build          : Compila desenvolvimento (data/output)
::     release        : Compila release (BotRPA_Release)
::     installer       : Gera instalador Windows
::     deps            : Instala dependencias (GCC, .NET, ADB)
::     adb             : Baixa tools ADB
::     monitor         : Monitor de tela (unidade de teste)
::     help            : Exibe este guia
::
:: =============================================================================
::
::  Arquitetura de Build:
::
::    1. scripts\build.bat                    -> compilacao C# e C nativo
::    2. scripts\build_release.bat             -> build portatil (BotRPA_Release)
::    3. scripts\build_installer.bat           -> build de instalador (setup.exe)
::    4. scripts\install_dependencies.bat     -> instala GCC e .NET
::    5. scripts\setup_adb.bat                 -> download de platform-tools
::    6. scripts\monitor_tela.bat              -> monitoramento de tela
::    7. scripts\analise_xml.ps1               -> analisador XML
::    8. scripts\gravar_fluxo_manual.ps1      -> gravador de fluxo manual
::
:: =============================================================================
::
::  Cenarios Tipicos (com o unified script):
::
::  A. Desenvolvimento (compilacao local):
::     scripts\unified.bat build
::
::  B. Setup completo (ambiente limpo):
::     scripts\unified.bat deps
::     scripts\unified.bat adb
::     scripts\unified.bat build
::
::  C. Build para distribuicao:
::     scripts\unified.bat build
::     scripts\unified.bat release
::     scripts\unified.bat installer
::
::  D. Teste de automacao:
::     scripts\unified.bat deps
::     scripts\unified.bat adb
::     scripts\unified.bat build
::     scripts\unified.bat monitor
::
:: =============================================================================
::
::  Tecnicas Avancadas:
::
::     Para desenvolvimento com CI/CD:
::       1. scripts\unified.bat build
::       2. scripts\unified.bat release
::       3. scripts\unified.bat installer
::
::     Para desenvolvimento local com monitor:
::       1. scripts\unified.bat deps
::       2. scripts\unified.bat adb
::       3. scripts\unified.bat build
::       4. scripts\unified.bat monitor
::
:: =============================================================================
::
::  Observacoes:
::
::    - Todos os scripts sao FAT32-compatíveis (max 255 chars path)
::    - Todos os scripts usam encoding UTF-8 sem BOM
::    - Todos os scripts sao portateis (funcionam em qualquer Windows 10/11)
::    - Todos os scripts sao versionados no controle de versao
::
::    - scripts\unified.bat e a documentacao devem ser priorizados na gestao do projeto
::
:: =============================================================================
::  [FIM]
