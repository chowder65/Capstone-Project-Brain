using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace UserAPI.Models;

public class Chat
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId Id { get; set; }
    
    [BsonElement("userEmail")]
    public string UserEmail { get; set; }

    [BsonElement("messages")]
    public List<Message> Messages { get; set; } = new List<Message>();

    public Chat(string userEmail)
    {
        UserEmail = userEmail;
    }

    public Chat() { }
}