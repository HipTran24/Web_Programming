using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web_Project.Models;

namespace Web_Project.Services.Premium
{
    public sealed class PremiumPlanSettingsService : IPremiumPlanSettingsService
    {
        private const string AmountKey = "premium.plan.amount.vnd";
        private const string DaysKey = "premium.plan.days";
        private readonly AppDbContext _dbContext;
        private readonly MoMoPaymentSettings _moMoSettings;

        public PremiumPlanSettingsService(
            AppDbContext dbContext,
            IOptions<MoMoPaymentSettings> moMoSettings)
        {
            _dbContext = dbContext;
            _moMoSettings = moMoSettings.Value;
        }

        public async Task<PremiumPlanSettings> GetSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == AmountKey || x.SettingKey == DaysKey)
                .ToListAsync(cancellationToken);

            var amountSetting = settings.FirstOrDefault(x => x.SettingKey == AmountKey);
            var daysSetting = settings.FirstOrDefault(x => x.SettingKey == DaysKey);

            return new PremiumPlanSettings
            {
                Amount = ParseDecimal(amountSetting?.SettingValue, GetFallbackAmount()),
                Days = ParseInt(daysSetting?.SettingValue, GetFallbackDays()),
                UpdatedAt = settings.Count == 0 ? null : settings.Max(x => (DateTime?)x.UpdatedAt),
                UpdatedByUserId = settings
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => x.UpdatedByUserId)
                    .FirstOrDefault()
            };
        }

        public async Task<PremiumPlanSettings> UpdateSettingsAsync(
            decimal amount,
            int days,
            int updatedByUserId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var roundedAmount = Math.Round(Math.Max(0m, amount), 0, MidpointRounding.AwayFromZero);
            var safeDays = Math.Clamp(days, 1, 3650);

            await UpsertAsync(
                AmountKey,
                roundedAmount.ToString("0", CultureInfo.InvariantCulture),
                "Premium price in VND",
                updatedByUserId,
                now,
                cancellationToken);
            await UpsertAsync(
                DaysKey,
                safeDays.ToString(CultureInfo.InvariantCulture),
                "Premium duration in days",
                updatedByUserId,
                now,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return await GetSettingsAsync(cancellationToken);
        }

        private async Task UpsertAsync(
            string key,
            string value,
            string description,
            int updatedByUserId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == key, cancellationToken);

            if (setting is null)
            {
                _dbContext.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    Description = description,
                    UpdatedByUserId = updatedByUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                return;
            }

            setting.SettingValue = value;
            setting.Description = description;
            setting.UpdatedByUserId = updatedByUserId;
            setting.UpdatedAt = now;
        }

        private decimal GetFallbackAmount()
        {
            if (_moMoSettings.PremiumAmount > 0m)
            {
                return _moMoSettings.PremiumAmount;
            }

            return 99000m;
        }

        private int GetFallbackDays()
        {
            if (_moMoSettings.PremiumDays > 0)
            {
                return _moMoSettings.PremiumDays;
            }

            return 30;
        }

        private static decimal ParseDecimal(string? value, decimal fallback)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Max(0m, parsed)
                : fallback;
        }

        private static int ParseInt(string? value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Clamp(parsed, 1, 3650)
                : fallback;
        }
    }
}
