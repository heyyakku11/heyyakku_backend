using Microsoft.Extensions.Logging;
using Yakku.Application.Auth.Interfaces;

namespace Yakku.Infrastructure.Email
{
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendOtpAsync(string email, string otp, CancellationToken cancellationToken = default)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("OTP generated for {Email}. Email delivery is not configured.", email);
                _logger.LogInformation("Development OTP for {Email}: {Otp}", email, otp);
            }
            else
            {
                _logger.LogInformation("OTP dispatched for {Email}", email);
            }

            return Task.CompletedTask;
        }
    }
}
