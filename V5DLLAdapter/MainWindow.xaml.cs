using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
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
    /// 
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        StrategyDLL dll = new StrategyDLL();
        StrategyServer server = null;
        ConsoleRedirectWriter consoleRedirectWriter = new ConsoleRedirectWriter();

        int _port = 5555;
        public int Port
        {
            get => _port;
            set { _port = value; notify("Path"); }
        }
        string _path = "";
        public string Path
        {
            get => _path;
            set { _path = value; notify("Path"); }
        }
        public bool IsRunning => dll.IsLoaded && server != null;

        public readonly int MAX_LOG_ITEMS = 2000;

        public struct LogEntry
        {
            public string message;
            public string tag;
            public DateTime dateTime;
            public Severity severity;
            public Brush Color
            {
                get
                {
                    switch (severity)
                    {
                        case Severity.Verbose: return Brushes.Gray;
                        case Severity.Warning: return Brushes.Yellow;
                        case Severity.Error: return Brushes.Red;
                        default: return Brushes.White;
                    }
                }
            }

            public override string ToString()
            {
                string severityString = "?";
                switch (severity)
                {
                    case Severity.Verbose:
                        severityString = "V";
                        break;
                    case Severity.Info:
                        severityString = "I";
                        break;
                    case Severity.Warning:
                        severityString = "W";
                        break;
                    case Severity.Error:
                        severityString = "E";
                        break;
                }
                return $"[{dateTime}] {severityString}/{tag}: {message}";
            }
        }

        ObservableCollection<LogEntry> _logOutput = new ObservableCollection<LogEntry>();
        public ObservableCollection<LogEntry> LogOutput
        {
            get => _logOutput;
            set { _logOutput = value; notify("LogOutput"); }
        }

        Severity _logLevel = Severity.Info;
        public Severity LogLevel { get => _logLevel; set { _logLevel = value; notify("LogLevel"); } }

        string _logFilterKeyword = "";
        public string LogFilterKeyword { get => _logFilterKeyword; set { _logFilterKeyword = value; notify("LogFilterKeyword"); } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                    Log(text, tag: "StdOut", severity: Severity.Verbose);
                });
            };
            logItems.Items.Filter = FilterLog;
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            filePathEdit.IsEnabled = browseBtn.IsEnabled = portEdit.IsEnabled = !IsRunning;
            startStopBtn.Content = IsRunning ? "停止" : "启动";
            UpdateTitle();
            if (e.PropertyName == "LogFilterKeyword")
            {
                logItems.Items.Filter = logItems.Items.Filter;
            }
        }

        private void Start()
        {
            if (!IsRunning)
            {
                if (dll.Load(Path))
                {
                    Log($"已加载策略程序 {Path}", severity: Severity.Verbose);
                    try
                    {
                        server = new StrategyServer(Port, dll);
                        Task.Run(server.Run);
                        Log("策略服务器开始运行", severity: Severity.Info);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message, severity: Severity.Error);
                        dll.Unload();
                    }
                }
                else
                {
                    Log($"无法加载指定的策略程序 {Path}", severity: Severity.Error);
                }
                notify("IsRunning");
            }
        }

        private void Stop()
        {
            if (IsRunning)
            {
                server.Dispose();
                server = null;
                dll.Unload();
                notify("IsRunning");
                Log("策略服务器已停止", severity: Severity.Info);
            }
        }

        private void StartStopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning) { Stop(); } else { Start(); }
        }

        private void UpdateTitle()
        {
            if (IsRunning)
            {
                Title = $"V5DLLAdapter - {dll.DLL}";
            }
            else
            {
                Title = "V5DLLAdapter";
            }
        }

        public void Log(string message, string tag = "V5DLLAdapter", Severity severity = Severity.Info)
        {
            if (LogOutput.Count >= MAX_LOG_ITEMS)
            {
                LogOutput.RemoveAt(0);
            }
            bool scrollToEnd = logScroller.VerticalOffset == logScroller.ScrollableHeight;
            var entry = new LogEntry
            {
                dateTime = DateTime.Now,
                severity = severity,
                tag = tag,
                message = message
            };
            LogOutput.Add(entry);
            if (scrollToEnd)
            {
                logScroller.ScrollToEnd();
            }
        }

        private bool FilterLog(object obj)
        {
            if (obj is LogEntry entry)
            {
                if (entry.severity < LogLevel)
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(LogFilterKeyword) && !entry.ToString().Contains(LogFilterKeyword))
                {
                    return false;
                }
            }
            return true;
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
            Log("正在对当前策略程序执行 GetTeamInfo 调用", tag: "StrategyTest", severity: Severity.Verbose);
            StrategyClient client = null;
            try
            {
                client = new StrategyClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, Port))
                {
                    Timeout = 10000,
                    RetryInterval = 1000
                };
            }
            catch (Exception ex)
            {
                Log(ex.Message, tag: "StrategyTest", severity: Severity.Error);
            }
            if (client == null)
            {
                return;
            }
            TestBtn.IsEnabled = false;
            Task.Run(() =>
            {
                try
                {
                    var info = client.GetTeamInfo();
                    Dispatcher.Invoke(() =>
                    {
                        Log($"TeamName={info.TeamName}", tag: "StrategyTest", severity: Severity.Info);
                        TestBtn.IsEnabled = true;
                    });
                }
                catch (TimeoutException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Log("当前策略没有回应", tag: "StrategyTest", severity: Severity.Warning);
                        TestBtn.IsEnabled = true;
                    });
                }
            });
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var hWnd = PresentationSource.FromVisual(this) as HwndSource;
            hWnd.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_APP = 0x8000;
            const long CMD_START = 1;
            const long CMD_STOP = 2;
            if (msg == WM_APP)
            {
                switch (wParam.ToInt64())
                {
                    case CMD_START:
                        {
                            Start();
                        }
                        return new IntPtr(1);
                    case CMD_STOP:
                        {
                            Stop();
                        }
                        return new IntPtr(1);
                }
            }
            return IntPtr.Zero;
        }

        private void LogLevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            logItems.Items.Filter = logItems.Items.Filter;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            switch (e.Source)
            {
                case MenuItem x when "clearLog".Equals(x.CommandParameter):
                    {
                        LogOutput.Clear();
                    }
                    break;
                case MenuItem x when "copyItem".Equals(x.CommandParameter) && x.DataContext is LogEntry ent:
                    {
                        Clipboard.SetText(ent.ToString());
                    }
                    break;
                case MenuItem x when "copyItemMessage".Equals(x.CommandParameter) && x.DataContext is LogEntry ent:
                    {
                        Clipboard.SetText(ent.message);
                    }
                    break;
                case MenuItem x when "copyLog".Equals(x.CommandParameter):
                    {
                        var q = from ent in LogOutput where FilterLog(ent) select ent.ToString();
                        Clipboard.SetText(string.Join(Environment.NewLine, q));
                    }
                    break;
                case MenuItem x when "copyLogMessage".Equals(x.CommandParameter):
                    {
                        var q = from ent in LogOutput where FilterLog(ent) select ent.message;
                        Clipboard.SetText(string.Join(Environment.NewLine, q));
                    }
                    break;
            }
        }

        private void TextBlock_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.Source is TextBlock tb)
            {
                tb.Background = Brushes.MidnightBlue;
            }
        }

        private void TextBlock_ContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            if (e.Source is TextBlock tb)
            {
                tb.Background = null;
            }
        }

        private void FilterKeywordEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter&&e.Source is TextBox tb)
            {
                tb.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }
    }

    public enum Severity
    {
        Verbose, Info, Warning, Error
    }
}
