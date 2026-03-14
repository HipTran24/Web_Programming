namespace Web_Project.Models
{
    public class EmailOtpSettings
    {
        public int ExpireMinutes { get; set; } = 10;

        public int MaxAttempts { get; set; } = 5;
    }
}
