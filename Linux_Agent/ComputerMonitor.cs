using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SystemMonitor
{
    public static class ComputerMonitor
    {
        private static Dictionary<string, NetworkStats> _previousNetworkStats = new Dictionary<string, NetworkStats>();
        private static long _prevIdleTime = 0;
        private static long _prevTotalTime = 0;
        
        // Переменные для подсчета энергопотребления процессора (Intel RAPL)
        private static long _prevCpuEnergyUj = 0;
        private static DateTime _prevCpuEnergyTime = DateTime.MinValue;

        private class NetworkStats
        {
            public DateTime LastUpdateTime { get; set; }
            public long LastBytesSent { get; set; }
            public long LastBytesReceived { get; set; }
        }

        public static void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Запускаем умную автоустановку пакетов (lm-sensors, smartmontools)
                InstallDependenciesIfLinux();
                
                // Загружаем ядерные модули, включая msr для работы turbostat
                ExecBash("modprobe -q msr; modprobe -q coretemp; modprobe -q k10temp; modprobe -q drivetemp");
            }
        }

        public static void Close()
        {
            _previousNetworkStats.Clear();
        }

        public static SystemData GetSystemInfo()
        {
            return new SystemData
            {
                Timestamp = DateTime.UtcNow,
                ComputerName = Environment.MachineName,
                Os = GetOsInfo(),
                Cpu = GetCpuDetailed(),
                Gpus = GetGpuDetailed(), 
                Ram = GetRamDetailed(),
                Disks = GetDiskDetailed(),
                Networks = GetNetworkDetailed()
            };
        }

        private static OsData GetOsInfo()
        {
            return new OsData
            {
                Name = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                UserName = Environment.UserName,
                BootTime = (DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64)).ToString("yyyy-MM-dd HH:mm")
            };
        }

        // ==========================================
        // 1. ПРОЦЕССОР (Чтение напрямую из ядра + Turbostat)
        // ==========================================
        private static CpuData GetCpuDetailed()
        {
            var cpu = new CpuData
            {
                LogicalCores = Environment.ProcessorCount,
                PhysicalCores = Math.Max(1, Environment.ProcessorCount / 2),
                ProcessCount = Process.GetProcesses().Length,
                Name = "Unknown CPU",
                Temperature = "N/A",
                Power = "N/A",
                Load = GetCpuLoad(),
                CurrentFrequency = 0
            };

            try
            {
                // 1. Имя процессора
                if (File.Exists("/proc/cpuinfo"))
                {
                    var modelLine = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name"));
                    if (modelLine != null) cpu.Name = modelLine.Split(':')[1].Trim();
                }

                // 2. Частота процессора (в MHz)
                if (File.Exists("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"))
                {
                    if (int.TryParse(File.ReadAllText("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"), out int freqKhz))
                    {
                        cpu.CurrentFrequency = freqKhz / 1000;
                    }
                }

                // 3. Температура процессора (Ищем hwmon)
                double maxTemp = 0;
                if (Directory.Exists("/sys/class/hwmon"))
                {
                    foreach (var hwmon in Directory.GetDirectories("/sys/class/hwmon"))
                    {
                        string namePath = Path.Combine(hwmon, "name");
                        if (File.Exists(namePath))
                        {
                            string name = File.ReadAllText(namePath).Trim().ToLower();
                            if (name == "coretemp" || name == "k10temp" || name == "zenpower")
                            {
                                foreach (var tempFile in Directory.GetFiles(hwmon, "temp*_input"))
                                {
                                    if (double.TryParse(File.ReadAllText(tempFile).Trim(), out double t))
                                        maxTemp = Math.Max(maxTemp, t / 1000.0);
                                }
                            }
                        }
                    }
                }
                if (maxTemp > 0) cpu.Temperature = $"{maxTemp:F1}°C";

                // ==========================================
                // ПИТАНИЕ ПРОЦЕССОРА (Нативные методы)
                // ==========================================
                
                // Метод 1: Intel RAPL 
                string raplPath = "/sys/class/powercap/intel-rapl/intel-rapl:0/energy_uj";
                if (File.Exists(raplPath))
                {
                    if (long.TryParse(File.ReadAllText(raplPath).Trim(), out long currentEnergy))
                    {
                        DateTime now = DateTime.UtcNow;
                        if (_prevCpuEnergyUj > 0 && _prevCpuEnergyTime != DateTime.MinValue)
                        {
                            double seconds = (now - _prevCpuEnergyTime).TotalSeconds;
                            if (seconds > 0)
                            {
                                double deltaEnergy = currentEnergy - _prevCpuEnergyUj;
                                if (deltaEnergy >= 0) 
                                {
                                    double watts = (deltaEnergy / 1000000.0) / seconds;
                                    cpu.Power = $"{watts:F1} W";
                                }
                            }
                        }
                        _prevCpuEnergyUj = currentEnergy;
                        _prevCpuEnergyTime = now;
                    }
                }

                // Метод 2: Hwmon (Для AMD с установленным zenpower или старых ПК)
                if (cpu.Power == "N/A" && Directory.Exists("/sys/class/hwmon"))
                {
                    foreach (var hwmon in Directory.GetDirectories("/sys/class/hwmon"))
                    {
                        string powerPath = Path.Combine(hwmon, "power1_input");
                        if (!File.Exists(powerPath)) powerPath = Path.Combine(hwmon, "power1_average");
                        
                        if (File.Exists(powerPath) && double.TryParse(File.ReadAllText(powerPath).Trim(), out double p))
                        {
                            cpu.Power = $"{p / 1000000.0:F1} W";
                            break;
                        }
                    }
                }

                // ==========================================
                // ЧЕРЕЗ TURBOSTAT
                // ==========================================
                if (cpu.Power == "N/A" || cpu.Temperature == "N/A")
                {
                    string turboCmd = "/usr/bin/turbostat --quiet --Summary --num_iterations 1 --show PkgWatt,CoreTmp,Bzy_MHz 2>&1";
                    string output = ExecBash(turboCmd);
                    
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        var headers = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        var values = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                        // Ватты
                        int wattIdx = headers.IndexOf("PkgWatt");
                        if (wattIdx >= 0 && wattIdx < values.Count)
                        {
                            string wStr = values[wattIdx].Replace(",", ".");
                            if (double.TryParse(wStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double w))
                                cpu.Power = $"{w:F1} W";
                        }

                        // Температура
                        int tempIdx = headers.IndexOf("CoreTmp");
                        if (tempIdx >= 0 && tempIdx < values.Count && cpu.Temperature == "N/A")
                        {
                            if (double.TryParse(values[tempIdx], out double t))
                                cpu.Temperature = $"{t:F0}°C";
                        }
                        
                        // Частота
                        int freqIdx = headers.IndexOf("Bzy_MHz");
                        if (freqIdx >= 0 && freqIdx < values.Count && cpu.CurrentFrequency == 0)
                        {
                            if (int.TryParse(values[freqIdx], out int f))
                                cpu.CurrentFrequency = f;
                        }
                    }
                }
            }
            catch { }

            return cpu;
        }

        private static int GetCpuLoad()
        {
            try
            {
                var lines = File.ReadAllLines("/proc/stat");
                var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                long user = long.Parse(parts[1]);
                long nice = long.Parse(parts[2]);
                long system = long.Parse(parts[3]);
                long idle = long.Parse(parts[4]);
                long iowait = long.Parse(parts[5]);
                long irq = long.Parse(parts[6]);
                long softirq = long.Parse(parts[7]);

                long currentIdle = idle + iowait;
                long currentTotal = user + nice + system + idle + iowait + irq + softirq;

                long totalDiff = currentTotal - _prevTotalTime;
                long idleDiff = currentIdle - _prevIdleTime;

                _prevTotalTime = currentTotal;
                _prevIdleTime = currentIdle;

                if (totalDiff == 0) return 0;
                return (int)((totalDiff - idleDiff) * 100.0 / totalDiff);
            }
            catch { return 0; }
        }

        // ==========================================
        // 2. ВИДЕОКАРТА (Универсально: Nvidia-smi + DRM для AMD/Intel)
        // ==========================================
        private static List<GpuData> GetGpuDetailed()
        {
            var gpus = new List<GpuData>();
            bool hasNvidia = false;

            //  Ищем NVIDIA через nvidia-smi
            try
            {
                string output = ExecBash("/usr/bin/nvidia-smi --query-gpu=name,memory.total,memory.used,utilization.gpu,temperature.gpu,power.draw --format=csv,noheader,nounits 2>/dev/null");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    hasNvidia = true; 
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                        if (parts.Length >= 6)
                        {
                            gpus.Add(new GpuData
                            {
                                Name = parts[0],                 
                                Vendor = "Nvidia",
                                Type = "GPU",
                                Memory = $"{parts[2]} / {parts[1]} MB", 
                                Utilization = $"{parts[3]}%",    
                                Temperature = $"{parts[4]}°C",   
                                Power = $"{parts[5]} W"          
                            });
                        }
                    }
                }
            }
            catch { }

            //  Ищем AMD и Intel через файлы ядра Linux (sysfs)
            try
            {
                if (Directory.Exists("/sys/class/drm"))
                {
                    foreach (var dir in Directory.GetDirectories("/sys/class/drm"))
                    {
                        var name = new DirectoryInfo(dir).Name;
                        if (name.StartsWith("card") && !name.Contains("-"))
                        {
                            string ueventPath = Path.Combine(dir, "device/uevent");
                            if (!File.Exists(ueventPath)) continue;

                            string uevent = File.ReadAllText(ueventPath).ToLower();
                            
                            // Если это драйвер Nvidia, но мы ее уже нашли через nvidia-smi - пропускаем
                            if (uevent.Contains("driver=nvidia") && hasNvidia) continue;

                            if (uevent.Contains("driver=amdgpu") || uevent.Contains("driver=i915") || uevent.Contains("driver=xe"))
                            {
                                var gpu = new GpuData { Type = "GPU", Memory = "N/A", Utilization = "N/A", Temperature = "N/A", Power = "N/A" };

                                if (uevent.Contains("driver=amdgpu")) { gpu.Vendor = "AMD"; gpu.Name = "AMD Radeon GPU"; }
                                else { gpu.Vendor = "Intel"; gpu.Name = "Intel Graphics"; }

                                string hwmonDir = Path.Combine(dir, "device/hwmon");
                                if (Directory.Exists(hwmonDir))
                                {
                                    foreach (var hwmon in Directory.GetDirectories(hwmonDir))
                                    {
                                        string tempPath = Path.Combine(hwmon, "temp1_input");
                                        if (File.Exists(tempPath) && double.TryParse(File.ReadAllText(tempPath).Trim(), out double t))
                                            gpu.Temperature = $"{t / 1000.0:F1}°C";
                                            
                                        string powerPath = File.Exists(Path.Combine(hwmon, "power1_average")) 
                                            ? Path.Combine(hwmon, "power1_average") 
                                            : Path.Combine(hwmon, "power1_input");
                                            
                                        if (File.Exists(powerPath) && double.TryParse(File.ReadAllText(powerPath).Trim(), out double p))
                                            gpu.Power = $"{p / 1000000.0:F1} W"; 
                                    }
                                }

                                string busyPath = Path.Combine(dir, "device/gpu_busy_percent");
                                if (File.Exists(busyPath) && int.TryParse(File.ReadAllText(busyPath).Trim(), out int busy))
                                    gpu.Utilization = $"{busy}%";

                                string memUsedPath = Path.Combine(dir, "device/mem_info_vram_used");
                                string memTotalPath = Path.Combine(dir, "device/mem_info_vram_total");
                                if (File.Exists(memUsedPath) && File.Exists(memTotalPath))
                                {
                                    if (long.TryParse(File.ReadAllText(memUsedPath).Trim(), out long used) &&
                                        long.TryParse(File.ReadAllText(memTotalPath).Trim(), out long total))
                                    {
                                        gpu.Memory = $"{(used / 1048576)} / {(total / 1048576)} MB";
                                    }
                                }

                                gpus.Add(gpu);
                            }
                        }
                    }
                }
            }
            catch { }

            if (gpus.Count == 0)
            {
                gpus.Add(new GpuData { Name = "Generic / Unknown Display", Vendor = "Unknown", Type = "vGPU", Memory = "N/A", Utilization = "N/A", Temperature = "N/A", Power = "N/A" });
            }

            return gpus;
        }

        // ==========================================
        // 3. ОПЕРАТИВНАЯ ПАМЯТЬ
        // ==========================================
        private static RamData GetRamDetailed()
        {
            var ram = new RamData { Total = "0 GB", Used = "0 GB", Available = "0 GB", Load = "0%" };
            try
            {
                if (File.Exists("/proc/meminfo"))
                {
                    var lines = File.ReadAllLines("/proc/meminfo");
                    double totalKb = 0, availKb = 0;

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemTotal:")) totalKb = double.Parse(line.Replace("MemTotal:", "").Replace("kB", "").Trim());
                        if (line.StartsWith("MemAvailable:")) availKb = double.Parse(line.Replace("MemAvailable:", "").Replace("kB", "").Trim());
                    }

                    if (totalKb > 0)
                    {
                        double usedKb = totalKb - availKb;
                        ram.Total = $"{(totalKb / 1024 / 1024):F1} GB";
                        ram.Available = $"{(availKb / 1024 / 1024):F1} GB";
                        ram.Used = $"{(usedKb / 1024 / 1024):F1} GB";
                        ram.Load = $"{(usedKb / totalKb * 100):F1}%";
                    }
                }
            }
            catch { }
            return ram;
        }

        // ==========================================
        // 4. НАКОПИТЕЛИ
        // ==========================================
        private static List<DiskData> GetDiskDetailed()
        {
            var disks = new List<DiskData>();

            if (Directory.Exists("/sys/block"))
            {
                foreach (var dir in Directory.GetDirectories("/sys/block"))
                {
                    var name = new DirectoryInfo(dir).Name;
                    if (name.StartsWith("loop") || name.StartsWith("ram") || name.StartsWith("sr")) continue;

                    string modelPath = Path.Combine(dir, "device/model");
                    string sizePath = Path.Combine(dir, "size");

                    if (File.Exists(sizePath))
                    {
                        string modelName = File.Exists(modelPath) ? File.ReadAllText(modelPath).Trim() : name;
                        var diskItem = new DiskData
                        {
                            Model = $"{name} ({modelName})",
                            Temperature = "N/A",
                            Partitions = new List<PartitionData>()
                        };

                        if (long.TryParse(File.ReadAllText(sizePath).Trim(), out long sectors))
                        {
                            diskItem.TotalCapacity = FormatBytes(sectors * 512);
                        }

                        // ТЕМПЕРАТУРА НАКОПИТЕЛЯ
                        
                        // 1. Нативный способ 
                        string hwmonDir = Path.Combine(dir, "device/hwmon");
                        if (Directory.Exists(hwmonDir))
                        {
                            var hwmons = Directory.GetDirectories(hwmonDir);
                            if (hwmons.Length > 0)
                            {
                                string tempPath = Path.Combine(hwmons[0], "temp1_input");
                                if (File.Exists(tempPath) && double.TryParse(File.ReadAllText(tempPath).Trim(), out double t))
                                    diskItem.Temperature = $"{t / 1000.0:F0}°C";
                            }
                        }

                        // 2. Нативный способ СПЕЦИАЛЬНО ДЛЯ NVMe 
                        if (diskItem.Temperature == "N/A" && name.StartsWith("nvme"))
                        {
                            // Имя обычно выглядит как nvme0n1. Нам нужно отрезать кусок, чтобы получить nvme0
                            string nvmeBase = name.Split('n')[0]; 
                            string nvmeHwmon = $"/sys/class/nvme/{nvmeBase}/device/hwmon";
                            if (Directory.Exists(nvmeHwmon))
                            {
                                var hwmons = Directory.GetDirectories(nvmeHwmon);
                                if (hwmons.Length > 0)
                                {
                                    // У NVMe часто 2 или 3 датчика. temp1_input - контроллер, temp2_input - чипы памяти (Composite)
                                    string tempPath = Path.Combine(hwmons[0], "temp1_input");
                                    if (!File.Exists(tempPath)) tempPath = Path.Combine(hwmons[0], "temp2_input"); 
                                    
                                    if (File.Exists(tempPath) && double.TryParse(File.ReadAllText(tempPath).Trim(), out double t))
                                        diskItem.Temperature = $"{t / 1000.0:F0}°C";
                                }
                            }
                        }

                        // 3.  smartctl (если нативных файлов нет)
                        if (diskItem.Temperature == "N/A")
                        {
                            string smartCmd = $"smartctl -a /dev/{name} 2>/dev/null | grep -i temp";
                            string output = ExecBash(smartCmd);
                            
                            var lines = output.Split('\n');
                            foreach (var line in lines)
                            {
                                // Игнорируем строки с критическими порогами (Critical Threshold), нам нужна только текущая температура
                                if (line.Contains("Threshold", StringComparison.OrdinalIgnoreCase)) continue;
                                
                                // Для NVMe 
                                if (line.Contains("Temperature:") && line.Contains("Celsius"))
                                {
                                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 2 && double.TryParse(parts[1], out double temp))
                                        diskItem.Temperature = $"{temp}°C";
                                }
                                // Для SATA HDD/SSD 
                                else if (line.Contains("Temperature_Celsius") || line.Contains("Airflow_Temperature_Cel"))
                                {
                                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length > 0 && double.TryParse(parts.Last(), out double temp))
                                        diskItem.Temperature = $"{temp}°C";
                                }
                            }
                        }

                        foreach (var drive in DriveInfo.GetDrives())
                        {
                            try
                            {
                                if (drive.DriveType != DriveType.Fixed) continue;
                                string format = drive.DriveFormat.ToLower();
                                if (format == "squashfs" || format == "tmpfs" || format == "overlay" || format == "devtmpfs") continue;

                                diskItem.Partitions.Add(new PartitionData
                                {
                                    DriveLetter = drive.Name,
                                    TotalSpace = FormatBytes(drive.TotalSize),
                                    FreeSpace = FormatBytes(drive.AvailableFreeSpace)
                                });
                            }
                            catch { }
                        }

                        disks.Add(diskItem);
                    }
                }
            }

            return disks;
        }

        // ==========================================
        // 5. СЕТЬ
        // ==========================================
        private static List<NetworkData> GetNetworkDetailed()
        {
            var nets = new List<NetworkData>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();

            DateTime now = DateTime.Now;

            foreach (var ni in interfaces)
            {
                try
                {
                    var nd = new NetworkData { Name = ni.Name, AdapterName = ni.Description, MacAddress = ni.GetPhysicalAddress().ToString(), Type = ni.NetworkInterfaceType.ToString() };
                    var v4 = ni.GetIPProperties().UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (v4 != null) nd.Ipv4 = v4.Address.ToString();

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
                            nd.SendSpeed = FormatBitsSpeed((sent - prev.LastBytesSent) * 8.0 / sec);
                            nd.ReceiveSpeed = FormatBitsSpeed((recv - prev.LastBytesReceived) * 8.0 / sec);
                            nd.Speed = $"{nd.ReceiveSpeed} ↓ / {nd.SendSpeed} ↑";
                        }
                    }
                    else { nd.SendSpeed = "0 B/s"; nd.ReceiveSpeed = "0 B/s"; nd.Speed = "Calculating..."; }

                    _previousNetworkStats[ni.Id] = new NetworkStats { LastBytesSent = sent, LastBytesReceived = recv, LastUpdateTime = now };
                    nets.Add(nd);
                }
                catch { }
            }
            return nets;
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ==========================================
        private static void InstallDependenciesIfLinux()
        {
            try
            {
                bool sensorsExists = File.Exists("/usr/bin/sensors") || File.Exists("/usr/sbin/sensors");
                bool smartctlExists = File.Exists("/usr/bin/smartctl") || File.Exists("/usr/sbin/smartctl");
                
                if (sensorsExists && smartctlExists) return;
                
                Console.WriteLine("[Setup] Установка зависимостей (lm-sensors, smartmontools)...");
                
                string cmd = "";
                if (File.Exists("/usr/bin/apt") || File.Exists("/usr/bin/apt-get"))
                    cmd = "apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y lm-sensors smartmontools linux-tools-common linux-tools-generic";
                else if (File.Exists("/usr/bin/pacman"))
                    cmd = "pacman -Sy --noconfirm lm_sensors smartmontools";
                else if (File.Exists("/usr/bin/dnf"))
                    cmd = "dnf install -y lm_sensors smartmontools";
                else if (File.Exists("/usr/bin/yum"))
                    cmd = "yum install -y lm_sensors smartmontools";

                if (!string.IsNullOrEmpty(cmd))
                {
                    var proc = Process.Start(new ProcessStartInfo 
                    { 
                        FileName = "bash", 
                        Arguments = $"-c \"{cmd} && yes | sensors-detect --auto\"", 
                        UseShellExecute = false, 
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    proc.WaitForExit();
                    Console.WriteLine("[Setup] Зависимости установлены!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Setup] Ошибка установки: {ex.Message}");
            }
        }

        private static string ExecBash(string cmd)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{cmd}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);
                return output.Trim();
            }
            catch { return ""; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0; double d = bytes;
            while (d >= 1024 && i < suffixes.Length - 1) { d /= 1024; i++; }
            return $"{d:F1} {suffixes[i]}";
        }

        private static string FormatBitsSpeed(double bitsPerSec)
        {
            if (bitsPerSec < 0) return "0 B/s";
            if (bitsPerSec < 1024) return $"{(int)bitsPerSec} bits/s";
            if (bitsPerSec < 1048576) return $"{(bitsPerSec / 1024):F1} Kbit/s";
            if (bitsPerSec < 1073741824) return $"{(bitsPerSec / 1048576):F1} Mbit/s";
            return $"{(bitsPerSec / 1073741824):F2} Gbit/s";
        }
    }
}