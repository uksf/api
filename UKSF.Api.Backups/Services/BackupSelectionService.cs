using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Backups.Services;

public interface IBackupSelectionService
{
    IEnumerable<DomainBackupEntry> GetEntries();
    DomainBackupEntry GetEntry(string id);
    Task<DomainBackupEntry> AddEntry(DomainBackupEntry entry);
    Task<DomainBackupEntry> UpdateEntry(DomainBackupEntry entry);
    Task DeleteEntry(string id);
}

public class BackupSelectionService(IBackupEntriesContext context, IFileSystemProvider fileSystemProvider) : IBackupSelectionService
{
    public IEnumerable<DomainBackupEntry> GetEntries()
    {
        return context.Get().OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase);
    }

    public DomainBackupEntry GetEntry(string id)
    {
        return context.GetSingle(id) ?? throw new UksfException("Backup entry not found", 404);
    }

    public async Task<DomainBackupEntry> AddEntry(DomainBackupEntry entry)
    {
        var prepared = Prepare(entry);
        Validate(prepared);

        await context.Add(prepared);
        return prepared;
    }

    public async Task<DomainBackupEntry> UpdateEntry(DomainBackupEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry?.Id))
        {
            throw new UksfException("Backup entry id is required", 400);
        }

        GetEntry(entry.Id);

        var prepared = Prepare(entry);
        Validate(prepared);

        await context.Replace(prepared);
        return prepared;
    }

    public async Task DeleteEntry(string id)
    {
        GetEntry(id);
        await context.Delete(id);
    }

    private static DomainBackupEntry Prepare(DomainBackupEntry entry)
    {
        if (entry is null)
        {
            throw new UksfException("Backup entry is required", 400);
        }

        return new DomainBackupEntry
        {
            Id = entry.Id,
            Path = BackupPaths.Normalise(entry.Path),
            EntryType = entry.EntryType,
            Recursive = entry.EntryType == BackupEntryType.Folder && entry.Recursive,
            IncludePatterns =
                (entry.IncludePatterns ?? []).Select(x => x?.Trim())
                                             .Where(x => !string.IsNullOrWhiteSpace(x))
                                             .Distinct(StringComparer.OrdinalIgnoreCase)
                                             .ToList(),
            Excludes = (entry.Excludes ?? []).Select(NormaliseExclude).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Enabled = entry.Enabled
        };
    }

    /// <summary>A bare name pattern stays as typed; anything else is a real path and is normalised as one.</summary>
    private static string NormaliseExclude(string exclude)
    {
        return BackupGlob.IsGlob(exclude) && !BackupGlob.HasSeparator(exclude) ? exclude.Trim() : BackupPaths.Normalise(exclude);
    }

    private void Validate(DomainBackupEntry entry)
    {
        ValidateTarget(entry);
        ValidatePatterns(entry);
        ValidateExcludes(entry);
        ValidateAgainstExisting(entry);
    }

    private static void ValidatePatterns(DomainBackupEntry entry)
    {
        if (entry.IncludePatterns.Count == 0)
        {
            return;
        }

        if (entry.EntryType == BackupEntryType.File)
        {
            throw new UksfException("Patterns can only be set on a folder entry", 400);
        }

        var withSeparator = entry.IncludePatterns.FirstOrDefault(BackupGlob.HasSeparator);
        if (withSeparator is not null)
        {
            throw new UksfException($"A pattern matches a file name, so it cannot contain a path: {withSeparator}", 400);
        }
    }

    private void ValidateTarget(DomainBackupEntry entry)
    {
        if (entry.EntryType == BackupEntryType.Folder)
        {
            if (!fileSystemProvider.DirectoryExists(entry.Path))
            {
                throw new UksfException($"Folder not found: {entry.Path}", 400);
            }

            return;
        }

        if (!fileSystemProvider.FileExists(entry.Path))
        {
            throw new UksfException($"File not found: {entry.Path}", 400);
        }
    }

    private static void ValidateExcludes(DomainBackupEntry entry)
    {
        if (entry.Excludes.Count == 0)
        {
            return;
        }

        if (entry.EntryType == BackupEntryType.File)
        {
            throw new UksfException("Excludes can only be set on a folder entry", 400);
        }

        // A bare name pattern applies at any depth, so only real paths are checked for being inside the selection.
        var outside = entry.Excludes.Where(x => BackupGlob.HasSeparator(x) || !BackupGlob.IsGlob(x))
                           .FirstOrDefault(x => !BackupPaths.Contains(entry.Path, x) || string.Equals(x, entry.Path, StringComparison.OrdinalIgnoreCase));
        if (outside is not null)
        {
            throw new UksfException($"Exclude is not inside the selected folder: {outside}", 400);
        }
    }

    private void ValidateAgainstExisting(DomainBackupEntry entry)
    {
        var others = context.Get(x => x.Id != entry.Id).ToList();

        if (others.Any(x => string.Equals(x.Path, entry.Path, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UksfException($"Path is already selected: {entry.Path}", 400);
        }

        var overlapping = others.FirstOrDefault(x => Overlaps(x, entry));
        if (overlapping is not null)
        {
            throw new UksfException($"Path overlaps an existing selection: {overlapping.Path}", 400);
        }
    }

    private static bool Overlaps(DomainBackupEntry existing, DomainBackupEntry entry)
    {
        if (existing.EntryType == BackupEntryType.Folder && existing.Recursive && BackupPaths.Contains(existing.Path, entry.Path))
        {
            return true;
        }

        return entry.EntryType == BackupEntryType.Folder && entry.Recursive && BackupPaths.Contains(entry.Path, existing.Path);
    }
}
