using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using com.google.zxing;
using com.google.zxing.qrcode;

namespace Spinner
{
    public partial class QrCodeControl : UserControl
    {
        // reflection cache (performans)
        private MethodInfo _matrixGetMethod;

        public QrCodeControl()
        {
            InitializeComponent();
            Loaded += (_, __) => Render();
        }

        public static readonly DependencyProperty QrDataProperty =
            DependencyProperty.Register(nameof(QrData), typeof(string), typeof(QrCodeControl),
                new PropertyMetadata(string.Empty, OnAnyChanged));

        public string QrData
        {
            get => (string)GetValue(QrDataProperty);
            set => SetValue(QrDataProperty, value);
        }

        public static readonly DependencyProperty QrWidthProperty =
            DependencyProperty.Register(nameof(QrWidth), typeof(int), typeof(QrCodeControl),
                new PropertyMetadata(256, OnAnyChanged));

        public int QrWidth
        {
            get => (int)GetValue(QrWidthProperty);
            set => SetValue(QrWidthProperty, value);
        }

        public static readonly DependencyProperty QrHeightProperty =
            DependencyProperty.Register(nameof(QrHeight), typeof(int), typeof(QrCodeControl),
                new PropertyMetadata(256, OnAnyChanged));

        public int QrHeight
        {
            get => (int)GetValue(QrHeightProperty);
            set => SetValue(QrHeightProperty, value);
        }

        public static readonly DependencyProperty DarkColorHexProperty =
            DependencyProperty.Register(nameof(DarkColorHex), typeof(string), typeof(QrCodeControl),
                new PropertyMetadata("#FF000000", OnAnyChanged));

        public string DarkColorHex
        {
            get => (string)GetValue(DarkColorHexProperty);
            set => SetValue(DarkColorHexProperty, value);
        }

        public static readonly DependencyProperty LightColorHexProperty =
            DependencyProperty.Register(nameof(LightColorHex), typeof(string), typeof(QrCodeControl),
                new PropertyMetadata("#FFFFFFFF", OnAnyChanged));

        public string LightColorHex
        {
            get => (string)GetValue(LightColorHexProperty);
            set => SetValue(LightColorHexProperty, value);
        }

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((QrCodeControl)d).Render();

        private void Render()
        {
            if (!IsLoaded) return;

            if (string.IsNullOrWhiteSpace(QrData) || QrWidth <= 0 || QrHeight <= 0)
            {
                PART_Image.Source = null;
                return;
            }

            try
            {
                var dark = ParseColor(DarkColorHex, Colors.Black);
                var light = ParseColor(LightColorHex, Colors.White);

                var writer = new QRCodeWriter();
                var matrix = writer.encode(QrData, BarcodeFormat.QR_CODE, QrWidth, QrHeight);

                // get / get_Renamed method cache et
                CacheMatrixGetter(matrix);

                int mw = ReadIntPropOrMethod(matrix, "Width", "getWidth", "get_Width", "width");
                int mh = ReadIntPropOrMethod(matrix, "Height", "getHeight", "get_Height", "height");

                int width = (mw > 0) ? mw : QrWidth;
                int height = (mh > 0) ? mh : QrHeight;

                PART_Image.Source = BuildBitmapSourceFromMatrix(matrix, width, height, dark, light);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                PART_Image.Source = null;
            }
        }

        private void CacheMatrixGetter(object matrix)
        {
            if (_matrixGetMethod != null) return;

            var t = matrix.GetType();

            _matrixGetMethod =
                t.GetMethod("get_Renamed", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(int) }, null)
                ?? t.GetMethod("get", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(int) }, null);

            if (_matrixGetMethod == null)
                throw new MissingMethodException($"BitMatrix getter not found on type: {t.FullName}");
        }

        private BitmapSource BuildBitmapSourceFromMatrix(object matrix, int width, int height, Color dark, Color light)
        {
            int stride = width * 4; // BGRA32
            byte[] pixels = new byte[height * stride];

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    // -1 => white, digerleri => black (senin eski kodunla uyumlu)
                    bool isDark = MatrixIsDark(matrix, x, y);

                    Color c = isDark ? dark : light;
                    int i = row + (x * 4);
                    pixels[i + 0] = c.B;
                    pixels[i + 1] = c.G;
                    pixels[i + 2] = c.R;
                    pixels[i + 3] = c.A;
                }
            }

            var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            bmp.Freeze();
            return bmp;
        }

        private bool MatrixIsDark(object matrix, int x, int y)
        {
            object val = _matrixGetMethod.Invoke(matrix, new object[] { x, y });

            // Bazı portlarda bool gelir (true => black)
            if (val is bool b) return b;

            // Bazı portlarda -1 white, 0/1 black
            if (val is sbyte sb) return sb != -1;
            if (val is byte by) return by != 255;   // byte'ta -1 => 255
            if (val is short sh) return sh != -1;
            if (val is int i) return i != -1;

            // fallback: bilinmiyorsa white say
            return false;
        }

        private static int ReadIntPropOrMethod(object obj, params string[] names)
        {
            var t = obj.GetType();

            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (p != null && p.PropertyType == typeof(int))
                    return (int)p.GetValue(obj);

                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(int))
                    return (int)m.Invoke(obj, null);
            }

            return 0;
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return fallback;
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch { return fallback; }
        }
    }
}
