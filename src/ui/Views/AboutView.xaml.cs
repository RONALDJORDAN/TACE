using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InnovPlayUI.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
            
            // Define a versão atualizada no card de cabeçalho
            try
            {
                TxtAboutVersion.Text = $"v{Core.OtaUpdateManager.CurrentVersion}";
            }
            catch { }
        }

        private void BtnTabGuide_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(PanelGuide, BtnTabGuide);
        }

        private void BtnTabFaq_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(PanelFaq, BtnTabFaq);
        }

        private void BtnTabTech_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(PanelTech, BtnTabTech);
        }

        private void SwitchTab(StackPanel targetPanel, Button targetButton)
        {
            // Oculta todos os painéis
            PanelGuide.Visibility = Visibility.Collapsed;
            PanelFaq.Visibility = Visibility.Collapsed;
            PanelTech.Visibility = Visibility.Collapsed;

            // Restaura o estilo neutro de todos os botões de sub-aba
            ResetButtonStyle(BtnTabGuide);
            ResetButtonStyle(BtnTabFaq);
            ResetButtonStyle(BtnTabTech);

            // Exibe o painel selecionado
            targetPanel.Visibility = Visibility.Visible;

            // Aplica estilo ativo ao botão selecionado
            targetButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#513BA6"));
            targetButton.Foreground = Brushes.White;
        }

        private void ResetButtonStyle(Button btn)
        {
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#151525"));
            btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A9E"));
        }
    }
}
