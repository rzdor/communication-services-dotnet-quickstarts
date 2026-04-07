using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Communication;
using Azure.Communication.CallAutomation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Call_Automation_GCCH.Models;
using Call_Automation_GCCH.Services;
using System.ComponentModel.DataAnnotations;

namespace Call_Automation_GCCH.Controllers
{
    [ApiController]
    [Route("api/recordings")]
    [Produces("application/json")]
    public class RecordingsController : ControllerBase
    {
        private readonly ICallAutomationService _service;
        private readonly ILogger<RecordingsController> _logger;
        private readonly AcsCommunicationSettings _config;

        public RecordingsController(
            ICallAutomationService service,
            ILogger<RecordingsController> logger,
            IOptions<AcsCommunicationSettings> configOptions)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configOptions.Value ?? throw new ArgumentNullException(nameof(configOptions));
        }

        /// <summary>
        /// Starts a recording with configurable options asynchronously
        /// </summary>
        /// <param name="callConnectionId">The call connection ID for the call to record</param>
        /// <param name="isAudioVideo">True for AudioVideo content, false for Audio only</param>
        /// <param name="recordingFormat">Recording format (valid options: Mp3, Mp4, Wav)</param>
        /// <param name="isMixed">True for mixed channel (all participants combined), false for unmixed (separate streams)</param>
        /// <param name="isRecordingWithCallConnectionId">Whether to use call connection ID for recording</param>
        /// <param name="isPauseOnStart">Whether to pause recording on start</param>
        [HttpPost("startRecordingAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> StartRecordingAsync(
            string callConnectionId,
            bool isAudioVideo = false,
            string recordingFormat = "Mp3",
            bool isMixed = true,
            bool isRecordingWithCallConnectionId = true,
            bool isPauseOnStart = false)
        {
            try
            {
                // Validate required parameter
                if (string.IsNullOrWhiteSpace(callConnectionId))
                {
                    return BadRequest("CallConnectionId is required.");
                }

                // Trim whitespace from text inputs (except callConnectionId)
                recordingFormat = recordingFormat?.Trim() ?? "Mp3";

                // Convert bool parameters to enums
                var recordingContent = isAudioVideo ? RecordingContent.AudioVideo : RecordingContent.Audio;
                var recordingChannel = isMixed ? RecordingChannel.Mixed : RecordingChannel.Unmixed;

                // Parse and validate recording format using switch case
                RecordingFormat format;
                switch (recordingFormat.ToLowerInvariant())
                {
                    case "mp3":
                        format = RecordingFormat.Mp3;
                        break;
                    case "mp4":
                        format = RecordingFormat.Mp4;
                        break;
                    case "wav":
                        format = RecordingFormat.Wav;
                        break;
                    default:
                        return BadRequest($"Invalid recording format '{recordingFormat}'. Valid options: Mp3, Mp4, Wav");
                }

                // Validate format compatibility
                if (recordingContent == RecordingContent.AudioVideo && format != RecordingFormat.Mp4)
                {
                    return BadRequest("AudioVideo content is only supported with Mp4 format.");
                }

                CallConnectionProperties callConnectionProperties = _service.GetCallConnectionProperties(callConnectionId);
                var serverCallId = callConnectionProperties.ServerCallId;
                var correlationId = callConnectionProperties.CorrelationId;
                CallLocator callLocator = new ServerCallLocator(serverCallId);

                var recordingOptions = isRecordingWithCallConnectionId
                    ? new StartRecordingOptions(callConnectionProperties.CallConnectionId)
                    : new StartRecordingOptions(callLocator);

                recordingOptions.RecordingContent = recordingContent;
                recordingOptions.RecordingFormat = format;
                recordingOptions.RecordingChannel = recordingChannel;
                recordingOptions.RecordingStateCallbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                recordingOptions.PauseOnStart = isPauseOnStart;

                _service.RecordingFileFormat = format.ToString();

                var recordingResult = await _service.GetCallAutomationClient().GetCallRecording().StartAsync(recordingOptions);
                var recordingId = recordingResult.Value.RecordingId;

                string successMessage = $"Recording started. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Content: {recordingContent}, Format: {format}, Channel: {recordingChannel}, Status: {recordingResult.GetRawResponse().Status.ToString()}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording started. RecordingId: {recordingId}. Content: {recordingContent}, Format: {format}, Channel: {recordingChannel}, Status: {recordingResult.GetRawResponse().Status.ToString()}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error starting recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to start recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Starts a recording with configurable options synchronously
        /// </summary>
        /// <param name="callConnectionId">The call connection ID for the call to record</param>
        /// <param name="isAudioVideo">True for AudioVideo content, false for Audio only</param>
        /// <param name="recordingFormat">Recording format (valid options: Mp3, Mp4, Wav)</param>
        /// <param name="isMixed">True for mixed channel (all participants combined), false for unmixed (separate streams)</param>
        /// <param name="isRecordingWithCallConnectionId">Whether to use call connection ID for recording</param>
        /// <param name="isPauseOnStart">Whether to pause recording on start</param>
        [HttpPost("startRecording")]
        [Tags("Recording APIs")]
        public IActionResult StartRecording(
            string callConnectionId,
            bool isAudioVideo = false,
            string recordingFormat = "Mp3",
            bool isMixed = true,
            bool isRecordingWithCallConnectionId = true,
            bool isPauseOnStart = false)
        {
            try
            {
                // Validate required parameter
                if (string.IsNullOrWhiteSpace(callConnectionId))
                {
                    return BadRequest("CallConnectionId is required.");
                }

                // Trim whitespace from text inputs (except callConnectionId)
                recordingFormat = recordingFormat?.Trim() ?? "Mp3";

                // Convert bool parameters to enums
                var recordingContent = isAudioVideo ? RecordingContent.AudioVideo : RecordingContent.Audio;
                var recordingChannel = isMixed ? RecordingChannel.Mixed : RecordingChannel.Unmixed;

                // Parse and validate recording format using switch case
                RecordingFormat format;
                switch (recordingFormat.ToLowerInvariant())
                {
                    case "mp3":
                        format = RecordingFormat.Mp3;
                        break;
                    case "mp4":
                        format = RecordingFormat.Mp4;
                        break;
                    case "wav":
                        format = RecordingFormat.Wav;
                        break;
                    default:
                        return BadRequest($"Invalid recording format '{recordingFormat}'. Valid options: Mp3, Mp4, Wav");
                }

                // Validate format compatibility
                if (recordingContent == RecordingContent.AudioVideo && format != RecordingFormat.Mp4)
                {
                    return BadRequest("AudioVideo content is only supported with Mp4 format.");
                }

                CallConnectionProperties callConnectionProperties = _service.GetCallConnectionProperties(callConnectionId);
                var serverCallId = callConnectionProperties.ServerCallId;
                var correlationId = callConnectionProperties.CorrelationId;
                CallLocator callLocator = new ServerCallLocator(serverCallId);

                var recordingOptions = isRecordingWithCallConnectionId
                    ? new StartRecordingOptions(callConnectionProperties.CallConnectionId)
                    : new StartRecordingOptions(callLocator);

                recordingOptions.RecordingContent = recordingContent;
                recordingOptions.RecordingFormat = format;
                recordingOptions.RecordingChannel = recordingChannel;
                recordingOptions.RecordingStateCallbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                recordingOptions.PauseOnStart = isPauseOnStart;

                _service.RecordingFileFormat = format.ToString();

                var recordingResult = _service.GetCallAutomationClient().GetCallRecording().Start(recordingOptions);
                var recordingId = recordingResult.Value.RecordingId;

                string successMessage = $"Recording started. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Content: {recordingContent}, Format: {format}, Channel: {recordingChannel}, Status: {recordingResult.GetRawResponse().Status.ToString()}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording started. RecordingId: {recordingId}. Content: {recordingContent}, Format: {format}, Channel: {recordingChannel}, Status: {recordingResult.GetRawResponse().Status.ToString()}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error starting recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to start recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Pauses a recording asynchronously
        /// </summary>
        [HttpPost("pauseRecordingAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> PauseRecordingAsync(
            string callConnectionId,
            string recordingId)
        {
            try
            {
                var callConnection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var pauseResult = await _service.GetCallAutomationClient().GetCallRecording().PauseAsync(recordingId);

                string successMessage = $"Recording paused successfully. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Status: {pauseResult.Status}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording paused. RecordingId: {recordingId}. Status: {pauseResult.Status}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error pausing recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to pause recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Pauses a recording synchronously
        /// </summary>
        [HttpPost("pauseRecording")]
        [Tags("Recording APIs")]
        public IActionResult PauseRecording(
            string callConnectionId,
            string recordingId)
        {
            try
            {
                var callConnection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var pauseResult = _service.GetCallAutomationClient().GetCallRecording().Pause(recordingId);

                string successMessage = $"Recording paused successfully. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Status: {pauseResult.Status}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording paused. RecordingId: {recordingId}. Status: {pauseResult.Status}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error pausing recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to pause recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Resumes a recording asynchronously
        /// </summary>
        [HttpPost("resumeRecordingAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> ResumeRecordingAsync(
            string callConnectionId,
            string recordingId)
        {
            try
            {
                var callConnection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var resumeRecordingResult = await _service.GetCallAutomationClient().GetCallRecording().ResumeAsync(recordingId);

                string successMessage = $"Recording resumed successfully. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Status: {resumeRecordingResult.Status}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording resumed. RecordingId: {recordingId}. Status: {resumeRecordingResult.Status}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error resuming recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to resume recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Resumes a recording synchronously
        /// </summary>
        [HttpPost("resumeRecording")]
        [Tags("Recording APIs")]
        public IActionResult ResumeRecording(
            string callConnectionId,
            string recordingId)
        {
            try
            {
                var callConnection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var resumeRecordingResult = _service.GetCallAutomationClient().GetCallRecording().Resume(recordingId);

                string successMessage = $"Recording resumed successfully. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Status: {resumeRecordingResult.Status}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording resumed. RecordingId: {recordingId}. Status: {resumeRecordingResult.Status}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error resuming recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to resume recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Stops a recording asynchronously
        /// </summary>
        [HttpPost("stopRecordingAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> StopRecordingAsync(
            string callConnectionId,
            string recordingId)
        {
            try
            {
                var callConnection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var stopRecordingResult = await _service.GetCallAutomationClient().GetCallRecording().StopAsync(recordingId);

                string successMessage = $"Recording stopped successfully. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Status: {stopRecordingResult.Status}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording stopped. RecordingId: {recordingId}. Status: {stopRecordingResult.Status}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error stopping recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to stop recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Stops a recording synchronously
        /// </summary>
        [HttpPost("stopRecording")]
        [Tags("Recording APIs")]
        public IActionResult StopRecording(
            string callConnectionId,
            string recordingId)
        {
            try
            {
                var callConnection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var stopRecordingResult = _service.GetCallAutomationClient().GetCallRecording().Stop(recordingId);

                string successMessage = $"Recording stopped successfully. RecordingId: {recordingId}. CallConnectionId: {callConnectionId}, CorrelationId: {correlationId}, Status: {stopRecordingResult.Status}";
                _logger.LogInformation(successMessage);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording stopped. RecordingId: {recordingId}. Status: {stopRecordingResult.Status}"
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error stopping recording: {ex.Message}. CallConnectionId: {callConnectionId}";
                _logger.LogError(errorMessage);
                return Problem($"Failed to stop recording: {ex.Message}. CallConnectionId: {callConnectionId}");
            }
        }

        /// <summary>
        /// Downloads the recording to a local temp file asynchronously.
        /// </summary>
        [HttpPost("downloadRecordingAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> DownloadRecordingAsync(string callConnectionId)
        {
            try
            {
                var location = _service.RecordingLocation;
                var format = _service.RecordingFileFormat;

                if (string.IsNullOrEmpty(location))
                    return Problem("Recording location is not available from the events.");
                if (string.IsNullOrEmpty(format))
                    return Problem("Recording file format is not available from the events.");

                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;
                var date = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileName = $"Recording_{callConnectionId}_{date}.{format}";

                var tempDir = Path.Combine(Path.GetTempPath(), "call-recordings");
                Directory.CreateDirectory(tempDir);
                var localFilePath = Path.Combine(tempDir, fileName);

                using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
                var downloadResult = await _service.GetCallAutomationClient().GetCallRecording().DownloadToAsync(new Uri(location), fileStream);

                _logger.LogInformation("Recording downloaded. Path={Path}, Status={Status}", localFilePath, downloadResult.Status);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording downloaded. Path: {localFilePath}, Status: {downloadResult.Status}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading recording. CallConnectionId={CallConnectionId}", callConnectionId);
                return Problem($"Failed to download recording: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads the recording to a local temp file synchronously.
        /// </summary>
        [HttpPost("downloadRecording")]
        [Tags("Recording APIs")]
        public IActionResult DownloadRecording(string callConnectionId)
        {
            try
            {
                var location = _service.RecordingLocation;
                var format = _service.RecordingFileFormat;

                if (string.IsNullOrEmpty(location))
                    return Problem("Recording location is not available from the events.");
                if (string.IsNullOrEmpty(format))
                    return Problem("Recording file format is not available from the events.");

                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;
                var date = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileName = $"Recording_{callConnectionId}_{date}.{format}";

                var tempDir = Path.Combine(Path.GetTempPath(), "call-recordings");
                Directory.CreateDirectory(tempDir);
                var localFilePath = Path.Combine(tempDir, fileName);

                using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
                var downloadResult = _service.GetCallAutomationClient().GetCallRecording().DownloadTo(new Uri(location), fileStream);

                _logger.LogInformation("Recording downloaded. Path={Path}, Status={Status}", localFilePath, downloadResult.Status);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = $"Recording downloaded. Path: {localFilePath}, Status: {downloadResult.Status}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading recording. CallConnectionId={CallConnectionId}", callConnectionId);
                return Problem($"Failed to download recording: {ex.Message}");
            }
        }

        // ──────────── GET RECORDING STATE ──────────────────────────────────────────

        [HttpGet("getRecordingStateAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> GetRecordingStateAsync(string recordingId)
        {
            try
            {
                if (string.IsNullOrEmpty(recordingId))
                    return BadRequest("RecordingId is required");

                var result = await _service.GetCallAutomationClient().GetCallRecording().GetStateAsync(recordingId);

                _logger.LogInformation($"Recording state retrieved. RecordingId: {recordingId}, Status: {result.Value.RecordingState}");

                return Ok(new
                {
                    RecordingId = recordingId,
                    RecordingState = result.Value.RecordingState?.ToString(),
                    Status = result.GetRawResponse().Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recording state. RecordingId: {RecordingId}", recordingId);
                return Problem($"Failed to get recording state: {ex.Message}");
            }
        }

        [HttpGet("getRecordingState")]
        [Tags("Recording APIs")]
        public IActionResult GetRecordingState(string recordingId)
        {
            try
            {
                if (string.IsNullOrEmpty(recordingId))
                    return BadRequest("RecordingId is required");

                var result = _service.GetCallAutomationClient().GetCallRecording().GetState(recordingId);

                _logger.LogInformation($"Recording state retrieved. RecordingId: {recordingId}, Status: {result.Value.RecordingState}");

                return Ok(new
                {
                    RecordingId = recordingId,
                    RecordingState = result.Value.RecordingState?.ToString(),
                    Status = result.GetRawResponse().Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recording state. RecordingId: {RecordingId}", recordingId);
                return Problem($"Failed to get recording state: {ex.Message}");
            }
        }

        // ──────────── DELETE RECORDING ─────────────────────────────────────────────

        [HttpDelete("deleteRecordingAsync")]
        [Tags("Recording APIs")]
        public async Task<IActionResult> DeleteRecordingAsync(string recordingLocation)
        {
            try
            {
                if (string.IsNullOrEmpty(recordingLocation))
                    return BadRequest("Recording location URL is required");

                var result = await _service.GetCallAutomationClient().GetCallRecording().DeleteAsync(new Uri(recordingLocation));

                _logger.LogInformation($"Recording deleted. Location: {recordingLocation}, Status: {result.Status}");

                return Ok(new { RecordingLocation = recordingLocation, Status = result.Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recording. Location: {RecordingLocation}", recordingLocation);
                return Problem($"Failed to delete recording: {ex.Message}");
            }
        }

        [HttpDelete("deleteRecording")]
        [Tags("Recording APIs")]
        public IActionResult DeleteRecording(string recordingLocation)
        {
            try
            {
                if (string.IsNullOrEmpty(recordingLocation))
                    return BadRequest("Recording location URL is required");

                var result = _service.GetCallAutomationClient().GetCallRecording().Delete(new Uri(recordingLocation));

                _logger.LogInformation($"Recording deleted. Location: {recordingLocation}, Status: {result.Status}");

                return Ok(new { RecordingLocation = recordingLocation, Status = result.Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recording. Location: {RecordingLocation}", recordingLocation);
                return Problem($"Failed to delete recording: {ex.Message}");
            }
        }
    }
}
