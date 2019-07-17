using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Models.Launcher;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ILauncherFileService : IDataService<LauncherFile> {
        Task UpdateAllVersions();
        FileStreamResult GetLauncherFile(params string[] file);
        Task<Stream> GetUpdatedFiles(IEnumerable<LauncherFile> files);
    }
}
