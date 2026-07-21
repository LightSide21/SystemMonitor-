namespace SystemMonitor
{
    public class ProcessItem
    {
        public int Id { get; set; }          // PID процесса
        public string Name { get; set; }     // Имя
        public long MemoryBytes { get; set; } // Память
        public string MemoryFormatted { get; set; }
        public string Status { get; set; }
    }
}