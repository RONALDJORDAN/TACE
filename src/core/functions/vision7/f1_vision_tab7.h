#ifndef F1_VISION_TAB7_H
#define F1_VISION_TAB7_H

#include "../pipeline.h"

// =============================================================================
//  PIPELINE F1: POSITIVO TAB VISION 7 (ANDROID 14)
//  Plataforma TACE - TACE Engine
//  Isolamento Estrito por Equipamento (Regra de Ouro #1)
// =============================================================================

/**
 * @brief Orquestrador Mestre do Pipeline F1 (Instalação e Configuração Completa).
 * 
 * Executa sequencialmente todas as etapas de preparação, debloat, envio de arquivos,
 * instalação, login com proxy reverso (Gnirehtet), importação de pacote ZIP,
 * aplicação de wallpaper municipal e finalização segura do dispositivo.
 * 
 * @param serial       Serial identificador do dispositivo no ADB.
 * @param apk_path     Caminho local absoluto do arquivo APK da aplicação.
 * @param pacote_path  Caminho local absoluto do pacote avaliativo (.ZIP).
 * @param wp_path      Caminho local absoluto do wallpaper customizado (opcional).
 * @param package_name Nome do pacote Android (ex: "br.com.yantec.innovava").
 * @param login        E-mail ou identificador de acesso do operador/município.
 * @param senha        Senha de autenticação da aplicação.
 * @param municipio    Nome do município cadastrado para matching e login.
 * @param cb           Callback de telemetria e log de eventos para a interface WPF.
 * @return int         PIPELINE_SUCCESS (1) em caso de sucesso ou PIPELINE_ERROR (0).
 */
int V7_ExecutarPipelineCompleto(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb);

// =============================================================================
//  DETALHAMENTO DE CADA UMA DAS ETAPAS DO PIPELINE F1
// =============================================================================

/**
 * @brief [Etapa 1] Desbloqueio e Preparação de Tela.
 * - Verifica o estado da tela (Dozing/Asleep) e acorda via KEYCODE_POWER se necessário.
 * - Desbloqueia a tela através de swipe vertical simulado (300,1000 -> 300,300).
 * - Trava a orientação da tela em modo Retrato/Vertical (Portrait), desativa a
 *   rotação automática e define o timeout de tela para 5 minutos (300000ms).
 */
int V7_Etapa1(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 1.2] Ativação do Modo Noturno / Dark Mode.
 * - Habilita o tema escuro nativo do Android (`cmd uimode night yes` e `ui_night_mode 2`),
 *   reduzindo o cansaço visual e otimizando a durabilidade da bateria.
 */
int V7_Etapa1_2_DarkMode(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 1.5] Isolamento de Bloatwares e Apps Google Dispensáveis.
 * - Desativa, suspende e desinstala em lote (batched) pacotes desnecessários
 *   (Câmeras, Calculadora, Chrome, Contatos, Drive, Google TV, Maps, YouTube, Play Store, Agenda, Assistente, etc.).
 * - Preserva estritamente os componentes de sistema e o seletor DocumentsUI (SAF).
 */
int V7_Etapa1_5(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 1.6] Limpeza de Armazenamento e Cópia de Arquivos.
 * - Executa a purga do diretório /sdcard/ (preservando Android e Pictures).
 * - Recria o diretório de trabalho seguro `/sdcard/avaliacoes/`.
 * - Realiza o upload (ADB push) do APK e do Pacote ZIP com sanitização de nomes (8.3).
 */
int V7_Etapa1_6(const char *serial, const char *apk_path, const char *pacote_path, LogCallback cb);

/**
 * @brief [Etapa 1.7] Otimização de Hardware, Energia e Redes.
 * - Desativa conexões não essenciais (Wi-Fi, Bluetooth, Localização GPS, escaneamento BLE).
 * - Ajusta o brilho da tela para 70% (178/255) e define a escala de fonte para 1.5.
 */
int V7_Etapa1_7(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 1.9] Limpeza do Launcher e Organização da Área de Trabalho.
 * - Limpa o cache e dados do inicializador (`pm clear com.android.launcher3`),
 *   removendo ícones fantasmas e preparando a tela inicial para o atalho limpo.
 */
