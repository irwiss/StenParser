using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Primitives;

public class ParserService
{
    public event Action? OnParserUpdated;

    public int CurrentGroup { get; private set; }
    public Dictionary<string, int[]> CallGroups { get; private set; }= new();
    public Dictionary<int, string> NumberAliases { get; } = new();
    public Dictionary<int, DateTimeOffset> Answered { get; } = new();
    public HashSet<string> AnswerCodes { get; } = new();
    public HashSet<string> BroadcastCodes { get; } = new();
    public List<string> RecentInputs { get; } = new();

    private readonly ILogger<ParserService> _logger;
    private readonly IConfiguration _configuration;
    
    private DateTimeOffset lastBroadcastTime = DateTimeOffset.Now;
    private int lastBroadcastNum = 0;

    public ParserService(ILogger<ParserService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        if (File.Exists("aliases.csv"))
        {
            using var reader = new StreamReader("aliases.csv");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Encoding = System.Text.Encoding.UTF8,
                HasHeaderRecord = false,
            };
            using var csv = new CsvReader(reader, config);
            NumberAliases = csv.GetRecords<NumberAliasModel>()
                .ToDictionary(x => x.Number, x => x.Alias);
        } else
        {
            logger.LogWarning("Couldn't find aliases.csv file to load number aliases from");
        }

        configuration.GetReloadToken().RegisterChangeCallback(state =>
        {
            ReloadGroups();
        }, null);
        ReloadGroups();

        ParseNumbersFromConfiguration(BroadcastCodes, "Broadcasts");
        ParseNumbersFromConfiguration(AnswerCodes, "AnswerCodes");

        // logger.LogInformation("Aliases: {NumberAliases}", NumberAliases);
        logger.LogInformation("Listening for broadcast numbers: {BroadcastCodes}", BroadcastCodes);
        logger.LogInformation("Listening for expected answer numbers: {AnswerCodes}", AnswerCodes);

        ChangeToken.OnChange(() => _configuration.GetReloadToken(), ReloadGroups);
        ReloadGroups();
    }

    private void ReloadGroups()
    {
        CallGroups = _configuration.GetSection("CallGroups").Get<Dictionary<string, int[]>>() ?? new Dictionary<string, int[]>();
        OnParserUpdated?.Invoke();
        _logger.LogWarning("Configuration reload token triggered.");
    }

    private void ParseNumbersFromConfiguration(HashSet<string> destination, string section)
    {
        int[]? nums = _configuration.GetSection(section).Get<int[]>();
        foreach (int num in nums ?? Array.Empty<int>())
        {
            destination.Add(num.ToString().PadLeft(4, 'F'));
        }
    }

    public void ParseLine(string line)
    {
        RecentInputs.Insert(0, line);
        if (RecentInputs.Count > 200)
        {
            RecentInputs.RemoveAt(RecentInputs.Count - 1);
        }
        OnParserUpdated?.Invoke();
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            _logger.LogDebug("Line '{line}' can't be split to 5 parts via whitespace", line);
            return; // ignore
        }
        _logger.LogInformation("Parse Input: '{line}'", line);
        if (AnswerCodes.Contains(parts[2]) && parts[0].StartsWith("05"))
        {
            if (!int.TryParse(parts[2].Replace("F", ""), out int target))
            {
                _logger.LogWarning("Couldn't parse target from '{line}'", line);
            }
            else
            {
                if(!int.TryParse(parts[1].Replace("F", ""), out int from))
                {
                    _logger.LogWarning("Couldn't parse caller from '{line}'", line);
                }
                else
                {
                    if (Answered.TryAdd(from, DateTimeOffset.UtcNow))
                    {
                        OnParserUpdated?.Invoke();
                        SaveCurrentState();
                        _logger.LogInformation("Call from '{from}' to '{to}'", from, target);
                    }
                    else
                    {
                        _logger.LogWarning("Duplicate call from '{from}' to '{to}'", from, target);
                    }
                }
            }
        }
        if (BroadcastCodes.Contains(parts[2]))
        {
            if (!int.TryParse(parts[2].Replace("F", ""), out int target))
            {
                _logger.LogWarning("Couldn't parse broadcast from '{line}'", line);
            }
            else
            {
                if (!int.TryParse(parts[1].Replace("F", ""), out int from))
                {
                    _logger.LogWarning("Couldn't parse broadcast from '{line}'", line);
                }
                else
                {
                    Answered.Clear();
                    lastBroadcastTime = DateTimeOffset.Now;
                    lastBroadcastNum = target;
                    SaveCurrentState();
                    OnParserUpdated?.Invoke();
                    _logger.LogInformation("Broadcast from '{from}' to '{target}'", from, target);
                }
            }
        }
    }

    private void SaveCurrentState()
    {
        string res = $"Broadcast test to {lastBroadcastNum} started at {lastBroadcastTime:yy-MM-dd HH:mm:ss}\n\n";
        foreach(var (num, when) in Answered.OrderBy(x => x.Value)) {
            res += $"{num} answered at {when.ToLocalTime():yy-MM-dd HH:mm:ss}\n";
        }
        string dateString = lastBroadcastTime.ToString("yyyy-MM-dd HH-mm-ss");
        res = res.Replace("\n", "\r\n");
        File.WriteAllText($"broadcast {dateString} to {lastBroadcastNum}.txt", res);
    }

    public string GetNumberAlias(int number)
    {
        return NumberAliases.TryGetValue(number, out string? alias)
            ? (alias ?? "???")
            : "???";
    }
}

public class NumberAliasModel
{
    [CsvHelper.Configuration.Attributes.Index(0)]
    public int Number { get; set; }
    [CsvHelper.Configuration.Attributes.Index(1)]
    public string Alias { get; set; } = string.Empty;
}

