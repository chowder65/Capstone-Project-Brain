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
    private readonly IMongoCollection<BsonDocument> collection;
    private readonly JwtSettings jwtSettings;

    public UserController(JwtSettings jwtSettings)
    {
        jwtSettings = jwtSettings;
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        var client = new MongoClient(connectionString);
        collection = client.GetDatabase("UserAPI").GetCollection<BsonDocument>("Users");
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public async Task CreateUser(User user)
    {
        user.Id = await GetNextSequenceValue("UserId");

        var bsonDocument = user.ToBsonDocument();
        await collection.InsertOneAsync(bsonDocument);
    }

    public async Task<User?> GetUser(string userName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("UserName", userName);
        var userDocument = await collection.Find(filter).FirstOrDefaultAsync();
        return userDocument != null ? BsonSerializer.Deserialize<User>(userDocument) : null;
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
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id));
        var hashedPassword = HashPassword(newPassword);
        var update = Builders<BsonDocument>.Update.Set("Password", hashedPassword);
        var result = await collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteUser(string id)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id));
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
                new Claim(ClaimTypes.NameIdentifier, user.Id),
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
    
    public async Task<string> GetNextSequenceValue(string sequenceName)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        var client = new MongoClient(connectionString);
        var counterCollection = client.GetDatabase("UserAPI").GetCollection<BsonDocument>("Counters");

        var filter = Builders<BsonDocument>.Filter.Eq("_id", sequenceName);
        var update = Builders<BsonDocument>.Update.Inc("sequence_value", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After,
            IsUpsert = true
        };

        var result = await counterCollection.FindOneAndUpdateAsync(filter, update, options);

        return result["sequence_value"].AsString;
    }
}