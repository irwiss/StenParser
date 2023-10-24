namespace StenParser
{
    public class StenParserOptions
    {
        public string SerialPortName { get; set; } = string.Empty;
        public TimeSpan SerialRetryWaitTime { get; set; } = TimeSpan.FromSeconds(10);
        public HashSet<int> BroadcastCodes { get; set; } = new();
        public HashSet<int> AnswerCodes { get; set; } = new();
        public HashSet<int> AlertCodes { get; set; } = new();
        public string AliasesFilename { get; set; } = string.Empty;
        public string AlertsLogFilename { get; set; } = string.Empty;
        public Dictionary<string, HashSet<int>> CallGroups { get; set; } = new();
    }
}
