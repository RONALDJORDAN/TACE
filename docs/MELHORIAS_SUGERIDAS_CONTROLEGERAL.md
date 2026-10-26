# Melhorias sugeridas - ControleGeral.bat

**Data:** 26/10/2026  
**Projeto:** Bot_RPA  
**Ambiente:** Windows, GCC 16.1.0 (WinGet), .NET 8 SDK 8.0.424, Inno Setup 6

---

## Resultado dos testes (3 opções)

| Opção | Nome | Status | Artefato |
|-------|------|--------|----------|
| [1] | Build TESTE - motor C + UI em data\output | OK | `data\output\BotEngine.dll` + `data\output\BotRPAui.exe` |
| [2] | Build RELEASE - pacote portatil PnP | OK | `BotRPA_Release\` (binarios, config, tools, banco de dados) |
| [3] | Build INSTALADOR - setup.exe via Inno Setup | OK | `scripts\scripts de controle\BotRPA_Instalador_26.10.2026.0908.exe` (61,5 MB) |

**Log de auditoria:** `docs\registros\2026-10-26\09-07_..._DESKTOP_BUILD` e `09-08_...`

---

## Melhorias sugeridas

### 1. Build / pipeline

- **Instalar Inno Setup automaticamente** na opção [4] de deps (atualmente so GCC e .NET tem rotina). Adicionar `winget install --id JRSoftware.InnoSetup -e`.
- **Encadear opcoes:** a opcao [3] exige que a [2] tenha rodado antes. Permitir que `ControleGeral.bat 3` rode a [2] automaticamente se `BotRPA_Release\` nao existir.
- **Build incremental:** compilar tudo sempre, mesmo sem alteracao nos fontes. Adicionar verificacao de timestamp/hash dos `.c` e `.cs` antes de recompilar.
- **Limpeza seletiva do `data\output`:** arquivos antigos (`window_dump_*.xml`, logs, `arquivos\`) permanecem entre builds. Adicionar opcao de limpeza antes do build.
- **Checksum de integridade:** gerar `SHA256SUMS.txt` com hashes de `BotEngine.dll`, `BotRPAui.exe` e do instalador para verificacao pelo usuario final.

### 2. Instalador (Inno Setup)

- **Assinatura de codigo:** o `setup.exe` e binarios nao sao assinados; o Windows mostra "editor desconhecido". Adicionar `SignTool` no `:build_installer`.
- **Caminhos relativos no `.iss`:** `..\..\BotRPA_Release\*` depende do diretorio de execução. Tornar explicito ou usar `SourceDir` fixo.
- **Versao semantica:** a versao eh um timestamp (`DD.MM.YYYY.HHMM`). Considerar `MAJOR.MINOR.PATCH` gerado a partir de arquivo de versao ou Git tag.

### 3. Qualidade

- **Testes automatizados:** nao ha testes de unidade para o motor C nem para a UI WPF. Adicionar projeto de teste e rodar no `:build_teste`.
- **Limite no monitor de tela:** o loop `:monitor_loop` eh infinito. Adicionar limite maximo de capturas ou duracao.

---

## Arquivos relevantes

- `scripts\scripts de controle\ControleGeral.bat` - script unico de operacoes
- `scripts\scripts de controle\BotRPA.iss` - script do instalador Inno Setup
- `scripts\scripts de controle\InstallerAssets\` - assets do instalador (termo de uso, imagens)
- `data\output\` - saida do Build TESTE
- `BotRPA_Release\` - saida do Build RELEASE
- `docs\registros\` - auditoria de builds
