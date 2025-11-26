using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CursorBackup.Models;

namespace CursorBackup.Services
{
    /// <summary>
    /// Service for backing up and restoring Cursor settings
    /// </summary>
    public class BackupService
    {
        /// <summary>
        /// Backs up selected settings to the specified destination
        /// </summary>
        public BackupResult BackupSettings(List<CursorSettingItem> selectedSettings, string destinationPath)
        {
            var result = new BackupResult();
            
            if (!Directory.Exists(destinationPath))
            {
                try
                {
                    Directory.CreateDirectory(destinationPath);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BackupError
                    {
                        SettingName = "Destination Directory",
                        ErrorMessage = $"Cannot create destination directory: {ex.Message}"
                    });
                    return result;
                }
            }

            foreach (var setting in selectedSettings.Where(s => s.IsSelected && s.IsAvailable))
            {
                try
                {
                    // Special handling for Extensions List - generate a text file instead of copying directory
                    if (setting.Type == SettingType.ExtensionsList && Directory.Exists(setting.SourcePath))
                    {
                        var destPath = Path.Combine(destinationPath, setting.DestinationPath);
                        var destDir = Path.GetDirectoryName(destPath);
                        
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        // Generate extensions list file
                        var extensions = Directory.GetDirectories(setting.SourcePath)
                            .Select(dir => Path.GetFileName(dir))
                            .OrderBy(name => name)
                            .ToList();

                        var listContent = string.Join(Environment.NewLine, extensions);
                        File.WriteAllText(destPath, listContent);
                        
                        result.SuccessCount++;
                        result.SuccessfulItems.Add(setting.Name);
                        continue;
                    }

                    var destPathNormal = Path.Combine(destinationPath, setting.DestinationPath);
                    var destDirNormal = Path.GetDirectoryName(destPathNormal);
                    
                    if (!string.IsNullOrEmpty(destDirNormal) && !Directory.Exists(destDirNormal))
                    {
                        Directory.CreateDirectory(destDirNormal);
                    }

                    if (File.Exists(setting.SourcePath))
                    {
                        File.Copy(setting.SourcePath, destPathNormal, overwrite: true);
                        result.SuccessCount++;
                        result.SuccessfulItems.Add(setting.Name);
                    }
                    else if (Directory.Exists(setting.SourcePath))
                    {
                        // For directories, ensure destination is also a directory
                        if (File.Exists(destPathNormal))
                        {
                            // If destination path exists as a file, use it as directory name
                            destPathNormal = Path.Combine(Path.GetDirectoryName(destPathNormal) ?? destinationPath, Path.GetFileNameWithoutExtension(destPathNormal));
                        }
                        
                        CopyDirectory(setting.SourcePath, destPathNormal, overwrite: true);
                        result.SuccessCount++;
                        result.SuccessfulItems.Add(setting.Name);
                    }
                    else
                    {
                        result.Warnings.Add($"Source not found: {setting.SourcePath}");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BackupError
                    {
                        SettingName = setting.Name,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Merges settings from source to destination without overwriting existing items
        /// </summary>
        public MergeResult MergeSettings(List<CursorSettingItem> sourceSettings, string destinationPath)
        {
            var result = new MergeResult();

            if (!Directory.Exists(destinationPath))
            {
                try
                {
                    Directory.CreateDirectory(destinationPath);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BackupError
                    {
                        SettingName = "Destination Directory",
                        ErrorMessage = $"Cannot create destination directory: {ex.Message}"
                    });
                    return result;
                }
            }

            foreach (var setting in sourceSettings.Where(s => s.IsSelected && s.IsAvailable))
            {
                try
                {
                    // Special handling for Extensions List - generate a text file instead of copying directory
                    if (setting.Type == SettingType.ExtensionsList && Directory.Exists(setting.SourcePath))
                    {
                        var destPath = Path.Combine(destinationPath, setting.DestinationPath);
                        var destDir = Path.GetDirectoryName(destPath);

                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        // Check for conflicts
                        if (File.Exists(destPath))
                        {
                            result.Conflicts.Add(new ConflictInfo
                            {
                                SettingName = setting.Name,
                                DestinationPath = destPath,
                                SourcePath = setting.SourcePath,
                                ConflictType = ConflictType.FileExists
                            });
                            continue; // Skip conflicting items
                        }

                        // Generate extensions list file
                        var extensions = Directory.GetDirectories(setting.SourcePath)
                            .Select(dir => Path.GetFileName(dir))
                            .OrderBy(name => name)
                            .ToList();

                        var listContent = string.Join(Environment.NewLine, extensions);
                        File.WriteAllText(destPath, listContent);
                        
                        result.MergedCount++;
                        result.MergedItems.Add(setting.Name);
                        continue;
                    }

                    var destPathNormal = Path.Combine(destinationPath, setting.DestinationPath);
                    var destDirNormal = Path.GetDirectoryName(destPathNormal);

                    if (!string.IsNullOrEmpty(destDirNormal) && !Directory.Exists(destDirNormal))
                    {
                        Directory.CreateDirectory(destDirNormal);
                    }

                    // Check for conflicts
                    bool exists = File.Exists(destPathNormal) || Directory.Exists(destPathNormal);
                    
                    if (exists)
                    {
                        result.Conflicts.Add(new ConflictInfo
                        {
                            SettingName = setting.Name,
                            DestinationPath = destPathNormal,
                            SourcePath = setting.SourcePath,
                            ConflictType = File.Exists(destPathNormal) ? ConflictType.FileExists : ConflictType.DirectoryExists
                        });
                        continue; // Skip conflicting items
                    }

                    if (File.Exists(setting.SourcePath))
                    {
                        File.Copy(setting.SourcePath, destPathNormal, overwrite: false);
                        result.MergedCount++;
                        result.MergedItems.Add(setting.Name);
                    }
                    else if (Directory.Exists(setting.SourcePath))
                    {
                        // For directories, ensure destination is also a directory
                        if (File.Exists(destPathNormal))
                        {
                            // If destination path exists as a file, use it as directory name
                            destPathNormal = Path.Combine(Path.GetDirectoryName(destPathNormal) ?? destinationPath, Path.GetFileNameWithoutExtension(destPathNormal));
                        }
                        
                        CopyDirectory(setting.SourcePath, destPathNormal, overwrite: false);
                        result.MergedCount++;
                        result.MergedItems.Add(setting.Name);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BackupError
                    {
                        SettingName = setting.Name,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return result;
        }

        private void CopyDirectory(string sourceDir, string destDir, bool overwrite)
        {
            if (!Directory.Exists(sourceDir))
                return;

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                if (overwrite || !File.Exists(destFile))
                {
                    File.Copy(file, destFile, overwrite);
                }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir, overwrite);
            }
        }
    }

    public class BackupResult
    {
        public int SuccessCount { get; set; }
        public List<string> SuccessfulItems { get; set; } = new();
        public List<BackupError> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class BackupError
    {
        public string SettingName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class MergeResult
    {
        public int MergedCount { get; set; }
        public int SuccessCount => MergedCount;
        public List<string> MergedItems { get; set; } = new();
        public List<ConflictInfo> Conflicts { get; set; } = new();
        public List<BackupError> Errors { get; set; } = new();
    }

    public class ConflictInfo
    {
        public string SettingName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public ConflictType ConflictType { get; set; }
    }

    public enum ConflictType
    {
        FileExists,
        DirectoryExists
    }
}

