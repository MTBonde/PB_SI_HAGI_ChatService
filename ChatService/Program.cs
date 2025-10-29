using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Hagi.Robust;
using Hagi.Robust.Probes;

Console.WriteLine($"ChatService v{ChatService.ServiceVersion.Current} starting...");

var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

Console.WriteLine($"Connecting to RabbitMQ at {rabbitMqHost}...");

var rabbitProbe = new RabbitMqProbe(rabbitMqHost, 5672);
await HagiRobust.WaitForDependenciesAsync(new[] { rabbitProbe });

Console.WriteLine("RabbitMQ is ready!");

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
    
    // Declare exchange: relay.chat.private (direct)
    await channel.ExchangeDeclareAsync(
        exchange: "relay.chat.private",
        type: ExchangeType.Direct,
        durable: true,
        autoDelete: false);
    Console.WriteLine("Declared exchange: relay.chat.private (direct, durable)");

    // Declare exchange: relay.chat.global (fanout)
    await channel.ExchangeDeclareAsync(
        exchange: "relay.chat.global",
        type: ExchangeType.Fanout,
        durable: true,
        autoDelete: false);
    Console.WriteLine("Declared exchange: relay.chat.global (fanout, durable)");

    // Declare a durable queue for ChatService to receive messages from RelayService
    var chatServiceQueueResult = await channel.QueueDeclareAsync(
        queue: "chat.service.queue",
        durable: true,
        exclusive: false,
        autoDelete: false);

    var chatServiceQueueName = chatServiceQueueResult.QueueName;
    Console.WriteLine($"Declared queue: {chatServiceQueueName}");

    // Bind the queue to receive messages from relay.chat.global exchange
    await channel.QueueBindAsync(
        queue: chatServiceQueueName,
        exchange: "relay.chat.global",
        routingKey: "");

    Console.WriteLine("Queue bound to relay.chat.global exchange");

    // Create message consumer to process incoming messages from RelayService
    var messageConsumer = new AsyncEventingBasicConsumer(channel);

    messageConsumer.ReceivedAsync += async (sender, eventArgs) =>
    {
        try
        {
            // Extract message from RabbitMQ delivery
            var messageBody = eventArgs.Body.ToArray();
            var messageText = Encoding.UTF8.GetString(messageBody);

            Console.WriteLine($"[ChatService] Received raw message: {messageText}");

            // Parse message as RelayMessage
            var relayMessage = System.Text.Json.JsonSerializer.Deserialize<RelayMessage>(messageText);

            if (relayMessage != null)
            {
                Console.WriteLine($"[ChatService] Parsed message:");
                Console.WriteLine($"  Type: {relayMessage.Type}");
                Console.WriteLine($"  Content: {relayMessage.Content}");
                Console.WriteLine($"  Timestamp: {relayMessage.Timestamp}");
                // Console.WriteLine($"  UserId: {relayMessage.UserId}");
                // Console.WriteLine($"  Username: {relayMessage.Username}");
                // Console.WriteLine($"  UserRole: {relayMessage.UserRole}");

                // TODO: Process the message
                // - Save to database
                // - Forward to other services
                // - Send notifications
            }

            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[ChatService] Error processing message: {exception.Message}");
        }
    };

    // Start consuming messages from the queue
    await channel.BasicConsumeAsync(
        queue: chatServiceQueueName,
        autoAck: true,
        consumer: messageConsumer);

    Console.WriteLine("ChatService consumer started. Listening for messages from RelayService...");
    Console.WriteLine($"Ready to receive messages on queue: {chatServiceQueueName}");

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

/// <summary>
/// Represents a message received from RelayService through RabbitMQ.
/// </summary>
public class RelayMessage
{
    /// <summary>
    /// Gets or sets the message type (e.g., "chat.global", "chat.private").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    // /// <summary>
    // /// Gets or sets the username of the user who sent the message.
    // /// </summary>
    public string Username { get; set; } = string.Empty;

    // /// <summary>
    // /// Gets or sets the role of the user who sent the message.
    // /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    // Future enhancement: Add user context information
    // /// <summary>
    // /// Gets or sets the unique identifier of the user who sent the message.
    // /// </summary>
    // public string UserId { get; set; } = string.Empty;
}
