using System;
using System.Threading.Tasks;
using Azure.Communication;
using Azure.Communication.CallAutomation;
using Call_Automation_GCCH.Models;
using Call_Automation_GCCH.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Call_Automation_GCCH.Controllers
{
    [ApiController]
    [Route("api/connect")]
    [Produces("application/json")]
    public class ConnectController : ControllerBase
    {
        private readonly ICallAutomationService _service;
        private readonly ILogger<ConnectController> _logger;
        private readonly AcsCommunicationSettings _config;

        public ConnectController(
            ICallAutomationService service,
            ILogger<ConnectController> logger, IOptions<AcsCommunicationSettings> configOptions)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configOptions.Value ?? throw new ArgumentNullException(nameof(configOptions));
        }

        // ──────────── CONNECT TO SERVER CALL ───────────────────────────────────────

        /// <summary>
        /// Connects to an existing server call with optional media streaming and transcription.
        /// </summary>
        /// <param name="serverCallId">The server call ID to connect to</param>
        /// <param name="enableMediaStreaming">Whether to enable media streaming on connect</param>
        /// <param name="isMixedAudio">True for mixed audio channel, false for unmixed</param>
        /// <param name="enableTranscription">Whether to enable transcription on connect</param>
        /// <param name="transcriptionLocale">Transcription locale (e.g. en-US, es-ES)</param>
        /// <param name="operationContext">Custom context string for correlating events</param>
        [HttpPost("connectCallAsync")]
        [Tags("Connect Call APIs")]
        public Task<IActionResult> ConnectCallAsync(
            string serverCallId,
            bool enableMediaStreaming = false,
            bool isMixedAudio = false,
            bool enableTranscription = false,
            string transcriptionLocale = "en-US",
            string operationContext = "ConnectCallContext")
            => HandleConnectCall(serverCallId, enableMediaStreaming, isMixedAudio, enableTranscription, transcriptionLocale, operationContext, async: true);

        [HttpPost("connectCall")]
        [Tags("Connect Call APIs")]
        public IActionResult ConnectCall(
            string serverCallId,
            bool enableMediaStreaming = false,
            bool isMixedAudio = false,
            bool enableTranscription = false,
            string transcriptionLocale = "en-US",
            string operationContext = "ConnectCallContext")
            => HandleConnectCall(serverCallId, enableMediaStreaming, isMixedAudio, enableTranscription, transcriptionLocale, operationContext, async: false).Result;

        // ───────────── PRIVATE HANDLERS ──────────────────────────────────────────

        private async Task<IActionResult> HandleConnectCall(
            string serverCallId,
            bool enableMediaStreaming,
            bool isMixedAudio,
            bool enableTranscription,
            string transcriptionLocale,
            string operationContext,
            bool async)
        {
            if (string.IsNullOrEmpty(serverCallId))
                return BadRequest("serverCallId is required");

            _logger.LogInformation("Connecting to call. ServerCallId={ServerCallId}, MediaStreaming={MediaStreaming}, Transcription={Transcription}",
                serverCallId, enableMediaStreaming, enableTranscription);

            try
            {
                var callbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                var websocketUri = _config.CallbackUriHost.Replace("https", "wss") + "/ws";

                var callLocator = new ServerCallLocator(serverCallId);
                var connectOpts = new ConnectCallOptions(callLocator, callbackUri)
                {
                    OperationContext = operationContext
                };

                if (enableMediaStreaming)
                {
                    var audioChannel = isMixedAudio ? MediaStreamingAudioChannel.Mixed : MediaStreamingAudioChannel.Unmixed;
                    connectOpts.MediaStreamingOptions = new MediaStreamingOptions(
                        new Uri(websocketUri),
                        MediaStreamingContent.Audio,
                        audioChannel,
                        MediaStreamingTransport.Websocket,
                        false);
                }

                if (enableTranscription)
                {
                    connectOpts.TranscriptionOptions = new TranscriptionOptions(
                        new Uri(websocketUri),
                        transcriptionLocale,
                        false,
                        TranscriptionTransport.Websocket);
                }

                ConnectCallResult result = async
                    ? await _service.GetCallAutomationClient().ConnectCallAsync(connectOpts)
                    : _service.GetCallAutomationClient().ConnectCall(connectOpts);

                var props = result.CallConnectionProperties;
                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = props.CallConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = props.CallConnectionState.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to call");
                return Problem($"Failed to connect to call: {ex.Message}");
            }
        }
    }

}
