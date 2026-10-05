using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BotRpaUI.Views
{
    public partial class MunicipiosWindow : Window
    {
        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetMunicipios([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] MunicipioModel[] out_lista, int max_items);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_SaveMunicipios([In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] MunicipioModel[] lista, int count);

        private ObservableCollection<MunicipioModel> _lista;
        private bool _isUpdating = false;
        private bool _isCopying = false;

        public MunicipiosWindow()
        {
            InitializeComponent();
            _lista = new ObservableCollection<MunicipioModel>();
            ListMunicipios.ItemsSource = _lista;
            
            CarregarBanco();
        }

        private void CarregarBanco()
        {
            try
            {
                MunicipioModel[] buffer = new MunicipioModel[256];
                int count = Engine_GetMunicipios(buffer, 256);

                // Ordenação Alfabética Estrita pelo Nome do Município (A-Z)
                Array.Sort(buffer, 0, count, Comparer<MunicipioModel>.Create((a, b) => string.Compare(a.Nome, b.Nome, StringComparison.CurrentCultureIgnoreCase)));
                
                _lista.Clear();
                for (int i = 0; i < count; i++)
                {
                    _lista.Add(buffer[i]);
                }

                if (_lista.Count > 0)
                    ListMunicipios.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar municípios: " + ex.Message);
            }
        }

        private void SalvarBanco()
        {
            try
            {
                MunicipioModel[] array = _lista.ToArray();
                Engine_SaveMunicipios(array, array.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar municípios: " + ex.Message);
            }
        }

        private void ListMunicipios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isCopying || _isUpdating) return;

            if (ListMunicipios.SelectedIndex >= 0)
            {
                var m = _lista[ListMunicipios.SelectedIndex];
                TxtNome.Text = m.Nome;
                TxtLogin.Text = m.Login;
                TxtSenha.Text = m.Senha;
                TxtWallpaper.Text = System.IO.Path.GetFileName(m.WallpaperPath);
                TxtWallpaper.Tag = m.WallpaperPath;
                
                string safeName = BotRpaUI.Core.PathHelper.SanitizeFolderName(m.Nome);
                string safeNameAscii = BotRpaUI.Core.PathHelper.SanitizeFolderNameAscii(m.Nome);
                string root = BotRpaUI.Core.PathHelper.GetRootDirectory();
                
                string fullApk = BotRpaUI.Core.PathHelper.GetDynamicApkPath(m.Nome);
                TxtApk.Text = !string.IsNullOrEmpty(fullApk) ? System.IO.Path.GetFileName(fullApk) : "";

                string fullPacote = BotRpaUI.Core.PathHelper.GetDynamicPacotePath(m.Nome);
                TxtPacote.Text = !string.IsNullOrEmpty(fullPacote) ? System.IO.Path.GetFileName(fullPacote) : "";
            }
            else
            {
                TxtNome.Text = "";
                TxtLogin.Text = "";
                TxtSenha.Text = "";
                TxtApk.Text = "";
                TxtPacote.Text = "";
                TxtWallpaper.Text = "";
                TxtWallpaper.Tag = "";
            }
        }

        private void BtnNovo_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            _isUpdating = true;
            ListMunicipios.SelectedIndex = -1;
            TxtNome.Text = "Novo Municipio";
            TxtLogin.Text = "";
            TxtSenha.Text = "";
            TxtApk.Text = "";
            TxtPacote.Text = "";
            TxtWallpaper.Text = "";
            TxtWallpaper.Tag = "";
            _isUpdating = false;
            TxtNome.Focus();
        }

        private async Task CopiarArquivoComProgressoAsync(string sourcePath, string targetPath, IProgress<double> progress)
        {
            const int bufferSize = 1024 * 1024; // 1MB buffer
            byte[] buffer = new byte[bufferSize];

            using (FileStream sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
            using (FileStream targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            {
                long totalBytes = sourceStream.Length;
                long totalBytesCopied = 0;
                int bytesRead;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await targetStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesCopied += bytesRead;
                    
                    if (totalBytes > 0)
                    {
                        double pct = ((double)totalBytesCopied / totalBytes) * 100.0;
                        progress.Report(pct);
                    }
                }
            }
        }

        private async void CopiarArquivo(TextBox targetTextBox, string filter)
        {
            if (_isCopying) return;

            if (string.IsNullOrWhiteSpace(TxtNome.Text) || TxtNome.Text == "Novo Municipio")
            {
                MessageBox.Show("Por favor, preencha um nome válido para o Município antes de anexar arquivos.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string safeName = BotRpaUI.Core.PathHelper.SanitizeFolderNameAscii(TxtNome.Text);
            
            // Diretório padronizado externo baseando-se no tipo de arquivo
            string tipoPasta = filter.Contains(".apk") ? "apks" : "pacotes";
            string externalBaseDir = System.IO.Path.Combine(BotRpaUI.Core.PathHelper.GetRootDirectory(), "data", "output", "arquivos", tipoPasta);
            string externalMunicipioDir = System.IO.Path.Combine(externalBaseDir, safeName);
            
            try
            {
                if (!System.IO.Directory.Exists(externalMunicipioDir))
                    System.IO.Directory.CreateDirectory(externalMunicipioDir);
            }
            catch {}

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = filter;
            if (System.IO.Directory.Exists(externalMunicipioDir))
            {
                dlg.InitialDirectory = externalMunicipioDir;
            }
            
            if (dlg.ShowDialog() == true)
            {
                // Bloquear cliques e interações por flag para não deixar a tela branca
                _isCopying = true;
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                GridCopyProgress.Visibility = Visibility.Visible;
                BarCopyProgress.Value = 0;
                try
                {
                    string fileName = System.IO.Path.GetFileName(dlg.FileName);
                    string externalPath = System.IO.Path.Combine(externalMunicipioDir, fileName);

                    var progress = new Progress<double>(val =>
                    {
                        BarCopyProgress.Value = val;
                    });

                    // 1. Limpa arquivos antigos de TODAS as pastas candidatas do município para não deixar resíduos fantasma
                    string root = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    string rawName = TxtNome.Text.Trim();
                    string cleanName = BotRpaUI.Core.PathHelper.SanitizeFolderName(rawName);
                    string extPattern = filter.Contains(".apk") ? "*.apk" : "*.zip";

                    string[] candidatePurgeDirs = new string[] {
                        externalMunicipioDir,
                        System.IO.Path.Combine(root, "data", "output", "arquivos", tipoPasta, cleanName),
                        System.IO.Path.Combine(root, "Arquivos", rawName),
                        System.IO.Path.Combine(root, "Arquivos", cleanName),
                        System.IO.Path.Combine(root, "Arquivos", safeName),
                        System.IO.Path.Combine(root, "data", "input", "municipios", rawName),
                        System.IO.Path.Combine(root, "data", "input", "municipios", cleanName),
                        System.IO.Path.Combine(root, "data", "input", "municipios", safeName)
                    };

                    foreach (var pDir in candidatePurgeDirs)
                    {
                        if (System.IO.Directory.Exists(pDir))
                        {
                            foreach (var f in System.IO.Directory.GetFiles(pDir, extPattern))
                            {
                                try { System.IO.File.Delete(f); } catch {}
                            }
                        }
                    }

                    // 2. Copia para a pasta padronizada externa
                    if (System.IO.Path.GetFullPath(dlg.FileName).ToLower() != System.IO.Path.GetFullPath(externalPath).ToLower())
                    {
                        TxtCopyStatus.Text = $"Arquivando {fileName} em arquivos/{tipoPasta}...";
                        await CopiarArquivoComProgressoAsync(dlg.FileName, externalPath, progress);
                    }

                    // Salva apenas o nome do arquivo no Text
                    targetTextBox.Text = fileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao copiar arquivo: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    GridCopyProgress.Visibility = Visibility.Collapsed;
                    System.Windows.Input.Mouse.OverrideCursor = null;
                    _isCopying = false;
                }
            }
        }

        private void MostrarDetalhes(TextBox targetTextBox, string fileDescription)
        {
            string? relativePath = targetTextBox.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                MessageBox.Show($"Nenhum arquivo anexado para {fileDescription}.", "Detalhes do Arquivo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string root = BotRpaUI.Core.PathHelper.GetRootDirectory();
                string fullPath = System.IO.Path.Combine(root, relativePath.Replace('/', '\\'));
                string fileName = System.IO.Path.GetFileName(relativePath);
                string safeName = string.Join("_", TxtNome.Text.Split(System.IO.Path.GetInvalidFileNameChars()));
                string externalPath = System.IO.Path.Combine(root, "Arquivos", safeName, fileName);

                bool existsInternal = System.IO.File.Exists(fullPath);
                bool existsExternal = System.IO.File.Exists(externalPath);

                long sizeBytes = existsInternal ? new System.IO.FileInfo(fullPath).Length : (existsExternal ? new System.IO.FileInfo(externalPath).Length : 0);
                string sizeStr = sizeBytes > 0 ? $"{((double)sizeBytes / 1024.0 / 1024.0):F2} MB" : "Não disponível";

                string statusInternal = existsInternal ? "Disponível para Execução (OK)" : "NÃO ENCONTRADO (Execução)";
                string statusExternal = existsExternal ? "Arquivado com Sucesso (OK)" : "NÃO ENCONTRADO (Arquivo)";

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"=== Detalhes do Arquivo ({fileDescription}) ===");
                sb.AppendLine($"Nome: {fileName}");
                sb.AppendLine($"Tamanho: {sizeStr}");
                sb.AppendLine();
                sb.AppendLine($"[Caminho Interno (Execução RPA)]");
                sb.AppendLine($"Status: {statusInternal}");
                sb.AppendLine($"Relativo: {relativePath}");
                sb.AppendLine($"Físico: {fullPath}");
                sb.AppendLine();
                sb.AppendLine($"[Caminho Mestre (Organização)]");
                sb.AppendLine($"Status: {statusExternal}");
                sb.AppendLine($"Físico: {externalPath}");

                MessageBox.Show(sb.ToString(), $"Detalhes do Arquivo - {fileName}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar detalhes: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDetalhesApk_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            MostrarDetalhes(TxtApk, "APK da Aplicação");
        }

        private void BtnDetalhesPacote_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            MostrarDetalhes(TxtPacote, "Pacote de Dados ZIP");
        }

        private void BtnDetalhesWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            MostrarDetalhes(TxtWallpaper, "Imagem de Wallpaper");
        }

        private void BtnProcurarApk_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            CopiarArquivo(TxtApk, "Arquivos APK (*.apk)|*.apk|Todos os Arquivos (*.*)|*.*");
        }

        private void BtnProcurarPacote_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            CopiarArquivo(TxtPacote, "Arquivos ZIP/Dados (*.zip;*.tar;*.gz)|*.zip;*.tar;*.gz|Todos os Arquivos (*.*)|*.*");
        }

        private void BtnProcurarWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            CopiarArquivo(TxtWallpaper, "Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos os Arquivos (*.*)|*.*");
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            int idx = ListMunicipios.SelectedIndex;
            if (idx >= 0 && idx < _lista.Count)
            {
                var target = _lista[idx];
                string municipioNome = target.Nome;

                var result = MessageBox.Show($"Tem certeza que deseja excluir o município '{municipioNome}' e TODOS os seus arquivos e pastas locais?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    BotRpaUI.Core.PathHelper.DeleteMunicipioFolders(municipioNome);
                    _lista.RemoveAt(idx);
                    SalvarBanco();
                    MessageBox.Show($"Município '{municipioNome}' e suas pastas de arquivos foram excluídos com sucesso!", "Bot_RPA", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                MessageBox.Show("O nome do município não pode estar vazio.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MunicipioModel novo = new MunicipioModel
            {
                Nome = TxtNome.Text.Trim(),
                Login = TxtLogin.Text.Trim(),
                Senha = TxtSenha.Text.Trim(),
                WallpaperPath = TxtWallpaper.Tag?.ToString() ?? ""
            };

            int idx = ListMunicipios.SelectedIndex;
            if (idx >= 0)
            {
                _lista[idx] = novo; // Atualiza existente
            }
            else
            {
                _lista.Add(novo); // Insere novo
                ListMunicipios.SelectedItem = novo;
            }

            SalvarBanco();
            MessageBox.Show("Dados salvos com sucesso no cofre!", "Bot_RPA", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
