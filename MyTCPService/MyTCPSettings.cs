using System;
using System.Collections.Generic;
using System.Text;
using TCPService.Interfaces;

namespace TCPService
{
    public class MyTCPSettings : IMyTCPSettings
    {
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public int BufferSize { get; set; }
        public int Timeout { get; set; }
        public int CheckTime { get; set; }
        public int ReconnectCount { get; set; }       
    }
}