int V7_Etapa1_9_ClearLauncher(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 2] Instalação do Aplicativo Principal.
 * - Executa a instalação silenciosa do APK (`adb install -r -d`).
 * - Valida o retorno do gerenciador de pacotes (Package Manager) garantindo sucesso.
 */
int V7_Etapa2(const char *serial, const char *apk_path, const char *package_name, LogCallback cb);

/**
 * @brief [Etapa 2.1] Criação de Atalho na Tela Inicial.
 * - Dispara broadcast de instalação de atalho para o Launcher com o nome da aplicação.
 */
int V7_Etapa2_1_AddShortcut(const char *serial, const char *package_name, LogCallback cb);

/**
 * @brief [Etapa 2.5] Inicialização do Reverse Tethering (Ponte de Internet).
 * - Configura e inicia o túnel Gnirehtet, compartilhando a conexão de internet do PC
 *   com o tablet Android via USB sem necessidade de Wi-Fi ativo.
 */
int V7_Etapa2_5(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 3] Concessão de Permissões e Inicialização do Aplicativo.
 * - Concede todas as permissões de tempo de execução necessárias (armazenamento, câmera, etc.).
 * - Inicia a Activity principal da aplicação via comando `am start`.
 */
int V7_Etapa3(const char *serial, const char *package_name, LogCallback cb);

/**
 * @brief [Etapa 4] Autenticação e Login Automático.
 * - Localiza os campos visuais de E-mail/Usuário, Senha e Seleção de Município.
 * - Digita as credenciais de forma segura e aciona o botão de login através de
 *   estratégias determinísticas com fallback em UIAutomator.
 */
int V7_Etapa4(const char *serial, const char *login, const char *senha, const char *municipio, LogCallback cb);

/**
 * @brief [Etapa 4 - Variante F2] Autenticação sem revalidação de município.
 * - Comportamento idêntico a V7_Etapa4, exceto que PULA o bloco de
 *   validação/re-seleção do município após a senha (pré-clique Entrar).
 * - Uso exclusivo do pipeline F2 Vision 7 (f2_vision_tab7.c).
 * - F1 permanece chamando V7_Etapa4 (com recheck) — zero efeito colateral.
 */
int V7_Etapa4_SkipMunicipioRecheck(const char *serial, const char *login, const char *senha, const char *municipio, LogCallback cb);

/**
 * @brief [Etapa 4.5] Encerramento do Reverse Tethering.
 * - Desconecta e finaliza o túnel Gnirehtet com segurança após o login ser concluído.
 */
int V7_Etapa4_5_StopGnirehtet(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 5] Navegação para Importação do Pacote Avaliativo.
 * - Navega pelos menus internos da aplicação até a tela de importação de banco de dados ZIP.
 */
int V7_Etapa5(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 6] Seleção do Pacote ZIP no Seletor de Arquivos (SAF).
 * - Interage com o gerenciador de arquivos nativo (DocumentsUI), localiza o pacote
 *   em `/sdcard/avaliacoes/` e confirma a importação do conteúdo avaliativo.
 */
int V7_Etapa6(const char *serial, LogCallback cb);

/**
 * @brief [Etapa 7a] Fechamento de Aplicativos e Transição.
 * - Aguarda a finalização da extração do banco de dados, encerra os aplicativos
 *   abertos em segundo plano e prepara o encerramento do pipeline.
 */
int F1_Etapa7_CloseApps(const char *serial, const char *package_name, LogCallback cb);

/**
 * @brief [Etapa 7b] Finalização Segura e Desligamento Remoto.
 * - Aplica purga final de memória, garante antenas desligadas e emite o desligamento
 *   completo do dispositivo via comando `reboot -p`.
 */
int V7_Etapa7_PowerOff(const char *serial, LogCallback cb);

// =============================================================================
//  FUNÇÕES AUXILIARES / HELPERS EXPORTADOS
// =============================================================================

/**
 * @brief Extrai coordenadas de centroide (X, Y) a partir do XML de UIAutomator.
 */
int get_centroid_from_xml(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count);

/**
 * @brief Realiza o dump da hierarquia de tela atual do Android e retorna o XML em memória.
 */
char *dump_and_read_xml(const char *serial, LogCallback cb);

#endif // F1_VISION_TAB7_H
