using System;
using System.Threading;
using TCPService;
using TCPService.Interfaces;

namespace MyTcpServerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            RunServer();
        }

        static void RunServer()
        {
            using (IMyTCPServer server = new MyTCPServerFactory().CreateServer("127.0.0.1", 13005))
            {
                Console.WriteLine("Server_Test");
                server.Start();
                server.Read("test");
                Thread.Sleep(300);
                server.Write("test", "Test_From_Server.");
                server.Read("test");
                server.Read("test1");
                server.Write("test1", "Second_From_Server.");
                server.Read("test1");
                Thread.Sleep(1000);
                Console.WriteLine("Press Enter for exit.");
                Console.Read();

            }
        }
    }
}
