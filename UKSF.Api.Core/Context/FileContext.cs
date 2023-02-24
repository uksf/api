namespace UKSF.Api.Core.Context;

public interface IFileContext
{
    string AppDirectory { get; }
    Task<string> ReadAllText(string path);
    bool Exists(string path);
    void CreateFile(string path);
    Task WriteTextToFile(string path, string text);
    void Rename(string path, string newPath);
}

public class FileContext : IFileContext
{
    public string AppDirectory => AppDomain.CurrentDomain.BaseDirectory;

    public Task<string> ReadAllText(string path)
    {
        return File.ReadAllTextAsync(path);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Create(path).Close();
    }

    public Task WriteTextToFile(string path, string text)
    {
        return File.WriteAllTextAsync(path, text);
    }

    public void Rename(string path, string newPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.Move(path, newPath);
    }
}
