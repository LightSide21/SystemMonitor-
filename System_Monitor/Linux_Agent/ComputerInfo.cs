using System;

namespace SystemMonitor
{
    public class ComputerInfo
    {
        public string ConnectionCode { get; set; }
        public string ComputerName { get; set; }
        public string OsVersion { get; set; }
        
        public ComputerInfo()
        {
            ComputerName = Environment.MachineName;
            OsVersion = Environment.OSVersion.ToString();
        }
    }
}