using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using UserAPI.Models;
using System.Text;

namespace AdminAPI.Controllers;

public class AdminController
{
    private readonly IMongoCollection<Account> adminCollection;
    private readonly IMongoCollection<Chat> chatCollection;
    private readonly JwtSettings jwtSettings;

    public AdminController(JwtSettings jwtSettings)
    {
        this.jwtSettings = jwtSettings;
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var client = new MongoClient(connectionString);
        adminCollection = client.GetDatabase("AdminAPI").GetCollection<Account>("Admins");
        chatCollection = client.GetDatabase("UserAPI").GetCollection<Chat>("Chats"); // Fixed typo from Account to Chat
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public async Task CreateAdmin(Account admin)
    {
        admin.Id = ObjectId.GenerateNewId().ToString();
        admin.Password = HashPassword(admin.Password);
        admin.Role = "Admin";
        await adminCollection.InsertOneAsync(admin);
    }

    public async Task<Account?> GetAdmin(string email)
    {
        var filter = Builders<Account>.Filter.Eq(a => a.Email, email);
        return await adminCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<string> LogIn(string email, string password)
    {
        Console.WriteLine($"AdminController.LogIn called with email: {email}");
        var admin = await adminCollection.Find(a => a.Email == email).FirstOrDefaultAsync();
        if (admin == null)
        {
            Console.WriteLine($"No admin found for {email}");
            return null;
        }
        Console.WriteLine($"Found admin: {admin.Email}, Role: {admin.Role}");
        if (admin.Role != "Admin" || !BCrypt.Net.BCrypt.Verify(password, admin.Password))
        {
            Console.WriteLine($"Invalid role or password for {email}");
            return null;
        }
        var token = GenerateJwtToken(admin);
        Console.WriteLine($"Generated token: {token}");
        return token;
    }

    public async Task DeleteChat(string chatId, string userEmail, string token)
    {
        ValidateAdminToken(token);
        var chatFilter = Builders<Chat>.Filter.Eq(c => c.Id, ObjectId.Parse(chatId));
        var chat = await chatCollection.Find(chatFilter).FirstOrDefaultAsync();
        if (chat == null || chat.UserEmail != userEmail)
            throw new Exception("Chat not found or user mismatch");
        await chatCollection.DeleteOneAsync(chatFilter);
    }

    public async Task DeleteUser(string userId, string token)
    {
        ValidateAdminToken(token);
        var userFilter = Builders<Account>.Filter.Eq(a => a.Id, userId); // Use string directly

        var user = await adminCollection.Find(userFilter).FirstOrDefaultAsync();
        if (user != null)
        {
            await adminCollection.DeleteOneAsync(userFilter);
        }
        else
        {
            var userCollection = adminCollection.Database.Client.GetDatabase("UserAPI").GetCollection<Account>("Users");
            user = await userCollection.Find(userFilter).FirstOrDefaultAsync();
            if (user != null)
            {
                await userCollection.DeleteOneAsync(userFilter);
            }
        }

        if (user != null)
        {
            var chatFilter = Builders<Chat>.Filter.Eq(c => c.UserEmail, user.Email);
            await chatCollection.DeleteManyAsync(chatFilter);
        }
    }

    public async Task<List<Chat>> GetUserChats(string userEmail, string token)
    {
        ValidateAdminToken(token);
        var filter = Builders<Chat>.Filter.Eq(c => c.UserEmail, userEmail);
        return await chatCollection.Find(filter).ToListAsync();
    }

    public async Task<List<Account>> GetAllUsers(string token)
    {
        ValidateAdminToken(token);
        var adminUsers = await adminCollection.Find(Builders<Account>.Filter.Empty).ToListAsync();
        var userCollection = adminCollection.Database.Client.GetDatabase("UserAPI").GetCollection<Account>("Users");
        var regularUsers = await userCollection.Find(Builders<Account>.Filter.Empty).ToListAsync();

        var allUsers = new List<Account>();
        allUsers.AddRange(adminUsers);
        allUsers.AddRange(regularUsers);
        return allUsers;
    }

    private string GenerateJwtToken(Account account)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id),
                new Claim(ClaimTypes.Email, account.Email),
                new Claim(ClaimTypes.Role, account.Role)
            }),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings.ExpirationInMinutes),
            Issuer = jwtSettings.Issuer,
            Audience = jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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
        return principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public void ValidateAdminToken(string token)
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
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin")
            throw new UnauthorizedAccessException("Admin privileges required.");
    }
}