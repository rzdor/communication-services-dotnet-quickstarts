using Azure.Communication;
using Azure.Communication.CallAutomation;
using Call_Automation_GCCH;
using Call_Automation_GCCH.Logging;
using Call_Automation_GCCH.Models;
using Call_Automation_GCCH.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

var commSection = builder.Configuration.GetSection("CommunicationSettings");
// This reads "CommunicationSettings" from appsettings.json
builder.Services.Configure<AcsCommunicationSettings>(commSection);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OrderActionsBy(apiDesc =>
    {
        var tag = apiDesc.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Http.TagsAttribute>()
            .SelectMany(t => t.Tags)
            .FirstOrDefault() ?? "zzz";
        return tag;
    });
});

// Add CallAutomationService as a singleton behind ICallAutomationService
// Client is initialized lazily — use the /api/configuration/setConnectionString endpoint
// to provide ACS credentials at runtime from Swagger.
builder.Services.AddSingleton<ICallAutomationService, CallAutomationService>(sp => {
    string connectionString = commSection["AcsConnectionString"] ?? string.Empty;
    bool isArizona = bool.Parse(commSection["IsArizona"] ?? "true");
    string pmaEndpoint = (isArizona ? commSection["PmaEndpointArizona"] : commSection["PmaEndpointTexas"]) ?? string.Empty;

    var logger = sp.GetRequiredService<ILogger<CallAutomationService>>();

    if (string.IsNullOrEmpty(connectionString))
    {
        logger.LogWarning("AcsConnectionString is not set. Use POST /api/configuration/setConnectionString to configure at runtime.");
    }

    return new CallAutomationService(connectionString, pmaEndpoint, logger);
});

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new ConsoleCollectorLoggerProvider());

var app = builder.Build();

app.UseStaticFiles(); // Required to serve wwwroot

app.UseSwagger(); // This generates /swagger/v1/swagger.json

app.UseSwaggerUI(c =>
{
    // So the UI is served at /swagger
    c.RoutePrefix = "swagger";

    // Use the custom GCCHSwagger.html from wwwroot/swagger-ui
    c.IndexStream = () =>
    {
        var path = Path.Combine(builder.Environment.WebRootPath, "swagger-ui", "GCCHSwagger.html");
        return File.OpenRead(path);
    };
});

// Configure the audio files path
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "audio")),
    RequestPath = "/audio"
});

// Enable WebSocket support
app.UseWebSockets();
app.Use(async (context, next) =>
{
  // Get the logger instance from the DI container
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

  if (context.Request.Path == "/ws")
  {
    logger.LogInformation($"Request received. Path: {context.Request.Path}");
    if (context.WebSockets.IsWebSocketRequest)
    {
      logger.LogInformation("WebSocket request received.");
      using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
      await WebSocketStreamingHandler.ProcessRequest(webSocket);
    }
    else
    {
      context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
  }
  else
  {
    await next(context);
  }
});

// Add custom WebSocket middleware
// app.UseMiddleware<Call_Automation_GCCH.Middleware.WebSocketMiddleware>();

app.MapControllers();

app.Run();