using Azure.Communication.CallAutomation;
using Azure.Communication.CallAutomation;

namespace Call_Automation_GCCH.Services
{
    /// <summary>
    /// Abstraction over CallAutomationClient for testability and extensibility.
    /// </summary>
    public interface ICallAutomationService
    {
        CallAutomationClient GetCallAutomationClient();
        CallConnection GetCallConnection(string callConnectionId);
        CallMedia GetCallMedia(string callConnectionId);
        CallConnectionProperties GetCallConnectionProperties(string callConnectionId);
        void UpdateClient(string connectionString, string pmaEndpoint);
        string GetCurrentPmaEndpoint();

        string? RecordingLocation { get; set; }
        string RecordingFileFormat { get; set; }
    }
}
