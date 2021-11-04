using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TCPService.Interfaces
{
    public interface IMyTCPClientSet : IDisposable
    {
        string EndPointAddress { get; set; }
        System.Net.Sockets.TcpClient Client { get; }
        NetworkStream NetworkStream { get; }
        SemaphoreSlim SemaphoreLock { get; set; }
        CancellationTokenSource TokenSource { get; set; }
        CancellationToken Token { get; set; }
    }
}