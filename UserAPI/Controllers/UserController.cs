using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UserAPI.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace UserAPI.Controllers;

public class UserController
{
    private readonly IMongoCollection<User> collection;
    private readonly JwtSettings jwtSettings;

    public UserController(JwtSettings jwtSettings)
    {
        this.jwtSettings = jwtSettings;
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        var client = new MongoClient(connectionString);
        collection = client.GetDatabase("UserAPI").GetCollection<User>("Users");
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public async Task CreateUser(User user)
    {
        user.Id = ObjectId.GenerateNewId();
        await collection.InsertOneAsync(user);
    }

    public async Task<User?> GetUser(string userName)
    {
        var filter = Builders<User>.Filter.Eq("UserName", userName);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<string?> LogIn(string userName, string password)
    {
        var user = await GetUser(userName);
        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            return GenerateJwtToken(user);
        }
        return null;
    }

    public async Task<bool> ChangePassword(string id, string newPassword)
    {
        var objectId = ObjectId.Parse(id);
        var filter = Builders<User>.Filter.Eq(u => u.Id, objectId);
        var hashedPassword = HashPassword(newPassword);
        var update = Builders<User>.Update.Set(u => u.Password, hashedPassword);
        var result = await collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteUser(string id)
    {
        var objectId = ObjectId.Parse(id);
        var filter = Builders<User>.Filter.Eq(u => u.Id, objectId);
        await collection.DeleteOneAsync(filter);
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            }),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings.ExpirationInMinutes),
            Issuer = jwtSettings.Issuer,
            Audience = jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
