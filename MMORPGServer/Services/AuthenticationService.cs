using MMORPGServer.Interfaces;

namespace MMORPGServer.Services
{
    public sealed class AuthenticationService : IAuthenticationService
    {
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(ILogger<AuthenticationService> logger)
        {
            _logger = logger;
        }

        public async ValueTask<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            await Task.Delay(10);

            if (username == "test" && password == "test")
            {
                _logger.LogInformation("Authentication successful for user: {Username}", username);
                return new AuthenticationResult(true, 12345);
            }

            _logger.LogWarning("Authentication failed for user: {Username}", username);
            return new AuthenticationResult(false, 0, "Invalid credentials");
        }

        public ValueTask<bool> ValidateSessionAsync(uint userId, string sessionToken)
        {
            return ValueTask.FromResult(true);
        }

        public ValueTask<string> CreateSessionTokenAsync(uint userId)
        {
            return ValueTask.FromResult(Guid.NewGuid().ToString());
        }

        public ValueTask RevokeSessionAsync(uint userId)
        {
            return ValueTask.CompletedTask;
        }
    }
}
