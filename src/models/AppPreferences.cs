namespace VBAudioMonitor;

/// <summary>
/// Persistent user configuration preferences.
/// </summary>
internal sealed record Preferences(
    string Source,
    string Target,
    int Channel,
    int Latency,
    int Volume,
    bool Dark = false,
    int Version = 1,
    Dictionary<string, int>? DeviceVolumes = null,
    Dictionary<string, int>? DeviceUnmuteVolumes = null,
    int UnmuteVolume = 750,
    List<ListeningProfile>? Profiles = null,
    string Language = "zh-CN"
)
{
    internal int SliderVolume => Math.Clamp(Version < 2 && Volume is >= 0 and <= 100 ? Volume * 10 : Volume, 0, 1000);

    internal int TargetVolume(string targetId)
    {
        if (DeviceVolumes != null && DeviceVolumes.TryGetValue(targetId, out int volume))
            return Math.Clamp(volume, 0, 1000);
        return Target == targetId ? SliderVolume : 500;
    }

    internal int TargetUnmuteVolume(string targetId)
    {
        if (DeviceUnmuteVolumes != null && DeviceUnmuteVolumes.TryGetValue(targetId, out int volume))
            return Math.Clamp(volume, 1, 1000);
        int targetVolume = TargetVolume(targetId);
        if (targetVolume > 0) return targetVolume;
        return Target == targetId ? Math.Clamp(UnmuteVolume, 1, 1000) : 500;
    }
}
