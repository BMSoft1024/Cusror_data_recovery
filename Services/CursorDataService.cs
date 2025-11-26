using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Data.SQLite;
using CursorBackup.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CursorBackup.Services;

namespace CursorBackup.Services
{
    /// <summary>
    /// Service for discovering and managing Cursor settings and data
    /// </summary>
    public class CursorDataService
    {
        private readonly string _cursorAppDataPath;
        private readonly string _cursorUserDataPath;

        public CursorDataService()
        {
            _cursorAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cursor");
            _cursorUserDataPath = Path.Combine(_cursorAppDataPath, "User");
        }

        /// <summary>
        /// Discovers all available Cursor settings from the current installation
        /// </summary>
        public List<CursorSettingItem> DiscoverCurrentSettings()
        {
            var settings = new List<CursorSettingItem>();

            // Global Settings
            var settingsPath = Path.Combine(_cursorUserDataPath, "settings.json");
            settings.Add(new CursorSettingItem
            {
                Id = "settings.json",
                Name = "Global Settings",
                Description = "Cursor/VSCode user settings",
                Category = "Configuration",
                SourcePath = settingsPath,
                DestinationPath = "User/settings.json",
                IsAvailable = File.Exists(settingsPath),
                Type = SettingType.GlobalSettings
            });

            // Keybindings
            var keybindingsPath = Path.Combine(_cursorUserDataPath, "keybindings.json");
            settings.Add(new CursorSettingItem
            {
                Id = "keybindings.json",
                Name = "Keybindings",
                Description = "Keyboard shortcuts configuration",
                Category = "Configuration",
                SourcePath = keybindingsPath,
                DestinationPath = "User/keybindings.json",
                IsAvailable = File.Exists(keybindingsPath),
                Type = SettingType.Keybindings
            });

            // State Database
            var stateDbPath = Path.Combine(_cursorAppDataPath, "state.vscdb");
            settings.Add(new CursorSettingItem
            {
                Id = "state.vscdb",
                Name = "State Database",
                Description = "Cursor application state database",
                Category = "Data",
                SourcePath = stateDbPath,
                DestinationPath = "state.vscdb",
                IsAvailable = File.Exists(stateDbPath),
                Type = SettingType.StateDatabase
            });

            // State Database Backup
            var stateDbBackupPath = Path.Combine(_cursorAppDataPath, "state.vscdb.backup");
            settings.Add(new CursorSettingItem
            {
                Id = "state.vscdb.backup",
                Name = "State Database Backup",
                Description = "Backup of state database",
                Category = "Data",
                SourcePath = stateDbBackupPath,
                DestinationPath = "state.vscdb.backup",
                IsAvailable = File.Exists(stateDbBackupPath),
                Type = SettingType.StateDatabase
            });

            // Language Packs
            var languagePacksPath = Path.Combine(_cursorAppDataPath, "languagepacks.json");
            settings.Add(new CursorSettingItem
            {
                Id = "languagepacks.json",
                Name = "Language Packs",
                Description = "Installed language pack configuration",
                Category = "Configuration",
                SourcePath = languagePacksPath,
                DestinationPath = "languagepacks.json",
                IsAvailable = File.Exists(languagePacksPath),
                Type = SettingType.LanguagePacks
            });

            // Global Storage
            var globalStoragePath = Path.Combine(_cursorUserDataPath, "globalStorage");
            settings.Add(new CursorSettingItem
            {
                Id = "globalStorage",
                Name = "Global Storage",
                Description = "Extensions global storage data",
                Category = "Data",
                SourcePath = globalStoragePath,
                DestinationPath = "User/globalStorage",
                IsAvailable = Directory.Exists(globalStoragePath),
                Type = SettingType.GlobalStorage
            });

            // Workspace Storage (for project-specific settings)
            var workspaceStoragePath = Path.Combine(_cursorUserDataPath, "workspaceStorage");
            if (Directory.Exists(workspaceStoragePath))
            {
                settings.Add(new CursorSettingItem
                {
                    Id = "workspaceStorage",
                    Name = "Workspace Storage",
                    Description = "Project-specific workspace storage",
                    Category = "Data",
                    SourcePath = workspaceStoragePath,
                    DestinationPath = "User/workspaceStorage",
                    IsAvailable = true,
                    Type = SettingType.WorkspaceSettings
                });
            }

            // Extensions List
            var extensionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "extensions");
            if (Directory.Exists(extensionsPath))
            {
                settings.Add(new CursorSettingItem
                {
                    Id = "extensions_list",
                    Name = "Extensions List",
                    Description = "List of installed extensions",
                    Category = "Configuration",
                    SourcePath = extensionsPath,
                    DestinationPath = "extensions_list.txt",
                    IsAvailable = true,
                    Type = SettingType.ExtensionsList
                });
            }

            // Search for Cursor Documentation in ALL possible locations
            // NOTE: Individual documentation URLs from state.vscdb are now handled via DiscoverDocumentationGroups()
            // Only file/folder-based documentation locations are added here
            var docLocations = new List<(string Path, string Description)>();
            
            // 1. %USERPROFILE%\.cursor\docs
            var globalCursorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor");
            var globalDocsPath = Path.Combine(globalCursorPath, "docs");
            if (Directory.Exists(globalDocsPath))
            {
                docLocations.Add((globalDocsPath, "Global Cursor docs (%USERPROFILE%\\.cursor\\docs)"));
            }
            
            // 2. Check state.vscdb for docs metadata
            var docsStateDbPath = Path.Combine(_cursorAppDataPath, "state.vscdb");
            if (File.Exists(docsStateDbPath))
            {
                try
                {
                    // Try to query database for docs-related tables
                    using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={docsStateDbPath}"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND (name LIKE '%doc%' OR name LIKE '%index%' OR name LIKE '%embed%' OR name LIKE '%cursor%')";
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    var tables = new List<string>();
                                    while (reader.Read())
                                    {
                                        tables.Add(reader.GetString(0));
                                    }
                                    if (tables.Count > 0)
                                    {
                                        docLocations.Add((docsStateDbPath, $"State DB contains docs tables: {string.Join(", ", tables)}"));
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Database might be locked or in use - add it anyway as potential location
                    var dbInfo = new FileInfo(docsStateDbPath);
                    docLocations.Add((docsStateDbPath, $"State DB (locked/in use, {dbInfo.Length} bytes, modified: {dbInfo.LastWriteTime:yyyy-MM-dd})"));
                }
            }
            
            // 3. Check globalStorage for anysphere.cursor-retrieval (based on documentation research)
            var docsGlobalStoragePath = Path.Combine(_cursorUserDataPath, "globalStorage");
            if (Directory.Exists(docsGlobalStoragePath))
            {
                // Search for anysphere directories
                var anysphereDirs = Directory.GetDirectories(docsGlobalStoragePath, "*anysphere*", SearchOption.TopDirectoryOnly);
                foreach (var dir in anysphereDirs)
                {
                    // Check for doc/index files
                    var docFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                        .Where(f => 
                            f.ToLowerInvariant().Contains("doc") || 
                            f.ToLowerInvariant().Contains("index") ||
                            f.ToLowerInvariant().EndsWith(".md") ||
                            f.ToLowerInvariant().EndsWith(".txt") ||
                            f.ToLowerInvariant().EndsWith(".json"))
                        .ToList();
                    if (docFiles.Count > 0)
                    {
                        docLocations.Add((dir, $"GlobalStorage anysphere ({docFiles.Count} doc/index files)"));
                    }
                    
                    // Also check for index.json specifically
                    var indexJson = Path.Combine(dir, "index.json");
                    if (File.Exists(indexJson))
                    {
                        docLocations.Add((indexJson, "GlobalStorage index.json"));
                    }
                }
                
                // Also check for cursor-retrieval in globalStorage
                var cursorRetrievalDirs = Directory.GetDirectories(docsGlobalStoragePath, "*cursor-retrieval*", SearchOption.TopDirectoryOnly);
                foreach (var dir in cursorRetrievalDirs)
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        docLocations.Add((dir, $"GlobalStorage cursor-retrieval ({files.Length} files)"));
                    }
                }
            }
            
