using DotNetEnv;
using UserAPI.Controllers;
using UserAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AdminAPI.Controllers;

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
app.UseAuthentication();
app.UseAuthorization();

UserController controller = new UserController(jwtSettings);

app.MapPost("/User/Create", async (User user) =>
{
    await controller.CreateUser(user);
    return Results.Ok("User created successfully.");
});

app.MapGet("/User/GetByUserName", async (string userName) =>
{
    var user = await controller.GetUser(userName);
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

app.MapDelete("/User/Delete", async (string username, string password, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        await controller.DeleteUser(username, password, token);
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

AdminController adminController = new AdminController(jwtSettings);

app.MapPost("/Admin/Create", async (Admin admin) =>
{
    await adminController.CreateAdmin(admin);
    return Results.Ok("Admin created successfully.");
});

app.MapGet("/Admin/GetByUserName", async (string userName) =>
{
    var admin = await adminController.GetAdmin(userName);
    return admin is not null ? Results.Ok(admin) : Results.NotFound("Admin not found.");
});

app.MapPost("/Admin/LogIn", async (string userName, string password) =>
{
    var token = await adminController.LogIn(userName, password);
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

app.MapDelete("/Admin/Delete", async (string username, string password, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        await adminController.DeleteAdmin(username, password, token);
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

app.MapDelete("/Admin/DeleteUser", async (string userId, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    try
    {
        await adminController.DeleteUser(userId, token);
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

// app.MapGet("/Admin/GetUserChats", async (string userId, HttpContext httpContext) =>
// {
//     var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
//
//     try
//     {
//         var chats = await adminController.GetUserChats(userId, token);
//         return Results.Ok(chats);
//     }
//     catch (UnauthorizedAccessException ex)
//     {
//         return Results.Unauthorized();
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(ex.Message);
//     }
// });

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

app.Run();
