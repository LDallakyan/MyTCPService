using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPService.Interfaces;

namespace TCPService
{
    public class MyTCPClient : IMyTCPClient
    {
        #region Public_Members
        public bool IsConnected
        {
            get
            {
                return (client != null && client.Connected);
            }           
        }

        public IMyTCPSettings Settings { get; set; }
        #endregion

        #region Private_Members       
        private SemaphoreSlim semaphoreLock = new SemaphoreSlim(1);
        private System.Net.Sockets.TcpClient client = null;
        private NetworkStream networkStream = null;

        private IDictionary<string,string> commands = new  Dictionary<string,string>();
        private IMyTCPServiceLogger logger = null;

        private bool isTimeout = false;

        private Task serverReceiverHandler = null;
        private Task serverConnectionChecker = null;
        private CancellationTokenSource tokenSource;
        private CancellationToken token;
        #endregion

        #region Constructors
        public MyTCPClient(IMyTCPSettings settings)
        {           
            Settings = settings;
        }

        public MyTCPClient(IMyTCPSettings settings, IMyTCPServiceLogger logger)
        {
           Settings = settings;
           this.logger = logger;
        }
        #endregion

        #region Public_Methods
        public void Connect()
        {
            if (IsConnected)
                return;
            
            try
            {                
                client?.Dispose();
                client = new TcpClient();
                int reconnectCount = 0;
                for (; reconnectCount <= Settings.ReconnectCount; reconnectCount++)
                {
                    IAsyncResult result = client.BeginConnect(Settings.IPAddress, Settings.Port, null, null);                   
                    result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(Settings.Timeout));
                   if (!client.Connected)
                   {   
                        continue;
                   }
                   else
                   {
                       client.EndConnect(result);
                       networkStream = client.GetStream();
                       tokenSource = new CancellationTokenSource();
                       token = tokenSource.Token;
                       serverConnectionChecker = Task.Factory.StartNew(() => ServerConnectionChecker(), token);
                       serverReceiverHandler = Task.Factory.StartNew(() => ReadFromServer(token), token);
                       isTimeout = false;
                       return;
                   }
                }
                client.Close();
                isTimeout = true;
                throw new TimeoutException("Failed connecting to the server. Timeout!");
            }
            catch (TimeoutException ex)
            {
                logger?.Write(ex.Message);
            }
            catch (Exception ex)
            {
                logger?.Write(ex);
                throw;
            }               
        }

        public void Disconnect()
        {
            tokenSource.Cancel();
            client.Close();          
        }

        public void Dispose()
        {
            if (tokenSource != null)
            {
                if (!tokenSource.IsCancellationRequested)
                {
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                }
            }

            networkStream?.Close();
            networkStream?.Dispose();

            client?.Close();
            client?.Dispose();
        }

        public void Read(string varName)
        {
            try
            {
                semaphoreLock.Wait();
                if (!commands.ContainsKey(varName))
                    throw new KeyNotFoundException($"The {varName} command not found or not available.");
                CheckConnectionStatus();
                byte[] bytes = Encoding.UTF8.GetBytes(commands[varName]);
                MemoryStream memoryStream = new MemoryStream();
                memoryStream.Write(bytes, 0, bytes.Length);
                memoryStream.Seek(0, SeekOrigin.Begin);
                Send(bytes.Length, memoryStream);
            }
            catch (KeyNotFoundException ex)
            {
                logger?.Write(ex);
            }
            catch (Exception ex)
            {
                logger?.Write(ex);
                throw;
            }
            finally
            {
                semaphoreLock.Release();
            }
        }        

        public void Write(string varName, string value)
        {
            try
            {
                semaphoreLock.Wait();

                CheckConnectionStatus();

                if (commands.ContainsKey(varName))
                {
                    commands[varName] = value;
                }
                else
                {
                    commands.Add(new KeyValuePair<string, string>(varName, value));
                }
            }
            catch(TimeoutException ex)
            {
                logger?.Write(ex.Message);
            }
            catch (Exception ex)
            {
                logger?.Write(ex);
                throw;
            }
            finally
            {
                semaphoreLock.Release();
            }
        }
        #endregion

        #region Private_Methods
        private void CheckConnectionStatus()
        { 
            if(isTimeout)
                throw new TimeoutException("Timeout connecting to the server. Please reconnect again.");

            if (!IsConnected)
                Connect();
        }

        private void Send(long contentLength, Stream stream)
        {
            long bytesRemaining = contentLength;
            int bytesRead = 0;
            byte[] buffer = new byte[Settings.BufferSize];

            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    networkStream.Write(buffer, 0, bytesRead);
                    bytesRemaining -= bytesRead;
                }
            }
        }

        private async Task ReadFromServer(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        
                        logger?.Write("Server is disconnected.");
                        break;
                    }

                    if (IsConnected)
                    {
                        byte[] data = await ReadFromServerAsync(token).ConfigureAwait(false);

                        if (data == null)
                        {
                            await Task.Delay(Settings.Timeout).ConfigureAwait(false);
                            continue;
                        }

                        logger?.Write("From Server: " + Encoding.UTF8.GetString(data));
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when(ex is IOException ||
                                      ex is SocketException ||
                                      ex is TaskCanceledException ||
                                      ex is OperationCanceledException ||
                                      ex is ObjectDisposedException
                                     )
            {
            }
            catch (Exception ex)
            {
                logger?.Write(ex);
                throw;
            }
            Dispose();
        }

        private async Task<byte[]> ReadFromServerAsync(CancellationToken token)
        {  
            byte[] buffer = new byte[Settings.BufferSize];
            int read = 0;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                read = await networkStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (read > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    return memoryStream.ToArray();
                }
                return null;
            }
        }

        private async Task ServerConnectionChecker()
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    break;              
                if (!IsConnected)
                {
                    tokenSource.Cancel();
                    isTimeout = true;
                }               
                await Task.Delay(Settings.CheckTime).ConfigureAwait(false);
            }
        }
        #endregion
    }
}
