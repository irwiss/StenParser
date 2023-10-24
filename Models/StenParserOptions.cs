public class StenParserOptions
{
    public string SerialPortName { get; set; } = string.Empty;
    public List<int> BroadcastCodes { get; set; } = new();
    public List<int> AnswerCodes { get; set; } = new();
    public Dictionary<string, List<int>> CallGroups { get; set; }= new();
}
