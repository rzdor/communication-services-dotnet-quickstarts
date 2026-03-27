using System.ComponentModel.DataAnnotations;
using Azure.Communication.CallAutomation;
using Azure.Communication.Media;
using Azure.Communication.Media.Tests;
using Azure.Identity;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CallAutomation_AzOpenAI_Voice.Models;
using CallAutomation_AzOpenAI_Voice.Services;
using CallAutomationOpenAI;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Log unhandled exceptions to stdout for App Service diagnostics
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    Console.Error.WriteLine($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
};

// Add Razor Pages
builder.Services.AddRazorPages();

// Register clients as transient — created fresh per request
builder.Services.AddTransient<MediaClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config.GetValue<string>("MediaSDKConnectionString");
    if (string.IsNullOrEmpty(connStr) || connStr.StartsWith("<"))
        throw new InvalidOperationException("MediaSDKConnectionString is not configured. Set it in App Service Application Settings.");
    var options = new MediaClientOptions("dev", false, TimeSpan.FromSeconds(10));
    return new MediaClient(connStr, options);
});

builder.Services.AddTransient<CallAutomationClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config.GetValue<string>("AcsConnectionString");
    if (string.IsNullOrEmpty(connStr) || connStr.StartsWith("<"))
        throw new InvalidOperationException("AcsConnectionString is not configured. Set it in App Service Application Settings.");
    return new CallAutomationClient(connStr);
});

// Register CallSessionManager as singleton
builder.Services.AddSingleton<CallSessionManager>();

// Register IConfiguration explicitly (already available, but make it clear)
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Enable EventSource error reporting (non-fatal if native SDK not available)
System.Diagnostics.Tracing.EventSource.SetCurrentThreadActivityId(Guid.NewGuid());
MediaSdkEventListener? listener = null;
try
{
    listener = new MediaSdkEventListener();
}
catch (Exception ex)
{
    Console.WriteLine($"MediaSdkEventListener init skipped: {ex.Message}");
}

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

var appBaseUrl = builder.Configuration.GetValue<string>("AppBaseUrl");

// Return plain JSON errors instead of HTML error pages
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var message = error?.Error?.Message ?? "Internal server error";
        startupLogger.LogError(error?.Error, "Unhandled exception");
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = message }));
    });
});

app.UseStaticFiles();
app.UseWebSockets();
app.UseRouting();
app.MapRazorPages();

app.MapGet("/api/health", (CallSessionManager sessionManager) =>
{
    return Results.Ok(new
    {
        status = "healthy",
        activeCalls = sessionManager.ActiveCallCount,
        totalCalls = sessionManager.TotalCallCount,
        timestamp = DateTime.UtcNow
    });
});

// Polling API for active calls (replaces SSE — more reliable through IIS)
app.MapGet("/api/calls/active", (CallSessionManager sessionManager) =>
{
    var activeSessions = sessionManager.GetActiveSessions().Select(s => new
    {
        s.CorrelationId,
        s.CallConnectionId,
        s.CallerId,
        Status = s.Status.ToString(),
        Duration = s.Duration.ToString(@"hh\:mm\:ss"),
        s.StartTime
    });
    return Results.Ok(new { activeSessions, total = sessionManager.TotalCallCount });
});

// Polling API for transcript of a specific call
app.MapGet("/api/calls/{correlationId}/transcript", (
    string correlationId,
    int? since,
    CallSessionManager sessionManager) =>
{
    var session = sessionManager.GetSession(correlationId);
    if (session == null)
    {
        // Check completed sessions
        var completed = sessionManager.GetCompletedSessions()
            .FirstOrDefault(s => s.CorrelationId == correlationId);
        if (completed == null)
            return Results.NotFound(new { ended = true });
        var allEntries = completed.GetTranscriptSnapshot().Select(t => new
        {
            t.Speaker,
            t.Text,
            Timestamp = t.Timestamp.ToString("HH:mm:ss")
        });
        return Results.Ok(new { entries = allEntries, ended = true, total = allEntries.Count() });
    }

    var transcript = session.GetTranscriptSnapshot();
    var startIndex = since ?? 0;
    var newEntries = transcript.Skip(startIndex).Select(t => new
    {
        t.Speaker,
        t.Text,
        Timestamp = t.Timestamp.ToString("HH:mm:ss")
    });
    return Results.Ok(new { entries = newEntries, ended = false, total = transcript.Count });
});

// Test endpoint: send whats_the_weather.wav to Media SDK (outgoing audio stream)
app.MapPost("/api/calls/{correlationId}/test/media", async (
    string correlationId,
    CallSessionManager sessionManager,
    ILogger<Program> logger) =>
{
    var session = sessionManager.GetSession(correlationId);
    if (session?.MediaHandler == null)
        return Results.NotFound(new { error = "Call session or media handler not found" });

    var wavPath = Path.Combine(AppContext.BaseDirectory, "whats_the_weather.wav");
    if (!File.Exists(wavPath))
        return Results.NotFound(new { error = "whats_the_weather.wav not found" });

    using var audioStream = File.OpenRead(wavPath);
    using var memoryStream = new MemoryStream();
    await audioStream.CopyToAsync(memoryStream);
    await session.MediaHandler.SendMessageAsync(memoryStream.ToArray());

    logger.LogInformation("Sent test audio to Media SDK for call {Id}", correlationId);
    session.AddTranscript("System", "Sent test audio (whats_the_weather.wav) to Media SDK");
    return Results.Ok(new { message = "Audio sent to Media SDK" });
});

