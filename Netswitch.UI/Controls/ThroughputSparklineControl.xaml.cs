using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Netswitch.UI.Controls;

public partial class ThroughputSparklineControl : UserControl
{
    private readonly double[] _downloadBuffer = new double[MaxDataPoints];
    private readonly double[] _uploadBuffer = new double[MaxDataPoints];
    private int _bufferIndex = 0;
    private int _bufferCount = 0;
    private const int MaxDataPoints = 60;

    // Reusable objects for download line
    private readonly Path _downloadPath;
    private readonly PathGeometry _downloadGeometry;
    private readonly PathFigure _downloadFigure;

    // Reusable objects for upload line
    private readonly Path _uploadPath;
    private readonly PathGeometry _uploadGeometry;
    private readonly PathFigure _uploadFigure;

    public static readonly DependencyProperty DownloadRateProperty =
        DependencyProperty.Register(nameof(DownloadRate), typeof(double), typeof(ThroughputSparklineControl),
            new PropertyMetadata(0.0, OnRateChanged));

    public static readonly DependencyProperty UploadRateProperty =
        DependencyProperty.Register(nameof(UploadRate), typeof(double), typeof(ThroughputSparklineControl),
            new PropertyMetadata(0.0, OnRateChanged));

    public double DownloadRate
    {
        get => (double)GetValue(DownloadRateProperty);
        set => SetValue(DownloadRateProperty, value);
    }

    public double UploadRate
    {
        get => (double)GetValue(UploadRateProperty);
        set => SetValue(UploadRateProperty, value);
    }

    public ThroughputSparklineControl()
    {
        InitializeComponent();

        // Download line (cyan)
        _downloadFigure = new PathFigure { IsClosed = false };
        _downloadGeometry = new PathGeometry();
        _downloadGeometry.Figures.Add(_downloadFigure);
        _downloadPath = new Path
        {
            Data = _downloadGeometry,
            Stroke = new SolidColorBrush(Color.FromRgb(6, 182, 212)), // Cyan
            StrokeThickness = 2,
            Effect = new DropShadowEffect { Color = Color.FromRgb(6, 182, 212), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.5 }
        };

        // Upload line (magenta)
        _uploadFigure = new PathFigure { IsClosed = false };
        _uploadGeometry = new PathGeometry();
        _uploadGeometry.Figures.Add(_uploadFigure);
        _uploadPath = new Path
        {
            Data = _uploadGeometry,
            Stroke = new SolidColorBrush(Color.FromRgb(192, 38, 211)), // Magenta
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Effect = new DropShadowEffect { Color = Color.FromRgb(192, 38, 211), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.4 }
        };

        SparkCanvas.Children.Add(_downloadPath);
        SparkCanvas.Children.Add(_uploadPath);
        SizeChanged += (_, _) => Redraw();
    }

    private static void OnRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThroughputSparklineControl control)
        {
            control.UpdateData();
        }
    }

    private void UpdateData()
    {
        _downloadBuffer[_bufferIndex] = DownloadRate;
        _uploadBuffer[_bufferIndex] = UploadRate;
        _bufferIndex = (_bufferIndex + 1) % MaxDataPoints;
        if (_bufferCount < MaxDataPoints)
            _bufferCount++;

        Redraw();
    }

    private void Redraw()
    {
        if (_bufferCount < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _downloadPath.Visibility = Visibility.Collapsed;
            _uploadPath.Visibility = Visibility.Collapsed;
            return;
        }

        _downloadPath.Visibility = Visibility.Visible;
        _uploadPath.Visibility = Visibility.Visible;

        var startIndex = _bufferCount < MaxDataPoints ? 0 : _bufferIndex;
        var width = ActualWidth;
        var height = ActualHeight - 4;
        var xStep = width / (MaxDataPoints - 1);

        // Find max for scaling
        double maxRate = 1024; // Minimum 1 KB/s scale
        for (int i = 0; i < _bufferCount; i++)
        {
            var idx = (startIndex + i) % MaxDataPoints;
            maxRate = Math.Max(maxRate, Math.Max(_downloadBuffer[idx], _uploadBuffer[idx]));
        }

        DrawLine(_downloadFigure, _downloadBuffer, startIndex, width, height, xStep, maxRate);
        DrawLine(_uploadFigure, _uploadBuffer, startIndex, width, height, xStep, maxRate);
    }

    private void DrawLine(PathFigure figure, double[] buffer, int startIndex, double width, double height, double xStep, double maxRate)
    {
        figure.Segments.Clear();

        var firstIdx = startIndex % MaxDataPoints;
        var firstY = height - (buffer[firstIdx] / maxRate * height);
        figure.StartPoint = new Point(0, firstY);

        for (int i = 1; i < _bufferCount; i++)
        {
            var idx = (startIndex + i) % MaxDataPoints;
            var x = i * xStep;
            var y = height - (buffer[idx] / maxRate * height);

            var prevIdx = (startIndex + i - 1) % MaxDataPoints;
            var prevX = (i - 1) * xStep;
            var prevY = height - (buffer[prevIdx] / maxRate * height);
            var cp = new Point((prevX + x) / 2, (prevY + y) / 2);

            figure.Segments.Add(new QuadraticBezierSegment(cp, new Point(x, y), true));
        }
    }
}
