namespace UKSF.Api.Core.Services;

public interface IFileSystemService
{
    bool FileExists(string path);
    void DeleteFile(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    long GetFileLength(string path);
    bool AreFilesEqual(string sourcePath, string destPath);
    void CopyFile(string source, string destination, bool overwrite);
    void CreateDirectory(string path);
}

public class FileSystemService : IFileSystemService
{
    private const int ComparisonBufferSize = 81920;

    public bool FileExists(string path) => File.Exists(path);

    public void DeleteFile(string path) => File.Delete(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public bool AreFilesEqual(string sourcePath, string destPath)
    {
        var sourceLength = new FileInfo(sourcePath).Length;
        var destLength = new FileInfo(destPath).Length;

        if (sourceLength != destLength)
        {
            return false;
        }

        using var sourceStream = File.OpenRead(sourcePath);
        using var destStream = File.OpenRead(destPath);

        var sourceBuffer = new byte[ComparisonBufferSize];
        var destBuffer = new byte[ComparisonBufferSize];

        int sourceBytesRead;
        while ((sourceBytesRead = sourceStream.Read(sourceBuffer, 0, ComparisonBufferSize)) > 0)
        {
            var destBytesRead = destStream.ReadAtLeast(destBuffer, sourceBytesRead);
            if (sourceBytesRead != destBytesRead || !sourceBuffer.AsSpan(0, sourceBytesRead).SequenceEqual(destBuffer.AsSpan(0, destBytesRead)))
            {
                return false;
            }
        }

        return true;
    }

    public void CopyFile(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
