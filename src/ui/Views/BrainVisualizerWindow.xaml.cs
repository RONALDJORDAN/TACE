using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BotRpaUI.Views
{
    public partial class BrainVisualizerWindow : Window
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct LearnStrategyExport
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string TargetId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Query;
            public int Score;
            public int Hits;
            public int Misses;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string History;
        }

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategyCount();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategy(int index, out LearnStrategyExport outStrategy);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_RefreshDevices();

        // ============================================================
        // MODELO DE DADOS 3D (HOLO-SPHERE MATRIX)
        // ============================================================
        private class Node3D
        {
            public double OrigX, OrigY, OrigZ;
            public double RotX, RotY, RotZ;
            public double ScreenX, ScreenY;
            public double Scale = 1.0;
            public double BaseSize = 20.0;
            public int ZIndex;
            public double Opacity = 1.0;

            public FrameworkElement VisualElement = null!;
            public Border? VisualBadge;
            public string TargetId = "";
            public string Type = ""; // "CORE", "TARGET", "STRATEGY", "COGNITIVE", "MICRO"
            public Color BaseColor;
            public LearnStrategyExport StrategyData;
            public List<LearnStrategyExport>? TargetStrategies;
        }

        private class Synapse3D
        {
            public Node3D From = null!;
            public Node3D To = null!;
            public Line VisualLine = null!;
            public Color BaseColor;
            public bool IsCore;
        }

        // ============================================================
        // ESTADO INTERNO DA JANELA E DA CÂMERA 3D
        // ============================================================
        private string _currentFilter = "ALL";
        private string _searchText = "";
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _rotX = 0.25;  // Inclinação vertical (Pitch)
        private double _rotY = 0.0;   // Rotação horizontal (Yaw)
        private double _velRotX = 0.0;
        private double _velRotY = 0.0;
        private double _zoomScale = 1.0;
        private bool _uiVisible = true;

        private bool _showTagCore = true;
        private bool _showTagTargets = true;
        private bool _showTagCognitive = true;
        private bool _showTagStrategies = false;
        private bool _showTagMicro = false;

        private System.Diagnostics.Stopwatch? _animationStopwatch;
        private double _lastFrameSeconds;
        private bool _isRenderingSubscribed = false;
        private System.Windows.Threading.DispatcherTimer? _liveTimer;
        private System.Windows.Threading.DispatcherTimer? _searchDebounce;
        private (int Count, int Hits, int Misses, int ScoreSum) _lastKnownSignature = (-1, 0, 0, 0);
        private int _knownStrategies;
        private int _knownTargets;
        private int _knownHits;
        private int _knownMisses;
        private int _knownScoreSum;
        private string _knownHistory = "";

        private readonly List<Node3D> _nodes3D = new List<Node3D>();
        private readonly List<Synapse3D> _synapses3D = new List<Synapse3D>();

        public BrainVisualizerWindow()
        {
            InitializeComponent();

            var mainWin = Application.Current != null ? Application.Current.MainWindow : null;
            if (mainWin != null && mainWin != this)
            {
                this.Owner = mainWin;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_isRenderingSubscribed)
            {
                CompositionTarget.Rendering -= OnCompositionTargetRendering;
                _isRenderingSubscribed = false;
            }
            if (_liveTimer != null)
            {
                _liveTimer.Stop();
                _liveTimer.Tick -= LiveTimer_Tick;
                _liveTimer = null;
            }
            if (_searchDebounce != null)
            {
                _searchDebounce.Stop();
                _searchDebounce = null;
            }
            base.OnClosed(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RenderBrain();
            StartLiveTimer();
        }

        private void StartLiveTimer()
        {
            if (_liveTimer != null) return;
            _liveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            _liveTimer.Tick += LiveTimer_Tick;
            _liveTimer.Start();
        }

        private void LiveTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsLoaded) return;
            try
            {
                var signature = ReadLearnSignature();
                if (signature != _lastKnownSignature)
                {
                    _lastKnownSignature = signature;
                    RenderBrain();
                    return;
                }
                int active = Engine_RefreshDevices();
                if (TxtActiveDevices != null) TxtActiveDevices.Text = active.ToString();
            }
            catch { }
        }

        private (int Count, int Hits, int Misses, int ScoreSum) ReadLearnSignature()
        {
            int count = 0;
            int hits = 0;
            int misses = 0;
            int scoreSum = 0;

            count = Engine_Learn_GetStrategyCount();
            for (int i = 0; i < count; i++)
            {
                if (Engine_Learn_GetStrategy(i, out LearnStrategyExport strat) != 0)
                {
                    hits += strat.Hits;
                    misses += strat.Misses;
                    scoreSum += strat.Score;
                }
            }
            return (count, hits, misses, scoreSum);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (NodeInspectorPanel != null && NodeInspectorPanel.Visibility == Visibility.Visible)
                {
                    NodeInspectorPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.Close();
                }
            }
            else if (e.Key == Key.H)
            {
                ToggleUIVisibility();
            }
        }

        private void BtnToggleUI_Click(object sender, RoutedEventArgs e)
        {
            ToggleUIVisibility();
        }

        private void ToggleUIVisibility()
        {
            _uiVisible = !_uiVisible;
            Visibility v = _uiVisible ? Visibility.Visible : Visibility.Collapsed;

            if (TopHeaderPanel != null) TopHeaderPanel.Visibility = v;
            if (LegendPanel != null) LegendPanel.Visibility = v;
            if (FooterTipsPanel != null) FooterTipsPanel.Visibility = v;
            if (ZoomControlsPanel != null) ZoomControlsPanel.Visibility = v;
            if (NodeInspectorPanel != null) NodeInspectorPanel.Visibility = Visibility.Collapsed;

            if (BtnFloatingShowUI != null)
            {
                BtnFloatingShowUI.Visibility = _uiVisible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || BrainCanvas == null || NodeInspectorPanel == null) return;
            if (sender is RadioButton btn && btn.Tag != null)
            {
                _currentFilter = btn.Tag.ToString() ?? "ALL";
                NodeInspectorPanel.Visibility = Visibility.Collapsed;
                RenderBrain();
            }
        }

        private void TagFilter_Click(object sender, RoutedEventArgs e)
        {
            _showTagCore = ChkTagCore?.IsChecked == true;
            _showTagTargets = ChkTagTargets?.IsChecked == true;
            _showTagCognitive = ChkTagCognitive?.IsChecked == true;
            _showTagStrategies = ChkTagStrategies?.IsChecked == true;
            _showTagMicro = ChkTagMicro?.IsChecked == true;

            if (IsLoaded) RenderBrain();
        }

        private void BtnToggleAllTags_Click(object sender, RoutedEventArgs e)
        {
            bool anyOff = !(_showTagCore && _showTagTargets && _showTagCognitive && _showTagStrategies && _showTagMicro);
            _showTagCore = anyOff;
            _showTagTargets = anyOff;
            _showTagCognitive = anyOff;
            _showTagStrategies = anyOff;
            _showTagMicro = anyOff;

            if (ChkTagCore != null) ChkTagCore.IsChecked = anyOff;
            if (ChkTagTargets != null) ChkTagTargets.IsChecked = anyOff;
            if (ChkTagCognitive != null) ChkTagCognitive.IsChecked = anyOff;
            if (ChkTagStrategies != null) ChkTagStrategies.IsChecked = anyOff;
            if (ChkTagMicro != null) ChkTagMicro.IsChecked = anyOff;

            if (IsLoaded) RenderBrain();
        }

        private void BtnCloseInspector_Click(object sender, RoutedEventArgs e)
        {
            if (NodeInspectorPanel != null) NodeInspectorPanel.Visibility = Visibility.Collapsed;
        }

        private void BtnCopyQuery_Click(object sender, RoutedEventArgs e)
        {
            if (InspectorQueryText != null && !string.IsNullOrEmpty(InspectorQueryText.Text))
            {
                try
                {
                    Clipboard.SetText(InspectorQueryText.Text);
                    BtnCopyQuery.Content = "COPIADO!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (s, args) =>
                    {
                        BtnCopyQuery.Content = "COPIAR";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch { }
            }
        }

        // ============================================================
        // CONTROLES DE NAVEGAÇÃO 3D (MOUSE ROTATION & ZOOM)
        // ============================================================
        private void BrainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (BrainCanvas == null) return;
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            BrainCanvas.CaptureMouse();
        }

        private void BrainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                _velRotY = deltaX * 0.006;
                _velRotX = -deltaY * 0.006;

                _rotY += _velRotY;
                _rotX += _velRotX;
                _rotX = Math.Clamp(_rotX, -1.35, 1.35);

                _dragStartPoint = currentPoint;
            }
        }

        private void BrainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && BrainCanvas != null)
            {
                _isDragging = false;
                BrainCanvas.ReleaseMouseCapture();
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.12 : 0.88;
            _zoomScale = Math.Clamp(_zoomScale * factor, 0.4, 3.0);
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomScale = Math.Clamp(_zoomScale * 1.2, 0.4, 3.0);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomScale = Math.Clamp(_zoomScale * 0.8, 0.4, 3.0);
        }

        private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _zoomScale = 1.0;
            _rotX = 0.25;
            _rotY = 0.0;
            _velRotX = 0.0;
            _velRotY = 0.0;
            if (CanvasTranslate != null)
            {
                CanvasTranslate.X = 0;
                CanvasTranslate.Y = 0;
            }
        }

        // ============================================================
        // CONSTRUÇÃO DO MAPA 3D DE ESTRATÉGIAS (RENDERBRAIN)
        // ============================================================
        private void RenderBrain()
        {
            if (BrainCanvas == null) return;

            BrainCanvas.Children.Clear();
            _nodes3D.Clear();
            _synapses3D.Clear();

            int strategyCount = 0;
            try { strategyCount = Engine_Learn_GetStrategyCount(); } catch { }

            var allStrategies = new List<LearnStrategyExport>();
            for (int i = 0; i < strategyCount; i++)
            {
                if (Engine_Learn_GetStrategy(i, out LearnStrategyExport strat) != 0)
                {
                    allStrategies.Add(strat);
                }
            }

            // Filtragem por Perfil de Hardware + busca viva
            var filtered = allStrategies.Where(s =>
            {
                bool hw;
                if (_currentFilter == "V7") hw = s.TargetId != null && s.TargetId.StartsWith("v7_");
                else if (_currentFilter == "M7") hw = s.TargetId != null && (s.TargetId.StartsWith("f7_") || s.TargetId.StartsWith("m7_"));
                else if (_currentFilter == "T20") hw = s.TargetId != null && (s.TargetId.StartsWith("t20_") || s.TargetId.StartsWith("nokia_"));
                else if (_currentFilter == "F1") hw = s.TargetId != null && !s.TargetId.StartsWith("v7_") && !s.TargetId.StartsWith("f7_") && !s.TargetId.StartsWith("m7_") && !s.TargetId.StartsWith("t20_");
                else hw = true;
                if (!hw) return false;
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    string q = (_searchText ?? "").Trim().ToLowerInvariant();
                    string tid = (s.TargetId ?? "").ToLowerInvariant();
                    string qry = (s.Query ?? "").ToLowerInvariant();
                    if (!tid.Contains(q) && !qry.Contains(q)) return false;
                }
                return true;
            }).ToList();

            var targetGroups = filtered.GroupBy(s => s.TargetId).OrderBy(g => GetEtapaOrder(g.Key)).ToList();

            // Atualização dos indicadores do cabeçalho
            int totalHits = filtered.Sum(s => s.Hits);
            int totalMisses = filtered.Sum(s => s.Misses);
            int totalOps = totalHits + totalMisses;
            int totalScore = filtered.Sum(s => s.Score);
            string globalAccuracy = totalOps > 0 ? $"{(double)totalHits / totalOps * 100.0:F1}%" : "—";
            string combinedHistory = string.Concat(filtered
                .OrderByDescending(s => s.Score)
                .Select(s => s.History ?? ""));
            string recentHistory = new string(combinedHistory.Where(c => c == '1' || c == '0').Take(10).ToArray());

            _knownStrategies = filtered.Count;
            _knownTargets = targetGroups.Count;
            _knownHits = totalHits;
            _knownMisses = totalMisses;
            _knownScoreSum = totalScore;
            _knownHistory = recentHistory;

            // A assinatura usa o total do motor (não o filtro), para que o timer
            // não re-renderize continuamente quando um filtro está ativo.
            _lastKnownSignature = (
                strategyCount,
                allStrategies.Sum(s => s.Hits),
                allStrategies.Sum(s => s.Misses),
                allStrategies.Sum(s => s.Score));

            TxtTotalNodes.Text = targetGroups.Count.ToString();
            TxtTotalStrategies.Text = filtered.Count.ToString();
            if (TxtTotalRules != null)
                TxtTotalRules.Text = totalOps.ToString();
            if (TxtTotalParameters != null)
                TxtTotalParameters.Text = totalScore.ToString("+0;-0;0");
            TxtGlobalAccuracy.Text = globalAccuracy;

            try
            {
                int active = Engine_RefreshDevices();
                if (TxtActiveDevices != null) TxtActiveDevices.Text = active.ToString();
            }
            catch { }

            // ------------------------------------------------------------
            // 1. NÓ CENTRAL (MOTOR DE ESTRATÉGIAS)
            // ------------------------------------------------------------
            Node3D coreNode = new Node3D
            {
                OrigX = 0,
                OrigY = 0,
                OrigZ = 0,
                BaseSize = 92,
                Type = "CORE",
                TargetId = "BotLearn_Core",
                BaseColor = (Color)ColorConverter.ConvertFromString("#58A6FF")
            };

            Border coreOrb = new Border
            {
                Background = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFFF"), 0.0),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#58A6FF"), 0.20),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#1F3A5F"), 0.60),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#0D1117"), 1.0)
                    }
                },
                BorderBrush = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#58A6FF"),
                    (Color)ColorConverter.ConvertFromString("#3FB950"),
                    new Point(0, 0),
                    new Point(1, 1)),
                BorderThickness = new Thickness(2.2),
                CornerRadius = new CornerRadius(46),
                Cursor = Cursors.Hand,
                ToolTip = "Motor de Estratégias BotLearn — clique para inspecionar"
            };

            Border innerRing = new Border
            {
                Margin = new Thickness(5),
                Background = new RadialGradientBrush((Color)ColorConverter.ConvertFromString("#0F1B2A"), (Color)ColorConverter.ConvertFromString("#0D1117")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#58A6FF")),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(40)
            };

            StackPanel coreContent = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock coreTitle = new TextBlock
            {
                Text = "BOTLEARN",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Black,
                FontSize = 8.5,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock coreVer = new TextBlock
            {
                Text = "MOTOR",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#58A6FF")),
                FontWeight = FontWeights.Black,
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            };

            Border coreBadge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F6FEB")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#58A6FF")),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            TextBlock coreBadgeTxt = new TextBlock
            {
                Text = "NÚCLEO",
                Foreground = Brushes.White,
                FontSize = 7.5,
                FontWeight = FontWeights.Bold
            };
            coreBadge.Child = coreBadgeTxt;

            coreContent.Children.Add(coreTitle);
            coreContent.Children.Add(coreVer);
            coreContent.Children.Add(coreBadge);

            innerRing.Child = coreContent;
            coreOrb.Child = innerRing;

            coreOrb.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowCoreInspector();
            };

            coreNode.VisualElement = coreOrb;

            Border coreFloatingBadge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD121222")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#58A6FF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 2, 6, 2),
                IsHitTestVisible = false
            };
            TextBlock coreFloatingBadgeTxt = new TextBlock
            {
                Text = "MOTOR DE ESTRATÉGIAS",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Black,
                FontSize = 9.5
            };
            coreFloatingBadge.Child = coreFloatingBadgeTxt;
            coreNode.VisualBadge = coreFloatingBadge;

            BrainCanvas.Children.Add(coreOrb);
            BrainCanvas.Children.Add(coreFloatingBadge);
            _nodes3D.Add(coreNode);

            // ------------------------------------------------------------
            // 3. CRIAR NÓS ALVOS (DISTRIBUÍDOS NA ESFERA 3D)
            // ------------------------------------------------------------
            double baseDim = Math.Min(ActualWidth > 0 ? ActualWidth : 1200, ActualHeight > 0 ? ActualHeight : 800);
            double sphereRadius = Math.Clamp(baseDim * 0.30, 200.0, 400.0);
            int numTargets = targetGroups.Count;

            var targetNodesList = new List<Node3D>();

            for (int i = 0; i < numTargets; i++)
            {
                var group = targetGroups[i];
                string tid = group.Key;

                // Distribuição esférica tipo Fibonacci
                double phi = Math.Acos(1.0 - 2.0 * (i + 0.5) / Math.Max(numTargets, 1));
                double theta = Math.PI * (1.0 + Math.Sqrt(5.0)) * i;

                // Ajuste por modelo (hemisférios visuais dedicados)
                if (tid.StartsWith("v7_")) phi = Math.Clamp(phi * 0.7, 0.2, 1.2); // Norte
                else if (tid.StartsWith("f7_") || tid.StartsWith("m7_")) theta += 1.2; // Leste
                else if (tid.StartsWith("t20_") || tid.StartsWith("nokia_")) theta -= 1.2; // Oeste
                else phi = Math.PI - Math.Clamp(phi * 0.7, 0.2, 1.2); // Sul

                double tx = sphereRadius * Math.Sin(phi) * Math.Cos(theta);
                double ty = sphereRadius * Math.Cos(phi);
                double tz = sphereRadius * Math.Sin(phi) * Math.Sin(theta);

                Color targetColor;
                if (tid.StartsWith("v7_")) targetColor = (Color)ColorConverter.ConvertFromString("#3FB950");
                else if (tid.StartsWith("f7_") || tid.StartsWith("m7_")) targetColor = (Color)ColorConverter.ConvertFromString("#D29922");
                else if (tid.StartsWith("t20_") || tid.StartsWith("nokia_")) targetColor = (Color)ColorConverter.ConvertFromString("#58A6FF");
                else targetColor = (Color)ColorConverter.ConvertFromString("#8B98A7");

                Node3D targetNode = new Node3D
                {
                    OrigX = tx,
                    OrigY = ty,
                    OrigZ = tz,
                    BaseSize = 26,
                    Type = "TARGET",
                    TargetId = tid,
                    BaseColor = targetColor,
                    TargetStrategies = group.ToList()
                };

                Border targetOrb = new Border
                {
                    Background = new SolidColorBrush(targetColor),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1.8),
                    CornerRadius = new CornerRadius(13),
                    Cursor = Cursors.Hand,
                    ToolTip = $"{FormatarTargetName(tid)} ({group.Count()} estratégias)"
                };

                string friendly = FormatarTargetName(tid);
                targetOrb.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    ShowTargetInspector(tid, targetNode.TargetStrategies ?? new List<LearnStrategyExport>());
                };

                Border targetBadge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD121222")),
                    BorderBrush = new SolidColorBrush(targetColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 2, 6, 2),
                    IsHitTestVisible = false
                };
                TextBlock targetBadgeTxt = new TextBlock
                {
                    Text = friendly,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10
                };
                targetBadge.Child = targetBadgeTxt;

                targetNode.VisualElement = targetOrb;
                targetNode.VisualBadge = targetBadge;
                BrainCanvas.Children.Add(targetOrb);
                BrainCanvas.Children.Add(targetBadge);
                _nodes3D.Add(targetNode);
                targetNodesList.Add(targetNode);

                // Sinapse do Núcleo Central para o Alvo
                CriarSinapse(coreNode, targetNode, 100, true);

                // ------------------------------------------------------------
                // 4. CRIAR ESTRATÉGIAS SATÉLITES DO ALVO
                // ------------------------------------------------------------
                var strats = group.ToList();
                int numStrats = strats.Count;
                double satDist = 85.0;

                for (int j = 0; j < numStrats; j++)
                {
                    var strat = strats[j];
                    double satAngle = (2.0 * Math.PI / Math.Max(numStrats, 1)) * j;

                    // Vetores perpendiculares locais para orbitar ao redor do alvo
                    double nx = tx / sphereRadius;
                    double ny = ty / sphereRadius;
                    double nz = tz / sphereRadius;

                    double upX = 0, upY = 1, upZ = 0;
                    if (Math.Abs(ny) > 0.9) { upX = 1; upY = 0; upZ = 0; }

                    // Produto vetorial u x n
                    double rx = upY * nz - upZ * ny;
                    double ry = upZ * nx - upX * nz;
                    double rz = upX * ny - upY * nx;
                    double rLen = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                    rx /= rLen; ry /= rLen; rz /= rLen;

                    // Produto vetorial n x r
                    double ux = ny * rz - nz * ry;
                    double uy = nz * rx - nx * rz;
                    double uz = nx * ry - ny * rx;

                    double sx = tx + (nx * 20.0) + (rx * Math.Cos(satAngle) + ux * Math.Sin(satAngle)) * satDist;
                    double sy = ty + (ny * 20.0) + (ry * Math.Cos(satAngle) + uy * Math.Sin(satAngle)) * satDist;
                    double sz = tz + (nz * 20.0) + (rz * Math.Cos(satAngle) + uz * Math.Sin(satAngle)) * satDist;

                    Color stratColor;
                    if (strat.Score > 50) stratColor = (Color)ColorConverter.ConvertFromString("#3FB950");
                    else if (strat.Score > 0) stratColor = (Color)ColorConverter.ConvertFromString("#D29922");
                    else if (strat.Score == 0) stratColor = (Color)ColorConverter.ConvertFromString("#8B98A7");
                    else stratColor = (Color)ColorConverter.ConvertFromString("#F85149");

                    Node3D stratNode = new Node3D
                    {
                        OrigX = sx,
                        OrigY = sy,
                        OrigZ = sz,
                        BaseSize = 13,
                        Type = "STRATEGY",
                        TargetId = tid,
                        BaseColor = stratColor,
                        StrategyData = strat
                    };

                    Border stratOrb = new Border
                    {
                        Background = new SolidColorBrush(stratColor),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(7),
                        Cursor = Cursors.Hand,
                        ToolTip = $"{strat.Query} (Score: {strat.Score})"
                    };

                    stratOrb.MouseLeftButtonDown += (s, e) =>
                    {
                        e.Handled = true;
                        ShowStrategyInspector(strat);
                    };

                    stratNode.VisualElement = stratOrb;

                    Border stratBadge = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD121222")),
                        BorderBrush = new SolidColorBrush(stratColor),
                        BorderThickness = new Thickness(0.8),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(4, 1, 4, 1),
                        IsHitTestVisible = false
                    };
                    string qShort = strat.Query;
                    if (qShort.Length > 16) qShort = qShort.Substring(0, 14) + "..";
                    TextBlock stratBadgeTxt = new TextBlock
                    {
                        Text = $"{qShort} ({strat.Score})",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Normal,
                        FontSize = 8.5
                    };
                    stratBadge.Child = stratBadgeTxt;
                    stratNode.VisualBadge = stratBadge;

                    BrainCanvas.Children.Add(stratOrb);
                    BrainCanvas.Children.Add(stratBadge);
                    _nodes3D.Add(stratNode);

                    // Sinapse do Alvo para a Estratégia
                    CriarSinapse(targetNode, stratNode, strat.Score, false);
                }
            }

            // ------------------------------------------------------------
            // 5. COMPONENTES CONCEITUAIS DO MOTOR
            // ------------------------------------------------------------
            var cognitiveNodes = new (string Name, string Role, string Details, string Symbol)[]
            {
                ("Seed compilado", "Ordem determinística", "Estratégias iniciais embarcadas na DLL e usadas no modo determinístico.", ""),
                ("Leitura de alvos", "UIAutomator e visão", "Localiza elementos por query, texto e estrutura da tela.", ""),
                ("Fallback de seletores", "Rotas alternativas", "Usa a próxima estratégia quando a primeira não encontra o alvo.", ""),
                ("Feedback de execução", "Acertos e falhas", "Atualiza o score nos fluxos que registram acerto ou falha.", ""),
                ("Persistência", "bot_learn.json", "Persiste scores e histórico apenas quando o modo adaptativo está ativo.", ""),
                ("Ranking por score", "Ordenação heurística", "Ordena os seletores de cada alvo pelo score atual.", ""),
                ("Histórico recente", "Até 10 execuções", "Mantém os últimos resultados registrados por seletor.", ""),
                ("Escopo atual", "F1, V7, M7 e T20", "Alvos embarcados; a cobertura de feedback varia conforme o modelo.", ""),
                ("Limites do motor", "16 seletores por alvo", "Teto compilado para o ranking de cada alvo.", ""),
                ("Modo de operação", "Determinístico ou adaptativo", "Controlado nas Configurações; o modo adaptativo persiste dados.", ""),
                ("Filtro de hardware", "Modelos suportados", "Segmenta alvos e seletores por modelo de tablet.", ""),
                ("Auditoria visual", "Somente leitura", "Este mapa exibe o estado atual; alterações são feitas no fluxo do bot.", "")
            };

            double cogRadius = sphereRadius * 0.61;
            var createdCogNodes = new List<Node3D>();
            for (int k = 0; k < cognitiveNodes.Length; k++)
            {
                var cog = cognitiveNodes[k];
                double phi = Math.Acos(1.0 - 2.0 * (k + 0.5) / cognitiveNodes.Length);
                double theta = Math.PI * (1.0 + Math.Sqrt(5.0)) * k;

                double cx = cogRadius * Math.Sin(phi) * Math.Cos(theta);
                double cy = cogRadius * Math.Cos(phi);
                double cz = cogRadius * Math.Sin(phi) * Math.Sin(theta);

                Color cogColor = (Color)ColorConverter.ConvertFromString("#58A6FF");

                Node3D cogNode = new Node3D
                {
                    OrigX = cx,
                    OrigY = cy,
                    OrigZ = cz,
                    BaseSize = 18,
                    Type = "COGNITIVE",
                    TargetId = cog.Name,
                    BaseColor = cogColor
                };

                Border cogOrb = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#151B23")),
                    BorderBrush = new SolidColorBrush(cogColor),
                    BorderThickness = new Thickness(1.4),
                    CornerRadius = new CornerRadius(9),
                    Cursor = Cursors.Hand,
                    ToolTip = $"{cog.Name} — {cog.Role}"
                };
                TextBlock cogTxt = new TextBlock
                {
                    Text = cog.Symbol,
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                cogOrb.Child = cogTxt;

                cogOrb.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    ShowCognitiveInspector(cog.Name, cog.Role, cog.Details);
                };

                cogNode.VisualElement = cogOrb;

                Border cogBadge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD121222")),
                    BorderBrush = new SolidColorBrush(cogColor),
                    BorderThickness = new Thickness(0.9),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 1.5, 5, 1.5),
                    IsHitTestVisible = false
                };
                TextBlock cogBadgeTxt = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(cog.Symbol) ? cog.Name : $"{cog.Symbol} {cog.Name}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 9
                };
                cogBadge.Child = cogBadgeTxt;
                cogNode.VisualBadge = cogBadge;

                BrainCanvas.Children.Add(cogOrb);
                BrainCanvas.Children.Add(cogBadge);
                _nodes3D.Add(cogNode);
                createdCogNodes.Add(cogNode);

                // Conexão do motor com o componente conceitual
                CriarSinapse(coreNode, cogNode, 100, true);
            }

            // ------------------------------------------------------------
            // 6. CONEXÕES VISUAIS DO MAPA
            // Em filtros específicos (V7/M7/T20), oculta a malha decorativa.
            // ------------------------------------------------------------
            int microCount = (_currentFilter == "ALL") ? 24 : 0;
            for (int m = 0; m < microCount; m++)
            {
                double mRadius = (m % 2 == 0) ? sphereRadius * 0.82 : sphereRadius * 1.15;
                double phi = Math.Acos(1.0 - 2.0 * (m + 0.5) / microCount);
                double theta = Math.PI * (1.0 + Math.Sqrt(5.0)) * m * 1.35;

                double mx = mRadius * Math.Sin(phi) * Math.Cos(theta);
                double my = mRadius * Math.Cos(phi);
                double mz = mRadius * Math.Sin(phi) * Math.Sin(theta);

                Color mColor = (m % 3 == 0)
                    ? (Color)ColorConverter.ConvertFromString("#3FB950")
                    : (m % 3 == 1)
                        ? (Color)ColorConverter.ConvertFromString("#58A6FF")
                        : (Color)ColorConverter.ConvertFromString("#8B98A7");

                Node3D mNode = new Node3D
                {
                    OrigX = mx,
                    OrigY = my,
                    OrigZ = mz,
                    BaseSize = 8,
                    Type = "MICRO",
                    TargetId = $"ConexaoVisual_{m + 1}",
                    BaseColor = mColor
                };

                Border mOrb = new Border
                {
                    Background = new SolidColorBrush(mColor),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(0.8),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand,
                    ToolTip = $"Conexão visual #{m + 1:D2} (elemento ilustrativo)"
                };

                int microId = m + 1;
                string microType = (m % 2 == 0) ? "Anel interno" : "Anel externo";
                mOrb.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    ShowMicroNeuronInspector(microId, microType);
                };

                mNode.VisualElement = mOrb;

                Border mBadge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD121222")),
                    BorderBrush = new SolidColorBrush(mColor),
                    BorderThickness = new Thickness(0.6),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(3, 1, 3, 1),
                    IsHitTestVisible = false
                };
                TextBlock mBadgeTxt = new TextBlock
                {
                    Text = $"Conexão #{m + 1:D2}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Normal,
                    FontSize = 7.5
                };
                mBadge.Child = mBadgeTxt;
                mNode.VisualBadge = mBadge;

                BrainCanvas.Children.Add(mOrb);
                BrainCanvas.Children.Add(mBadge);
                _nodes3D.Add(mNode);

                // Conectar a conexão visual ao componente conceitual mais próximo
                if (createdCogNodes.Count > 0)
                {
                    int nearestCogIdx = m % createdCogNodes.Count;
                    CriarSinapseLateral(createdCogNodes[nearestCogIdx], mNode, mColor);
                }
            }

            // ------------------------------------------------------------
            // 7. CONEXÕES LATERAIS DE FLUXO (PIPELINE ENTRE ALVOS)
            // ------------------------------------------------------------
            for (int i = 0; i < targetNodesList.Count - 1; i++)
            {
                CriarSinapseLateral(targetNodesList[i], targetNodesList[i + 1], targetNodesList[i].BaseColor);
            }

            EnsureRenderLoop();
        }

        private void CriarSinapse(Node3D from, Node3D to, int score, bool isCore)
        {
            Color c;
            if (isCore) c = (Color)ColorConverter.ConvertFromString("#58A6FF");
            else if (score > 50) c = (Color)ColorConverter.ConvertFromString("#3FB950");
            else if (score > 0) c = (Color)ColorConverter.ConvertFromString("#D29922");
            else if (score == 0) c = (Color)ColorConverter.ConvertFromString("#8B98A7");
            else c = (Color)ColorConverter.ConvertFromString("#F85149");

            Line line = new Line
            {
                Stroke = new SolidColorBrush(c),
                StrokeThickness = isCore ? 1.6 : 1.0,
                Opacity = isCore ? 0.6 : 0.4,
                IsHitTestVisible = false
            };

            BrainCanvas.Children.Add(line);

            _synapses3D.Add(new Synapse3D
            {
                From = from,
                To = to,
                VisualLine = line,
                BaseColor = c,
                IsCore = isCore
            });
        }

        private void CriarSinapseLateral(Node3D from, Node3D to, Color c)
        {
            Line line = new Line
            {
                Stroke = new SolidColorBrush(c),
                StrokeThickness = 0.8,
                Opacity = 0.28,
                IsHitTestVisible = false
            };

            BrainCanvas.Children.Add(line);

            _synapses3D.Add(new Synapse3D
            {
                From = from,
                To = to,
                VisualLine = line,
                BaseColor = c,
                IsCore = false
            });
        }

        // ============================================================
        // LOOP DE RENDERIZAÇÃO E PROJEÇÃO 3D (60 FPS NATIVO)
        // ============================================================
        private void EnsureRenderLoop()
        {
            if (_animationStopwatch == null)
            {
                _animationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                _lastFrameSeconds = _animationStopwatch.Elapsed.TotalSeconds;
            }
            if (!_isRenderingSubscribed)
            {
                CompositionTarget.Rendering += OnCompositionTargetRendering;
                _isRenderingSubscribed = true;
            }
        }

        private void OnCompositionTargetRendering(object? sender, EventArgs e)
        {
            if (_animationStopwatch == null) return;
            if (WindowState == WindowState.Minimized || !IsVisible) return;

            double currentSec = _animationStopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentSec - _lastFrameSeconds;
            if (deltaTime < 1.0 / 30.0) return; // Throttle 30fps para iGPU fraca
            _lastFrameSeconds = currentSec;

            if (deltaTime > 0.1) deltaTime = 0.033;

            // Rotação suave contínua quando o usuário não estiver arrastando
            if (!_isDragging)
            {
                _rotY += (0.16 + _velRotY) * deltaTime;
                _rotX += _velRotX * deltaTime;
                _rotX = Math.Clamp(_rotX, -1.35, 1.35);

                _velRotY *= 0.94;
                _velRotX *= 0.94;
            }

            double centerX = ActualWidth > 0 ? ActualWidth / 2.0 : 600;
            double centerY = ActualHeight > 0 ? ActualHeight / 2.0 : 400;
            double cameraD = 1000.0;

            double cosY = Math.Cos(_rotY), sinY = Math.Sin(_rotY);
            double cosX = Math.Cos(_rotX), sinX = Math.Sin(_rotX);

            // 1. ATUALIZAR E PROJETAR NÓS 3D
            for (int i = 0; i < _nodes3D.Count; i++)
            {
                var n = _nodes3D[i];

                // Rotação 3D Yaw (em Y)
                double x1 = n.OrigX * cosY + n.OrigZ * sinY;
                double y1 = n.OrigY;
                double z1 = -n.OrigX * sinY + n.OrigZ * cosY;

                // Rotação 3D Pitch (em X)
                double x2 = x1;
                double y2 = y1 * cosX - z1 * sinX;
                double z2 = y1 * sinX + z1 * cosX;

                n.RotX = x2;
                n.RotY = y2;
                n.RotZ = z2;

                // Projeção com Perspectiva
                double dist = cameraD - z2;
                if (dist < 100) dist = 100;
                double k = (cameraD / dist) * _zoomScale;
                n.Scale = k;

                double sx = centerX + x2 * k;
                double sy = centerY + y2 * k;
                n.ScreenX = sx;
                n.ScreenY = sy;

                double size = n.BaseSize * k;
                n.ZIndex = (int)(z2 + 2000.0);

                // Profundidade (Z-Buffering / Fog Visual)
                double zNorm = Math.Clamp((z2 + 350.0) / 700.0, 0.0, 1.0);
                n.Opacity = 0.25 + (0.75 * zNorm);

                bool isTypeVisible = n.Type switch
                {
                    "CORE" => _showTagCore,
                    "TARGET" => _showTagTargets,
                    "COGNITIVE" => _showTagCognitive,
                    "STRATEGY" => _showTagStrategies,
                    "MICRO" => _showTagMicro,
                    _ => true
                };
                // Busca viva: dim nuts não correspondentes, destaca match com opacidade cheia
                bool isSearchMatch = true;
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    string q = _searchText.Trim().ToLowerInvariant();
                    string tid = (n.TargetId ?? "").ToLowerInvariant();
                    string qry = n.Type == "STRATEGY" ? (n.StrategyData.Query ?? "").ToLowerInvariant() : "";
                    isSearchMatch = tid.Contains(q) || qry.Contains(q) || n.Type == "CORE";
                }
                if (!isTypeVisible)
                {
                    n.VisualElement.Visibility = Visibility.Collapsed;
                    if (n.VisualBadge != null) n.VisualBadge.Visibility = Visibility.Collapsed;
                    continue;
                }
                n.VisualElement.Visibility = Visibility.Visible;

                n.VisualElement.Width = size;
                n.VisualElement.Height = size;
                n.VisualElement.Opacity = isSearchMatch ? n.Opacity : 0.12;
                Canvas.SetLeft(n.VisualElement, sx - size / 2.0);
                Canvas.SetTop(n.VisualElement, sy - size / 2.0);
                Panel.SetZIndex(n.VisualElement, n.ZIndex);

                // Atualizar Badge do Alvo / Nós com Tags
                if (n.VisualBadge != null)
                {
                    if (!isSearchMatch || z2 < -80)
                    {
                        n.VisualBadge.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        n.VisualBadge.Visibility = Visibility.Visible;
                        n.VisualBadge.Opacity = Math.Clamp((z2 + 80.0) / 180.0, 0.0, 1.0);
                        Canvas.SetLeft(n.VisualBadge, sx + size / 2.0 + 5.0);
                        Canvas.SetTop(n.VisualBadge, sy - 10.0);
                        Panel.SetZIndex(n.VisualBadge, n.ZIndex + 1);
                    }
                }
            }

            // 2. ATUALIZAR SINAPSES 3D (oculta se endpoint filtrado)
            for (int i = 0; i < _synapses3D.Count; i++)
            {
                var syn = _synapses3D[i];
                if (syn.From.VisualElement.Visibility != Visibility.Visible || syn.To.VisualElement.Visibility != Visibility.Visible)
                {
                    syn.VisualLine.Visibility = Visibility.Collapsed;
                    continue;
                }
                syn.VisualLine.Visibility = Visibility.Visible;
                syn.VisualLine.X1 = syn.From.ScreenX;
                syn.VisualLine.Y1 = syn.From.ScreenY;
                syn.VisualLine.X2 = syn.To.ScreenX;
                syn.VisualLine.Y2 = syn.To.ScreenY;

                double avgZ = (syn.From.RotZ + syn.To.RotZ) / 2.0;
                double zNorm = Math.Clamp((avgZ + 350.0) / 700.0, 0.0, 1.0);
                double synOpacity = (syn.IsCore ? 0.45 : 0.25) + (0.50 * zNorm);
                syn.VisualLine.Opacity = synOpacity;
                Panel.SetZIndex(syn.VisualLine, (int)(avgZ + 1950.0));
            }
        }

        // ============================================================
        // PAINEL INSPECTOR (DETALHES HOLOGRÁFICOS)
        // ============================================================
        private void ShowStrategyInspector(LearnStrategyExport strat)
        {
            InspectorTitle.Text = FormatarTargetName(strat.TargetId);
            InspectorSubtitle.Text = $"Seletor: {strat.TargetId}";
            InspectorQueryText.Text = strat.Query;
            InspectorScoreText.Text = $"{strat.Score:+0;-0;0}";

            int total = strat.Hits + strat.Misses;
            InspectorAccuracyText.Text = total > 0 ? $"{(double)strat.Hits / total * 100.0:F1}%" : "—";
            InspectorHitsText.Text = strat.Hits.ToString();
            InspectorMissesText.Text = strat.Misses.ToString();

            RenderHistoryDots(strat.History);
            NodeInspectorPanel.Visibility = Visibility.Visible;
        }

        private void ShowTargetInspector(string targetId, List<LearnStrategyExport> strats)
        {
            InspectorTitle.Text = FormatarTargetName(targetId);
            InspectorSubtitle.Text = $"Alvo: {targetId} · {strats.Count} seletores · {GetEtapaLabel(targetId)}";
            var ordered = strats.OrderByDescending(s => s.Score).ToList();
            var lines = ordered.Select(s => $"[{s.Score:+0;-0;0}] {s.Query} (H:{s.Hits}/M:{s.Misses})");
            InspectorQueryText.Text = $"Alvo: {targetId}\nEtapa: {GetEtapaLabel(targetId)}\n\n" + string.Join("\n", lines);

            double avgScore = strats.Count > 0 ? strats.Average(s => s.Score) : 0;
            int totalHits = strats.Sum(s => s.Hits);
            int totalMisses = strats.Sum(s => s.Misses);
            int total = totalHits + totalMisses;

            InspectorScoreText.Text = strats.Count > 0 ? $"{avgScore:+0.0;-0.0;0.0}" : "—";
            InspectorAccuracyText.Text = total > 0 ? $"{(double)totalHits / total * 100.0:F1}%" : "—";
            InspectorHitsText.Text = totalHits.ToString();
            InspectorMissesText.Text = totalMisses.ToString();

            string combined = string.Concat(ordered.Select(s => s.History ?? ""));
            string agg = new string(combined.Where(c => c == '1' || c == '0').Take(10).ToArray());
            RenderHistoryDots(agg);
            NodeInspectorPanel.Visibility = Visibility.Visible;
        }

        private void ShowCognitiveInspector(string name, string role, string details)
        {
            InspectorTitle.Text = name;
            InspectorSubtitle.Text = $"Componente conceitual · {role}";
            InspectorQueryText.Text = $"{details}\n\nEste nó representa uma etapa conceitual do motor BotLearn; não possui telemetria independente.";
            InspectorScoreText.Text = "—";
            InspectorAccuracyText.Text = "—";
            InspectorHitsText.Text = "—";
            InspectorMissesText.Text = "—";
            InspectorHistoryDots.Items.Clear();
            NodeInspectorPanel.Visibility = Visibility.Visible;
        }

        private void ShowMicroNeuronInspector(int id, string type)
        {
            InspectorTitle.Text = $"Conexão visual #{id:D2}";
            InspectorSubtitle.Text = $"Elemento ilustrativo do mapa · {type}";
            InspectorQueryText.Text = "Conexão visual sem telemetria própria. Serve apenas para organizar a leitura do mapa 3D.";
            InspectorScoreText.Text = "—";
            InspectorAccuracyText.Text = "—";
            InspectorHitsText.Text = "—";
            InspectorMissesText.Text = "—";
            InspectorHistoryDots.Items.Clear();
            NodeInspectorPanel.Visibility = Visibility.Visible;
        }

        private void ShowCoreInspector()
        {
            int total = _knownHits + _knownMisses;
            double avgScore = _knownStrategies > 0 ? (double)_knownScoreSum / _knownStrategies : 0;

            InspectorTitle.Text = "MOTOR DE ESTRATÉGIAS BOTLEARN";
            InspectorSubtitle.Text = "Núcleo do motor nativo";
            InspectorQueryText.Text =
                $"Motor: BotEngine.dll (Win32)\n" +
                $"Alvos visíveis: {_knownTargets}\n" +
                $"Seletores visíveis: {_knownStrategies}\n" +
                $"Execuções registradas: {total}\n\n" +
                "Este painel exibe os dados atuais do mapa; alterações são feitas no fluxo do bot.";
            InspectorScoreText.Text = _knownStrategies > 0 ? $"{avgScore:+0.0;-0.0;0.0}" : "—";
            InspectorAccuracyText.Text = total > 0 ? $"{(double)_knownHits / total * 100.0:F1}%" : "—";
            InspectorHitsText.Text = _knownHits.ToString();
            InspectorMissesText.Text = _knownMisses.ToString();
            RenderHistoryDots(_knownHistory);
            NodeInspectorPanel.Visibility = Visibility.Visible;
        }

        private void RenderHistoryDots(string history)
        {
            InspectorHistoryDots.Items.Clear();
            if (string.IsNullOrEmpty(history)) return;

            foreach (char ch in history.Take(10))
            {
                Ellipse dot = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Margin = new Thickness(0, 0, 6, 0),
                    Fill = ch == '1'
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FB950"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F85149"))
                };
                InspectorHistoryDots.Items.Add(dot);
            }
        }

        private static string FormatarTargetName(string targetId)
        {
            return targetId switch
            {
                "v7_spinner_municipio"     => "Gaveta Município (V7)",
                "v7_campo_email"           => "E-mail Aplicador (V7)",
                "v7_campo_senha"           => "Senha Aplicador (V7)",
                "v7_botao_entrar"          => "Botão Entrar (V7)",
                "v7_botao_importar_zip"    => "Menu Importar (V7)",
                "v7_btn_procurar_arquivo"  => "Procurar ZIP (V7)",
                "v7_btn_sincronizar_agora" => "Sincronizar (V7)",
                "f7_spinner_municipio"     => "Gaveta Município (M7)",
                "f7_campo_email"           => "E-mail Aplicador (M7)",
                "f7_campo_senha"           => "Senha Aplicador (M7)",
                "f7_botao_entrar"          => "Botão Entrar (M7)",
                "f7_btn_procurar_arquivo"  => "Procurar ZIP (M7)",
                "t20_spinner_municipio"    => "Gaveta Município (T20)",
                "t20_campo_email"          => "E-mail Aplicador (T20)",
                "t20_campo_senha"          => "Senha Aplicador (T20)",
                "t20_botao_entrar"         => "Botão Entrar (T20)",
                "t20_botao_importar_zip"   => "Menu Importar (T20)",
                "t20_btn_procurar_arquivo" => "Procurar ZIP (T20)",
                "spinner_municipio"        => "Gaveta Município (F1)",
                "campo_email"              => "E-mail Aplicador (F1)",
                "campo_senha"              => "Senha Aplicador (F1)",
                "botao_entrar"             => "Botão Entrar (F1)",
                "botao_importar_zip"       => "Menu Importar (F1)",
                "f1_btn_procurar_arquivo"  => "Procurar ZIP (F1)",
                _                          => targetId
            };
        }

        private static int GetEtapaOrder(string targetId)
        {
            string t = (targetId ?? "").ToLowerInvariant();
            if (t.Contains("spinner_municipio")) return 2;
            if (t.Contains("campo_email")) return 3;
            if (t.Contains("campo_senha")) return 4;
            if (t.Contains("botao_entrar")) return 5;
            if (t.Contains("importar_zip")) return 6;
            if (t.Contains("procurar_arquivo")) return 7;
            if (t.Contains("sincronizar")) return 8;
            return 99;
        }

        private static string GetEtapaLabel(string targetId)
        {
            return GetEtapaOrder(targetId) switch
            {
                2 => "Etapa 2: Município",
                3 => "Etapa 3: E-mail",
                4 => "Etapa 4a: Senha",
                5 => "Etapa 4b: Entrar",
                6 => "Etapa 5: Importar ZIP",
                7 => "Etapa 6: Procurar arquivo",
                8 => "Etapa F6: Sincronizar",
                _ => "Etapa —"
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                _searchText = tb.Text ?? "";
                if (SearchWatermark != null)
                    SearchWatermark.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;

                if (_searchDebounce == null)
                {
                    _searchDebounce = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(280)
                    };
                    _searchDebounce.Tick += (_, _) =>
                    {
                        _searchDebounce?.Stop();
                        if (IsLoaded) RenderBrain();
                    };
                }
                _searchDebounce.Stop();
                _searchDebounce.Start();
            }
        }

        private void TopHeaderPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }
    }
}
