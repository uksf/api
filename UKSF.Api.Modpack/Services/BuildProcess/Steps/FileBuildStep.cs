using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using MoreLinq;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps {
    public class FileBuildStep : BuildStep {
        private const double FILE_COPY_TASK_SIZE_THRESHOLD = 5_000_000_000;
        private const double FILE_COPY_TASK_COUNT_THRESHOLD = 50;
        private const double FILE_DELETE_TASK_COUNT_THRESHOLD = 50;

        internal static List<FileInfo> GetDirectoryContents(DirectoryInfo source, string searchPattern = "*") => source.GetFiles(searchPattern, SearchOption.AllDirectories).ToList();

        internal async Task AddFiles(string sourcePath, string targetPath) {
            DirectoryInfo source = new(sourcePath);
            DirectoryInfo target = new(targetPath);
            IEnumerable<FileInfo> sourceFiles = GetDirectoryContents(source);
            List<FileInfo> addedFiles = sourceFiles.Select(sourceFile => new { sourceFile, targetFile = new FileInfo(sourceFile.FullName.Replace(source.FullName, target.FullName)) })
                                                   .Where(x => !x.targetFile.Exists)
                                                   .Select(x => x.sourceFile)
                                                   .ToList();
            await CopyFiles(source, target, addedFiles);
        }

        internal async Task UpdateFiles(string sourcePath, string targetPath) {
            DirectoryInfo source = new(sourcePath);
            DirectoryInfo target = new(targetPath);
            IEnumerable<FileInfo> sourceFiles = GetDirectoryContents(source);
            List<FileInfo> updatedFiles = sourceFiles.Select(sourceFile => new { sourceFile, targetFile = new FileInfo(sourceFile.FullName.Replace(source.FullName, target.FullName)) })
                                                     .Where(x => x.targetFile.Exists && (x.targetFile.Length != x.sourceFile.Length || x.targetFile.LastWriteTime < x.sourceFile.LastWriteTime))
                                                     .Select(x => x.sourceFile)
                                                     .ToList();
            await CopyFiles(source, target, updatedFiles);
        }

        internal async Task DeleteFiles(string sourcePath, string targetPath, bool matchSubdirectories = false) {
            DirectoryInfo source = new(sourcePath);
            DirectoryInfo target = new(targetPath);
            IEnumerable<FileInfo> targetFiles = GetDirectoryContents(target);
            List<FileInfo> deletedFiles = targetFiles.Select(targetFile => new { targetFile, sourceFile = new FileInfo(targetFile.FullName.Replace(target.FullName, source.FullName)) })
                                                     .Where(
                                                         x => {
                                                             if (x.sourceFile.Exists) return false;
                                                             if (!matchSubdirectories) return true;

                                                             string sourceSubdirectoryPath = x.sourceFile.FullName.Replace(sourcePath, "")
                                                                                              .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                                                                                              .First();
                                                             DirectoryInfo sourceSubdirectory = new(Path.Join(sourcePath, sourceSubdirectoryPath));
                                                             return sourceSubdirectory.Exists;
                                                         }
                                                     )
                                                     .Select(x => x.targetFile)
                                                     .ToList();
            await DeleteFiles(deletedFiles);
            await DeleteEmptyDirectories(target);
        }

        internal async Task CopyDirectory(string sourceDirectory, string targetDirectory) {
            DirectoryInfo source = new(sourceDirectory);
            DirectoryInfo target = new(targetDirectory);
            List<FileInfo> files = GetDirectoryContents(source);
            await CopyFiles(source, target, files);
        }

        internal async Task CopyFiles(FileSystemInfo source, FileSystemInfo target, List<FileInfo> files, bool flatten = false) {
            Directory.CreateDirectory(target.FullName);
            if (files.Count == 0) {
                StepLogger.Log("No files to copy");
                return;
            }

            long totalSize = files.Select(x => x.Length).Sum();
            if (files.Count > FILE_COPY_TASK_COUNT_THRESHOLD || totalSize > FILE_COPY_TASK_SIZE_THRESHOLD) {
                await ParallelCopyFiles(source, target, files, totalSize, flatten);
            } else {
                SimpleCopyFiles(source, target, files, flatten);
            }
        }

        internal async Task DeleteDirectoryContents(string path) {
            DirectoryInfo directory = new(path);
            if (!directory.Exists) {
                StepLogger.Log("Directory does not exist");
                return;
            }

            DeleteDirectories(directory.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList());
            await DeleteFiles(directory.GetFiles("*", SearchOption.AllDirectories).ToList());
        }

        internal void DeleteDirectories(List<DirectoryInfo> directories) {
            if (directories.Count == 0) {
                StepLogger.Log("No directories to delete");
                return;
            }

            foreach (DirectoryInfo directory in directories) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                StepLogger.Log($"Deleting directory: {directory}");
                directory.Delete(true);
            }
        }

        internal async Task DeleteFiles(List<FileInfo> files) {
            if (files.Count == 0) {
                StepLogger.Log("No files to delete");
                return;
            }

            if (files.Count > FILE_DELETE_TASK_COUNT_THRESHOLD) {
                await ParallelDeleteFiles(files);
            } else {
                SimpleDeleteFiles(files);
            }
        }

        internal async Task DeleteEmptyDirectories(DirectoryInfo directory) {
            foreach (DirectoryInfo subDirectory in directory.GetDirectories()) {
                await DeleteEmptyDirectories(subDirectory);
                if (subDirectory.GetFiles().Length == 0 && subDirectory.GetDirectories().Length == 0) {
                    StepLogger.Log($"Deleting directory: {subDirectory}");
                    subDirectory.Delete(false);
                }
            }
        }

        internal async Task ParallelProcessFiles(IEnumerable<FileInfo> files, int taskLimit, Func<FileInfo, Task> process, Func<string> getLog, string error) {
            SemaphoreSlim taskLimiter = new(taskLimit);
            IEnumerable<Task> tasks = files.Select(
                file => {
                    return Task.Run(
                        async () => {
                            CancellationTokenSource.Token.ThrowIfCancellationRequested();

                            try {
                                await taskLimiter.WaitAsync(CancellationTokenSource.Token);
                                CancellationTokenSource.Token.ThrowIfCancellationRequested();

                                await process(file);
                                StepLogger.LogInline(getLog());
                            } catch (OperationCanceledException) {
                                throw;
                            } catch (Exception exception) {
                                throw new Exception($"{error} '{file}'\n{exception.Message}{(exception.InnerException != null ? $"\n{exception.InnerException.Message}" : "")}", exception);
                            } finally {
                                taskLimiter.Release();
                            }
                        },
                        CancellationTokenSource.Token
                    );
                }
            );

            StepLogger.Log(getLog());
            await Task.WhenAll(tasks);
        }

        internal async Task BatchProcessFiles(IEnumerable<FileInfo> files, int batchSize, Func<FileInfo, Task> process, Func<string> getLog, string error) {
            StepLogger.Log(getLog());
            IEnumerable<IEnumerable<FileInfo>> fileBatches = files.Batch(batchSize);
            foreach (IEnumerable<FileInfo> fileBatch in fileBatches) {
                List<FileInfo> fileList = fileBatch.ToList();
                IEnumerable<Task> tasks = fileList.Select(
                    async file => {
                        try {
                            CancellationTokenSource.Token.ThrowIfCancellationRequested();
                            await process(file);
                        } catch (OperationCanceledException) {
                            throw;
                        } catch (Exception exception) {
                            throw new Exception($"{error} '{file}'\n{exception.Message}{(exception.InnerException != null ? $"\n{exception.InnerException.Message}" : "")}", exception);
                        }
                    }
                );
                await Task.WhenAll(tasks);
                StepLogger.LogInline(getLog());
            }
        }

        private void SimpleCopyFiles(FileSystemInfo source, FileSystemInfo target, IEnumerable<FileInfo> files, bool flatten = false) {
            foreach (FileInfo file in files) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                string targetFile = flatten ? Path.Join(target.FullName, file.Name) : file.FullName.Replace(source.FullName, target.FullName);
                StepLogger.Log($"Copying '{file}' to '{target.FullName}'");
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                file.CopyTo(targetFile, true);
            }
        }

        private async Task ParallelCopyFiles(FileSystemInfo source, FileSystemInfo target, IEnumerable<FileInfo> files, long totalSize, bool flatten = false) {
            long copiedSize = 0;
            string totalSizeString = totalSize.Bytes().ToString("#.#");
            await BatchProcessFiles(
                files,
                10,
                file => {
                    string targetFile = flatten ? Path.Join(target.FullName, file.Name) : file.FullName.Replace(source.FullName, target.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                    file.CopyTo(targetFile, true);
                    Interlocked.Add(ref copiedSize, file.Length);
                    return Task.CompletedTask;
                },
                () => $"Copied {copiedSize.Bytes().ToString("#.#")} of {totalSizeString}",
                "Failed to copy file"
            );
        }

        private void SimpleDeleteFiles(IEnumerable<FileInfo> files) {
            foreach (FileInfo file in files) {
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                StepLogger.Log($"Deleting file: {file}");
                file.Delete();
            }
        }

        private async Task ParallelDeleteFiles(IReadOnlyCollection<FileInfo> files) {
            int deleted = 0;
            int total = files.Count;
            await BatchProcessFiles(
                files,
                10,
                file => {
                    file.Delete();
                    Interlocked.Increment(ref deleted);
                    return Task.CompletedTask;
                },
                () => $"Deleted {deleted} of {total} files",
                "Failed to delete file"
            );
        }
    }
}
