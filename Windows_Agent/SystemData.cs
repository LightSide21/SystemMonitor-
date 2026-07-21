using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SystemMonitor
{
    public class SystemData
    {
        public string ComputerId { get; set; }
        public string ComputerName { get; set; }
        public DateTime Timestamp { get; set; }
        public string ConfigHash { get; set; }

        public OsData Os { get; set; } = new OsData();
        public CpuData Cpu { get; set; } = new CpuData();
        public List<GpuData> Gpus { get; set; } = new List<GpuData>();
        public RamData Ram { get; set; } = new RamData();
        public List<DiskData> Disks { get; set; } = new List<DiskData>();
        public List<NetworkData> Networks { get; set; } = new List<NetworkData>();

        // --- Метод для генерации хеша ---
        public string GetConfigHash()
        {
            var sb = new StringBuilder();
            sb.Append(Os.Name);
            sb.Append(Cpu.Name);
            sb.Append(Ram.Total);
            // Добавляем модели GPU и дисков в хеш
            foreach (var gpu in Gpus) sb.Append(gpu.Name);
            foreach (var disk in Disks) sb.Append(disk.Model);

            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    
    
    public class OsData
    {
        public string Name { get; set; }
        public string Architecture { get; set; }
        public string UserName { get; set; }
        public string BootTime { get; set; }
    }
    
    public class CpuData
    {
        public string Name { get; set; }
        public int LogicalCores { get; set; }
        public int PhysicalCores { get; set; }
        public int CurrentFrequency { get; set; } 
        public int MaxFrequency { get; set; }     
        public int Load { get; set; }             
        public string Temperature { get; set; } 
        public int ProcessCount { get; set; }
        public string Power { get; set; } = "N/A";

        
        public double TemperatureNumeric => ParseHelper.ToDouble(Temperature);
    }
    
    public class GpuData
    {
        public string Name { get; set; }
        public string Vendor { get; set; }
        public string Type { get; set; }          
        public string Memory { get; set; }        
        public string Utilization { get; set; }   
        public string Temperature { get; set; }
        public string Power { get; set; } = "N/A";

               public double UtilizationNumeric => ParseHelper.ToDouble(Utilization);
        public double TemperatureNumeric => ParseHelper.ToDouble(Temperature);
    }
    
    public class RamData
    {
        public string Total { get; set; }
        public string Used { get; set; }
        public string Available { get; set; }
        public string Load { get; set; }          
        public string MemoryType { get; set; }
        public string Frequency { get; set; }
        
        public List<MemorySlotData> Slots { get; set; } = new List<MemorySlotData>();
        
       
        public double LoadNumeric => ParseHelper.ToDouble(Load);
    }

    public class MemorySlotData
    {
        public string BankLabel { get; set; }
        public string Capacity { get; set; }
        public string Speed { get; set; }
        public string Manufacturer { get; set; }
        public string FormFactor { get; set; }
    }
    
    public class DiskData
    {
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string TotalCapacity { get; set; }
        public string Temperature { get; set; }
        public List<PartitionData> Partitions { get; set; } = new List<PartitionData>();
    }

    public class PartitionData
    {
        public string DriveLetter { get; set; }
        public string TotalSpace { get; set; }
        public string FreeSpace { get; set; }
    }
    
    public class NetworkData
    {
        public string Name { get; set; }
        public string AdapterName { get; set; }
        public string Type { get; set; }
        public string MacAddress { get; set; }
        public string Ipv4 { get; set; }
        public string Speed { get; set; }         
        public string SendSpeed { get; set; }
        public string ReceiveSpeed { get; set; }
        public string BytesSent { get; set; }
        public string BytesReceived { get; set; }
    }

    
    public static class ParseHelper
    {
        public static double ToDouble(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            var match = Regex.Match(input, @"[\d\.,]+");
            if (match.Success)
            {
                if (double.TryParse(match.Value.Replace(",", "."), 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out double result))
                {
                    return result;
                }
            }
            return 0;
        }
    }
}