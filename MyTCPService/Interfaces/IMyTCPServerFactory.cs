using System;
using System.Collections.Generic;
using System.Text;

namespace TCPService.Interfaces
{
    public interface IMyTCPServerFactory
    {
        IMyTCPServer CreateServer(string IPAddress, int port);       
    }
}
