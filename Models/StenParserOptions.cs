namespace StenParser
{
    public class StenParserOptions
    {
        public string SerialPortName { get; set; } = string.Empty;
        public HashSet<int> BroadcastCodes { get; set; } = new();
        public HashSet<int> AnswerCodes { get; set; } = new();
        public Dictionary<string, HashSet<int>> CallGroups { get; set; } = new();
    }
}
