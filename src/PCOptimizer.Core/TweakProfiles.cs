namespace PCOptimizer.Core;

public sealed record SavedTweakProfile(int SchemaVersion, string Name, IReadOnlyList<string> TweakIds);

public static class TweakProfiles
{
    public static void Validate(SavedTweakProfile profile)
    {
        if (profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80
            || profile.Name.Any(char.IsControl) || profile.TweakIds is null || profile.TweakIds.Count is < 1 or > 256
            || profile.TweakIds.Distinct(StringComparer.Ordinal).Count() != profile.TweakIds.Count)
            throw new InvalidDataException("Formato de perfil no admitido.");
        var definitions = profile.TweakIds.Select(ExecutableTweaks.Get).ToArray();
        if (definitions.Where(t => t.ExclusiveGroup is not null).GroupBy(t => t.ExclusiveGroup).Any(g => g.Count() > 1))
            throw new InvalidDataException("El perfil contiene ajustes mutuamente excluyentes.");
    }
    public static SavedTweakProfile Read(string path)
    {
        var profile = LocalJsonFile.Read<SavedTweakProfile>(path) ?? throw new InvalidDataException("El perfil está vacío.");
        Validate(profile);
        return profile;
    }
    public static void Save(string path, SavedTweakProfile profile)
    {
        Validate(profile);
        LocalJsonFile.Write(path, profile);
    }
}