            // 4. Check workspaceStorage for cursor-retrieval indexing (project-specific docs)
            var docsWorkspaceStoragePath = Path.Combine(_cursorUserDataPath, "workspaceStorage");
            if (Directory.Exists(docsWorkspaceStoragePath))
            {
                var retrievalDirs = Directory.GetDirectories(docsWorkspaceStoragePath, "*cursor-retrieval*", SearchOption.AllDirectories);
                var totalFiles = 0;
                var projectCount = 0;
                var projectNames = new List<string>();
                
                foreach (var dir in retrievalDirs)
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                        .Where(f => 
                            f.ToLowerInvariant().EndsWith(".txt") || 
                            f.ToLowerInvariant().EndsWith(".md") ||
                            f.ToLowerInvariant().Contains("index") ||
                            f.ToLowerInvariant().EndsWith(".json"))
                        .ToList();
                    if (files.Count > 0)
                    {
                        totalFiles += files.Count;
                        projectCount++;
                        
                        // Try to get project name from parent workspace
                        var workspaceFolder = Directory.GetParent(dir)?.FullName;
                        if (!string.IsNullOrEmpty(workspaceFolder))
                        {
                            var workspaceJson = Path.Combine(workspaceFolder, "workspace.json");
                            if (File.Exists(workspaceJson))
                            {
                                try
                                {
                                    var wsJson = File.ReadAllText(workspaceJson);
                                    var ws = JsonConvert.DeserializeObject<dynamic>(wsJson);
                                    var folder = ws?.folder?.ToString();
                                    if (!string.IsNullOrEmpty(folder))
                                    {
                                        var normalized = NormalizePath(folder);
                                        var projName = Path.GetFileName(normalized) ?? Path.GetFileName(workspaceFolder);
                                        if (!projectNames.Contains(projName))
                                        {
                                            projectNames.Add(projName);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                if (totalFiles > 0)
                {
                    var projectsInfo = projectNames.Count > 0 
                        ? $" in projects: {string.Join(", ", projectNames.Take(5))}" 
                        : "";
                    docLocations.Add((docsWorkspaceStoragePath, $"WorkspaceStorage indexing ({totalFiles} files in {projectCount} projects{projectsInfo})"));
                }
            }
            
            // Add all found documentation locations
            if (docLocations.Count > 0)
            {
                foreach (var (path, desc) in docLocations)
                {
                    var isDir = Directory.Exists(path);
                    var fileCount = isDir ? Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length : 0;
                    var dirCount = isDir ? Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length : 0;
                    
                    settings.Add(new CursorSettingItem
                    {
                        Id = $"docs_{path.GetHashCode()}",
                        Name = $"Cursor Documentation: {desc}",
                        Description = $"{desc}\n" +
                                     $"Location: {path}\n" +
                                     (isDir ? $"Files: {fileCount}, Folders: {dirCount}" : $"File size: {new FileInfo(path).Length} bytes"),
                        Category = "Documentation",
                        SourcePath = path,
                        DestinationPath = path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%")
                                              .Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%"),
                        IsAvailable = true,
                        Type = SettingType.Other
                    });
                }
            }
            else
            {
                // No docs found - add warning
                settings.Add(new CursorSettingItem
                {
                    Id = "docs_not_found",
                    Name = "Cursor Documentation - NOT FOUND",
                    Description = "WARNING: Cursor documentation not found!\n\n" +
                                 "Searched locations:\n" +
                                 $"- %USERPROFILE%\\.cursor\\docs\n" +
                                 $"- %APPDATA%\\Cursor\\state.vscdb (database)\n" +
                                 $"- %APPDATA%\\Cursor\\User\\globalStorage\\*anysphere*\n" +
                                 $"- %APPDATA%\\Cursor\\User\\workspaceStorage\\*cursor-retrieval*\n\n" +
                                 "The documentation is likely stored in the cloud or has been deleted.",
                    Category = "Documentation",
                    SourcePath = "",
                    DestinationPath = "",
                    IsAvailable = false,
                    Type = SettingType.Other
                });
            }
            
            // Check for rules folder in global .cursor
            if (Directory.Exists(globalCursorPath))
            {
                var globalRulesPath = Path.Combine(globalCursorPath, "rules");
                if (Directory.Exists(globalRulesPath))
                {
                    var rulesFiles = Directory.GetFiles(globalRulesPath, "*", SearchOption.AllDirectories);
                    foreach (var rulesFile in rulesFiles)
                    {
                        var relativePath = Path.GetRelativePath(globalCursorPath, rulesFile);
                        settings.Add(new CursorSettingItem
                        {
                            Id = $"global_rules_{relativePath.GetHashCode()}",
                            Name = $"Global Rules: {Path.GetFileName(rulesFile)}",
                            Description = $"Global Cursor rules file - %USERPROFILE%\\.cursor\\rules ({relativePath})",
                            Category = "Configuration",
                            SourcePath = rulesFile,
                            DestinationPath = $".cursor/{relativePath.Replace('\\', '/')}",
                            IsAvailable = true,
                            Type = SettingType.Rules
                        });
                    }
                }
            }

            return settings;
        }

        /// <summary>
        /// Normalizes a path for comparison (handles URI encoding, separators, case)
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            try
            {
                // 1. URI decoding
                var decoded = Uri.UnescapeDataString(path);
                
                // 2. file:/// prefix handling
                if (decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                {
                    decoded = decoded.Substring(8);
                }
                else if (decoded.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    decoded = decoded.Substring(7);
                }
                
                // 3. URI parsing - try to handle as URI first
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
                    var localPath = uri.LocalPath;
                    
                    // Unix-style Windows path handling (/g:/... -> G:\...)
                    if (localPath.StartsWith("/") && localPath.Length > 1 && localPath[1] == ':')
                    {
                        localPath = localPath.Substring(1).Replace('/', '\\');
                    }
                    decoded = localPath;
                }
                else
                {
                    // If URI parsing failed, try directly
                    // Unix-style Windows path handling (/g:/... -> G:\...)
                    if (decoded.StartsWith("/") && decoded.Length > 1 && decoded[1] == ':')
                    {
                        decoded = decoded.Substring(1).Replace('/', '\\');
                    }
                }
                
                // 4. Separator normalization
                decoded = decoded.Replace('/', '\\');
                
                // 5. Full path (absolute path) - only if valid Windows path
                if (Path.IsPathRooted(decoded))
                {
                    try
                    {
                        // Try to convert to full path
                        // If failed (e.g., WSL path), keep original
                        if (!decoded.StartsWith("\\\\") && !decoded.StartsWith("\\wsl"))
                        {
                            decoded = Path.GetFullPath(decoded);
                        }
                    }
                    catch
                    {
                        // If failed (e.g., WSL path or remote path), keep original
                    }
                }
                
                // 6. Remove trailing separator
                return decoded.TrimEnd('\\', '/');
            }
            catch
            {
                // If any error, at least try to normalize separator
                return path.Replace('/', '\\').TrimEnd('\\', '/');
            }
        }

        /// <summary>
        /// Checks if two paths match (case-insensitive, normalized)
        /// </summary>
        private bool PathsMatch(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                return false;

            var normalized1 = NormalizePath(path1);
            var normalized2 = NormalizePath(path2);

            // Exact match
            if (normalized1.Equals(normalized2, StringComparison.OrdinalIgnoreCase))
                return true;

            // Contains match (one path contains the other)
            if (normalized1.Contains(normalized2, StringComparison.OrdinalIgnoreCase) ||
                normalized2.Contains(normalized1, StringComparison.OrdinalIgnoreCase))
            {
                // Additional check: ensure it's not just a partial match
                var parts1 = normalized1.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                var parts2 = normalized2.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                
                // If one is a subset of the other, it's a match
                if (parts1.Length >= parts2.Length)
                {
                    var match = true;
                    for (int i = 0; i < parts2.Length; i++)
                    {
                        if (!parts1[i].Equals(parts2[i], StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
                else
                {
                    var match = true;
                    for (int i = 0; i < parts1.Length; i++)
                    {
                        if (!parts2[i].Equals(parts1[i], StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates MD5 hash of content for reliable duplicate detection
        /// </summary>
        private string CalculateContentHash(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return "empty";
            
            try
            {
                using (var md5 = MD5.Create())
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    var hash = md5.ComputeHash(bytes);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                // Fallback to simple hash if MD5 fails
                return content.GetHashCode().ToString();
            }
        }
        
        /// <summary>
        /// Check if session content is real chat data, not just UI state (same as Python is_real_chat_session)
        /// </summary>
        private bool IsRealChatSession(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return false;
            
            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                if (json == null)
                    return false;
                
                var keys = json.Keys.ToList();
                
                // UI state keys: collapsed, isHidden, size, numberOfVisibleViews
                // Real chat keys: message, content, role, requests, sessionId, messages
                var hasChatKeys = keys.Any(k => 
                    k.Equals("message", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("content", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("role", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("requests", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("sessionId", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("messages", StringComparison.OrdinalIgnoreCase));
                
                var hasOnlyUIKeys = keys.All(k => 
                    k.Equals("collapsed", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("isHidden", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("size", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("numberOfVisibleViews", StringComparison.OrdinalIgnoreCase));
                
                return hasChatKeys && !hasOnlyUIKeys;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Groups prompts by bubble sessions - matches prompts with bubbles and groups them
        /// </summary>
        private List<PromptSession> GroupPromptsByBubbleSessions(List<dynamic> prompts, string workspaceStateDb)
        {
            Logger.LogInfo($"[CursorDataService] GroupPromptsByBubbleSessions: Starting with {prompts.Count} prompts");
            var sessions = new List<PromptSession>();
            
            // Get bubbles from global storage
            var globalStateDb = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cursor", "User", "globalStorage", "state.vscdb");
            
            if (!File.Exists(globalStateDb))
            {
                Logger.LogWarning($"[CursorDataService] GroupPromptsByBubbleSessions: Global state DB not found at {globalStateDb}");
                return sessions;
            }
            
            Logger.LogInfo($"[CursorDataService] GroupPromptsByBubbleSessions: Using global state DB: {globalStateDb}");
            
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={globalStateDb};Mode=ReadOnly"))
                {
                    connection.Open();
                    
                    // Check if cursorDiskKV exists
                    using (var checkTable = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='cursorDiskKV'", connection))
                    {
                        using (var reader = checkTable.ExecuteReader())
                        {
                            if (!reader.Read())
                                return sessions; // No bubbles table
                        }
                    }
                    
                    // Get all bubbles grouped by session
                    var bubbleSessions = new Dictionary<string, List<(JObject bubble, long? createdAt, int type)>>();
                    
                    using (var command = new SQLiteCommand("SELECT key, value FROM cursorDiskKV WHERE key LIKE 'bubbleId:%'", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    var key = reader.GetString(0);
                                    var valueJson = reader.GetString(1);
                                    var bubble = JObject.Parse(valueJson);
                                    
                                    // Extract session ID
                                    var keyParts = key.Split(':');
                                    if (keyParts.Length < 2)
                                        continue;
                                    
                                    var sessionId = keyParts[1];
                                    
                                    if (!bubbleSessions.ContainsKey(sessionId))
                                        bubbleSessions[sessionId] = new List<(JObject, long?, int)>();
                                    
                                    var bubbleType = bubble["type"]?.Value<int>() ?? 0;
                                    var text = bubble["text"]?.ToString()?.Trim();
                                    
                                    if (string.IsNullOrEmpty(text))
                                        continue;
                                    
                                    long? createdAt = null;
                                    if (bubble["createdAt"] != null)
                                    {
                                        try
                                        {
                                            var createdAtStr = bubble["createdAt"].ToString();
                                            if (DateTime.TryParse(createdAtStr, out var dt))
                                            {
                                                createdAt = ((DateTimeOffset)dt).ToUnixTimeMilliseconds();
                                            }
                                        }
                                        catch { }
                                    }
                                    
                                    bubbleSessions[sessionId].Add((bubble, createdAt, bubbleType));
                                }
                                catch { }
                            }
                        }
                    }
                    
                    // Sort bubbles by timestamp
                    foreach (var sessionId in bubbleSessions.Keys.ToList())
                    {
                        bubbleSessions[sessionId] = bubbleSessions[sessionId]
                            .OrderBy(b => b.createdAt ?? 0)
                            .ToList();
                    }
                    
                    Logger.LogInfo($"[CursorDataService] GroupPromptsByBubbleSessions: Found {bubbleSessions.Count} bubble sessions");
                    
                    // Match prompts with bubbles
                    var matchedPromptIndices = new HashSet<int>();
                    
                    foreach (var sessionId in bubbleSessions.Keys)
                    {
                        var session = new PromptSession { SessionId = sessionId, HasBubbles = true };
                        int userBubbleCount = 0;
                        int assistantBubbleCount = 0;
                        
                        // First, count ASSISTANT bubbles to determine if session has responses
                        foreach (var bubble in bubbleSessions[sessionId])
                        {
                            if (bubble.type != 1) // ASSISTANT bubble
                            {
                                assistantBubbleCount++;
                            }
                        }
                        
                        // Only mark as having responses if there are ASSISTANT bubbles
                        session.HasResponses = assistantBubbleCount > 0;
                        
                        foreach (var bubble in bubbleSessions[sessionId])
                        {
                            if (bubble.type == 1) // USER bubble
                            {
                                userBubbleCount++;
                                var bubbleText = bubble.bubble["text"]?.ToString()?.Trim() ?? "";
                                
                                // Find matching prompt
                                for (int i = 0; i < prompts.Count; i++)
                                {
                                    if (matchedPromptIndices.Contains(i))
                                        continue;
                                    
                                    var promptObj = JObject.FromObject(prompts[i]);
                                    var promptText = promptObj["text"]?.ToString()?.Trim() ?? "";
                                    
                                    if (string.IsNullOrEmpty(promptText))
                                        continue;
                                    
                                    // Check if they match - use more flexible matching
                                    // Normalize whitespace and compare first 200 characters
                                    var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                    var bubbleNormalized = bubbleText.Replace("\r", "").Replace("\n", " ").Trim();
                                    
                                    var promptStart = promptNormalized.Length > 200 ? promptNormalized.Substring(0, 200) : promptNormalized;
                                    var bubbleStart = bubbleNormalized.Length > 200 ? bubbleNormalized.Substring(0, 200) : bubbleNormalized;
                                    
                                    // Try exact match first
                                    if (promptStart.Equals(bubbleStart, StringComparison.OrdinalIgnoreCase))
                                    {
                                        session.Prompts.Add(prompts[i]);
                                        matchedPromptIndices.Add(i);
                                        Logger.LogDebug($"[CursorDataService] GroupPromptsByBubbleSessions: Matched prompt #{i} (exact) with session {sessionId.Substring(0, Math.Min(8, sessionId.Length))}...");
                                        break;
                                    }
                                    
                                    // Try contains match
                                    if (promptStart.Length > 50 && bubbleStart.Length > 50 &&
                                        (promptStart.Contains(bubbleStart, StringComparison.OrdinalIgnoreCase) ||
                                         bubbleStart.Contains(promptStart, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        session.Prompts.Add(prompts[i]);
                                        matchedPromptIndices.Add(i);
                                        Logger.LogDebug($"[CursorDataService] GroupPromptsByBubbleSessions: Matched prompt #{i} (contains) with session {sessionId.Substring(0, Math.Min(8, sessionId.Length))}...");
                                        break;
                                    }
                                    
                                    // Try word-based similarity (first 10 words) - simplified to avoid dynamic binding issues
                                    var promptWordsArray = promptStart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                    var bubbleWordsArray = bubbleStart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                    
                                    if (promptWordsArray.Length >= 3 && bubbleWordsArray.Length >= 3)
                                    {
                                        // Take first 10 words manually
                                        var promptWords = new List<string>();
                                        for (int w = 0; w < Math.Min(10, promptWordsArray.Length); w++)
                                        {
                                            promptWords.Add(promptWordsArray[w]);
                                        }
                                        
                                        var bubbleWords = new List<string>();
                                        for (int w = 0; w < Math.Min(10, bubbleWordsArray.Length); w++)
                                        {
                                            bubbleWords.Add(bubbleWordsArray[w]);
                                        }
                                        
                                        var matchingWords = promptWords.Intersect(bubbleWords, StringComparer.OrdinalIgnoreCase).Count();
                                        var totalWords = Math.Max(promptWords.Count, bubbleWords.Count);
                                        var similarity = totalWords > 0 ? (double)matchingWords / totalWords : 0.0;
                                        
                                        // If 60% of words match, consider it a match
                                        if (similarity >= 0.6)
                                        {
                                            session.Prompts.Add(prompts[i]);
                                            matchedPromptIndices.Add(i);
                                            Logger.LogDebug($"[CursorDataService] GroupPromptsByBubbleSessions: Matched prompt #{i} (similarity: {similarity:P0}) with session {sessionId.Substring(0, Math.Min(8, sessionId.Length))}...");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (session.Prompts.Count > 0)
                        {
                            sessions.Add(session);
                            Logger.LogInfo($"[CursorDataService] GroupPromptsByBubbleSessions: Session {sessionId.Substring(0, Math.Min(8, sessionId.Length))}... has {session.Prompts.Count} matched prompts (from {userBubbleCount} USER bubbles, {assistantBubbleCount} ASSISTANT bubbles, HasResponses={session.HasResponses})");
                        }
                    }
                    
                    // Add unmatched prompts as separate session
                    var unmatchedPrompts = new List<dynamic>();
                    for (int i = 0; i < prompts.Count; i++)
                    {
                        if (!matchedPromptIndices.Contains(i))
                            unmatchedPrompts.Add(prompts[i]);
                    }
                    
                    if (unmatchedPrompts.Count > 0)
                    {
                        // Try to find responses for unmatched prompts by searching all bubble sessions
                        // This helps find responses for old chats that weren't matched by text similarity
                        int unmatchedWithResponses = 0;
                        foreach (var prompt in unmatchedPrompts)
                        {
                            var promptObj = JObject.FromObject(prompt);
                            var promptText = promptObj["text"]?.ToString()?.Trim() ?? "";
                            
                            if (string.IsNullOrEmpty(promptText))
                                continue;
                            
                            // Try to find matching bubble in any session
                            foreach (var sessionId in bubbleSessions.Keys)
                            {
                                var bubbles = bubbleSessions[sessionId];
                                var sortedBubbles = bubbles.OrderBy(b => b.createdAt ?? 0).ToList();
                                
                                // Try to match prompt to USER bubble
                                for (int i = 0; i < sortedBubbles.Count; i++)
                                {
                                    var bubble = sortedBubbles[i];
                                    if (bubble.type == 1) // USER bubble
                                    {
                                        var bubbleText = bubble.bubble["text"]?.ToString()?.Trim() ?? "";
                                        
                                        // Check if they match (more flexible matching for old chats)
                                        var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                        var bubbleNormalized = bubbleText.Replace("\r", "").Replace("\n", " ").Trim();
                                        
                                        var promptStart = promptNormalized.Length > 100 ? promptNormalized.Substring(0, 100) : promptNormalized;
                                        var bubbleStart = bubbleNormalized.Length > 100 ? bubbleNormalized.Substring(0, 100) : bubbleNormalized;
                                        
                                        // More lenient matching for old chats - try multiple strategies
                                        bool isMatch = false;
                                        
                                        // Strategy 1: Exact or contains match
                                        if (promptStart.Equals(bubbleStart, StringComparison.OrdinalIgnoreCase) ||
                                            (promptStart.Length > 30 && bubbleStart.Length > 30 &&
                                             (promptStart.Contains(bubbleStart, StringComparison.OrdinalIgnoreCase) ||
                                              bubbleStart.Contains(promptStart, StringComparison.OrdinalIgnoreCase))))
                                        {
                                            isMatch = true;
                                        }
                                        
                                        // Strategy 2: Word-based similarity (for old chats with slight text differences)
                                        if (!isMatch && promptStart.Length > 20 && bubbleStart.Length > 20)
                                        {
                                            var promptWords = promptStart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                            var bubbleWords = bubbleStart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                            
                                            if (promptWords.Length >= 3 && bubbleWords.Length >= 3)
                                            {
                                                var matchingWords = promptWords.Intersect(bubbleWords, StringComparer.OrdinalIgnoreCase).Count();
                                                var totalWords = Math.Max(promptWords.Length, bubbleWords.Length);
                                                var similarity = totalWords > 0 ? (double)matchingWords / totalWords : 0.0;
                                                
                                                // Lower threshold for old chats (40% instead of 60%)
                                                if (similarity >= 0.4)
                                                {
                                                    isMatch = true;
                                                }
                                            }
                                        }
                                        
                                        if (isMatch)
                                        {
                                            // Check if there's an ASSISTANT response after this USER bubble
                                            for (int j = i + 1; j < sortedBubbles.Count; j++)
                                            {
                                                var nextBubble = sortedBubbles[j];
                                                if (nextBubble.type == 1) // Stop at next USER bubble
                                                    break;
                                                if (nextBubble.type != 1) // ASSISTANT bubble found
                                                {
                                                    unmatchedWithResponses++;
                                                    break;
                                                }
                                            }
                                            break; // Found match, move to next prompt
                                        }
                                    }
                                }
                            }
                        }
                        
                        var unmatchedSession = new PromptSession 
                        { 
                            SessionId = "unmatched", 
                            HasBubbles = unmatchedWithResponses > 0, // Mark as having bubbles if we found responses
                            HasResponses = unmatchedWithResponses > 0, // Mark as having responses if we found any
                            Prompts = unmatchedPrompts 
                        };
                        
                        sessions.Add(unmatchedSession);
                        Logger.LogInfo($"[CursorDataService] GroupPromptsByBubbleSessions: Created unmatched session with {unmatchedPrompts.Count} prompts ({unmatchedWithResponses} with potential responses)");
                    }
                    
                    Logger.LogInfo($"[CursorDataService] GroupPromptsByBubbleSessions: Created {sessions.Count} total sessions ({sessions.Count(s => s.HasBubbles)} with bubbles, {sessions.Count(s => !s.HasBubbles)} without)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[CursorDataService] GroupPromptsByBubbleSessions: Error grouping prompts by bubble sessions");
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Helper class for prompt sessions
        /// </summary>
        private class PromptSession
        {
            public string SessionId { get; set; } = "";
            public bool HasBubbles { get; set; }
            public bool HasResponses { get; set; }
            public List<dynamic> Prompts { get; set; } = new List<dynamic>();
        }
        
        /// <summary>
        /// Groups prompts into threads based on heuristics (new conversation indicators)
        /// </summary>
        private List<List<dynamic>> GroupPromptsIntoThreads(List<dynamic> prompts)
        {
            var threads = new List<List<dynamic>>();
            if (prompts == null || prompts.Count == 0)
                return threads;

            var currentThread = new List<dynamic>();
            
            // Keywords that indicate a new conversation/thread
            var newThreadIndicators = new[]
            {
                "create", "implement",
                "make", "add",
                "a new", "new project",
                "start", "begin"
            };

            for (int i = 0; i < prompts.Count; i++)
            {
                var prompt = prompts[i];
                if (prompt == null)
                    continue;

                string? text = null;
                try
                {
                    if (prompt is Newtonsoft.Json.Linq.JObject jobj)
                    {
                        text = jobj["text"]?.ToString();
                    }
                    else if (prompt is System.Dynamic.ExpandoObject expando)
                    {
                        var dict = (IDictionary<string, object>)expando;
                        text = dict.ContainsKey("text") ? dict["text"]?.ToString() : null;
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(text))
                    text = prompt.ToString();

                var textLower = text.ToLowerInvariant();
                var isNewThread = false;

                // First prompt always starts a thread
                if (i == 0)
                {
                    isNewThread = true;
                }
                // Check if text starts with new thread indicators
                else if (textLower.Length > 0)
                {
                    var first50Chars = textLower.Length > 50 ? textLower.Substring(0, 50) : textLower;
                    isNewThread = newThreadIndicators.Any(indicator => first50Chars.Contains(indicator));
                }

                if (isNewThread && currentThread.Count > 0)
                {
                    // Save current thread and start new one
                    threads.Add(new List<dynamic>(currentThread));
                    currentThread.Clear();
                }

                currentThread.Add(prompt);
            }

            // Add last thread
            if (currentThread.Count > 0)
            {
                threads.Add(currentThread);
            }

            return threads;
        }

        /// <summary>
        /// Discovers chat histories from workspace storage
        /// </summary>
        public List<CursorProject> DiscoverChatHistories()
        {
            Logger.LogInfo("[CursorDataService] DiscoverChatHistories: Starting chat history discovery");
            var projects = new List<CursorProject>();
            var workspaceStoragePath = Path.Combine(_cursorUserDataPath, "workspaceStorage");

            if (!Directory.Exists(workspaceStoragePath))
            {
                Logger.LogWarning($"[CursorDataService] DiscoverChatHistories: Workspace storage path does not exist: {workspaceStoragePath}");
                return projects;
            }
            
            Logger.LogInfo($"[CursorDataService] DiscoverChatHistories: Workspace storage path: {workspaceStoragePath}");

            // Get current working directory for matching - try multiple sources
            var currentProjectPaths = new List<string>();
            
            // 1. Environment.CurrentDirectory
            try
            {
                var currentDir = Environment.CurrentDirectory;
                if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir))
                {
                    currentProjectPaths.Add(NormalizePath(currentDir));
                }
            }
            catch { }

            // 2. AppDomain.CurrentDomain.BaseDirectory (if different)
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
                {
                    var normalized = NormalizePath(baseDir);
                    if (!currentProjectPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        currentProjectPaths.Add(normalized);
                    }
                }
            }
            catch { }

            // 3. Try to get from process working directory
            try
            {
                var processDir = Directory.GetCurrentDirectory();
                if (!string.IsNullOrEmpty(processDir) && Directory.Exists(processDir))
                {
                    var normalized = NormalizePath(processDir);
                    if (!currentProjectPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        currentProjectPaths.Add(normalized);
                    }
                }
            }
            catch { }

            // Each workspace has a folder with a UUID name
            var workspaceFolders = Directory.GetDirectories(workspaceStoragePath);

            foreach (var workspaceFolder in workspaceFolders)
            {
                var workspaceId = Path.GetFileName(workspaceFolder);
                
                // Try to find workspace.json to get project path
                var workspaceJsonPath = Path.Combine(workspaceFolder, "workspace.json");
                string? projectPath = null;
                string projectName = workspaceId;

                if (File.Exists(workspaceJsonPath))
                {
                    try
                    {
                        var workspaceJson = File.ReadAllText(workspaceJsonPath);
                        var workspace = JsonConvert.DeserializeObject<dynamic>(workspaceJson);
                        projectPath = workspace?.folder?.ToString();
                        if (!string.IsNullOrEmpty(projectPath))
                        {
                            // Normalize the project path using the helper method
                            projectPath = NormalizePath(projectPath);
                            projectName = Path.GetFileName(projectPath) ?? workspaceId;
                        }
                    }
                    catch
                    {
                        // If parsing fails, use folder name
                    }
                }

                // Normalize project path for comparison
                string normalizedProjectPath = workspaceFolder;
                if (!string.IsNullOrEmpty(projectPath))
                {
                    normalizedProjectPath = NormalizePath(projectPath);
                    // If normalization failed or returned empty, try manual fallback
                    if (string.IsNullOrEmpty(normalizedProjectPath) || normalizedProjectPath == projectPath)
                    {
                        // Fallback: manual decoding for Unix-style Windows paths
                        try
                        {
                            var decoded = Uri.UnescapeDataString(projectPath);
                            if (decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                            {
                                decoded = decoded.Substring(8);
                            }
                            else if (decoded.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                            {
                                decoded = decoded.Substring(7);
                            }
                            // Unix-style Windows path: /g:/... -> G:\...
                            if (decoded.StartsWith("/") && decoded.Length > 1 && decoded[1] == ':')
                            {
                                decoded = decoded.Substring(1).Replace('/', '\\');
                                // Convert to uppercase drive letter
                                if (decoded.Length > 0 && char.IsLower(decoded[0]))
                                {
                                    decoded = char.ToUpper(decoded[0]) + decoded.Substring(1);
                                }
                            }
                            else
                            {
                                decoded = decoded.Replace('/', '\\');
                            }
                            normalizedProjectPath = decoded.TrimEnd('\\', '/');
                            // Try to get full path if it's a valid Windows path
                            if (Path.IsPathRooted(normalizedProjectPath) && !normalizedProjectPath.StartsWith("\\\\"))
                            {
                                try
                                {
                                    normalizedProjectPath = Path.GetFullPath(normalizedProjectPath);
                                }
                                catch
                                {
                                    // Keep the normalized path if GetFullPath fails
                                }
                            }
                        }
                        catch
                        {
                            normalizedProjectPath = workspaceFolder;
                        }
                    }
                }
                else
                {
                    normalizedProjectPath = NormalizePath(workspaceFolder);
                }

                var project = new CursorProject
                {
                    ProjectPath = normalizedProjectPath,
                    ProjectName = projectName
                };

                // Check if this is the current project - prioritize current project in results
                bool isCurrentProject = false;
                if (!string.IsNullOrEmpty(normalizedProjectPath))
                {
                    foreach (var currentPath in currentProjectPaths)
                    {
                        if (PathsMatch(currentPath, normalizedProjectPath))
                        {
                            isCurrentProject = true;
                            // Mark project name to indicate it's current
                            projectName = $"[CURRENT] {projectName}";
                            break;
                        }
                    }
                }

                // Helper method to validate and add chat files
                void AddChatFilesFromDirectory(string directoryPath, string sourceType)
                {
                    if (!Directory.Exists(directoryPath))
                        return;

                    var chatFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileNameWithoutExtension(f);
                            // Chat files have UUID-like names (30-50 chars) OR check content
                            bool isValidName = fileName.Length >= 30 && fileName.Length <= 50;
                            
                            // Validate it's a valid chat JSON file
                            try
                            {
                                var jsonContent = File.ReadAllText(f);
                                var json = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                                
                                if (json == null)
                                    return false;

                                var jsonStr = json.ToString();
                                // Look for common chat file indicators - check for actual chat structure
                                bool hasChatContent = (jsonStr.Contains("requests", StringComparison.OrdinalIgnoreCase) && 
                                                       (jsonStr.Contains("message", StringComparison.OrdinalIgnoreCase) || 
                                                        jsonStr.Contains("response", StringComparison.OrdinalIgnoreCase))) ||
                                                      (jsonStr.Contains("sessionId", StringComparison.OrdinalIgnoreCase) && 
                                                       jsonStr.Contains("creationDate", StringComparison.OrdinalIgnoreCase)) ||
                                                      (jsonStr.Contains("messages", StringComparison.OrdinalIgnoreCase) && 
                                                       jsonStr.Contains("role", StringComparison.OrdinalIgnoreCase)) ||
                                                      jsonStr.Contains("conversation", StringComparison.OrdinalIgnoreCase);
                                
                                // Accept if valid name OR has chat content
                                return isValidName || hasChatContent;
                            }
                            catch
                            {
                                return false; // Invalid JSON, skip
                            }
                        })
                        .ToList();

                    // Add only validated chat files
                    foreach (var chatFile in chatFiles)
                    {
                        var chatId = Path.GetFileNameWithoutExtension(chatFile);
                        var fileName = Path.GetFileName(chatFile);
                        
                        // Check if already added
                        if (!project.ChatHistories.Any(ch => ch.SourcePath == chatFile))
                        {
                            project.ChatHistories.Add(new ChatHistoryItem
                            {
                                ChatId = chatId,
                                ChatName = $"Chat: {fileName}",
                                SourcePath = chatFile,
                                IsAvailable = File.Exists(chatFile)
                            });
                        }
                    }
                }

                // 1. chatSessions directory (most common)
                var chatSessionsPath = Path.Combine(workspaceFolder, "chatSessions");
                AddChatFilesFromDirectory(chatSessionsPath, "chatSessions");
                
                // 2. chatEditingSessions directory (newer Cursor versions)
                var chatEditingSessionsPath = Path.Combine(workspaceFolder, "chatEditingSessions");
                AddChatFilesFromDirectory(chatEditingSessionsPath, "chatEditingSessions");
                
                // 3. Check for other possible chat storage locations
                var otherChatDirs = new[] { "chat", "chats", "conversations", "cursor-chat" };
                foreach (var chatDirName in otherChatDirs)
                {
                    var chatDirPath = Path.Combine(workspaceFolder, chatDirName);
                    if (Directory.Exists(chatDirPath))
                    {
                        var chatFiles = Directory.GetFiles(chatDirPath, "*.json", SearchOption.AllDirectories)
                            .Where(f =>
                            {
                                try
                                {
                                    var jsonContent = File.ReadAllText(f);
                                    var json = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                                    if (json == null) return false;
                                    var jsonStr = json.ToString();
                                    return jsonStr.Contains("message", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("role", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("content", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("sessionId", StringComparison.OrdinalIgnoreCase);
                                }
                                catch
                                {
                                    return false;
                                }
                            })
                            .ToList();
                        
                        foreach (var chatFile in chatFiles)
                        {
                            var chatId = $"chat_{Path.GetFileNameWithoutExtension(chatFile)}";
                            var fileName = Path.GetFileName(chatFile);
                            
                            // Check if already added
                            if (!project.ChatHistories.Any(ch => ch.SourcePath == chatFile))
                            {
                                project.ChatHistories.Add(new ChatHistoryItem
                                {
                                    ChatId = chatId,
                                    ChatName = $"Chat: {fileName}",
                                    SourcePath = chatFile,
                                    IsAvailable = File.Exists(chatFile)
                                });
                            }
                        }
                    }
                }
                
                // 4. Check workspace state.vscdb for aiService.prompts (combined chat from prompts)
                var workspaceStateDb = Path.Combine(workspaceFolder, "state.vscdb");
                if (File.Exists(workspaceStateDb))
                {
                    try
                    {
                        // Query state.vscdb for aiService.prompts and group into threads
                        using (var connection = new SQLiteConnection($"Data Source={workspaceStateDb};Mode=ReadOnly"))
                        {
                            connection.Open();
                            
                            // Get aiService.prompts
                            using (var command = new SQLiteCommand("SELECT value FROM ItemTable WHERE key = 'aiService.prompts'", connection))
                            {
                                using (var reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        var promptsJson = reader.GetString(0);
                                        if (!string.IsNullOrEmpty(promptsJson))
                                        {
                                            try
                                            {
                                                var prompts = JsonConvert.DeserializeObject<List<dynamic>>(promptsJson);
                                                if (prompts != null && prompts.Count > 0)
                                                {
                                                    // NEW: Group prompts by bubble sessions first
                                                    // This ensures prompts with matching bubbles are grouped together
                                                    Logger.LogInfo($"[CursorDataService] DiscoverChatHistories: Grouping {prompts.Count} prompts by bubble sessions for project {projectName}");
                                                    var promptSessions = GroupPromptsByBubbleSessions(prompts, workspaceStateDb);
                                                    
                                                    // Add each session as a separate chat
                                                    int sessionIndex = 0;
                                                    foreach (var session in promptSessions)
                                                    {
                                                        sessionIndex++;
                                                        var sessionChatPath = Path.Combine(workspaceFolder, $"{projectName}_session_{sessionIndex}.json");
                                                        
                                                        // Check for duplicates
                                                        var isDuplicate = project.ChatHistories.Any(ch => 
                                                            ch.SourcePath == sessionChatPath || 
                                                            (ch.IsFromStateDb && ch.StateDbPath == workspaceStateDb && 
                                                             ch.ChatId == $"{workspaceId}_session_{sessionIndex}"));
                                                        
                                                        if (!isDuplicate)
                                                        {
                                                            var sessionName = (session.HasBubbles && session.HasResponses)
                                                                ? $"Chat: {projectName} - Session {sessionIndex} ({session.Prompts.Count} prompts, with responses)"
                                                                : $"Chat: {projectName} - Session {sessionIndex} ({session.Prompts.Count} prompts, no responses)";
                                                            
                                                            project.ChatHistories.Add(new ChatHistoryItem
                                                            {
                                                                ChatId = $"{workspaceId}_session_{sessionIndex}",
                                                                ChatName = sessionName,
                                                                SourcePath = sessionChatPath,
                                                                IsAvailable = true,
                                                                IsFromStateDb = true,
                                                                StateDbPath = workspaceStateDb,
                                                                ProjectPath = projectPath
                                                            });
                                                            Logger.LogInfo($"[CursorDataService] DiscoverChatHistories: Added chat session: {sessionName}");
                                                        }
                                                        else
                                                        {
                                                            Logger.LogDebug($"[CursorDataService] DiscoverChatHistories: Skipped duplicate session {sessionIndex}");
                                                        }
                                                    }
                                                    
                                                    // FALLBACK: If no sessions found, use thread grouping
                                                    if (promptSessions.Count == 0)
                                                    {
                                                        var threads = GroupPromptsIntoThreads(prompts);
                                                        for (int i = 0; i < threads.Count; i++)
                                                        {
                                                            var thread = threads[i];
                                                            var threadChatPath = Path.Combine(workspaceFolder, $"{projectName}_thread_{i + 1}.json");
                                                            
                                                            var isDuplicate = project.ChatHistories.Any(ch => 
                                                                ch.SourcePath == threadChatPath || 
                                                                (ch.IsFromStateDb && ch.StateDbPath == workspaceStateDb && 
                                                                 ch.ChatId == $"{workspaceId}_thread_{i + 1}"));
                                                            
                                                            if (!isDuplicate)
                                                            {
                                                                project.ChatHistories.Add(new ChatHistoryItem
                                                                {
                                                                    ChatId = $"{workspaceId}_thread_{i + 1}",
                                                                    ChatName = $"Chat: {projectName} - Thread {i + 1} ({thread.Count} prompts)",
                                                                    SourcePath = threadChatPath,
                                                                    IsAvailable = true,
                                                                    IsFromStateDb = true,
                                                                    StateDbPath = workspaceStateDb,
                                                                    ProjectPath = projectPath
                                                                });
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                // Failed to parse prompts, skip
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Also get composerChatViewPane UUIDs as separate chat sessions
                            // Check for duplicate content using MD5 hash (same logic as Python script)
                            var sessionContentHashes = new Dictionary<string, string>(); // content hash -> first UUID
                            using (var command = new SQLiteCommand("SELECT key, value FROM ItemTable WHERE key LIKE 'workbench.panel.composerChatViewPane.%'", connection))
                            {
                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        var key = reader.GetString(0);
                                        
                                        // Get value, handle null/empty - EXACT same as Python
                                        string? value = null;
                                        try
                                        {
                                            if (reader.IsDBNull(1))
                                                value = null;
                                            else
                                                value = reader.GetString(1);
                                        }
                                        catch
                                        {
                                            value = null;
                                        }
                                        
                                        // Extract UUID from key (last part after last dot) - EXACT same as Python
                                        var uuid = key.Split('.').LastOrDefault();
                                        if (string.IsNullOrEmpty(uuid) || uuid.Length <= 30)
                                            continue;
                                        
                                        // Check if this is real chat content, not just UI state (same as Python is_real_chat_session)
                                        if (!IsRealChatSession(value))
                                        {
                                            // Skip UI-only sessions
                                            continue;
                                        }
                                        
                                        // Calculate content hash - EXACT same as Python calculate_content_hash
                                        var contentHash = CalculateContentHash(value);
                                        
                                        // Check if we've seen this content before - EXACT same as Python
                                        if (sessionContentHashes.ContainsKey(contentHash))
                                        {
                                            // This is a duplicate - skip it (same as Python continue)
                                            continue;
                                        }
                                        
                                        // This is unique content - add it (same as Python assignment)
                                        sessionContentHashes[contentHash] = uuid;
                                        
                                        var composerChatPath = Path.Combine(workspaceFolder, $"{projectName}_session_{uuid}.json");
                                        
                                        // Add directly - hash check already ensures uniqueness (same as Python)
                                        project.ChatHistories.Add(new ChatHistoryItem
                                        {
                                            ChatId = $"{workspaceId}_session_{uuid}",
                                            ChatName = $"Chat: {projectName} - Session {uuid}",
                                            SourcePath = composerChatPath,
                                            IsAvailable = true,
                                            IsFromStateDb = true,
                                            StateDbPath = workspaceStateDb,
                                            ProjectPath = projectPath
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Failed to read state.vscdb, skip
                    }
                }
                
                // 5. Check workspace state.vscdb for chat metadata (if chatSessions is empty) - OLD CHECK
                if (project.ChatHistories.Count == 0)
                {
                    if (File.Exists(workspaceStateDb))
                    {
                        try
                        {
                            using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={workspaceStateDb};Read Only=True"))
                            {
                                conn.Open();
                                using (var cmd = conn.CreateCommand())
                                {
                                    // Try to find chat-related tables
                                    cmd.CommandText = @"
                                        SELECT name FROM sqlite_master 
                                        WHERE type='table' 
                                        AND (name LIKE '%chat%' OR name LIKE '%session%')
                                    ";
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.HasRows)
                                        {
                                            var tables = new List<string>();
                                            while (reader.Read())
                                            {
                                                tables.Add(reader.GetString(0));
                                            }
                                            if (tables.Count > 0)
                                            {
                                                // Add a placeholder indicating chats might be in database
                                                project.ChatHistories.Add(new ChatHistoryItem
                                                {
                                                    ChatId = "db_chats",
                                                    ChatName = $"[Chats in database: {string.Join(", ", tables)}]",
                                                    SourcePath = workspaceStateDb,
                                                    IsAvailable = true
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Database might be locked or inaccessible
                        }
                    }
                }

                // Add ALL projects that have chat histories OR are the current project
                // IMPORTANT: Show current project even if no chats found (chats might be in cloud)
                if (project.ChatHistories.Count > 0 || isCurrentProject)
                {
                    // If current project but no chats, add a placeholder message
                    if (isCurrentProject && project.ChatHistories.Count == 0)
                    {
                        project.ChatHistories.Add(new ChatHistoryItem
                        {
                            ChatId = "no_chats_yet",
                            ChatName = "[No chat sessions found - chats may be in cloud storage or state.vscdb]",
                            SourcePath = workspaceFolder,
                            IsAvailable = false
                        });
                    }
                    projects.Add(project);
                }
                // Also add projects with chatEditingSessions even if no chatSessions
                else if (Directory.Exists(Path.Combine(workspaceFolder, "chatEditingSessions")))
                {
                    projects.Add(project);
                }
            }

            Logger.LogInfo($"[CursorDataService] DiscoverChatHistories: Found {projects.Count} projects before merging");
            
            // Merge projects with the same name
            var mergedProjects = new Dictionary<string, CursorProject>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var project in projects)
            {
                var projectName = project.ProjectName;
                Logger.LogDebug($"[CursorDataService] DiscoverChatHistories: Processing project '{projectName}' with {project.ChatHistories.Count} chat histories");
                // Remove [CURRENT] prefix for merging
                var cleanName = projectName.Replace("[CURRENT] ", "", StringComparison.OrdinalIgnoreCase).Trim();
                
                if (mergedProjects.TryGetValue(cleanName, out var existingProject))
                {
                    // Merge chat histories from this project into existing one
                    foreach (var chat in project.ChatHistories)
                    {
                        // Check if chat already exists (by SourcePath)
                        if (!existingProject.ChatHistories.Any(ch => ch.SourcePath == chat.SourcePath))
                        {
                            existingProject.ChatHistories.Add(chat);
                        }
                    }
                    // Keep [CURRENT] prefix if either project had it
                    if (projectName.StartsWith("[CURRENT]", StringComparison.OrdinalIgnoreCase))
                    {
                        existingProject.ProjectName = $"[CURRENT] {cleanName}";
                    }
                }
                else
                {
                    mergedProjects[cleanName] = project;
                }
            }
            
            // Sort projects: current project first, then alphabetically
            var finalProjects = mergedProjects.Values
                .OrderByDescending(p => p.ProjectName.StartsWith("[CURRENT]", StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p.ProjectName.Replace("[CURRENT] ", "", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            Logger.LogInfo($"[CursorDataService] DiscoverChatHistories: Returning {finalProjects.Count} merged projects");
            foreach (var project in finalProjects)
            {
                Logger.LogInfo($"[CursorDataService] DiscoverChatHistories: Final project '{project.ProjectName}' has {project.ChatHistories.Count} chat histories");
            }
            
            return finalProjects;
        }

        /// <summary>
        /// Loads settings from a backup folder - recursively scans the entire folder
        /// </summary>
        public List<CursorSettingItem> LoadSettingsFromBackup(string backupPath)
        {
            var settings = new List<CursorSettingItem>();
            var foundIds = new HashSet<string>(); // To avoid duplicates

            if (!Directory.Exists(backupPath))
                return settings;

            // Recursively scan for all relevant files and folders
            ScanDirectoryForSettings(backupPath, backupPath, settings, foundIds);

            // Also discover documentation groups from backup (if globalStorage/state.vscdb exists)
            var globalStorageStateDbPath = Path.Combine(backupPath, "User", "globalStorage", "state.vscdb");
            if (!File.Exists(globalStorageStateDbPath))
            {
                // Try alternative path
                globalStorageStateDbPath = Path.Combine(backupPath, "globalStorage", "state.vscdb");
            }
            
            if (File.Exists(globalStorageStateDbPath))
            {
                try
                {
                    var docGroups = DiscoverDocumentationGroupsFromBackup(globalStorageStateDbPath);
                    foreach (var group in docGroups)
                    {
                        foreach (var doc in group.Documentations)
                        {
                            // Check if already added
                            if (!foundIds.Contains(doc.Id))
                            {
                                foundIds.Add(doc.Id);
                                settings.Add(doc);
                            }
                        }
                    }
                }
                catch
                {
                    // Failed to load documentation, continue
                }
            }

            return settings;
        }

        /// <summary>
        /// Discovers documentation groups from a backup state.vscdb file
        /// </summary>
        public List<DocumentationGroup> DiscoverDocumentationGroupsFromBackup(string stateDbPath)
        {
            var groups = new List<DocumentationGroup>();
            var foundUrls = new HashSet<string>(); // To avoid duplicates
            var docsByDomain = new Dictionary<string, List<CursorSettingItem>>();
            
            if (!File.Exists(stateDbPath))
                return groups;
            
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={stateDbPath}"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Search cursorDiskKV for documentation URLs (same logic as DiscoverDocumentationGroups)
                        cmd.CommandText = @"
                            SELECT key, value 
                            FROM cursorDiskKV 
                            WHERE (key LIKE '%composerData%' OR key LIKE '%bubbleId%' OR key LIKE '%messageRequestContext%')
                            AND (value LIKE '%developers.%' OR value LIKE '%docs.%' OR value LIKE '%graph-api%' OR value LIKE '%facebook%' OR value LIKE '%flutter%' OR value LIKE '%github%' OR value LIKE '%google%')
                        ";
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                                if (string.IsNullOrEmpty(value))
                                    continue;
                                
                                var urls = ExtractDocumentationUrls(value);
                                foreach (var url in urls)
                                {
                                    if (!IsValidDocumentationUrl(url) || foundUrls.Contains(url))
                                        continue;
                                    
                                    foundUrls.Add(url);
                                    
                                    var domain = ExtractDomainFromUrl(url);
                                    var docName = ExtractDocNameFromUrl(url);
                                    
                                    if (string.IsNullOrEmpty(domain))
                                        domain = "Other";
                                    
                                    if (!docsByDomain.ContainsKey(domain))
                                    {
                                        docsByDomain[domain] = new List<CursorSettingItem>();
                                    }
                                    
                                    var docItem = new CursorSettingItem
                                    {
                                        Id = $"doc_{url.GetHashCode()}",
                                        Name = docName,
                                        Description = $"Documentation URL: {url}\n" +
                                                    $"Stored in: {Path.GetFileName(stateDbPath)}",
                                        Category = "Documentation",
                                        SourcePath = stateDbPath, // Reference to state.vscdb
                                        DestinationPath = Path.GetRelativePath(Path.GetDirectoryName(stateDbPath) ?? "", stateDbPath).Replace('\\', '/'),
                                        IsAvailable = true,
                                        Type = SettingType.Documentation
                                    };
                                    
                                    docsByDomain[domain].Add(docItem);
                                }
                            }
                        }
                    }
                }
                
                // Create DocumentationGroup objects
                foreach (var kvp in docsByDomain.OrderBy(x => x.Key))
                {
                    var group = new DocumentationGroup
                    {
                        GroupName = kvp.Key,
                        Documentations = new System.Collections.ObjectModel.ObservableCollection<CursorSettingItem>(kvp.Value.OrderBy(d => d.Name))
                    };
                    groups.Add(group);
                }
            }
            catch
            {
                // Failed to read state.vscdb, return empty list
            }
            
            return groups;
        }

        /// <summary>
        /// Recursively scans a directory for Cursor settings files
        /// </summary>
        private void ScanDirectoryForSettings(string rootPath, string currentPath, List<CursorSettingItem> settings, HashSet<string> foundIds)
        {
            try
            {
                // Scan files
                var files = Directory.GetFiles(currentPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file).ToLowerInvariant();
                    var relativePath = Path.GetRelativePath(rootPath, file);
                    var relativeDir = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";

                    // settings.json
                    if (fileName == "settings.json" && !foundIds.Contains("settings.json"))
                    {
                        foundIds.Add("settings.json");
                        settings.Add(new CursorSettingItem
                        {
                            Id = $"settings_{relativePath.GetHashCode()}",
                            Name = "Global Settings",
                            Description = $"Cursor/VSCode user settings ({relativeDir})",
                            Category = "Configuration",
                            SourcePath = file,
                            DestinationPath = relativePath.Replace('\\', '/'),
                            IsAvailable = true,
                            Type = SettingType.GlobalSettings
                        });
                    }
                    // keybindings.json
                    else if (fileName == "keybindings.json" && !foundIds.Contains("keybindings.json"))
                    {
                        foundIds.Add("keybindings.json");
                        settings.Add(new CursorSettingItem
                        {
                            Id = $"keybindings_{relativePath.GetHashCode()}",
                            Name = "Keybindings",
                            Description = $"Keyboard shortcuts configuration ({relativeDir})",
                            Category = "Configuration",
                            SourcePath = file,
                            DestinationPath = relativePath.Replace('\\', '/'),
                            IsAvailable = true,
                            Type = SettingType.Keybindings
                        });
                    }
                    // state.vscdb - ONLY add the main one (root level), NOT workspace ones
                    else if (fileName == "state.vscdb")
                    {
                        // Only add if it's the main state.vscdb (at root or User/globalStorage level), not workspace ones
                        var isMainStateDb = relativeDir == "" || 
                                           relativeDir.Equals("User/globalStorage", StringComparison.OrdinalIgnoreCase) ||
                                           relativeDir.Equals("globalStorage", StringComparison.OrdinalIgnoreCase);
                        
                        if (isMainStateDb && !foundIds.Contains("state.vscdb"))
                        {
                            foundIds.Add("state.vscdb");
                            settings.Add(new CursorSettingItem
                            {
                                Id = "state.vscdb",
                                Name = "State Database",
                                Description = "Cursor application state database",
                                Category = "Data",
                                SourcePath = file,
                                DestinationPath = relativePath.Replace('\\', '/'),
                                IsAvailable = true,
                                Type = SettingType.StateDatabase
                            });
                        }
                        // Skip workspace state.vscdb files - they're handled separately for chat histories
                    }
                    // languagepacks.json
                    else if (fileName == "languagepacks.json" && !foundIds.Contains("languagepacks.json"))
                    {
                        foundIds.Add("languagepacks.json");
                        settings.Add(new CursorSettingItem
                        {
                            Id = $"languagepacks_{relativePath.GetHashCode()}",
                            Name = "Language Packs",
                            Description = $"Installed language pack configuration ({relativeDir})",
                            Category = "Configuration",
                            SourcePath = file,
                            DestinationPath = relativePath.Replace('\\', '/'),
                            IsAvailable = true,
                            Type = SettingType.LanguagePacks
                        });
                    }
                }

                // Scan directories
                var directories = Directory.GetDirectories(currentPath);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir).ToLowerInvariant();
                    var relativePath = Path.GetRelativePath(rootPath, dir);
                    var relativeDir = relativePath.Replace('\\', '/');

                    // globalStorage
                    if (dirName == "globalstorage" && !foundIds.Contains($"globalstorage_{relativeDir}"))
                    {
                        foundIds.Add($"globalstorage_{relativeDir}");
                        settings.Add(new CursorSettingItem
                        {
                            Id = $"globalstorage_{relativePath.GetHashCode()}",
                            Name = "Global Storage",
                            Description = $"Extensions global storage data ({relativeDir})",
                            Category = "Data",
                            SourcePath = dir,
                            DestinationPath = relativeDir,
                            IsAvailable = true,
                            Type = SettingType.GlobalStorage
                        });
                    }
                    // workspaceStorage - for chat histories
                    else if (dirName == "workspacestorage")
                    {
                        // Add workspaceStorage as a setting item itself
                        var wsRelative = Path.GetRelativePath(rootPath, dir);
                        var wsId = $"workspacestorage_{wsRelative.GetHashCode()}";
                        if (!foundIds.Contains(wsId))
                        {
                            foundIds.Add(wsId);
                            settings.Add(new CursorSettingItem
                            {
                                Id = wsId,
                                Name = "Workspace Storage",
                                Description = $"Workspace storage directory ({wsRelative})",
                                Category = "Data",
                                SourcePath = dir,
                                DestinationPath = wsRelative.Replace('\\', '/'),
                                IsAvailable = true,
                                Type = SettingType.WorkspaceSettings
                            });
                        }
                        
                        // This will be handled separately for chat histories
                        ScanWorkspaceStorageForChats(rootPath, dir, settings, foundIds);
                    }
                    // .cursor/rules and .cursor/docs
                    else if (dirName == ".cursor")
                    {
                        // Check for rules folder
                        var rulesPath = Path.Combine(dir, "rules");
                        if (Directory.Exists(rulesPath))
                        {
                            var rulesFiles = Directory.GetFiles(rulesPath, "*", SearchOption.AllDirectories);
                            foreach (var rulesFile in rulesFiles)
                            {
                                var rulesRelative = Path.GetRelativePath(rootPath, rulesFile);
                                var rulesId = $"rules_{rulesRelative.GetHashCode()}";
                                if (!foundIds.Contains(rulesId))
                                {
                                    foundIds.Add(rulesId);
                                    settings.Add(new CursorSettingItem
                                    {
                                        Id = rulesId,
                                        Name = $"Rules: {Path.GetFileName(rulesFile)}",
                                        Description = $"Cursor rules file ({Path.GetDirectoryName(rulesRelative)})",
                                        Category = "Configuration",
                                        SourcePath = rulesFile,
                                        DestinationPath = rulesRelative.Replace('\\', '/'),
                                        IsAvailable = true,
                                        Type = SettingType.Rules
                                    });
                                }
                            }
                        }
                        
                        // Check for docs folder (global cursor documentation)
                        var docsPath = Path.Combine(dir, "docs");
                        if (Directory.Exists(docsPath))
                        {
                            var docsRelative = Path.GetRelativePath(rootPath, docsPath);
                            var docsId = $"cursor_docs_{docsRelative.GetHashCode()}";
                            if (!foundIds.Contains(docsId))
                            {
                                foundIds.Add(docsId);
                                settings.Add(new CursorSettingItem
                                {
                                    Id = docsId,
                                    Name = "Cursor Global Documentation",
                                    Description = $"Global Cursor documentation folder ({docsRelative})",
                                    Category = "Documentation",
                                    SourcePath = docsPath,
                                    DestinationPath = docsRelative.Replace('\\', '/'),
                                    IsAvailable = true,
                                    Type = SettingType.Other
                                });
                            }
                        }
                    }
                    // Also check for rules folder directly
                    else if (dirName == "rules")
                    {
                        var rulesFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                        foreach (var rulesFile in rulesFiles)
                        {
                            var rulesRelative = Path.GetRelativePath(rootPath, rulesFile);
                            var rulesId = $"rules_{rulesRelative.GetHashCode()}";
                            if (!foundIds.Contains(rulesId))
                            {
                                foundIds.Add(rulesId);
                                settings.Add(new CursorSettingItem
                                {
                                    Id = rulesId,
                                    Name = $"Rules: {Path.GetFileName(rulesFile)}",
                                    Description = $"Cursor rules file ({Path.GetDirectoryName(rulesRelative)})",
                                    Category = "Configuration",
                                    SourcePath = rulesFile,
                                    DestinationPath = rulesRelative.Replace('\\', '/'),
                                    IsAvailable = true,
                                    Type = SettingType.Rules
                                });
                            }
                        }
                    }

                    // Recursively scan subdirectories
                    ScanDirectoryForSettings(rootPath, dir, settings, foundIds);
                }
            }
            catch (Exception)
            {
                // Ignore access errors and continue scanning
            }
        }

        /// <summary>
        /// Scans workspaceStorage directory for chat histories
        /// </summary>
        private void ScanWorkspaceStorageForChats(string rootPath, string workspaceStoragePath, List<CursorSettingItem> settings, HashSet<string> foundIds)
        {
            try
            {
                if (!Directory.Exists(workspaceStoragePath))
                    return;

                var workspaceFolders = Directory.GetDirectories(workspaceStoragePath);
                foreach (var workspaceFolder in workspaceFolders)
                {
                    var workspaceId = Path.GetFileName(workspaceFolder);
                    var workspaceJsonPath = Path.Combine(workspaceFolder, "workspace.json");
                    
                    string? projectPath = null;
                    string projectName = workspaceId;

                    if (File.Exists(workspaceJsonPath))
                    {
                        try
                        {
                            var workspaceJson = File.ReadAllText(workspaceJsonPath);
                            var workspace = JsonConvert.DeserializeObject<dynamic>(workspaceJson);
                            projectPath = workspace?.folder?.ToString();
                            if (!string.IsNullOrEmpty(projectPath))
                            {
                                // Extract project name from URI
                                try
                                {
                                    var uri = new Uri(projectPath);
                                    projectName = Path.GetFileName(uri.LocalPath) ?? workspaceId;
                                }
                                catch
                                {
                                    projectName = Path.GetFileName(projectPath) ?? workspaceId;
                                }
                            }
                        }
                        catch
                        {
                            // If parsing fails, use folder name
                        }
                    }

                    // 1. Look for actual chat files in chatSessions directory
                    // Cursor stores chat files in: workspaceStorage\[workspace-id]\chatSessions\[uuid].json
                    var chatSessionsPath = Path.Combine(workspaceFolder, "chatSessions");
                    if (Directory.Exists(chatSessionsPath))
                    {
                        var chatFiles = Directory.GetFiles(chatSessionsPath, "*.json", SearchOption.TopDirectoryOnly)
                            .Where(f =>
                            {
                                var fileName = Path.GetFileNameWithoutExtension(f);
                                // Chat files have UUID-like names (36 chars with hyphens)
                                // Example: 3e758e78-0947-4e44-8363-0ccaa285c4d6.json
                                if (fileName.Length < 30 || fileName.Length > 50)
                                    return false;

                                // Validate it's a valid chat JSON file
                                try
                                {
                                    var jsonContent = File.ReadAllText(f);
                                    var json = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                                    
                                    // Check if it has chat-like structure
                                    // Chat files typically have: messages, title, id, or similar fields
                                    if (json == null)
                                        return false;

                                    var jsonStr = json.ToString();
                                    // Look for common chat file indicators
                                    return jsonStr.Contains("message", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("role", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("content", StringComparison.OrdinalIgnoreCase);
                                }
                                catch
                                {
                                    return false; // Invalid JSON, skip
                                }
                            })
                            .ToList();

                        // Add only validated chat files
                        foreach (var chatFile in chatFiles)
                        {
                            var chatRelative = Path.GetRelativePath(rootPath, chatFile);
                            var chatId = $"chat_{chatRelative.GetHashCode()}";
                            
                            if (!foundIds.Contains(chatId))
                            {
                                foundIds.Add(chatId);
                                var fileName = Path.GetFileName(chatFile);
                                settings.Add(new CursorSettingItem
                                {
                                    Id = chatId,
                                    Name = $"Chat: {projectName} - {fileName}",
                                    Description = $"Validated chat history from project '{projectName}' (chatSessions)",
                                    Category = "Chat History",
                                    SourcePath = chatFile,
                                    DestinationPath = chatRelative.Replace('\\', '/'),
                                    IsAvailable = true,
                                    Type = SettingType.ChatHistory,
                                    ProjectPath = projectPath ?? workspaceFolder
                                });
                            }
                        }
                    }
                    
                    // 2. Check for chatEditingSessions directory
                    var chatEditingSessionsPath = Path.Combine(workspaceFolder, "chatEditingSessions");
                    if (Directory.Exists(chatEditingSessionsPath))
                    {
                        var chatFiles = Directory.GetFiles(chatEditingSessionsPath, "*.json", SearchOption.TopDirectoryOnly)
                            .Where(f =>
                            {
                                var fileName = Path.GetFileNameWithoutExtension(f);
                                if (fileName.Length < 30 || fileName.Length > 50)
                                    return false;
                                
                                try
                                {
                                    var jsonContent = File.ReadAllText(f);
                                    var json = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                                    if (json == null)
                                        return false;
                                    
                                    var jsonStr = json.ToString();
                                    return jsonStr.Contains("message", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("role", StringComparison.OrdinalIgnoreCase) ||
                                           jsonStr.Contains("content", StringComparison.OrdinalIgnoreCase);
                                }
                                catch
                                {
                                    return false;
                                }
                            })
                            .ToList();
                        
                        foreach (var chatFile in chatFiles)
                        {
                            var chatRelative = Path.GetRelativePath(rootPath, chatFile);
                            var chatId = $"chat_{chatRelative.GetHashCode()}";
                            
                            if (!foundIds.Contains(chatId))
                            {
                                foundIds.Add(chatId);
                                var fileName = Path.GetFileName(chatFile);
                                settings.Add(new CursorSettingItem
                                {
                                    Id = chatId,
                                    Name = $"Chat: {projectName} - {fileName}",
                                    Description = $"Validated chat history from project '{projectName}' (chatEditingSessions)",
                                    Category = "Chat History",
                                    SourcePath = chatFile,
                                    DestinationPath = chatRelative.Replace('\\', '/'),
                                    IsAvailable = true,
                                    Type = SettingType.ChatHistory,
                                    ProjectPath = projectPath ?? workspaceFolder
                                });
                            }
                        }
                    }
                    
                    // 3. Check state.vscdb for prompts and group into threads (same logic as DiscoverChatHistories)
                    var stateDbPath = Path.Combine(workspaceFolder, "state.vscdb");
                    if (File.Exists(stateDbPath))
                    {
                        try
                        {
                            using (var connection = new SQLiteConnection($"Data Source={stateDbPath};Mode=ReadOnly"))
                            {
                                connection.Open();
                                
                                // Get aiService.prompts and group into threads
                                using (var command = new SQLiteCommand("SELECT value FROM ItemTable WHERE key = 'aiService.prompts'", connection))
                                {
                                    using (var reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            var promptsJson = reader.GetString(0);
                                            if (!string.IsNullOrEmpty(promptsJson))
                                            {
                                                try
                                                {
                                                    var prompts = JsonConvert.DeserializeObject<List<dynamic>>(promptsJson);
                                                    if (prompts != null && prompts.Count > 0)
                                                    {
                                                        // Group prompts into threads
                                                        var threads = GroupPromptsIntoThreads(prompts);
                                                        
                                                        // Add each thread as a separate chat history (NO COMBINED VERSION)
                                                        for (int i = 0; i < threads.Count; i++)
                                                        {
                                                            var thread = threads[i];
                                                            var threadChatPath = Path.Combine(workspaceFolder, $"{projectName}_thread_{i + 1}.json");
                                                            var chatRelative = Path.GetRelativePath(rootPath, threadChatPath);
                                                            var chatId = $"thread_{i + 1}_{workspaceId}";
                                                            
                                                            // Check for duplicates
                                                            var isDuplicate = foundIds.Contains(chatId) || 
                                                                settings.Any(s => s.SourcePath == threadChatPath || 
                                                                                (s.SourcePath.Contains("_thread_") && 
                                                                                 s.SourcePath.Contains($"_thread_{i + 1}") &&
                                                                                 s.ProjectPath == (projectPath ?? workspaceFolder)));
                                                            
                                                            if (!isDuplicate)
                                                            {
                                                                foundIds.Add(chatId);
                                                                settings.Add(new CursorSettingItem
                                                                {
                                                                    Id = chatId,
                                                                    Name = $"Chat: {projectName} - Thread {i + 1} ({thread.Count} prompts)",
                                                                    Description = $"Chat thread {i + 1} from state.vscdb for project '{projectName}'",
                                                                    Category = "Chat History",
                                                                    SourcePath = threadChatPath,
                                                                    DestinationPath = chatRelative.Replace('\\', '/'),
                                                                    IsAvailable = true,
                                                                    Type = SettingType.ChatHistory,
                                                                    ProjectPath = projectPath ?? workspaceFolder
                                                                });
                                                            }
                                                        }
                                                    }
                                                }
                                                catch
                                                {
                                                    // Failed to parse prompts, skip
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                // Also get composerChatViewPane UUIDs as separate chat sessions
                                // Check for duplicate content using MD5 hash (same logic as Python script)
                                var sessionContentHashes = new Dictionary<string, string>(); // content hash -> first UUID
                                using (var command = new SQLiteCommand("SELECT key, value FROM ItemTable WHERE key LIKE 'workbench.panel.composerChatViewPane.%'", connection))
                                {
                                    using (var reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            var key = reader.GetString(0);
                                            
                                            // Get value, handle null/empty - EXACT same as Python
                                            string? value = null;
                                            try
                                            {
                                                if (reader.IsDBNull(1))
                                                    value = null;
                                                else
                                                    value = reader.GetString(1);
                                            }
                                            catch
                                            {
                                                value = null;
                                            }
                                            
                                            // Extract UUID from key (last part after last dot) - EXACT same as Python
                                            var uuid = key.Split('.').LastOrDefault();
                                            if (string.IsNullOrEmpty(uuid) || uuid.Length <= 30)
                                                continue;
                                            
                                            // Check if this is real chat content, not just UI state (same as Python is_real_chat_session)
                                            if (!IsRealChatSession(value))
                                            {
                                                // Skip UI-only sessions
                                                continue;
                                            }
                                            
                                            // Calculate content hash - EXACT same as Python calculate_content_hash
                                            var contentHash = CalculateContentHash(value);
                                            
                                            // Check if we've seen this content before - EXACT same as Python
                                            if (sessionContentHashes.ContainsKey(contentHash))
                                            {
                                                // This is a duplicate - skip it (same as Python continue)
                                                continue;
                                            }
                                            
                                            // This is unique content - add it (same as Python assignment)
                                            sessionContentHashes[contentHash] = uuid;
                                            
                                            var composerChatPath = Path.Combine(workspaceFolder, $"{projectName}_session_{uuid}.json");
                                            var chatRelative = Path.GetRelativePath(rootPath, composerChatPath);
                                            var chatId = $"session_{uuid}_{workspaceId}";
                                            
                                            // Add directly - hash check already ensures uniqueness (same as Python)
                                            foundIds.Add(chatId);
                                            settings.Add(new CursorSettingItem
                                            {
                                                Id = chatId,
                                                Name = $"Chat: {projectName} - Session {uuid}",
                                                Description = $"Chat session from composerChatViewPane for project '{projectName}'",
                                                Category = "Chat History",
                                                SourcePath = composerChatPath,
                                                DestinationPath = chatRelative.Replace('\\', '/'),
                                                IsAvailable = true,
                                                Type = SettingType.ChatHistory,
                                                ProjectPath = projectPath ?? workspaceFolder
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Failed to read state.vscdb, skip
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue
                System.Diagnostics.Debug.WriteLine($"Error scanning workspaceStorage: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers documentation entries from globalStorage state.vscdb
        /// </summary>
        private List<CursorSettingItem> DiscoverDocumentationFromGlobalStorage(string stateDbPath)
        {
            var docs = new List<CursorSettingItem>();
            var foundUrls = new HashSet<string>(); // To avoid duplicates
            
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={stateDbPath}"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Search cursorDiskKV for documentation URLs
                        cmd.CommandText = @"
                            SELECT key, value 
                            FROM cursorDiskKV 
                            WHERE (key LIKE 'composerData:%' 
                               OR key LIKE 'bubbleId:%'
                               OR key LIKE 'messageRequestContext:%')
                               AND (value LIKE '%developers.%' 
                               OR value LIKE '%docs.%'
                               OR value LIKE '%graph-api%')
                            LIMIT 100";
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var key = reader.GetString(0);
                                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                                
                                if (string.IsNullOrEmpty(value))
                                    continue;
                                
                                // Extract URLs from value
                                var urls = ExtractDocumentationUrls(value);
                                
                                foreach (var url in urls)
                                {
                                    if (!foundUrls.Contains(url))
                                    {
                                        foundUrls.Add(url);
                                        
                                        // Extract doc name from URL
                                        var docName = ExtractDocNameFromUrl(url);
                                        
                                        docs.Add(new CursorSettingItem
                                        {
                                            Id = $"doc_{url.GetHashCode()}",
                                            Name = $"Documentation: {docName}",
                                            Description = $"Documentation URL: {url}\n" +
                                                         $"Source: {key}\n" +
                                                         $"Stored in: globalStorage/state.vscdb",
                                            Category = "Documentation",
                                            SourcePath = stateDbPath,
                                            DestinationPath = $"globalStorage/state.vscdb",
                                            IsAvailable = true,
                                            Type = SettingType.Documentation,
                                            ProjectPath = null
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error discovering documentation: {ex.Message}");
            }
            
            return docs;
        }

        /// <summary>
        /// Discovers documentation groups from globalStorage state.vscdb
        /// </summary>
        public List<DocumentationGroup> DiscoverDocumentationGroups()
        {
            var groups = new List<DocumentationGroup>();
            var foundUrls = new HashSet<string>(); // To avoid duplicates
            var docsByDomain = new Dictionary<string, List<CursorSettingItem>>();
            
            var globalStorageStateDbPath = Path.Combine(_cursorUserDataPath, "globalStorage", "state.vscdb");
            if (!File.Exists(globalStorageStateDbPath))
                return groups;
            
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={globalStorageStateDbPath}"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Search cursorDiskKV for documentation URLs
                        cmd.CommandText = @"
                            SELECT key, value 
                            FROM cursorDiskKV 
                            WHERE (key LIKE 'composerData:%' 
                               OR key LIKE 'bubbleId:%'
                               OR key LIKE 'messageRequestContext:%')
                               AND (value LIKE '%developers.%' 
                               OR value LIKE '%docs.%'
                               OR value LIKE '%graph-api%')
                            LIMIT 100";
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var key = reader.GetString(0);
                                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                                
                                if (string.IsNullOrEmpty(value))
                                    continue;
                                
                                // Extract URLs from value
                                var urls = ExtractDocumentationUrls(value);
                                
                                foreach (var url in urls)
                                {
                                    if (!foundUrls.Contains(url) && IsValidDocumentationUrl(url))
                                    {
                                        foundUrls.Add(url);
                                        
                                        // Extract doc name and domain from URL
                                        var docName = ExtractDocNameFromUrl(url);
                                        var domain = ExtractDomainFromUrl(url);
                                        
                                        if (string.IsNullOrEmpty(domain))
                                            domain = "Other";
                                        
                                        if (!docsByDomain.ContainsKey(domain))
                                        {
                                            docsByDomain[domain] = new List<CursorSettingItem>();
                                        }
                                        
                                        docsByDomain[domain].Add(new CursorSettingItem
                                        {
                                            Id = $"doc_{url.GetHashCode()}",
                                            Name = docName,
                                            Description = $"Documentation URL: {url}\n" +
                                                         $"Source: {key}\n" +
                                                         $"Stored in: globalStorage/state.vscdb",
                                            Category = "Documentation",
                                            SourcePath = globalStorageStateDbPath,
                                            DestinationPath = $"globalStorage/state.vscdb",
                                            IsAvailable = true,
                                            Type = SettingType.Documentation,
                                            ProjectPath = null
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Create groups by domain
                foreach (var kvp in docsByDomain.OrderBy(x => x.Key))
                {
                    var group = new DocumentationGroup
                    {
                        GroupName = kvp.Key,
                        IsExpanded = false
                    };
                    
                    foreach (var doc in kvp.Value.OrderBy(d => d.Name))
                    {
                        group.Documentations.Add(doc);
                    }
                    
                    groups.Add(group);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error discovering documentation groups: {ex.Message}");
            }
            
            return groups;
        }

        /// <summary>
        /// Extracts domain name from URL for grouping
        /// </summary>
        private string ExtractDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host;
                
                // Extract meaningful domain name
                if (host.Contains("developers.facebook.com"))
                    return "Facebook Developers";
                else if (host.Contains("developers.google.com"))
                    return "Google Developers";
                else if (host.Contains("docs.flutter.dev"))
                    return "Flutter";
                else if (host.Contains("docs.cursor.com"))
                    return "Cursor";
                else if (host.Contains("help.gradle.org"))
                    return "Gradle";
                else if (host.Contains("docs."))
                {
                    var domain = host.Replace("docs.", "").Split('.')[0];
                    if (domain.Length > 0)
                    {
                        domain = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(domain);
                    }
                    return domain;
                }
                else if (host.Contains("api") || host.Contains("cursor"))
                {
                    return "Cursor Services";
                }
                
                // Fallback: use host name
                var parts = host.Split('.');
                if (parts.Length >= 2)
                {
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parts[parts.Length - 2]);
                }
                
                return host;
            }
            catch
            {
                return "Other";
            }
        }

        /// <summary>
        /// Extracts documentation URLs from a JSON string or raw text
        /// </summary>
        private List<string> ExtractDocumentationUrls(string value)
        {
            var urls = new List<string>();
            
            try
            {
                // Try to parse as JSON
                var json = JsonConvert.DeserializeObject<dynamic>(value);
                if (json != null)
                {
                    urls.AddRange(ExtractUrlsFromJson(json));
                }
            }
            catch
            {
                // Not JSON, extract URLs from raw text
                var urlPattern = new System.Text.RegularExpressions.Regex(@"https?://[^\s<>""{}|\\^`\[\]]+");
                var matches = urlPattern.Matches(value);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var url = match.Value;
                    // Filter out invalid URLs (ending with single quote, incomplete URLs)
                    if (url.EndsWith("'") || url.EndsWith("://") || url.Length < 10)
                        continue;
                    
                    if (url.Contains("developers.") || url.Contains("docs.") || url.Contains("graph-api"))
                    {
                        urls.Add(url);
                    }
                }
            }
            
            return urls.Distinct().Where(url => IsValidDocumentationUrl(url)).ToList();
        }

        /// <summary>
        /// Validates if a URL is a valid documentation URL
        /// </summary>
        private bool IsValidDocumentationUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            
            // Filter out invalid URLs
            if (url.EndsWith("'") || url.EndsWith("\"") || url.EndsWith("://") || url.Length < 10)
                return false;
            
            // Must be a valid HTTP/HTTPS URL
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;
            
            // Must contain a valid domain (at least one dot after http:// or https://)
            var afterProtocol = url.Substring(url.IndexOf("://") + 3);
            if (!afterProtocol.Contains(".") || afterProtocol.StartsWith(".") || afterProtocol.EndsWith("."))
                return false;
            
            return true;
        }

        /// <summary>
        /// Recursively extracts URLs from JSON object
        /// </summary>
        private List<string> ExtractUrlsFromJson(dynamic json)
        {
            var urls = new List<string>();
            
            if (json == null)
                return urls;
            
            try
            {
                if (json is Newtonsoft.Json.Linq.JObject jobj)
                {
                    foreach (var prop in jobj.Properties())
                    {
                        if (prop.Value is Newtonsoft.Json.Linq.JValue jval && jval.Value is string str)
                        {
                            if (str.Contains("http") && (str.Contains("developers.") || str.Contains("docs.")))
                            {
                                var urlPattern = new System.Text.RegularExpressions.Regex(@"https?://[^\s<>""{}|\\^`\[\]]+");
                                var matches = urlPattern.Matches(str);
                                foreach (System.Text.RegularExpressions.Match match in matches)
                                {
                                    urls.Add(match.Value);
                                }
                            }
                        }
                        else
                        {
                            urls.AddRange(ExtractUrlsFromJson(prop.Value));
                        }
                    }
                }
                else if (json is Newtonsoft.Json.Linq.JArray jarr)
                {
                    foreach (var item in jarr)
                    {
                        urls.AddRange(ExtractUrlsFromJson(item));
                    }
                }
                else if (json is string str)
                {
                    if (str.Contains("http") && (str.Contains("developers.") || str.Contains("docs.")))
                    {
                        var urlPattern = new System.Text.RegularExpressions.Regex(@"https?://[^\s<>""{}|\\^`\[\]]+");
                        var matches = urlPattern.Matches(str);
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            urls.Add(match.Value);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return urls;
        }

        /// <summary>
        /// Extracts a readable name from a documentation URL
        /// </summary>
        private string ExtractDocNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host;
                var path = uri.AbsolutePath.Trim('/');
                
                // Extract meaningful name
                if (host.Contains("developers.facebook.com"))
                {
                    if (path.Contains("graph-api"))
                        return "Facebook Graph API";
                    return "Facebook Developers";
                }
                else if (host.Contains("developers.google.com"))
                {
                    var parts = path.Split('/');
                    if (parts.Length > 0)
                    {
                        var lastPart = parts[parts.Length - 1];
                        if (!string.IsNullOrEmpty(lastPart))
                        {
                            lastPart = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lastPart.Replace("-", " "));
                            return $"Google {lastPart}";
                        }
                    }
                    return "Google Developers";
                }
                else if (host.Contains("docs.flutter.dev"))
                {
                    return "Flutter Documentation";
                }
                else if (host.Contains("docs.cursor.com"))
                {
                    return "Cursor Documentation";
                }
                else if (host.Contains("docs."))
                {
                    var domain = host.Replace("docs.", "").Split('.')[0];
                    if (domain.Length > 0)
                    {
                        domain = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(domain);
                    }
                    return $"{domain} Documentation";
                }
                
                return $"{host} - {path}";
            }
            catch
            {
                return url;
            }
        }
    }
}

