using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using TACEUI.Core;

namespace TACEUI.Views
{
    public sealed class PortaUsbNomeItem
    {
        public string Porta { get; set; } = string.Empty;
        public string PortaExibicao => UsbPortNameService.ObterRotuloCurto(Porta);
        public string Detalhe { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
    }

    public partial class UsbPortNamingWindow : Window
    {
        private readonly ObservableCollection<PortaUsbNomeItem> _itens = new ObservableCollection<PortaUsbNomeItem>();

        public UsbPortNamingWindow(IEnumerable<KeyValuePair<string, string>> portasDaSessao)
        {
            InitializeComponent();

            var nomesSalvos = UsbPortNameService.ObterTodos();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var porta in portasDaSessao)
            {
                string chave = (porta.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(chave) || chave == "N/A" || !vistos.Add(chave)) continue;

                nomesSalvos.TryGetValue(chave, out string? nome);
                _itens.Add(new PortaUsbNomeItem
                {
                    Porta = chave,
                    Detalhe = porta.Value ?? string.Empty,
                    Nome = nome ?? string.Empty
                });
            }

            foreach (var salvo in nomesSalvos)
            {
                if (!vistos.Add(salvo.Key)) continue;
                _itens.Add(new PortaUsbNomeItem
                {
                    Porta = salvo.Key,
                    Detalhe = "sem aparelho conectado no momento",
                    Nome = salvo.Value
                });
            }

            var ordenada = _itens.OrderBy(x => x.Porta, StringComparer.OrdinalIgnoreCase).ToList();
            _itens.Clear();
            foreach (var item in ordenada) _itens.Add(item);

            ListPortas.ItemsSource = _itens;
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            UsbPortNameService.SalvarNomes(
                _itens.Select(i => new KeyValuePair<string, string>(i.Porta, i.Nome ?? string.Empty)));
            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
