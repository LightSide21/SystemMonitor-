using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
using Hardware.Info;
using System.IO;
using System.Reflection;

namespace SystemMonitor
{
    public static class ComputerMonitor
    {
        private static Computer _computer;
    
        private static bool _initialized = false;
        
        
        private static readonly IHardwareInfo _hardwareInfo = new HardwareInfo();

        private static Dictionary<string, NetworkStats> _previousNetworkStats = new Dictionary<string, NetworkStats>();

        private class NetworkStats
        {
            public DateTime LastUpdateTime { get; set; }
            public long LastBytesSent { get; set; }
            public long LastBytesReceived { get; set; }
        }


        private static void EnsurePawnIoInstalled()
{
    // Проверяем, установлен ли PawnIO в системе 
    string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PawnIO");
    if (Directory.Exists(installPath)) 
        return; 

    try
    {
        // Путь, куда распакуем установщик
        string tempExePath = Path.Combine(Path.GetTempPath(), "PawnIO_setup.exe");

        // Вытаскиваем файл из внутренностей нашей программы
        var assembly = Assembly.GetExecutingAssembly();
        
        using (Stream stream = assembly.GetManifestResourceStream("SystemMonitor.Resources.PawnIO_setup.exe"))
        {
            if (stream == null) return;
            using (FileStream fileStream = new FileStream(tempExePath, FileMode.Create))
            {
                stream.CopyTo(fileStream);
            }
        }

        // Запускаем установку в тихом режиме 
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = tempExePath,
            Arguments = "-install", // Ключи скрытой установки
            UseShellExecute = true,
            Verb = "runas" // На всякий случай запрашиваем права админа 
        });
        
        
        process?.WaitForExit();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Не удалось установить PawnIO: {ex.Message}");
    }
}

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                EnsurePawnIoInstalled();
                _computer = new Computer()
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsNetworkEnabled = true, 
                    IsStorageEnabled = true  
                };
                _computer.Open();
                Thread.Sleep(200);
                _initialized = true;
            }
            catch
            {
                _computer = null;
                _initialized = false;
            }
        }

        public static void Close()
        {
            try
            {
                _computer?.Close();
            }
            catch { }
            _computer = null;
            _initialized = false;
            _previousNetworkStats.Clear();
        }

        public static SystemData GetSystemInfo()
{
    if (!_initialized) Initialize();

    if (_computer != null)
    {
        try
        {
            
            _computer.Accept(new UpdateVisitor());
        }
        catch { }
    }

            var data = new SystemData();
            data.Timestamp = DateTime.Now;
            data.ComputerName = Environment.MachineName;

            data.Os = GetOsInfo();
            data.Cpu = GetCpuDetailed();
            data.Gpus = GetGpuDetailed();
            data.Ram = GetRamDetailed();
            data.Disks = GetDiskDetailed();
            data.Networks = GetNetworkDetailed();

            return data;
        }




#region CPU Logic

