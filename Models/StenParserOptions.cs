namespace StenParser
{
    public class StenParserOptions
    {
        public string SerialPortName { get; set; } = "COM1";
        public TimeSpan SerialRetryWaitTime { get; set; } = TimeSpan.FromSeconds(10);
        public HashSet<int> BroadcastCodes { get; set; } = new();
        public HashSet<int> AnswerCodes { get; set; } = new();
        public HashSet<int> AlertCodes { get; set; } = new();
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        public string AliasesFilename { get; set; } = "./aliases-default.csv";
        public string AlertsLogFilename { get; set; } = "./alerts-log-default.txt";
        public Dictionary<string, HashSet<int>> CallGroups { get; set; } = new();
    }
}
