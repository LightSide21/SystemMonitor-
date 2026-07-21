using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms; 

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
                        
                        Process.Start(new ProcessStartInfo("shutdown", "/s /f /t 0") { CreateNoWindow = true, UseShellExecute = false });
                        break;

                    case "REBOOT":
                        
                        Process.Start(new ProcessStartInfo("shutdown", "/r /f /t 0") { CreateNoWindow = true, UseShellExecute = false });
                        break;
                        
                    case "SLEEP":
                        
                        Application.SetSuspendState(PowerState.Suspend, false, false);
                        break;

                    case "LOCK":
                        
                        Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                        break;

                    case "KILL_PROCESS":
                        
                        if (int.TryParse(cmd.Payload, out int pid))
                        {
                            var processToKill = Process.GetProcessById(pid);
                            processToKill.Kill(); 
                        }
                        else if (!string.IsNullOrEmpty(cmd.Payload))
                        {
                            
                            foreach (var proc in Process.GetProcessesByName(cmd.Payload))
                            {
                                proc.Kill();
                            }
                        }
                        break;

                    case "GET_PROCESSES":
                        // Собираем запущенные процессы
                        var processList = GetActiveProcesses();
                        
                        await _apiClient.ReportProcessesAsync(_computerId, processList);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения команды: {ex.Message}");
            }
        }

        private List<ProcessItem> GetActiveProcesses()
        {
            var list = new List<ProcessItem>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    // Игнорируем системные процессы с PID 0 
                    if (p.Id == 0 || p.Id == 4) continue;

                    string currentStatus = "Работает";

                    try
                    {
                        if (!p.Responding)
                        {
                            currentStatus = "Зависло";
                        }
                    }
                    catch {  }

                    long memBytes = p.WorkingSet64;
                    double memMb = memBytes / 1048576.0;
                    string formattedMem = memMb >= 1024 
                        ? $"{(memMb / 1024.0):F2} GB" 
                        : $"{memMb:F1} MB";

                    list.Add(new ProcessItem 
                    { 
                        Id = p.Id, 
                        Name = p.ProcessName, 
                        MemoryBytes = memBytes,          
                        MemoryFormatted = formattedMem,  
                        Status = currentStatus 
                    });
                }
                catch {  }
            }

            // Сортируем по потреблению памяти 
            return list.OrderByDescending(p => p.MemoryBytes).Take(100).ToList();
        }
    }
}