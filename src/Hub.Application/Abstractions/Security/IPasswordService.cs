namespace Hub.Application.Abstractions.Security;

public interface IPasswordService
{
    string Hash(HubUser user, string password);
    bool Verify(HubUser user, string password);
}