// Test endpoint: send whats_the_weather.wav to OpenAI service
app.MapPost("/api/calls/{correlationId}/test/openai", async (
    string correlationId,
    CallSessionManager sessionManager,
    ILogger<Program> logger) =>
{
    var session = sessionManager.GetSession(correlationId);
    if (session?.AiService == null)
        return Results.NotFound(new { error = "Call session or AI service not found" });

    var wavPath = Path.Combine(AppContext.BaseDirectory, "whats_the_weather.wav");
    if (!File.Exists(wavPath))
        return Results.NotFound(new { error = "whats_the_weather.wav not found" });

    using var audioStream = File.OpenRead(wavPath);
    await session.AiService.SendAudioToExternalAI(audioStream);

    logger.LogInformation("Sent test audio to OpenAI for call {Id}", correlationId);
    session.AddTranscript("System", "Sent test audio (whats_the_weather.wav) to OpenAI");
    return Results.Ok(new { message = "Audio sent to OpenAI" });
});

app.MapPost("/api/incomingCall", async (
    [FromBody] EventGridEvent[] eventGridEvents,
    ILogger<Program> logger,
    CallSessionManager sessionManager,
    CallAutomationClient acsClient,
    MediaClient mc) =>
{
    foreach (var eventGridEvent in eventGridEvents)
    {
        try
        {
            logger.LogInformation("Incoming Call event received.");

            // Handle system events
            if (eventGridEvent.TryGetSystemEventData(out object eventData))
            {
                if (eventData is SubscriptionValidationEventData subscriptionValidationEventData)
                {
                    var responseData = new SubscriptionValidationResponse
                    {
                        ValidationResponse = subscriptionValidationEventData.ValidationCode
                    };
                    return Results.Ok(responseData);
                }
            }

            var jsonObject = Helper.GetJsonObject(eventGridEvent.Data);
            var callerId = Helper.GetCallerId(jsonObject);
            var incomingCallContext = Helper.GetIncomingCallContext(jsonObject);
            var callbackUri = new Uri(new Uri(appBaseUrl), $"/api/callbacks/{Guid.NewGuid()}?callerId={callerId}");
            logger.LogInformation("Callback Url: {CallbackUri}", callbackUri);
            var websocketUri = appBaseUrl.TrimEnd('/').Replace("https", "wss") + "/ws";
            logger.LogInformation("WebSocket Url: {WsUri}", websocketUri);

            var mediaStreamingOptions = new MediaStreamingOptions(MediaStreamingAudioChannel.Mixed)
            {
                TransportUri = new Uri(websocketUri),
                MediaStreamingContent = MediaStreamingContent.Audio,
                StartMediaStreaming = true,
                EnableBidirectional = true,
                AudioFormat = AudioFormat.Pcm24KMono
            };

            var options = new AnswerCallOptions(incomingCallContext, callbackUri)
            {
                MediaStreamingOptions = mediaStreamingOptions,
            };

            AnswerCallResult answerCallResult = await acsClient.AnswerCallAsync(options);
            var connectionId = answerCallResult.CallConnection.CallConnectionId;
            var correlationId = answerCallResult.CallConnectionProperties.CorrelationId;
            logger.LogInformation("Answered call for connection id: {Id}", connectionId);

            // Create per-call session (keyed by correlationId)
            var session = sessionManager.CreateSession(correlationId, connectionId, callerId);

            // Connect via Media SDK
            var roomConnector = new TestCallRoomConnector(mc, logger, app.Services);
            session.RoomConnector = roomConnector;

            var connected = await roomConnector.ConnectAsync(correlationId);
            if (connected)
            {
                logger.LogInformation("MediaSDK connected for call {Id}", correlationId);
                try
                {
                    var mediaHandler = new AcsMediaStreamingHandler(roomConnector);
                    session.MediaHandler = mediaHandler;

                    var openAIService = new AzureOpenAIService(mediaHandler, builder.Configuration);
                    openAIService.OnTranscript = (speaker, text) => session.AddTranscript(speaker, text);
                    session.AiService = openAIService;

                    mediaHandler.aiServiceHandler = openAIService;
                    roomConnector.aiServiceHandler = openAIService;
                    openAIService.StartConversation();

                    session.Status = CallStatus.Active;
                    logger.LogInformation("AI service started for call {Id}", correlationId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start AI service for call {Id}", correlationId);
                    sessionManager.FailSession(correlationId, ex.Message);
                }
            }
            else
            {
                logger.LogError("MediaSDK connection failed for call {Id}: {Error}", correlationId, roomConnector.LastError);
                sessionManager.FailSession(correlationId, roomConnector.LastError ?? "MediaSDK connection failed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error processing incoming call event");
        }
    }
    return Results.Ok();
});

// Handle call automation callback events
app.MapPost("/api/callbacks/{contextId}", async (
    [FromBody] CloudEvent[] cloudEvents,
    [FromRoute] string contextId,
    [Required] string callerId,
    ILogger<Program> logger,
    CallSessionManager sessionManager) =>
{
    foreach (var cloudEvent in cloudEvents)
    {
        CallAutomationEventBase @event = CallAutomationEventParser.Parse(cloudEvent);
        logger.LogInformation("Callback event received: {Type}", @event.GetType().Name);

        // Clean up session when call disconnects (lookup by CallConnectionId from ACS event)
        if (@event is CallDisconnected disconnected)
        {
            logger.LogInformation("Call disconnected: {Id}", disconnected.CallConnectionId);
            sessionManager.EndSessionByCallConnectionId(disconnected.CallConnectionId);
        }
    }

    return Results.Ok();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            try
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception received {ex}");
            }
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

app.Run();            