private static CpuData GetCpuDetailed()
{
    var cpu = new CpuData();
    try
    {
        //  Базовые данные
        cpu.LogicalCores = Environment.ProcessorCount;
        cpu.PhysicalCores = Math.Max(1, cpu.LogicalCores / 2);
        cpu.ProcessCount = Process.GetProcesses().Length;
        cpu.Name = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";

        //  ЧТЕНИЕ ДАННЫХ ИЗ ЖЕЛЕЗА 
        var cpuHardware = _computer?.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpuHardware != null)
        {
            cpuHardware.Update();
            cpu.Name = cpuHardware.Name;

            // Загрузка
            var loadSensor = cpuHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"));
            if (loadSensor?.Value != null) cpu.Load = (int)loadSensor.Value.Value;

            // Температура
            var tempSensors = cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue && s.Value.Value > 0).ToList();
            var mainTemp = tempSensors.FirstOrDefault(s => s.Name.Contains("Package") || s.Name.Contains("Core Average") || s.Name.Contains("Tctl")) ?? tempSensors.FirstOrDefault();
            if (mainTemp?.Value != null) cpu.Temperature = $"{mainTemp.Value.Value:F1}°C";

            // ЧАСТОТА: Ищем датчики Clock для ядер процессора
            var clockSensors = cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Value.HasValue && s.Value.Value > 0 && s.Name.Contains("Core")).ToList();
            if (clockSensors.Any())
            {
                
                cpu.CurrentFrequency = (int)clockSensors.Average(s => s.Value.Value);
                cpu.MaxFrequency = cpu.CurrentFrequency; 
            }

            // --- ПОТРЕБЛЕНИЕ ПРОЦЕССОРА (ВАТТЫ) ---
            var powerSensors = cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Power && s.Value.HasValue).ToList();
            // Ищем датчик всего пакета процессора 
            var mainPower = powerSensors.FirstOrDefault(s => s.Name.Contains("Package")) ?? powerSensors.FirstOrDefault();
            if (mainPower?.Value != null) 
            {
                cpu.Power = $"{mainPower.Value.Value:F1} W";
            }
        }

        //  Запасной вариант 
        if (cpu.CurrentFrequency <= 0)
        {
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    if (obj["CurrentClockSpeed"] != null) cpu.CurrentFrequency = Convert.ToInt32(obj["CurrentClockSpeed"]);
                    if (obj["MaxClockSpeed"] != null) cpu.MaxFrequency = Convert.ToInt32(obj["MaxClockSpeed"]);
                    break;
                }
            }
        }

        //  проверки
        if (string.IsNullOrEmpty(cpu.Temperature)) cpu.Temperature = "N/A";
        if (cpu.CurrentFrequency <= 0) cpu.CurrentFrequency = 1000;
        if (cpu.MaxFrequency <= 0) cpu.MaxFrequency = cpu.CurrentFrequency;
    }
    catch 
    {
        cpu.Name = "Ошибка получения ЦП";
        cpu.Temperature = "N/A";
    }
    
    return cpu;
}

        #endregion

        #region GPU Logic

        private static List<GpuData> GetGpuDetailed()
        {
            var gpus = new List<GpuData>();
            try
            {
                _hardwareInfo.RefreshVideoControllerList();

                var gpuHardware = _computer?.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel).ToList();

                if (gpuHardware != null && gpuHardware.Count > 0)
                {
                    foreach (var hw in gpuHardware)
                    {
                        var g = new GpuData { Name = hw.Name };
                        
                        if (hw.HardwareType == HardwareType.GpuIntel) { g.Vendor = "Intel"; g.Type = "Integrated (iGPU)"; }
                        else if (hw.HardwareType == HardwareType.GpuNvidia) { g.Vendor = "NVIDIA"; g.Type = "Discrete (dGPU)"; }
                        else if (hw.HardwareType == HardwareType.GpuAmd) { g.Vendor = "AMD"; g.Type = "Discrete (dGPU)"; }

                       
                        var hwGpu = _hardwareInfo.VideoControllerList.FirstOrDefault(v => v.Name.Contains(hw.Name) || hw.Name.Contains(v.Name));

                        var memSensor = hw.Sensors.FirstOrDefault(s => (s.SensorType == SensorType.Data || s.SensorType == SensorType.SmallData) && (s.Name.Contains("Memory") || s.Name.Contains("VRAM")));
                        
                        if (memSensor?.Value != null) 
                            g.Memory = FormatBytes((long)(memSensor.Value.Value * 1024 * 1024));
                        else if (hwGpu != null)
                            g.Memory = FormatBytes((long)hwGpu.AdapterRAM);
                        else 
                            g.Memory = "N/A";

                        var loadSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && (s.Name.Contains("Core") || s.Name.Contains("GPU Core") || s.Name.Contains("D3D") || s.Name.Contains("3D")));
                        if (loadSensor == null) loadSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && !s.Name.Contains("Memory"));

                        g.Utilization = loadSensor?.Value != null ? $"{loadSensor.Value.Value:F1}%" : "0%";

                        var temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                        g.Temperature = temp?.Value != null ? $"{temp.Value.Value:F1}°C" : "N/A";

                        // --- ПОТРЕБЛЕНИЕ ВИДЕОКАРТЫ (ВАТТЫ) ---
                        var gpuPowerSensors = hw.Sensors.Where(s => s.SensorType == SensorType.Power && s.Value.HasValue).ToList();
                        // Для Nvidia это "GPU Power" или "GPU Total", для AMD - "GPU Package"
                        var gpuMainPower = gpuPowerSensors.FirstOrDefault(s => s.Name.Contains("Total") || s.Name.Contains("Package") || s.Name.Contains("Board")) 
                                        ?? gpuPowerSensors.FirstOrDefault();
                        if (gpuMainPower?.Value != null) 
                        {
                            g.Power = $"{gpuMainPower.Value.Value:F1} W";
                        }

                        gpus.Add(g);
                    }
                }
                else
                {
                    
                    foreach (var hwGpu in _hardwareInfo.VideoControllerList)
                    {
                        var g = new GpuData { Name = hwGpu.Name ?? "Unknown GPU" };
                        string nameLower = g.Name.ToLower();
                        if (nameLower.Contains("nvidia")) g.Vendor = "NVIDIA";
                        else if (nameLower.Contains("amd") || nameLower.Contains("radeon")) g.Vendor = "AMD";
                        else if (nameLower.Contains("intel")) g.Vendor = "Intel";
                        else g.Vendor = "Unknown";

                        g.Type = "Unknown";
                        g.Memory = FormatBytes((long)hwGpu.AdapterRAM);
                        g.Utilization = "N/A";
                        g.Temperature = "N/A";
                        gpus.Add(g);
                    }
                }
            }
            catch { }
            return gpus;
        }

        #endregion

        #region RAM Logic

        private static RamData GetRamDetailed()
        {
            var ram = new RamData();
            try
            {
                _hardwareInfo.RefreshMemoryStatus();
                _hardwareInfo.RefreshMemoryList();

                ulong totalBytes = _hardwareInfo.MemoryStatus.TotalPhysical;
                ulong availBytes = _hardwareInfo.MemoryStatus.AvailablePhysical;
                ulong usedBytes = totalBytes > availBytes ? totalBytes - availBytes : 0;

                

                if (totalBytes > 0)
                {
                    ram.Total = FormatBytes((long)totalBytes);
                    ram.Available = FormatBytes((long)availBytes);
                    ram.Used = FormatBytes((long)usedBytes);
                    ram.Load = $"{(usedBytes * 100.0 / totalBytes):F1}%";
                }

                foreach (var mem in _hardwareInfo.MemoryList)
                {
                    var slot = new MemorySlotData
                    {
                        BankLabel = mem.BankLabel,
                        Manufacturer = mem.Manufacturer,
                        Capacity = FormatBytes((long)mem.Capacity),
                        Speed = mem.Speed > 0 ? $"{mem.Speed} MHz" : null,
                        FormFactor = mem.FormFactor.ToString()
                    };
                    ram.Slots.Add(slot);
                    
                    if (ram.Frequency == null && mem.Speed > 0)
                        ram.Frequency = $"{mem.Speed} MHz";
                }
            }
            catch { }
            return ram;
        }

        #endregion

        #region Disk Logic

        private static List<DiskData> GetDiskDetailed()
{
    var disks = new List<DiskData>();
    try
    {
        _hardwareInfo.RefreshDriveList();
        
        
        var lhmDrives = _computer?.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();

        foreach (var drive in _hardwareInfo.DriveList)
        {
            var diskItem = new DiskData 
            { 
                Model = drive.Model ?? "Unknown Disk",
                SerialNumber = drive.SerialNumber?.Trim(),
                TotalCapacity = FormatBytes((long)drive.Size),
                Temperature = "N/A" 
            };

            // Ищем совпадение диска по имени и забираем температуру
            if (lhmDrives != null)
            {
                var match = lhmDrives.FirstOrDefault(h => h.Name.Contains(diskItem.Model) || diskItem.Model.Contains(h.Name));
                if (match != null)
                {
                    var tSensor = match.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                    if (tSensor?.Value != null)
                    {
                        diskItem.Temperature = $"{tSensor.Value.Value:F0}°C";
                    }
                }
            }

            diskItem.Partitions = new List<PartitionData>();
            foreach (var partition in drive.PartitionList)
            {
                foreach (var volume in partition.VolumeList)
                {
                    diskItem.Partitions.Add(new PartitionData
                    {
                        DriveLetter = volume.Name,
                        TotalSpace = FormatBytes((long)volume.Size),
                        FreeSpace = FormatBytes((long)volume.FreeSpace)
                    });
                }
            }
            disks.Add(diskItem);
        }
    }
    catch { }
    return disks;
}

        #endregion

        #region Network Logic

        private static List<NetworkData> GetNetworkDetailed()
        {
            var nets = new List<NetworkData>();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && 
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();
                
                DateTime now = DateTime.Now;

                foreach (var ni in interfaces)
                {
                    try
                    {
                        var nd = new NetworkData 
                        { 
                            Name = ni.Name, 
                            AdapterName = ni.Description, 
                            MacAddress = ni.GetPhysicalAddress().ToString()
                        };

                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) nd.Type = "WiFi";
                        else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) nd.Type = "Ethernet";
                        else nd.Type = "Other";

                        var ipProps = ni.GetIPProperties();
                        var v4 = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                        if (v4 != null) nd.Ipv4 = v4.Address.ToString();
                        
                        if (string.IsNullOrEmpty(nd.Ipv4) || nd.Ipv4.StartsWith("169.254")) continue;

                        var stats = ni.GetIPStatistics();
                        long sent = stats.BytesSent;
                        long recv = stats.BytesReceived;

                        nd.BytesSent = FormatBytes(sent);
                        nd.BytesReceived = FormatBytes(recv);

                        if (_previousNetworkStats.ContainsKey(ni.Id))
                        {
                            var prev = _previousNetworkStats[ni.Id];
                            double sec = (now - prev.LastUpdateTime).TotalSeconds;
                            if (sec > 0)
                            {
                                double sendRate = (sent - prev.LastBytesSent) * 8.0 / sec;
                                double recvRate = (recv - prev.LastBytesReceived) * 8.0 / sec;
                                nd.SendSpeed = FormatBitsSpeed(sendRate);
                                nd.ReceiveSpeed = FormatBitsSpeed(recvRate);
                                nd.Speed = $"{nd.ReceiveSpeed} ↓ / {nd.SendSpeed} ↑";
                            }
                        }
                        else
                        {
                            nd.SendSpeed = "0 B/s";
                            nd.ReceiveSpeed = "0 B/s";
                            nd.Speed = "Calculating...";
                        }

                        _previousNetworkStats[ni.Id] = new NetworkStats 
                        { 
                            LastBytesSent = sent, 
                            LastBytesReceived = recv, 
                            LastUpdateTime = now 
                        };

                        nets.Add(nd);
                    }
                    catch { }
                }
            }
            catch { }
            return nets;
        }

        #endregion

        #region OS Info

       private static OsData GetOsInfo()
        {
            var os = new OsData();
            try
            {
                os.Name = RuntimeInformation.OSDescription;
                os.Architecture = RuntimeInformation.OSArchitecture.ToString();
                os.UserName = Environment.UserName;
                
                
                long tickCount = Environment.TickCount64;
                TimeSpan uptime = TimeSpan.FromMilliseconds(tickCount);
                DateTime bootTime = DateTime.Now - uptime;
                
                os.BootTime = bootTime.ToString("yyyy-MM-dd HH:mm");
            }
            catch { }
            return os;
        }

        #endregion

        #region Helpers

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double d = bytes;
            while (d >= 1024 && i < suffixes.Length - 1) { d /= 1024; i++; }
            return $"{d:F1} {suffixes[i]}";
        }

        private static string FormatBitsSpeed(double bitsPerSec)
        {
            if (bitsPerSec < 0) return "0 B/s";
            if (bitsPerSec < 1024) return $"{(int)bitsPerSec} bits/s";
            if (bitsPerSec < 1024 * 1024) return $"{(bitsPerSec / 1024):F1} Kbit/s";
            if (bitsPerSec < 1024 * 1024 * 1024) return $"{(bitsPerSec / 1048576):F1} Mbit/s";
            return $"{(bitsPerSec / 1073741824):F2} Gbit/s";
        }

        
public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();          
        foreach (IHardware sub in hardware.SubHardware)
            sub.Accept(this);       
    }

    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}

        #endregion
    }
}