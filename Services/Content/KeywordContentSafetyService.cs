using System.Text.RegularExpressions;

namespace Web_Project.Services.Content
{
    public sealed class KeywordContentSafetyService : IContentSafetyService
    {
        private static readonly Regex PoliticalPattern = new(
            @"\b(ch[ií]nh\s*tr[iị]|b[aầ]u\s*c[uử]|qu[oố]c\s*h[oộ]i|ch[ií]nh\s*ph[ủu]|nh[aà]\s*nư[oớ]c|t[oổ]ng\s*th[oố]ng|th[ủu]\s*tư[ớo]ng|đ[aả]ng|đ[aạ]i\s*bi[ểe]u|ch[ếe]\s*đ[ộo]|tuy[eê]n\s*truy[ềe]n|bi[ểe]u\s*t[ìi]nh|ph[aả]n\s*đ[ộo]ng|l[ãa]nh\s*đ[ạa]o)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex UserTargetingPattern = new(
            @"\b(c[aá]ng\s*k[ií]ch|t[aấ]n\s*c[oô]ng\s*c[aá]\s*nh[aâ]n|b[oô]i\s*nh[oọ]|x[uú]c\s*ph[aạ]m|qu[ấa]y\s*r[ốo]i|đe\s*d[oọ]a|l[oộ]\s*th[oô]ng\s*tin\s*c[aá]\s*nh[aâ]n|doxx|nh[aắ]m\s*v[aà]o\s*(ngư[oờ]i\s*d[uù]ng|c[aá]\s*nh[aâ]n)|k[ỳy]\s*th[iị]|th[ùu]\s*hằn|c[oô]ng\s*k[ií]ch\s*ngư[oờ]i\s*d[uù]ng)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Task<ContentSafetyReview> AnalyzeAsync(
            string extractedText,
            string summary,
            IReadOnlyCollection<string> keyPoints,
            string fileName,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            var corpus = string.Join(
                "\n",
                [
                    extractedText ?? string.Empty,
                    summary ?? string.Empty,
                    string.Join("\n", keyPoints ?? Array.Empty<string>()),
                    fileName ?? string.Empty,
                    sourceUrl ?? string.Empty,
                ]);

            if (string.IsNullOrWhiteSpace(corpus))
            {
                return Task.FromResult(ContentSafetyReview.Safe);
            }

            var flags = new List<string>();
            if (PoliticalPattern.IsMatch(corpus))
            {
                flags.Add("Nội dung có dấu hiệu liên quan đến chủ đề chính trị nhạy cảm.");
            }

            if (UserTargetingPattern.IsMatch(corpus))
            {
                flags.Add("Nội dung có dấu hiệu nhắm trực tiếp vào cá nhân hoặc người dùng.");
            }

            if (flags.Count == 0)
            {
                return Task.FromResult(ContentSafetyReview.Safe);
            }

            var joinedFlags = string.Join(" ", flags);
            return Task.FromResult(new ContentSafetyReview
            {
                RequiresAdminReview = true,
                BlocksSummarization = true,
                IsPolicyViolation = true,
                ModerationStatus = "Pending",
                WarningTitle = "Nội dung bị chặn để chờ kiểm duyệt chính sách",
                WarningMessage = $"{joinedFlags} Hệ thống đã tạm chặn phân tích và chuyển nội dung sang trạng thái chờ admin duyệt theo chính sách hệ thống.",
                ModerationReason = ContentModerationPolicy.BuildPolicyViolationReason(flags, "Bị bộ lọc dự phòng phát hiện có dấu hiệu vi phạm chính sách hệ thống."),
                ReviewSource = "KeywordFallback",
                Flags = flags,
            });
        }
    }
}
