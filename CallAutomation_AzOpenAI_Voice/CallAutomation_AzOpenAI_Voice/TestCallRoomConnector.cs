using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Communication.Media;
using CallAutomationOpenAI;
using Microsoft.Extensions.Logging;

public class RemoteStreamInfo
{
    public long StreamId { get; set; }
    public string EndpointId { get; set; } = string.Empty;
    public string StreamType { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string? LastState { get; set; }
    public DateTime? LastStateAt { get; set; }
    public object? SdkStream { get; set; }
}

public class TestCallRoomConnector
{
    private readonly MediaClient _mediaClient;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _connected;
    private MediaConnection? _connection;
    private MediaSession? _session;
    public string? LastError { get; private set; }
    public string? EndpointId => _connection?.EndpointId;
    public string? ResourceId => _connection?.ResourceId;
    public string? MediaConnectionState => _connection?.ConnectionState.ToString();
    public string? ServiceOrigin { get; private set; }
    public long MediaPacketsSent => _mediaPacketsSent;
    public long MediaPacketsReceived => _mediaPacketsReceived;
    public long DataPacketsDropped => _dataPacketsDropped;
    private long _mediaPacketsSent;
    private long _mediaPacketsReceived;
    private long _dataPacketsDropped;
    private readonly ConcurrentDictionary<long, RemoteStreamInfo> _remoteStreams = new();
    private readonly ConcurrentQueue<string> _streamStateLog = new();
    public IReadOnlyCollection<RemoteStreamInfo> RemoteStreams => _remoteStreams.Values.ToList().AsReadOnly();
    public IReadOnlyCollection<string> StreamStateLog => _streamStateLog.ToArray();
    public int RemoteAudioStreamCount => _remoteStreams.Values.Count(s => s.StreamType == "Audio");
    public int RemoteDataStreamCount => _remoteStreams.Values.Count(s => s.StreamType == "Data");
    public OutgoingAudioStream OutgoingAudioStream { get; private set; }
    public AzureOpenAIService aiServiceHandler { get; set; }

