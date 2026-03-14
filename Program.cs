using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Wed_Project.Models;
using Wed_Project.Security;
using Wed_Project.Services.AI;
using Wed_Project.Services.Auth;
using Wed_Project.Services.Content;
using Wed_Project.Services.Email;
using Wed_Project.Services.Otp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<EmailOtpSettings>(builder.Configuration.GetSection("EmailOtp"));
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));
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
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();
builder.Services.AddHttpClient<IGeminiSummaryService, GeminiSummaryService>();
builder.Services.AddScoped<ISummaryProcessingService, SummaryProcessingService>();

var app = builder.Build();

if (jwtSigningMaterial.IsAsymmetric && jwtSigningMaterial.IsEphemeral)
{
    app.Logger.LogWarning("JWT asymmetric mode is using an auto-generated in-memory key pair. Tokens will be invalid after restart.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapStaticAssets();

app.MapGet("/", () => Results.Redirect("/home/index.html"));
app.MapGet("/home", () => Results.Redirect("/home/index.html"));
app.MapGet("/error", () => Results.Problem("Đã xảy ra lỗi không mong muốn."));

app.Run();
