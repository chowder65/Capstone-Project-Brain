using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace UserAPI.Models;

public class Chat
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    [BsonElement("userEmail")]
    public string UserEmail { get; set; }
    
    [BsonElement("chatName")]
    public string ChatName { get; set; }

    [BsonElement("messages")]
    public List<Message> Messages { get; set; } = new List<Message>();

    public Chat(string userEmail, string chatName)
    {
        Id = ObjectId.GenerateNewId().ToString();
        UserEmail = userEmail;
        ChatName = chatName ?? $"Chat {ObjectId.GenerateNewId().ToString().Substring(18)}";
    }

    public Chat() { }
}