using System;
using System.Collections.Generic;
using System.Text;

namespace TCPService.Interfaces
{
    public interface IMyTCPClientFactory
    {
        IMyTCPClient CreateClient(string IPAddress, int port);
    }
}
