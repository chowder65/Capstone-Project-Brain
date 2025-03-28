﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserAPI.Models;

[BsonIgnoreExtraElements]
public class Admin
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId Id { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}