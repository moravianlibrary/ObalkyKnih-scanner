using System;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using WIA;
using System.IO;

namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            this.applicationVersion.Content = Assembly.GetEntryAssembly().GetName().Version.Major
                 + "." + Assembly.GetEntryAssembly().GetName().Version.Minor;
            this.wiaVersion.Content = typeof(Device).Assembly.GetName().Version.Major
                 + "." + typeof(Device).Assembly.GetName().Version.Minor;
            DateTime buildTime = new System.IO.FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime;
            this.buildYear.Content = buildTime.Day.ToString() + ". "  + buildTime.Month + ". "
                + buildTime.Year.ToString();
        }
    }
}
