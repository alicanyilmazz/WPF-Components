using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Spinner
{
    public partial class MainWindow : Window
    {
        // Yılan uzunluğu: toplam yolun yüzdesi
        private const double SnakeFraction = 0.40;

        // Bir tam tur süresi (sn)
        private const double PeriodSeconds = 7.0;
        private const double JumpCycleSeconds = 3.5;   // 1 kaybolup-gelme süresi
        private const double JumpVisiblePortion = 0.65; // döngünün yüzde kaçı görünür
        private double _jumpPhase;                      // 0..1 arası faz

        // ✅ Çizgi her yerde eşit kalınlık
        private const double SnakeThickness = 3.0;
        private const double MinThickness = 4.0;   // uçlardaki kalınlık
        private const double MaxThickness = 5.0;   // ortadaki kalınlık
        // Path üzerindeki noktalar (eşit aralıklı sample)
        private List<Point> _points = new List<Point>();
        private List<double> _segLengths = new List<double>();
        private List<double> _cumLengths = new List<double>();
        private double _totalLength;

        // Animasyon state
        private double _headPos;
        private TimeSpan _lastRenderTime;
        private double _progress; // 0..1 tur ilerlemesi

        // Geometri ölçüleri (kenarlara eşit süre dağıtmak için)
        private double _rectW, _rectH, _radius;
        private double _topLen, _sideLen, _arcLen;
        private double _effPerimeter;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetupGeometry();
            RootCanvas.SizeChanged += (_, __) => SetupGeometry();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void SetupGeometry()
        {
            double w = RootCanvas.ActualWidth;
            double h = RootCanvas.ActualHeight;
            if (w <= 0 || h <= 0)
                return;

            double rectWidth = w;
            double rectHeight = h;
            double radius = 22;

            _rectW = rectWidth;
            _rectH = rectHeight;
            _radius = radius;

            var rect = new Rect(0, 0, rectWidth, rectHeight);
            var rectGeom = new RectangleGeometry(rect, radius, radius);

            BaseBorderPath.Data = rectGeom;

            // Gerçek uzunluklar
            _topLen = Math.Max(0, _rectW - 2 * _radius);
            _sideLen = Math.Max(0, _rectH - 2 * _radius);
            _arcLen = Math.PI * _radius / 2.0; // her köşe çeyrek yay

            // Kenarlara eşit süre -> efektif çevre
            double avgStraight = (_topLen + _sideLen) / 2.0;
            _effPerimeter = 4 * avgStraight + 4 * _arcLen;

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
            _progress = 0;

            // Flatten al
            var flat = geometry.GetFlattenedPathGeometry(0.2, ToleranceType.Absolute);

            var rawPts = new List<Point>();

            foreach (var fig in flat.Figures)
            {
                if (fig.Segments.Count == 0)
                    continue;

                rawPts.Add(fig.StartPoint);

                foreach (var seg in fig.Segments.OfType<PolyLineSegment>())
                {
                    rawPts.AddRange(seg.Points);
                }
            }

            if (rawPts.Count < 2) return;

            // ✅ Eşit aralıkla resample (sol/sağ kenarlar da akıcı olsun)
            double step = 1.5;
            var sampled = Resample(rawPts, step);

            _points = sampled;

            for (int i = 0; i < _points.Count - 1; i++)
            {
                double len = Distance(_points[i], _points[i + 1]);
                if (len <= 0) continue;

                _segLengths.Add(len);
                _totalLength += len;
                _cumLengths.Add(_totalLength);
            }

            // ✅ Loop kapat
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
            var result = new List<Point>();
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

            var args = (RenderingEventArgs)e;
            if (_lastRenderTime == TimeSpan.Zero)
            {
                _lastRenderTime = args.RenderingTime;
                return;
            }

            double dt = (args.RenderingTime - _lastRenderTime).TotalSeconds;
            _lastRenderTime = args.RenderingTime;

            // Çizgi her zaman düzgün ilerlesin (kesintisiz hareket)
            double speed = _totalLength / PeriodSeconds;

            _headPos += speed * dt;
            while (_headPos >= _totalLength)
                _headPos -= _totalLength;

            double snakeLen = _totalLength * SnakeFraction;
            double tailPos = _headPos - snakeLen;
            if (tailPos < 0)
                tailPos += _totalLength;

            // Önce normal geometrimizi çiziyoruz
            UpdateSnakePath(tailPos, _headPos);

            // 🔥 Şimdi yumuşak görünürlük (fade in / fade out)
            _jumpPhase += dt / JumpCycleSeconds;
            _jumpPhase -= Math.Floor(_jumpPhase); // 0..1'de tut

            double phase = _jumpPhase;
            double opacity;

            // 0.0–0.3   : tam görünür
            // 0.3–0.5   : yavaşça kaybol (1 -> 0)
            // 0.5–0.7   : kayıp (neredeyse görünmez)
            // 0.7–0.9   : yavaşça ortaya çık (0 -> 1)
            // 0.9–1.0   : tekrar tam görünür

            if (phase < 0.3)
            {
                opacity = 1.0;
            }
            else if (phase < 0.5)
            {
                double t = (phase - 0.3) / 0.2;   // 0..1
                opacity = 1.0 - t;               // 1 -> 0
            }
            else if (phase < 0.7)
            {
                opacity = 0; // 0 yerine, çok hafif gölge
            }
            else if (phase < 0.9)
            {
                double t = (phase - 0.7) / 0.2;   // 0..1
                opacity = t;                     // 0 -> 1
            }
            else
            {
                opacity = 1.0;
            }

            SnakePath.Opacity = opacity;
        }

        // Kenarlara eşit süre veren mapping
        private double MapProgressToDistance(double p)
        {
            double avgStraight = (_topLen + _sideLen) / 2.0;
            double sEff = p * _effPerimeter;

            // Top straight
            if (sEff < avgStraight)
                return (sEff / avgStraight) * _topLen;

            sEff -= avgStraight;

            // Arc 1
            if (sEff < _arcLen)
                return _topLen + sEff;

            sEff -= _arcLen;

            // Right straight
            if (sEff < avgStraight)
                return _topLen + _arcLen + (sEff / avgStraight) * _sideLen;

            sEff -= avgStraight;

            // Arc 2
            if (sEff < _arcLen)
                return _topLen + _arcLen + _sideLen + sEff;

            sEff -= _arcLen;

            // Bottom straight
            if (sEff < avgStraight)
                return _topLen + 2 * _arcLen + _sideLen + (sEff / avgStraight) * _topLen;

            sEff -= avgStraight;

            // Arc 3
            if (sEff < _arcLen)
                return 2 * _topLen + 2 * _arcLen + _sideLen + sEff;

            sEff -= _arcLen;

            // Left straight
            if (sEff < avgStraight)
                return 2 * _topLen + 3 * _arcLen + _sideLen + (sEff / avgStraight) * _sideLen;

            sEff -= avgStraight;

            // Arc 4
            return 2 * _topLen + 3 * _arcLen + 2 * _sideLen + Math.Min(sEff, _arcLen);
        }

        private void UpdateSnakePath(double tail, double head)
        {
            var centers = new List<Point>();

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

            var geom = BuildConstantWidthGeometry(centers);
            SnakePath.Data = geom;
        }

        private List<Point> GetPointsForRange(double startDist, double endDist)
        {
            var pts = new List<Point>();
            if (endDist <= startDist || _points.Count < 2)
                return pts;

            var startPoint = PointOnPath(startDist, out int startIndex);
            var endPoint = PointOnPath(endDist, out int endIndex);

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

        // ✅ Her yerde eşit kalınlıklı şerit
        //private Geometry BuildConstantWidthGeometry(List<Point> centers)
        //{
        //    int n = centers.Count;
        //    var leftPts = new List<Point>(n);
        //    var rightPts = new List<Point>(n);

        //    Vector lastDir = new Vector(1, 0);

        //    for (int i = 0; i < n; i++)
        //    {
        //        Point p = centers[i];

        //        Vector dir;
        //        if (i == 0)
        //            dir = centers[1] - centers[0];
        //        else if (i == n - 1)
        //            dir = centers[n - 1] - centers[n - 2];
        //        else
        //        {
        //            Vector d1 = centers[i] - centers[i - 1];
        //            Vector d2 = centers[i + 1] - centers[i];
        //            dir = d1 + d2;
        //        }

        //        if (dir.LengthSquared < 1e-6)
        //            dir = lastDir;
        //        else
        //            lastDir = dir;

        //        dir.Normalize();

        //        Vector nrm = new Vector(-dir.Y, dir.X);
        //        double half = SnakeThickness / 2.0;

        //        leftPts.Add(p + nrm * half);
        //        rightPts.Add(p - nrm * half);
        //    }

        //    var fig = new PathFigure
        //    {
        //        StartPoint = leftPts[0],
        //        IsClosed = true,
        //        IsFilled = true
        //    };

        //    var seg = new PolyLineSegment();

        //    for (int i = 1; i < leftPts.Count; i++)
        //        seg.Points.Add(leftPts[i]);

        //    for (int i = rightPts.Count - 1; i >= 0; i--)
        //        seg.Points.Add(rightPts[i]);

        //    fig.Segments.Add(seg);

        //    var geom = new PathGeometry();
        //    geom.Figures.Add(fig);
        //    return geom;
        //}
        private Geometry BuildConstantWidthGeometry(List<Point> centers)
        {
            int n = centers.Count;
            var leftPts = new List<Point>(n);
            var rightPts = new List<Point>(n);

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

                // 0..1 arasında param: 0 = kuyruk, 1 = baş
                double t = (double)i / (n - 1);

                // Ortası kalın, uçlarda ince: 0..1..0 tepe fonksiyonu
                double s = 1.0 - 2.0 * Math.Abs(t - 0.5); // 0..1..0
                if (s < 0) s = 0;

                double thickness = MinThickness + (MaxThickness - MinThickness) * s;
                double half = thickness / 2.0;

                Point left = p + nrm * half;
                Point right = p - nrm * half;

                leftPts.Add(left);
                rightPts.Add(right);
            }

            var fig = new PathFigure
            {
                StartPoint = leftPts[0],
                IsClosed = true,
                IsFilled = true
            };

            var seg = new PolyLineSegment();

            for (int i = 1; i < leftPts.Count; i++)
                seg.Points.Add(leftPts[i]);

            for (int i = rightPts.Count - 1; i >= 0; i--)
                seg.Points.Add(rightPts[i]);

            fig.Segments.Add(seg);

            var geom = new PathGeometry();
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
            return _points.Last();
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
    }
}
