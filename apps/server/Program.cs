using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Bordle.Server.Data;
using Bordle.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// load env vars via .env from root
Env.Load();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "Bordle";
    config.Title = "Bordle API v1";
    config.Version = "v1";
});

builder.Services.AddHealthChecks();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    // ignore circular references in JSON serialization
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

    // allow parsing Discord Snowflake IDs (which are sent as strings from JS due to MAX_SAFE_INTEGER limits) into C# longs
    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number/MAX_SAFE_INTEGER
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
});

builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<DictionaryService>();
builder.Services.AddHostedService<PuzzleGeneratorWorker>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bordle",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "bordle",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // rate limiting policy for word submissions on per user basis
    options.AddPolicy("WordSubmissionLimit", httpContext =>
    {
        var userId = httpContext.User.FindFirstValue("userId") ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(30),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

var dictionaryService = app.Services.GetRequiredService<DictionaryService>();
await dictionaryService.InitializeAsync();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "Bordle";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseOpenApi();

// register endpoints
app.RegisterDiscordEndpoints();
app.RegisterSubmissionEndpoints();
app.RegisterPuzzleEndpoints();

app.MapHealthChecks("/health");

app.Run();
