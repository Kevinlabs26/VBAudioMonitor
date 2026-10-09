using NAudio.CoreAudioApi;

namespace VBAudioMonitor;

/// <summary>
/// Represents an audio endpoint device with configuration metadata.
/// </summary>
internal sealed record Endpoint(string Id, string Name, int Channels, int SampleRate)
{
    public override string ToString() => Name;

    /// <summary>
    /// Indicates whether this endpoint represents a virtual audio driver.
    /// </summary>
    public bool IsVirtual => Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Enumerates all active playback audio endpoints.
    /// </summary>
    public static List<Endpoint> List()
    {
        using var deviceEnumerator = new MMDeviceEnumerator();
        var discoveredEndpoints = new List<Endpoint>();

        foreach (var endpointDevice in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
            using (endpointDevice)
            using (var audioClientInstance = endpointDevice.AudioClient)
            {
                var mixFormat = audioClientInstance.MixFormat;
                discoveredEndpoints.Add(new Endpoint(
                    endpointDevice.ID,
                    endpointDevice.FriendlyName,
                    mixFormat.Channels,
                    mixFormat.SampleRate));
            }
            }
            catch (System.Runtime.InteropServices.COMException) { }
        }

        return discoveredEndpoints;
    }

    /// <summary>
    /// Selects the most suitable physical output device for monitoring.
    /// </summary>
    public static Endpoint? PreferredOutput(List<Endpoint> availableEndpoints, string? savedEndpointId = null)
    {
        var physicalEndpoints = availableEndpoints.Where(endpoint => !endpoint.IsVirtual).ToList();
        var savedEndpoint = physicalEndpoints.FirstOrDefault(endpoint => endpoint.Id == savedEndpointId);
        if (savedEndpoint != null)
        {
            return savedEndpoint;
        }

        try
        {
            using var deviceEnumerator = new MMDeviceEnumerator();
            using var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var defaultMatch = physicalEndpoints.FirstOrDefault(endpoint => endpoint.Id == defaultDevice.ID);
            if (defaultMatch != null)
            {
                return defaultMatch;
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Default multimedia endpoint is unavailable in some sandbox environments.
        }

        return physicalEndpoints.FirstOrDefault(endpoint => endpoint.Name.Contains("耳机")
            || endpoint.Name.Contains("Headphone", StringComparison.OrdinalIgnoreCase)
            || endpoint.Name.Contains("casque", StringComparison.OrdinalIgnoreCase))
            ?? physicalEndpoints.FirstOrDefault(endpoint => endpoint.Name.Contains("扬声器")
                || endpoint.Name.Contains("Speakers", StringComparison.OrdinalIgnoreCase)
                || endpoint.Name.Contains("Haut-parleurs", StringComparison.OrdinalIgnoreCase))
            ?? physicalEndpoints.FirstOrDefault();
    }
}