    public TestCallRoomConnector(MediaClient mediaClient, ILogger logger, IServiceProvider serviceProvider)
    {
        _mediaClient = mediaClient;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ConnectAsync(string sessionId)
    {
        try
        {
            ServiceOrigin = _mediaClient.ServiceOrigin;
            _logger.LogInformation("Service Origin URL: {ServiceOrigin}", ServiceOrigin);

            _connection = await _mediaClient.CreateMediaConnectionAsync(new MediaConnectionOptions());

            _logger.LogInformation("MediaConnection {State} {EndpointId}", _connection.ConnectionState, _connection.EndpointId);

            _connection.OnStateChanged += OnConnectionStateChanged;
            _connection.OnStatsReportReceived += OnStatsReportReceived;

            _session = _connection.CreateMediaSession(
                sessionId: sessionId,
                options: new MediaSessionOptions(new HashSet<uint>()));

            _session.OnIncomingAudioStreamAdded += OnIncomingAudioStreamAdded;
            _session.OnIncomingAudioStreamRemoved += OnIncomingAudioStreamRemoved;

            await _session.JoinAsync();

            OutgoingAudioStream = _session.AddOutgoingAudioStream();

            _logger.LogInformation("MediaConnection {State}", _connection.ConnectionState);

            _connected = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to room");
            LastError = ex.Message;
            _connected = false;
            return false;
        }
    }

    public void Disconnect()
    {
        _connected = false;
        try { _session?.Dispose(); } catch { }
        try { _connection?.Dispose(); } catch { }
        _session = null;
        _connection = null;
        _remoteStreams.Clear();
        _logger.LogInformation("MediaSDK disconnected");
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _logger.LogInformation("ConnectionStateChanged: {State}", e.ConnectionState);
    }

    private void OnIncomingAudioStreamAdded(object? sender, IncomingAudioStreamAddedEventArgs e)
    {
        var stream = e.IncomingAudioStream;
        _logger.LogInformation("IncomingAudioStreamAdded - StreamId({StreamId}) EndpointId({EndpointId})", stream.Id, stream.EndpointId);

        _remoteStreams[stream.Id] = new RemoteStreamInfo
        {
            StreamId = stream.Id,
            EndpointId = stream.EndpointId ?? "",
            StreamType = "Audio",
            SdkStream = stream
        };

        stream.OnIncomingAudioStreamReceived += OnIncomingAudioStreamReceived;
#pragma warning disable ACS_MEDIA_SDK_STREAM_STATE_API
        stream.StateReceived += (s, stateArgs) => OnStreamStateReceived(stream.Id, stream.EndpointId, stateArgs);
#pragma warning restore ACS_MEDIA_SDK_STREAM_STATE_API
    }

    private void OnIncomingAudioStreamReceived(object? sender, IncomingAudioStreamReceivedEventArgs args)
    {
        var count = Interlocked.Increment(ref _mediaPacketsReceived);
        var dataLen = args.Data?.ReadDataAsSpan().Length ?? 0;
        if (count <= 10 || count % 100 == 1)
            _logger.LogInformation("MediaPacketsReceived: {Count}, DataLen: {DataLen}, ComfortNoise: {ComfortNoise}", count, dataLen, args.ComfortNoise);

        if (!_connected || aiServiceHandler == null)
        {
            if (count <= 5)
                _logger.LogWarning("Cannot forward audio to OpenAI: connected={Connected}, aiHandler={HasHandler}", _connected, aiServiceHandler != null);
            return;
        }

        var audioData = args.Data.ReadDataAsSpan().ToArray();
        _ = Task.Run(async () =>
        {
            try
            {
                await aiServiceHandler.SendAudioToExternalAI(new MemoryStream(audioData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending audio to OpenAI");
            }
        });
    }

    private void OnStreamStateReceived(long streamId, string? endpointId,
#pragma warning disable ACS_MEDIA_SDK_STREAM_STATE_API
        IncomingStreamStateEventArgs e)
#pragma warning restore ACS_MEDIA_SDK_STREAM_STATE_API
    {
        var stateHex = BitConverter.ToString(e.State).Replace("-", "");
        _logger.LogInformation("StreamStateReceived - StreamId({StreamId}) Endpoint({EndpointId}) State({StateHex})", streamId, endpointId, stateHex);

        _streamStateLog.Enqueue($"[{DateTime.UtcNow:HH:mm:ss}] Stream {streamId} ({endpointId}): {stateHex}");
        while (_streamStateLog.Count > 50) _streamStateLog.TryDequeue(out _);

        if (_remoteStreams.TryGetValue(streamId, out var info))
        {
            info.LastState = stateHex;
            info.LastStateAt = DateTime.UtcNow;
        }
    }

    private void OnIncomingAudioStreamRemoved(object? sender, IncomingAudioStreamRemovedEventArgs e)
    {
        var stream = e.IncomingAudioStream;
        stream.OnIncomingAudioStreamReceived -= OnIncomingAudioStreamReceived;
        _remoteStreams.TryRemove(stream.Id, out _);
        _logger.LogInformation("IncomingAudioStreamRemoved - StreamId({StreamId})", stream.Id);
        stream.Dispose();
    }

    internal virtual void OnIncomingDataStreamAdded(object? sender, IncomingDataStreamAddedEventArgs e)
    {
        var stream = e.IncomingDataStream;
        _logger.LogInformation("IncomingDataStreamAdded - StreamId({StreamId}), PayloadType({PayloadType})", stream.Id, stream.PayloadType);

        _remoteStreams[stream.Id] = new RemoteStreamInfo
        {
            StreamId = stream.Id,
            EndpointId = $"PayloadType:{stream.PayloadType}",
            StreamType = "Data",
            SdkStream = stream
        };

        stream.OnIncomingDataStreamReceived += OnIncomingDataStreamReceived;
        stream.OnIncomingDataDropped += OnIncomingDataDropped;
    }

    private void OnIncomingDataStreamReceived(object? sender, IncomingDataStreamReceivedEventArgs e)
    {
    }

    private void OnIncomingDataDropped(object? sender, IncomingDataDroppedEventArgs e)
    {
        Interlocked.Increment(ref _dataPacketsDropped);
        _logger.LogWarning("IncomingDataDropped - DroppedCount({DroppedCount}) InboundId({MessageId})", e.DroppedCount, e.MessageId);
    }

    private void OnIncomingDataStreamRemoved(object? sender, IncomingDataStreamRemovedEventArgs e)
    {
        _remoteStreams.TryRemove(e.IncomingDataStream.Id, out _);
        _logger.LogInformation("IncomingDataStreamRemoved - StreamId({StreamId})", e.IncomingDataStream.Id);
    }

    private void OnStatsReportReceived(object? sender, StatsReportReceivedEventArgs e)
    {
        _logger.LogInformation("StatsReport: IsConnected={IsConnected} Audio(In:{AudioIn}/Out:{AudioOut})",
            e.StatsReport.IsConnected, e.StatsReport.Audio?.Inbound?.Aggregate, e.StatsReport.Audio?.Outbound?.Aggregate);
    }

    public bool IsConnected => _connected;
}
