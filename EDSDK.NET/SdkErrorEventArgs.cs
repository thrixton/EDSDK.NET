using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace EDSDK.NET
{
    public class SdkErrorEventArgs : EventArgs
    {
        public string Error { get; set; }
        public LogLevel ErrorLevel { get; set; }
    }
}
