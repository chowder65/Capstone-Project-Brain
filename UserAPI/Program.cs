using UserAPI.Controllers;
using UserAPI.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

UserController controller = new UserController();



app.MapPost("/User/Create", async (User user) =>
{
    var hashedPassword = controller.HashPassword(user.Password);
    user.Password = hashedPassword;

    await controller.CreateUser(user);
    return Results.Ok("User created successfully.");
});

app.MapGet("/User/GetByUserName", async (string userName) =>
{
    var user = new User();
    var userDocument = await controller.GetUser(userName);

    return userDocument is not null ? Results.Ok(userDocument) : Results.NotFound("User not found.");
});

app.MapPut("/User/UpdateUserName", async (int id, string newUserName) =>
{
    var user = new User();
    await controller.UpdateUsername(id, newUserName);
    return Results.Ok("Username updated successfully.");
});

app.MapDelete("/User/Delete", async (int id) =>
{
    var user = new User();
    await controller.DeleteUser(id);
    return Results.Ok("User deleted successfully.");
});

app.MapPost("/User/LogIn", async (string userName, string password) =>
{
    var user = new User();
    var loginSuccess = await controller.LogIn(userName, password);

    return loginSuccess ? Results.Ok("Login successful.") : Results.Unauthorized();
});

app.Run();
