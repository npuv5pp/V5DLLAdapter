using System;
using Python.Runtime;

namespace V5PyAdapter
{
    class Program
    {
        static void Main(string[] args)
        {
            string name = args[0];
            Console.WriteLine("Hello World!");
            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                sys.path.append(".");
                
                dynamic strategy = Py.Import(name);
                Console.WriteLine(strategy.get_team_info(1));
            }
        }
    }
}