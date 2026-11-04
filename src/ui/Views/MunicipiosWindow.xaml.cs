using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TACEUI.Core;

namespace TACEUI.Views
{
    public partial class MunicipiosWindow : Window
    {
        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetMunicipios([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] MunicipioModel[] out_lista, int max_items);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_SaveMunicipios([In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] MunicipioModel[] lista, int count);

        private const int LimiteMunicipios = 256;
        private static readonly SolidColorBrush CorAnexado = CriarCor("#4EDEA3");
        private static readonly SolidColorBrush CorPendente = CriarCor("#6E7681");
        private static readonly SolidColorBrush CorErro = CriarCor("#FF8080");

        private readonly ObservableCollection<MunicipioModel> _lista = new ObservableCollection<MunicipioModel>();
        private readonly ICollectionView _view;
        private bool _isUpdating;
        private bool _isCopying;
        private string? _nomeEmEdicao;

        public MunicipiosWindow()
        {
            _view = CollectionViewSource.GetDefaultView(_lista);
            _view.Filter = FiltrarMunicipio;

            InitializeComponent();
            ListMunicipios.ItemsSource = _view;

            CarregarBanco();
        }

        private static SolidColorBrush CriarCor(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static bool MesmoNome(string? a, string? b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.CurrentCultureIgnoreCase);
        }

        private bool FiltrarMunicipio(object obj)
        {
            if (obj is not MunicipioModel municipio) return false;

            string termo = (TxtBuscaMunicipio?.Text ?? "").Trim();
            if (termo.Length == 0) return true;

            return (municipio.Nome ?? "").Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                || (municipio.Login ?? "").Contains(termo, StringComparison.CurrentCultureIgnoreCase);
        }

