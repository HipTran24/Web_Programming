namespace Web_Project.Models.Dtos.User
{
    public class NotificationSettingsRequest
    {
        public bool NotifyReviewReminder { get; set; }

        public bool NotifyQuizResult { get; set; }

        public bool NotifyProductNews { get; set; }
    }
}
