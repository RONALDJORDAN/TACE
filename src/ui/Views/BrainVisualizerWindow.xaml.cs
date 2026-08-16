using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace InnovPlayUI.Views
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

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategyCount();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategy(int index, out LearnStrategyExport outStrategy);

        private class SynapsePulse
        {
            public Ellipse Particle { get; set; } = null!;
            public double StartX { get; set; }
            public double StartY { get; set; }
            public double EndX { get; set; }
            public double EndY { get; set; }
            public double Progress { get; set; }
            public double Speed { get; set; }
            public double BaseSize { get; set; }
            public bool Reversed { get; set; }
        }

        // Estado Interno
        private string _currentFilter = "ALL";
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Point _translateStartPoint;
        private double _zoomScale = 1.0;
        private bool _uiVisible = true;
        private System.Diagnostics.Stopwatch? _animationStopwatch;
        private double _lastFrameSeconds;
        private bool _isRenderingSubscribed = false;
        private readonly List<SynapsePulse> _synapsePulses = new List<SynapsePulse>();
        private readonly Random _random = new Random();

        public BrainVisualizerWindow()
        {
            InitializeComponent();

            // Garante que o Mapa Neural abra no mesmo monitor onde a janela principal do sistema está ativa
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
            base.OnClosed(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RenderBrain();
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

        // ============================================================
        // FILTROS E BUSCA DINÂMICA DE NÓS
        // ============================================================
        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || BrainCanvas == null || NodeInspectorPanel == null) return;
            if (sender is RadioButton btn && btn.Tag != null)
            {
                _currentFilter = btn.Tag.ToString() ?? "ALL";
                NodeInspectorPanel.Visibility = Visibility.Collapsed;
                RenderBrain();
                ApplySearchFilter();
            }
        }

        private void ApplySearchFilter()
        {
            // Busca por texto desativada no header
        }

        private void BtnCopyQuery_Click(object sender, RoutedEventArgs e)
        {
            if (InspectorQueryText != null && !string.IsNullOrEmpty(InspectorQueryText.Text))
            {
                try
                {
                    Clipboard.SetText(InspectorQueryText.Text);
                    BtnCopyQuery.Content = "✓ COPIADO!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (s, args) =>
                    {
                        BtnCopyQuery.Content = "📋 COPIAR";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrainVisualizer] Erro ao copiar: {ex.Message}");
                }
            }
        }

        // ============================================================
        // NAVEGAÇÃO INTERATIVA (PAN & ZOOM)
        // ============================================================
        private void BrainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (BrainCanvas == null || CanvasTranslate == null) return;
            if (e.OriginalSource == BrainCanvas || e.OriginalSource is Path || e.OriginalSource is Canvas || e.OriginalSource is Line)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(this);
                _translateStartPoint = new Point(CanvasTranslate.X, CanvasTranslate.Y);
                BrainCanvas.CaptureMouse();
            }
        }

        private void BrainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && CanvasTranslate != null)
            {
                Point currentPoint = e.GetPosition(this);
                Vector delta = currentPoint - _dragStartPoint;
                CanvasTranslate.X = _translateStartPoint.X + delta.X;
                CanvasTranslate.Y = _translateStartPoint.Y + delta.Y;
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
            if (BrainCanvas == null) return;
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
            ApplyZoom(zoomFactor, e.GetPosition(BrainCanvas));
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (BrainCanvas == null) return;
            ApplyZoom(1.25, new Point(BrainCanvas.ActualWidth / 2, BrainCanvas.ActualHeight / 2));
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (BrainCanvas == null) return;
            ApplyZoom(0.75, new Point(BrainCanvas.ActualWidth / 2, BrainCanvas.ActualHeight / 2));
        }

        private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasScale == null || CanvasTranslate == null) return;
            _zoomScale = 1.0;
            CanvasScale.CenterX = SystemParameters.PrimaryScreenWidth / 2;
            CanvasScale.CenterY = SystemParameters.PrimaryScreenHeight / 2;
            CanvasScale.ScaleX = 1.0;
            CanvasScale.ScaleY = 1.0;
            CanvasTranslate.X = 0;
            CanvasTranslate.Y = 0;
        }

        private void ApplyZoom(double factor, Point center)
        {
            if (CanvasScale == null) return;
            double newScale = _zoomScale * factor;
            if (newScale < 0.3 || newScale > 3.5) return;

            _zoomScale = newScale;
            CanvasScale.CenterX = SystemParameters.PrimaryScreenWidth / 2;
            CanvasScale.CenterY = SystemParameters.PrimaryScreenHeight / 2;
            CanvasScale.ScaleX = _zoomScale;
            CanvasScale.ScaleY = _zoomScale;
        }

        private void BtnCloseInspector_Click(object sender, RoutedEventArgs e)
        {
            if (NodeInspectorPanel != null)
            {
                NodeInspectorPanel.Visibility = Visibility.Collapsed;
            }
        }

        // ============================================================
        // RENDERIZAÇÃO DO GRAFO NEURAL SCI-FI ADAPTATIVO
        // ============================================================
        private void RenderBrain()
        {
            if (BrainCanvas == null || TxtTotalNodes == null || TxtTotalStrategies == null || TxtGlobalAccuracy == null) return;
            BrainCanvas.Children.Clear();
            _synapsePulses.Clear();

            double screenWidth = SystemParameters.PrimaryScreenWidth > 0 ? SystemParameters.PrimaryScreenWidth : 1920;
            double screenHeight = SystemParameters.PrimaryScreenHeight > 0 ? SystemParameters.PrimaryScreenHeight : 1080;

            double centerX = screenWidth / 2;
            double centerY = screenHeight / 2;

            var rawStrategies = new List<LearnStrategyExport>();
            try
            {
                int count = Engine_Learn_GetStrategyCount();
                for (int i = 0; i < count; i++)
                {
                    Engine_Learn_GetStrategy(i, out LearnStrategyExport strategy);
                    rawStrategies.Add(strategy);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrainVisualizer] Erro ao carregar estratégias em C: {ex.Message}");
            }

            var filteredStrategies = rawStrategies.Where(s =>
            {
                if (_currentFilter == "V7") return s.TargetId != null && s.TargetId.StartsWith("v7_");
                if (_currentFilter == "M7") return s.TargetId != null && (s.TargetId.StartsWith("f7_") || s.TargetId.StartsWith("m7_"));
                if (_currentFilter == "T20") return s.TargetId != null && (s.TargetId.StartsWith("t20_") || s.TargetId.StartsWith("nokia_"));
                if (_currentFilter == "F1") return s.TargetId != null && !s.TargetId.StartsWith("v7_") && !s.TargetId.StartsWith("f7_") && !s.TargetId.StartsWith("m7_") && !s.TargetId.StartsWith("t20_") && !s.TargetId.StartsWith("nokia_");
                return true; // ALL
            }).ToList();


            var targets = filteredStrategies.GroupBy(s => s.TargetId).ToList();

            int totalNodes = targets.Count + filteredStrategies.Count + 1;
            int totalHits = filteredStrategies.Sum(s => s.Hits);
            int totalMisses = filteredStrategies.Sum(s => s.Misses);
            int totalOps = totalHits + totalMisses;
            int globalAccuracy = totalOps > 0 ? (int)Math.Round((double)totalHits / totalOps * 100.0) : 100;

            TxtTotalNodes.Text = totalNodes.ToString();
            TxtTotalStrategies.Text = filteredStrategies.Count.ToString();
            
            // Cálculo do Quantitativo de Parâmetros Neurais da IA Adaptativa
            int totalParameters = (targets.Count * 12) + (filteredStrategies.Count * 8) + 128;
            if (TxtTotalParameters != null)
            {
                TxtTotalParameters.Text = totalParameters.ToString("N0");
            }
            
            TxtGlobalAccuracy.Text = $"{globalAccuracy}%";

            double screenMin = Math.Min(screenWidth, screenHeight);
            double totalAvailableRadius = screenMin * 0.30;
            double radius1 = Math.Max(180, totalAvailableRadius * 0.64);
            double radius2 = Math.Max(110, totalAvailableRadius * 0.36);

            // Anéis Orbitais Concêntricos (Background Sci-Fi Matrix)
            DrawOrbitalRing(centerX, centerY, radius1, "#25513BA6", 1.5, true);
            DrawOrbitalRing(centerX, centerY, radius1 + radius2, "#14513BA6", 1.0, false);

            if (targets.Count > 0)
            {
                double angleStep = 360.0 / targets.Count;

                for (int i = 0; i < targets.Count; i++)
                {
                    var targetGroup = targets[i];
                    double targetAngle = (angleStep * i) * (Math.PI / 180.0);
                    
                    double tx = centerX + radius1 * Math.Cos(targetAngle);
                    double ty = centerY + radius1 * Math.Sin(targetAngle);

                    // Conexão do Nó Central (borda 48px) para o Nó Alvo (borda 14px)
                    double coreOffset = 48.0;
                    double targetOffset = 14.0;
                    double startX = centerX + coreOffset * Math.Cos(targetAngle);
                    double startY = centerY + coreOffset * Math.Sin(targetAngle);
                    double endX = tx - targetOffset * Math.Cos(targetAngle);
                    double endY = ty - targetOffset * Math.Sin(targetAngle);

                    DrawConnection(startX, startY, endX, endY, 100, 2.5, true);
                    DrawTargetNode(tx, ty, targetAngle, targetGroup.Key, targetGroup.ToList());

                    var targetStrategies = targetGroup.ToList();
                    double arcSpread = Math.Min(100.0 * (Math.PI / 180.0), (360.0 / targets.Count) * 0.85 * (Math.PI / 180.0));
                    double startAngle = targetAngle - (arcSpread / 2);
                    double stratStep = targetStrategies.Count > 1 ? arcSpread / (targetStrategies.Count - 1) : 0;

                    for (int j = 0; j < targetStrategies.Count; j++)
                    {
                        var strat = targetStrategies[j];
                        double stratAngle = targetStrategies.Count > 1 ? startAngle + (stratStep * j) : targetAngle;
                        
                        double sx = tx + radius2 * Math.Cos(stratAngle);
                        double sy = ty + radius2 * Math.Sin(stratAngle);

                        // Conexão do Nó Alvo (borda 14px) para a Estratégia (borda 7px)
                        double stratOffset = 7.0;
                        double startSx = tx + targetOffset * Math.Cos(stratAngle);
                        double startSy = ty + targetOffset * Math.Sin(stratAngle);
                        double endSx = sx - stratOffset * Math.Cos(stratAngle);
                        double endSy = sy - stratOffset * Math.Sin(stratAngle);

                        DrawConnection(startSx, startSy, endSx, endSy, strat.Score, 1.5, false);
                        DrawStrategyNode(sx, sy, stratAngle, strat);
                    }
                }
            }

            // Nó Central desenhado por último para sobreposição limpa no topo
            DrawCentralNode(centerX, centerY);

            EnsurePulseAnimationTimer();
            ApplySearchFilter();
        }

        private const double TARGET_FRAME_TIME = 1.0 / 30.0; // Taxa de quadros travada em 30 FPS estritos

        private void EnsurePulseAnimationTimer()
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
            if (_synapsePulses == null || _synapsePulses.Count == 0 || _animationStopwatch == null) return;

            double currentSec = _animationStopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentSec - _lastFrameSeconds;

            // Trava estrita de 30 FPS (ignora renders intermediários para economizar CPU/GPU)
            if (deltaTime < TARGET_FRAME_TIME) return;

            _lastFrameSeconds = currentSec;
            if (deltaTime > 0.1) deltaTime = TARGET_FRAME_TIME;

            for (int i = 0; i < _synapsePulses.Count; i++)
            {
                var pulse = _synapsePulses[i];
                pulse.Progress += pulse.Speed * deltaTime;
                if (pulse.Progress >= 1.0)
                {
                    pulse.Progress = 0.0;
                    pulse.Reversed = !pulse.Reversed;
                }

                double p = pulse.Progress;
                double curX = pulse.Reversed ? pulse.EndX + (pulse.StartX - pulse.EndX) * p : pulse.StartX + (pulse.EndX - pulse.StartX) * p;
                double curY = pulse.Reversed ? pulse.EndY + (pulse.StartY - pulse.EndY) * p : pulse.StartY + (pulse.EndY - pulse.StartY) * p;

                // Suavização ultra-fluida de opacidade (fade-in / fade-out nas extremidades dos nós)
                double alpha = 1.0;
                if (p < 0.15) alpha = p / 0.15;
                else if (p > 0.85) alpha = (1.0 - p) / 0.15;

                pulse.Particle.Opacity = Math.Clamp(alpha, 0.0, 1.0);

                // Micro-pulsação orgânica senoidal do tamanho do elétron sináptico
                double pulseScale = 0.85 + (0.35 * Math.Sin(p * Math.PI));
                double currentSize = pulse.BaseSize * pulseScale;
                pulse.Particle.Width = currentSize;
                pulse.Particle.Height = currentSize;

                Canvas.SetLeft(pulse.Particle, curX - (currentSize / 2.0));
                Canvas.SetTop(pulse.Particle, curY - (currentSize / 2.0));
            }
        }

        private void DrawOrbitalRing(double cx, double cy, double radius, string colorHex, double thickness, bool isDashed)
        {
            Ellipse ring = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                StrokeThickness = thickness,
                IsHitTestVisible = false
            };
            if (isDashed)
            {
                ring.StrokeDashArray = new DoubleCollection { 6, 6 };
            }
            Canvas.SetLeft(ring, cx - radius);
            Canvas.SetTop(ring, cy - radius);
            BrainCanvas.Children.Add(ring);
        }

        private void DrawConnection(double x1, double y1, double x2, double y2, int score, double thickness, bool isCore)
        {
            Color c;
            if (score > 50) c = (Color)ColorConverter.ConvertFromString("#4EDEA3");
            else if (score > 0) c = (Color)ColorConverter.ConvertFromString("#F5C542");
            else if (score == 0) c = (Color)ColorConverter.ConvertFromString("#7A7A8A");
            else c = (Color)ColorConverter.ConvertFromString("#FF6B6B");

            Line line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = new SolidColorBrush(c),
                StrokeThickness = thickness,
                Opacity = score > 50 ? 0.85 : 0.4
            };

            if (score > 50 || isCore)
            {
                line.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = c,
                    BlurRadius = 12,
                    ShadowDepth = 0
                };
            }

            BrainCanvas.Children.Add(line);

            // Adicionar partículas de pulso neural com fluidez extrema
            Color particleColor = isCore ? (Color)ColorConverter.ConvertFromString("#00E5FF") : c;
            double pSize = isCore ? 7.5 : 5.0;
            int count = isCore ? 2 : 1;

            for (int k = 0; k < count; k++)
            {
                Ellipse pulseDot = new Ellipse
                {
                    Width = pSize,
                    Height = pSize,
                    Fill = new SolidColorBrush(particleColor),
                    IsHitTestVisible = false
                };

                BrainCanvas.Children.Add(pulseDot);

                double initialProgress = (k == 0) ? _random.NextDouble() : (_random.NextDouble() * 0.5 + 0.5);

                _synapsePulses.Add(new SynapsePulse
                {
                    Particle = pulseDot,
                    StartX = x1,
                    StartY = y1,
                    EndX = x2,
                    EndY = y2,
                    Progress = initialProgress % 1.0,
                    Speed = isCore ? 0.35 + (_random.NextDouble() * 0.15) : 0.25 + (_random.NextDouble() * 0.15),
                    BaseSize = pSize,
                    Reversed = _random.Next(0, 2) == 1
                });
            }
        }

        private void DrawCentralNode(double x, double y)
        {
            Ellipse circle = new Ellipse
            {
                Width = 96,
                Height = 96,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#513BA6")),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2.5,
                Cursor = Cursors.Hand,
                ToolTip = "InnoAI — Núcleo Neural de Decisão"
            };

            circle.MouseEnter += (s, e) => { circle.Width = 104; circle.Height = 104; Canvas.SetLeft(circle, x - 52); Canvas.SetTop(circle, y - 52); };
            circle.MouseLeave += (s, e) => { circle.Width = 96; circle.Height = 96; Canvas.SetLeft(circle, x - 48); Canvas.SetTop(circle, y - 48); };

            Canvas.SetLeft(circle, x - 48);
            Canvas.SetTop(circle, y - 48);
            BrainCanvas.Children.Add(circle);

            TextBlock txt = new TextBlock
            {
                Text = "InnoAI",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Black,
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(txt, x - 27);
            Canvas.SetTop(txt, y - 11);
            BrainCanvas.Children.Add(txt);
        }

        private void DrawTargetNode(double x, double y, double angle, string targetId, List<LearnStrategyExport> strategies)
        {
            string friendlyName = FormatarTargetName(targetId);
            Color nodeColor;
            if (targetId.StartsWith("v7_")) nodeColor = (Color)ColorConverter.ConvertFromString("#00F5D4");
            else if (targetId.StartsWith("f7_") || targetId.StartsWith("m7_")) nodeColor = (Color)ColorConverter.ConvertFromString("#FFB703");
            else if (targetId.StartsWith("t20_") || targetId.StartsWith("nokia_")) nodeColor = (Color)ColorConverter.ConvertFromString("#00E5FF");
            else nodeColor = (Color)ColorConverter.ConvertFromString("#8570E6");

            Ellipse circle = new Ellipse
            {
                Width = 28,
                Height = 28,
                Fill = new SolidColorBrush(nodeColor),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Tag = targetId,
                ToolTip = $"{friendlyName} ({strategies.Count} estratégias)"
            };

            circle.MouseEnter += (s, e) => { circle.Width = 34; circle.Height = 34; Canvas.SetLeft(circle, x - 17); Canvas.SetTop(circle, y - 17); };
            circle.MouseLeave += (s, e) => { circle.Width = 28; circle.Height = 28; Canvas.SetLeft(circle, x - 14); Canvas.SetTop(circle, y - 14); };

            circle.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowTargetInspector(targetId, strategies);
            };

            Canvas.SetLeft(circle, x - 14);
            Canvas.SetTop(circle, y - 14);
            BrainCanvas.Children.Add(circle);

            Border badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6111122")),
                BorderBrush = new SolidColorBrush(nodeColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                IsHitTestVisible = false,
                Tag = targetId
            };


            TextBlock txt = new TextBlock
            {
                Text = friendlyName,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            badge.Child = txt;

            bool isRightSide = Math.Cos(angle) >= 0;
            if (isRightSide)
            {
                Canvas.SetLeft(badge, x + 18);
            }
            else
            {
                Canvas.SetLeft(badge, x - 190);
            }
            Canvas.SetTop(badge, y - 14);
            BrainCanvas.Children.Add(badge);
        }

        private void DrawStrategyNode(double x, double y, double angle, LearnStrategyExport strat)
        {
            Color c;
            if (strat.Score > 50) c = (Color)ColorConverter.ConvertFromString("#4EDEA3");
            else if (strat.Score > 0) c = (Color)ColorConverter.ConvertFromString("#F5C542");
            else if (strat.Score == 0) c = (Color)ColorConverter.ConvertFromString("#7A7A8A");
            else c = (Color)ColorConverter.ConvertFromString("#FF6B6B");

            string shortQuery = strat.Query.Length > 20 ? strat.Query.Substring(0, 20) + "..." : strat.Query;
            Ellipse circle = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(c),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                Tag = $"{strat.TargetId} {strat.Query}",
                ToolTip = $"{shortQuery} | Score: {strat.Score}"
            };

            circle.MouseEnter += (s, e) => { circle.Width = 20; circle.Height = 20; Canvas.SetLeft(circle, x - 10); Canvas.SetTop(circle, y - 10); };
            circle.MouseLeave += (s, e) => { circle.Width = 14; circle.Height = 14; Canvas.SetLeft(circle, x - 7); Canvas.SetTop(circle, y - 7); };

            circle.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowStrategyInspector(strat);
            };

            Canvas.SetLeft(circle, x - 7);
            Canvas.SetTop(circle, y - 7);
            BrainCanvas.Children.Add(circle);

            Border badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E60D0D18")),
                BorderBrush = new SolidColorBrush(c),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                Cursor = Cursors.Hand,
                Tag = $"{strat.TargetId} {strat.Query}"
            };

            badge.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowStrategyInspector(strat);
            };

            TextBlock txt = new TextBlock
            {
                Text = $"{shortQuery} [{strat.Score}]",
                Foreground = new SolidColorBrush(c),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            badge.Child = txt;

            bool isRightSide = Math.Cos(angle) >= 0;
            if (isRightSide)
            {
                Canvas.SetLeft(badge, x + 12);
            }
            else
            {
                Canvas.SetLeft(badge, x - 160);
            }
            Canvas.SetTop(badge, y - 11);
            BrainCanvas.Children.Add(badge);
        }

        // ============================================================
        // PAINEL INSPECTOR (DETALHES HOLOGRÁFICOS)
        // ============================================================
        private void ShowStrategyInspector(LearnStrategyExport strat)
        {
            InspectorTitle.Text = FormatarTargetName(strat.TargetId);
            InspectorSubtitle.Text = $"Estratégia IA: {strat.TargetId}";
            InspectorQueryText.Text = strat.Query;
            InspectorScoreText.Text = strat.Score.ToString();

            int total = strat.Hits + strat.Misses;
            int acc = total > 0 ? (int)Math.Round((double)strat.Hits / total * 100.0) : 100;
            InspectorAccuracyText.Text = $"{acc}%";
            InspectorHitsText.Text = strat.Hits.ToString();
            InspectorMissesText.Text = strat.Misses.ToString();

            RenderHistoryDots(strat.History);
            NodeInspectorPanel.Visibility = Visibility.Visible;
        }

        private void ShowTargetInspector(string targetId, List<LearnStrategyExport> strats)
        {
            InspectorTitle.Text = FormatarTargetName(targetId);
            InspectorSubtitle.Text = $"Grupo de Seletores: {targetId} ({strats.Count} estratégias)";
            InspectorQueryText.Text = $"Alvo Mapeado: {targetId}\nTotal de Seletores Aprendidos: {strats.Count}";

            int avgScore = strats.Count > 0 ? (int)strats.Average(s => s.Score) : 0;
            int totalHits = strats.Sum(s => s.Hits);
            int totalMisses = strats.Sum(s => s.Misses);
            int total = totalHits + totalMisses;
            int acc = total > 0 ? (int)Math.Round((double)totalHits / total * 100.0) : 100;

            InspectorScoreText.Text = avgScore.ToString();
            InspectorAccuracyText.Text = $"{acc}%";
            InspectorHitsText.Text = totalHits.ToString();
            InspectorMissesText.Text = totalMisses.ToString();

            string combinedHistory = strats.Count > 0 ? strats.First().History : "";
            RenderHistoryDots(combinedHistory);
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
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EDEA3"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B"))
                };
                InspectorHistoryDots.Items.Add(dot);
            }
        }

        private static string FormatarTargetName(string targetId)
        {
            return targetId switch
            {
                "v7_spinner_municipio"     => "📍 Gaveta Município (V7)",
                "v7_campo_email"           => "📧 E-mail Aplicador (V7)",
                "v7_campo_senha"           => "🔒 Senha Aplicador (V7)",
                "v7_botao_entrar"          => "▶ Botão Entrar (V7)",
                "v7_botao_importar_zip"    => "📦 Menu Importar (V7)",
                "v7_btn_procurar_arquivo"  => "📁 Procurar ZIP (V7)",
                "v7_btn_sincronizar_agora" => "🔄 Sincronizar (V7)",
                "f7_spinner_municipio"     => "📍 Gaveta Município (M7)",
                "f7_campo_email"           => "📧 E-mail Aplicador (M7)",
                "f7_campo_senha"           => "🔒 Senha Aplicador (M7)",
                "f7_botao_entrar"          => "▶ Botão Entrar (M7)",
                "f7_btn_procurar_arquivo"  => "📁 Procurar ZIP (M7)",
                "t20_spinner_municipio"    => "📍 Gaveta Município (T20)",
                "t20_campo_email"          => "📧 E-mail Aplicador (T20)",
                "t20_campo_senha"          => "🔒 Senha Aplicador (T20)",
                "t20_botao_entrar"         => "▶ Botão Entrar (T20)",
                "t20_botao_importar_zip"   => "📦 Menu Importar (T20)",
                "t20_btn_procurar_arquivo" => "📁 Procurar ZIP (T20)",
                "spinner_municipio"        => "📍 Gaveta Município (F1)",

                "campo_email"              => "📧 E-mail Aplicador (F1)",
                "campo_senha"              => "🔒 Senha Aplicador (F1)",
                "botao_entrar"             => "▶ Botão Entrar (F1)",
                "botao_importar_zip"       => "📦 Menu Importar (F1)",
                "f1_btn_procurar_arquivo"  => "📁 Procurar ZIP (F1)",
                _                          => targetId
            };
        }
    }
}
