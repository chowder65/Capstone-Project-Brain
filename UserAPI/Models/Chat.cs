using MongoDB.Bson;

namespace UserAPI.Models;
public class Chat
{
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();
    public string UserEmail { get; set; }
    public string ChatName { get; set; }
    public List<Message> Messages { get; set; } = new List<Message>();

    public Chat(string userEmail, string chatName)
    {
        UserEmail = userEmail;
        ChatName = chatName ?? $"Chat_{DateTime.UtcNow.Ticks}";
    }
}