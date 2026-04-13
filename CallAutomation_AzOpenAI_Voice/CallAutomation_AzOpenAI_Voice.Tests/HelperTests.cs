using CallAutomationOpenAI;

namespace CallAutomation_AzOpenAI_Voice.Tests;

public class HelperTests
{
    private const string SampleServerCallId =
        "aHR0cHM6Ly9hcGkuZXAtZGV2LnNreXBlLm5ldC9hcGkvdjIvZXAvY29udi1pbnQtMTE5LXNkZi1ha3MuY29udi1kZXYuc2t5cGUubmV0L2NvbnYvUi1zWVJMWWllVUd2bEFDYk50dWlUUT9pPTEwLTEyOC0xNC0yMjYmZT02MzkxMTM2MTMyOTMwMDEwMjk=";

    private const string ExpectedGuid = "4418eb47-22b6-4179-af94-009b36dba24d";

    [Fact]
    public void ExtractMediaSessionId_WithValidServerCallId_ReturnsExpectedGuid()
    {
        var result = Helper.ExtractMediaSessionId(SampleServerCallId);
        Assert.Equal(ExpectedGuid, result);
    }

    [Fact]
    public void ExtractMediaSessionId_WithInvalidBase64_Throws()
    {
        Assert.Throws<FormatException>(() => Helper.ExtractMediaSessionId("not-valid-base64!!!"));
    }

    [Fact]
    public void ExtractMediaSessionId_WithNoConvSegment_Throws()
    {
        // Base64 of "https://example.com/no/conv/segment" — but actually has conv, so use one without
        var urlWithoutConv = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("https://example.com/api/v2/other/path"));

        Assert.Throws<ArgumentException>(() => Helper.ExtractMediaSessionId(urlWithoutConv));
    }

    [Fact]
    public void TryDecodeUrlString_ValidEncodedGuid_ReturnsTrue()
    {
        var guid = Guid.Parse(ExpectedGuid);
        var encoded = guid.ToEncodedUrlString();

        var success = encoded.TryDecodeUrlString(out var decoded);

        Assert.True(success);
        Assert.Equal(guid, decoded);
    }

    [Fact]
    public void TryDecodeUrlString_RoundTrip_ReturnsOriginalGuid()
    {
        // The encoded value from the URL in the sample serverCallId
        const string encodedFromUrl = "R-sYRLYieUGvlACbNtuiTQ";

        var success = encodedFromUrl.TryDecodeUrlString(out var guid);

        Assert.True(success);
        Assert.Equal(ExpectedGuid, guid.ToString());
    }

    [Fact]
    public void TryDecodeUrlString_InvalidInput_ReturnsFalse()
    {
        var success = "not_a_valid_guid_encoded".TryDecodeUrlString(out var guid);

        Assert.False(success);
        Assert.Equal(Guid.Empty, guid);
    }

    [Fact]
    public void TryDecodeUrlString_EmptyString_ReturnsFalse()
    {
        var success = "".TryDecodeUrlString(out var guid);

        Assert.False(success);
        Assert.Equal(Guid.Empty, guid);
    }
}
