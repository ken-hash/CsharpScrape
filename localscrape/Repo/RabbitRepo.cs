using System.Text;
using System.Text.Json;
using localscrape.Helpers;
using localscrape.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
public interface IRabbitRepo
{
    Task Send(DownloadObject obj);
    Task SendBatch(IEnumerable<DownloadObject> list);
    Task Dispose();
}

public class RabbitRepo : IRabbitRepo
{
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QUEUE_NAME = "download_queue";
    private IDotEnvHelper _envHelper;
    private readonly ILogger _logger;

    public RabbitRepo(ILogger logger)
    {
        _envHelper = new DotEnvHelper(logger);
        _logger = logger;
        InitialiseRabbit();
    }

    private async void InitialiseRabbit()
    {
        _logger.LogInformation($"Initialising RabbitMQ");

        try
        {
            var rabbitMQHost = _envHelper.GetEnvValue("rabbitMQHost");
            var rabbitUser = _envHelper.GetEnvValue("rabbitMQUser");
            var rabbitPass = _envHelper.GetEnvValue("rabbitMQPassword");
            var factory = new ConnectionFactory()
            {
                HostName = rabbitMQHost,
                UserName = rabbitUser,
                Password = rabbitPass,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedFrameMax = 1073741824,
                MaxInboundMessageBodySize = 1073741824
            };
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.QueueDeclareAsync(
                queue: QUEUE_NAME,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );
            _logger.LogInformation("RabbitMQ initialization complete. Queue declared and QoS set.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ");
            throw;
        }
    }

    public async Task Send(DownloadObject obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var body = Encoding.UTF8.GetBytes(json);
        var prop = new BasicProperties { 
            Persistent = true
        };

        await _channel!.BasicPublishAsync(
            exchange: "",
            routingKey: QUEUE_NAME,
            body: body,
            basicProperties: prop,
            mandatory: true
        );
        _logger.LogInformation($"RabbitMQ published {obj}");
    }

    public async Task SendBatch(IEnumerable<DownloadObject> list)
    {
        foreach (var obj in list)
        {
            var json = JsonSerializer.Serialize(obj);
            var body = Encoding.UTF8.GetBytes(json);
            await _channel!.BasicPublishAsync(
                exchange: "",
                routingKey: QUEUE_NAME,
                body: body
            );
            _logger.LogInformation($"RabbitMQ published {obj}");
        }
    }

    public async Task Dispose()
    {
        await _channel!.CloseAsync();
        await _connection!.CloseAsync();
    }
}
