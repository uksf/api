namespace UKSF.Api.Modpack.Models;

public readonly struct ModpackVersion : IComparable<ModpackVersion>
{
    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }
    public string VersionString { get; init; }
    public bool IsValid { get; init; }

    public ModpackVersion(string version)
    {
        VersionString = version;
        var versionParts = version.Split('.');

        if (versionParts.Length == 3 &&
            int.TryParse(versionParts[0], out var major) &&
            int.TryParse(versionParts[1], out var minor) &&
            int.TryParse(versionParts[2], out var patch))
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            IsValid = true;
        }
        else
        {
            Major = Minor = Patch = 0;
            IsValid = false;
        }
    }

    public bool IsValidIncrementFrom(ModpackVersion previousVersion)
    {
        if (!IsValid || !previousVersion.IsValid)
        {
            return false;
        }

        if (Major == previousVersion.Major + 1 && Minor == 0 && Patch == 0)
        {
            return true;
        }

        if (Major == previousVersion.Major && Minor == previousVersion.Minor + 1 && Patch == 0)
        {
            return true;
        }

        return Major == previousVersion.Major && Minor == previousVersion.Minor && Patch == previousVersion.Patch + 1;
    }

    public bool IsNewerThan(ModpackVersion otherVersion)
    {
        if (!IsValid || !otherVersion.IsValid)
        {
            return false;
        }

        if (Major > otherVersion.Major)
        {
            return true;
        }

        if (Major < otherVersion.Major)
        {
            return false;
        }

        if (Minor > otherVersion.Minor)
        {
            return true;
        }

        if (Minor < otherVersion.Minor)
        {
            return false;
        }

        return Patch > otherVersion.Patch;
    }

    public int CompareTo(ModpackVersion other)
    {
        if (!IsValid || !other.IsValid)
        {
            return -1;
        }

        return Major != other.Major ? Major.CompareTo(other.Major) :
            Minor != other.Minor    ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
    }

    public override string ToString()
    {
        return IsValid ? $"{Major}.{Minor}.{Patch}" : "Invalid version";
    }
}
