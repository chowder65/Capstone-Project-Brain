using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UserAPI.Controllers;

namespace UserAPI
{
    public class RabbitMQConsumerService : BackgroundService
    {
        private readonly ChatController _chatController;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMQConsumerService(ChatController chatController, IConnection connection)
        {
            _chatController = chatController;
            _connection = connection;
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false); // Updated to 3 parameters
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var payload = JsonSerializer.Deserialize<dynamic>(message);
                    string chatId = payload.ChatId;
                    var chatRequest = JsonSerializer.Deserialize<ChatRequestBody>(JsonSerializer.Serialize(payload.ChatRequest));
                    string token = ea.BasicProperties.Headers?["Token"]?.ToString() ?? throw new Exception("Token missing");

                    // Process with ChatController
                    var llmResponse = await _chatController.SendMessageWithLLM(chatId, chatRequest, token);

                    // Send response back to reply queue
                    var responseProps = _channel.CreateBasicProperties();
                    responseProps.CorrelationId = ea.BasicProperties.CorrelationId;
                    var responseBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LLMResponse
                    {
                        Response = llmResponse.Response,
                        DetectedEmotion = llmResponse.DetectedEmotion
                    }));
                    _channel.BasicPublish(exchange: "", routingKey: ea.BasicProperties.ReplyTo, basicProperties: responseProps, body: responseBody);

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    Console.WriteLine($"Processed message from userapi_queue: {chatId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: "userapi_queue", autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }

    public class LLMResponse
    {
        public string Response { get; set; }
        public string DetectedEmotion { get; set; }
    }
}