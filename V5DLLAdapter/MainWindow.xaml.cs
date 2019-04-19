using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using V5RPC;

namespace V5DLLAdapter
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        StrategyDLL dll = new StrategyDLL();
        StrategyServer server = null;
        ConsoleRedirectWriter consoleRedirectWriter = new ConsoleRedirectWriter();

        int _port = 5555;
        public int Port
        {
            get { return _port; }
            set { _port = value; notify("Path"); }
        }
        string _path = "";
        public string Path
        {
            get { return _path; }
            set { _path = value; notify("Path"); }
        }
        public bool IsRunning { get { return dll.IsLoaded && server != null; } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void notify(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            PropertyChanged += OnPropertyChanged;
            notify("IsRunning");
            consoleRedirectWriter.OnWrite += (string text) =>
            {
                Dispatcher.Invoke(() =>
                {
                    logBox.AppendText(text);
                    logBox.ScrollToEnd();
                });
            };
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            filePathEdit.IsEnabled = browseBtn.IsEnabled = portEdit.IsEnabled = !IsRunning;
            startStopBtn.Content = IsRunning ? "停止" : "启动";
        }

        private void StartStopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRunning)
            {
                if (dll.Load(Path))
                {
                    try
                    {
                        server = new StrategyServer(Port, dll);
                        Task.Run(server.Run);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        dll.Unload();
                    }
                }
                else
                {
                    MessageBox.Show("无法加载指定的 DLL");
                }
            }
            else
            {
                server.Dispose();
                server = null;
                dll.Unload();
            }
            notify("IsRunning");
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length != 0)
                {
                    Path = files[0];
                }
            }
        }

        private new void PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog()
            {
                DefaultExt = ".dll",
                Filter = "动态链接库|*.dll|应用程序|*.exe|所有文件|*.*"
            };
            if (dlg.ShowDialog(this) ?? false)
            {
                Path = dlg.FileName;
            }
        }

        private void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            TestBtn.IsEnabled = false;
            StrategyClient client = new StrategyClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, Port));
            Task.Run(() =>
            {
                string message;
                try
                {
                    var info = client.GetTeamInfo();
                    message = info.TeamName;
                }
                catch (TimeoutException)
                {
                    message = "请求超时";
                }
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message);
                    TestBtn.IsEnabled = true;
                });
            });
        }
    }
}
