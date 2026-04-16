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

        // - Add -----------------------------------------------------------------------

        /// <summary>Adds a participant to an active call.</summary>
        [HttpPost("addParticipantAsync")]
        [Tags("Add/Remove Participant APIs")]
        public Task<IActionResult> AddParticipantAsync([FromBody] AddParticipantRequest request)
            => HandleAddParticipant(request, async: true);
        [HttpPost("addParticipant")]
        [Tags("Add/Remove Participant APIs")]
        public IActionResult AddParticipant([FromBody] AddParticipantRequest request)
            => HandleAddParticipant(request, async: false).Result;

        /// <summary>Removes a participant from a call.</summary>
        [HttpPost("removeParticipantAsync")]
        [Tags("Add/Remove Participant APIs")]
        public Task<IActionResult> RemoveParticipantAsync([FromBody] RemoveParticipantRequest request)
            => HandleRemoveParticipant(request, async: true);
        [HttpPost("removeParticipant")]
        [Tags("Add/Remove Participant APIs")]
        public IActionResult RemoveParticipant([FromBody] RemoveParticipantRequest request)
            => HandleRemoveParticipant(request, async: false).Result;

        /// <summary>Gets a specific participant's details.</summary>
        [HttpPost("getParticipantAsync")]
        [Tags("Get Participant APIs")]
        public Task<IActionResult> GetParticipantAsync([FromBody] ParticipantIdentifierRequest request)
            => HandleGetParticipant(request, async: true);
        [HttpPost("getParticipant")]
        [Tags("Get Participant APIs")]
        public IActionResult GetParticipant([FromBody] ParticipantIdentifierRequest request)
            => HandleGetParticipant(request, async: false).Result;

        /// <summary>Mutes a participant.</summary>
        [HttpPost("muteParticipantAsync")]
        [Tags("Mute Participant APIs")]
        public Task<IActionResult> MuteParticipantAsync([FromBody] ParticipantIdentifierRequest request)
            => HandleMuteParticipant(request, async: true);
        [HttpPost("muteParticipant")]
        [Tags("Mute Participant APIs")]
        public IActionResult MuteParticipant([FromBody] ParticipantIdentifierRequest request)
            => HandleMuteParticipant(request, async: false).Result;

        /// <summary>Gets all participants in a call.</summary>
        [HttpGet("getParticipantsAsync")]
        [Tags("Get Participant APIs")]
        public Task<IActionResult> GetParticipantsAsync(string callConnectionId)
            => HandleGetAllParticipants(callConnectionId, async: true);
        [HttpGet("getParticipants")]
        [Tags("Get Participant APIs")]
        public IActionResult GetParticipants(string callConnectionId)
            => HandleGetAllParticipants(callConnectionId, async: false).Result;

        /// <summary>Cancels a pending add-participant invitation.</summary>
        [HttpPost("cancelAddParticipantAsync")]
        [Tags("Add/Remove Participant APIs")]
        public Task<IActionResult> CancelAddParticipantAsync([FromBody] CancelAddParticipantRequest request)
            => HandleCancelAddParticipant(request, async: true);
        [HttpPost("cancelAddParticipant")]
        [Tags("Add/Remove Participant APIs")]
        public IActionResult CancelAddParticipant([FromBody] CancelAddParticipantRequest request)
            => HandleCancelAddParticipant(request, async: false).Result;

        // --------------- Shared Handlers --------------------------------------------

        private async Task<IActionResult> HandleAddParticipant(AddParticipantRequest request, bool async)
        {
            var opName = request.IsPstn ? "PSTN" : "ACS";
            _logger.LogInformation("Adding {OpName} participant {ParticipantId} to call {CallId}", opName, request.ParticipantId, request.CallConnectionId);
            try
            {
                var props = _service.GetCallConnectionProperties(request.CallConnectionId);
                var connection = _service.GetCallConnection(request.CallConnectionId);

                CallInvite invite = request.IsPstn
                    ? new CallInvite(new PhoneNumberIdentifier(request.ParticipantId), new PhoneNumberIdentifier(_config.AcsPhoneNumber))
                    : new CallInvite(new CommunicationUserIdentifier(request.ParticipantId));

                var options = new AddParticipantOptions(invite)
                { OperationContext = request.OperationContext, InvitationTimeoutInSeconds = request.InvitationTimeoutInSeconds };

                Response<AddParticipantResult> result = async ? await connection.AddParticipantAsync(options) : connection.AddParticipant(options);

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = request.CallConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = $"{result.GetRawResponse().Status}; InviteId={result.Value.InvitationId}"
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error adding participant"); return Problem($"Failed to add participant: {ex.Message}"); }
        }

        private async Task<IActionResult> HandleRemoveParticipant(RemoveParticipantRequest request, bool async)
        {
            _logger.LogInformation("Removing participant {Id} from call {CallId}", request.ParticipantId, request.CallConnectionId);
            try
            {
                var props = _service.GetCallConnectionProperties(request.CallConnectionId);
                var connection = _service.GetCallConnection(request.CallConnectionId);
                var target = request.IsPstn
                    ? (CommunicationIdentifier)new PhoneNumberIdentifier(request.ParticipantId)
                    : new CommunicationUserIdentifier(request.ParticipantId);
                var options = new RemoveParticipantOptions(target) { OperationContext = request.OperationContext };

                Response<RemoveParticipantResult> result = async ? await connection.RemoveParticipantAsync(options) : connection.RemoveParticipant(options);

                return Ok(new CallConnectionResponse { CallConnectionId = request.CallConnectionId, CorrelationId = props.CorrelationId, Status = $"{result.GetRawResponse().Status}" });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error removing participant"); return Problem($"Failed to remove participant: {ex.Message}"); }
        }

        private async Task<IActionResult> HandleGetParticipant(ParticipantIdentifierRequest request, bool async)
        {
            _logger.LogInformation("Getting participant {Id} for call {CallId}", request.ParticipantId, request.CallConnectionId);
            try
            {
                var props = _service.GetCallConnectionProperties(request.CallConnectionId);
                var connection = _service.GetCallConnection(request.CallConnectionId);
                var id = request.IsPstn
                    ? (CommunicationIdentifier)new PhoneNumberIdentifier(request.ParticipantId)
                    : new CommunicationUserIdentifier(request.ParticipantId);

                CallParticipant participant = async ? await connection.GetParticipantAsync(id) : connection.GetParticipant(id);

                if (participant == null)
                    return NotFound(new { request.CallConnectionId, correlationId = props.CorrelationId, Message = "Not found" });

                return Ok(new { CallConnectionId = request.CallConnectionId, CorrelationId = props.CorrelationId,
                    Participant = new { RawId = participant.Identifier.RawId, IsOnHold = participant.IsOnHold, IsMuted = participant.IsMuted } });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error getting participant"); return Problem($"Failed to get participant: {ex.Message}"); }
        }

        private async Task<IActionResult> HandleMuteParticipant(ParticipantIdentifierRequest request, bool async)
        {
            _logger.LogInformation("Muting participant {Id} on call {CallId}", request.ParticipantId, request.CallConnectionId);
            try
            {
                var props = _service.GetCallConnectionProperties(request.CallConnectionId);
                var connection = _service.GetCallConnection(request.CallConnectionId);
                var target = request.IsPstn
                    ? (CommunicationIdentifier)new PhoneNumberIdentifier(request.ParticipantId)
                    : new CommunicationUserIdentifier(request.ParticipantId);

                Response<MuteParticipantResult> result = async ? await connection.MuteParticipantAsync(target) : connection.MuteParticipant(target);

                return Ok(new CallConnectionResponse { CallConnectionId = request.CallConnectionId, CorrelationId = props.CorrelationId, Status = $"{result.GetRawResponse().Status}" });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error muting participant"); return Problem($"Failed to mute participant: {ex.Message}"); }
        }

        private async Task<IActionResult> HandleGetAllParticipants(string callConnectionId, bool async)
        {
            _logger.LogInformation("Getting all participants for call {CallId}", callConnectionId);
            try
            {
                var props = _service.GetCallConnectionProperties(callConnectionId);
                var connection = _service.GetCallConnection(callConnectionId);
                var response = async ? await connection.GetParticipantsAsync() : connection.GetParticipants();

                var participants = response.Value.Select(p => new
                {
                    RawId = p.Identifier.RawId,
                    IsOnHold = p.IsOnHold,
                    IsMuted = p.IsMuted
                }).ToList();

                return Ok(new { CallConnectionId = callConnectionId, CorrelationId = props.CorrelationId, Participants = participants });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all participants");
                return Problem($"Failed to get participants: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleCancelAddParticipant(CancelAddParticipantRequest request, bool async)
        {
            if (string.IsNullOrEmpty(request.CallConnectionId))
                return BadRequest("callConnectionId is required");
            if (string.IsNullOrEmpty(request.InvitationId))
                return BadRequest("invitationId is required");

            _logger.LogInformation("Cancelling add participant. CallId={CallId}, InvitationId={InvitationId}", request.CallConnectionId, request.InvitationId);
            try
            {
                var props = _service.GetCallConnectionProperties(request.CallConnectionId);
                var connection = _service.GetCallConnection(request.CallConnectionId);
                var options = new CancelAddParticipantOperationOptions(request.InvitationId) { OperationContext = request.OperationContext };

                var result = async ? await connection.CancelAddParticipantOperationAsync(options) : connection.CancelAddParticipantOperation(options);

                return Ok(new CallConnectionResponse
                { CallConnectionId = request.CallConnectionId, CorrelationId = props.CorrelationId, Status = result.GetRawResponse().Status.ToString() });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error cancelling add participant"); return Problem($"Failed to cancel add participant: {ex.Message}"); }
        }
    }
}
