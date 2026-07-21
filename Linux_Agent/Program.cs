﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SystemMonitor.Properties; // Для доступа к Settings.Default

namespace SystemMonitor
{
    class Program
    {
        private static ApiClient _apiClient;
        private static ComputerInfo _computerInfo;

        static async Task Main(string[] args)
        {
            // 1. Загружаем настройки
            var settings = Settings.Default;
            
            // Генерируем код подключения, если его еще нет
            if (string.IsNullOrWhiteSpace(settings.ConnectionCode))
            {
                settings.ConnectionCode = ConnectionCodeGenerator.GenerateCode();
                settings.Save(); 
            }

            // 2. Логика запуска в фоне (Linux)
            if (args.Length == 0 || args[0] != "--run")
            {
                // Если запустили без флага --run, создаем фоновый процесс через nohup
                string exePath = Environment.ProcessPath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"nohup '{exePath}' --run > /dev/null 2>&1 &\"",
                    UseShellExecute = false
                });
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=================================================");
                Console.WriteLine("✅ System Monitor успешно запущен в фоне!");
                Console.WriteLine($"🔑 ВАШ КОД ПОДКЛЮЧЕНИЯ: {settings.ConnectionCode}");
                Console.WriteLine("=================================================");
                Console.ResetColor();
                Console.WriteLine("Теперь вы можете закрыть этот терминал.");
                return;
            }

            // ==========================================================
            // РАБОЧАЯ ЧАСТЬ (Выполняется только в процессе с флагом --run)
            // ==========================================================

            
            ComputerMonitor.Initialize();
            
            _computerInfo = new ComputerInfo();
            _apiClient = new ApiClient();
            
            
            _apiClient.SetServerUrl(settings.ServerUrl);
            _computerInfo.ConnectionCode = settings.ConnectionCode;
            
            // Бесконечный цикл сбора и отправки данных
            while (true)
            {
                try
                {
                    // Собираем данные 
                    var systemData = await Task.Run(() => ComputerMonitor.GetSystemInfo());
                    
                    // Заполняем метаданные
                    systemData.ComputerId = _computerInfo.ConnectionCode;
                    systemData.ComputerName = Environment.MachineName;
                    systemData.Timestamp = DateTime.UtcNow;
                    systemData.ConfigHash = systemData.GetConfigHash();

                    
                    ServerCommand command = await _apiClient.SendDataAsync(systemData);
                    
                    // Если сервер прислал команду 
                    if (command != null)
                    {
                        var executor = new CommandExecutor(_apiClient, _computerInfo.ConnectionCode);
                        await executor.ExecuteAsync(command);
                        await _apiClient.ReportCommandExecutedAsync(command.Id);
                    }
                }
                catch (Exception ex)
                {
                    
                }

                // Пауза между итерациями (2 секунды)
                await Task.Delay(2000); 
            }
        }
    }
}