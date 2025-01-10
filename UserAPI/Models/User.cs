namespace UserAPI.Models;



public class User{
    public int Id { get; set;}
    public required string UserName { get; set;}
    public required string Password { get; set;}
    

    public string HashPassword(string password) {
        return HashPassword(password);
    }

    
}