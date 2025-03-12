using DotNetEnv;
using UserAPI.Controllers;
using UserAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AdminAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UserAPI.Services;
using StackExchange.Redis;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<JwtSettings>(jwtSettings);
builder.Services.AddSingleton<UserController>();
builder.Services.AddSingleton<ChatController>();
builder.Services.AddSingleton<AdminController>();
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
    return factory.CreateConnection();
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = new ConfigurationOptions
    {
        EndPoints = { "localhost:6379" },
        AbortOnConnectFail = false,
        ConnectTimeout = 5000,
        SyncTimeout = 5000
    };
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddHostedService<RabbitMQConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Commented out for local testing
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();

void PublishMessage(IModel channel, string messageType, object payload, string token, string correlationId = null)
{
    var props = channel.CreateBasicProperties();
    props.CorrelationId = correlationId ?? Guid.NewGuid().ToString();
    props.Headers = new Dictionary<string, object>
    {
        { "Token", Encoding.UTF8.GetBytes(token) },
        { "MessageType", Encoding.UTF8.GetBytes(messageType) }
    };
    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
    channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
    Console.WriteLine($"Published {messageType} to userapi_queue with CorrelationId: {props.CorrelationId}");
}

app.MapPost("/User/Create", async (Account user, UserController userController) =>
{
    var factory = new ConnectionFactory() { HostName = "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    var correlationId = Guid.NewGuid().ToString();
    var props = channel.CreateBasicProperties();
    props.CorrelationId = correlationId;
    props.Headers = new Dictionary<string, object> { ["MessageType"] = "create_user" };

    var message = JsonSerializer.Serialize(user);
    var body = Encoding.UTF8.GetBytes(message);

    channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
    Console.WriteLine($"Published create_user to userapi_queue with CorrelationId: {correlationId}");
    return Results.Accepted($"/api/Result?correlationId={correlationId}", new { CorrelationId = correlationId });
});

app.MapGet("/User/GetByEmail", (string email, IConnection rabbitMqConnection) =>
{
    try
    {
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "get_user_by_email", new { email }, "", correlationId);
        return Results.Json(new { Message = "User lookup queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/User/LogIn", (string email, string password, IConnection rabbitMqConnection) =>
{
    try
    {
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "login", new { email, password }, "", correlationId);
        return Results.Json(new { Message = "Login queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPut("/User/ChangePassword", (string id, string newPassword, HttpContext httpContext, IConnection rabbitMqConnection) =>
{
    try
    {
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "change_password", new { id, newPassword }, token, correlationId);
        return Results.Json(new { Message = "Password change queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/User/Delete", (string email, string password, HttpContext httpContext, IConnection rabbitMqConnection) =>
{
    try
    {
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "delete_user", new { email, password }, token, correlationId);
        return Results.Json(new { Message = "User deletion queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/User/Chats", (HttpContext httpContext, IConnection rabbitMqConnection) =>
{
    try
    {
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "get_chats", new { }, token, correlationId);
        return Results.Json(new { Message = "Chat fetch queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/Chat/StartChat", async (ChatRequest chatRequest, ChatController chatController, IConfiguration config) =>
{
    var factory = new ConnectionFactory() { HostName = config["RabbitMQ:Host"] ?? "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    var correlationId = Guid.NewGuid().ToString();
    var props = channel.CreateBasicProperties();
    props.CorrelationId = correlationId;
    props.Headers = new Dictionary<string, object> 
    { 
        ["MessageType"] = "start_new_chat",
        ["Token"] = Encoding.UTF8.GetBytes(config["Token"] ?? "")
    };

    var message = JsonSerializer.Serialize(chatRequest);
    var body = Encoding.UTF8.GetBytes(message);

    channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
    Console.WriteLine($"Published start_new_chat to userapi_queue with CorrelationId: {correlationId}");
    return Results.Accepted($"/api/Result?correlationId={correlationId}", new { CorrelationId = correlationId });
});

app.MapPost("/Chat/SendMessage", async (SendMessageRequest sendMessageRequest, ChatController chatController, IConfiguration config) =>
{
    var factory = new ConnectionFactory() { HostName = config["RabbitMQ:Host"] ?? "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    var correlationId = Guid.NewGuid().ToString();
    var props = channel.CreateBasicProperties();
    props.CorrelationId = correlationId;
    props.Headers = new Dictionary<string, object> 
    { 
        ["MessageType"] = "send_message",
        ["Token"] = Encoding.UTF8.GetBytes(config["Token"] ?? "")
    };

    var message = JsonSerializer.Serialize(sendMessageRequest);
    var body = Encoding.UTF8.GetBytes(message);

    channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
    Console.WriteLine($"Published send_message to userapi_queue with CorrelationId: {correlationId}");
    return Results.Accepted($"/api/Result?correlationId={correlationId}", new { CorrelationId = correlationId });
});

app.MapGet("/User/Chat/History", ([FromQuery] string chatId, HttpContext httpContext, IConnection rabbitMqConnection) =>
{
    try
    {
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "get_chat_history", new { chatId }, token, correlationId);
        return Results.Json(new { Message = "Chat history fetch queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/Admin/Create", (Account admin, IConnection rabbitMqConnection) =>
{
    try
    {
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "create_admin", admin, "", correlationId);
        return Results.Json(new { Message = "Admin creation queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/Admin/LogIn", (string email, string password, IConnection rabbitMqConnection) =>
{
    try
    {
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "admin_login", new { email, password }, "", correlationId);
        return Results.Json(new { Message = "Admin login queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/Admin/DeleteChat", (string chatId, string userEmail, HttpContext httpContext, IConnection rabbitMqConnection) =>
{
    try
    {
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "delete_chat", new { chatId, userEmail }, token, correlationId);
        return Results.Json(new { Message = "Chat deletion queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/Admin/DeleteUser", async (string userId, string token, AdminController adminController) =>
{
    var factory = new ConnectionFactory() { HostName = "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    var correlationId = Guid.NewGuid().ToString();
    var props = channel.CreateBasicProperties();
    props.CorrelationId = correlationId;
    props.Headers = new Dictionary<string, object> { ["MessageType"] = "delete_user", ["Token"] = Encoding.UTF8.GetBytes(token) };

    var message = JsonSerializer.Serialize(new { userId });
    var body = Encoding.UTF8.GetBytes(message);

    channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
    Console.WriteLine($"Published delete_user to userapi_queue with CorrelationId: {correlationId}");
    return Results.Accepted($"/api/Result?correlationId={correlationId}", new { CorrelationId = correlationId });
});

app.MapGet("/Admin/GetUserChats", (string userEmail, HttpContext httpContext, IConnection rabbitMqConnection) =>
{
    try
    {
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false);
        var correlationId = Guid.NewGuid().ToString();
        PublishMessage(channel, "get_user_chats", new { userEmail }, token, correlationId);
        return Results.Json(new { Message = "User chats fetch queued", CorrelationId = correlationId }, statusCode: 202);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/Admin/GetAllUsers", async (string token, AdminController adminController) =>
{
    var factory = new ConnectionFactory() { HostName = "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    var correlationId = Guid.NewGuid().ToString();
    var props = channel.CreateBasicProperties();
    props.CorrelationId = correlationId;
    props.Headers = new Dictionary<string, object> { ["MessageType"] = "get_all_users", ["Token"] = Encoding.UTF8.GetBytes(token) };

    var message = "{}";
    var body = Encoding.UTF8.GetBytes(message);

    channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
    Console.WriteLine($"Published get_all_users to userapi_queue with CorrelationId: {correlationId}");
    return Results.Accepted($"/api/Result?correlationId={correlationId}", new { CorrelationId = correlationId });
});

app.MapGet("/api/Result", async (string correlationId, IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    var result = await db.StringGetAsync(correlationId);
    Console.WriteLine($"Redis value for {correlationId}: {result}");
    if (!result.HasValue)
    {
        Console.WriteLine($"No value found in Redis for {correlationId}");
        return Results.Ok(new { Status = "pending" }); // Changed from NoContent
    }
    try
    {
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(result.ToString());
        string status = deserialized["Status"]?.ToString();
        Console.WriteLine($"Deserialized Status: '{status}'");
        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            object resultData = deserialized["Result"];
            Console.WriteLine($"Deserialized Result: {resultData}");
            // Do not delete key here: await db.KeyDeleteAsync(correlationId);
            return Results.Ok(new { Status = status, Result = resultData });
        }
        return Results.Ok(new { Status = "pending" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Deserialization error for {correlationId}: {ex.Message}");
        return Results.Ok(new { Status = "pending" });
    }
});

app.Run();