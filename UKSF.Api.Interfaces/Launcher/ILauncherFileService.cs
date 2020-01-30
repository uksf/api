using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Launcher;

namespace UKSF.Api.Interfaces.Launcher {
    public interface ILauncherFileService : IDataBackedService<ILauncherFileDataService> {
        Task UpdateAllVersions();
        FileStreamResult GetLauncherFile(params string[] file);
        Task<Stream> GetUpdatedFiles(IEnumerable<LauncherFile> files);
    }
}
