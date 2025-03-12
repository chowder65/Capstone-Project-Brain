using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using UserAPI.Models;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using MongoDB.Bson;

namespace UserAPI.Controllers
{
    public class ChatController
    {
        private readonly IMongoCollection<Chat> chatCollection;
        private readonly JwtSettings jwtSettings;

        public ChatController(JwtSettings jwtSettings)
        {
            this.jwtSettings = jwtSettings;
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "mongodb://localhost:27017";
            var mongoClient = new MongoClient(connectionString);
            var database = mongoClient.GetDatabase("UserAPI");
            chatCollection = database.GetCollection<Chat>("Chats");
        }

        public async Task<string> StartNewChat(string userId, string token, string chatName)
        {
            var userEmail = ValidateToken(token);
            var chat = new Chat(userEmail, chatName);
            await chatCollection.InsertOneAsync(chat);
            return chat.Id.ToString(); 
        }

        public async Task AddMessageToChat(string chatId, string messageText, string token)
        {
            var userEmail = ValidateToken(token);
            var filter = Builders<Chat>.Filter.And(
                Builders<Chat>.Filter.Eq(c => c.Id, ObjectId.Parse(chatId)),
                Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
            );
            var userMessage = new Message(messageText) { IsUser = true };
            var update = Builders<Chat>.Update.Push(c => c.Messages, userMessage);
            await chatCollection.UpdateOneAsync(filter, update);
        }

        public async Task<LLMResponse> SendMessageWithLLM(string chatId, ChatRequestBody chatRequest, string token)
        {
            var userEmail = ValidateToken(token);
            var filter = Builders<Chat>.Filter.And(
                Builders<Chat>.Filter.Eq(c => c.Id, ObjectId.Parse(chatId)),
                Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
            );

            var chat = await chatCollection.Find(filter).FirstOrDefaultAsync();
            if (chat == null) throw new Exception("Chat not found.");

            var pastMessages = chat.Messages.Select(m => new PastMessageBody
            {
                user = m.IsUser ? m.Text : "",
                assistant = !m.IsUser ? m.Text : ""
            }).ToList();

            var requestBody = new ChatRequestBody
            {
                prompt = "Respond naturally based on the conversation:",
                past_messages = pastMessages.Count > 0 ? pastMessages : null,
                new_message = chatRequest.new_message
            };

            var userMessage = new Message(chatRequest.new_message) { IsUser = true };
            var update = Builders<Chat>.Update.Push(c => c.Messages, userMessage);
            await chatCollection.UpdateOneAsync(filter, update);

            var llmUrl = "http://llm:8000/llm";
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await new HttpClient().PostAsync(llmUrl, content);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var llmResponse = JsonSerializer.Deserialize<LLMResponse>(jsonResponse);

            var aiMessage = new Message(llmResponse.Response) { IsUser = false };
            update = Builders<Chat>.Update.Push(c => c.Messages, aiMessage);
            await chatCollection.UpdateOneAsync(filter, update);

            return llmResponse;
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
                Builders<Chat>.Filter.Eq(c => c.Id, ObjectId.Parse(chatId)),
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
                Builders<Chat>.Filter.Eq(c => c.Id, ObjectId.Parse(chatId)),
                Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail)
            );
            await chatCollection.DeleteOneAsync(filter);
        }

        public string ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true
            }, out _);
            return principal.FindFirstValue(ClaimTypes.Email);
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