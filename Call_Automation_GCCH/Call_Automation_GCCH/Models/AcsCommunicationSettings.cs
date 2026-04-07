namespace Call_Automation_GCCH.Models
{
    /// <summary>
    /// Application settings for Azure Communication Services.
    /// Can be pre-configured in appsettings.json or set at runtime via the
    /// /api/configuration endpoints in Swagger.
    /// </summary>
    public class AcsCommunicationSettings
    {
        public string? AcsConnectionString { get; set; }
        public string? AcsPhoneNumber { get; set; }
        public string? PmaEndpoint { get; set; }
        public string? CallbackUriHost { get; set; }
    }
}
