using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Spinner
{
    public partial class CountdownTimer : UserControl, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _uiTimer;
        private readonly Stopwatch _sw = new Stopwatch();

        private string _lastText = "";
        private bool _completedRaised;

        public CountdownTimer()
        {
            InitializeComponent();

            // UI için: sık tick at ama ekrana sadece değişince yaz
            _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _uiTimer.Tick += (_, __) => Update();

            Loaded += (_, __) =>
            {
                UpdateDisplay(Duration);
                if (AutoStart) Start();
            };

            Unloaded += (_, __) => Stop();
        }

        // Başlangıç süresi
        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), typeof(CountdownTimer),
                new PropertyMetadata(TimeSpan.FromSeconds(30)));

        public TimeSpan Duration
        {
            get => (TimeSpan)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        // Otomatik başlasın mı?
        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register(nameof(AutoStart), typeof(bool), typeof(CountdownTimer),
                new PropertyMetadata(false));

        public bool AutoStart
        {
            get => (bool)GetValue(AutoStartProperty);
            set => SetValue(AutoStartProperty, value);
        }

        private string _remainingDisplay = "00:00";
        public string RemainingDisplay
        {
            get => _remainingDisplay;
            private set
            {
                _remainingDisplay = value;
                OnPropertyChanged(nameof(RemainingDisplay));
            }
        }

        public event EventHandler CountdownCompleted;

        public void Start()
        {
            if (Duration <= TimeSpan.Zero)
            {
                UpdateDisplay(TimeSpan.Zero);
                return;
            }

            _completedRaised = false;
            _sw.Restart();
            _uiTimer.Start();
            Update();
        }

        public void Stop()
        {
            _uiTimer.Stop();
            _sw.Stop();
        }

        public void Reset()
        {
            Stop();
            UpdateDisplay(Duration);
        }

        private void Update()
        {
            var remaining = Duration - _sw.Elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                UpdateDisplay(remaining);
                Stop();

                if (!_completedRaised)
                {
                    _completedRaised = true;
                    CountdownCompleted?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            UpdateDisplay(remaining);
        }

        private void UpdateDisplay(TimeSpan remaining)
        {
            string text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";

            // Aynı text'i tekrar tekrar yazma (UI daha az yorulsun)
            if (text != _lastText)
            {
                _lastText = text;
                RemainingDisplay = text;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
