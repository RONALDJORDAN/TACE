# Reverse Tethering (Internet do PC para o Android)

Esta ferramenta utiliza o projeto Open-Source **Gnirehtet** (mantido pela Genymobile, mesmos criadores do scrcpy) para enviar a conexão de internet do computador diretamente para o tablet Android através do cabo USB.

## Por que guardar essa ferramenta para o futuro?
Em cenários onde a sala de aula ou escola não possui Wi-Fi, mas os tablets precisam sincronizar dados com o Ministério da Educação (ou qualquer servidor), podemos plugar o tablet no PC via USB e usar a internet do próprio PC!

## Como Usar

### 1. Download Automático
Execute o arquivo `1_instalar_ferramenta.ps1` (Botão direito -> Executar com PowerShell). 
Ele fará o download da última versão do Gnirehtet automaticamente e extrairá os arquivos necessários (`gnirehtet.exe` e `gnirehtet.apk`).

### 2. Compartilhar Internet (Modo Manual)
Com um aparelho Android conectado via ADB:
1. Execute `2_iniciar_internet.bat`.
2. O tablet pedirá uma confirmação de "Solicitação de Conexão VPN" (Isso é normal, o Android cria uma VPN local via USB para puxar a internet do PC).
3. Aceite no tablet. Pronto! O tablet agora tem internet do seu PC.

## Integração no Motor C (Para o Futuro)
Se no futuro quisermos integrar isso em um botão `[F3 - Ativar Internet USB]` na plataforma InnovPlay, bastará adicionar no Motor C:
```c
// Comando ADB para injetar a internet no aparelho:
adb_exec("-s SERIAL shell am start -a android.intent.action.MAIN -n com.genymobile.gnirehtet/.GnirehtetActivity");
// Em seguida, iniciar a ponte no PC:
system("tools\\reverse_tethering\\gnirehtet.exe run");
```
