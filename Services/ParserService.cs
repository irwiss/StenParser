using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;

namespace StenParser
{
    public class ParserService
    {
        public event Action? OnParserUpdated;

        public StenParserOptions Options { get; private set; } = new();
        public Dictionary<int, DateTimeOffset> Answered { get; } = new();
        public Dictionary<int, string> NumberAliases { get; } = new();
        public List<string> RecentInputs { get; } = new();

        private readonly ILogger<ParserService> logger;
        private readonly IOptionsMonitor<StenParserOptions> optionsMonitor;
        private DateTimeOffset lastBroadcastTime = DateTimeOffset.Now;
        private int lastBroadcastNum = 0;
        private readonly HashSet<string> answerCodeStrings = new();
        private readonly HashSet<string> broadcastCodeStrings = new();

        private const string aliasesFilename = "aliases.csv";

        public ParserService(ILogger<ParserService> logger, IOptionsMonitor<StenParserOptions> optionsMonitor)
        {
            this.logger = logger;
            this.optionsMonitor = optionsMonitor;

            if (File.Exists(aliasesFilename))
            {
                using StreamReader reader = new(aliasesFilename);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Encoding = System.Text.Encoding.UTF8,
                    HasHeaderRecord = false,
                };
                using var csv = new CsvReader(reader, config);
                NumberAliases = csv.GetRecords<NumberAliasModel>()
                    .ToDictionary(x => x.Number, x => x.Alias);
            } else {
                logger.LogWarning($"Couldn't find '{aliasesFilename}', no number aliases loaded.");
            }

            this.optionsMonitor.OnChange(ReloadOptions);
            ReloadOptions(this.optionsMonitor.CurrentValue);
        }

        private void ReloadOptions(StenParserOptions options)
        {
            Options = options;

            void ConvertNumbersToCodeStrings(HashSet<string> destination, List<int> nums)
            {
                nums.ForEach(num => destination.Add(num.ToString().PadLeft(4, 'F')));
            }

            ConvertNumbersToCodeStrings(broadcastCodeStrings, options.BroadcastCodes);
            ConvertNumbersToCodeStrings(answerCodeStrings, options.AnswerCodes);

            logger.LogInformation("Listening for broadcast strings: {BroadcastCodeStrings}", broadcastCodeStrings);
            logger.LogInformation("Listening for expected answer strings: {AnswerCodeStrings}", answerCodeStrings);

            OnParserUpdated?.Invoke();
            logger.LogWarning("Configuration reloaded.");
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
                logger.LogDebug("Line '{line}' can't be split to 5 parts via whitespace", line);
                return; // ignore
            }
            logger.LogInformation("Parse Input: '{line}'", line);
            if (answerCodeStrings.Contains(parts[2]) && parts[0].StartsWith("05"))
            {
                if (!int.TryParse(parts[2].Replace("F", ""), out int target))
                {
                    logger.LogWarning("Couldn't parse target from '{line}'", line);
                }
                else
                {
                    if(!int.TryParse(parts[1].Replace("F", ""), out int from))
                    {
                        logger.LogWarning("Couldn't parse caller from '{line}'", line);
                    }
                    else
                    {
                        if (Answered.TryAdd(from, DateTimeOffset.UtcNow))
                        {
                            OnParserUpdated?.Invoke();
                            SaveCurrentState();
                            logger.LogInformation("Call from '{from}' to '{to}'", from, target);
                        }
                        else
                        {
                            logger.LogWarning("Duplicate call from '{from}' to '{to}'", from, target);
                        }
                    }
                }
            }
            if (broadcastCodeStrings.Contains(parts[2]))
            {
                if (!int.TryParse(parts[2].Replace("F", ""), out int target))
                {
                    logger.LogWarning("Couldn't parse broadcast from '{line}'", line);
                }
                else
                {
                    if (!int.TryParse(parts[1].Replace("F", ""), out int from))
                    {
                        logger.LogWarning("Couldn't parse broadcast from '{line}'", line);
                    }
                    else
                    {
                        Answered.Clear();
                        lastBroadcastTime = DateTimeOffset.Now;
                        lastBroadcastNum = target;
                        SaveCurrentState();
                        OnParserUpdated?.Invoke();
                        logger.LogInformation("Broadcast from '{from}' to '{target}'", from, target);
                    }
                }
            }
        }

        private void SaveCurrentState()
        {
            string res = $"Broadcast test to {lastBroadcastNum} started at {lastBroadcastTime:yy-MM-dd HH:mm:ss}\n\n";
            foreach((int num, DateTimeOffset when) in Answered.OrderBy(x => x.Value)) {
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
}
