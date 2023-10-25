using System.IO.Ports;

namespace StenParser
{
    public class SerialService : IHostedService
    {
        private readonly ILogger<SerialService> logger;
        private readonly ParserService parserService;

        public SerialService(ILogger<SerialService> logger, ParserService parserService)
        {
            this.logger = logger;
            this.parserService = parserService;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("SerialService starting.");
            logger.LogInformation("Serial ports available: {PortNames}.", new object?[] { SerialPort.GetPortNames() });
            _ = Task.Run(() => StartReading(stoppingToken), stoppingToken);
            return Task.CompletedTask;
        }

        private async void StartReading(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using SerialPort _serial = new();
                    _serial.PortName = parserService.Options.SerialPortName;
                    _serial.BaudRate = 9600;
                    _serial.Parity = Parity.Even;
                    _serial.DataBits = 7;
                    _serial.StopBits = StopBits.Two;
                    _serial.Handshake = Handshake.None;
                    _serial.Encoding = System.Text.Encoding.ASCII;
                    _serial.Open();
                    _serial.DiscardOutBuffer();
                    _serial.DiscardInBuffer();
                    logger.LogInformation("Serial opened '{port}'", _serial.PortName);
                    while (_serial.IsOpen)
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
                        parserService.ParseLine(line);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogError(ex.Message);
                }
                catch (FileNotFoundException ex)
                {
                    logger.LogError("{Message} Available Ports: {Ports}", ex.Message, SerialPort.GetPortNames());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Serial: Exception.");
                }
                finally
                {
                    logger.LogInformation("Closing serial port");
                }
                if (!cancellationToken.IsCancellationRequested)
                {
                    // wait before trying to reopen
                    logger.LogInformation("Waiting {WaitTime} before retrying.", parserService.Options.SerialRetryWaitTime);
                    await Task.Delay(parserService.Options.SerialRetryWaitTime, cancellationToken);
                }
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Serial Hosted Service is stopping.");
            return Task.CompletedTask;
        }
    }
}
