using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using UserAPI.Models;

namespace UserAPI.Controllers
{
    public class ChatController
    {
        private readonly IMongoCollection<Chat> chatCollection;
        private readonly JwtSettings jwtSettings;

        public ChatController(JwtSettings jwtSettings)
        {
            this.jwtSettings = jwtSettings;
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("ChatAPI");
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
            var update = Builders<Chat>.Update.Push(c => c.Messages, new Message(messageText));
            await chatCollection.UpdateOneAsync(filter, update);
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
}
