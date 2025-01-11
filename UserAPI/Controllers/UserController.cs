using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UserAPI.Models;
using BCrypt;

namespace UserAPI.Controllers;

public class UserController
{
    
    private static readonly string ConnectionString = "mongodb://localhost:27017/";
    private static readonly MongoClient Client = new MongoClient(ConnectionString);
    private static readonly IMongoCollection<BsonDocument> collection =
        Client.GetDatabase("UserAPI").GetCollection<BsonDocument>("Users");
    
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public async Task CreateUser(User user)
    {
        var bsonDocument = user.ToBsonDocument();
        await collection.InsertOneAsync(bsonDocument);
    }

    public async Task<User?> GetUser(string userName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("UserName", userName);
        var userDocument = await collection.Find(filter).FirstOrDefaultAsync();

        if (userDocument != null)
        {
            return BsonSerializer.Deserialize<User>(userDocument);
        }
        return null;
    }


    public async Task UpdateUsername(int id, string newUserName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        var update = Builders<BsonDocument>.Update.Set("UserName", newUserName);
        await collection.UpdateOneAsync(filter, update);
    }

    public async Task DeleteUser(int id)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        await collection.DeleteOneAsync(filter);
    }

    public async Task ChangePassword(int id, string newPassword)
    {
        var hashedPassword = HashPassword(newPassword);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        var update = Builders<BsonDocument>.Update.Set("Password", hashedPassword);
        await collection.UpdateOneAsync(filter, update);
    }

    public async Task<bool> LogIn(string userName, string password)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("UserName", userName);
        var userDocument = await collection.Find(filter).FirstOrDefaultAsync();

        if (userDocument != null)
        {
            var storedHashedPassword = userDocument["Password"].AsString;
            return BCrypt.Net.BCrypt.Verify(password, storedHashedPassword);
        }
        return false;
    }
}