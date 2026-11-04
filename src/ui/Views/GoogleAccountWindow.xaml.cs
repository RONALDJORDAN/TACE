using System;
using System.Windows;

namespace TACEUI.Views
{
    public partial class GoogleAccountWindow : Window
    {
        public string Email { get; private set; } = string.Empty;
        public string Senha { get; private set; } = string.Empty;

        public GoogleAccountWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtEmail.Focus();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            TxtErro.Visibility = Visibility.Collapsed;
            string email = TxtEmail.Text.Trim();
            string senha = TxtSenha.Password;

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                MostrarErro("Informe um e-mail Google valido.");
                return;
            }
            if (email.Length > 200)
            {
                MostrarErro("O e-mail informado e muito longo.");
                return;
            }
            if (string.IsNullOrEmpty(senha))
            {
                MostrarErro("Informe a senha da conta Google.");
                return;
            }
            if (senha.Length > 200)
            {
                MostrarErro("A senha informada e muito longa.");
                return;
            }

            Email = email;
            Senha = senha;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Senha = string.Empty;
            TxtSenha.Clear();
            DialogResult = false;
        }

        public void ClearPassword()
        {
            Senha = string.Empty;
            TxtSenha.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult != true)
            {
                Senha = string.Empty;
                TxtSenha.Clear();
            }
            base.OnClosed(e);
        }

        private void MostrarErro(string mensagem)
        {
            TxtErro.Text = mensagem;
            TxtErro.Visibility = Visibility.Visible;
        }
    }
}
