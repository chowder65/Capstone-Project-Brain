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
        user.Password = HashPassword(user.Password);
        user.Id = ObjectId.GenerateNewId();
        await collection.InsertOneAsync(user);
    }

    public async Task<User?> GetUser(string email)
    {
        var filter = Builders<User>.Filter.Eq("Email", email);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<string?> LogIn(string email, string password)
    {
        var user = await GetUser(email);
        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            return GenerateJwtToken(user);
        }
        return null;
    }

    public async Task<bool> ChangePassword(string id, string newPassword, string token)
    {
        var userIdFromToken = ValidateToken(token);

        if (userIdFromToken != id)
        {
            throw new UnauthorizedAccessException("You are not authorized to change this password.");
        }

        var objectId = ObjectId.Parse(id);
        var filter = Builders<User>.Filter.Eq(u => u.Id, objectId);
        var hashedPassword = HashPassword(newPassword);
        var update = Builders<User>.Update.Set(u => u.Password, hashedPassword);
        var result = await collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteUser(string email, string password, string token)
    {
        var userIdFromToken = ValidateToken(token);

        var user = await GetUser(email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.Id.ToString() != userIdFromToken)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this user.");
        }

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
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
                new Claim(ClaimTypes.Name, user.Email)
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

            return principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch (Exception)
        {
            throw new UnauthorizedAccessException("Invalid or expired token.");
        }
    }
}
