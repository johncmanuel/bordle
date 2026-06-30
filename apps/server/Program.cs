using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Bordle.Server.Data;

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

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // ignore circular references in JSON serialization
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

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

// register endpoints
app.RegisterDiscordEndpoints();

app.MapHealthChecks("/health");

app.Run();
