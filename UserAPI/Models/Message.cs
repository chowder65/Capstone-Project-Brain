using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserAPI.Models;

public class Message
{
    [BsonElement("text")]
    public string Text { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    public Message(string text)
    {
        Text = text;
        Timestamp = DateTime.UtcNow;
    }
}