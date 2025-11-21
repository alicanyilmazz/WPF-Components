using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Spinner
{
    public partial class MainWindow : Window
    {
        // Yılan uzunluğu: çevrenin yüzde kaçı
        private const double SnakeFraction = 0.25;   // 6x / 24x
        // Bir tam tur süresi (sn) – hız
        private const double PeriodSeconds = 10.0;

        // Path üzerindeki noktalar (flattened)
        private List<Point> _points = new List<Point>();
        private List<double> _segLengths = new List<double>();
        private List<double> _cumLengths = new List<double>();
        private double _totalLength;

        // Animasyon durumu
        private double _headPos; // [0, _totalLength)
        private TimeSpan _lastRenderTime;

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

            // İçte yuvarlak köşeli dikdörtgen alanı
            double rectWidth = w;
            double rectHeight = h;
            double radius = 22; // köşe yumuşaklığı (oynayabilirsin)

            var rect = new Rect(0, 0, rectWidth, rectHeight);

            // Yuvarlatılmış dikdörtgen geometrisi
            var rectGeom = new RectangleGeometry(rect, radius, radius);

            // Temel border path’i bu geometriyle çiz
            BaseBorderPath.Data = rectGeom;

            // Yılan da aynı geometriyi takip edecek
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

            var flat = geometry.GetFlattenedPathGeometry();

            foreach (var fig in flat.Figures)
            {
                if (fig.Segments.Count == 0)
                    continue;

                Point last = fig.StartPoint;
                _points.Add(last);

                foreach (var seg in fig.Segments.OfType<PolyLineSegment>())
                {
                    foreach (var pt in seg.Points)
                    {
                        double len = Distance(last, pt);
                        if (len > 0)
                        {
                            _points.Add(pt);
                            _segLengths.Add(len);
                            _totalLength += len;
                            _cumLengths.Add(_totalLength);
                            last = pt;
                        }
                    }
                }

                // Figure kapalıysa (closed), son nokta başlangıca zaten dönmüş olur.
            }

            // _points: N nokta
            // _segLengths: N-1 segment
            // _cumLengths: N-1 cumulative length
            if (_points.Count < 2)
            {
                _totalLength = 0;
            }
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

            double speed = _totalLength / PeriodSeconds;

            _headPos += speed * dt;
            while (_headPos >= _totalLength)
                _headPos -= _totalLength;

            double snakeLen = _totalLength * SnakeFraction;
            double tailPos = _headPos - snakeLen;
            if (tailPos < 0)
                tailPos += _totalLength;

            UpdateSnakePath(tailPos, _headPos);
        }

        private void UpdateSnakePath(double tail, double head)
        {
            var geom = new PathGeometry();

            if (tail <= head)
            {
                var fig = BuildFigureForRange(tail, head);
                if (fig != null)
                    geom.Figures.Add(fig);
            }
            else
            {
                // wrap: [tail, total) ve [0, head]
                var fig1 = BuildFigureForRange(tail, _totalLength);
                var fig2 = BuildFigureForRange(0, head);

                if (fig1 != null) geom.Figures.Add(fig1);
                if (fig2 != null) geom.Figures.Add(fig2);
            }

            SnakePath.Data = geom;
        }

        private PathFigure BuildFigureForRange(double startDist, double endDist)
        {
            if (endDist <= startDist || _points.Count < 2)
                return null;

            // start noktası
            var startPoint = PointOnPath(startDist, out int startIndex);
            // end noktası
            var endPoint = PointOnPath(endDist, out int endIndex);

            var pts = new List<Point> { startPoint };

            // Aradaki tam noktalar
            int i = startIndex + 1;
            while (i <= endIndex && i < _points.Count)
            {
                pts.Add(_points[i]);
                i++;
            }

            // Eğer endIndex, startIndex ile aynı segmentte ise
            if (pts.Count == 1)
            {
                pts.Add(endPoint);
            }
            else
            {
                // Son noktayı endPoint olarak güncelle
                pts[pts.Count - 1] = endPoint;
            }

            var fig = new PathFigure
            {
                StartPoint = pts[0],
                IsClosed = false,
                IsFilled = false
            };

            if (pts.Count > 1)
            {
                var seg = new PolyLineSegment();
                for (int k = 1; k < pts.Count; k++)
                    seg.Points.Add(pts[k]);

                fig.Segments.Add(seg);
            }

            return fig;
        }

        private Point PointOnPath(double dist, out int segIndex)
        {
            // dist: [0, _totalLength)
            // segIndex: dist'in düştüğü segment
            for (int i = 0; i < _segLengths.Count; i++)
            {
                double segStart = (i == 0) ? 0 : _cumLengths[i - 1];
                double segEnd = _cumLengths[i];

                if (dist <= segEnd)
                {
                    double t = (dist - segStart) / _segLengths[i];
                    var p0 = _points[i];
                    var p1 = _points[i + 1];
                    segIndex = i;
                    return Lerp(p0, p1, t);
                }
            }

            // Güvenlik için: son noktayı ver
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
