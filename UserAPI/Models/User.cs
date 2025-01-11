using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace UserAPI.Models;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; } // Ideally store only hashed passwords

    private static readonly string ConnectionString = "mongodb://localhost:27017/";
    private static readonly MongoClient Client = new MongoClient(ConnectionString);
    private static readonly IMongoCollection<BsonDocument> collection =
        Client.GetDatabase("UserAPI").GetCollection<BsonDocument>("Users");

    public User() {}

    public string HashPassword(string password)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
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

        // Deserialize the BSON document into the User model
        if (userDocument != null)
        {
            return BsonSerializer.Deserialize<User>(userDocument);
        }
        return null;
    }


    public async Task UpdateUsername(int id, string newUserName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Id", id);
        var update = Builders<BsonDocument>.Update.Set("UserName", newUserName);
        await collection.UpdateOneAsync(filter, update);
    }

    public async Task DeleteUser(int id)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Id", id);
        await collection.DeleteOneAsync(filter);
    }

    public async Task ChangePassword(int id, string newPassword)
    {
        var hashedPassword = HashPassword(newPassword);
        var filter = Builders<BsonDocument>.Filter.Eq("Id", id);
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
            return storedHashedPassword == HashPassword(password);
        }
        return false;
    }
}
