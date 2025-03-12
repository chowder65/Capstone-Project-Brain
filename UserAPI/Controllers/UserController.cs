using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using UserAPI.Models;
using System.Text;

namespace UserAPI.Controllers;

public class UserController
{
    private readonly IMongoCollection<Account> collection;
    private readonly JwtSettings jwtSettings;

    public UserController(JwtSettings jwtSettings)
    {
        this.jwtSettings = jwtSettings;
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var client = new MongoClient(connectionString);
        collection = client.GetDatabase("UserAPI").GetCollection<Account>("Users");
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public async Task<string> CreateUser(Account user)
    {
        user.Id = ObjectId.GenerateNewId().ToString();
        user.Password = HashPassword(user.Password);
        user.Role = "User";
        await collection.InsertOneAsync(user);
        return GenerateJwtToken(user);
    }

    public async Task<Account?> GetUser(string email)
    {
        var filter = Builders<Account>.Filter.Eq(a => a.Email, email);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<string?> LogIn(string email, string password)
    {
        var user = await GetUser(email);
        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password) && user.Role == "User")
        {
            return GenerateJwtToken(user);
        }
        Console.WriteLine($"User login failed for {email}");
        return null;
    }

    public async Task<bool> ChangePassword(string id, string newPassword, string token)
    {
        var userIdFromToken = ValidateToken(token);
        if (userIdFromToken != id)
            throw new UnauthorizedAccessException("Unauthorized to change this password.");

        var filter = Builders<Account>.Filter.Eq(u => u.Id, id); // Use string directly
        var update = Builders<Account>.Update.Set(u => u.Password, HashPassword(newPassword));
        var result = await collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteUser(string email, string password, string token)
    {
        var userIdFromToken = ValidateToken(token);
        var user = await GetUser(email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");
        if (user.Id != userIdFromToken)
            throw new UnauthorizedAccessException("Unauthorized to delete this user.");

        var filter = Builders<Account>.Filter.Eq(u => u.Id, user.Id); // Use string directly
        await collection.DeleteOneAsync(filter);
    }

    private string GenerateJwtToken(Account user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
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
}