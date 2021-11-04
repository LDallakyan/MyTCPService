using System;
using System.Collections.Generic;
using System.Text;
using TCPService.Interfaces;

namespace TCPService
{
    public class MyTCPServerFactory : IMyTCPServerFactory
    {
        public IMyTCPServer CreateServer(string ipAddress, int port)
        {
            IMyTCPSettings settings = new MyTCPSettings()
            {
                IPAddress = ipAddress,
                Port = port,
                BufferSize = 35536,
                Timeout = 1000,
                CheckTime = 100,
                ReconnectCount = 3             
            };
            IMyTCPServiceLogger logger = new TCPServiceLogger();

            return new MyTCPServer(settings, logger);
        }
    }
}
