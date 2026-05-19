using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SystemMonitor
{
    public class CommandExecutor
    {
        private ApiClient _apiClient;
        private string _computerId;

        public CommandExecutor(ApiClient apiClient, string computerId)
        {
            _apiClient = apiClient;
            _computerId = computerId;
        }

        public async Task ExecuteAsync(ServerCommand cmd)
        {
            try
            {
                switch (cmd.Action.ToUpper())
                {
                    case "SHUTDOWN":
                        Process.Start(new ProcessStartInfo("shutdown", "-h now") { CreateNoWindow = true });
                        break;

                    case "REBOOT":
                        Process.Start(new ProcessStartInfo("reboot") { CreateNoWindow = true });
                        break;
                        
                    case "SLEEP":
                        Process.Start(new ProcessStartInfo("systemctl", "suspend") { CreateNoWindow = true });
                        break;

                    case "GET_PROCESSES":
                        var processes = GetTopProcesses();
                        await _apiClient.ReportProcessesAsync(_computerId, processes);
                        break;

                    case "KILL_PROCESS":
                        if (int.TryParse(cmd.Payload, out int pid))
                        {
                            // SIGKILL сигнал в Linux
                            Process.Start(new ProcessStartInfo("kill", $"-9 {pid}") { CreateNoWindow = true });
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения {cmd.Action}: {ex.Message}");
            }
        }

        private List<ProcessItem> GetTopProcesses()
        {
            var list = new List<ProcessItem>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == 0) continue;

                    long memBytes = p.WorkingSet64;
                    double memMb = memBytes / 1048576.0;
                    string formattedMem = memMb >= 1024 ? $"{(memMb / 1024.0):F2} GB" : $"{memMb:F1} MB";

                    list.Add(new ProcessItem 
                    { 
                        Id = p.Id, 
                        Name = p.ProcessName, 
                        MemoryBytes = memBytes,          
                        MemoryFormatted = formattedMem,  
                        Status = "Работает" 
                    });
                }
                catch { }
            }
            return list.OrderByDescending(p => p.MemoryBytes).Take(100).ToList();
        }
    }
}