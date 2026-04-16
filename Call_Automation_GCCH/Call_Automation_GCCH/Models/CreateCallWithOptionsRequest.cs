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
    }

    /// <summary>
    /// Request body for creating a group call with CreateGroupCallOptions.
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
        /// Transcription configuration. Set to null or omit to disable transcription.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TranscriptionOptionsRequest? TranscriptionOptions { get; set; }

        /// <summary>
        /// Media streaming configuration. Set to null or omit to disable media streaming.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MediaStreamingOptionsRequest? MediaStreamingOptions { get; set; }
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
    }

    /// <summary>
    /// Request body for starting a recording.
    /// 
    /// Example — basic recording:
    /// { "callConnectionId": "..." }
    /// 
    /// Example — recording with custom options:
    /// {
    ///   "callConnectionId": "...",
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
        /// The call connection ID for the call to record.
        /// </summary>
        [Required]
        public string CallConnectionId { get; set; } = default!;

        /// <summary>
        /// Whether to use call connection ID (true) or server call locator (false) for recording.
        /// </summary>
        /// <example>true</example>
        public bool UseCallConnectionId { get; set; } = true;

        /// <summary>
        /// Recording configuration. Omit for defaults (Audio, Mp3, Mixed, no pause).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecordingOptionsRequest? RecordingOptions { get; set; }
    }
}
