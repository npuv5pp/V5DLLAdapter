using System;
using Python.Runtime;
using V5RPC;

namespace V5PyAdapter
{
    class Program
    {
        static void Main(string[] args)
        {
            string name = args[0];
            int port = int.Parse(args[1]);
            Console.WriteLine("Hello World!");
            PythonStrategy strategy = new PythonStrategy(name);
            StrategyServer server = new StrategyServer(port, strategy);
            server.Run();
        }
    }
}