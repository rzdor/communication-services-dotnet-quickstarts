using CallAutomationOpenAI;
using Microsoft.Extensions.Logging;

public class AcsMediaStreamingHandler
{
    private TestCallRoomConnector m_roomConnector;
    private readonly ILogger _logger;
    public AzureOpenAIService aiServiceHandler { get; set; }
    public long MediaPacketsSent => _mediaPacketsSent;
    private long _mediaPacketsSent;

    public AcsMediaStreamingHandler(TestCallRoomConnector roomConnector, ILogger logger)
    {
        m_roomConnector = roomConnector;
        _logger = logger;
    }
      
    public async Task SendMessageAsync(byte[] message)
    {
        if (m_roomConnector != null && m_roomConnector.OutgoingAudioStream != null)
        {
            var count = Interlocked.Increment(ref _mediaPacketsSent);
            _logger.LogInformation("MediaPacketsSent: {Count}", count);
            m_roomConnector.OutgoingAudioStream.Write(message);
        } else
        {
            _logger.LogWarning("OutgoingAudioStream is not available. Cannot send message.");
        }
    }

    public async Task WriteInputStream(string data)
    {
        _logger.LogInformation("WriteInputStream -> {Data}", data);
        using Stream audioStream = File.OpenRead($"whats_the_weather.wav");
        await aiServiceHandler.SendAudioToExternalAI(audioStream);
    }

    public async Task ProcessConversation(AzureOpenAIService aiServiceHandler)
    {
        this.aiServiceHandler = aiServiceHandler;
    }
}