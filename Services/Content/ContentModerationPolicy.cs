namespace Web_Project.Services.Content
{
    public static class ContentModerationPolicy
    {
        public const string PolicyReasonPrefix = "Vi phạm chính sách hệ thống";

        public static bool IsPolicyViolationReason(string? reason)
        {
            var normalized = (reason ?? string.Empty).Trim();
            return normalized.StartsWith(PolicyReasonPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildPolicyViolationReason(IEnumerable<string> flags, string? rationale = null)
        {
            var fragments = (flags ?? Array.Empty<string>())
                .Where(flag => !string.IsNullOrWhiteSpace(flag))
                .Select(flag => flag.Trim().TrimEnd('.', ';'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rationaleText = (rationale ?? string.Empty).Trim().TrimEnd('.', ';');
            if (!string.IsNullOrWhiteSpace(rationaleText))
            {
                fragments.Add(rationaleText);
            }

            if (fragments.Count == 0)
            {
                return $"{PolicyReasonPrefix}.";
            }

            return $"{PolicyReasonPrefix}: {string.Join("; ", fragments)}.";
        }

        public static string EnsurePolicyViolationReason(string? requestedReason, string? fallbackReason = null)
        {
            var normalizedRequested = (requestedReason ?? string.Empty).Trim();
            if (IsPolicyViolationReason(normalizedRequested))
            {
                return normalizedRequested;
            }

            var normalizedFallback = (fallbackReason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedRequested))
            {
                return IsPolicyViolationReason(normalizedFallback)
                    ? normalizedFallback
                    : $"{PolicyReasonPrefix}.";
            }

            return $"{PolicyReasonPrefix}: {normalizedRequested.TrimEnd('.', ';')}.";
        }
    }
}
