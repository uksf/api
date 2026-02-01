namespace UKSF.Api.Core.Services;

public interface IFileSystemService
{
    bool FileExists(string path);
    void DeleteFile(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive);
}

public class FileSystemService : IFileSystemService
{
    public bool FileExists(string path) => File.Exists(path);

    public void DeleteFile(string path) => File.Delete(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
}
