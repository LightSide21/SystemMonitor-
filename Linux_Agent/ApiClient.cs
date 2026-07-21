using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SystemMonitor
{
    public class ApiClient
    {
        private HttpClient _httpClient;
        private string _serverUrl;
        
        public ApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }
        
        public void SetServerUrl(string url)
        {
            _serverUrl = url.TrimEnd('/');
        }
        
        public async Task<bool> HandshakeAsync(string computerId, string configHash)
        {
            try
            {
                var payload = new { ComputerId = computerId, ConfigHash = configHash };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_serverUrl}/api/agent/handshake", content);
                
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ServerCommand> SendDataAsync(SystemData data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_serverUrl}/ingest", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(responseString))
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        return JsonSerializer.Deserialize<ServerCommand>(responseString, options);
                    }
                }
                else
                {
                    // === КРАСИВАЯ РАСШИФРОВКА HTTP-КОДОВ ===
                    int statusCode = (int)response.StatusCode;
                    string errorMessage = $"Код {statusCode} ({response.StatusCode})";

                    switch (statusCode)
                    {
                        case 400: errorMessage += " - Неверный запрос. Сервер не смог прочитать JSON."; break;
                        case 401: errorMessage += " - Ошибка авторизации. ПК не привязан или токен устарел."; break;
                        case 403: errorMessage += " - Доступ запрещен. У агента нет прав на это действие."; break;
                        case 404: errorMessage += " - Не найдено. Проверьте правильность URL сервера."; break;
                        case 408: errorMessage += " - Истекло время ожидания запроса."; break;
                        case 429: errorMessage += " - Слишком много запросов (DDoS защита). Сервер просит подождать."; break;
                        case 500: errorMessage += " - Внутренняя ошибка сервера. Произошел сбой в Java-коде."; break;
                        case 502: errorMessage += " - Плохой шлюз. Ошибка на стороне Nginx/Proxy."; break;
                        case 503: errorMessage += " - Сервис недоступен. Сервер перегружен или выключен."; break;
                    }

                    // Передаем эту ошибку в MainForm.cs
                    throw new Exception(errorMessage);
                }
                return null;
            }
            catch (HttpRequestException)
            {
                
                throw new Exception("Сервер физически выключен, порт закрыт или неверный IP-адрес.");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task ReportCommandExecutedAsync(long commandId)
        {
            try
            {
                await _httpClient.PostAsync($"{_serverUrl}/ingest/command/{commandId}/status?status=EXECUTED", null);
            }
            catch { }
        }

        public async Task ReportProcessesAsync(string computerId, System.Collections.Generic.List<ProcessItem> processes)
        {
            try
            {
                var json = JsonSerializer.Serialize(processes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                await _httpClient.PostAsync($"{_serverUrl}/ingest/processes/{computerId}", content);
            }
            catch { }
        }
    }
}