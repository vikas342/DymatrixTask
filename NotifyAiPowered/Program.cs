using System.Text.Json.Serialization;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Services.Implementations;
using NotifyAiPowered.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with JSON String Enum conversion
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure strongly typed settings
builder.Services.Configure<RateLimiterSettings>(
    builder.Configuration.GetSection(RateLimiterSettings.SectionName));

builder.Services.Configure<GeminiSettings>(
    builder.Configuration.GetSection(GeminiSettings.SectionName));
    
builder.Services.Configure<DiscordSettings>(
    builder.Configuration.GetSection(DiscordSettings.SectionName));

// Register Singleton Rate Limiter (maintains rolling window state across requests)
builder.Services.AddSingleton<IRateLimiterService, SlidingWindowRateLimiterService>();

// Register Typed HTTP Clients for LLM and Discord Dispatcher
builder.Services.AddHttpClient<ILlmAnalysisService, GeminiLlmService>((client) =>
{
    var timeout = builder.Configuration.GetValue<int>("Gemini:TimeoutSeconds");
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

builder.Services.AddHttpClient<INotificationDispatcher, DiscordWebhookDispatcher>((client) =>
{
    var timeout = builder.Configuration.GetValue<int>("Discord:TimeoutSeconds");
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

// Configure OpenAPI & Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "NotifyAiPowered API",
        Version = "v1",
        Description = "AI-Powered Notification Gateway with Rate Limiting, Gemini Incident Analysis, and Discord Dispatching."
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotifyAiPowered API v1");
    });

}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose Program class for WebApplicationFactory in Integration Tests
public partial class Program { }
