using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace TACEUI.Core
{
    /// <summary>
    /// Gera o relatório executivo vertical em PDF com capa, contracapa, sumário,
    /// indicadores, gráficos vetoriais, dados por dispositivo e anexo ordenado.
    /// </summary>
    public static class RelatorioPdfGenerator
    {
        private const double PageWidth = 595.28;
        private const double PageHeight = 841.89;
        private const double MarginX = 40;
        private const double ContentWidth = PageWidth - (MarginX * 2);
        private const double BottomY = 778;

        private const string Empresa = "InnovEdu";
        private const string Setor = "Setor Operacional";
        private const string Plataforma = "Plataforma TACE";

        private static readonly XColor CorTexto = XColor.FromArgb(28, 28, 40);
        private static readonly XColor CorSuave = XColor.FromArgb(105, 105, 125);
        private static readonly XColor CorRoxo = XColor.FromArgb(81, 59, 166);
        private static readonly XColor CorRoxoClaro = XColor.FromArgb(213, 205, 246);
        private static readonly XColor CorVerde = XColor.FromArgb(30, 160, 120);
        private static readonly XColor CorAzul = XColor.FromArgb(62, 132, 210);
        private static readonly XColor CorAmarelo = XColor.FromArgb(214, 150, 25);
        private static readonly XColor CorVermelho = XColor.FromArgb(190, 70, 80);
        private static readonly XColor CorLinha = XColor.FromArgb(226, 226, 236);
        private static readonly XColor CorFundo = XColor.FromArgb(247, 247, 251);
        private static readonly XColor CorFundoEscuro = XColor.FromArgb(17, 17, 27);

        public static void Gerar(string caminho, List<DispositivoDB> dispositivos, bool somenteSelecionados, string versao)
        {
            var gerador = new Gerador(versao);
            gerador.Executar(caminho, dispositivos ?? new List<DispositivoDB>(), somenteSelecionados);
        }

        private sealed class Gerador
        {
            private readonly PdfDocument _doc = new PdfDocument();
            private readonly string _versao;
            private readonly Dictionary<string, int> _paginas = new Dictionary<string, int>(StringComparer.Ordinal);

            public Gerador(string versao)
            {
                _versao = string.IsNullOrWhiteSpace(versao) ? "0.0.0" : versao;
            }

            public void Executar(string caminho, List<DispositivoDB> lista, bool somenteSelecionados)
            {
                try
                {
                    _doc.Info.Title = "Relatório Executivo de Dispositivos — TACE";
                    _doc.Info.Author = Empresa;
                    _doc.Info.Subject = "Inventário, conformidade e rastreabilidade da frota de dispositivos";
                    _doc.Info.Creator = Plataforma;
                    _doc.Info.Keywords = "InnovEdu, Operacional, TACE, dispositivos, inventário, auditoria";

                    DesenharCapa(lista, somenteSelecionados);
                    DesenharContracapa(lista, somenteSelecionados);

                    PdfPage sumarioPage = _doc.AddPage();
                    sumarioPage.Size = PageSize.A4;

                    DesenharVisaoGeral(lista, somenteSelecionados);
                    DesenharResumoAnalitico(lista);
                    DesenharAnexo(lista);

                    DesenharSumario(sumarioPage);

                    _doc.Save(caminho);
                }
                finally
                {
                    _doc.Dispose();
                }
            }

            // =====================================================================
            // CAPA / CONTRACAPA / SUMÁRIO
            // =====================================================================
            private void DesenharCapa(List<DispositivoDB> lista, bool somenteSelecionados)
            {
                PdfPage page = _doc.AddPage();
                page.Size = PageSize.A4;

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    gfx.DrawRectangle(new XSolidBrush(CorFundoEscuro), 0, 0, PageWidth, PageHeight);
                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), 0, 0, 8, PageHeight);
                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), 0, 0, PageWidth, 6);

                    gfx.DrawString("InnovEdu", F(25, true), XBrushes.White, new XRect(48, 48, 330, 36), XStringFormats.TopLeft);
                    gfx.DrawString("SETOR OPERACIONAL", F(8.5, true), new XSolidBrush(CorRoxoClaro), new XRect(50, 90, 330, 14), XStringFormats.TopLeft);
                    gfx.DrawLine(new XPen(CorRoxo, 2), 50, 118, 226, 118);

                    gfx.DrawString("RELATÓRIO EXECUTIVO", F(10, true), new XSolidBrush(CorRoxoClaro), new XRect(50, 214, 420, 16), XStringFormats.TopLeft);
                    gfx.DrawString("Dispositivos", F(40, true), XBrushes.White, new XRect(48, 240, 500, 52), XStringFormats.TopLeft);
                    gfx.DrawString("da Frota", F(40, true), XBrushes.White, new XRect(48, 292, 500, 52), XStringFormats.TopLeft);

                    DesenharParagrafo(
                        gfx,
                        "Inventário, conformidade e rastreabilidade dos equipamentos registrados na Plataforma TACE.",
                        F(11), new XSolidBrush(XColor.FromArgb(190, 190, 206)),
                        50, 362, 398, 16);

                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), 50, 430, 228, 28);
                    gfx.DrawString(
                        somenteSelecionados ? "SELEÇÃO PERSONALIZADA" : "BASE COMPLETA",
                        F(8, true), XBrushes.White, new XRect(50, 430, 228, 28), XStringFormats.Center);

                    gfx.DrawString(lista.Count.ToString("N0"), F(62, true), XBrushes.White, new XRect(48, 492, 320, 76), XStringFormats.TopLeft);
                    gfx.DrawString("dispositivos no escopo do relatório", F(10), new XSolidBrush(CorRoxoClaro), new XRect(52, 570, 350, 18), XStringFormats.TopLeft);

                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), 470, 214, 4, 188);
                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(34, 34, 52)), 486, 214, 4, 188);
                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(24, 24, 38)), 502, 214, 4, 188);

                    gfx.DrawLine(new XPen(XColor.FromArgb(52, 52, 72), 0.8), 50, 696, PageWidth - 50, 696);
                    gfx.DrawString(
                        DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("pt-BR")),
                        F(10, true), XBrushes.White, new XRect(50, 708, 320, 16), XStringFormats.TopLeft);
                    gfx.DrawString(
                        $"Emitido pela {Plataforma} · v{_versao}",
                        F(8), new XSolidBrush(XColor.FromArgb(160, 160, 180)), new XRect(50, 726, 420, 14), XStringFormats.TopLeft);
                }
            }

            private void DesenharContracapa(List<DispositivoDB> lista, bool somenteSelecionados)
            {
                PdfPage page = _doc.AddPage();
                page.Size = PageSize.A4;

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    gfx.DrawRectangle(XBrushes.White, 0, 0, PageWidth, PageHeight);
                    gfx.DrawRectangle(new XSolidBrush(CorFundoEscuro), 0, 0, PageWidth, 150);
                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), 0, 150, PageWidth, 4);

                    gfx.DrawString("InnovEdu", F(20, true), XBrushes.White, new XRect(40, 34, 300, 30), XStringFormats.TopLeft);
                    gfx.DrawString("SETOR OPERACIONAL", F(8, true), new XSolidBrush(CorRoxoClaro), new XRect(42, 68, 300, 14), XStringFormats.TopLeft);
                    gfx.DrawString("Ficha técnica do documento", F(24, true), XBrushes.White, new XRect(40, 98, 500, 32), XStringFormats.TopLeft);

                    double y = 188;
                    gfx.DrawString("CONTROLE DO DOCUMENTO", F(8, true), new XSolidBrush(CorRoxo), new XRect(MarginX, y, ContentWidth, 12), XStringFormats.TopLeft);
                    y += 24;

                    var linhas = new[]
                    {
                        ("Empresa", Empresa),
                        ("Setor responsável", Setor),
                        ("Plataforma", Plataforma),
                        ("Versão do sistema", _versao),
                        ("Escopo", somenteSelecionados ? "Seleção personalizada" : "Base completa"),
                        ("Total de dispositivos", lista.Count.ToString("N0")),
                        ("Data de emissão", DateTime.Now.ToString("dd/MM/yyyy HH:mm", new CultureInfo("pt-BR"))),
                        ("Origem dos dados", "SQLite local · data/bot_database.db"),
                        ("Classificação", "Uso interno")
                    };

                    foreach (var linha in linhas)
                    {
                        gfx.DrawString(linha.Item1, F(8.5, true), new XSolidBrush(CorSuave), new XRect(MarginX, y, 140, 16), XStringFormats.TopLeft);
                        gfx.DrawString(linha.Item2, F(9.5), new XSolidBrush(CorTexto), new XRect(MarginX + 150, y, ContentWidth - 150, 16), XStringFormats.TopLeft);
                        gfx.DrawLine(new XPen(CorLinha, 0.5), MarginX, y + 19, PageWidth - MarginX, y + 19);
                        y += 27;
                    }

                    y += 12;
                    gfx.DrawString("SOBRE ESTE RELATÓRIO", F(8, true), new XSolidBrush(CorRoxo), new XRect(MarginX, y, ContentWidth, 12), XStringFormats.TopLeft);
                    y += 20;

                    y = DesenharParagrafo(
                        gfx,
                        "Este documento consolida a posição da frota de dispositivos gerenciada pela Plataforma TACE. "
                        + "Os indicadores apresentados apoiam a operação na conferência patrimonial, na validação de endereços MAC e na identificação de lacunas de cadastro.",
                        F(9), new XSolidBrush(CorTexto), MarginX, y, ContentWidth, 14);

                    y += 18;
                    gfx.DrawRectangle(new XSolidBrush(CorFundo), MarginX, y, ContentWidth, 88);
                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), MarginX, y, 4, 88);
                    gfx.DrawString("NOTA DE INTEGRIDADE", F(7.5, true), new XSolidBrush(CorRoxo), new XRect(MarginX + 16, y + 12, ContentWidth - 32, 10), XStringFormats.TopLeft);
                    DesenharParagrafo(
                        gfx,
                        "As informações refletem o estado da base no momento da emissão. Alterações posteriores de cadastro, auditoria ou sincronização podem produzir divergências em relação a este documento.",
                        F(8.5), new XSolidBrush(CorSuave), MarginX + 16, y + 28, ContentWidth - 32, 13);

                    gfx.DrawString("ESTRUTURA DO DOCUMENTO", F(8, true), new XSolidBrush(CorRoxo), new XRect(MarginX, 652, ContentWidth, 12), XStringFormats.TopLeft);
                    DesenharBullet(gfx, "Capa e contracapa: identificação, controle e integridade do documento.", 674);
                    DesenharBullet(gfx, "Seções 1 e 2: indicadores, gráficos e análise da qualidade do cadastro.", 690);
                    DesenharBullet(gfx, "Seção 3: relação compacta de equipamentos para conferência física.", 706);

                    gfx.DrawLine(new XPen(CorLinha, 0.8), MarginX, 802, PageWidth - MarginX, 802);
                    gfx.DrawString($"{Empresa} · {Setor}", F(7), new XSolidBrush(CorSuave), new XRect(MarginX, 810, 240, 10), XStringFormats.TopLeft);
                    gfx.DrawString("Página 2", F(7, true), new XSolidBrush(CorSuave), new XRect(PageWidth - MarginX - 80, 810, 80, 10), XStringFormats.TopRight);
                }
            }

            private void DesenharSumario(PdfPage page)
            {
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    gfx.DrawRectangle(XBrushes.White, 0, 0, PageWidth, PageHeight);
                    gfx.DrawRectangle(new XSolidBrush(CorFundoEscuro), 0, 0, PageWidth, 150);
                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), 0, 150, PageWidth, 4);

                    gfx.DrawString("InnovEdu", F(20, true), XBrushes.White, new XRect(40, 34, 300, 30), XStringFormats.TopLeft);
                    gfx.DrawString("SETOR OPERACIONAL", F(8, true), new XSolidBrush(CorRoxoClaro), new XRect(42, 68, 300, 14), XStringFormats.TopLeft);
                    gfx.DrawString("Sumário", F(30, true), XBrushes.White, new XRect(40, 96, 400, 40), XStringFormats.TopLeft);

                    double y = 196;
                    gfx.DrawString("NAVEGAÇÃO DO DOCUMENTO", F(8, true), new XSolidBrush(CorRoxo), new XRect(MarginX, y, ContentWidth, 12), XStringFormats.TopLeft);
                    y += 28;

                    var itens = new List<(string Numero, string Titulo, string Descricao, int Pagina)>
                    {
                        ("00", "Ficha Técnica do Documento", "Dados de controle, escopo e integridade das informações.", 2),
                        ("01", "Visão Geral e Indicadores", "Indicadores, gráficos e leitura executiva da frota.", ObterPagina("1. Visão Geral e Indicadores")),
                        ("02", "Resumo Analítico da Frota", "Distribuições, cobertura de cadastro e lacunas identificadas.", ObterPagina("2. Resumo Analítico da Frota")),
                        ("03", "Anexo — Equipamentos Registrados", "Relação compacta por marca, modelo, número de série e MAC.", ObterPagina("3. Anexo — Equipamentos Registrados"))
                    };

                    foreach (var item in itens)
                    {
                        gfx.DrawEllipse(new XSolidBrush(CorRoxo), MarginX, y - 2, 28, 28);
                        gfx.DrawString(item.Numero, F(8, true), XBrushes.White, new XRect(MarginX, y - 2, 28, 28), XStringFormats.Center);

                        gfx.DrawString(item.Titulo, F(11, true), new XSolidBrush(CorTexto), new XRect(MarginX + 42, y, 350, 15), XStringFormats.TopLeft);
                        gfx.DrawString(item.Descricao, F(8), new XSolidBrush(CorSuave), new XRect(MarginX + 42, y + 17, 380, 12), XStringFormats.TopLeft);

                        double paginaX = PageWidth - MarginX - 38;
                        gfx.DrawString(item.Pagina.ToString("N0"), F(10, true), new XSolidBrush(CorRoxo), new XRect(paginaX, y, 38, 15), XStringFormats.TopRight);
                        DesenharLinhaPontilhada(gfx, MarginX + 42, y + 33, PageWidth - MarginX - 50, y + 33);
                        y += 68;
                    }

                    gfx.DrawRectangle(new XSolidBrush(CorFundo), MarginX, 640, ContentWidth, 92);
                    gfx.DrawRectangle(new XSolidBrush(CorRoxo), MarginX, 640, 4, 92);
                    gfx.DrawString("DOCUMENTO GERADO AUTOMATICAMENTE", F(7.5, true), new XSolidBrush(CorRoxo), new XRect(MarginX + 16, 654, ContentWidth - 32, 10), XStringFormats.TopLeft);
                    DesenharParagrafo(
                        gfx,
                        $"{Empresa} · {Setor} · {Plataforma} v{_versao}. Este sumário reflete a organização do relatório emitido em {DateTime.Now:dd/MM/yyyy HH:mm}.",
                        F(8.5), new XSolidBrush(CorSuave), MarginX + 16, 672, ContentWidth - 32, 13);

                    gfx.DrawLine(new XPen(CorLinha, 0.8), MarginX, 802, PageWidth - MarginX, 802);
                    gfx.DrawString($"{Empresa} · {Setor}", F(7), new XSolidBrush(CorSuave), new XRect(MarginX, 810, 240, 10), XStringFormats.TopLeft);
                    gfx.DrawString("Página 3", F(7, true), new XSolidBrush(CorSuave), new XRect(PageWidth - MarginX - 80, 810, 80, 10), XStringFormats.TopRight);
                }
            }

            // =====================================================================
            // SEÇÃO 1 — VISÃO GERAL E INDICADORES
            // =====================================================================
            private void DesenharVisaoGeral(List<DispositivoDB> lista, bool somenteSelecionados)
            {
                const string secao = "1. Visão Geral e Indicadores";
                _paginas[secao] = _doc.Pages.Count + 1;

                ReportPage page = NovaPagina(secao);
                try
                {
                    XGraphics gfx = page.Gfx;
                    int total = lista.Count;
                    int comPatrimonio = lista.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio));
                    int comMac = lista.Count(d => MacValido(d.MAC));
                    int completos = lista.Count(d => CadastroCompleto(d));

                    double pctPatrimonio = total > 0 ? comPatrimonio * 100.0 / total : 0;
                    double pctMac = total > 0 ? comMac * 100.0 / total : 0;
                    double pctCompleto = total > 0 ? completos * 100.0 / total : 0;

                    page.Y = DesenharParagrafo(
                        gfx,
                        somenteSelecionados
                            ? "Recorte personalizado da base, com indicadores de cobertura e distribuição da seleção."
                            : "Panorama consolidado da frota, com indicadores de cobertura e distribuição por fabricante e modelo.",
                        F(9.5), new XSolidBrush(CorSuave), MarginX, page.Y, ContentWidth, 14) + 14;

                    DesenharKpis(gfx, page.Y, total, comPatrimonio, comMac, completos, pctPatrimonio, pctMac, pctCompleto);
                    page.Y += 82;

                    gfx.DrawString("Distribuição da frota", F(12, true), new XSolidBrush(CorTexto), new XRect(MarginX, page.Y, ContentWidth, 18), XStringFormats.TopLeft);
                    page.Y += 22;

                    var fabricantes = lista
                        .GroupBy(d => ValorOu(d.Fabricante, "Não informado"), StringComparer.OrdinalIgnoreCase)
                        .Select(g => (Nome: g.Key, Qtd: g.Count()))
                        .OrderByDescending(x => x.Qtd).ThenBy(x => x.Nome)
                        .Take(6)
                        .ToList();

                    var modelos = lista
                        .GroupBy(d => ValorOu(d.Modelo, "Não identificado"), StringComparer.OrdinalIgnoreCase)
                        .Select(g => (Nome: g.Key, Qtd: g.Count()))
                        .OrderByDescending(x => x.Qtd).ThenBy(x => x.Nome)
                        .Take(6)
                        .ToList();

                    double painelW = (ContentWidth - 14) / 2;
                    double painelH = 188;
                    DesenharPainelBarras(gfx, MarginX, page.Y, painelW, painelH, "Por fabricante", "Top 6 fabricantes", fabricantes, total, CorRoxo);
                    DesenharPainelBarras(gfx, MarginX + painelW + 14, page.Y, painelW, painelH, "Por modelo", "Top 6 modelos", modelos, total, CorVerde);
                    page.Y += painelH + 18;

                    gfx.DrawString("Cobertura do cadastro", F(12, true), new XSolidBrush(CorTexto), new XRect(MarginX, page.Y, ContentWidth, 18), XStringFormats.TopLeft);
                    page.Y += 22;

                    var cobertura = new[]
                    {
                        ("Patrimônio", lista.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio))),
                        ("Serial", lista.Count(d => SerialValido(d.Serial))),
                        ("MAC", comMac),
                        ("Modelo", lista.Count(d => ModeloIdentificado(d.Modelo))),
                        ("Android", lista.Count(d => SerialValido(d.Versao_Android) && !d.Versao_Android.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase))),
                        ("IMEI", lista.Count(d => SerialValido(d.IMEI) && !d.IMEI.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase)))
                    };
                    DesenharCobertura(gfx, MarginX, page.Y, ContentWidth, total, cobertura);
                    page.Y += 96;

                    DesenharLeituraExecutiva(gfx, page.Y, lista, total, comPatrimonio, comMac, completos);
                }
                finally
                {
                    page.Dispose();
                }
            }

            private void DesenharKpis(XGraphics gfx, double y, int total, int comPatrimonio, int comMac, int completos,
                double pctPatrimonio, double pctMac, double pctCompleto)
            {
                double gap = 8;
                double largura = (ContentWidth - (gap * 3)) / 4;
                double altura = 68;

                DesenharKpi(gfx, MarginX, y, largura, altura, "Dispositivos", total.ToString("N0"), "no escopo", CorRoxo);
                DesenharKpi(gfx, MarginX + (largura + gap), y, largura, altura, "Com patrimônio", $"{pctPatrimonio:F1}%", $"{comPatrimonio:N0} registros", CorVerde);
                DesenharKpi(gfx, MarginX + ((largura + gap) * 2), y, largura, altura, "MAC válido", $"{pctMac:F1}%", $"{comMac:N0} registros", CorAzul);
                DesenharKpi(gfx, MarginX + ((largura + gap) * 3), y, largura, altura, "Cadastro completo", $"{pctCompleto:F1}%", $"{completos:N0} registros", pctCompleto >= 80 ? CorVerde : CorAmarelo);
            }

            private void DesenharKpi(XGraphics gfx, double x, double y, double w, double h, string rotulo, string valor, string detalhe, XColor cor)
            {
                gfx.DrawRectangle(new XSolidBrush(CorFundo), x, y, w, h);
                gfx.DrawRectangle(new XSolidBrush(cor), x, y, w, 3);
                gfx.DrawString(rotulo.ToUpperInvariant(), F(6.8, true), new XSolidBrush(CorSuave), new XRect(x + 9, y + 10, w - 18, 10), XStringFormats.TopLeft);
                gfx.DrawString(valor, F(18, true), new XSolidBrush(CorTexto), new XRect(x + 9, y + 23, w - 18, 24), XStringFormats.TopLeft);
                gfx.DrawString(detalhe, F(6.8), new XSolidBrush(CorSuave), new XRect(x + 9, y + 49, w - 18, 10), XStringFormats.TopLeft);
            }

            private void DesenharPainelBarras(XGraphics gfx, double x, double y, double w, double h, string titulo, string subtitulo,
                List<(string Nome, int Qtd)> dados, int total, XColor cor)
            {
                gfx.DrawRectangle(new XSolidBrush(CorFundo), x, y, w, h);
                gfx.DrawRectangle(new XPen(CorLinha, 0.6), x, y, w, h);
                gfx.DrawString(titulo, F(9.5, true), new XSolidBrush(CorTexto), new XRect(x + 10, y + 9, w - 20, 14), XStringFormats.TopLeft);
                gfx.DrawString(subtitulo, F(6.8), new XSolidBrush(CorSuave), new XRect(x + 10, y + 23, w - 20, 10), XStringFormats.TopLeft);

                if (dados.Count == 0)
                {
                    gfx.DrawString("Sem dados para exibir.", F(8), new XSolidBrush(CorSuave), new XRect(x + 10, y + 52, w - 20, 14), XStringFormats.TopLeft);
                    return;
                }

                double rowY = y + 44;
                double labelW = 74;
                double barX = x + 10 + labelW;
                double barMax = w - labelW - 48;
                int max = Math.Max(1, dados.Max(d => d.Qtd));

                foreach (var item in dados)
                {
                    gfx.DrawString(Truncar(gfx, item.Nome, F(7.2), labelW - 4), F(7.2), new XSolidBrush(CorTexto),
                        new XRect(x + 10, rowY, labelW - 4, 12), XStringFormats.CenterLeft);
                    gfx.DrawRectangle(new XSolidBrush(CorLinha), barX, rowY + 2, barMax, 7);
                    gfx.DrawRectangle(new XSolidBrush(cor), barX, rowY + 2, Math.Max(2, barMax * item.Qtd / max), 7);
                    gfx.DrawString(item.Qtd.ToString("N0"), F(6.8, true), new XSolidBrush(CorSuave),
                        new XRect(x + w - 38, rowY, 28, 12), XStringFormats.CenterRight);
                    rowY += 21;
                }

                gfx.DrawString($"Base: {total:N0} dispositivo(s)", F(6.4), new XSolidBrush(CorSuave),
                    new XRect(x + 10, y + h - 14, w - 20, 10), XStringFormats.TopLeft);
            }

            private void DesenharCobertura(XGraphics gfx, double x, double y, double w, int total, (string Nome, int Qtd)[] itens)
            {
                double colW = (w - 16) / 2;
                double rowH = 27;

                for (int i = 0; i < itens.Length; i++)
                {
                    int coluna = i % 2;
                    int linha = i / 2;
                    double ix = x + (coluna * (colW + 16));
                    double iy = y + (linha * rowH);
                    double pct = total > 0 ? itens[i].Qtd * 100.0 / total : 0;
                    XColor cor = pct >= 90 ? CorVerde : pct >= 60 ? CorAmarelo : CorVermelho;

                    gfx.DrawString(itens[i].Nome, F(7.2, true), new XSolidBrush(CorTexto), new XRect(ix, iy, colW - 90, 12), XStringFormats.TopLeft);
                    gfx.DrawString($"{itens[i].Qtd:N0}/{total:N0} · {pct:F0}%", F(6.6), new XSolidBrush(CorSuave),
                        new XRect(ix + colW - 92, iy, 92, 12), XStringFormats.TopRight);
                    gfx.DrawRectangle(new XSolidBrush(CorLinha), ix, iy + 15, colW, 5);
                    gfx.DrawRectangle(new XSolidBrush(cor), ix, iy + 15, Math.Max(2, colW * pct / 100), 5);
                }
            }

            private void DesenharLeituraExecutiva(XGraphics gfx, double y, List<DispositivoDB> lista, int total, int comPatrimonio, int comMac, int completos)
            {
                if (y + 86 > BottomY) return;

                gfx.DrawRectangle(new XSolidBrush(CorFundo), MarginX, y, ContentWidth, 82);
                gfx.DrawRectangle(new XSolidBrush(CorRoxo), MarginX, y, 4, 82);
                gfx.DrawString("LEITURA EXECUTIVA", F(7.5, true), new XSolidBrush(CorRoxo), new XRect(MarginX + 16, y + 11, ContentWidth - 32, 10), XStringFormats.TopLeft);

                var topFabricante = lista.GroupBy(d => ValorOu(d.Fabricante, "Não informado"), StringComparer.OrdinalIgnoreCase)
                    .Select(g => (Nome: g.Key, Qtd: g.Count())).OrderByDescending(x => x.Qtd).FirstOrDefault();
                var topModelo = lista.GroupBy(d => ValorOu(d.Modelo, "Não identificado"), StringComparer.OrdinalIgnoreCase)
                    .Select(g => (Nome: g.Key, Qtd: g.Count())).OrderByDescending(x => x.Qtd).FirstOrDefault();

                int semPatrimonio = total - comPatrimonio;
                int macInvalido = total - comMac;
                int incompletos = total - completos;

                double py = y + 28;
                DesenharBullet(gfx, $"Fabricante predominante: {topFabricante.Nome} ({topFabricante.Qtd:N0} dispositivo(s)).", py); py += 14;
                DesenharBullet(gfx, $"Modelo predominante: {topModelo.Nome} ({topModelo.Qtd:N0} dispositivo(s)).", py); py += 14;
                DesenharBullet(gfx, $"{semPatrimonio:N0} sem patrimônio · {macInvalido:N0} com MAC inválido · {incompletos:N0} com cadastro incompleto.", py);
            }

            private void DesenharBullet(XGraphics gfx, string texto, double y)
            {
                gfx.DrawEllipse(new XSolidBrush(CorRoxo), MarginX + 18, y + 3, 4, 4);
                gfx.DrawString(texto, F(7.6), new XSolidBrush(CorSuave), new XRect(MarginX + 28, y, ContentWidth - 46, 12), XStringFormats.TopLeft);
            }

            // =====================================================================
            // SEÇÃO 2 — RESUMO ANALÍTICO
            // =====================================================================
            private void DesenharResumoAnalitico(List<DispositivoDB> lista)
            {
                const string secao = "2. Resumo Analítico da Frota";
                _paginas[secao] = _doc.Pages.Count + 1;

                ReportPage page = NovaPagina(secao);
                try
                {
                    int total = lista.Count;
                    page.Y = DesenharParagrafo(
                        page.Gfx,
                        "Consolidação por fabricante e modelo, acompanhada dos indicadores de qualidade do cadastro e das lacunas que exigem ação operacional.",
                        F(9.5), new XSolidBrush(CorSuave), MarginX, page.Y, ContentWidth, 14) + 14;

                    page.Gfx.DrawString("Distribuição por fabricante", F(11, true), new XSolidBrush(CorTexto), new XRect(MarginX, page.Y, ContentWidth, 16), XStringFormats.TopLeft);
                    page.Y += 20;

                    var fabricantes = lista
                        .GroupBy(d => ValorOu(d.Fabricante, "Não informado"), StringComparer.OrdinalIgnoreCase)
                        .Select(g => new
                        {
                            Nome = g.Key,
                            Itens = g.ToList(),
                            Qtd = g.Count()
                        })
                        .OrderByDescending(x => x.Qtd).ThenBy(x => x.Nome)
                        .ToList();

                    var linhasFabricantes = fabricantes.Select(g => new[]
                    {
                        g.Nome,
                        g.Qtd.ToString("N0"),
                        $"{(total > 0 ? g.Qtd * 100.0 / total : 0):F1}%",
                        g.Itens.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio)).ToString("N0"),
                        g.Itens.Count(d => MacValido(d.MAC)).ToString("N0")
                    });

                    string[] headersFab = { "Fabricante", "Qtd.", "% frota", "Com patrimônio", "MAC válido" };
                    double[] widthsFab = { 195, 55, 60, 100, 105 };
                    DesenharTabelaSimples(ref page, secao, headersFab, widthsFab, linhasFabricantes, CorRoxo);

                    page.Y += 18;
                    if (page.Y + 60 > BottomY)
                    {
                        page.Dispose();
                        page = NovaPagina(secao, true);
                    }

                    page.Gfx.DrawString("Distribuição por modelo", F(11, true), new XSolidBrush(CorTexto), new XRect(MarginX, page.Y, ContentWidth, 16), XStringFormats.TopLeft);
                    page.Y += 20;

                    var modelos = lista
                        .GroupBy(d => ValorOu(d.Modelo, "Não identificado"), StringComparer.OrdinalIgnoreCase)
                        .Select(g => new
                        {
                            Nome = g.Key,
                            Fabricante = g.Select(d => ValorOu(d.Fabricante, "—")).GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(f => f.Count()).Select(f => f.Key).FirstOrDefault() ?? "—",
                            Qtd = g.Count(),
                            ComPatrimonio = g.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio)),
                            MacValido = g.Count(d => MacValido(d.MAC))
                        })
                        .OrderByDescending(x => x.Qtd).ThenBy(x => x.Nome)
                        .ToList();

                    var linhasModelos = modelos.Select(m => new[]
                    {
                        m.Nome,
                        m.Fabricante,
                        m.Qtd.ToString("N0"),
                        $"{(total > 0 ? m.Qtd * 100.0 / total : 0):F1}%",
                        m.ComPatrimonio.ToString("N0"),
                        m.MacValido.ToString("N0")
                    });

                    string[] headersModelo = { "Modelo", "Fabricante", "Qtd.", "% frota", "Patrim.", "MAC" };
                    double[] widthsModelo = { 175, 120, 45, 55, 60, 60 };
                    DesenharTabelaSimples(ref page, secao, headersModelo, widthsModelo, linhasModelos, CorAzul);

                    page.Y += 18;
                    if (page.Y + 140 > BottomY)
                    {
                        page.Dispose();
                        page = NovaPagina(secao, true);
                    }

                    page.Gfx.DrawString("Qualidade do cadastro", F(11, true), new XSolidBrush(CorTexto), new XRect(MarginX, page.Y, ContentWidth, 16), XStringFormats.TopLeft);
                    page.Y += 20;

                    var indicadores = new[]
                    {
                        ("Patrimônio preenchido", lista.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio))),
                        ("Serial válido", lista.Count(d => SerialValido(d.Serial))),
                        ("MAC válido", lista.Count(d => MacValido(d.MAC))),
                        ("Modelo identificado", lista.Count(d => ModeloIdentificado(d.Modelo))),
                        ("Versão Android informada", lista.Count(d => VersaoAndroidValida(d))),
                        ("IMEI informado", lista.Count(d => SerialValido(d.IMEI) && !d.IMEI.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase)))
                    };

                    var linhasQualidade = indicadores.Select(i => new[]
                    {
                        i.Item1,
                        i.Item2.ToString("N0"),
                        $"{(total > 0 ? i.Item2 * 100.0 / total : 0):F1}%",
                        (total > 0 && i.Item2 * 100.0 / total >= 90) ? "Adequado" : (total > 0 && i.Item2 * 100.0 / total >= 60) ? "Atenção" : "Crítico"
                    });

                    string[] headersQualidade = { "Indicador", "Quantidade", "Cobertura", "Status" };
                    double[] widthsQualidade = { 235, 85, 85, 110 };
                    DesenharTabelaSimples(ref page, secao, headersQualidade, widthsQualidade, linhasQualidade, CorVerde);

                    page.Y += 18;
                    if (page.Y + 150 > BottomY)
                    {
                        page.Dispose();
                        page = NovaPagina(secao, true);
                    }

                    DesenharLacunas(ref page, secao, lista);
                }
                finally
                {
                    page.Dispose();
                }
            }

            private void DesenharLacunas(ref ReportPage page, string secao, List<DispositivoDB> lista)
            {
                int total = lista.Count;
                int semPatrimonio = lista.Count(d => string.IsNullOrWhiteSpace(d.Patrimonio));
                int macInvalido = lista.Count(d => !MacValido(d.MAC));
                int semSerial = lista.Count(d => !SerialValido(d.Serial));
                int semModelo = lista.Count(d => !ModeloIdentificado(d.Modelo));

                var duplicados = lista
                    .Where(d => !string.IsNullOrWhiteSpace(d.Patrimonio))
                    .GroupBy(d => d.Patrimonio.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .ToList();

                page.Gfx.DrawRectangle(new XSolidBrush(CorFundo), MarginX, page.Y, ContentWidth, 156);
                page.Gfx.DrawRectangle(new XSolidBrush(CorAmarelo), MarginX, page.Y, 4, 156);
                page.Gfx.DrawString("LACUNAS E RECOMENDAÇÕES", F(7.5, true), new XSolidBrush(CorAmarelo), new XRect(MarginX + 16, page.Y + 12, ContentWidth - 32, 10), XStringFormats.TopLeft);

                double y = page.Y + 32;
                DesenharBullet(page.Gfx, $"{semPatrimonio:N0} dispositivo(s) sem patrimônio — priorizar etiquetagem e conferência física.", y); y += 15;
                DesenharBullet(page.Gfx, $"{macInvalido:N0} dispositivo(s) com MAC ausente ou inválido — revisar coleta e sincronização.", y); y += 15;
                DesenharBullet(page.Gfx, $"{semSerial:N0} dispositivo(s) sem serial válido e {semModelo:N0} sem modelo identificado.", y); y += 15;

                if (duplicados.Count > 0)
                {
                    int registrosDuplicados = duplicados.Sum(g => g.Count());
                    DesenharBullet(page.Gfx, $"{duplicados.Count:N0} código(s) de patrimônio duplicado(s), afetando {registrosDuplicados:N0} registro(s).", y); y += 15;
                }

                DesenharBullet(page.Gfx, $"Base com {total:N0} dispositivo(s); manter auditoria contínua para sustentar a cobertura dos indicadores.", y);
                page.Y += 170;
            }

            // =====================================================================
            // SEÇÃO 3 — ANEXO
            // =====================================================================
            private void DesenharAnexo(List<DispositivoDB> lista)
            {
                const string secao = "3. Anexo — Equipamentos Registrados";
                _paginas[secao] = _doc.Pages.Count + 1;

                var ordenados = lista
                    .OrderBy(d => ValorOu(d.Fabricante, "ZZZ"), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(d => ValorOu(d.Modelo, "ZZZ"), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(d => ValorOu(d.Serial, "ZZZ"), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string[] headers = { "Marca", "Modelo", "Nº de Série", "MAC" };
                double[] widths = { 125, 185, 120, 85 };
                double rowHeight = 17;

                ReportPage page = NovaPagina(secao);
                try
                {
                    page.Y = DesenharParagrafo(
                        page.Gfx,
                        $"Relação compacta de {ordenados.Count:N0} equipamento(s), ordenada por marca, modelo e número de série. "
                        + "A listagem contém apenas os campos necessários para conferência física.",
                        F(9.2), new XSolidBrush(CorSuave), MarginX, page.Y, ContentWidth, 14) + 12;

                    DesenharCabecalhoTabela(page, headers, widths, CorAzul);
                    int indice = 0;

                    foreach (var d in ordenados)
                    {
                        if (page.Y + rowHeight > BottomY)
                        {
                            page.Dispose();
                            page = NovaPagina(secao, true);
                            page.Y += 4;
                            DesenharCabecalhoTabela(page, headers, widths, CorAzul);
                        }

                        string[] cells =
                        {
                            ValorOu(d.Fabricante, "—"),
                            ValorOu(d.Modelo, "Não identificado"),
                            ValorOu(d.Serial, "—"),
                            ValorOu(d.MAC, "—")
                        };

                        DesenharLinhaTabela(page, cells, widths, indice % 2 == 1, rowHeight);
                        indice++;
                    }

                    page.Y += 12;
                    if (page.Y + 40 > BottomY)
                    {
                        page.Dispose();
                        page = NovaPagina(secao, true);
                    }

                    page.Gfx.DrawRectangle(new XSolidBrush(CorFundo), MarginX, page.Y, ContentWidth, 34);
                    page.Gfx.DrawRectangle(new XSolidBrush(CorRoxo), MarginX, page.Y, 4, 34);
                    page.Gfx.DrawString(
                        $"Fim do anexo · {ordenados.Count:N0} equipamento(s) relacionado(s) · emitido em {DateTime.Now:dd/MM/yyyy HH:mm}.",
                        F(8), new XSolidBrush(CorSuave), new XRect(MarginX + 16, page.Y + 10, ContentWidth - 32, 14), XStringFormats.TopLeft);
                }
                finally
                {
                    page.Dispose();
                }
            }

            // =====================================================================
            // COMPONENTES DE PÁGINA
            // =====================================================================
            private ReportPage NovaPagina(string secao, bool continuacao = false)
            {
                PdfPage page = _doc.AddPage();
                page.Size = PageSize.A4;
                XGraphics gfx = XGraphics.FromPdfPage(page);
                int numero = _doc.Pages.Count;

                gfx.DrawString(Empresa, F(11.5, true), new XSolidBrush(CorRoxo), new XRect(MarginX, 30, 220, 16), XStringFormats.TopLeft);
                gfx.DrawString(Setor, F(7.4), new XSolidBrush(CorSuave), new XRect(MarginX, 45, 220, 12), XStringFormats.TopLeft);
                gfx.DrawString("Relatório Executivo de Dispositivos", F(8.3, true), new XSolidBrush(CorSuave),
                    new XRect(PageWidth - MarginX - 260, 30, 260, 14), XStringFormats.TopRight);
                gfx.DrawString($"{Plataforma} · v{_versao}", F(7.4), new XSolidBrush(CorSuave),
                    new XRect(PageWidth - MarginX - 260, 45, 260, 12), XStringFormats.TopRight);
                gfx.DrawLine(new XPen(CorLinha, 0.8), MarginX, 62, PageWidth - MarginX, 62);

                gfx.DrawString(secao, F(16.5, true), new XSolidBrush(CorTexto), new XRect(MarginX, 78, ContentWidth, 26), XStringFormats.TopLeft);
                if (continuacao)
                {
                    gfx.DrawString("continuação", F(7.6), new XSolidBrush(CorSuave),
                        new XRect(PageWidth - MarginX - 120, 84, 120, 12), XStringFormats.TopRight);
                }

                gfx.DrawLine(new XPen(CorLinha, 0.8), MarginX, 802, PageWidth - MarginX, 802);
                gfx.DrawString($"{Empresa} · {Setor}", F(7), new XSolidBrush(CorSuave), new XRect(MarginX, 810, 240, 10), XStringFormats.TopLeft);
                gfx.DrawString("Documento confidencial", F(7), new XSolidBrush(CorSuave),
                    new XRect((PageWidth / 2) - 80, 810, 160, 10), XStringFormats.TopCenter);
                gfx.DrawString($"Página {numero}", F(7, true), new XSolidBrush(CorSuave),
                    new XRect(PageWidth - MarginX - 80, 810, 80, 10), XStringFormats.TopRight);

                return new ReportPage(gfx, 108);
            }

            private void DesenharCabecalhoTabela(ReportPage page, string[] headers, double[] widths, XColor cor)
            {
                const double altura = 20;
                page.Gfx.DrawRectangle(new XSolidBrush(cor), MarginX, page.Y, ContentWidth, altura);

                double x = MarginX;
                for (int i = 0; i < headers.Length; i++)
                {
                    page.Gfx.DrawString(headers[i], F(7.3, true), XBrushes.White,
                        new XRect(x + 4, page.Y, widths[i] - 8, altura), XStringFormats.CenterLeft);
                    x += widths[i];
                }

                page.Y += altura;
            }

            private void DesenharLinhaTabela(ReportPage page, string[] cells, double[] widths, bool alternada, double altura)
            {
                if (alternada)
                {
                    page.Gfx.DrawRectangle(new XSolidBrush(CorFundo), MarginX, page.Y, ContentWidth, altura);
                }

                page.Gfx.DrawLine(new XPen(CorLinha, 0.4), MarginX, page.Y + altura, PageWidth - MarginX, page.Y + altura);

                bool primeiraColunaNumerica = cells.Length > 0 && int.TryParse(cells[0], out _);
                double x = MarginX;
                for (int i = 0; i < cells.Length; i++)
                {
                    XStringFormat formato = i == 0 && primeiraColunaNumerica ? XStringFormats.Center : XStringFormats.CenterLeft;
                    page.Gfx.DrawString(Truncar(page.Gfx, cells[i], F(7.1), widths[i] - 8), F(7.1), new XSolidBrush(CorTexto),
                        new XRect(x + 4, page.Y, widths[i] - 8, altura), formato);
                    x += widths[i];
                }

                page.Y += altura;
            }

            private void DesenharTabelaSimples(ref ReportPage page, string secao, string[] headers, double[] widths,
                IEnumerable<string[]> linhas, XColor corCabecalho)
            {
                DesenharCabecalhoTabela(page, headers, widths, corCabecalho);
                int indice = 0;

                foreach (var linha in linhas)
                {
                    if (page.Y + 17 > BottomY)
                    {
                        page.Dispose();
                        page = NovaPagina(secao, true);
                        page.Y += 4;
                        DesenharCabecalhoTabela(page, headers, widths, corCabecalho);
                    }

                    DesenharLinhaTabela(page, linha, widths, indice % 2 == 1, 17);
                    indice++;
                }
            }

            private int ObterPagina(string secao)
            {
                return _paginas.TryGetValue(secao, out int pagina) ? pagina : 0;
            }

            private sealed class ReportPage : IDisposable
            {
                public XGraphics Gfx { get; }
                public double Y { get; set; }

                public ReportPage(XGraphics gfx, double y)
                {
                    Gfx = gfx;
                    Y = y;
                }

                public void Dispose()
                {
                    Gfx.Dispose();
                }
            }
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static XFont F(double tamanho, bool negrito = false, bool italico = false)
        {
            XFontStyle estilo = negrito && italico
                ? XFontStyle.BoldItalic
                : negrito
                    ? XFontStyle.Bold
                    : italico
                        ? XFontStyle.Italic
                        : XFontStyle.Regular;

            return new XFont("Arial", tamanho, estilo);
        }

        private static string ValorOu(string? valor, string fallback)
        {
            if (string.IsNullOrWhiteSpace(valor)) return fallback;
            string limpo = valor.Trim();
            if (limpo.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                limpo.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase) ||
                limpo.Equals("Pendente", StringComparison.OrdinalIgnoreCase) ||
                limpo == "---" || limpo.StartsWith("?", StringComparison.Ordinal))
            {
                return fallback;
            }
            return limpo;
        }

        private static bool SerialValido(string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            string valor = serial.Trim();
            return !valor.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("pendente", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("n/a", StringComparison.OrdinalIgnoreCase)
                && !valor.StartsWith("?", StringComparison.Ordinal);
        }

        private static bool MacValido(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return false;
            string valor = mac.Trim();
            if (valor.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase) ||
                valor == "---" || valor == "02:00:00:00:00:00")
            {
                return false;
            }

            return Regex.IsMatch(valor, @"^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$")
                || Regex.IsMatch(valor, @"^[0-9A-Fa-f]{12}$");
        }

        private static bool ModeloIdentificado(string? modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo)) return false;
            string valor = modelo.Trim();
            return !valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("N/A", StringComparison.OrdinalIgnoreCase);
        }

        private static bool VersaoAndroidValida(DispositivoDB d)
        {
            string valor = (d.Versao_Android ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(valor)
                && !valor.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CadastroCompleto(DispositivoDB d)
        {
            return !string.IsNullOrWhiteSpace(d.Patrimonio)
                && SerialValido(d.Serial)
                && MacValido(d.MAC)
                && ModeloIdentificado(d.Modelo);
        }

        private static string Truncar(XGraphics gfx, string texto, XFont fonte, double largura)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            if (gfx.MeasureString(texto, fonte).Width <= largura) return texto;

            const string reticencias = "…";
            int baixo = 0;
            int alto = texto.Length;

            while (baixo < alto)
            {
                int meio = (baixo + alto + 1) / 2;
                string teste = texto.Substring(0, meio).TrimEnd() + reticencias;
                if (gfx.MeasureString(teste, fonte).Width <= largura) baixo = meio;
                else alto = meio - 1;
            }

            return baixo <= 0 ? reticencias : texto.Substring(0, baixo).TrimEnd() + reticencias;
        }

        private static double DesenharParagrafo(XGraphics gfx, string texto, XFont fonte, XBrush brush,
            double x, double y, double largura, double alturaLinha)
        {
            if (string.IsNullOrWhiteSpace(texto)) return y;

            var linha = new StringBuilder();
            foreach (string palavra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string tentativa = linha.Length == 0 ? palavra : linha + " " + palavra;
                if (gfx.MeasureString(tentativa, fonte).Width > largura && linha.Length > 0)
                {
                    gfx.DrawString(linha.ToString(), fonte, brush, new XRect(x, y, largura, alturaLinha), XStringFormats.TopLeft);
                    y += alturaLinha;
                    linha.Clear();
                    linha.Append(palavra);
                }
                else
                {
                    linha.Clear();
                    linha.Append(tentativa);
                }
            }

            if (linha.Length > 0)
            {
                gfx.DrawString(linha.ToString(), fonte, brush, new XRect(x, y, largura, alturaLinha), XStringFormats.TopLeft);
                y += alturaLinha;
            }

            return y;
        }

        private static void DesenharLinhaPontilhada(XGraphics gfx, double x1, double y, double x2, double y2)
        {
            XPen pen = new XPen(CorLinha, 0.7);
            double atual = x1;
            while (atual < x2)
            {
                double fim = Math.Min(atual + 3, x2);
                gfx.DrawLine(pen, atual, y, fim, y2);
                atual += 6;
            }
        }
    }
}
