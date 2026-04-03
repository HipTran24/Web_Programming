namespace Web_Project.Models.Dtos.User
{
    public class NotificationSettingsResponse
    {
        public bool NotifyReviewReminder { get; set; } = true;

        public bool NotifyQuizResult { get; set; }

        public bool NotifyProductNews { get; set; } = true;
    }
}
