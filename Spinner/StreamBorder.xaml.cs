using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Spinner
{
    public partial class StreamBorderControl : UserControl
    {
        // ========== Dependency Properties ==========

        // Yılan uzunluğu: toplam yolun yüzdesi
        public static readonly DependencyProperty SnakeFractionProperty =
            DependencyProperty.Register(
                "SnakeFraction",
                typeof(double),
                typeof(StreamBorderControl),
                new PropertyMetadata(0.32, OnVisualPropertyChanged));

        public double SnakeFraction
        {
            get { return (double)GetValue(SnakeFractionProperty); }
            set { SetValue(SnakeFractionProperty, value); }
        }

        // Bir tam tur süresi (sn)
        public static readonly DependencyProperty PeriodSecondsProperty =
            DependencyProperty.Register(
                "PeriodSeconds",
                typeof(double),
                typeof(StreamBorderControl),
                new PropertyMetadata(7.0));

        public double PeriodSeconds
        {
            get { return (double)GetValue(PeriodSecondsProperty); }
            set { SetValue(PeriodSecondsProperty, value); }
        }

        // Uçlardaki kalınlık
        public static readonly DependencyProperty MinThicknessProperty =
            DependencyProperty.Register(
                "MinThickness",
                typeof(double),
                typeof(StreamBorderControl),
                new PropertyMetadata(4.0, OnVisualPropertyChanged));

        public double MinThickness
        {
            get { return (double)GetValue(MinThicknessProperty); }
            set { SetValue(MinThicknessProperty, value); }
        }

        // Ortadaki kalınlık
        public static readonly DependencyProperty MaxThicknessProperty =
            DependencyProperty.Register(
                "MaxThickness",
                typeof(double),
                typeof(StreamBorderControl),
                new PropertyMetadata(6.0, OnVisualPropertyChanged));

        public double MaxThickness
        {
            get { return (double)GetValue(MaxThicknessProperty); }
            set { SetValue(MaxThicknessProperty, value); }
        }

        // Köşe yarıçapı
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                "CornerRadius",
                typeof(double),
                typeof(StreamBorderControl),
                new PropertyMetadata(22.0, OnGeometryPropertyChanged));

        public double CornerRadius
        {
            get { return (double)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        // Arka border rengi
        public static readonly DependencyProperty BorderColorProperty =
            DependencyProperty.Register(
                "BorderColor",
                typeof(Color),
                typeof(StreamBorderControl),
                new PropertyMetadata(Color.FromRgb(0x17, 0xB2, 0xE6), OnBorderPropertyChanged));

        public Color BorderColor
        {
            get { return (Color)GetValue(BorderColorProperty); }
            set { SetValue(BorderColorProperty, value); }
        }

        // Arka border kalınlığı
        public static readonly DependencyProperty BorderStrokeThicknessProperty =
            DependencyProperty.Register(
                "BorderStrokeThickness",
                typeof(double),
                typeof(StreamBorderControl),
                new PropertyMetadata(0.8, OnBorderPropertyChanged));

        public double BorderStrokeThickness
        {
            get { return (double)GetValue(BorderStrokeThicknessProperty); }
            set { SetValue(BorderStrokeThicknessProperty, value); }
        }

        // Gradient dış / kenar rengi (Outer)
        public static readonly DependencyProperty OuterColorProperty =
            DependencyProperty.Register(
                "OuterColor",
                typeof(Color),
                typeof(StreamBorderControl),
                new PropertyMetadata(Color.FromRgb(0x00, 0xD8, 0xFF), OnGradientPropertyChanged));

        public Color OuterColor
        {
            get { return (Color)GetValue(OuterColorProperty); }
            set { SetValue(OuterColorProperty, value); }
        }

        // Gradient glow rengi (Inner)
        public static readonly DependencyProperty InnerColorProperty =
            DependencyProperty.Register(
                "InnerColor",
                typeof(Color),
                typeof(StreamBorderControl),
                new PropertyMetadata(Color.FromRgb(0x40, 0xF0, 0xFF), OnGradientPropertyChanged));

        public Color InnerColor
        {
            get { return (Color)GetValue(InnerColorProperty); }
            set { SetValue(InnerColorProperty, value); }
        }

        // Gradient merkez (fosforlu) rengi
        public static readonly DependencyProperty CenterColorProperty =
            DependencyProperty.Register(
                "CenterColor",
                typeof(Color),
                typeof(StreamBorderControl),
                new PropertyMetadata(Color.FromRgb(0x80, 0xFA, 0xFF), OnGradientPropertyChanged));

        public Color CenterColor
        {
            get { return (Color)GetValue(CenterColorProperty); }
            set { SetValue(CenterColorProperty, value); }
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StreamBorderControl control = (StreamBorderControl)d;
            control._needsUpdate = true;
        }

        private static void OnGeometryPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StreamBorderControl control = (StreamBorderControl)d;
            control.SetupGeometry();
        }

        private static void OnBorderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StreamBorderControl control = (StreamBorderControl)d;
            control.UpdateBorderVisual();
        }

        private static void OnGradientPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StreamBorderControl control = (StreamBorderControl)d;
            control.UpdateGradientColors();
        }

        // ========== Internal state ==========

        private List<Point> _points = new List<Point>();
        private List<double> _segLengths = new List<double>();
        private List<double> _cumLengths = new List<double>();
        private double _totalLength;

        private double _headPos;
        private TimeSpan _lastRenderTime;
        private bool _needsUpdate = true;

        public StreamBorderControl()
        {
            InitializeComponent();

            Loaded += SnakeSpinnerControl_Loaded;
            Unloaded += SnakeSpinnerControl_Unloaded;
            RootCanvas.SizeChanged += RootCanvas_SizeChanged;
        }

        private void RootCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetupGeometry();
        }

        private void SnakeSpinnerControl_Loaded(object sender, RoutedEventArgs e)
        {
            SetupGeometry();
            UpdateBorderVisual();
            UpdateGradientColors();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void SnakeSpinnerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }

        private void SetupGeometry()
        {
            double w = RootCanvas.ActualWidth;
            double h = RootCanvas.ActualHeight;
            if (w <= 0 || h <= 0)
                return;

            double rectWidth = w;
            double rectHeight = h;
            double radius = CornerRadius;

            Rect rect = new Rect(0, 0, rectWidth, rectHeight);
            RectangleGeometry rectGeom = new RectangleGeometry(rect, radius, radius);

            BaseBorderPath.Data = rectGeom;

            BuildFlattenedPath(rectGeom);
        }

        private void BuildFlattenedPath(Geometry geometry)
        {
            _points.Clear();
            _segLengths.Clear();
            _cumLengths.Clear();
            _totalLength = 0;
            _headPos = 0;
            _lastRenderTime = TimeSpan.Zero;
            _needsUpdate = true;

            PathGeometry flat = geometry.GetFlattenedPathGeometry(0.2, ToleranceType.Absolute);

            List<Point> rawPts = new List<Point>();

            foreach (PathFigure fig in flat.Figures)
            {
                if (fig.Segments.Count == 0)
                    continue;

                rawPts.Add(fig.StartPoint);

                foreach (PolyLineSegment seg in fig.Segments.OfType<PolyLineSegment>())
                {
                    rawPts.AddRange(seg.Points);
                }
            }

            if (rawPts.Count < 2)
                return;

            double step = 1.0; // daha yumuşak hareket için
            List<Point> sampled = Resample(rawPts, step);

            _points = sampled;

            for (int i = 0; i < _points.Count - 1; i++)
            {
                double len = Distance(_points[i], _points[i + 1]);
                if (len <= 0) continue;

                _segLengths.Add(len);
                _totalLength += len;
                _cumLengths.Add(_totalLength);
            }

            // Loop kapat
            if (Distance(_points[_points.Count - 1], _points[0]) > 0.01)
            {
                double closeLen = Distance(_points[_points.Count - 1], _points[0]);
                _points.Add(_points[0]);
                _segLengths.Add(closeLen);
                _totalLength += closeLen;
                _cumLengths.Add(_totalLength);
            }
        }

        private List<Point> Resample(List<Point> pts, double step)
        {
            List<Point> result = new List<Point>();
            result.Add(pts[0]);

            double acc = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Point a = pts[i];
                Point b = pts[i + 1];
                double segLen = Distance(a, b);
                if (segLen <= 0) continue;

                Vector dir = b - a;
                dir.Normalize();

                double t = 0;
                while (t + step - acc <= segLen)
                {
                    double dist = step - acc;
                    t += dist;
                    Point p = a + dir * t;
                    result.Add(p);
                    acc = 0;
                }

                acc += segLen - t;
            }

            return result;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_totalLength <= 0)
                return;

            RenderingEventArgs args = (RenderingEventArgs)e;
            if (_lastRenderTime == TimeSpan.Zero)
            {
                _lastRenderTime = args.RenderingTime;
                return;
            }

            double dt = (args.RenderingTime - _lastRenderTime).TotalSeconds;
            _lastRenderTime = args.RenderingTime;

            double period = PeriodSeconds;
            if (period <= 0.01)
                period = 0.01;

            double speed = _totalLength / period;

            _headPos += speed * dt;
            while (_headPos >= _totalLength)
                _headPos -= _totalLength;

            double snakeLen = _totalLength * SnakeFraction;
            double tailPos = _headPos - snakeLen;
            if (tailPos < 0)
                tailPos += _totalLength;

            UpdateSnakePath(tailPos, _headPos);
            SnakePath.Opacity = 1.0;
        }

        private void UpdateSnakePath(double tail, double head)
        {
            List<Point> centers = new List<Point>();

            if (tail <= head)
            {
                centers.AddRange(GetPointsForRange(tail, head));
            }
            else
            {
                centers.AddRange(GetPointsForRange(tail, _totalLength));
                centers.AddRange(GetPointsForRange(0, head));
            }

            if (centers.Count < 2)
            {
                SnakePath.Data = null;
                return;
            }

            Geometry geom = BuildTaperedGeometry(centers);
            SnakePath.Data = geom;
        }

        private List<Point> GetPointsForRange(double startDist, double endDist)
        {
            List<Point> pts = new List<Point>();
            if (endDist <= startDist || _points.Count < 2)
                return pts;

            int startIndex;
            Point startPoint = PointOnPath(startDist, out startIndex);
            int endIndex;
            Point endPoint = PointOnPath(endDist, out endIndex);

            pts.Add(startPoint);

            int i = startIndex + 1;
            while (i <= endIndex && i < _points.Count)
            {
                pts.Add(_points[i]);
                i++;
            }

            if (pts.Count == 1)
                pts.Add(endPoint);
            else
                pts[pts.Count - 1] = endPoint;

            return pts;
        }

        private Geometry BuildTaperedGeometry(List<Point> centers)
        {
            int n = centers.Count;
            List<Point> leftPts = new List<Point>(n);
            List<Point> rightPts = new List<Point>(n);

            Vector lastDir = new Vector(1, 0);

            for (int i = 0; i < n; i++)
            {
                Point p = centers[i];

                Vector dir;
                if (i == 0)
                    dir = centers[1] - centers[0];
                else if (i == n - 1)
                    dir = centers[n - 1] - centers[n - 2];
                else
                {
                    Vector d1 = centers[i] - centers[i - 1];
                    Vector d2 = centers[i + 1] - centers[i];
                    dir = d1 + d2;
                }

                if (dir.LengthSquared < 1e-6)
                    dir = lastDir;
                else
                    lastDir = dir;

                dir.Normalize();

                Vector nrm = new Vector(-dir.Y, dir.X);

                double t = (double)i / (n - 1);
                double s = 1.0 - 2.0 * Math.Abs(t - 0.5);
                if (s < 0) s = 0;

                double thickness = MinThickness + (MaxThickness - MinThickness) * s;
                double half = thickness / 2.0;

                Point left = p + nrm * half;
                Point right = p - nrm * half;

                leftPts.Add(left);
                rightPts.Add(right);
            }

            PathFigure fig = new PathFigure();
            fig.StartPoint = leftPts[0];
            fig.IsClosed = true;
            fig.IsFilled = true;

            PolyLineSegment seg = new PolyLineSegment();
            for (int i = 1; i < leftPts.Count; i++)
                seg.Points.Add(leftPts[i]);
            for (int i = rightPts.Count - 1; i >= 0; i--)
                seg.Points.Add(rightPts[i]);

            fig.Segments.Add(seg);

            PathGeometry geom = new PathGeometry();
            geom.Figures.Add(fig);
            return geom;
        }

        private Point PointOnPath(double dist, out int segIndex)
        {
            for (int i = 0; i < _segLengths.Count; i++)
            {
                double segStart = (i == 0) ? 0 : _cumLengths[i - 1];
                double segEnd = _cumLengths[i];

                if (dist <= segEnd)
                {
                    double t = (dist - segStart) / _segLengths[i];
                    segIndex = i;
                    return Lerp(_points[i], _points[i + 1], t);
                }
            }

            segIndex = _segLengths.Count - 1;
            return _points[_points.Count - 1];
        }

        private static Point Lerp(Point a, Point b, double t)
        {
            return new Point(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t);
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ===== Border & Gradient helpers =====

        private void UpdateBorderVisual()
        {
            BaseBorderPath.Stroke = new SolidColorBrush(BorderColor);
            BaseBorderPath.StrokeThickness = BorderStrokeThickness;
        }

        private void UpdateGradientColors()
        {
            // OuterColor → uçlar ve kenarlar
            Color outer = OuterColor;
            Color outerTransparent = Color.FromArgb(0x00, outer.R, outer.G, outer.B);
            Color outerFade = Color.FromArgb(0xAA, outer.R, outer.G, outer.B);
            Color outerFull = Color.FromArgb(0xFF, outer.R, outer.G, outer.B);

            OuterTailTransparent.Color = outerTransparent;
            OuterHeadTransparent.Color = outerTransparent;
            OuterTailFade.Color = outerFade;
            OuterHeadFade.Color = outerFade;
            EdgeLeft.Color = outerFull;
            EdgeRight.Color = outerFull;

            // InnerColor → glow
            Color innerFull = Color.FromArgb(0xFF, InnerColor.R, InnerColor.G, InnerColor.B);
            GlowLeft.Color = innerFull;
            GlowRight.Color = innerFull;

            // CenterColor → merkez
            Color centerFull = Color.FromArgb(0xFF, CenterColor.R, CenterColor.G, CenterColor.B);
            CenterStop.Color = centerFull;
        }
    }
}
