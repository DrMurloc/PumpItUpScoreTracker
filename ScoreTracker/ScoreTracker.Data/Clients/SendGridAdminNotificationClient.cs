using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ScoreTracker.Data.Clients
{
    public sealed class SendGridAdminNotificationClient : IAdminNotificationClient
    {
        private readonly SendGridConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public SendGridAdminNotificationClient(IOptions<SendGridConfiguration> options, IMemoryCache cache)
        {
            _configuration = options.Value;
            _cache = cache;
        }

        public async Task NotifyAdmin(string message, CancellationToken cancellationToken)
        {
            await _cache.GetOrCreateAsync($"{nameof(SendGridAdminNotificationClient)}-{nameof(NotifyAdmin)}-{message}",
                async o =>
                {
                    var client = new SendGridClient(_configuration.ApiKey);
                    var emailMessage = MailHelper.CreateSingleEmail(new EmailAddress(_configuration.FromEmail),
                        new EmailAddress(_configuration.ToEmail), "PIU Scores Update", message, string.Empty);
                    await client.SendEmailAsync(emailMessage, cancellationToken);
                    return true;
                });
        }
    }
}