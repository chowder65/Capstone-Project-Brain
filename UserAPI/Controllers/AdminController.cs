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
    private readonly IMongoCollection<Admin> adminCollection;
    private readonly IMongoCollection<User> userCollection;
    private readonly IMongoCollection<Chat> chatCollection;
    private readonly JwtSettings jwtSettings;

    public AdminController(JwtSettings jwtSettings)
    {
        this.jwtSettings = jwtSettings;
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("AdminAPI");
        adminCollection = database.GetCollection<Admin>("Admins");
        userCollection = database.GetCollection<User>("Users");
        chatCollection = database.GetCollection<Chat>("Chats");
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public async Task CreateAdmin(Admin admin)
    {
        admin.Id = ObjectId.GenerateNewId();
        admin.Password = HashPassword(admin.Password);
        await adminCollection.InsertOneAsync(admin);
    }

    public async Task<Admin?> GetAdmin(string userName)
    {
        var filter = Builders<Admin>.Filter.Eq("UserName", userName);
        return await adminCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<string?> LogIn(string userName, string password)
    {
        var admin = await GetAdmin(userName);
        password = password.Trim();
        if (admin != null)
        {
            Console.WriteLine($"Input Password: {password}");
            Console.WriteLine($"Stored Password: {admin.Password}");
            Console.WriteLine($"Password Match: {BCrypt.Net.BCrypt.Verify(password, admin.Password)}");

            if (BCrypt.Net.BCrypt.Verify(password, admin.Password))
            {
                return GenerateJwtToken(admin);
            }
        }
        return null;
    }
    public async Task<bool> ChangePassword(string id, string newPassword, string token)
    {
        var adminIdFromToken = ValidateToken(token);

        if (adminIdFromToken != id)
        {
            throw new UnauthorizedAccessException("You are not authorized to change this password.");
        }

        var objectId = ObjectId.Parse(id);
        var filter = Builders<Admin>.Filter.Eq(a => a.Id, objectId);
        var hashedPassword = HashPassword(newPassword);
        var update = Builders<Admin>.Update.Set(a => a.Password, hashedPassword);
        var result = await adminCollection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteAdmin(string username, string password, string token)
    {
        var adminIdFromToken = ValidateToken(token);

        var admin = await GetAdmin(username);
        if (admin == null || !BCrypt.Net.BCrypt.Verify(password, admin.Password))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (admin.Id.ToString() != adminIdFromToken)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this admin.");
        }

        var filter = Builders<Admin>.Filter.Eq(a => a.Id, admin.Id);
        await adminCollection.DeleteOneAsync(filter);
    }

    public async Task DeleteUser(string userId, string token)
    {
        ValidateAdminToken(token);

        var objectId = ObjectId.Parse(userId);
        var filter = Builders<User>.Filter.Eq(u => u.Id, objectId);
        await userCollection.DeleteOneAsync(filter);
    }

    public async Task<User?> GetUser(string userId, string token)
    {
        ValidateAdminToken(token);

        var objectId = ObjectId.Parse(userId);
        var filter = Builders<User>.Filter.Eq(u => u.Id, objectId);
        return await userCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateUser(string userId, User updatedUser, string token)
    {
        ValidateAdminToken(token);

        var objectId = ObjectId.Parse(userId);
        var filter = Builders<User>.Filter.Eq(u => u.Id, objectId);
        var update = Builders<User>.Update
            .Set(u => u.Email, updatedUser.Email)
            .Set(u => u.Email, updatedUser.Email)
            .Set(u => u.Password, HashPassword(updatedUser.Password));
        var result = await userCollection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    private string GenerateJwtToken(Admin admin)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.UserName),
                new Claim(ClaimTypes.Role, "Admin") // Add role claim for admin
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

    internal void ValidateAdminToken(string token)
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