using System;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Shared.Context
{
    public interface IFileContext
    {
        string AppDirectory { get; }
        Task<string> ReadAllText(string path);
        bool Exists(string path);
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
    }
}
