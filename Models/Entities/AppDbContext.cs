using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Web_Project.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Content> Contents { get; set; }
        public DbSet<AIProcess> AIProcesses { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<UserAnswer> UserAnswers { get; set; }
        public DbSet<StudyStatistic> StudyStatistics { get; set; }
        public DbSet<AISystemLog> AISystemLogs { get; set; }
        public DbSet<GuestSession> GuestSessions { get; set; }
        public DbSet<DailyUsageCounter> DailyUsageCounters { get; set; }
        public DbSet<ContentModeration> ContentModerations { get; set; }
        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<EmailVerificationOtp> EmailVerificationOtps { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ApplyUtcDateTimeConverters(modelBuilder);
            const string userIdNotNullFilter = "[UserId] IS NOT NULL";
            const string guestSessionIdNotNullFilter = "[GuestSessionId] IS NOT NULL";
            const string dailyUsageActorConstraint = "(([UserId] IS NOT NULL AND [GuestSessionId] IS NULL) OR ([UserId] IS NULL AND [GuestSessionId] IS NOT NULL))";
            const string contentSourceTypeConstraint = "[SourceType] IN (N'FileUpload', N'TextUrl', N'VideoUrl', N'DocumentUrl')";
            const string contentUrlFieldsConstraint = "(([SourceType] = N'FileUpload' AND [SourceUrl] IS NULL AND [FetchStatus] IS NULL AND [FetchError] IS NULL) OR ([SourceType] <> N'FileUpload' AND [SourceUrl] IS NOT NULL AND [FetchStatus] IS NOT NULL))";

            foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.NoAction;
            }

            modelBuilder.Entity<GuestSession>()
                .HasIndex(x => x.GuestToken)
                .IsUnique();

            modelBuilder.Entity<Role>()
                .HasIndex(x => x.RoleName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(x => x.Status)
                .HasDefaultValue(true);

            modelBuilder.Entity<User>()
                .HasQueryFilter(x => x.Status);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<EmailVerificationOtp>()
                .HasIndex(x => new { x.Email, x.Purpose, x.IsUsed, x.ExpiresAt });

            modelBuilder.Entity<EmailVerificationOtp>()
                .HasIndex(x => x.UserId);

            modelBuilder.Entity<EmailVerificationOtp>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DailyUsageCounter>()
                .HasIndex(x => new { x.UsageDate, x.UserId })
                .IsUnique()
                .HasFilter(userIdNotNullFilter);

            modelBuilder.Entity<DailyUsageCounter>()
                .HasIndex(x => new { x.UsageDate, x.GuestSessionId })
                .IsUnique()
                .HasFilter(guestSessionIdNotNullFilter);

            modelBuilder.Entity<DailyUsageCounter>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_DailyUsageCounters_Actor",
                    dailyUsageActorConstraint));

            modelBuilder.Entity<DailyUsageCounter>()
                .HasOne(x => x.User)
                .WithMany(x => x.DailyUsageCounters)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DailyUsageCounter>()
                .HasOne(x => x.GuestSession)
                .WithMany(x => x.DailyUsageCounters)
                .HasForeignKey(x => x.GuestSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContentModeration>()
                .HasIndex(x => x.ContentId)
                .IsUnique();

            modelBuilder.Entity<ContentModeration>()
                .HasIndex(x => x.Status);

            modelBuilder.Entity<ContentModeration>()
                .HasOne(x => x.Content)
                .WithOne(x => x.ContentModeration)
                .HasForeignKey<ContentModeration>(x => x.ContentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContentModeration>()
                .HasOne(x => x.ReviewedBy)
                .WithMany(x => x.ReviewedContentModerations)
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Content>()
                .HasIndex(x => x.SourceType);

            modelBuilder.Entity<Content>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Contents_SourceType",
                    contentSourceTypeConstraint));

            modelBuilder.Entity<Content>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Contents_UrlFieldsBySource",
                    contentUrlFieldsConstraint));

            modelBuilder.Entity<AdminAuditLog>()
                .HasOne(x => x.AdminUser)
                .WithMany(x => x.AdminAuditLogs)
                .HasForeignKey(x => x.AdminUserId);

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(x => x.SettingKey)
                .IsUnique();

            modelBuilder.Entity<SystemSetting>()
                .HasOne(x => x.UpdatedBy)
                .WithMany(x => x.UpdatedSystemSettings)
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(x => x.OrderId)
                .IsUnique();

            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(x => x.RequestId)
                .IsUnique();

            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });

            modelBuilder.Entity<PaymentTransaction>()
                .HasOne(x => x.User)
                .WithMany(x => x.PaymentTransactions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSubscription>()
                .HasIndex(x => new { x.UserId, x.PlanCode, x.IsActive, x.ExpiresAt });

            modelBuilder.Entity<UserSubscription>()
                .HasOne(x => x.User)
                .WithMany(x => x.UserSubscriptions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserSubscription>()
                .HasOne(x => x.PaymentTransaction)
                .WithMany()
                .HasForeignKey(x => x.PaymentTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Content>()
                .HasOne(x => x.User)
                .WithMany(x => x.Contents)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Quiz>()
                .HasOne(x => x.User)
                .WithMany(x => x.Quizzes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(x => x.User)
                .WithMany(x => x.QuizAttempts)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AISystemLog>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudyStatistic>()
                .HasQueryFilter(x => x.User.Status);

            modelBuilder.Entity<StudyStatistic>()
                .HasOne(x => x.User)
                .WithOne(x => x.StudyStatistic)
                .HasForeignKey<StudyStatistic>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AIProcess>()
                .HasOne(x => x.Content)
                .WithOne(x => x.AIProcess)
                .HasForeignKey<AIProcess>(x => x.ContentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quiz>()
                .HasOne(x => x.Content)
                .WithMany(x => x.Quizzes)
                .HasForeignKey(x => x.ContentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
                .HasOne(x => x.Quiz)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(x => x.Quiz)
                .WithMany(x => x.QuizAttempts)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserAnswer>()
                .HasOne(x => x.QuizAttempt)
                .WithMany(x => x.UserAnswers)
                .HasForeignKey(x => x.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserAnswer>()
                .HasOne(x => x.Question)
                .WithMany(x => x.UserAnswers)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.NoAction);
        }

        private static void ApplyUtcDateTimeConverters(ModelBuilder modelBuilder)
        {
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => NormalizeToUtc(v),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? NormalizeToUtc(v.Value) : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
