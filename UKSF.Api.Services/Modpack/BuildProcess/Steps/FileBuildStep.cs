using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using MoreLinq;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class FileBuildStep : BuildStep {
        private const double FILE_COPY_TASK_SIZE_THRESHOLD = 5_000_000_000;
        private const double FILE_COPY_TASK_OUNT_THRESHOLD = 150;

        internal List<FileInfo> GetDirectoryContents(DirectoryInfo source, string searchPattern = "*") {
            List<FileInfo> files = source.GetFiles(searchPattern).ToList();
            foreach (DirectoryInfo subDirectory in source.GetDirectories()) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                files.AddRange(GetDirectoryContents(subDirectory));
            }

            return files;
        }

        internal async Task AddFiles(string sourcePath, string targetPath) {
            DirectoryInfo source = new DirectoryInfo(sourcePath);
            DirectoryInfo target = new DirectoryInfo(targetPath);
            IEnumerable<FileInfo> sourceFiles = GetDirectoryContents(source);
            List<FileInfo> addedFiles = sourceFiles.Select(sourceFile => new { sourceFile, targetFile = new FileInfo(sourceFile.FullName.Replace(source.FullName, target.FullName)) })
                                                   .Where(x => !x.targetFile.Exists)
                                                   .Select(x => x.sourceFile)
                                                   .ToList();
            await CopyFiles(source, target, addedFiles);
        }

        internal async Task UpdateFiles(string sourcePath, string targetPath) {
            DirectoryInfo source = new DirectoryInfo(sourcePath);
            DirectoryInfo target = new DirectoryInfo(targetPath);
            IEnumerable<FileInfo> sourceFiles = GetDirectoryContents(source);
            List<FileInfo> updatedFiles = sourceFiles.Select(sourceFile => new { sourceFile, targetFile = new FileInfo(sourceFile.FullName.Replace(source.FullName, target.FullName)) })
                                                     .Where(x => x.targetFile.Exists && (x.targetFile.Length != x.sourceFile.Length || x.targetFile.LastWriteTime < x.sourceFile.LastWriteTime))
                                                     .Select(x => x.sourceFile)
                                                     .ToList();
            await CopyFiles(source, target, updatedFiles);
        }

        internal async Task DeleteFiles(string sourcePath, string targetPath) {
            DirectoryInfo source = new DirectoryInfo(sourcePath);
            DirectoryInfo target = new DirectoryInfo(targetPath);
            IEnumerable<FileInfo> targetFiles = GetDirectoryContents(target);
            List<FileInfo> deletedFiles = targetFiles.Select(targetFile => new { targetFile, sourceFile = new FileInfo(targetFile.FullName.Replace(target.FullName, source.FullName)) })
                                                     .Where(x => !x.sourceFile.Exists)
                                                     .Select(x => x.targetFile)
                                                     .ToList();
            await DeleteFiles(deletedFiles);
            await DeleteEmptyDirectories(target);
        }

        internal async Task CopyDirectory(string sourceDirectory, string targetDirectory) {
            DirectoryInfo source = new DirectoryInfo(sourceDirectory);
            DirectoryInfo target = new DirectoryInfo(targetDirectory);
            List<FileInfo> files = GetDirectoryContents(source);
            await CopyFiles(source, target, files);
        }

        internal async Task CopyFiles(FileSystemInfo source, FileSystemInfo target, List<FileInfo> files, bool flatten = false) {
            Directory.CreateDirectory(target.FullName);
            if (files.Count == 0) {
                await Logger.Log("No files to copy");
                return;
            }

            double totalSize = files.Select(x => x.Length).Sum();
            await Logger.Log($"{totalSize.Bytes().ToString("#.#")} of files to copy");
            if (files.Count > FILE_COPY_TASK_OUNT_THRESHOLD || totalSize > FILE_COPY_TASK_SIZE_THRESHOLD) {
                await BatchCopyFiles(source, target, files, totalSize, flatten);
            } else {
                await SimpleCopyFiles(source, target, files, flatten);
            }
        }

        internal async Task DeleteDirectoryContents(string path, string searchPattern = "*") {
            DirectoryInfo directory = new DirectoryInfo(path);
            foreach (DirectoryInfo subDirectory in directory.GetDirectories("*", SearchOption.TopDirectoryOnly)) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                await Logger.Log($"Deleting: {subDirectory}");
                subDirectory.Delete(true);
            }

            await DeleteFiles(directory.GetFiles("*", SearchOption.AllDirectories));
        }

        internal async Task DeleteFiles(IEnumerable<FileInfo> files) {
            foreach (FileInfo file in files) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                await Logger.Log($"Deleting: {file}");
                file.Delete();
            }
        }

        internal async Task DeleteEmptyDirectories(DirectoryInfo directory) {
            foreach (DirectoryInfo subDirectory in directory.GetDirectories()) {
                await DeleteEmptyDirectories(subDirectory);
                if (subDirectory.GetFiles().Length == 0 && subDirectory.GetDirectories().Length == 0) {
                    await Logger.Log($"Deleting: {subDirectory}");
                    subDirectory.Delete(false);
                }
            }
        }

        private async Task SimpleCopyFiles(FileSystemInfo source, FileSystemInfo target, IEnumerable<FileInfo> files, bool flatten = false) {
            foreach (FileInfo file in files) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                string targetFile = flatten ? Path.Join(target.FullName, file.Name) : file.FullName.Replace(source.FullName, target.FullName);
                await Logger.Log($"Copying file: {file}");
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                file.CopyTo(targetFile, true);
            }
        }

        private async Task BatchCopyFiles(FileSystemInfo source, FileSystemInfo target, IEnumerable<FileInfo> files, double totalSize, bool flatten = false) {
            double copiedSize = 0;
            await Logger.Log($"Copied {copiedSize.Bytes().ToString("#.#")} of {totalSize.Bytes().ToString("#.#")}");
            IEnumerable<IEnumerable<FileInfo>> fileBatches = files.Batch(10);
            foreach (IEnumerable<FileInfo> fileBatch in fileBatches) {
                List<FileInfo> fileList = fileBatch.ToList();
                IEnumerable<Task> tasks = fileList.Select(
                    file => {
                        try {
                            CancellationTokenSource.Token.ThrowIfCancellationRequested();
                            string targetFile = flatten ? Path.Join(target.FullName, file.Name) : file.FullName.Replace(source.FullName, target.FullName);
                            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                            file.CopyTo(targetFile, true);
                        } catch (OperationCanceledException) {
                            throw;
                        } catch (Exception exception) {
                            throw new Exception($"Failed copying file '{file}'\n{exception.Message}", exception);
                        }

                        return Task.CompletedTask;
                    }
                );
                await Task.WhenAll(tasks);
                copiedSize += fileList.Select(x => x.Length).Sum();
                await Logger.LogInline($"Copied {copiedSize.Bytes().ToString("#.#")} of {totalSize.Bytes().ToString("#.#")}");
            }
        }
    }
}
