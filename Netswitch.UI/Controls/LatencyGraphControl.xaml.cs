using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Netswitch.UI.Controls;

public partial class LatencyGraphControl : UserControl
{
    private readonly double[] _latencyBuffer = new double[MaxDataPoints];
    private int _bufferIndex = 0;
    private int _bufferCount = 0;
    private const int MaxDataPoints = 60;

    // Reusable WPF objects
    private readonly Path _graphPath;
    private readonly PathGeometry _pathGeometry;
    private readonly PathFigure _pathFigure;
    private readonly LinearGradientBrush _fillBrush;
    private readonly SolidColorBrush _strokeBrush;
    private readonly DropShadowEffect _glowEffect;

    public static readonly DependencyProperty CurrentLatencyProperty =
        DependencyProperty.Register(nameof(CurrentLatency), typeof(double), typeof(LatencyGraphControl),
            new PropertyMetadata(0.0, OnLatencyChanged));

    public double CurrentLatency
    {
        get => (double)GetValue(CurrentLatencyProperty);
        set => SetValue(CurrentLatencyProperty, value);
    }

    public LatencyGraphControl()
    {
        InitializeComponent();

        // Pre-create reusable objects
        _fillBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Transparent, 1)
            }
        };

        _strokeBrush = new SolidColorBrush(Colors.White);
        _glowEffect = new DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.6
        };

        _pathFigure = new PathFigure { IsClosed = true };
        _pathGeometry = new PathGeometry();
        _pathGeometry.Figures.Add(_pathFigure);

        _graphPath = new Path
        {
            Data = _pathGeometry,
            Fill = _fillBrush,
            Stroke = _strokeBrush,
            StrokeThickness = 2,
            Effect = _glowEffect
        };

        GraphCanvas.Children.Add(_graphPath);
        SizeChanged += OnSizeChanged;
    }

    private static void OnLatencyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LatencyGraphControl control && e.NewValue is double latency)
        {
            control.AddLatencyPoint(latency);
        }
    }

    private void AddLatencyPoint(double latency)
    {
        _latencyBuffer[_bufferIndex] = latency;
        _bufferIndex = (_bufferIndex + 1) % MaxDataPoints;
        if (_bufferCount < MaxDataPoints)
            _bufferCount++;

        RedrawGraph();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawGraph();
    }

    private void RedrawGraph()
    {
        if (_bufferCount < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _graphPath.Visibility = Visibility.Collapsed;
            return;
        }

        _graphPath.Visibility = Visibility.Visible;

        // Read from ring buffer in order
        var startIndex = _bufferCount < MaxDataPoints ? 0 : _bufferIndex;
        var width = ActualWidth;
        var height = ActualHeight - 10;
        var xStep = width / (MaxDataPoints - 1);

        // Find max for scaling
        double maxLatency = 100;
        for (int i = 0; i < _bufferCount; i++)
        {
            var idx = (startIndex + i) % MaxDataPoints;
            if (_latencyBuffer[idx] > maxLatency)
                maxLatency = _latencyBuffer[idx];
        }

        // Rebuild path segments
        _pathFigure.StartPoint = new Point(0, height);
        _pathFigure.Segments.Clear();

        double lastLatency = 0;
        for (int i = 0; i < _bufferCount; i++)
        {
            var idx = (startIndex + i) % MaxDataPoints;
            var latency = _latencyBuffer[idx];
            var x = i * xStep;
            var y = height - (latency / maxLatency * height);

            if (i == 0)
            {
                _pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
            else
            {
                var prevX = (i - 1) * xStep;
                var prevIdx = (startIndex + i - 1) % MaxDataPoints;
                var prevY = height - (_latencyBuffer[prevIdx] / maxLatency * height);
                var controlPoint = new Point((prevX + x) / 2, (prevY + y) / 2);
                _pathFigure.Segments.Add(new QuadraticBezierSegment(controlPoint, new Point(x, y), true));
            }

            lastLatency = latency;
        }

        // Close the path for fill
        _pathFigure.Segments.Add(new LineSegment(new Point((_bufferCount - 1) * xStep, height), true));
        _pathFigure.Segments.Add(new LineSegment(new Point(0, height), true));

        // Update colors based on latest latency
        var fillColor = GetColorForLatency(lastLatency);
        var strokeColor = GetStrokeColorForLatency(lastLatency);

        _fillBrush.GradientStops[0] = new GradientStop(Color.FromArgb(80, fillColor.R, fillColor.G, fillColor.B), 0);
        _fillBrush.GradientStops[1] = new GradientStop(Color.FromArgb(20, fillColor.R, fillColor.G, fillColor.B), 1);
        _strokeBrush.Color = strokeColor;
        _glowEffect.Color = strokeColor;
    }

    private Color GetColorForLatency(double latency)
    {
        if (latency <= 20)
            return Color.FromRgb(16, 185, 129); // Green
        else if (latency <= 50)
            return Color.FromRgb(245, 158, 11); // Yellow
        else
            return Color.FromRgb(239, 68, 68); // Red
    }

    private Color GetStrokeColorForLatency(double latency)
    {
        if (latency <= 20)
            return Color.FromRgb(34, 197, 94); // Bright green
        else if (latency <= 50)
            return Color.FromRgb(251, 146, 60); // Bright yellow
        else
            return Color.FromRgb(248, 113, 113); // Bright red
    }
}
