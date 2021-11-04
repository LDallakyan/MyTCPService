using System;
using System.Collections.Generic;
using System.Text;

namespace TCPService.Interfaces
{
    public interface IMyTCPServiceLogger
    {
        void Write(string message);
        void Write(Exception exception);      
    }
}
