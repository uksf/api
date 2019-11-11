using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Launcher;
using UKSFWebsite.Api.Models.Launcher;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Launcher {
    public class LauncherFileService : ILauncherFileService {
        private readonly ILauncherFileDataService data;

        public LauncherFileService(ILauncherFileDataService data) => this.data = data;

        public ILauncherFileDataService Data() => data;

        public async Task UpdateAllVersions() {
            List<LauncherFile> storedVersions = data.Get();
            string launcherDirectory = Path.Combine(VariablesWrapper.VariablesDataService().GetSingle("LAUNCHER_LOCATION").AsString(), "Launcher");
            List<string> fileNames = new List<string>();
            foreach (string filePath in Directory.EnumerateFiles(launcherDirectory)) {
                string fileName = Path.GetFileName(filePath);
                string version = FileVersionInfo.GetVersionInfo(filePath).FileVersion;
                fileNames.Add(fileName);
                LauncherFile storedFile = storedVersions.FirstOrDefault(x => x.fileName == fileName);
                if (storedFile == null) {
                    await data.Add(new LauncherFile {fileName = fileName, version = version});
                    continue;
                }

                if (storedFile.version != version) {
                    await data.Update(storedFile.id, Builders<LauncherFile>.Update.Set(x => x.version, version));
                }
            }

            foreach (LauncherFile storedVersion in storedVersions.Where(storedVersion => fileNames.All(x => x != storedVersion.fileName))) {
                await data.Delete(storedVersion.id);
            }
        }

        public FileStreamResult GetLauncherFile(params string[] file) {
            string[] paths = file.Prepend(VariablesWrapper.VariablesDataService().GetSingle("LAUNCHER_LOCATION").AsString()).ToArray();
            string path = Path.Combine(paths);
            FileStreamResult fileStreamResult = new FileStreamResult(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), MimeUtility.GetMimeMapping(path));
            return fileStreamResult;
        }

        public async Task<Stream> GetUpdatedFiles(IEnumerable<LauncherFile> files) {
            string launcherDirectory = Path.Combine(VariablesWrapper.VariablesDataService().GetSingle("LAUNCHER_LOCATION").AsString(), "Launcher");
            List<LauncherFile> storedVersions = data.Get();
            List<string> updatedFiles = new List<string>();
            List<string> deletedFiles = new List<string>();
            foreach (LauncherFile launcherFile in files) {
                LauncherFile storedFile = storedVersions.FirstOrDefault(x => x.fileName == launcherFile.fileName);
                if (storedFile == null) {
                    deletedFiles.Add(launcherFile.fileName);
                    continue;
                }

                if (storedFile.version != launcherFile.version || new Random().Next(0, 100) > 80) { //TODO: remove before release
                    updatedFiles.Add(launcherFile.fileName);
                }
            }

            string updateFolderName = Guid.NewGuid().ToString("N");
            string updateFolder = Path.Combine(VariablesWrapper.VariablesDataService().GetSingle("LAUNCHER_LOCATION").AsString(), updateFolderName);
            Directory.CreateDirectory(updateFolder);

            string deletedFilesPath = Path.Combine(updateFolder, "deleted");
            File.WriteAllLines(deletedFilesPath, deletedFiles);

            foreach (string file in updatedFiles) {
                File.Copy(Path.Combine(launcherDirectory, file), Path.Combine(updateFolder, file), true);
            }

            string updateZipPath = Path.Combine(VariablesWrapper.VariablesDataService().GetSingle("LAUNCHER_LOCATION").AsString(), $"{updateFolderName}.zip");
            ZipFile.CreateFromDirectory(updateFolder, updateZipPath);
            MemoryStream stream = new MemoryStream();
            await using (FileStream fileStream = new FileStream(updateZipPath, FileMode.Open, FileAccess.Read, FileShare.None)) {
                await fileStream.CopyToAsync(stream);
            }

            File.Delete(updateZipPath);
            Directory.Delete(updateFolder, true);

            stream.Position = 0;
            return stream;
        }
    }
}
