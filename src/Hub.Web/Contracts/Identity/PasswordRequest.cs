namespace Hub.Web.Contracts.Identity;

public record PasswordRequest(string CurrentPassword, string NewPassword);
