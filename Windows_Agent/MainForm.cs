using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SystemMonitor
{
    public partial class MainForm : Form
    {
        private bool _isUpdating = false;
        private Timer _updateTimer;
        private ApiClient _apiClient;
        private ComputerInfo _computerInfo;
        
        // UI элементы
        private Label _lblStatus;
        private Label _lblConnectionCode;
        private Button _btnStartStop;
        private RichTextBox _txtLog;
        private RichTextBox _txtData;
        
        private bool _isMonitoring = false;
        
        public MainForm()
        {
            InitializeUI();
            
            // Инициализируем информацию о компьютере
            _computerInfo = new ComputerInfo();
            
            // Инициализируем API клиент
            _apiClient = new ApiClient();
            
            // Загружаем настройки 
            LoadSettings();
            
            // Показываем код подключения
            ShowConnectionCode();
        }
        
        private void InitializeUI()
        {
            this.Text = "System Monitor";
            this.Size = new Size(600, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Панель статуса
            _lblStatus = new Label
            {
                Text = "Статус: Остановлено",
                Location = new Point(10, 10),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            
            // Код подключения
            _lblConnectionCode = new Label
            {
                Text = "Код подключения: --- ---",
                Location = new Point(10, 40),
                Size = new Size(300, 30),
                Font = new Font("Arial", 14, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            
            // Кнопка старт/стоп 
            _btnStartStop = new Button
            {
                Text = "Запустить мониторинг",
                Location = new Point(10, 80), 
                Size = new Size(150, 40),
                BackColor = Color.LightGreen,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _btnStartStop.Click += BtnStartStop_Click;
            
            // Текстовое поле для данных 
            _txtData = new RichTextBox
            {
                Location = new Point(10, 130), 
                Size = new Size(560, 230), 
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BackColor = Color.White,
                ForeColor = Color.Black,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // Лог
            _txtLog = new RichTextBox
            {
                Location = new Point(10, 370),
                Size = new Size(560, 150),
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            
            // Добавляем элементы на форму
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_lblConnectionCode);
            this.Controls.Add(_btnStartStop);
            this.Controls.Add(_txtData);
            this.Controls.Add(_txtLog);
        }
        
        private void LoadSettings()
        {
            try
            {
                // Скрыто читаем URL из файла настроек приложения
                string serverUrl = Properties.Settings.Default.ServerUrl;
                if (string.IsNullOrWhiteSpace(serverUrl)) 
                {
                    serverUrl = "http://localhost:8080";
                }
                _apiClient.SetServerUrl(serverUrl);
            }
            catch
            {
                _apiClient.SetServerUrl("http://localhost:8080");
            }
        }
        
        private void ShowConnectionCode()
        {
            // Пытаемся взять код из настроек
            string savedCode = Properties.Settings.Default.ConnectionCode;

            // Проверяем, не пустой ли он
            if (!string.IsNullOrWhiteSpace(savedCode))
            {
                _computerInfo.ConnectionCode = savedCode;
                LogMessage($"[Settings] Загружен сохраненный ID: {savedCode}");
            }
            else
            {
                //  Если пусто — генерируем новый
                string newCode = ConnectionCodeGenerator.GenerateCode();
                _computerInfo.ConnectionCode = newCode;
                
                //  Сохраняем в настройки
                Properties.Settings.Default.ConnectionCode = newCode;
                Properties.Settings.Default.Save();
                
                LogMessage($"[Settings] Сгенерирован и сохранен НОВЫЙ ID: {newCode}");
            }

            _lblConnectionCode.Text = $"Код подключения: {_computerInfo.ConnectionCode}";
        }
        
        private async void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!_isMonitoring)
            {
                await StartMonitoringAsync();
            }
            else
            {
                StopMonitoring();
            }
        }

        private async Task StartMonitoringAsync()
        {
            try
            {
                _btnStartStop.Enabled = false;
                _btnStartStop.Text = "Инициализация...";
                LogMessage("Инициализация оборудования (может занять время)...");

                LogMessage("1. Начинаем опрос железа (LHM)..."); 
                await Task.Run(() => ComputerMonitor.Initialize());
                LogMessage("2. Опрос железа (LHM) завершен!");
                
                _updateTimer = new Timer();
                _updateTimer.Interval = 2000; 
                _updateTimer.Tick += UpdateTimer_Tick;
                _updateTimer.Start();
                
                _isMonitoring = true;
                _btnStartStop.Text = "Остановить";
                _btnStartStop.BackColor = Color.LightCoral;
                _lblStatus.Text = "Статус: Работает";
                _lblStatus.ForeColor = Color.Green;
                
                LogMessage("Отправка данных запущена");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка запуска: {ex.Message}");
            }
            finally
            {
                _btnStartStop.Enabled = true;
            }
        }
        
        private void StopMonitoring()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Dispose();
                _updateTimer = null;
            }
            
            ComputerMonitor.Close();
            
            _isMonitoring = false;
            _btnStartStop.Text = "Запустить мониторинг";
            _btnStartStop.BackColor = Color.LightGreen;
            _lblStatus.Text = "Статус: Остановлено";
            _lblStatus.ForeColor = Color.Red;
            
            LogMessage("Мониторинг остановлен");
        }
        
        private async void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Выносим тяжелый сбор метрик в фоновый поток 
                
                var systemData = await Task.Run(() => ComputerMonitor.GetSystemInfo());
                
                
                systemData.ComputerId = _computerInfo.ConnectionCode;
                systemData.ComputerName = Environment.MachineName;
                systemData.Timestamp = DateTime.UtcNow;
                systemData.ConfigHash = systemData.GetConfigHash(); 

                //  Отправляем метрики и ждем команду 
                ServerCommand command = await _apiClient.SendDataAsync(systemData);
                
                //  Выполнение команд 
                if (command != null)
                {
                    LogMessage($"[СЕРВЕР] Получена команда: {command.Action} {command.Payload}");
                    
                    
                    var executor = new CommandExecutor(_apiClient, _computerInfo.ConnectionCode);
                    await executor.ExecuteAsync(command); // Выполняем асинхронно
                    
                    await _apiClient.ReportCommandExecutedAsync(command.Id);
                    LogMessage($"[СЕРВЕР] Команда {command.Action} выполнена.");
                }
                
                //  Обновляем текстовое поле в интерфейсе
                UpdateDataDisplay(systemData);
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
            }
        }

        private void UpdateDataDisplay(SystemData data)
        {
            if (_txtData.InvokeRequired)
            {
                _txtData.Invoke(new Action(() => UpdateDataDisplay(data)));
                return;
            }

            var sb = new System.Text.StringBuilder();
            
            // --- HEADER ---
            sb.AppendLine($"Компьютер: {data.ComputerName} (ID: {data.ComputerId})");
            sb.AppendLine($"ОС: {data.Os.Name} | Uptime: {data.Os.BootTime}");
            sb.AppendLine($"Время: {data.Timestamp:yyyy-MM-dd HH:mm:ss}");
            
            // --- CPU ---
            sb.AppendLine("\n--- CPU ---");
            if (data.Cpu != null)
            {
                sb.AppendLine($"Модель: {data.Cpu.Name}");
                sb.AppendLine($"Ядра: {data.Cpu.PhysicalCores} физ. / {data.Cpu.LogicalCores} лог.");
                
                
                double freqGhz = data.Cpu.CurrentFrequency / 1000.0;
                sb.AppendLine($"Частота: {freqGhz:F2} GHz, Загрузка: {data.Cpu.Load}%");
                sb.AppendLine($"Температура: {data.Cpu.Temperature}"); // Уже строка с "°C"
                sb.AppendLine($"  Потребление: {data.Cpu.Power}");
            }
            else sb.AppendLine("CPU данные недоступны");

            // --- RAM ---
            sb.AppendLine("\n--- RAM ---");
            if (data.Ram != null)
            {
                
                sb.AppendLine($"Всего: {data.Ram.Total}, Использовано: {data.Ram.Used}");
                sb.AppendLine($"Доступно: {data.Ram.Available}, Загрузка: {data.Ram.Load}"); 
            }
            else sb.AppendLine("RAM данные недоступны");

            // --- GPU ---
            sb.AppendLine("\n--- GPU ---");
            if (data.Gpus != null && data.Gpus.Count > 0)
            {
                foreach (var g in data.Gpus)
                {
                    sb.AppendLine($"- {g.Name} ({g.Type})");
                    
                    sb.AppendLine($"  Память: {g.Memory}, Загрузка: {g.Utilization}");
                    sb.AppendLine($"  Температура: {g.Temperature}");
                    sb.AppendLine($"  Потребление: {g.Power}");
                }
            }
            else sb.AppendLine("Нет данных GPU");

            // --- DISKS ---
            sb.AppendLine("\n--- DISKS ---");
            if (data.Disks != null && data.Disks.Count > 0)
            {
                foreach (var d in data.Disks)
                {
                    
                    sb.AppendLine($"Диск: {d.Model} ({d.TotalCapacity}) Temp: {d.Temperature}");
                    
                    
                    if (d.Partitions.Count > 0)
                    {
                        foreach(var p in d.Partitions)
                        {
                             sb.AppendLine($"  [{p.DriveLetter}] Свободно: {p.FreeSpace} из {p.TotalSpace}");
                        }
                    }
                }
            }
            else sb.AppendLine("Нет данных о дисках");

            // --- NETWORK ---
            sb.AppendLine("\n--- NETWORK ---");
            if (data.Networks != null && data.Networks.Count > 0)
            {
                foreach (var n in data.Networks)
                {
                    sb.AppendLine($"{n.Name} ({n.Type})");
                    sb.AppendLine($"  IP: {n.Ipv4} | MAC: {n.MacAddress}");
                    // Скорости теперь строки
                    sb.AppendLine($"  DL: {n.ReceiveSpeed} | UL: {n.SendSpeed}");
                    sb.AppendLine($"  Трафик (сессия): ↓ {n.BytesReceived} / ↑ {n.BytesSent}");
                }
            }
            else sb.AppendLine("Активные сетевые адаптеры не найдены");

            _txtData.Text = sb.ToString();
        }
        
        private void LogMessage(string message)
        {
            if (_txtLog.InvokeRequired)
            {
                _txtLog.Invoke(new Action(() => LogMessage(message)));
                return;
            }
            
            // Ограничиваем размер лога
            if (_txtLog.TextLength > 10000)
            {
                _txtLog.Clear();
            }
            
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _txtLog.ScrollToCaret();
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopMonitoring();
            base.OnFormClosing(e);
        }
    }
}