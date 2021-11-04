using System;
using System.Threading;
using TCPService;
using TCPService.Interfaces;

namespace MyTcpClientTest
{
    class Program
    {
        static void Main(string[] args)
        {
            RunClient();
        }

        static void RunClient()
        {
            using (IMyTCPClient client = new MyTCPClientFactory().CreateClient("127.0.0.1", 13005))
            {
                Console.WriteLine("Client_Test");
                Thread.Sleep(100);
                client.Connect();
                client.Write("test", "Test_From_Client.");
                client.Read("test");
                client.Read("test1");
                Thread.Sleep(300);
                client.Write("test1", "Second_Tect_From_Client.");
                client.Read("test1");
                client.Disconnect();
                Thread.Sleep(1000);
                Console.WriteLine("Press Enter for exit.");
                Console.Read();
            }
        }

    }
}
