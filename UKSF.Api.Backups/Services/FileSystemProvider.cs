namespace UKSF.Api.Backups.Services;

public interface IFileSystemProvider
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    IEnumerable<string> GetDirectories(string path);
    IEnumerable<string> GetFiles(string path);
    IEnumerable<string> GetDrives();
    long GetFileSize(string path);
    DateTime GetLastWriteTimeUtc(string path);
    Stream OpenRead(string path);
    void CreateDirectory(string path);
    void WriteAllText(string path, string contents);
    void DeleteFile(string path);
    void DeleteDirectory(string path);
    Stream Create(string path);
}

public class FileSystemProvider : IFileSystemProvider
{
    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        return Directory.EnumerateDirectories(path);
    }

    public IEnumerable<string> GetFiles(string path)
    {
        return Directory.EnumerateFiles(path);
    }

    public IEnumerable<string> GetDrives()
    {
        return DriveInfo.GetDrives().Where(x => x.IsReady).Select(x => x.Name);
    }

    public long GetFileSize(string path)
    {
        return new FileInfo(path).Length;
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return File.GetLastWriteTimeUtc(path);
    }

    // Sources are live: TeamSpeak's sqlite db, nginx logs and the API's own files are all open elsewhere.
    public Stream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(path, contents);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    public Stream Create(string path)
    {
        return File.Create(path);
    }
}
