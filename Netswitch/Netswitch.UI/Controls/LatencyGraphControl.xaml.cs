using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Netswitch.UI.Controls;

public partial class LatencyGraphControl : UserControl
{
    private readonly Queue<double> _latencyHistory = new();
    private const int MaxDataPoints = 60;
    
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
        _latencyHistory.Enqueue(latency);
        
        if (_latencyHistory.Count > MaxDataPoints)
        {
            _latencyHistory.Dequeue();
        }
        
        RedrawGraph();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawGraph();
    }

    private void RedrawGraph()
    {
        GraphCanvas.Children.Clear();
        
        if (_latencyHistory.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var points = _latencyHistory.ToArray();
        var maxLatency = Math.Max(100, points.Max()); // At least 100ms scale
        var width = ActualWidth;
        var height = ActualHeight - 10; // Leave space for top/bottom padding
        var xStep = width / (MaxDataPoints - 1);

        // Create gradient path
        var pathFigure = new PathFigure { StartPoint = new Point(0, height) };
        
        // Build the wave path
        for (int i = 0; i < points.Length; i++)
        {
            var x = i * xStep;
            var y = height - (points[i] / maxLatency * height);
            
            if (i == 0)
            {
                pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
            else
            {
                // Smooth curve using quadratic bezier
                var prevX = (i - 1) * xStep;
                var prevY = height - (points[i - 1] / maxLatency * height);
                var controlPoint = new Point((prevX + x) / 2, (prevY + y) / 2);
                pathFigure.Segments.Add(new QuadraticBezierSegment(controlPoint, new Point(x, y), true));
            }
        }

        // Close the path to create filled area
        pathFigure.Segments.Add(new LineSegment(new Point((points.Length - 1) * xStep, height), true));
        pathFigure.Segments.Add(new LineSegment(new Point(0, height), true));

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        // Determine color based on latest latency
        var latestLatency = points[^1];
        var fillColor = GetColorForLatency(latestLatency);
        var strokeColor = GetStrokeColorForLatency(latestLatency);

        // Create filled area
        var path = new Path
        {
            Data = pathGeometry,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(80, fillColor.R, fillColor.G, fillColor.B), 0),
                    new GradientStop(Color.FromArgb(20, fillColor.R, fillColor.G, fillColor.B), 1)
                }
            },
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = 2
        };

        GraphCanvas.Children.Add(path);

        // Add glow effect
        path.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = strokeColor,
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.6
        };
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
