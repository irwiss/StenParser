using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;

namespace StenParser
{
    public class ParserService
    {
        public event Action? OnParserUpdated;
        public event Action<int, int>? OnAlertDial;

        public StenParserOptions Options { get; private set; } = new();
        public Dictionary<int, DateTimeOffset> Answered { get; } = new();
        public Dictionary<int, string> NumberAliases { get; } = new();
        public List<string> RecentInputs { get; } = new();

        private readonly ILogger<ParserService> logger;
        private readonly IOptionsMonitor<StenParserOptions> optionsMonitor;
        private DateTimeOffset lastBroadcastTime = DateTimeOffset.Now;
        private int lastBroadcastNum = 0;

        public ParserService(ILogger<ParserService> logger, IOptionsMonitor<StenParserOptions> optionsMonitor)
        {
            this.logger = logger;
            this.optionsMonitor = optionsMonitor;

            if (File.Exists(Options.AliasesFilename))
            {
                using StreamReader reader = new(Options.AliasesFilename);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Encoding = System.Text.Encoding.UTF8,
                    HasHeaderRecord = false,
                };
                using var csv = new CsvReader(reader, config);
                NumberAliases = csv.GetRecords<NumberAliasModel>()
                    .ToDictionary(x => x.Number, x => x.Alias);
            } else {
                logger.LogWarning("'{AliasesFilename}' is missing, no number aliases loaded.", Options.AliasesFilename);
            }

            this.optionsMonitor.OnChange(ReloadOptions);
            ReloadOptions(this.optionsMonitor.CurrentValue);
        }

        private void ReloadOptions(StenParserOptions options)
        {
            Options = options;

            logger.LogInformation("Listening for broadcast strings: {BroadcastCodes}", options.BroadcastCodes);
            logger.LogInformation("Listening for expected answer strings: {AnswerCodes}", options.AnswerCodes);

            OnParserUpdated?.Invoke();
            logger.LogWarning("Configuration reloaded.");
        }

        private static bool TryParseStenNumber(string input, out int number)
        {
            return int.TryParse(input.TrimStart('F'), out number);
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
            if (!parts[0].StartsWith("05"))
            {
                return; // ignore anything but calls
            }

            if (!TryParseStenNumber(parts[1], out int source))
            {
                logger.LogWarning("Couldn't parse source number from '{line}'", line);
            }
            if (!TryParseStenNumber(parts[2], out int target))
            {
                logger.LogWarning("Couldn't parse target number from '{line}'", line);
            }

            if (Options.AnswerCodes.Contains(target))
            {
                if (Answered.TryAdd(source, DateTimeOffset.UtcNow))
                {
                    OnParserUpdated?.Invoke();
                    SaveCurrentState();
                    logger.LogInformation("Call from '{source}' to '{target}'", source, target);
                }
                else
                {
                    logger.LogWarning("Duplicate call from '{source}' to '{target}'", source, target);
                }
            }
            else if (Options.BroadcastCodes.Contains(target))
            {
                Answered.Clear();
                lastBroadcastTime = DateTimeOffset.Now;
                lastBroadcastNum = target;
                SaveCurrentState();
                OnParserUpdated?.Invoke();
                logger.LogInformation("Broadcast from '{source}' to '{target}'", source, target);
            }
            else if (Options.AlertCodes.Contains(target))
            {
                try {
                    File.AppendAllText(Options.AlertsLogFilename, $"Alert triggered from {source} to {target} at {DateTimeOffset.Now.ToString(Options.DateTimeFormat)}{Environment.NewLine}");
                    OnAlertDial?.Invoke(source, target);
                    logger.LogWarning("Alert triggered from {source} to {target}", source, target);
                } catch (Exception ex) {
                    logger.LogError(ex, "Couldn't log alert to file.");
                }
            }
        }

        private void SaveCurrentState()
        {
            string dateString = lastBroadcastTime.ToString("yyyy-MM-dd-HH-mm-ss");
            string stateFilename = $"./broadcast-{dateString}-to-{lastBroadcastNum}.txt";
            try {
                string res = $"Broadcast test to {lastBroadcastNum} started at {lastBroadcastTime.ToString(Options.DateTimeFormat)}\n\n";
                foreach((int num, DateTimeOffset when) in Answered.OrderBy(x => x.Value)) {
                    res += $"{num} answered at {when.ToLocalTime().ToString(Options.DateTimeFormat)}\n";
                }
                res = res.Replace("\n", Environment.NewLine);
                File.WriteAllText(stateFilename, res);
            } catch (Exception ex) {
                logger.LogError(ex, "Couldn't write to {stateFilename}", stateFilename);
            }
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
