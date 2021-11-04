using System;
using System.Collections.Generic;
using System.Text;
using TCPService.Interfaces;

namespace TCPService
{
    public class TCPServiceLogger : IMyTCPServiceLogger
    {
        public void Write(string message)
        {
            Console.WriteLine(message);
        }

        public void Write(Exception exception)
        {
            Console.WriteLine($"{exception.Message}");
        }     
    }
}
