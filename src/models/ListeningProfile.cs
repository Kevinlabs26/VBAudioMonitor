namespace VBAudioMonitor;

internal sealed record ListeningProfile(
    string Name,
    string Source,
    string Target,
    int Channel,
    int Latency,
    int Volume,
    int UnmuteVolume = 750)
{
    public override string ToString() => Name;

    internal static bool IsValidName(string? name)
    {
        if (name == null || name.Any(char.IsControl)) return false;
        return name.Trim().Length is >= 1 and <= 24;
    }

    internal bool IsValid => IsValidName(Name)
        && !string.IsNullOrWhiteSpace(Source) && !string.IsNullOrWhiteSpace(Target) && Source != Target
        && Channel >= 0 && Channel % 2 == 0
        && Latency is 60 or 100 or 200
        && Volume is >= 0 and <= 1000
        && UnmuteVolume is >= 1 and <= 1000;
}
