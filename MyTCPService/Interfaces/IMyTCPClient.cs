using System;
using System.Collections.Generic;
using System.Text;

namespace TCPService.Interfaces
{
    public interface IMyTCPClient : IDisposable
    {
        void Write(string varName, string value);
        void Read(string varName);
        bool IsConnected { get; }
        IMyTCPSettings Settings { get; set; }
        void Connect();
        void Disconnect();
    }
}
