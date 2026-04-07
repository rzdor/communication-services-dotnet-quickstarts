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
    [Route("api/calls")]
    [Produces("application/json")]
    public class CallController : ControllerBase
    {
        private readonly ICallAutomationService _service;
        private readonly ILogger<CallController> _logger;
        private readonly AcsCommunicationSettings _config;

        public CallController(
            ICallAutomationService service,
            ILogger<CallController> logger,
            IOptions<AcsCommunicationSettings> configOptions)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configOptions.Value ?? throw new ArgumentNullException(nameof(configOptions));
        }

        //
        // CREATE CALL (ACS or PSTN)
        //

        [HttpPost("createCall")]
        [Tags("Outbound Call APIs")]
        public IActionResult CreateCall(
            string target,
            bool isPstn = false)
            => HandleCreateCall(target, isPstn, async: false).Result;

        [HttpPost("createCallAsync")]
        [Tags("Outbound Call APIs")]
        public Task<IActionResult> CreateCallAsync(
            string target,
            bool isPstn = false)
            => HandleCreateCall(target, isPstn, async: true);

        //
        // TRANSFER CALL
        //

        [HttpPost("transferCall")]
        [Tags("Transfer Call APIs")]
        public IActionResult TransferCall(
            string callConnectionId,
            string transferTarget,
            string transferee,
            bool isPstn = false)
            => HandleTransferCall(callConnectionId, transferTarget, transferee, isPstn, async: false).Result;

        [HttpPost("transferCallAsync")]
        [Tags("Transfer Call APIs")]
        public Task<IActionResult> TransferCallAsync(
            string callConnectionId,
            string transferTarget,
            string transferee,
            bool isPstn = false)
            => HandleTransferCall(callConnectionId, transferTarget, transferee, isPstn, async: true);

        //
        // HANG UP
        //

        [HttpPost("hangup")]
        [Tags("Disconnect call APIs")]
        public IActionResult Hangup(
            string callConnectionId,
            bool isForEveryone)
            => HandleHangup(callConnectionId, isForEveryone, async: false).Result;

        [HttpPost("hangupAsync")]
        [Tags("Disconnect call APIs")]
        public Task<IActionResult> HangupAsync(
            string callConnectionId,
            bool isForEveryone)
            => HandleHangup(callConnectionId, isForEveryone, async: true);

        //
        // GROUP CALL (PSTN or ACS if you like—you could extend to both)
        //
        [HttpPost("createGroupCallAsync")]
        [Tags("Group Call APIs")]
        public Task<IActionResult> CreateGroupCallAsync(
            [FromQuery] string targets)
            => HandleGroupCall(targets, async: true);

        [HttpPost("createGroupCall")]
        [Tags("Group Call APIs")]
        public Task<IActionResult> CreateGroupCall(
            [FromQuery] string targets)
            => HandleGroupCall(targets, async: false);

        // You could add a sync version if you really need it...

        //
        // ========  HELPERS  ========
        //

        private async Task<IActionResult> HandleCreateCall(
            string target,
            bool isPstn,
            bool async)
        {
            if (string.IsNullOrEmpty(target))
                return BadRequest("Target is required");

            var idType = isPstn ? "PSTN" : "ACS";
            _logger.LogInformation($"Starting {(async ? "async " : "")}create {idType} call to {target}");

            try
            {
                // Build identifier & invite
                var callbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                CallInvite invite = isPstn
                    ? new CallInvite(new PhoneNumberIdentifier(target),
                                     new PhoneNumberIdentifier(_config.AcsPhoneNumber))
                    : new CallInvite(new CommunicationUserIdentifier(target));

                var options = new CreateCallOptions(invite, callbackUri);

                // Call SDK
                CreateCallResult result = async
                    ? await _service.GetCallAutomationClient().CreateCallAsync(options)
                    : _service.GetCallAutomationClient().CreateCall(options);

                var props = result.CallConnectionProperties;
                _logger.LogInformation(
                    $"Created {idType} call: ConnId={props.CallConnectionId}, CorrId={props.CorrelationId}, Status={props.CallConnectionState}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = props.CallConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = props.CallConnectionState.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating {idType} call");
                return Problem($"Failed to create {idType} call: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleTransferCall(
            string callConnectionId,
            string transferTarget,
            string transferee,
            bool isPstn,
            bool async)
        {
            if (string.IsNullOrEmpty(callConnectionId))
                return BadRequest("callConnectionId is required");

            var idType = isPstn ? "PSTN" : "ACS";
            _logger.LogInformation($"Starting {(async ? "async " : "")}transfer {idType} call: {transferTarget} → {transferee}");

            try
            {
                var connection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                TransferToParticipantOptions options;
                if (isPstn)
                {
                    // PSTN → PSTN
                    options = new TransferToParticipantOptions(new PhoneNumberIdentifier(transferTarget))
                    {
                        OperationContext = "TransferCallContext",
                        Transferee = new PhoneNumberIdentifier(transferee)
                    };
                }
                else
                {
                    // ACS → ACS
                    options = new TransferToParticipantOptions(new CommunicationUserIdentifier(transferTarget))
                    {
                        OperationContext = "TransferCallContext",
                        Transferee = new CommunicationUserIdentifier(transferee)
                    };
                }

                // Call SDK
                Response<TransferCallToParticipantResult> resp = async
                    ? await connection.TransferCallToParticipantAsync(options)
                    : connection.TransferCallToParticipant(options);

                _logger.LogInformation(
                    $"Transfer complete. CallConnId={callConnectionId}, CorrId={correlationId}, Status={resp.GetRawResponse().Status}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = resp.GetRawResponse().Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring {idType} call");
                return Problem($"Failed to transfer {idType} call: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleHangup(
            string callConnectionId,
            bool isForEveryone,
            bool async)
        {
            if (string.IsNullOrEmpty(callConnectionId))
                return BadRequest("callConnectionId is required");

            _logger.LogInformation($"Starting {(async ? "async " : "")}hangup for {callConnectionId}");

            try
            {
                var connection = _service.GetCallConnection(callConnectionId);
                var correlationId = _service.GetCallConnectionProperties(callConnectionId).CorrelationId;

                var resp = async
                    ? await connection.HangUpAsync(isForEveryone)
                    : connection.HangUp(isForEveryone);

                _logger.LogInformation(
                    $"Hangup complete. ConnId={callConnectionId}, CorrId={correlationId}, Status={resp.Status}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = callConnectionId,
                    CorrelationId = correlationId,
                    Status = resp.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hanging up call");
                return Problem($"Failed to hang up call: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleGroupCall(
            string targets,
            bool async)
        {
            if (string.IsNullOrEmpty(targets))
                return BadRequest("Targets parameter is required");

            var targetList = targets.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(t => t.Trim())
                                   .Where(t => !string.IsNullOrWhiteSpace(t))
                                   .ToList();

            if (targetList.Count == 0)
                return BadRequest("At least one target is required");

            _logger.LogInformation($"Starting {(async ? "async " : "")}group call to {string.Join(", ", targetList)}");

            try
            {
                // Build identifiers based on format
                var idList = new List<CommunicationIdentifier>();

                foreach (var target in targetList)
                {
                    if (target.StartsWith("8:"))
                    {
                        // ACS participant
                        idList.Add(new CommunicationUserIdentifier(target));
                    }
                    else
                    {
                        // PSTN participant
                        if (!target.StartsWith("+"))
                            return BadRequest($"PSTN number '{target}' must include country code (e.g., +1 for US)");

                        idList.Add(new PhoneNumberIdentifier(target));
                    }
                }

                var callbackUri = new Uri(new Uri(_config.CallbackUriHost), "/api/callbacks");
                var sourceCallerId = new PhoneNumberIdentifier(_config.AcsPhoneNumber);

                var createGroupOpts = new CreateGroupCallOptions(idList, callbackUri)
                {
                    SourceCallerIdNumber = sourceCallerId,
                    // ... any media/transcription options you need
                };

                CreateCallResult result;
                if (async)
                    result = await _service.GetCallAutomationClient().CreateGroupCallAsync(createGroupOpts);
                else
                    result = _service.GetCallAutomationClient().CreateGroupCall(createGroupOpts);

                var props = result.CallConnectionProperties;
                _logger.LogInformation(
                    $"Group call created. ConnId={props.CallConnectionId}, CorrId={props.CorrelationId}");

                return Ok(new CallConnectionResponse
                {
                    CallConnectionId = props.CallConnectionId,
                    CorrelationId = props.CorrelationId,
                    Status = props.CallConnectionState.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group call");
                return Problem($"Failed to create group call: {ex.Message}");
            }
        }
    }

}
