using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Security.Claims;
using Web_Project.Infrastructure;
using Web_Project.Models;
using Web_Project.Middleware;
using Web_Project.Security;
using Web_Project.Services.AI;
using Web_Project.Services.Auth;
using Web_Project.Services.Content;
using Web_Project.Services.Email;
using Web_Project.Services.Notifications;
using Web_Project.Services.Otp;
using Web_Project.Services.Quiz;
using Web_Project.Services.Users;

var builder = WebApplication.CreateBuilder(args);
var forwardedHeadersEnabled = builder.Configuration.GetValue("ASPNETCORE_FORWARDEDHEADERS_ENABLED", false);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        DatabaseConnectionResolver.ResolveSqlServerConnectionString(builder.Configuration),
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure());
});
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<EmailOtpSettings>(builder.Configuration.GetSection("EmailOtp"));
builder.Services.Configure<GroqSettings>(builder.Configuration.GetSection("Groq"));
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));
builder.Services.Configure<AiRoutingSettings>(builder.Configuration.GetSection("AiRouting"));
builder.Services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection(GoogleAuthSettings.SectionName));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    jwtSettings.SecretKey = "CHANGE_THIS_TO_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS";
}

var jwtSigningMaterial = JwtSigningMaterial.Create(jwtSettings, builder.Environment.ContentRootPath);
builder.Services.AddSingleton(jwtSigningMaterial);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = jwtSigningMaterial.ValidationKey,
            ValidAlgorithms = [jwtSigningMaterial.Algorithm],
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token) &&
                    context.Request.Cookies.TryGetValue(AuthCookieHelper.CookieName, out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userIdRaw = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdRaw, out var userId))
                {
                    context.Fail("Phiên đăng nhập không hợp lệ.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var userExists = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId, context.HttpContext.RequestAborted);

                if (!userExists)
                {
                    context.Fail("Tài khoản không còn hoạt động.");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();
builder.Services.AddScoped<ISystemNotificationService, SystemNotificationService>();
builder.Services.AddSingleton<IAiRuntimeSettingsService, AiRuntimeSettingsService>();
builder.Services.AddHttpClient<GroqSummaryService>();
builder.Services.AddHttpClient<GeminiSummaryService>();
builder.Services.AddScoped<IGroqSummaryService, MultiModelSummaryService>();
builder.Services.AddScoped<KeywordContentSafetyService>();
builder.Services.AddScoped<IContentSafetyService, GeminiContentSafetyService>();
builder.Services.AddScoped<ISummaryProcessingService, SummaryProcessingService>();
builder.Services.AddScoped<IQuizGenerationService, QuizGenerationService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

if (jwtSigningMaterial.IsAsymmetric && jwtSigningMaterial.IsEphemeral)
{
    app.Logger.LogWarning("JWT asymmetric mode is using an auto-generated in-memory key pair. Tokens will be invalid after restart.");
}

async Task SeedAdminAccountAsync()
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Optionally auto-migrate database schema at startup.
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Skipping database migration on startup.");
    }

    var defaultAdminUsername = config["AdminAccount:Username"]?.Trim();
    var defaultAdminEmail = config["AdminAccount:Email"]?.Trim().ToLowerInvariant();
    var defaultAdminPassword = config["AdminAccount:Password"]?.Trim();
    var defaultAdminFullName = config["AdminAccount:FullName"]?.Trim();

    if (string.IsNullOrWhiteSpace(defaultAdminUsername) || string.IsNullOrWhiteSpace(defaultAdminEmail) || string.IsNullOrWhiteSpace(defaultAdminPassword))
    {
        if (app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning("AdminAccount chưa cấu hình. Sử dụng mặc định (dev). Đây không dùng cho production.");
            defaultAdminUsername = defaultAdminUsername ?? "admin";
            defaultAdminEmail = defaultAdminEmail ?? "admin@local";
            defaultAdminPassword = defaultAdminPassword ?? "Admin123!";
            defaultAdminFullName = defaultAdminFullName ?? "Administrator";
        }
        else
        {
            app.Logger.LogWarning("AdminAccount chưa cấu hình trong môi trường non-dev. Bỏ qua seed admin.");
            return;
        }
    }

    var adminRole = await dbContext.Roles.FirstOrDefaultAsync(x => x.RoleName == "Admin");
    if (adminRole == null)
    {
        adminRole = new Role { RoleName = "Admin" };
        dbContext.Roles.Add(adminRole);
        await dbContext.SaveChangesAsync();
    }

    var existingAdmin = await dbContext.Users
        .IgnoreQueryFilters()
        .Include(x => x.Role)
        .FirstOrDefaultAsync(x => x.Username == defaultAdminUsername || x.Email == defaultAdminEmail);

    if (existingAdmin != null)
    {
        var updated = false;

        if (existingAdmin.RoleId != adminRole.RoleId)
        {
            existingAdmin.RoleId = adminRole.RoleId;
            updated = true;
        }

        if (!existingAdmin.IsEmailVerified)
        {
            existingAdmin.IsEmailVerified = true;
            updated = true;
        }

        if (existingAdmin.IsLocked)
        {
            existingAdmin.IsLocked = false;
            updated = true;
        }

        if (!existingAdmin.Status)
        {
            existingAdmin.Status = true;
            updated = true;
        }

        if (existingAdmin.CreatedAt == default)
        {
            existingAdmin.CreatedAt = DateTime.UtcNow;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingAdmin.FullName) && !string.IsNullOrWhiteSpace(defaultAdminFullName))
        {
            existingAdmin.FullName = defaultAdminFullName;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingAdmin.Email) && !string.IsNullOrWhiteSpace(defaultAdminEmail))
        {
            existingAdmin.Email = defaultAdminEmail;
            updated = true;
        }

        if (updated)
        {
            dbContext.Users.Update(existingAdmin);
            await dbContext.SaveChangesAsync();
        }

        app.Logger.LogInformation("Admin account already existed and was kept intact: {Username}", existingAdmin.Username);
        return;
    }

    var newAdmin = new User
    {
        Username = defaultAdminUsername,
        FullName = defaultAdminFullName ?? "Administrator",
        Email = defaultAdminEmail,
        PasswordHash = PasswordHashUtility.HashPassword(defaultAdminPassword),
        RoleId = adminRole.RoleId,
        Status = true,
        IsLocked = false,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    dbContext.Users.Add(newAdmin);
    await dbContext.SaveChangesAsync();
    app.Logger.LogInformation("Created admin account: {Username}", newAdmin.Username);
}

await SeedAdminAccountAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseResponseCompression();

app.Use(async (context, next) =>
{
    static void ApplyResponseHeaders(HttpContext httpContext)
    {
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        httpContext.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        httpContext.Response.Headers["X-Frame-Options"] = "DENY";

        var requestPath = httpContext.Request.Path.Value ?? string.Empty;
        if (requestPath.StartsWith("/home/", StringComparison.OrdinalIgnoreCase) &&
            requestPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            httpContext.Response.Headers["Pragma"] = "no-cache";
            httpContext.Response.Headers["Expires"] = "0";
        }

        var isStaticAsset = requestPath.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/home/css/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/home/js/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/home/images/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/home/assets/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/home/lib/", StringComparison.OrdinalIgnoreCase);

        if (isStaticAsset)
        {
            httpContext.Response.Headers["Cache-Control"] = "public, max-age=604800";
            httpContext.Response.Headers["Vary"] = "Accept-Encoding";
        }
    }

    ApplyResponseHeaders(context);

    await next();

    if (!context.Response.HasStarted)
    {
        ApplyResponseHeaders(context);
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PortalRoutingMiddleware>();

app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapStaticAssets();

app.MapMethods("/", ["GET", "HEAD"], () => Results.Redirect("/home/index.html"));
app.MapMethods("/home", ["GET", "HEAD"], () => Results.Redirect("/home/index.html"));
app.MapGet("/error", () => Results.Problem("Đã xảy ra lỗi không mong muốn."));

app.Run();
