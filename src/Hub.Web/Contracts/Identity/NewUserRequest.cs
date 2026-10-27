namespace Hub.Web.Contracts.Identity;

public record NewUserRequest(string Email, string Name, string Password, string Role, string Department);
