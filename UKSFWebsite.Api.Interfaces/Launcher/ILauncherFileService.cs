using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Launcher;

namespace UKSFWebsite.Api.Interfaces.Launcher {
    public interface ILauncherFileService : IDataBackedService<ILauncherFileDataService> {
        Task UpdateAllVersions();
        FileStreamResult GetLauncherFile(params string[] file);
        Task<Stream> GetUpdatedFiles(IEnumerable<LauncherFile> files);
    }
}
