using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Spinner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //SpinnerControl.Width = 260;
            //SpinnerControl.Height = 90;
            //SpinnerControl.Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));

            SpinnerControl.PeriodSeconds = 6.0;
            SpinnerControl.SnakeFraction = 0.36;

            SpinnerControl.OuterColor = Color.FromRgb(0x00, 0xE5, 0xFF);
            SpinnerControl.InnerColor = Color.FromRgb(0x00, 0xB8, 0xFF);
            SpinnerControl.CenterColor = Color.FromRgb(0x80, 0xFA, 0xFF);
        }
    }
}
