using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserAPI.Models;

public class Message
{
    public string Text { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsUser { get; set; }

    public Message(string text)
    {
        Text = text;
    }
}