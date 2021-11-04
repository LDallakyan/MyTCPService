using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPService.Interfaces;
using System.Linq;

namespace TCPService
{
    class MyTCPServer : IMyTCPServer
    {
        #region Public-Members
        public bool IsListening
        {
            get
            {
                return isListening;
            }
        }

        public IMyTCPSettings Settings { get; set; }
        #endregion

        #region Private-Members
        bool isListening = false;
        TcpListener listener = null;

        CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken token;
        Task acceptConnections = null;
        Task checkClientsConnections = null;

        IMyTCPServiceLogger logger;
        ConcurrentDictionary<string, IMyTCPClientSet> clients = new ConcurrentDictionary<string, IMyTCPClientSet>();
        ConcurrentDictionary<string, string> commands = new ConcurrentDictionary<string, string>();
        #endregion

        #region Constructors
        public MyTCPServer(IMyTCPSettings settings)
        {
            Settings = settings;
        }

        public MyTCPServer(IMyTCPSettings settings, IMyTCPServiceLogger logger)
        {
            Settings = settings;
            this.logger = logger;
        }
        #endregion

        #region Public_Methods
        public void Start()
        {
            if (isListening) throw new InvalidOperationException("The server is already running.");

            IPAddress localAddr = IPAddress.Parse(Settings.IPAddress);
            listener = new TcpListener(localAddr, Settings.Port);
            listener.Start();
            isListening = true;
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
            checkClientsConnections = Task.Factory.StartNew(() => CheckClientsConnections(), token);
            acceptConnections = Task.Factory.StartNew(() => AcceptConnections(), token);
        }

        public void Stop()
        {
            if (!isListening)
            {
                logger?.Write("The server isn't running.");
                return;
            }

            listener.Stop();
            if (!tokenSource.IsCancellationRequested)
            {
                tokenSource.Cancel();               
                tokenSource.Dispose();
            }
            
            isListening = false;
          
          
        }

        public void Dispose()
        {
            try
            {
                isListening = false;
                if (tokenSource != null)
                {
                    if (!tokenSource.IsCancellationRequested)
                    {
                        tokenSource.Cancel();                        
                        tokenSource.Dispose();
                    }
                }

                if (clients != null && clients.Count > 0)
                {
                    foreach (KeyValuePair<string, IMyTCPClientSet> client in clients)
                    {
                        client.Value.Dispose();
                        logger?.Write($"{client.Value.EndPointAddress} is disconnected.");
                    }
                }                

                if (listener != null && listener.Server != null)
                {
                    listener.Server.Close();
                    listener.Server.Dispose();
                }

                if (listener != null)
                {
                    listener.Stop();
                }
            }
            catch (Exception ex)
            {
                logger.Write(ex);
                throw ex;
            }            
        }

