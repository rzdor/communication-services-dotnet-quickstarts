using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
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
    [Route("api/media")]
    [Produces("application/json")]
    public class MediaController : ControllerBase
    {
        private readonly ICallAutomationService _service;
        private readonly ILogger<MediaController> _logger;
        private readonly AcsCommunicationSettings _config;

        public MediaController(
            ICallAutomationService service,
            ILogger<MediaController> logger,
            IOptions<AcsCommunicationSettings> configOptions)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configOptions.Value ?? throw new ArgumentNullException(nameof(configOptions));
        }

        #region Play – 3 overloads: PlayOptions | PlaySource,targets | IEnumerable<PlaySource>,targets

        /// <summary>
        /// Plays audio to specific target(s).
        /// useOptions=true  ? Play(PlayOptions) with all configurable fields.
        /// useOptions=false ? Play(PlaySource, targets) simple overload.
        /// </summary>
        /// <param name="callConnectionId">The call connection ID</param>
        /// <param name="target">ACS user ID (8:...) or phone number (+...)</param>
        /// <param name="useOptions">true = PlayOptions overload, false = simple (PlaySource, targets) overload</param>
        /// <param name="interruptHoldAudio">Interrupts hold audio (PlayOptions only)</param>
        /// <param name="loop">Loop the audio (PlayOptions only)</param>
        /// <param name="operationContext">Custom context (PlayOptions only)</param>
        [HttpPost("/playAsync")]
        [Tags("Play Media")]
        public Task<IActionResult> PlayAsync(
            string callConnectionId, string target,
            bool useOptions = true,
            bool interruptHoldAudio = false, bool loop = false,
            string operationContext = "playContext")
            => HandlePlay(callConnectionId, target, useOptions, interruptHoldAudio, loop, operationContext, async: true);

        [HttpPost("/play")]
        [Tags("Play Media")]
        public IActionResult Play(
            string callConnectionId, string target,
            bool useOptions = true,
            bool interruptHoldAudio = false, bool loop = false,
            string operationContext = "playContext")
            => HandlePlay(callConnectionId, target, useOptions, interruptHoldAudio, loop, operationContext, async: false).Result;

        #endregion

        #region PlayToAll – 3 overloads: PlayToAllOptions | PlaySource | IEnumerable<PlaySource>

        /// <summary>
        /// Plays audio to all participants.
        /// useOptions=true  ? PlayToAll(PlayToAllOptions) with all configurable fields.
        /// useOptions=false ? PlayToAll(PlaySource) simple overload.
        /// </summary>
        /// <param name="callConnectionId">The call connection ID</param>
        /// <param name="useOptions">true = PlayToAllOptions overload, false = simple (PlaySource) overload</param>
        /// <param name="interruptCallMediaOperation">Interrupts current media / barge-in (PlayToAllOptions only)</param>
        /// <param name="loop">Loop the audio (PlayToAllOptions only)</param>
        /// <param name="operationContext">Custom context (PlayToAllOptions only)</param>
        [HttpPost("/playToAllAsync")]
        [Tags("Play Media")]
        public Task<IActionResult> PlayToAllAsync(
            string callConnectionId,
            bool useOptions = true,
            bool interruptCallMediaOperation = false, bool loop = false,
            string operationContext = "playToAllContext")
            => HandlePlayToAll(callConnectionId, useOptions, interruptCallMediaOperation, loop, operationContext, async: true);

        [HttpPost("/playToAll")]
        [Tags("Play Media")]
        public IActionResult PlayToAll(
            string callConnectionId,
            bool useOptions = true,
            bool interruptCallMediaOperation = false, bool loop = false,
            string operationContext = "playToAllContext")
            => HandlePlayToAll(callConnectionId, useOptions, interruptCallMediaOperation, loop, operationContext, async: false).Result;

        #endregion

        #region Hold – 3 overloads: HoldOptions | identifier | identifier,playSource

        /// <summary>
        /// Holds a participant.
        /// useOptions=true  ? Hold(HoldOptions) with OperationContext and optional PlaySource.
        /// useOptions=false ? Hold(CommunicationIdentifier) or Hold(identifier, PlaySource) simple overloads.
        /// </summary>
        /// <param name="callConnectionId">The call connection ID</param>
        /// <param name="target">ACS user ID (8:...) or phone number (+...)</param>
        /// <param name="useOptions">true = HoldOptions overload, false = simple overload</param>
        /// <param name="playHoldMusic">When true, plays hold music</param>
        /// <param name="operationContext">Custom context (HoldOptions only)</param>
        [HttpPost("/holdAsync")]
        [Tags("Hold Management")]
        public Task<IActionResult> HoldAsync(
            string callConnectionId, string target,
            bool useOptions = true, bool playHoldMusic = false,
            string operationContext = "holdContext")
            => HandleHold(callConnectionId, target, useOptions, playHoldMusic, operationContext, async: true);

        [HttpPost("/hold")]
        [Tags("Hold Management")]
        public IActionResult Hold(
            string callConnectionId, string target,
            bool useOptions = true, bool playHoldMusic = false,
            string operationContext = "holdContext")
            => HandleHold(callConnectionId, target, useOptions, playHoldMusic, operationContext, async: false).Result;

        #endregion

        #region Unhold – 2 overloads: UnholdOptions | identifier

        /// <summary>
        /// Unholds a participant.
        /// useOptions=true  ? Unhold(UnholdOptions).
        /// useOptions=false ? Unhold(CommunicationIdentifier).
        /// </summary>
        [HttpPost("/unholdAsync")]
        [Tags("Hold Management")]
        public Task<IActionResult> UnholdAsync(
            string callConnectionId, string target,
            bool useOptions = true,
            string operationContext = "unholdContext")
            => HandleUnhold(callConnectionId, target, useOptions, operationContext, async: true);

        [HttpPost("/unhold")]
        [Tags("Hold Management")]
        public IActionResult Unhold(
            string callConnectionId, string target,
            bool useOptions = true,
            string operationContext = "unholdContext")
            => HandleUnhold(callConnectionId, target, useOptions, operationContext, async: false).Result;

        #endregion

        #region StartMediaStreaming / StopMediaStreaming – options only

        /// <summary>
        /// Starts media streaming (StartMediaStreamingOptions overload).
        /// </summary>
        [HttpPost("/startMediaStreamingAsync")]
        [Tags("Media Streaming")]
        public Task<IActionResult> StartMediaStreamingAsync(
            string callConnectionId,
            string operationContext = "StartMediaStreamingContext")
            => HandleMediaStreaming(callConnectionId, start: true, operationContext, async: true);

        [HttpPost("/startMediaStreaming")]
        [Tags("Media Streaming")]
        public IActionResult StartMediaStreaming(
            string callConnectionId,
            string operationContext = "StartMediaStreamingContext")
            => HandleMediaStreaming(callConnectionId, start: true, operationContext, async: false).Result;

        [HttpPost("/stopMediaStreamingAsync")]
        [Tags("Media Streaming")]
        public Task<IActionResult> StopMediaStreamingAsync(
            string callConnectionId,
            string operationContext = "StopMediaStreamingContext")
            => HandleMediaStreaming(callConnectionId, start: false, operationContext, async: true);

        [HttpPost("/stopMediaStreaming")]
        [Tags("Media Streaming")]
        public IActionResult StopMediaStreaming(
            string callConnectionId,
            string operationContext = "StopMediaStreamingContext")
            => HandleMediaStreaming(callConnectionId, start: false, operationContext, async: false).Result;

        #endregion

        #region CreateCallWithMediaStreaming

        [HttpPost("/createCallWithMediaStreamingAsync")]
        [Tags("Media Streaming")]
        public Task<IActionResult> CreateCallWithMediaStreamingAsync(
            string target, bool isMixed = true, bool enableMediaStreaming = false,
            bool isEnableBidirectional = false, bool isPcm24kMono = false)
            => HandleCreateCallWithMediaStreaming(target, isMixed, enableMediaStreaming, isEnableBidirectional, isPcm24kMono, async: true);

        [HttpPost("/createCallWithMediaStreaming")]
        [Tags("Media Streaming")]
        public IActionResult CreateCallWithMediaStreaming(
            string target, bool isMixed = true, bool enableMediaStreaming = false,
            bool isEnableBidirectional = false, bool isPcm24kMono = false)
            => HandleCreateCallWithMediaStreaming(target, isMixed, enableMediaStreaming, isEnableBidirectional, isPcm24kMono, async: false).Result;

        #endregion

        #region CancelAllMediaOperations

        [HttpPost("/cancelAllMediaOperationAsync")]
        [Tags("Media Operations")]
        public Task<IActionResult> CancelAllMediaOperationAsync(string callConnectionId)
            => HandleCancelAllMediaOperations(callConnectionId, async: true);

        [HttpPost("/cancelAllMediaOperation")]
        [Tags("Media Operations")]
        public IActionResult CancelAllMediaOperation(string callConnectionId)
            => HandleCancelAllMediaOperations(callConnectionId, async: false).Result;

        #endregion

        #region Recognize – Dtmf, Choice, Speech, SpeechOrDtmf

        /// <summary>
        /// Starts recognition via StartRecognizing(CallMediaRecognizeOptions).
        /// </summary>
        [HttpPost("/startRecognizeAsync")]
        [Tags("Recognition")]
        public Task<IActionResult> StartRecognizeAsync(
            string callConnectionId, string target,
            string recognizeType = "Dtmf", int maxTonesToCollect = 4,
            bool interruptPrompt = false,
            int initialSilenceTimeoutSec = 15, int interToneTimeoutSec = 5, int endSilenceTimeoutSec = 5,
            string operationContext = "RecognizeContext")
            => HandleRecognize(callConnectionId, target, recognizeType, maxTonesToCollect, interruptPrompt,
                initialSilenceTimeoutSec, interToneTimeoutSec, endSilenceTimeoutSec, operationContext, async: true);

        [HttpPost("/startRecognize")]
        [Tags("Recognition")]
        public IActionResult StartRecognize(
            string callConnectionId, string target,
            string recognizeType = "Dtmf", int maxTonesToCollect = 4,
            bool interruptPrompt = false,
            int initialSilenceTimeoutSec = 15, int interToneTimeoutSec = 5, int endSilenceTimeoutSec = 5,
            string operationContext = "RecognizeContext")
            => HandleRecognize(callConnectionId, target, recognizeType, maxTonesToCollect, interruptPrompt,
                initialSilenceTimeoutSec, interToneTimeoutSec, endSilenceTimeoutSec, operationContext, async: false).Result;

        #endregion

        #region InterruptAudioAndAnnounce

        [HttpPost("/interruptAudioAndAnnounceAsync")]
        [Tags("Audio Announcements")]
        public Task<IActionResult> InterruptAudioAndAnnounceAsync(
            string callConnectionId, string target, string operationContext = "interruptContext")
            => HandleInterruptAudioAndAnnounce(callConnectionId, target, operationContext, async: true);

        [HttpPost("/interruptAudioAndAnnounce")]
        [Tags("Audio Announcements")]
        public IActionResult InterruptAudioAndAnnounce(
            string callConnectionId, string target, string operationContext = "interruptContext")
            => HandleInterruptAudioAndAnnounce(callConnectionId, target, operationContext, async: false).Result;

        #endregion

        #region Transcription – Start/Stop/Update with 2 overloads each

        /// <summary>
        /// Starts transcription via StartTranscription(StartTranscriptionOptions).
        /// </summary>
        [HttpPost("/startTranscriptionAsync")]
        [Tags("Transcription")]
        public Task<IActionResult> StartTranscriptionAsync(
            string callConnectionId, string locale = "en-US", string operationContext = "StartTranscriptionContext")
            => HandleTranscription(callConnectionId, TranscriptionAction.Start, locale, operationContext, async: true);

        [HttpPost("/startTranscription")]
        [Tags("Transcription")]
        public IActionResult StartTranscription(
            string callConnectionId, string locale = "en-US", string operationContext = "StartTranscriptionContext")
            => HandleTranscription(callConnectionId, TranscriptionAction.Start, locale, operationContext, async: false).Result;

        [HttpPost("/stopTranscriptionAsync")]
        [Tags("Transcription")]
        public Task<IActionResult> StopTranscriptionAsync(
            string callConnectionId, string operationContext = "StopTranscriptionContext")
            => HandleTranscription(callConnectionId, TranscriptionAction.Stop, null, operationContext, async: true);

        [HttpPost("/stopTranscription")]
        [Tags("Transcription")]
        public IActionResult StopTranscription(
            string callConnectionId, string operationContext = "StopTranscriptionContext")
            => HandleTranscription(callConnectionId, TranscriptionAction.Stop, null, operationContext, async: false).Result;

        /// <summary>
        /// Updates transcription locale.
        /// useOptions=true  ? UpdateTranscription(UpdateTranscriptionOptions).
        /// useOptions=false ? UpdateTranscription(string locale).
        /// </summary>
        [HttpPost("/updateTranscriptionAsync")]
        [Tags("Transcription")]
        public Task<IActionResult> UpdateTranscriptionAsync(
            string callConnectionId, string locale = "en-US", bool useOptions = false,
            string operationContext = "UpdateTranscriptionContext")
            => HandleUpdateTranscription(callConnectionId, locale, useOptions, operationContext, async: true);

        [HttpPost("/updateTranscription")]
        [Tags("Transcription")]
        public IActionResult UpdateTranscription(
            string callConnectionId, string locale = "en-US", bool useOptions = false,
            string operationContext = "UpdateTranscriptionContext")
            => HandleUpdateTranscription(callConnectionId, locale, useOptions, operationContext, async: false).Result;

        #endregion

        #region SendDtmfTones – 2 overloads: SendDtmfTonesOptions | (tones, identifier)

        /// <summary>
        /// useOptions=true  ? SendDtmfTones(SendDtmfTonesOptions).
        /// useOptions=false ? SendDtmfTones(tones, identifier).
        /// </summary>
        [HttpPost("/sendDtmfTonesAsync")]
        [Tags("Send DTMF")]
        public Task<IActionResult> SendDtmfTonesAsync(
            string callConnectionId, string target, string tones = "zero,one",
            bool useOptions = false, string operationContext = "SendDtmfContext")
            => HandleSendDtmfTones(callConnectionId, target, tones, useOptions, operationContext, async: true);

        [HttpPost("/sendDtmfTones")]
        [Tags("Send DTMF")]
        public IActionResult SendDtmfTones(
            string callConnectionId, string target, string tones = "zero,one",
            bool useOptions = false, string operationContext = "SendDtmfContext")
            => HandleSendDtmfTones(callConnectionId, target, tones, useOptions, operationContext, async: false).Result;

        #endregion

        #region ContinuousDtmf – 2 overloads each: identifier | ContinuousDtmfRecognitionOptions

        /// <summary>
        /// useOptions=true  ? Start/StopContinuousDtmfRecognition(ContinuousDtmfRecognitionOptions).
        /// useOptions=false ? Start/StopContinuousDtmfRecognition(CommunicationIdentifier).
        /// </summary>
        [HttpPost("/startContinuousDtmfAsync")]
        [Tags("Continuous DTMF")]
        public Task<IActionResult> StartContinuousDtmfAsync(
            string callConnectionId, string target,
            bool useOptions = false, string operationContext = "StartContinuousDtmfContext")
            => HandleContinuousDtmf(callConnectionId, target, start: true, useOptions, operationContext, async: true);

        [HttpPost("/startContinuousDtmf")]
        [Tags("Continuous DTMF")]
        public IActionResult StartContinuousDtmf(
            string callConnectionId, string target,
            bool useOptions = false, string operationContext = "StartContinuousDtmfContext")
            => HandleContinuousDtmf(callConnectionId, target, start: true, useOptions, operationContext, async: false).Result;

        [HttpPost("/stopContinuousDtmfAsync")]
        [Tags("Continuous DTMF")]
        public Task<IActionResult> StopContinuousDtmfAsync(
            string callConnectionId, string target,
            bool useOptions = false, string operationContext = "StopContinuousDtmfContext")
            => HandleContinuousDtmf(callConnectionId, target, start: false, useOptions, operationContext, async: true);

        [HttpPost("/stopContinuousDtmf")]
        [Tags("Continuous DTMF")]
        public IActionResult StopContinuousDtmf(
            string callConnectionId, string target,
            bool useOptions = false, string operationContext = "StopContinuousDtmfContext")
            => HandleContinuousDtmf(callConnectionId, target, start: false, useOptions, operationContext, async: false).Result;

        #endregion

        // ???????????????????????????????????????????????????????????????????????????
        //  PRIVATE HANDLERS
        // ???????????????????????????????????????????????????????????????????????????

        private FileSource BuildFileSource() => new FileSource(new Uri(_config.CallbackUriHost + "/audio/prompt.wav"));

        // ?? Play ????????????????????????????????????????????????????????????????????

        private async Task<IActionResult> HandlePlay(
            string callConnectionId, string target,
            bool useOptions, bool interruptHoldAudio, bool loop,
            string operationContext, bool async)
        {
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            _logger.LogInformation("Play. CallId={CallId}, Target={Target}, UseOptions={UseOptions}", callConnectionId, target, useOptions);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var fileSource = BuildFileSource();
                var targets = new List<CommunicationIdentifier> { identifier };

                Response<PlayResult> result;
                if (useOptions)
                {
                    // Overload: Play(PlayOptions, CancellationToken)
                    var opts = new PlayOptions(fileSource, targets)
                    {
                        OperationContext = operationContext,
                        InterruptHoldAudio = interruptHoldAudio,
                        Loop = loop
                    };
                    result = async ? await callMedia.PlayAsync(opts) : callMedia.Play(opts);
                }
                else
                {
                    // Overload: Play(PlaySource, IEnumerable<CommunicationIdentifier>, CancellationToken)
                    result = async ? await callMedia.PlayAsync(fileSource, targets) : callMedia.Play(fileSource, targets);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = result.GetRawResponse().Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Play");
                return Problem($"Failed to play: {ex.Message}");
            }
        }

        // ?? PlayToAll ???????????????????????????????????????????????????????????????

        private async Task<IActionResult> HandlePlayToAll(
            string callConnectionId,
            bool useOptions, bool interruptCallMediaOperation, bool loop,
            string operationContext, bool async)
        {
            _logger.LogInformation("PlayToAll. CallId={CallId}, UseOptions={UseOptions}", callConnectionId, useOptions);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var fileSource = BuildFileSource();

                Response<PlayResult> result;
                if (useOptions)
                {
                    // Overload: PlayToAll(PlayToAllOptions, CancellationToken)
                    var opts = new PlayToAllOptions(fileSource)
                    {
                        OperationContext = operationContext,
                        InterruptCallMediaOperation = interruptCallMediaOperation,
                        Loop = loop
                    };
                    result = async ? await callMedia.PlayToAllAsync(opts) : callMedia.PlayToAll(opts);
                }
                else
                {
                    // Overload: PlayToAll(PlaySource, CancellationToken)
                    result = async ? await callMedia.PlayToAllAsync(fileSource) : callMedia.PlayToAll(fileSource);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = result.GetRawResponse().Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PlayToAll");
                return Problem($"Failed to play to all: {ex.Message}");
            }
        }

        // ?? Hold ????????????????????????????????????????????????????????????????????

        private async Task<IActionResult> HandleHold(
            string callConnectionId, string target,
            bool useOptions, bool playHoldMusic, string operationContext, bool async)
        {
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            _logger.LogInformation("Hold. CallId={CallId}, Target={Target}, UseOptions={UseOptions}, PlayMusic={PlayMusic}",
                callConnectionId, target, useOptions, playHoldMusic);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);

                if (useOptions)
                {
                    // Overload: Hold(HoldOptions, CancellationToken)
                    var opts = new HoldOptions(identifier) { OperationContext = operationContext };
                    if (playHoldMusic) opts.PlaySource = BuildFileSource();
                    if (async) await callMedia.HoldAsync(opts); else callMedia.Hold(opts);
                }
                else if (playHoldMusic)
                {
                    // Overload: Hold(CommunicationIdentifier, PlaySource, CancellationToken)
                    if (async) await callMedia.HoldAsync(identifier, BuildFileSource());
                    else callMedia.Hold(identifier, BuildFileSource());
                }
                else
                {
                    // Overload: Hold(CommunicationIdentifier, CancellationToken)
                    if (async) await callMedia.HoldAsync(identifier); else callMedia.Hold(identifier);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Hold");
                return Problem($"Failed to hold: {ex.Message}");
            }
        }

        // ?? Unhold ??????????????????????????????????????????????????????????????????

        private async Task<IActionResult> HandleUnhold(
            string callConnectionId, string target,
            bool useOptions, string operationContext, bool async)
        {
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            _logger.LogInformation("Unhold. CallId={CallId}, Target={Target}, UseOptions={UseOptions}", callConnectionId, target, useOptions);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);

                if (useOptions)
                {
                    // Overload: Unhold(UnholdOptions, CancellationToken)
                    var opts = new UnholdOptions(identifier) { OperationContext = operationContext };
                    if (async) await callMedia.UnholdAsync(opts); else callMedia.Unhold(opts);
                }
                else
                {
                    // Overload: Unhold(CommunicationIdentifier, CancellationToken)
                    if (async) await callMedia.UnholdAsync(identifier); else callMedia.Unhold(identifier);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Unhold");
                return Problem($"Failed to unhold: {ex.Message}");
            }
        }

        // ?? MediaStreaming ??????????????????????????????????????????????????????????

        private async Task<IActionResult> HandleMediaStreaming(
            string callConnectionId, bool start, string operationContext, bool async)
        {
            _logger.LogInformation("{Action} media streaming. CallId={CallId}", start ? "Starting" : "Stopping", callConnectionId);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var callbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                Response response;

                if (start)
                {
                    var opts = new StartMediaStreamingOptions { OperationContext = operationContext, OperationCallbackUri = callbackUri };
                    response = async ? await callMedia.StartMediaStreamingAsync(opts) : callMedia.StartMediaStreaming(opts);
                }
                else
                {
                    var opts = new StopMediaStreamingOptions { OperationContext = operationContext, OperationCallbackUri = callbackUri };
                    response = async ? await callMedia.StopMediaStreamingAsync(opts) : callMedia.StopMediaStreaming(opts);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = response.Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during media streaming operation");
                return Problem($"Failed media streaming operation: {ex.Message}");
            }
        }

        // ?? CreateCallWithMediaStreaming ?????????????????????????????????????????????

        private async Task<IActionResult> HandleCreateCallWithMediaStreaming(
            string target, bool isMixed, bool enableMediaStreaming,
            bool enableBidirectional, bool pcm24kMono, bool async)
        {
            if (string.IsNullOrEmpty(target)) return BadRequest("Target is required");
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            bool isPstn = target.StartsWith("+");
            var audioChannel = isMixed ? MediaStreamingAudioChannel.Mixed : MediaStreamingAudioChannel.Unmixed;
            _logger.LogInformation("CreateCallWithMediaStreaming. Target={Target}, Channel={Channel}", target, audioChannel);
            try
            {
                var callbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                var websocketUri = _config.CallbackUriHost.Replace("https", "wss") + "/ws";

                var mediaOpts = new MediaStreamingOptions(
                    new Uri(websocketUri), MediaStreamingContent.Audio, audioChannel,
                    MediaStreamingTransport.Websocket, enableMediaStreaming)
                {
                    EnableBidirectional = enableBidirectional,
                    AudioFormat = pcm24kMono ? AudioFormat.Pcm24KMono : AudioFormat.Pcm16KMono
                };

                var invite = isPstn
                    ? new CallInvite(new PhoneNumberIdentifier(target), new PhoneNumberIdentifier(_config.AcsPhoneNumber))
                    : new CallInvite(new CommunicationUserIdentifier(target));

                var createOpts = new CreateCallOptions(invite, callbackUri) { MediaStreamingOptions = mediaOpts };
                var result = async
                    ? await _service.GetCallAutomationClient().CreateCallAsync(createOpts)
                    : _service.GetCallAutomationClient().CreateCall(createOpts);

                var props = result.Value.CallConnectionProperties;
                return Ok(new CallConnectionResponse { CallConnectionId = props.CallConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating call with media streaming");
                return Problem($"Failed to create call with media streaming: {ex.Message}");
            }
        }

        // ?? CancelAllMediaOperations ????????????????????????????????????????????????

        private async Task<IActionResult> HandleCancelAllMediaOperations(string callConnectionId, bool async)
        {
            _logger.LogInformation("CancelAllMediaOperations. CallId={CallId}", callConnectionId);
            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var callMedia = _service.GetCallMedia(callConnectionId);
                var result = async ? await callMedia.CancelAllMediaOperationsAsync() : callMedia.CancelAllMediaOperations();
                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = result.GetRawResponse().Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling all media operations");
                return Problem($"Failed to cancel all media operations: {ex.Message}");
            }
        }

        // ?? Recognize ???????????????????????????????????????????????????????????????

        private async Task<IActionResult> HandleRecognize(
            string callConnectionId, string target, string recognizeType,
            int maxTonesToCollect, bool interruptPrompt,
            int initialSilenceTimeoutSec, int interToneTimeoutSec, int endSilenceTimeoutSec,
            string operationContext, bool async)
        {
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");
            if (!Enum.TryParse<RecognizeType>(recognizeType, ignoreCase: true, out var type))
                return BadRequest($"Invalid recognizeType '{recognizeType}'. Valid: Dtmf, Choice, Speech, SpeechOrDtmf");

            _logger.LogInformation("Recognize. CallId={CallId}, Type={Type}", callConnectionId, type);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var fileSource = BuildFileSource();

                switch (type)
                {
                    case RecognizeType.Dtmf:
                        var dtmfOpts = new CallMediaRecognizeDtmfOptions(identifier, maxTonesToCollect: maxTonesToCollect)
                        { Prompt = fileSource, InterruptPrompt = interruptPrompt, InitialSilenceTimeout = TimeSpan.FromSeconds(initialSilenceTimeoutSec), InterToneTimeout = TimeSpan.FromSeconds(interToneTimeoutSec), OperationContext = operationContext };
                        if (async) await callMedia.StartRecognizingAsync(dtmfOpts); else callMedia.StartRecognizing(dtmfOpts);
                        break;
                    case RecognizeType.Choice:
                        var choiceOpts = new CallMediaRecognizeChoiceOptions(identifier, GetChoices())
                        { Prompt = fileSource, InterruptPrompt = interruptPrompt, InitialSilenceTimeout = TimeSpan.FromSeconds(initialSilenceTimeoutSec), OperationContext = operationContext };
                        if (async) await callMedia.StartRecognizingAsync(choiceOpts); else callMedia.StartRecognizing(choiceOpts);
                        break;
                    case RecognizeType.Speech:
                        var speechOpts = new CallMediaRecognizeSpeechOptions(identifier)
                        { Prompt = fileSource, InterruptPrompt = interruptPrompt, InitialSilenceTimeout = TimeSpan.FromSeconds(initialSilenceTimeoutSec), EndSilenceTimeout = TimeSpan.FromSeconds(endSilenceTimeoutSec), OperationContext = operationContext };
                        if (async) await callMedia.StartRecognizingAsync(speechOpts); else callMedia.StartRecognizing(speechOpts);
                        break;
                    case RecognizeType.SpeechOrDtmf:
                        var bothOpts = new CallMediaRecognizeSpeechOrDtmfOptions(identifier, maxTonesToCollect)
                        { Prompt = fileSource, InterruptPrompt = interruptPrompt, InitialSilenceTimeout = TimeSpan.FromSeconds(initialSilenceTimeoutSec), EndSilenceTimeout = TimeSpan.FromSeconds(endSilenceTimeoutSec), OperationContext = operationContext };
                        if (async) await callMedia.StartRecognizingAsync(bothOpts); else callMedia.StartRecognizing(bothOpts);
                        break;
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recognition");
                return Problem($"Failed to start recognition: {ex.Message}");
            }
        }

        private IEnumerable<RecognitionChoice> GetChoices() =>
            new List<RecognitionChoice>
            {
                new RecognitionChoice("yes", new[] { "yes", "yeah" }),
                new RecognitionChoice("no", new[] { "no", "nope" })
            };

        // ?? InterruptAudioAndAnnounce ???????????????????????????????????????????????

        private async Task<IActionResult> HandleInterruptAudioAndAnnounce(
            string callConnectionId, string target, string operationContext, bool async)
        {
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            _logger.LogInformation("InterruptAudioAndAnnounce. CallId={CallId}, Target={Target}", callConnectionId, target);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var opts = new InterruptAudioAndAnnounceOptions(BuildFileSource(), identifier) { OperationContext = operationContext };
                if (async) await callMedia.InterruptAudioAndAnnounceAsync(opts); else callMedia.InterruptAudioAndAnnounce(opts);
                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in InterruptAudioAndAnnounce");
                return Problem($"Failed to interrupt audio and announce: {ex.Message}");
            }
        }

        // ?? Transcription ???????????????????????????????????????????????????????????

        private enum TranscriptionAction { Start, Stop }

        private async Task<IActionResult> HandleTranscription(
            string callConnectionId, TranscriptionAction action, string? locale, string operationContext, bool async)
        {
            _logger.LogInformation("{Action} transcription. CallId={CallId}, Locale={Locale}", action, callConnectionId, locale);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);

                if (action == TranscriptionAction.Start)
                {
                    var opts = new StartTranscriptionOptions { Locale = locale ?? "en-US", OperationContext = operationContext };
                    if (async) await callMedia.StartTranscriptionAsync(opts); else callMedia.StartTranscription(opts);
                }
                else
                {
                    var opts = new StopTranscriptionOptions { OperationContext = operationContext };
                    if (async) await callMedia.StopTranscriptionAsync(opts); else callMedia.StopTranscription(opts);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transcription {Action}", action);
                return Problem($"Failed to {action.ToString().ToLower()} transcription: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleUpdateTranscription(
            string callConnectionId, string locale, bool useOptions, string operationContext, bool async)
        {
            _logger.LogInformation("UpdateTranscription. CallId={CallId}, Locale={Locale}, UseOptions={UseOptions}", callConnectionId, locale, useOptions);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);
                var props = _service.GetCallConnectionProperties(callConnectionId);

                if (useOptions)
                {
                    // Overload: UpdateTranscription(UpdateTranscriptionOptions, CancellationToken)
                    var opts = new UpdateTranscriptionOptions(locale) { OperationContext = operationContext };
                    if (async) await callMedia.UpdateTranscriptionAsync(opts); else callMedia.UpdateTranscription(opts);
                }
                else
                {
                    // Overload: UpdateTranscription(string locale, CancellationToken)
                    if (async) await callMedia.UpdateTranscriptionAsync(locale); else callMedia.UpdateTranscription(locale);
                }

                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transcription");
                return Problem($"Failed to update transcription: {ex.Message}");
            }
        }

        // ?? SendDtmfTones ???????????????????????????????????????????????????????????

        private async Task<IActionResult> HandleSendDtmfTones(
            string callConnectionId, string target, string tones,
            bool useOptions, string operationContext, bool async)
        {
            if (string.IsNullOrEmpty(callConnectionId)) return BadRequest("callConnectionId is required");
            var parsedTones = ParseTones(tones);
            if (parsedTones == null || parsedTones.Count == 0)
                return BadRequest("At least one valid tone is required. Valid: zero-nine, pound, asterisk (or 0-9, #, *)");
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            _logger.LogInformation("SendDtmfTones [{Tones}]. CallId={CallId}, UseOptions={UseOptions}", string.Join(",", parsedTones), callConnectionId, useOptions);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);

                if (useOptions)
                {
                    // Overload: SendDtmfTones(SendDtmfTonesOptions, CancellationToken)
                    var opts = new SendDtmfTonesOptions(parsedTones, identifier)
                    { OperationContext = operationContext, OperationCallbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks") };
                    if (async) await callMedia.SendDtmfTonesAsync(opts); else callMedia.SendDtmfTones(opts);
                }
                else
                {
                    // Overload: SendDtmfTones(IEnumerable<DtmfTone>, CommunicationIdentifier, CancellationToken)
                    if (async) await callMedia.SendDtmfTonesAsync(parsedTones, identifier);
                    else callMedia.SendDtmfTones(parsedTones, identifier);
                }

                var props = _service.GetCallConnectionProperties(callConnectionId);
                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending DTMF tones");
                return Problem($"Failed to send DTMF tones: {ex.Message}");
            }
        }

        // ?? ContinuousDtmf ?????????????????????????????????????????????????????????

        private async Task<IActionResult> HandleContinuousDtmf(
            string callConnectionId, string target, bool start,
            bool useOptions, string operationContext, bool async)
        {
            if (string.IsNullOrEmpty(callConnectionId)) return BadRequest("callConnectionId is required");
            var identifier = ResolveIdentifier(target);
            if (identifier == null) return BadRequest("target must be an ACS user ID (8:...) or phone number (+...)");

            var action = start ? "Starting" : "Stopping";
            _logger.LogInformation("{Action} continuous DTMF. CallId={CallId}, UseOptions={UseOptions}", action, callConnectionId, useOptions);
            try
            {
                var callMedia = _service.GetCallMedia(callConnectionId);

                if (start)
                {
                    if (useOptions)
                    {
                        // Overload: StartContinuousDtmfRecognition(ContinuousDtmfRecognitionOptions, CancellationToken)
                        var opts = new ContinuousDtmfRecognitionOptions(identifier) { OperationContext = operationContext };
                        if (async) await callMedia.StartContinuousDtmfRecognitionAsync(opts); else callMedia.StartContinuousDtmfRecognition(opts);
                    }
                    else
                    {
                        // Overload: StartContinuousDtmfRecognition(CommunicationIdentifier, CancellationToken)
                        if (async) await callMedia.StartContinuousDtmfRecognitionAsync(identifier);
                        else callMedia.StartContinuousDtmfRecognition(identifier);
                    }
                }
                else
                {
                    if (useOptions)
                    {
                        var opts = new ContinuousDtmfRecognitionOptions(identifier) { OperationContext = operationContext };
                        if (async) await callMedia.StopContinuousDtmfRecognitionAsync(opts); else callMedia.StopContinuousDtmfRecognition(opts);
                    }
                    else
                    {
                        if (async) await callMedia.StopContinuousDtmfRecognitionAsync(identifier);
                        else callMedia.StopContinuousDtmfRecognition(identifier);
                    }
                }

                var props = _service.GetCallConnectionProperties(callConnectionId);
                return Ok(new CallConnectionResponse { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Status = props.CallConnectionState.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error {Action} continuous DTMF", action.ToLower());
                return Problem($"Failed to {action.ToLower()} continuous DTMF: {ex.Message}");
            }
        }

        // ???????????????????????????????????????????????????????????????????????????
        //  SHARED HELPERS
        // ???????????????????????????????????????????????????????????????????????????

        private enum RecognizeType { Dtmf, Choice, Speech, SpeechOrDtmf }

        private static CommunicationIdentifier? ResolveIdentifier(string? target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (target.StartsWith("8:")) return new CommunicationUserIdentifier(target);
            if (target.StartsWith("+")) return new PhoneNumberIdentifier(target);
            return null;
        }

        private static List<DtmfTone>? ParseTones(string? tones)
        {
            if (string.IsNullOrWhiteSpace(tones)) return null;
            var map = new Dictionary<string, DtmfTone>(StringComparer.OrdinalIgnoreCase)
            {
                ["zero"] = DtmfTone.Zero, ["one"] = DtmfTone.One, ["two"] = DtmfTone.Two,
                ["three"] = DtmfTone.Three, ["four"] = DtmfTone.Four, ["five"] = DtmfTone.Five,
                ["six"] = DtmfTone.Six, ["seven"] = DtmfTone.Seven, ["eight"] = DtmfTone.Eight,
                ["nine"] = DtmfTone.Nine, ["pound"] = DtmfTone.Pound, ["asterisk"] = DtmfTone.Asterisk,
                ["0"] = DtmfTone.Zero, ["1"] = DtmfTone.One, ["2"] = DtmfTone.Two,
                ["3"] = DtmfTone.Three, ["4"] = DtmfTone.Four, ["5"] = DtmfTone.Five,
                ["6"] = DtmfTone.Six, ["7"] = DtmfTone.Seven, ["8"] = DtmfTone.Eight,
                ["9"] = DtmfTone.Nine, ["#"] = DtmfTone.Pound, ["*"] = DtmfTone.Asterisk
            };
            return tones.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(t => map.ContainsKey(t)).Select(t => map[t]).ToList();
        }
    }
}
