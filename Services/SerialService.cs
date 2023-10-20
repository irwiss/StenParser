using System.IO.Ports;

public class SerialService : IHostedService
{
    private readonly ILogger<SerialService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ParserService _parserService;

    public SerialService(ILogger<SerialService> logger, IConfiguration configuration, ParserService parserService)
    {
        _logger = logger;
        _configuration = configuration;
        _parserService = parserService;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Serial Hosted Service starting.");
        _ = Task.Run(() => StartReading(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }

    private async void StartReading(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using SerialPort _serial = new SerialPort();
                string? portName = _configuration.GetValue<string>("SerialPort");
                if (string.IsNullOrWhiteSpace(portName))
                {
                    throw new ArgumentException($"SerialPort '{portName}' is not valid");
                }
                _serial.PortName = portName;
                _serial.BaudRate = 9600;
                _serial.Parity = Parity.Even;
                _serial.DataBits = 7;
                _serial.StopBits = StopBits.Two;
                _serial.Handshake = Handshake.None;
                _serial.Encoding = System.Text.Encoding.ASCII;
                _serial.Open();
                _serial.DiscardOutBuffer();
                _serial.DiscardInBuffer();
                _logger.LogInformation("Serial opened '{port}'", portName);
                while(_serial.IsOpen)
                {
                    string line = _serial.ReadLine()
                        .TrimStart('\0')
                        .TrimEnd('\n')
                        .TrimEnd('\r')
                        .TrimEnd(' ');
                    // _logger.LogInformation("{ll}", BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes(line)));
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    _parserService.ParseLine(line);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Serial: Exception.");
            }
            finally
            {
                _logger.LogInformation("Closing serial port");
            }
            if(!cancellationToken.IsCancellationRequested)
            {
                TimeSpan waitTime = TimeSpan.FromSeconds(10);
                // wait before trying to reopen
                _logger.LogInformation("Waiting {timespan} before retrying.", waitTime.ToString());
                await Task.Delay(waitTime, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Serial Hosted Service is stopping.");
        return Task.CompletedTask;
    }
}
