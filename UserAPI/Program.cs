using DotNetEnv;
using UserAPI.Controllers;
using UserAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AdminAPI.Controllers;
using Microsoft.AspNetCore.Mvc;



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
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

UserController controller = new UserController(jwtSettings);

ChatController chatController = new ChatController(jwtSettings);

app.MapPost("/User/Create", async (User user) =>
{
    await controller.CreateUser(user);
    return Results.Ok("User created successfully.");
});

app.MapGet("/User/GetByEmail", async (string email) =>
{
    var user = await controller.GetUser(email);
    return user is not null ? Results.Ok(user) : Results.NotFound("User not found.");
});

app.MapPost("/User/LogIn", async (string email, string password) =>
{
    var token = await controller.LogIn(email, password);
    return token is not null ? Results.Ok(new { Token = token }) : Results.Unauthorized();
});

app.MapPut("/User/ChangePassword", async (string id, string newPassword, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        var success = await controller.ChangePassword(id, newPassword, token);
        return success ? Results.Ok("Password changed successfully.") : Results.NotFound("User not found.");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapDelete("/User/Delete", async (string email, string password, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        await controller.DeleteUser(email, password, token);
        return Results.Ok("User deleted successfully.");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/User/Chats", async (HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        var userId = chatController.ValidateToken(token);
        var userChats = await chatController.GetChatsByUserId(userId);
        return Results.Ok(userChats);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});



app.MapPost("/User/StartChat", async (HttpContext httpContext, [FromBody] ChatRequest request) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        var userId = chatController.ValidateToken(token);
        var chatId = await chatController.StartNewChat(userId, token, request.ChatName);
        return Results.Ok(new { ChatId = chatId });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/User/Chat/AddMessage", async ([FromBody] AddMessageRequest request, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        await chatController.AddMessageToChat(request.ChatId, request.Message, token);
        return Results.Ok("Message added successfully.");
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/User/Chat/History", async ([FromQuery] string chatId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    try
    {
        var chat = await chatController.GetChatHistory(chatId, token);
        return chat is not null ? Results.Ok(chat) : Results.NotFound("Chat not found.");
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});


AdminController adminController = new AdminController(jwtSettings);

app.MapPost("/Admin/Create", async (Admin admin) =>
{
    await adminController.CreateAdmin(admin);
    return Results.Ok("Admin created successfully.");
});

app.MapGet("/Admin/GetByEmail", async (string email) =>
{
    var admin = await adminController.GetAdmin(email);
    return admin is not null ? Results.Ok(admin) : Results.NotFound("Admin not found.");
});

app.MapPost("/Admin/LogIn", async (string email, string password) =>
{
    var token = await adminController.LogIn(email, password);
    return token is not null ? Results.Ok(new { Token = token }) : Results.Unauthorized();
});

app.MapPut("/Admin/ChangePassword", async (string id, string newPassword, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        var success = await adminController.ChangePassword(id, newPassword, token);
        return success ? Results.Ok("Password changed successfully.") : Results.NotFound("Admin not found.");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapDelete("/Admin/Delete", async (string email, string password, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        await adminController.DeleteAdmin(email, password, token);
        return Results.Ok("Admin deleted successfully.");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/Admin/GetUser", async (string userId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        var user = await adminController.GetUser(userId, token);
        return user is not null ? Results.Ok(user) : Results.NotFound("User not found.");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPut("/Admin/UpdateUser", async (string userId, User updatedUser, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        var success = await adminController.UpdateUser(userId, updatedUser, token);
        return success ? Results.Ok("User updated successfully.") : Results.NotFound("User not found.");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/Admin/Chat/History", async (string chatId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        chatController.ValidateAdminToken(token);
        var chat = await chatController.GetChatHistory(chatId, token);
        return chat is not null ? Results.Ok(chat) : Results.NotFound("Chat not found.");
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/Admin/UserChats", async (string userId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        chatController.ValidateAdminToken(token);
        var userChats = await chatController.GetChatsByUserId(userId);
        return Results.Ok(userChats);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapDelete("/Admin/DeleteChat", async (string chatId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        chatController.ValidateAdminToken(token);
        await chatController.DeleteChat(chatId);
        return Results.Ok("Chat deleted successfully.");
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapDelete("/Admin/DeleteUser", async (string userId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        adminController.ValidateAdminToken(token);
        await adminController.DeleteUser(userId, token);
        return Results.Ok("User and their chats deleted successfully.");
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.Run();

public class ChatRequest
{
    public string ChatName { get; set; }
}

public class AddMessageRequest
{
    public string ChatId { get; set; }
    public string Message { get; set; }
}