        private void CarregarBanco()
        {
            try
            {
                var buffer = new MunicipioModel[LimiteMunicipios];
                int count = Engine_GetMunicipios(buffer, buffer.Length);
                count = Math.Clamp(count, 0, buffer.Length);

                var ordenados = buffer
                    .Take(count)
                    .OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                _lista.Clear();
                foreach (var municipio in ordenados)
                {
                    _lista.Add(municipio);
                }

                _view.Refresh();
                AtualizarContagem();

                var primeiro = _view.Cast<MunicipioModel>().FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(primeiro.Nome))
                {
                    SelecionarPorNome(primeiro.Nome);
                }
                else
                {
                    LimparFormulario();
                }

                TxtStatusRodape.Text = count == 0
                    ? "Nenhum município cadastrado."
                    : $"{count} município(s) carregado(s).";
                TxtStatusRodape.Foreground = CorPendente;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar municípios: " + ex.Message, "Gerenciar Municípios",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SalvarBanco()
        {
            try
            {
                var array = _lista.ToArray();
                int resultado = Engine_SaveMunicipios(array, array.Length);
                if (resultado == 0)
                {
                    MessageBox.Show("O motor não conseguiu gravar municipios.csv. Verifique as permissões da pasta de dados.",
                        "Gerenciar Municípios", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar municípios: " + ex.Message, "Gerenciar Municípios",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void AtualizarContagem()
        {
            if (TxtMunicipiosCount == null) return;

            int quantidade = _view.Cast<object>().Count();
            TxtMunicipiosCount.Text = quantidade.ToString();
            if (TxtListaVazia != null)
            {
                TxtListaVazia.Visibility = quantidade == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SelecionarPorNome(string nome)
        {
            var item = _lista.FirstOrDefault(m => MesmoNome(m.Nome, nome));
            if (string.IsNullOrWhiteSpace(item.Nome)) return;

            ListMunicipios.SelectedItem = item;
            ListMunicipios.ScrollIntoView(item);
        }

        private void TxtBuscaMunicipio_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ListMunicipios == null) return;

            _view.Refresh();
            AtualizarContagem();

            if (ListMunicipios.SelectedItem is MunicipioModel atual
                && _view.Cast<MunicipioModel>().Any(m => MesmoNome(m.Nome, atual.Nome)))
            {
                return;
            }

            var primeiro = _view.Cast<MunicipioModel>().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(primeiro.Nome))
            {
                SelecionarPorNome(primeiro.Nome);
            }
        }

        private void ListMunicipios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isCopying || _isUpdating) return;

            if (ListMunicipios.SelectedItem is MunicipioModel municipio && !string.IsNullOrWhiteSpace(municipio.Nome))
            {
                PreencherFormulario(municipio);
            }
            else if (ListMunicipios.SelectedItem == null)
            {
                LimparFormulario();
            }
        }

        private void PreencherFormulario(MunicipioModel municipio)
        {
            _isUpdating = true;
            _nomeEmEdicao = municipio.Nome;

            TxtFormTitulo.Text = "Editar município";
            TxtFormSubtitulo.Text = $"Atualize os dados de {municipio.Nome}.";
            TxtNome.Text = municipio.Nome;
            TxtNome.IsReadOnly = true;
            TxtNomeHint.Text = "O nome é a chave das pastas vinculadas; para renomear, cadastre um novo município.";
            TxtLogin.Text = municipio.Login;
            PwdSenha.Password = municipio.Senha;

            string apk = PathHelper.GetDynamicApkPath(municipio.Nome);
            string pacote = PathHelper.GetDynamicPacotePath(municipio.Nome);
            string wallpaper = ResolverCaminho(municipio.WallpaperPath);

            TxtApk.Text = apk.Length > 0 ? Path.GetFileName(apk) : "";
            TxtApk.Tag = apk;
            TxtPacote.Text = pacote.Length > 0 ? Path.GetFileName(pacote) : "";
            TxtPacote.Tag = pacote;
            TxtWallpaper.Text = wallpaper.Length > 0 ? Path.GetFileName(wallpaper) : "";
            TxtWallpaper.Tag = municipio.WallpaperPath;

            AtualizarStatusArquivo(TxtApkStatus, apk);
            AtualizarStatusArquivo(TxtPacoteStatus, pacote);
            AtualizarStatusArquivo(TxtWallpaperStatus, wallpaper);

            BtnExcluir.IsEnabled = true;
            TxtStatusRodape.Text = "Editando município selecionado.";
            TxtStatusRodape.Foreground = CorPendente;
            _isUpdating = false;
        }

        private void LimparFormulario()
        {
            _isUpdating = true;
            _nomeEmEdicao = null;

            TxtFormTitulo.Text = "Novo município";
            TxtFormSubtitulo.Text = "Preencha a identificação e anexe os arquivos do RPA.";
            TxtNome.IsReadOnly = false;
            TxtNome.Clear();
            TxtNomeHint.Text = "O nome identifica as pastas de APK, pacote e wallpaper.";
            TxtLogin.Clear();
            PwdSenha.Clear();

            TxtApk.Clear();
            TxtPacote.Clear();
            TxtWallpaper.Clear();
            TxtApk.Tag = null;
            TxtPacote.Tag = null;
            TxtWallpaper.Tag = null;

            AtualizarStatusArquivo(TxtApkStatus, "");
            AtualizarStatusArquivo(TxtPacoteStatus, "");
            AtualizarStatusArquivo(TxtWallpaperStatus, "");

            BtnExcluir.IsEnabled = false;
            ListMunicipios.SelectedItem = null;
            TxtStatusRodape.Text = "Selecione um município ou crie um novo.";
            TxtStatusRodape.Foreground = CorPendente;
            _isUpdating = false;
        }

        private static string ResolverCaminho(string? caminho)
        {
            if (string.IsNullOrWhiteSpace(caminho)) return "";

            try
            {
                string valor = caminho.Trim();
                return Path.IsPathRooted(valor)
                    ? valor
                    : Path.GetFullPath(Path.Combine(PathHelper.GetRootDirectory(), valor));
            }
            catch
            {
                return caminho;
            }
        }

        private void AtualizarStatusArquivo(TextBlock destino, string? caminho)
        {
            bool anexado = !string.IsNullOrWhiteSpace(caminho) && File.Exists(caminho);
            destino.Text = anexado ? "Anexado" : "Não anexado";
            destino.Foreground = anexado ? CorAnexado : CorPendente;
        }

        private void BtnNovo_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;

            if (_lista.Count >= LimiteMunicipios)
            {
                MessageBox.Show($"O limite de {LimiteMunicipios} municípios foi atingido.", "Gerenciar Municípios",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtBuscaMunicipio.Clear();
            LimparFormulario();
            TxtNome.Focus();
        }

        private (string Pasta, string Descricao, string[] Extensoes) ObterTipoArquivo(TextBox destino)
        {
            if (ReferenceEquals(destino, TxtApk))
                return ("apks", "APK", new[] { ".apk" });

            if (ReferenceEquals(destino, TxtPacote))
                return ("pacotes", "pacote de dados", new[] { ".zip", ".tar", ".gz" });

            return ("wallpapers", "wallpaper", new[] { ".png", ".jpg", ".jpeg" });
        }

        private TextBlock ObterStatusArquivo(TextBox destino)
        {
            if (ReferenceEquals(destino, TxtApk)) return TxtApkStatus;
            if (ReferenceEquals(destino, TxtPacote)) return TxtPacoteStatus;
            return TxtWallpaperStatus;
        }

        private static void PurgarArquivos(string[] pastas, string[] extensoes, string arquivoPreservado)
        {
            var permitidas = new HashSet<string>(extensoes, StringComparer.OrdinalIgnoreCase);
            string preservado = Path.GetFullPath(arquivoPreservado);

            foreach (string pasta in pastas.Where(Directory.Exists))
            {
                foreach (string arquivo in Directory.GetFiles(pasta))
                {
                    if (!permitidas.Contains(Path.GetExtension(arquivo))) continue;
                    if (string.Equals(Path.GetFullPath(arquivo), preservado, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(arquivo); } catch { }
                }
            }
        }

        private async Task CopiarArquivoComProgressoAsync(string sourcePath, string targetPath, IProgress<double> progress)
        {
            const int bufferSize = 1024 * 1024;
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

            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                MessageBox.Show("Informe o nome do município antes de anexar arquivos.", "Atenção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNome.Focus();
                return;
            }

            string safeName = PathHelper.SanitizeFolderNameAscii(TxtNome.Text);
            var tipo = ObterTipoArquivo(targetTextBox);
            string root = PathHelper.GetRootDirectory();
            string externalBaseDir = Path.Combine(root, "data", "output", "arquivos", tipo.Pasta);
            string externalMunicipioDir = Path.Combine(externalBaseDir, safeName);

            try
            {
                Directory.CreateDirectory(externalMunicipioDir);
            }
            catch { }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = Directory.Exists(externalMunicipioDir) ? externalMunicipioDir : ""
            };

            if (dialog.ShowDialog() != true) return;

            _isCopying = true;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            GridCopyProgress.Visibility = Visibility.Visible;
            BarCopyProgress.Value = 0;

            try
            {
                string fileName = Path.GetFileName(dialog.FileName);
                string externalPath = Path.Combine(externalMunicipioDir, fileName);
                string rawName = TxtNome.Text.Trim();
                string cleanName = PathHelper.SanitizeFolderName(rawName);
                string cleanAscii = PathHelper.SanitizeFolderNameAscii(rawName);

                string[] candidateDirs =
                {
                    externalMunicipioDir,
                    Path.Combine(root, "data", "output", "arquivos", tipo.Pasta, cleanName),
                    Path.Combine(root, "data", "output", "arquivos", tipo.Pasta, cleanAscii),
                    Path.Combine(root, "Arquivos", rawName),
                    Path.Combine(root, "Arquivos", cleanName),
                    Path.Combine(root, "Arquivos", cleanAscii),
                    Path.Combine(root, "data", "input", "municipios", rawName),
                    Path.Combine(root, "data", "input", "municipios", cleanName),
                    Path.Combine(root, "data", "input", "municipios", cleanAscii)
                };

                PurgarArquivos(candidateDirs, tipo.Extensoes, dialog.FileName);

                if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(externalPath), StringComparison.OrdinalIgnoreCase))
                {
                    TxtCopyStatus.Text = $"Arquivando {fileName} em arquivos/{tipo.Pasta}...";
                    var progress = new Progress<double>(valor => BarCopyProgress.Value = valor);
                    await CopiarArquivoComProgressoAsync(dialog.FileName, externalPath, progress);
                }

                targetTextBox.Text = fileName;
                targetTextBox.Tag = externalPath;
                AtualizarStatusArquivo(ObterStatusArquivo(targetTextBox), externalPath);
                TxtStatusRodape.Text = $"{tipo.Descricao} anexado ao município.";
                TxtStatusRodape.Foreground = CorPendente;
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

        private void MostrarDetalhes(TextBox targetTextBox, string fileDescription)
        {
            string raw = targetTextBox.Tag?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show($"Nenhum arquivo anexado para {fileDescription}.", "Detalhes do arquivo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string fullPath = ResolverCaminho(raw);
                bool existe = File.Exists(fullPath);
                long tamanho = existe ? new FileInfo(fullPath).Length : 0;
                string tamanhoTexto = tamanho > 0 ? $"{tamanho / 1024.0 / 1024.0:F2} MB" : "Não disponível";

                var detalhes = new StringBuilder();
                detalhes.AppendLine($"Arquivo: {Path.GetFileName(fullPath)}");
                detalhes.AppendLine($"Tipo: {fileDescription}");
                detalhes.AppendLine($"Tamanho: {tamanhoTexto}");
                detalhes.AppendLine($"Status: {(existe ? "Disponível" : "Não encontrado")}");
                detalhes.AppendLine();
                detalhes.AppendLine($"Caminho completo:\n{fullPath}");

                MessageBox.Show(detalhes.ToString(), "Detalhes do arquivo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar detalhes: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDetalhesApk_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            MostrarDetalhes(TxtApk, "APK da aplicação");
        }

        private void BtnDetalhesPacote_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            MostrarDetalhes(TxtPacote, "Pacote de dados ZIP");
        }

        private void BtnDetalhesWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            MostrarDetalhes(TxtWallpaper, "Imagem de wallpaper");
        }

        private void BtnProcurarApk_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            CopiarArquivo(TxtApk, "Arquivos APK (*.apk)|*.apk|Todos os arquivos (*.*)|*.*");
        }

        private void BtnProcurarPacote_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            CopiarArquivo(TxtPacote, "Arquivos ZIP/dados (*.zip;*.tar;*.gz)|*.zip;*.tar;*.gz|Todos os arquivos (*.*)|*.*");
        }

        private void BtnProcurarWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            CopiarArquivo(TxtWallpaper, "Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos os arquivos (*.*)|*.*");
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;
            if (ListMunicipios.SelectedItem is not MunicipioModel alvo || string.IsNullOrWhiteSpace(alvo.Nome)) return;

            var confirmacao = MessageBox.Show(
                $"Excluir o município '{alvo.Nome}'?\n\nOs arquivos APK, pacote e wallpaper vinculados também serão removidos. Esta ação não pode ser desfeita.",
                "Excluir município",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmacao != MessageBoxResult.Yes) return;

            var existente = _lista.FirstOrDefault(m => MesmoNome(m.Nome, alvo.Nome));
            if (string.IsNullOrWhiteSpace(existente.Nome)) return;

            _lista.Remove(existente);
            if (!SalvarBanco())
            {
                CarregarBanco();
                return;
            }

            PathHelper.DeleteMunicipioFolders(alvo.Nome);
            _view.Refresh();
            AtualizarContagem();

            var proximo = _view.Cast<MunicipioModel>().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(proximo.Nome))
            {
                SelecionarPorNome(proximo.Nome);
            }
            else
            {
                LimparFormulario();
            }

