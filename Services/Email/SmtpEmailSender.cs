using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using Web_Project.Models;

namespace Web_Project.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly SmtpSettings _smtpSettings;

        public SmtpEmailSender(IOptions<SmtpSettings> smtpOptions, ILogger<SmtpEmailSender> logger)
        {
            _logger = logger;
            _smtpSettings = smtpOptions.Value;
        }

        public async Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string textBody,
            CancellationToken cancellationToken)
        {
            var host = (_smtpSettings.Host ?? string.Empty).Trim();
            var fromEmail = (_smtpSettings.FromEmail ?? string.Empty).Trim();
            var fromName = (_smtpSettings.FromName ?? string.Empty).Trim();
            var username = (_smtpSettings.Username ?? string.Empty).Trim();
            var password = RemoveWhitespace(_smtpSettings.Password ?? string.Empty);

            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogError("SMTP host is missing. Cannot send email to {ToEmail}.", toEmail);
                throw new InvalidOperationException("SMTP host is not configured.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toEmail));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));

            using var smtpClient = new SmtpClient(host, _smtpSettings.Port)
            {
                EnableSsl = _smtpSettings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                smtpClient.Credentials = new NetworkCredential(username, password);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await smtpClient.SendMailAsync(message, cancellationToken);
        }

        private static string RemoveWhitespace(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }
    }
}
