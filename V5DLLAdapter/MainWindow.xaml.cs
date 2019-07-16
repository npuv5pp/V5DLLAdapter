using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    public sealed partial class MainWindow : INotifyPropertyChanged, IDisposable
    {
        StrategyDllBase dll = new StrategyDll();
        StrategyServer server = null;
        ConsoleRedirectWriter consoleRedirectWriter = new ConsoleRedirectWriter();
        bool corruptedState = false;

        int _port = 5555;
        public int Port
        {
            get => _port;
            set { _port = value; Notify(nameof(Port)); }
        }
        string _path = "";
        public string Path
        {
            get => _path;
            set { _path = value; Notify(nameof(Path)); }
        }

        private bool _reverseCoordinate = false;
        public bool ReverseCoordinate
        {
            get => _reverseCoordinate;
            set { _reverseCoordinate = value; Notify(nameof(ReverseCoordinate)); }
        }

        public bool IsRunning => dll.IsLoaded && server != null;

        public const int MAX_LOG_ITEMS = 2000;

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

        public ObservableCollection<LogEntry> LogOutput { get; } = new ObservableCollection<LogEntry>();

        Severity _logLevel = Severity.Info;
        public Severity LogLevel { get => _logLevel; set { _logLevel = value; Notify(nameof(LogLevel)); } }

        string _logFilterKeyword = "";
        public string LogFilterKeyword { get => _logFilterKeyword; set { _logFilterKeyword = value; Notify(nameof(LogFilterKeyword)); } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            PropertyChanged += OnPropertyChanged;
            Notify(nameof(IsRunning));
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
            isYellowStrategy.IsEnabled
                = filePathEdit.IsEnabled
                = browseBtn.IsEnabled
                = portEdit.IsEnabled = !IsRunning;
            
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
                dll = new StrategyDll();
                if (!dll.Load(Path, ReverseCoordinate))
                {
                    dll = new LegacyDll();
                    if (dll.Load(Path, ReverseCoordinate))
                    {
                        Log("采用兼容模式", severity: Severity.Warning);
                    }
                    else
                    {
                        Log($"无法加载指定的策略程序 {Path}", severity: Severity.Error);
                        Notify(nameof(IsRunning));
                        return;
                    }
                }
                Log($"已加载策略程序 {Path}", severity: Severity.Verbose);
                try
                {
                    server = new StrategyServer(Port, dll);
                    Task.Run(() =>
                    {
                        try
                        {
                            server.Run();
                        }
                        catch (DllException e)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                corruptedState = true;
                                Log(e.Message + Environment.NewLine + e.MaskedInnerException.ToString(), severity: Severity.Error);
                                Stop();
                            });
                        }
                    });
                    Log("策略服务器开始运行", severity: Severity.Info);
                }
                catch (Exception ex)
                {
                    Log(ex.Message, severity: Severity.Error);
                    dll.Unload();
                }
                Notify(nameof(IsRunning));
            }
        }

        private void Stop()
        {
            if (IsRunning)
            {
                server.Dispose();
                server = null;
                dll.Unload();
                Notify(nameof(IsRunning));
                Log("策略服务器已停止", severity: Severity.Info);
            }
        }

        private void StartStopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning)
            {
                Stop();
            }
            else
            {
                if (corruptedState)
                {
                    var result = MessageBox.Show(
                        $"此前的本机代码出现过错误，继续运行可能导致未知的问题。{Environment.NewLine}仍然要继续吗？",
                        null, MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No);
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                Start();
            }
        }

        private void UpdateTitle()
        {
            Title = IsRunning ? $"V5DLLAdapter - {dll.Dll}" : "V5DLLAdapter";
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
            if (e.Key == Key.Enter && e.Source is TextBox tb)
            {
                tb.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var hWnd = PresentationSource.FromVisual(this) as HwndSource;
            hWnd.AddHook(WndProc);
            const int GWLP_USERDATA = -21;
            SetWindowLong(hWnd.Handle, GWLP_USERDATA, 0x56352B2B);
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_APP = 0x8000;
            const int WM_COPYDATA = 0x004A;
            if (msg == WM_APP)
            {
                const long CMD_START = 1;
                const long CMD_STOP = 2;
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
            else if (msg == WM_COPYDATA)
            {
                const long DATA_LOG = 1;
                var s = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                switch (s.dwData.ToInt64())
                {
                    case DATA_LOG:
                        {
                            var buffer = new byte[s.cbData];
                            Marshal.Copy(s.lpData, buffer, 0, buffer.Length);
                            string data = Encoding.Unicode.GetString(buffer);
                            ParseAndLog(data);
                        }
                        return new IntPtr(1);
                }
            }
            return IntPtr.Zero;
        }

#pragma warning disable CS0649
        struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public uint cbData;
            public IntPtr lpData;
        }
#pragma warning restore CS0649

        readonly Regex logRegex = new Regex(@"^([a-zA-z])\s*/\s*([^:]+)\s*:", RegexOptions.Compiled);
        private void ParseAndLog(string rawText)
        {
            var m = logRegex.Match(rawText);
            if (m.Success)
            {
                string severityChar = m.Groups[1].Value;
                string tag = m.Groups[2].Value.Trim();
                string message = rawText.Substring(m.Index + m.Length);
                Severity severity;
                switch (severityChar.ToUpper())
                {
                    case "V": severity = Severity.Verbose; break;
                    case "I": default: severity = Severity.Info; break;
                    case "W": severity = Severity.Warning; break;
                    case "E": severity = Severity.Error; break;
                }
                Log(message, tag, severity);
            }
            else
            {
                Log(rawText, tag: "CopyData");
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var app = (App) Application.Current;
            using (var iterator = app.Args.AsEnumerable().GetEnumerator())
            {
                bool start = false;
                while (iterator.MoveNext())
                {
                    switch (iterator.Current?.ToUpperInvariant())
                    {
                        case "-FILE":
                            if (!iterator.MoveNext())
                            {
                                Log("-File 选项缺少参数", severity: Severity.Error);
                                continue;
                            }

                            Path = iterator.Current;
                            break;
                        case "-PORT":
                            if (!iterator.MoveNext())
                            {
                                Log("-Port 选项缺少参数", severity: Severity.Error);
                                continue;
                            }

                            if (!ushort.TryParse(iterator.Current, out ushort port))
                            {
                                Log("-Port 选项的参数必须是有效的端口号", severity: Severity.Error);
                                continue;
                            }

                            Port = port;
                            break;
                        
                        case "-START":
                            start = true;
                            break;
                        
                        case "-YELLOW":
                            ReverseCoordinate = true;
                            break;
                    }
                }
                if (start)
                {
                    startStopBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            }
        }

        public void Dispose()
        {
            dll?.Dispose();
            server?.Dispose();
            consoleRedirectWriter?.Dispose();
        }
    }

    public enum Severity
    {
        Verbose, Info, Warning, Error
    }
}
