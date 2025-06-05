namespace MMORPGServer.Interfaces
{
    public interface IAuthenticationService
    {
        ValueTask<AuthenticationResult> AuthenticateAsync(string username, string password);
        ValueTask<bool> ValidateSessionAsync(uint userId, string sessionToken);
        ValueTask<string> CreateSessionTokenAsync(uint userId);
        ValueTask RevokeSessionAsync(uint userId);
    }
    public record AuthenticationResult(bool Success, uint UserId, string? ErrorMessage = null);
}