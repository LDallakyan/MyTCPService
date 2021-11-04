using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TCPService.Interfaces;

namespace TCPService
{
    public class MyTCPClientSet : IMyTCPClientSet
    {
        #region Public_Members
        public string EndPointAddress { get; set; }
        public TcpClient Client
        {
            get
            {
                return client;
            }
        }

        public NetworkStream NetworkStream
        {
            get
            {
                return networkStream;
            }
        }

        public SemaphoreSlim SemaphoreLock { get; set; }
        public CancellationTokenSource TokenSource { get; set; }
        public CancellationToken Token { get; set; }
        #endregion
        #region Private_Members
        TcpClient client;
        NetworkStream networkStream;
        #endregion

        #region Constructors
        public MyTCPClientSet(string endPointAddress, System.Net.Sockets.TcpClient tcp)
        {
            EndPointAddress = endPointAddress;
            client = tcp;
            networkStream = tcp.GetStream();
            TokenSource = new CancellationTokenSource();
            Token = TokenSource.Token;
            SemaphoreLock = new SemaphoreSlim(1, 1);
        }
        #endregion

        #region Public_Methods
        public void Dispose()
        {
            if(Token != null)
            {
                if(!TokenSource.IsCancellationRequested)
                {
                    TokenSource.Cancel();
                    TokenSource.Dispose();
                }
            }

            networkStream?.Close();
            client?.Close();
            client?.Dispose();
        }
        #endregion
    }
}
