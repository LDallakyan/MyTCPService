using System;
using System.Collections.Generic;
using System.Text;

namespace TCPService.Interfaces
{
    public interface IMyTCPServer : IDisposable
    {
        void Write(string varName, string value);
        void Read(string varName);
        void Start();
        void Stop();
    }
}