        public void Read(string varName)
        {
            try
            {
                string value;
                commands.TryGetValue(varName, out value);
                if (string.IsNullOrEmpty(value))
                {
                    throw new KeyNotFoundException($"The {varName} command not found or not available.");
                }
                SendToAll(value);                
            }
            catch(KeyNotFoundException ex)
            {
                logger?.Write(ex);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string varName, string value)
        {   
            try
            {  
                if (commands.ContainsKey(varName))
                {
                    string oldValue;
                    commands.TryGetValue(varName, out oldValue);
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new NullReferenceException("The command not found or not available.");
                    }
                    commands.TryUpdate(varName, value, oldValue);
                }
                else
                {
                    commands.TryAdd(varName, value);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Private_Methods
        private async Task AcceptConnections()
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                IMyTCPClientSet client = null;

                try
                {
                    System.Net.Sockets.TcpClient tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    string clientEndPointAdress = tcpClient.Client.RemoteEndPoint.ToString();
                    client = new MyTCPClientSet(clientEndPointAdress, tcpClient);
                    clients.TryAdd(clientEndPointAdress, client);
                    CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(client.Token, token);
                    Task readFromClientTask = Task.Run(() => ReadFromClient(client), linkedTokenSource.Token);
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException || ex is ObjectDisposedException)
                {                   
                    break;
                }
                catch (Exception ex)
                {
                    logger?.Write(ex);
                    continue;   
                }
            }
            isListening = false;
        }

        private async Task ReadFromClient(IMyTCPClientSet client)
        {          
            CancellationTokenSource linkedReadFromClientToken = CancellationTokenSource.CreateLinkedTokenSource(token, client.Token);

            while (true)
            {
                try
                {
                    if (!client.Client.Connected || client.Token.IsCancellationRequested)
                    {                       
                        break;
                    }                                      

                    byte[] data = await ReadFromClientAsync(client, linkedReadFromClientToken.Token).ConfigureAwait(false);
                    if (data == null)
                    {
                        await Task.Delay(Settings.Timeout, linkedReadFromClientToken.Token).ConfigureAwait(false);
                        continue;
                    }
                    logger?.Write("From Client: " + Encoding.UTF8.GetString(data));
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException || ex is ObjectDisposedException)
                {                   
                    break;
                }
                catch (Exception ex)
                {
                    logger.Write(ex);
                    break;
                }
            }
            IMyTCPClientSet currentValue;
            if(!string.IsNullOrEmpty(client.EndPointAddress))
                clients.TryRemove(client.EndPointAddress, out currentValue);      
            client?.Dispose();
        }

        private async Task<byte[]> ReadFromClientAsync(IMyTCPClientSet client, CancellationToken readToken)
        {
            byte[] buffer = new byte[Settings.BufferSize];
            int read = 0;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                while (true)
                {
                    read = await client.NetworkStream.ReadAsync(buffer, 0, buffer.Length, readToken).ConfigureAwait(false);

                    if (read > 0)
                    {
                        await memoryStream.WriteAsync(buffer, 0, read, readToken).ConfigureAwait(false);
                        return memoryStream.ToArray();
                    }
                    return null;
                }
            }
        }

        private void SendToAll(string messages)
        {           
            try
            {                
                foreach (var client in clients)
                {
                    Send(client.Value.EndPointAddress, messages);
                }
            }
            catch (Exception ex)
            {
                logger?.Write(ex);
            }   
        }

        public void Send(string endPointAddress, string data)
        {
            if (String.IsNullOrEmpty(endPointAddress)) throw new ArgumentNullException(nameof(endPointAddress));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream memoryStream = new MemoryStream();
            memoryStream.Write(bytes, 0, bytes.Length);
            memoryStream.Seek(0, SeekOrigin.Begin);
            SendInternal(endPointAddress, bytes.Length, memoryStream);
        }

        private void SendInternal(string endPointAddress, long contentLength, Stream stream)
        {
            IMyTCPClientSet client = null;
            if (!clients.TryGetValue(endPointAddress, out client)) return;
            if (client == null) return;

            long bytesRemaining = contentLength;
            int bytesRead = 0;
            byte[] buffer = new byte[Settings.BufferSize];

            try
            {
                client.SemaphoreLock.Wait();

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        client.NetworkStream.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                }

                client.NetworkStream.Flush();
            }
            finally
            {
               client?.SemaphoreLock.Release();
            }
        }        

        private bool IsClientConnected(System.Net.Sockets.TcpClient client)
        {            
            if (isListening && client.Connected)
            {               
                if ((client.Client.Poll(0, SelectMode.SelectWrite)) && (!client.Client.Poll(0, SelectMode.SelectError)))
                {
                    byte[] buffer = new byte[1];
                    Task.Delay(Settings.CheckTime).Wait();
                    if (client.Client.Receive(buffer, SocketFlags.Peek) == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        private async Task CheckClientsConnections()
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    break;
                try
                {  
                    foreach (KeyValuePair<string, IMyTCPClientSet> client in clients)
                    {
                        if (!client.Value.Client.Connected)
                        {                            
                            DisconnectClient(client.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.Write(ex);
                }
                await Task.Delay(Settings.CheckTime).ConfigureAwait(false);
            }
        }
        private void DisconnectClient(string endPointAddress)
        {
            if (String.IsNullOrEmpty(endPointAddress)) throw new ArgumentNullException(nameof(endPointAddress));

            IMyTCPClientSet client = null;

            if (!clients.TryGetValue(endPointAddress, out client))
            {
                logger?.Write($"Unable to find client: {endPointAddress}.");
            }
            else
            {
                IMyTCPClientSet currentValue;
                clients.TryRemove(endPointAddress, out currentValue);
                client?.Dispose();
            }           
        }
        #endregion
    }
}
