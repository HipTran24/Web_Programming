namespace Web_Project.Services.Email
{
    public interface IEmailSender
    {
        Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string textBody,
            CancellationToken cancellationToken);
    }
}
