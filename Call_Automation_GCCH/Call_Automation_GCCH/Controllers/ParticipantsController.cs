using System;
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
    [Route("api/participants")]
    [Produces("application/json")]
    public class ParticipantsController : ControllerBase
    {
        private readonly ICallAutomationService _service;
        private readonly ILogger<ParticipantsController> _logger;
        private readonly AcsCommunicationSettings _config;

        public ParticipantsController(
            ICallAutomationService service,
            ILogger<ParticipantsController> logger, IOptions<AcsCommunicationSettings> configOptions)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configOptions.Value ?? throw new ArgumentNullException(nameof(configOptions));
        }

        // ─ Add ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a participant to an active call.
        /// </summary>
        /// <param name="callConnectionId">The call connection ID</param>
        /// <param name="participantId">ACS user ID (8:...) or phone number (+...)</param>
        /// <param name="isPstn">True if participant is a PSTN number</param>
        /// <param name="invitationTimeoutInSeconds">Seconds to wait before the invitation times out</param>
        /// <param name="operationContext">Custom context string for correlating events</param>
        [HttpPost("addParticipant")]
        [Tags("Add/Remove Participant APIs")]
        public IActionResult AddParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn = false,
            int invitationTimeoutInSeconds = 30,
            string operationContext = "addParticipantContext")
            => HandleAddParticipant(callConnectionId, participantId, isPstn, invitationTimeoutInSeconds, operationContext, async: false).Result;

        [HttpPost("addParticipantAsync")]
        [Tags("Add/Remove Participant APIs")]
        public Task<IActionResult> AddParticipantAsync(
            string callConnectionId,
            string participantId,
            bool isPstn = false,
            int invitationTimeoutInSeconds = 30,
            string operationContext = "addParticipantContext")
            => HandleAddParticipant(callConnectionId, participantId, isPstn, invitationTimeoutInSeconds, operationContext, async: true);

        // ─ Remove ────────────────────────────────────────────────────────────────────

        [HttpPost("removeParticipant")]
        [Tags("Add/Remove Participant APIs")]
        public IActionResult RemoveParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn = false,
            string operationContext = "removeParticipantContext")
            => HandleRemoveParticipant(callConnectionId, participantId, isPstn, operationContext, async: false).Result;

        [HttpPost("removeParticipantAsync")]
        [Tags("Add/Remove Participant APIs")]
        public Task<IActionResult> RemoveParticipantAsync(
            string callConnectionId,
            string participantId,
            bool isPstn = false,
            string operationContext = "removeParticipantContext")
            => HandleRemoveParticipant(callConnectionId, participantId, isPstn, operationContext, async: true);

        // ─ Get ───────────────────────────────────────────────────────────────────────

        [HttpGet("getParticipant")]
        [Tags("Get Participant APIs")]
        public IActionResult GetParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn = false)
            => HandleGetParticipant(callConnectionId, participantId, isPstn, async: false).Result;

        [HttpGet("getParticipantAsync")]
        [Tags("Get Participant APIs")]
        public Task<IActionResult> GetParticipantAsync(
            string callConnectionId,
            string participantId,
            bool isPstn = false)
            => HandleGetParticipant(callConnectionId, participantId, isPstn, async: true);

        // ─ Mute ──────────────────────────────────────────────────────────────────────

        [HttpPost("muteParticipant")]
        [Tags("Mute Participant APIs")]
        public IActionResult MuteParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn = false)
            => HandleMuteParticipant(callConnectionId, participantId, isPstn, async: false).Result;

        [HttpPost("muteParticipantAsync")]
        [Tags("Mute Participant APIs")]
        public Task<IActionResult> MuteParticipantAsync(
            string callConnectionId,
            string participantId,
            bool isPstn = false)
            => HandleMuteParticipant(callConnectionId, participantId, isPstn, async: true);

        // ─ Get All Participants ──────────────────────────────────────────────────────

        [HttpGet("getParticipants")]
        [Tags("Get Participant APIs")]
        public IActionResult GetParticipants(string callConnectionId)
            => HandleGetAllParticipants(callConnectionId, async: false).Result;

        [HttpGet("getParticipantsAsync")]
        [Tags("Get Participant APIs")]
        public Task<IActionResult> GetParticipantsAsync(string callConnectionId)
            => HandleGetAllParticipants(callConnectionId, async: true);

        // ─ Cancel Add Participant ────────────────────────────────────────────────────

        [HttpPost("cancelAddParticipant")]
        [Tags("Add/Remove Participant APIs")]
        public IActionResult CancelAddParticipant(
            string callConnectionId,
            string invitationId,
            string operationContext = "cancelAddParticipantContext")
            => HandleCancelAddParticipant(callConnectionId, invitationId, operationContext, async: false).Result;

        [HttpPost("cancelAddParticipantAsync")]
        [Tags("Add/Remove Participant APIs")]
        public Task<IActionResult> CancelAddParticipantAsync(
            string callConnectionId,
            string invitationId,
            string operationContext = "cancelAddParticipantContext")
            => HandleCancelAddParticipant(callConnectionId, invitationId, operationContext, async: true);

        // ─────────────── Shared Handlers ────────────────────────────────────────────

        private async Task<IActionResult> HandleAddParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn,
            int invitationTimeoutInSeconds,
            string operationContext,
            bool async)
        {
            var opName = isPstn ? "PSTN" : "ACS";
            _logger.LogInformation("Adding {OpName} participant {ParticipantId} to call {CallId}, Timeout={Timeout}s",
                opName, participantId, callConnectionId, invitationTimeoutInSeconds);

            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);

                CallInvite invite = isPstn
                    ? new CallInvite(
                          new PhoneNumberIdentifier(participantId),
                          new PhoneNumberIdentifier(_config.AcsPhoneNumber))
                    : new CallInvite(new CommunicationUserIdentifier(participantId));

                var options = new AddParticipantOptions(invite)
                {
                    OperationContext = operationContext,
                    InvitationTimeoutInSeconds = invitationTimeoutInSeconds
                };

                Response<AddParticipantResult> result = async
                    ? await connection.AddParticipantAsync(options)
                    : connection.AddParticipant(options);

                _logger.LogInformation(
                    $"{opName} participant added: Call={callConnectionId}, CorrId={props.CorrelationId}, " +
                    $"Status={result.GetRawResponse().Status}, InviteId={result.Value.InvitationId}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = $"{result.GetRawResponse().Status}; InviteId={result.Value.InvitationId}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding {opName} participant");
                return Problem($"Failed to add participant: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleRemoveParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn,
            string operationContext,
            bool async)
        {
            var opName = isPstn ? "PSTN" : "ACS";
            _logger.LogInformation("Removing {OpName} participant {ParticipantId} from call {CallId}",
                opName, participantId, callConnectionId);

            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);

                var target = isPstn
                    ? (CommunicationIdentifier)new PhoneNumberIdentifier(participantId)
                    : new CommunicationUserIdentifier(participantId);

                var options = new RemoveParticipantOptions(target)
                {
                    OperationContext = operationContext
                };

                Response<RemoveParticipantResult> result = async
                     ? await connection.RemoveParticipantAsync(options)
                     : connection.RemoveParticipant(options);

                _logger.LogInformation(
                    $"{opName} participant removed: Call={callConnectionId}, CorrId={props.CorrelationId}, " +
                    $"Status={result.GetRawResponse().Status}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = $"{result.GetRawResponse().Status}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing {opName} participant");
                return Problem($"Failed to remove participant: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleGetParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn,
            bool async)
        {
            var opName = isPstn ? "PSTN" : "ACS";
            _logger.LogInformation($"Starting to get {opName} participant: {participantId} for call {callConnectionId}");

            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);

                CallParticipant participant = async
                    ? await connection.GetParticipantAsync(
                          isPstn
                            ? (CommunicationIdentifier)new PhoneNumberIdentifier(participantId)
                            : new CommunicationUserIdentifier(participantId))
                    : connection.GetParticipant(
                          isPstn
                            ? (CommunicationIdentifier)new PhoneNumberIdentifier(participantId)
                            : new CommunicationUserIdentifier(participantId));

                if (participant == null)
                    return NotFound(new { callConnectionId, correlationId = props.CorrelationId, Message = "Not found" });

                return Ok(new
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = props.CorrelationId,
                    Participant = new
                    {
                        RawId = participant.Identifier.RawId,
                        IsOnHold = participant.IsOnHold,
                        IsMuted = participant.IsMuted
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {opName} participant");
                return Problem($"Failed to get participant: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleMuteParticipant(
            string callConnectionId,
            string participantId,
            bool isPstn,
            bool async)
        {
            var opName = isPstn ? "PSTN" : "ACS";
            _logger.LogInformation($"Starting to mute {opName} participant: {participantId} for call {callConnectionId}");

            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);

                var target = isPstn
                    ? (CommunicationIdentifier)new PhoneNumberIdentifier(participantId)
                    : new CommunicationUserIdentifier(participantId);

                Response<MuteParticipantResult> result = async
                    ? await connection.MuteParticipantAsync(target)
                    : connection.MuteParticipant(target);

                _logger.LogInformation(
                    $"{opName} participant muted: Call={callConnectionId}, CorrId={props.CorrelationId}, " +
                    $"Status={result.GetRawResponse().Status}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = $"{result.GetRawResponse().Status}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error muting {opName} participant");
                return Problem($"Failed to mute participant: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleGetAllParticipants(
            string callConnectionId,
            bool async)
        {
            _logger.LogInformation($"Getting all participants for call {callConnectionId}");

            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);

                IReadOnlyList<CallParticipant> participantList;

                if (async)
                {
                    var response = await connection.GetParticipantsAsync();
                    participantList = response.Value;
                }
                else
                {
                    var response = connection.GetParticipants();
                    participantList = response.Value;
                }

                var participants = participantList.Select(p => new
                {
                    RawId = p.Identifier.RawId,
                    IsOnHold = p.IsOnHold,
                    IsMuted = p.IsMuted
                }).ToList();

                return Ok(new
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = props.CorrelationId,
                    Participants = participants
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all participants");
                return Problem($"Failed to get participants: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleCancelAddParticipant(
            string callConnectionId,
            string invitationId,
            string operationContext,
            bool async)
        {
            if (string.IsNullOrEmpty(callConnectionId))
                return BadRequest("callConnectionId is required");
            if (string.IsNullOrEmpty(invitationId))
                return BadRequest("invitationId is required");

            _logger.LogInformation("Cancelling add participant. CallId={CallId}, InvitationId={InvitationId}", callConnectionId, invitationId);

            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);

                var options = new CancelAddParticipantOperationOptions(invitationId)
                {
                    OperationContext = operationContext
                };

                var result = async
                    ? await connection.CancelAddParticipantOperationAsync(options)
                    : connection.CancelAddParticipantOperation(options);

                _logger.LogInformation(
                    $"Cancel add participant succeeded: Call={callConnectionId}, CorrId={props.CorrelationId}, " +
                    $"Status={result.GetRawResponse().Status}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = result.GetRawResponse().Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling add participant");
                return Problem($"Failed to cancel add participant: {ex.Message}");
            }
        }
    }
}