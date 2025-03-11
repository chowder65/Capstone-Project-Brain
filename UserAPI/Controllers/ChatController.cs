using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using UserAPI.Models;
using System.Net.Http;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace UserAPI.Controllers
{
    public class ChatController
    {
        private readonly IMongoCollection<Chat> chatCollection;
        private readonly JwtSettings jwtSettings;
        private static readonly HttpClient httpClient = new HttpClient();

        public ChatController(JwtSettings jwtSettings)
        {
            this.jwtSettings = jwtSettings;
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            var mongoClient = new MongoClient(connectionString); // For MongoDB
            var database = mongoClient.GetDatabase("ChatAPI");
            chatCollection = database.GetCollection<Chat>("Chats");
        }

        public async Task<string> StartNewChat(string userEmail, string token, string chatName = null)
        {
            var validatedEmail = ValidateToken(token);
            Console.WriteLine($"StartNewChat - Provided userEmail: {userEmail}, Validated email: {validatedEmail}");
            if (userEmail != validatedEmail)
            {
                throw new UnauthorizedAccessException("Email does not match authenticated user.");
            }
            var chat = new Chat(validatedEmail, chatName);
            Console.WriteLine($"Chat created - ID: {chat.Id}, UserEmail: {chat.UserEmail}, ChatName: {chat.ChatName}");
            await chatCollection.InsertOneAsync(chat);
            Console.WriteLine("Chat inserted into MongoDB");
            return chat.Id;
        }

        public async Task AddMessageToChat(string chatId, string messageText, string token)
        {
            var userEmail = ValidateToken(token);
            var filter = Builders<Chat>.Filter.And(
                Builders<Chat>.Filter.Eq(c => c.Id, chatId),
                Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
            );

            var userMessage = new Message(messageText);
            var update = Builders<Chat>.Update.Push(c => c.Messages, userMessage);
            await chatCollection.UpdateOneAsync(filter, update);
            Console.WriteLine($"Message added to chat: {messageText}");
        }

        public async Task<LLMResponse> SendMessageWithLLM(string chatId, ChatRequestBody chatRequest, string token)
{
    try
    {
        var userEmail = ValidateToken(token);
        var filter = Builders<Chat>.Filter.And(
            Builders<Chat>.Filter.Eq(c => c.Id, chatId),
            Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
        );

        var userMessage = new Message(chatRequest.new_message);
        var update = Builders<Chat>.Update.Push(c => c.Messages, userMessage);
        await chatCollection.UpdateOneAsync(filter, update);
        Console.WriteLine($"User message added: {chatRequest.new_message}");

        var factory = new ConnectionFactory { HostName = "rabbitmq", UserName = "guest", Password = "guest" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "llm_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        var replyQueue = channel.QueueDeclare(durable: false, exclusive: true, autoDelete: true).QueueName;
        var correlationId = Guid.NewGuid().ToString();

        var consumer = new EventingBasicConsumer(channel);
        var tcs = new TaskCompletionSource<string>();
        consumer.Received += (sender, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"Received LLM response: {response}");
                tcs.TrySetResult(response);
            }
        };
        channel.BasicConsume(queue: replyQueue, autoAck: true, consumer: consumer);

        var props = channel.CreateBasicProperties();
        props.ReplyTo = replyQueue;
        props.CorrelationId = correlationId;
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chatRequest));
        channel.BasicPublish(exchange: "", routingKey: "llm_queue", basicProperties: props, body: body);
        Console.WriteLine($"Message published to RabbitMQ with CorrelationId: {correlationId}");

        var llmResponseTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
        if (llmResponseTask == tcs.Task)
        {
            var jsonResponse = tcs.Task.Result;
            var llmResponse = JsonSerializer.Deserialize<LLMResponse>(jsonResponse);
            var aiMessageObj = new Message(llmResponse.Response);
            update = Builders<Chat>.Update.Push(c => c.Messages, aiMessageObj);
            await chatCollection.UpdateOneAsync(filter, update);
            Console.WriteLine($"LLM response added to MongoDB: {llmResponse.Response}");
            return llmResponse;
        }
        throw new TimeoutException("LLM response timed out after 10 seconds");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in SendMessageWithLLM: {ex.Message}");
        throw;
    }
}

        public class LLMResponse
        {
            public string Response { get; set; }
            public string DetectedEmotion { get; set; }
        }

        public async Task<Chat> GetChatHistory(string chatId, string token)
        {
            var userEmail = ValidateToken(token);
            var filter = Builders<Chat>.Filter.And(
                Builders<Chat>.Filter.Eq(c => c.Id, chatId),
                Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
            );
            return await chatCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<Chat>> GetChatsByUserId(string userEmail)
        {
            var filter = Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail);
            return await chatCollection.Find(filter).ToListAsync();
        }

        public async Task DeleteChat(string chatId, string userEmail)
        {
            var filter = Builders<Chat>.Filter.And(
                Builders<Chat>.Filter.Eq(c => c.Id, chatId),
                Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
            );
            await chatCollection.DeleteOneAsync(filter);
        }

        internal string ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);
            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true
                }, out SecurityToken validatedToken);
                return principal.FindFirstValue(ClaimTypes.Email);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Invalid or expired token.");
            }
        }

        public void ValidateAdminToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);
            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true
                }, out SecurityToken validatedToken);

                var role = principal.FindFirstValue(ClaimTypes.Role);
                if (role != "Admin")
                {
                    throw new UnauthorizedAccessException("You are not authorized to perform this action.");
                }
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Invalid or expired token.");
            }
        }
    }

    public class ChatRequestBody
    {
        public string prompt { get; set; }
        public List<PastMessageBody> past_messages { get; set; }
        public string new_message { get; set; }
    }

    public class PastMessageBody
    {
        public string user { get; set; }
        public string assistant { get; set; }
    }
}