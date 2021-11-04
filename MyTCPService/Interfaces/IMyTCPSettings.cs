using System;
using System.Collections.Generic;
using System.Text;

namespace TCPService.Interfaces
{
    public interface IMyTCPSettings
    {
        string IPAddress { get; set; }
        int Port { get; set; }
        int BufferSize { get; set; }
        int Timeout { get; set; }
        int CheckTime { get; set; }
        int ReconnectCount { get; set; }      
    }
}
