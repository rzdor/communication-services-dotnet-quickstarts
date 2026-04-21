using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Call_Automation_GCCH.Models
{
    /// <summary>
    /// Transcription configuration. Maps to Azure SDK TranscriptionOptions.
    /// </summary>
    public class TranscriptionOptionsRequest
    {
        /// <summary>
        /// Locale for speech recognition (e.g. en-US, es-ES, fr-FR).
        /// Maps to: TranscriptionOptions.Locale
        /// </summary>
        /// <example>en-US</example>
        [DefaultValue("en-US")]
        public string Locale { get; set; } = "en-US";

        /// <summary>
        /// Start transcription immediately when the call connects.
        /// Maps to: TranscriptionOptions.StartTranscription
        /// </summary>
        /// <example>false</example>
        public bool StartTranscription { get; set; } = false;

        /// <summary>
        /// Enable intermediate (partial) transcription results.
        /// Maps to: TranscriptionOptions.EnableIntermediateResults
        /// </summary>
        /// <example>false</example>
        public bool EnableIntermediateResults { get; set; } = false;
    }

    /// <summary>
    /// Media streaming configuration. Maps to Azure SDK MediaStreamingOptions.
    /// </summary>
    public class MediaStreamingOptionsRequest
    {
        /// <summary>
        /// Start media streaming immediately when the call connects.
        /// Maps to: MediaStreamingOptions.StartMediaStreaming
        /// </summary>
        /// <example>false</example>
        public bool StartMediaStreaming { get; set; } = false;

        /// <summary>
        /// Audio channel mode: "Mixed" (all participants combined) or "Unmixed" (separate per participant).
        /// Maps to: MediaStreamingOptions.MediaStreamingAudioChannel
        /// </summary>
        /// <example>Mixed</example>
        [DefaultValue("Mixed")]
        public string MediaStreamingAudioChannel { get; set; } = "Mixed";

        /// <summary>
        /// Enable bidirectional media streaming (send audio back into the call).
        /// Maps to: MediaStreamingOptions.EnableBidirectional
        /// </summary>
        /// <example>false</example>
        public bool EnableBidirectional { get; set; } = false;

        /// <summary>
        /// Audio format: "Pcm16KMono" or "Pcm24KMono".
        /// Maps to: MediaStreamingOptions.AudioFormat
        /// </summary>
        /// <example>Pcm16KMono</example>
        [DefaultValue("Pcm16KMono")]
        public string AudioFormat { get; set; } = "Pcm16KMono";
    }

    /// <summary>
    /// Call intelligence configuration for AI-powered features.
    /// Maps to Azure SDK CallIntelligenceOptions.
    /// </summary>
    public class CallIntelligenceOptionsRequest
    {
        /// <summary>
        /// The Cognitive Services endpoint URI for AI features (e.g. speech-to-text).
        /// Maps to: CallIntelligenceOptions.CognitiveServicesEndpoint
        /// </summary>
        /// <example>https://my-cognitive-services.cognitiveservices.azure.us/</example>
        [Required]
        public string CognitiveServicesEndpoint { get; set; } = default!;
    }

    /// <summary>
    /// Request body for creating an outbound call with CreateCallOptions.
    /// 
    /// To enable transcription: include the "transcriptionOptions" object.
    /// To enable media streaming: include the "mediaStreamingOptions" object.
    /// To disable either feature: set it to null or remove it from the JSON body.
    /// 
    /// Example — basic call (no transcription, no streaming):
    /// {
    ///   "target": "+18001234567",
    ///   "isPstn": true
    /// }
    /// 
    /// Example — call with transcription only:
    /// {
    ///   "target": "+18001234567",
    ///   "isPstn": true,
    ///   "transcriptionOptions": {
    ///     "locale": "en-US",
    ///     "startTranscription": true
    ///   }
    /// }
    /// 
    /// Example — call with both transcription and media streaming:
    /// {
    ///   "target": "+18001234567",
    ///   "isPstn": true,
    ///   "transcriptionOptions": {
    ///     "locale": "en-US",
    ///     "startTranscription": true,
    ///     "enableIntermediateResults": true
    ///   },
    ///   "mediaStreamingOptions": {
    ///     "startMediaStreaming": true,
    ///     "mediaStreamingAudioChannel": "Mixed",
    ///     "enableBidirectional": false,
    ///     "audioFormat": "Pcm16KMono"
    ///   }
    /// }
    /// </summary>
    public class CreateCallWithOptionsRequest
    {
        /// <summary>
        /// Target phone number (+1234567890) or ACS user ID (8:acs:...).
        /// </summary>
        /// <example>+18001234567</example>
        [Required]
        public string Target { get; set; } = default!;

        /// <summary>
        /// True if the target is a PSTN phone number, false for ACS user.
        /// </summary>
        /// <example>true</example>
        public bool IsPstn { get; set; } = false;

        /// <summary>
        /// Custom context string for correlating callback events.
        /// </summary>
        /// <example>createCallContext</example>
        [DefaultValue("createCallContext")]
        public string OperationContext { get; set; } = "createCallContext";

        /// <summary>
        /// Transcription configuration. Set to null or omit to disable transcription.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TranscriptionOptionsRequest? TranscriptionOptions { get; set; }

        /// <summary>
        /// Media streaming configuration. Set to null or omit to disable media streaming.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MediaStreamingOptionsRequest? MediaStreamingOptions { get; set; }

        /// <summary>
        /// Call intelligence configuration for AI features. Omit to disable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CallIntelligenceOptionsRequest? CallIntelligenceOptions { get; set; }
    }

    /// <summary>
    /// Request body for creating a group call
    /// 
    /// To enable transcription: include the "transcriptionOptions" object.
    /// To enable media streaming: include the "mediaStreamingOptions" object.
    /// To disable either feature: set it to null or remove it from the JSON body.
    /// 
    /// Example — basic group call:
    /// {
    ///   "targets": ["+18001234567", "+18009876543"]
    /// }
    /// 
    /// Example — group call with transcription:
    /// {
    ///   "targets": ["+18001234567"],
    ///   "transcriptionOptions": {
    ///     "locale": "en-US",
    ///     "startTranscription": true
    ///   }
    /// }
    /// </summary>
    public class CreateGroupCallWithOptionsRequest
    {
        /// <summary>
        /// List of target phone numbers (+...) and/or ACS user IDs (8:...).
        /// </summary>
        /// <example>["+18001234567", "+18009876543"]</example>
        [Required]
        [MinLength(1, ErrorMessage = "At least one target is required.")]
        public List<string> Targets { get; set; } = new();

        /// <summary>
        /// Custom context string for correlating callback events.
        /// </summary>
        /// <example>groupCallContext</example>
        [DefaultValue("groupCallContext")]
        public string OperationContext { get; set; } = "groupCallContext";

        /// <summary>
        /// Display name shown as the caller. Maps to: CreateGroupCallOptions.SourceDisplayName
        /// </summary>
        /// <example>Contoso Support</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SourceDisplayName { get; set; }

        /// <summary>
        /// Transcription configuration. Set to null or omit to disable transcription.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TranscriptionOptionsRequest? TranscriptionOptions { get; set; }

        /// <summary>
        /// Media streaming configuration. Set to null or omit to disable media streaming.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MediaStreamingOptionsRequest? MediaStreamingOptions { get; set; }

        /// <summary>
        /// Call intelligence configuration for AI features. Omit to disable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CallIntelligenceOptionsRequest? CallIntelligenceOptions { get; set; }
    }

    /// <summary>
    /// Request body for connecting to an existing server call.
    /// 
    /// Example — basic connect (no streaming/transcription):
    /// { "serverCallId": "aHR0cHM6..." }
    /// 
    /// Example — connect with media streaming:
    /// {
    ///   "serverCallId": "aHR0cHM6...",
    ///   "mediaStreamingOptions": { "startMediaStreaming": false, "mediaStreamingAudioChannel": "Mixed" }
    /// }
    /// </summary>
    public class ConnectCallRequest
    {
        /// <summary>
        /// The server call ID to connect to.
        /// </summary>
        /// <example>aHR0cHM6Ly9...</example>
        [Required]
        public string ServerCallId { get; set; } = default!;

        /// <summary>
        /// Custom context string for correlating events.
        /// </summary>
        /// <example>ConnectCallContext</example>
        [DefaultValue("ConnectCallContext")]
        public string OperationContext { get; set; } = "ConnectCallContext";

        /// <summary>
        /// Transcription configuration. Omit or set to null to disable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TranscriptionOptionsRequest? TranscriptionOptions { get; set; }

        /// <summary>
        /// Media streaming configuration. Omit or set to null to disable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MediaStreamingOptionsRequest? MediaStreamingOptions { get; set; }

        /// <summary>
        /// Call intelligence configuration for AI features. Omit to disable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CallIntelligenceOptionsRequest? CallIntelligenceOptions { get; set; }
    }

    /// <summary>
    /// Recording configuration for StartRecording.
    /// </summary>
    public class RecordingOptionsRequest
    {
        /// <summary>
        /// True for AudioVideo content, false for Audio only.
        /// </summary>
        /// <example>false</example>
        public bool IsAudioVideo { get; set; } = false;

        /// <summary>
        /// Recording format: "Mp3", "Mp4", or "Wav".
        /// AudioVideo content requires Mp4.
        /// </summary>
        /// <example>Mp3</example>
        [DefaultValue("Mp3")]
        public string RecordingFormat { get; set; } = "Mp3";

        /// <summary>
        /// True for mixed channel (all participants combined), false for unmixed (separate streams).
        /// </summary>
        /// <example>true</example>
        public bool IsMixed { get; set; } = true;

        /// <summary>
        /// Whether to pause recording on start. Use Resume to begin recording later.
        /// </summary>
        /// <example>false</example>
        public bool PauseOnStart { get; set; } = false;

        /// <summary>
        /// Azure Blob Storage container URI for external recording storage.
        /// Omit to use default ACS recording storage.
        /// </summary>
        /// <example>https://myaccount.blob.core.usgovcloudapi.net/recordings</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExternalStorageContainerUri { get; set; }

        /// <summary>
        /// Channel affinity list to map specific participants to specific audio channels.
        /// Each entry maps a participant identifier to a channel number (0-based).
        /// Only applicable when IsMixed is false (unmixed recording).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ChannelAffinityRequest>? ChannelAffinity { get; set; }
    }

    /// <summary>
    /// Maps a participant to a specific audio channel for unmixed recording.
    /// </summary>
    public class ChannelAffinityRequest
    {
        /// <summary>
        /// Participant identifier: ACS user ID (8:...) or phone number (+...).
        /// </summary>
        /// <example>+18001234567</example>
        [Required]
        public string Participant { get; set; } = default!;

        /// <summary>
        /// Zero-based audio channel number to assign to this participant.
        /// </summary>
        /// <example>0</example>
        [Required]
        public int Channel { get; set; }
    }

    /// <summary>
    /// Request body for starting a recording.
    /// 
    /// The SDK requires a "call locator" to identify which call to record.
    /// Use "callLocatorType" to choose one of three supported locator strategies:
    /// 
    /// ?????????????????????????????????????????????????????????????????????????????????????
    /// ? callLocatorType      ? Description                                                ?
    /// ?????????????????????????????????????????????????????????????????????????????????????
    /// ? "CallConnectionId"   ? Uses the callConnectionId you already have from             ?
    /// ?   (default)          ? creating/answering the call. Simplest option.               ?
    /// ?????????????????????????????????????????????????????????????????????????????????????
    /// ? "ServerCallLocator"  ? Uses a server call ID. If you omit "serverCallId", it is    ?
    /// ?                      ? auto-resolved from the callConnectionId. You can also       ?
    /// ?                      ? supply it explicitly (e.g. from an IncomingCall event).      ?
    /// ?????????????????????????????????????????????????????????????????????????????????????
    /// ? "GroupCallLocator"   ? Uses a group call ID for group/rooms scenarios.              ?
    /// ?                      ? You MUST provide "groupCallId".                              ?
    /// ?????????????????????????????????????????????????????????????????????????????????????
    /// 
    /// Example 1 — simplest (uses call connection ID):
    /// {
    ///   "callConnectionId": "411f0200-abcd-1234-..."
    /// }
    /// 
    /// Example 2 — server call locator (auto-resolved):
    /// {
    ///   "callConnectionId": "411f0200-abcd-1234-...",
    ///   "callLocatorType": "ServerCallLocator"
    /// }
    /// 
    /// Example 3 — server call locator (explicit ID from IncomingCall event):
    /// {
    ///   "callConnectionId": "411f0200-abcd-1234-...",
    ///   "callLocatorType": "ServerCallLocator",
    ///   "serverCallId": "aHR0cHM6Ly9hcGku..."
    /// }
    /// 
    /// Example 4 — group call locator:
    /// {
    ///   "callConnectionId": "411f0200-abcd-1234-...",
    ///   "callLocatorType": "GroupCallLocator",
    ///   "groupCallId": "29228d3e-fbc0-4fef-..."
    /// }
    /// 
    /// Example 5 — with recording options:
    /// {
    ///   "callConnectionId": "411f0200-abcd-1234-...",
    ///   "recordingOptions": {
    ///     "isAudioVideo": true,
    ///     "recordingFormat": "Mp4",
    ///     "isMixed": true,
    ///     "pauseOnStart": false
    ///   }
    /// }
    /// </summary>
    public class StartRecordingRequest
    {
        /// <summary>
        /// The call connection ID of the active call. Always required — used to resolve
        /// call properties and as the default locator when callLocatorType is "CallConnectionId".
        /// You receive this value from CreateCall, AnswerCall, or ConnectCall responses.
        /// </summary>
        /// <example>411f0200-abcd-1234-5678-000000000000</example>
        [Required]
        public string CallConnectionId { get; set; } = default!;

        /// <summary>
        /// Determines how the SDK locates the call for recording.
        /// 
        /// • "CallConnectionId" (default) — uses callConnectionId directly.
        ///   Simplest option; works for all standard outbound/inbound calls.
        /// 
        /// • "ServerCallLocator" — uses a server call ID. The server call ID is
        ///   auto-resolved from callConnectionId unless you provide "serverCallId" explicitly.
        ///   Useful when you have the server call ID from an IncomingCall event or callback.
        /// 
        /// • "GroupCallLocator" — uses a group call ID for group/rooms scenarios.
        ///   You MUST supply "groupCallId" when using this option.
        /// </summary>
        /// <example>CallConnectionId</example>
        [DefaultValue("CallConnectionId")]
        public string CallLocatorType { get; set; } = "CallConnectionId";

        /// <summary>
        /// The server call ID. Only used when callLocatorType is "ServerCallLocator".
        /// If omitted, the server call ID is automatically resolved from callConnectionId.
        /// You can find this value in the IncomingCall event payload or call connection properties.
        /// </summary>
        /// <example>aHR0cHM6Ly9hcGku...</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ServerCallId { get; set; }

        /// <summary>
        /// The group call ID. Required when callLocatorType is "GroupCallLocator".
        /// This is the ID of the group call you want to record (e.g. from a Rooms or group call scenario).
        /// </summary>
        /// <example>29228d3e-fbc0-4fef-abcd-000000000000</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GroupCallId { get; set; }

        /// <summary>
        /// Recording configuration. Omit for defaults (Audio, Mp3, Mixed, no pause).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecordingOptionsRequest? RecordingOptions { get; set; }
    }
}
