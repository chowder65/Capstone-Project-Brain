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
using RabbitMQ.Client.Events;
using UserAPI;

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
builder.Services.AddHostedService<RabbitMQConsumerService>();
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory { HostName = "rabbitmq", UserName = "guest", Password = "guest" };
    return factory.CreateConnection();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();

// --- User Endpoints ---
app.MapPost("/User/Create", async (User user, UserController controller) =>
{
    await controller.CreateUser(user);
    return Results.Ok("User created successfully.");
});

app.MapGet("/User/GetByEmail", async (string email, UserController controller) =>
{
    var user = await controller.GetUser(email);
    return user is not null ? Results.Ok(user) : Results.NotFound("User not found.");
});

app.MapPost("/User/LogIn", async (string email, string password, UserController controller) =>
{
    var token = await controller.LogIn(email, password);
    return token is not null ? Results.Ok(new { Token = token }) : Results.Unauthorized();
});

app.MapPut("/User/ChangePassword", async (string id, string newPassword, HttpContext httpContext, UserController controller) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        var success = await controller.ChangePassword(id, newPassword, token);
        return success ? Results.Ok("Password changed successfully.") : Results.NotFound("User not found.");
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    catch (Exception ex) { return Results.BadRequest(ex.Message); }
});

app.MapDelete("/User/Delete", async (string email, string password, HttpContext httpContext, UserController controller) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        await controller.DeleteUser(email, password, token);
        return Results.Ok("User deleted successfully.");
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    catch (Exception ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/User/Chats", async (HttpContext httpContext, ChatController chatController) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        var userId = chatController.ValidateToken(token);
        var userChats = await chatController.GetChatsByUserId(userId);
        return Results.Ok(userChats);
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
});

app.MapPost("/User/StartChat", async (HttpContext httpContext, [FromBody] ChatRequest request, ChatController chatController) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        var userId = chatController.ValidateToken(token);
        var chatId = await chatController.StartNewChat(userId, token, request.ChatName);
        return Results.Ok(new { ChatId = chatId });
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
});

app.MapPost("/User/Chat/SendMessageWithLLM", async ([FromBody] SendMessageRequest request, HttpContext httpContext, ChatController chatController, IConnection rabbitMqConnection) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        using var channel = rabbitMqConnection.CreateModel();
        channel.QueueDeclare(queue: "userapi_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var message = JsonSerializer.Serialize(new { ChatId = request.ChatId, ChatRequest = request.ChatRequest });
        var body = Encoding.UTF8.GetBytes(message);
        var correlationId = Guid.NewGuid().ToString();
        var replyQueue = channel.QueueDeclare(durable: false, exclusive: true, autoDelete: true).QueueName;

        var props = channel.CreateBasicProperties();
        props.CorrelationId = correlationId;
        props.ReplyTo = replyQueue;
        props.Headers = new Dictionary<string, object> { { "Token", token } };

        var tcs = new TaskCompletionSource<string>();
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (sender, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                tcs.TrySetResult(response);
            }
        };
        channel.BasicConsume(queue: replyQueue, autoAck: true, consumer: consumer);

        channel.BasicPublish(exchange: "", routingKey: "userapi_queue", basicProperties: props, body: body);
        Console.WriteLine($"Published to userapi_queue with CorrelationId: {correlationId}");

        var responseTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
        if (responseTask == tcs.Task)
        {
            var jsonResponse = tcs.Task.Result;
            var llmResponse = JsonSerializer.Deserialize<ChatController.LLMResponse>(jsonResponse);
            await chatController.AddMessageToChat(request.ChatId, llmResponse.Response, token); // Store in DB
            return Results.Ok(new { Response = llmResponse.Response, DetectedEmotion = llmResponse.DetectedEmotion });
        }
        return Results.StatusCode(504); 
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/User/Chat/History", async ([FromQuery] string chatId, HttpContext httpContext, ChatController chatController) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        var chat = await chatController.GetChatHistory(chatId, token);
        return chat is not null ? Results.Ok(chat) : Results.NotFound("Chat not found.");
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
});

app.MapPost("/Admin/Create", async (Admin admin, AdminController adminController) =>
{
    await adminController.CreateAdmin(admin);
    return Results.Ok("Admin created successfully.");
});

// app.MapDelete("/Admin/DeleteChat", async (string chatId, string userEmail, HttpContext httpContext, AdminController adminController) =>
// {
//     var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
//     try
//     {
//         adminController.ValidateAdminToken(token);
//         await adminController.DeleteChat(chatId, userEmail);
//         return Results.Ok("Chat deleted successfully.");
//     }
//     catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
//     catch (Exception ex) { return Results.BadRequest(ex.Message); }
// });

app.MapDelete("/Admin/DeleteUser", async (string userId, HttpContext httpContext, AdminController adminController) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        adminController.ValidateAdminToken(token);
        await adminController.DeleteUser(userId, token);
        return Results.Ok("User and their chats deleted successfully.");
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    catch (Exception ex) { return Results.BadRequest(ex.Message); }
});

app.Run();

public class SendMessageRequest
{
    public string ChatId { get; set; }
    public ChatRequestBody ChatRequest { get; set; }
}

public class ChatRequest
{
    public string ChatName { get; set; }
}