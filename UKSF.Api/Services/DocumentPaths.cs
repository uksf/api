namespace UKSF.Api.Services;

// Document/folder FullPath is a logical breadcrumb persisted in Mongo with a backslash
// separator (the Windows convention the production host produces). Build it OS-independently
// so dedup comparisons and messages match on any host, not just Windows.
public static class DocumentPaths
{
    private const char Separator = '\\';

    public static string CombineLogical(string parent, string name)
    {
        return string.IsNullOrEmpty(parent) ? name : $"{parent}{Separator}{name}";
    }
}
