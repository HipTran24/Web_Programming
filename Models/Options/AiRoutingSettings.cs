namespace Web_Project.Models
{
    public class AiRoutingSettings
    {
        public string PrimaryTextProvider { get; set; } = "gemini";

        public string PrimaryVisionProvider { get; set; } = "gemini";

        public bool EnforceDailyTokenBudget { get; set; } = false;

        public int ApproxCharsPerToken { get; set; } = 4;

        public int TextOutputTokenBudget { get; set; } = 1500;

        public int QuizOutputTokenBudget { get; set; } = 2200;

        public int ImageOutputTokenBudget { get; set; } = 900;

        public int GeminiDailyTokenBudget { get; set; } = 0;

        public int GroqDailyTokenBudget { get; set; } = 0;

        public int MinReservedTokensPerProvider { get; set; } = 0;

        public bool EnableProviderHealthSwitch { get; set; } = true;

        public int SlowRequestThresholdMs { get; set; } = 9000;

        public int SlowRequestStreakThreshold { get; set; } = 1;

        public int ConsecutiveFailureThreshold { get; set; } = 2;

        public int ProviderCooldownSeconds { get; set; } = 45;

        public bool PreferFastestHealthyProvider { get; set; } = true;

        public int ProviderExecutionTimeoutMs { get; set; } = 12000;
    }
}
