using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace V5DLLAdapter
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public string[] Args { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Args = e.Args;
        }
    }
}
