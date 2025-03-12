using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using UserAPI.Controllers;
using AdminAPI.Controllers;
using StackExchange.Redis;
using UserAPI.Models;

namespace UserAPI.Services
{
    public class RabbitMQConsumerService : BackgroundService
    {
        private readonly UserController _userController;
        private readonly ChatController _chatController;
        private readonly AdminController _adminController;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IConnectionMultiplexer _redis;

        public RabbitMQConsumerService(
            UserController userController,
            ChatController chatController,
            AdminController adminController,
            IConnectionMultiplexer redis)
        {
            _userController = userController;
            _chatController = chatController;
            _adminController = adminController;
            _redis = redis;
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare llm_queue
            _channel.QueueDeclare(queue: "llm_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("RabbitMQConsumerService started and subscribing to userapi_queue");
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var headers = ea.BasicProperties.Headers;
                    var messageTypeBytes = headers?["MessageType"] as byte[];
                    var messageType = messageTypeBytes != null ? Encoding.UTF8.GetString(messageTypeBytes) : null;

                    if (string.IsNullOrEmpty(messageType))
                        throw new Exception("MessageType header is missing or invalid");

                    string token = "";
                    if (headers != null && headers.TryGetValue("Token", out var tokenObj) && tokenObj is byte[] tokenBytes)
                    {
                        token = Encoding.UTF8.GetString(tokenBytes);
                    }

                    Console.WriteLine($"Processing {messageType} from userapi_queue with Token: '{token}'");
                    object response = null;

                    switch (messageType)
                    {
                        case "create_user":
                            var user = JsonSerializer.Deserialize<Account>(message);
                            var userToken = await _userController.CreateUser(user);
                            response = new LoginResponse { Token = userToken };
                            break;
                        case "get_user_by_email":
                            var emailPayload = JsonSerializer.Deserialize<JsonElement>(message);
                            response = await _userController.GetUser(emailPayload.GetProperty("email").GetString());
                            break;
                        case "login":
                            var loginPayload = JsonSerializer.Deserialize<JsonElement>(message);
                            var userEmail = loginPayload.GetProperty("email").GetString();
                            var userPassword = loginPayload.GetProperty("password").GetString();
                            var loginToken = await _userController.LogIn(userEmail, userPassword);
                            response = new LoginResponse { Token = loginToken };
                            break;
                        case "admin_login":
                            var adminLoginPayload = JsonSerializer.Deserialize<JsonElement>(message);
                            var adminEmail = adminLoginPayload.GetProperty("email").GetString();
                            var adminPassword = adminLoginPayload.GetProperty("password").GetString();
                            var adminToken = await _adminController.LogIn(adminEmail, adminPassword);
                            response = new LoginResponse { Token = adminToken };
                            break;
                        case "get_all_users":
                            response = await _adminController.GetAllUsers(token);
                            break;
                        case "start_new_chat":
                            var chatRequest = JsonSerializer.Deserialize<ChatRequest>(message);
                            response = await _chatController.StartNewChat(chatRequest.UserEmail, token, chatRequest.InitialMessage);
                            break;
                        case "send_message":
                            var sendMessageRequest = JsonSerializer.Deserialize<SendMessageRequest>(message);
                            await _chatController.AddMessageToChat(sendMessageRequest.ChatId, sendMessageRequest.Message, token);

                            
                            var llmProps = _channel.CreateBasicProperties();
                            llmProps.CorrelationId = ea.BasicProperties.CorrelationId;
                            llmProps.Headers = new Dictionary<string, object>
                            {
                                ["MessageType"] = "send_message",
                                ["Token"] = Encoding.UTF8.GetBytes(token)
                            };
                            var llmMessage = JsonSerializer.Serialize(sendMessageRequest);
                            var llmBody = Encoding.UTF8.GetBytes(llmMessage);
                            _channel.BasicPublish(exchange: "", routingKey: "llm_queue", basicProperties: llmProps, body: llmBody);
                            Console.WriteLine($"Queued send_message to llm_queue with CorrelationId: {ea.BasicProperties.CorrelationId}");

                            response = new { ChatId = sendMessageRequest.ChatId, Message = sendMessageRequest.Message };
                            break;
                        case "delete_user":
                            var deletePayload = JsonSerializer.Deserialize<JsonElement>(message);
                            var userId = deletePayload.GetProperty("userId").GetString();
                            await _adminController.DeleteUser(userId, token);
                            response = new { Message = "User deleted successfully" };
                            break;
                        case "llm_response":
                            var llmResponsePayload = JsonSerializer.Deserialize<JsonElement>(message);
                            var chatId = llmResponsePayload.GetProperty("ChatId").GetString();
                            var llmResponse = JsonSerializer.Deserialize<ChatController.LLMResponse>(llmResponsePayload.GetProperty("LLMResponse").GetRawText());
                            response = new { ChatId = chatId, Response = llmResponse.Response, DetectedEmotion = llmResponse.DetectedEmotion };
                            break;
                        default:
                            throw new Exception($"Unknown message type: {messageType}");
                    }

                    var db = _redis.GetDatabase();
                    var resultToStore = new { Status = "completed", Result = response ?? new LoginResponse { Token = null } };
                    var valueToStore = JsonSerializer.Serialize(resultToStore);
                    Console.WriteLine($"Storing in Redis: {valueToStore}");
                    await db.StringSetAsync(ea.BasicProperties.CorrelationId, valueToStore, TimeSpan.FromSeconds(60));
                    Console.WriteLine($"Stored result for {messageType} with CorrelationId: {ea.BasicProperties.CorrelationId}");

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
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

    public class LoginResponse { public string Token { get; set; } }
}