            TxtStatusRodape.Text = $"Município '{alvo.Nome}' excluído.";
            TxtStatusRodape.Foreground = CorPendente;
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;

            string nome = (TxtNome.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(nome))
            {
                MostrarErroValidacao("Informe o nome do município.", TxtNome);
                return;
            }

            if (nome.Length > 120)
            {
                MostrarErroValidacao("O nome do município deve ter no máximo 120 caracteres.", TxtNome);
                return;
            }

            if (nome.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || nome.EndsWith(".", StringComparison.Ordinal) || nome.EndsWith(" ", StringComparison.Ordinal))
            {
                MostrarErroValidacao("O nome contém caracteres inválidos para as pastas de arquivos.", TxtNome);
                return;
            }

            string chavePasta = PathHelper.SanitizeFolderNameAscii(nome);
            bool duplicado = _lista.Any(m =>
                !MesmoNome(m.Nome, _nomeEmEdicao)
                && (MesmoNome(m.Nome, nome)
                    || MesmoNome(PathHelper.SanitizeFolderNameAscii(m.Nome), chavePasta)));
            if (duplicado)
            {
                MostrarErroValidacao("Já existe um município com esse nome.", TxtNome);
                return;
            }

            if (_nomeEmEdicao == null && _lista.Count >= LimiteMunicipios)
            {
                MostrarErroValidacao($"O limite de {LimiteMunicipios} municípios foi atingido.", null);
                return;
            }

