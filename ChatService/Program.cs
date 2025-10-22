using RabbitMQ.Client;

Console.WriteLine($"ChatService v{ChatService.ServiceVersion.Current} starting...");

var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

Console.WriteLine($"Connecting to RabbitMQ at {rabbitMqHost}...");

try
{
    var factory = new ConnectionFactory
    {
        HostName = rabbitMqHost,
        Port = 5672,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
        AutomaticRecoveryEnabled = true
    };

    using var connection = await factory.CreateConnectionAsync();
    using var channel = await connection.CreateChannelAsync();

    Console.WriteLine("ChatService connected to RabbitMQ");

    // Declare exchange: chat.direct (direct)
    await channel.ExchangeDeclareAsync(
        exchange: "chat.direct",
        type: ExchangeType.Direct,
        durable: true,
        autoDelete: false);
    Console.WriteLine("Declared exchange: chat.direct (direct)");

    // Declare exchange: chat.fanout (fanout)
    await channel.ExchangeDeclareAsync(
        exchange: "chat.fanout",
        type: ExchangeType.Fanout,
        durable: true,
        autoDelete: false);
    Console.WriteLine("Declared exchange: chat.fanout (fanout)");

    // Declare exchange: chat.topic (topic)
    await channel.ExchangeDeclareAsync(
        exchange: "chat.topic",
        type: ExchangeType.Topic,
        durable: true,
        autoDelete: false);
    Console.WriteLine("Declared exchange: chat.topic (topic)");

    Console.WriteLine("ChatService is running. Press Ctrl+C to exit.");

    // Keep the worker running
    var cancellationTokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationTokenSource.Cancel();
    };

    cancellationTokenSource.Token.WaitHandle.WaitOne();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

Console.WriteLine("ChatService stopped.");