            var novo = new MunicipioModel
            {
                Nome = nome,
                Login = (TxtLogin.Text ?? "").Trim(),
                Senha = PwdSenha.Password,
                WallpaperPath = TxtWallpaper.Tag?.ToString() ?? ""
            };

            int indice = -1;
            if (_nomeEmEdicao != null)
            {
                indice = _lista.ToList().FindIndex(m => MesmoNome(m.Nome, _nomeEmEdicao));
            }

            if (indice >= 0)
            {
                _lista[indice] = novo;
            }
            else
            {
                _lista.Add(novo);
            }

            OrdenarLista();

            if (!SalvarBanco())
            {
                CarregarBanco();
                return;
            }

            _nomeEmEdicao = nome;
            _view.Refresh();
            AtualizarContagem();
            SelecionarPorNome(nome);
            TxtStatusRodape.Text = $"Dados de '{nome}' salvos.";
            TxtStatusRodape.Foreground = CorPendente;
        }

        private void MostrarErroValidacao(string mensagem, Control? foco)
        {
            TxtStatusRodape.Text = mensagem;
            TxtStatusRodape.Foreground = CorErro;
            MessageBox.Show(mensagem, "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            foco?.Focus();
        }

        private void OrdenarLista()
        {
            var ordenada = _lista
                .OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _lista.Clear();
            foreach (var municipio in ordenada)
            {
                _lista.Add(municipio);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isCopying)
            {
                MessageBox.Show("Aguarde a cópia do arquivo terminar antes de fechar esta janela.",
                    "Gerenciar Municípios", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Cancel = true;
            }

            base.OnClosing(e);
        }
    }
}
