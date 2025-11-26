using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using CursorBackup.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CursorBackup.Services;

namespace CursorBackup.Services
{
    /// <summary>
    /// Service for exporting chat histories to Markdown format
    /// </summary>
    public class ChatExportService
    {
        /// <summary>
        /// Exports selected chat histories to Markdown files organized by project
        /// Only exports actual chat files, not project directories
        /// </summary>
        public ExportResult ExportChatsToMarkdown(List<CursorSettingItem> chatSettings, string exportPath)
        {
            var result = new ExportResult();
            
            if (!Directory.Exists(exportPath))
            {
                Directory.CreateDirectory(exportPath);
            }

            // Filter chat files - include both regular JSON files AND state.vscdb-based chats
            var actualChats = new List<CursorSettingItem>();
            foreach (var chatSetting in chatSettings)
            {
                if (chatSetting.Type != SettingType.ChatHistory || !chatSetting.IsSelected || !chatSetting.IsAvailable)
                    continue;
                
                bool isValidPath = File.Exists(chatSetting.SourcePath) || // Regular chat files
                                  chatSetting.SourcePath.Contains("_combined_prompts", StringComparison.OrdinalIgnoreCase) || // State.vscdb combined prompts
                                  chatSetting.SourcePath.Contains("_thread", StringComparison.OrdinalIgnoreCase) || // State.vscdb threads
                                  chatSetting.SourcePath.Contains("_session", StringComparison.OrdinalIgnoreCase) || // State.vscdb sessions
                                  (chatSetting.Description != null && chatSetting.Description.Contains("IsFromStateDb: true", StringComparison.OrdinalIgnoreCase));
                
                bool isValidFormat = chatSetting.SourcePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                   chatSetting.Name.Contains("Chat:", StringComparison.OrdinalIgnoreCase) ||
                                   chatSetting.SourcePath.Contains("_combined_prompts", StringComparison.OrdinalIgnoreCase) ||
                                   chatSetting.SourcePath.Contains("_thread", StringComparison.OrdinalIgnoreCase) ||
                                   chatSetting.SourcePath.Contains("_session", StringComparison.OrdinalIgnoreCase) ||
                                   (chatSetting.Description != null && chatSetting.Description.Contains("IsFromStateDb: true", StringComparison.OrdinalIgnoreCase));
                
                if (isValidPath && isValidFormat)
                {
                    actualChats.Add(chatSetting);
                }
            }

            if (actualChats.Count == 0)
            {
                result.Errors.Add(new ExportError
                {
                    ChatName = "No chats found",
                    ErrorMessage = "No exportable chat files found. Only actual chat JSON files can be exported, not project folders."
                });
                return result;
            }

            // Group chats by project
            var chatsByProject = actualChats
                .GroupBy(s => s.ProjectPath ?? "Unknown")
                .ToList();

            foreach (var projectGroup in chatsByProject)
            {
                var projectPath = projectGroup.Key;
                var projectName = "Unknown";
                
                // Try to extract project name from path
                if (projectPath != "Unknown")
                {
                    try
                    {
                        if (Uri.TryCreate(projectPath, UriKind.Absolute, out var uri))
                        {
                            projectName = Path.GetFileName(uri.LocalPath) ?? "Unknown";
                        }
                        else
                        {
                            projectName = Path.GetFileName(projectPath) ?? "Unknown";
                        }
                    }
                    catch
                    {
                        projectName = Path.GetFileName(projectPath) ?? "Unknown";
                    }
                }
                
                // Create project directory
                var projectDir = Path.Combine(exportPath, SanitizeFileName(projectName));
                if (!Directory.Exists(projectDir))
                {
                    Directory.CreateDirectory(projectDir);
                }

                foreach (var chatSetting in projectGroup)
                {
                    try
                    {
                        string markdown;
                        
                        // Check if this is a state.vscdb-based chat (combined prompts, threads, or sessions)
                        bool isFromStateDb = chatSetting.SourcePath.Contains("_combined_prompts", StringComparison.OrdinalIgnoreCase) ||
                                            chatSetting.SourcePath.Contains("_thread", StringComparison.OrdinalIgnoreCase) ||
                                            chatSetting.SourcePath.Contains("_session", StringComparison.OrdinalIgnoreCase) ||
                                            chatSetting.Description.Contains("IsFromStateDb: true", StringComparison.OrdinalIgnoreCase);
                        
                        if (chatSetting.Type == SettingType.ChatHistory && isFromStateDb)
                        {
                            // This is a state.vscdb-based chat - need to read from state.vscdb
                            markdown = ConvertStateDbPromptsToMarkdown(chatSetting);
                        }
                        else if (File.Exists(chatSetting.SourcePath))
                        {
                            // Regular chat file
                            markdown = ConvertChatToMarkdown(chatSetting.SourcePath);
                        }
                        else
                        {
                            // File doesn't exist, skip
                            result.Errors.Add(new ExportError
                            {
                                ChatName = chatSetting.Name,
                                ErrorMessage = $"Chat file not found: {chatSetting.SourcePath}"
                            });
                            continue;
                        }
                        
                        string fileName = Path.GetFileNameWithoutExtension(chatSetting.SourcePath);
                        if (string.IsNullOrEmpty(fileName))
                        {
                            var name = chatSetting.Name ?? "Unknown";
                            name = name.Replace("Chat: ", string.Empty);
                            fileName = name.Replace(" - ", "_");
                        }
                        var markdownPath = Path.Combine(projectDir, $"{SanitizeFileName(fileName)}.md");
                        
                        File.WriteAllText(markdownPath, markdown, Encoding.UTF8);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(new ExportError
                        {
                            ChatName = chatSetting.Name,
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }

            return result;
        }

        private string ConvertChatToMarkdown(string chatFilePath)
        {
            try
            {
                var jsonContent = File.ReadAllText(chatFilePath);
                var json = JObject.Parse(jsonContent);
                
                var sb = new StringBuilder();
                sb.AppendLine($"# Chat Export: {Path.GetFileName(chatFilePath)}");
                sb.AppendLine();
                
                // Add metadata
                if (json["sessionId"] != null)
                {
                    sb.AppendLine($"**Session ID:** {json["sessionId"]}");
                }
                if (json["creationDate"] != null)
                {
                    var creationDate = json["creationDate"].ToString();
                    // Try to convert timestamp to readable date
                    if (long.TryParse(creationDate, out var timestamp))
                    {
                        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                        sb.AppendLine($"**Created:** {date:yyyy-MM-dd HH:mm:ss} (timestamp: {creationDate})");
                    }
                    else
                    {
                        sb.AppendLine($"**Created:** {creationDate}");
                    }
                }
                sb.AppendLine($"**Export Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"**Source File:** {chatFilePath}");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                int messageCount = 0;

                // Format 1: requests array (most common in Cursor)
                if (json["requests"] != null)
                {
                    var requests = json["requests"] as JArray;
                    if (requests != null && requests.Count > 0)
                    {
                        sb.AppendLine($"## Conversation ({requests.Count} messages)");
                        sb.AppendLine();
                        
                        foreach (var request in requests)
                        {
                            JToken? message = null;
                            string? role = "USER"; // Default to USER for requests
                            string? content = null;
                            string? timestamp = null;

                            // Get timestamp from request
                            if (request["timestamp"] != null)
                            {
                                timestamp = request["timestamp"]?.ToString();
                            }

                            // Try different message structures
                            if (request["message"] != null)
                            {
                                message = request["message"];
                                
                                // Cursor format: message.text or message.parts
                                if (message["text"] != null)
                                {
                                    content = message["text"]?.ToString() ?? "";
                                }
                                else if (message["parts"] != null)
                                {
                                    var parts = message["parts"] as JArray;
                                    if (parts != null && parts.Count > 0)
                                    {
                                        var contentParts = new List<string>();
                                        foreach (var part in parts)
                                        {
                                            if (part["text"] != null)
                                            {
                                                contentParts.Add(part["text"]?.ToString() ?? "");
                                            }
                                            else if (part.Type == JTokenType.String)
                                            {
                                                contentParts.Add(part.ToString());
                                            }
                                        }
                                        content = string.Join("\n", contentParts);
                                    }
                                }
                                else if (message["content"] != null)
                                {
                                    content = message["content"]?.ToString() ?? "";
                                }
                                
                                // Try to get role
                                if (message["role"] != null)
                                {
                                    role = message["role"]?.ToString() ?? "USER";
                                }
                            }
                            else if (request["role"] != null)
                            {
                                // Direct message format
                                role = request["role"]?.ToString() ?? "USER";
                                content = request["content"]?.ToString() ?? request["text"]?.ToString() ?? "";
                            }
                            else if (request["text"] != null)
                            {
                                // Direct text format
                                content = request["text"]?.ToString() ?? "";
                            }

                            // Also check response array for assistant messages
                            // CRITICAL: response.message can be STRING or OBJECT!
                            if (request["response"] != null)
                            {
                                var responses = request["response"] as JArray;
                                if (responses != null && responses.Count > 0)
                                {
                                    foreach (var response in responses)
                                    {
                                        string? respContent = null;
                                        
                                        // Format 1: response.message (MOST COMMON - can be string OR object)
                                        if (response["message"] != null)
                                        {
                                            var respMessage = response["message"];
                                            
                                            // Check if message is a STRING (direct text) - THIS IS THE COMMON CASE!
                                            if (respMessage.Type == JTokenType.String)
                                            {
                                                respContent = respMessage.ToString();
                                            }
                                            // Or if it's an OBJECT with text/parts
                                            else if (respMessage.Type == JTokenType.Object)
                                            {
                                                if (respMessage["text"] != null)
                                                {
                                                    respContent = respMessage["text"]?.ToString() ?? "";
                                                }
                                                else if (respMessage["parts"] != null)
                                                {
                                                    var parts = respMessage["parts"] as JArray;
                                                    if (parts != null && parts.Count > 0)
                                                    {
                                                        var contentParts = new List<string>();
                                                        foreach (var part in parts)
                                                        {
                                                            if (part["text"] != null)
                                                            {
                                                                contentParts.Add(part["text"]?.ToString() ?? "");
                                                            }
                                                            else if (part.Type == JTokenType.String)
                                                            {
                                                                contentParts.Add(part.ToString());
                                                            }
                                                        }
                                                        respContent = string.Join("\n", contentParts);
                                                    }
                                                }
                                                else if (respMessage["content"] != null)
                                                {
                                                    respContent = respMessage["content"]?.ToString() ?? "";
                                                }
                                            }
                                        }
                                        // Format 2: response.value (alternative format - some responses use this)
                                        else if (response["value"] != null)
                                        {
                                            respContent = response["value"]?.ToString() ?? "";
                                        }
                                        // Format 3: response.text (alternative)
                                        else if (response["text"] != null)
                                        {
                                            respContent = response["text"]?.ToString() ?? "";
                                        }
                                        // Format 4: response.parts
                                        else if (response["parts"] != null)
                                        {
                                            var parts = response["parts"] as JArray;
                                            if (parts != null && parts.Count > 0)
                                            {
                                                var contentParts = new List<string>();
                                                foreach (var part in parts)
                                                {
                                                    if (part["text"] != null)
                                                    {
                                                        contentParts.Add(part["text"]?.ToString() ?? "");
                                                    }
                                                    else if (part.Type == JTokenType.String)
                                                    {
                                                        contentParts.Add(part.ToString());
                                                    }
                                                }
                                                respContent = string.Join("\n", contentParts);
                                            }
                                        }
                                        // Format 5: response.content
                                        else if (response["content"] != null)
                                        {
                                            respContent = response["content"]?.ToString() ?? "";
                                        }
                                        // Format 6: direct string
                                        else if (response.Type == JTokenType.String)
                                        {
                                            respContent = response.ToString();
                                        }

                                        // Only add if we have actual content (skip empty responses)
                                        if (!string.IsNullOrEmpty(respContent) && respContent.Trim().Length > 0)
                                        {
                                            messageCount++;
                                            sb.AppendLine($"### ASSISTANT");
                                            
                                            if (!string.IsNullOrEmpty(timestamp))
                                            {
                                                if (long.TryParse(timestamp, out var ts))
                                                {
                                                    var date = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
                                                    sb.AppendLine($"*{date:yyyy-MM-dd HH:mm:ss}*");
                                                }
                                                else
                                                {
                                                    sb.AppendLine($"*{timestamp}*");
                                                }
                                            }
                                            
                                            sb.AppendLine();
                                            sb.AppendLine(respContent);
                                            sb.AppendLine();
                                            sb.AppendLine("---");
                                            sb.AppendLine();
                                        }
                                    }
                                }
                            }

                            // Add user message if we have content
                            if (!string.IsNullOrEmpty(content))
                            {
                                messageCount++;
                                sb.AppendLine($"### {role.ToUpperInvariant()}");
                                
                                if (!string.IsNullOrEmpty(timestamp))
                                {
                                    if (long.TryParse(timestamp, out var ts))
                                    {
                                        var date = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
                                        sb.AppendLine($"*{date:yyyy-MM-dd HH:mm:ss}*");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"*{timestamp}*");
                                    }
                                }
                                
                                sb.AppendLine();
                                sb.AppendLine(content);
                                sb.AppendLine();
                                sb.AppendLine("---");
                                sb.AppendLine();
                            }
                        }
                    }
                }
                // Format 2: messages array
                else if (json["messages"] != null)
                {
                    var messages = json["messages"] as JArray;
                    if (messages != null && messages.Count > 0)
                    {
                        sb.AppendLine($"## Conversation ({messages.Count} messages)");
                        sb.AppendLine();
                        
                        foreach (var message in messages)
                        {
                            var role = message["role"]?.ToString() ?? "unknown";
                            var content = message["content"]?.ToString() ?? "";
                            var timestamp = message["timestamp"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(content))
                            {
                                messageCount++;
                                sb.AppendLine($"### {role.ToUpperInvariant()}");
                                if (!string.IsNullOrEmpty(timestamp))
                                {
                                    if (long.TryParse(timestamp, out var ts))
                                    {
                                        var date = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
                                        sb.AppendLine($"*{date:yyyy-MM-dd HH:mm:ss}*");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"*{timestamp}*");
                                    }
                                }
                                sb.AppendLine();
                                sb.AppendLine(content);
                                sb.AppendLine();
                                sb.AppendLine("---");
                                sb.AppendLine();
                            }
                        }
                    }
                }
                // Format 3: conversations array
                else if (json["conversations"] != null)
                {
                    var conversations = json["conversations"] as JArray;
                    if (conversations != null)
                    {
                        foreach (var conv in conversations)
                        {
                            var title = conv["title"]?.ToString() ?? "Untitled Conversation";
                            sb.AppendLine($"## {title}");
                            sb.AppendLine();

                            var messages = conv["messages"] as JArray;
                            if (messages != null)
                            {
                                foreach (var message in messages)
                                {
                                    var role = message["role"]?.ToString() ?? "unknown";
                                    var content = message["content"]?.ToString() ?? "";
                                    
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        messageCount++;
                                        sb.AppendLine($"### {role.ToUpperInvariant()}");
                                        sb.AppendLine();
                                        sb.AppendLine(content);
                                        sb.AppendLine();
                                    }
                                }
                            }
                            sb.AppendLine("---");
                            sb.AppendLine();
                        }
                    }
                }

                // If no messages found, show raw structure
                if (messageCount == 0)
                {
                    sb.AppendLine("## Chat Structure (No messages found in standard format)");
                    sb.AppendLine();
                    sb.AppendLine("```json");
                    // Show first 2000 chars of JSON
                    var jsonStr = json.ToString(Formatting.Indented);
                    if (jsonStr.Length > 2000)
                    {
                        sb.AppendLine(jsonStr.Substring(0, 2000));
                        sb.AppendLine("... (truncated)");
                    }
                    else
                    {
                        sb.AppendLine(jsonStr);
                    }
                    sb.AppendLine("```");
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Total Messages Exported:** {messageCount}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                // If JSON parsing fails, return raw content with error info
                var content = File.ReadAllText(chatFilePath);
                var sb = new StringBuilder();
                sb.AppendLine($"# Chat Export: {Path.GetFileName(chatFilePath)}");
                sb.AppendLine();
                sb.AppendLine($"**Export Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"**Error:** {ex.Message}");
                sb.AppendLine();
                sb.AppendLine("## Raw Content");
                sb.AppendLine();
                sb.AppendLine("```");
                // Show first 5000 chars
                if (content.Length > 5000)
                {
                    sb.AppendLine(content.Substring(0, 5000));
                    sb.AppendLine("... (truncated)");
                }
                else
                {
                    sb.AppendLine(content);
                }
                sb.AppendLine("```");
                return sb.ToString();
            }
        }

        private void ExportChatDirectory(string chatDirPath, string projectDir, ExportResult result)
        {
            try
            {
                var dirName = Path.GetFileName(chatDirPath);
                var exportDir = Path.Combine(projectDir, SanitizeFileName(dirName));
                
                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                var files = Directory.GetFiles(chatDirPath, "*.json", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var markdown = ConvertChatToMarkdown(file);
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        var markdownPath = Path.Combine(exportDir, $"{SanitizeFileName(fileName)}.md");
                        
                        File.WriteAllText(markdownPath, markdown, Encoding.UTF8);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(new ExportError
                        {
                            ChatName = Path.GetFileName(file),
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ExportError
                {
                    ChatName = Path.GetFileName(chatDirPath),
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// Converts aiService.prompts from state.vscdb to markdown format
        /// Handles combined prompts, threads, and sessions
        /// </summary>
        private string ConvertStateDbPromptsToMarkdown(CursorSettingItem chatSetting)
        {
            var sb = new StringBuilder();
            
            // Extract project name and type from chat name
            var chatName = chatSetting.Name.Replace("Chat: ", "").Trim();
            var projectName = chatName;
            var chatType = "Combined Prompts";
            int? threadNumber = null;
            string? sessionUuid = null;
            
            // Determine chat type from source path
            if (chatSetting.SourcePath.Contains("_thread_", StringComparison.OrdinalIgnoreCase))
            {
                // Extract thread number from path (e.g., "project_thread_1.json")
                var fileName = Path.GetFileNameWithoutExtension(chatSetting.SourcePath);
                var threadMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"_thread_(\d+)");
                if (threadMatch.Success && int.TryParse(threadMatch.Groups[1].Value, out var threadNum))
                {
                    threadNumber = threadNum;
                    chatType = $"Thread {threadNum}";
                    projectName = chatName.Replace($" - Thread {threadNum}", "").Replace($" ({chatName.Split('(').LastOrDefault()?.TrimEnd(')')} prompts)", "").Trim();
                }
            }
            else if (chatSetting.SourcePath.Contains("_session_", StringComparison.OrdinalIgnoreCase))
            {
                // Extract session UUID from path
                var fileName = Path.GetFileNameWithoutExtension(chatSetting.SourcePath);
                var sessionMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"_session_([a-f0-9-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (sessionMatch.Success)
                {
                    sessionUuid = sessionMatch.Groups[1].Value;
                    chatType = $"Session {sessionUuid.Substring(0, 8)}...";
                    projectName = chatName.Replace($" - Session {sessionUuid.Substring(0, 8)}...", "").Trim();
                }
            }
            else
            {
                // Combined prompts
                projectName = chatName.Replace(" - Combined Prompts", "").Replace(" - All Prompts Combined", "").Trim();
            }
            
            // Extract state.vscdb path from Description or source path
            string? stateDbPath = null;
            
            // First try to get from Description (stored by ExportChats method)
            if (chatSetting.Description != null && chatSetting.Description.Contains("StateDbPath:", StringComparison.OrdinalIgnoreCase))
            {
                var lines = chatSetting.Description.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("StateDbPath:", StringComparison.OrdinalIgnoreCase))
                    {
                        stateDbPath = line.Substring("StateDbPath:".Length).Trim();
                        break;
                    }
                }
            }
            
            // If not found in Description, try to extract from source path (workspace folder)
            if (string.IsNullOrEmpty(stateDbPath))
            {
                var workspaceFolder = Path.GetDirectoryName(chatSetting.SourcePath);
                if (!string.IsNullOrEmpty(workspaceFolder))
                {
                    stateDbPath = Path.Combine(workspaceFolder, "state.vscdb");
                }
            }
            
            if (string.IsNullOrEmpty(stateDbPath) || !File.Exists(stateDbPath))
            {
                throw new FileNotFoundException($"State database not found: {stateDbPath ?? "unknown"}");
            }
            
            try
            {
                // Query state.vscdb for aiService.prompts AND aiService.generations
                using (var connection = new SQLiteConnection($"Data Source={stateDbPath};Mode=ReadOnly"))
                {
                    connection.Open();
                    
                    // Get prompts
                    List<dynamic>? prompts = null;
                    using (var command = new SQLiteCommand("SELECT value FROM ItemTable WHERE key = 'aiService.prompts'", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                throw new InvalidOperationException("aiService.prompts not found in state.vscdb");
                            }
                            
                            var promptsJson = reader.GetString(0);
                            if (string.IsNullOrEmpty(promptsJson))
                            {
                                throw new InvalidOperationException("aiService.prompts is empty");
                            }
                            
                            prompts = JsonConvert.DeserializeObject<List<dynamic>>(promptsJson);
                            if (prompts == null || prompts.Count == 0)
                            {
                                throw new InvalidOperationException("No prompts found in aiService.prompts");
                            }
                        }
                    }
                    
                    // Get generations (assistant responses)
                    List<dynamic>? generations = null;
                    using (var command = new SQLiteCommand("SELECT value FROM ItemTable WHERE key = 'aiService.generations'", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var generationsJson = reader.GetString(0);
                                if (!string.IsNullOrEmpty(generationsJson))
                                {
                                    generations = JsonConvert.DeserializeObject<List<dynamic>>(generationsJson);
                                }
                            }
                        }
                    }
                    
                    // Group prompts into threads if needed
                    List<dynamic> promptsToExport;
                    int threadStartIndex = 0;
                    if (threadNumber.HasValue)
                    {
                        // Export only the specified thread
                        var threads = GroupPromptsIntoThreads(prompts);
                        if (threadNumber.Value < 1 || threadNumber.Value > threads.Count)
                        {
                            throw new InvalidOperationException($"Thread {threadNumber.Value} not found. Available threads: 1-{threads.Count}");
                        }
                        promptsToExport = threads[threadNumber.Value - 1];
                        // Calculate start index for this thread
                        for (int i = 0; i < threadNumber.Value - 1; i++)
                        {
                            threadStartIndex += threads[i].Count;
                        }
                    }
                    else if (!string.IsNullOrEmpty(sessionUuid))
                    {
                        // For sessions, we don't have direct mapping yet, so export all prompts
                        // TODO: In the future, we might need to map session UUIDs to specific prompts
                        promptsToExport = prompts;
                    }
                    else
                    {
                        // Export all prompts (combined)
                        promptsToExport = prompts;
                    }
                    
                    // Match generations to prompts
                    var generationMap = new Dictionary<int, dynamic>();
                    if (generations != null && generations.Count > 0)
                    {
                        for (int i = 0; i < promptsToExport.Count; i++)
                        {
                            int absoluteIndex = threadStartIndex + i;
                            
                            // Try to find matching generation by timestamp proximity
                            var promptObj = JObject.FromObject(promptsToExport[i]);
                            var promptTime = promptObj["unixMs"]?.Value<long>() ?? promptObj["timestamp"]?.Value<long>() ?? 0;
                            
                            dynamic? bestMatch = null;
                            long bestTimeDiff = long.MaxValue;
                            
                            foreach (var gen in generations)
                            {
                                var genObj = JObject.FromObject(gen);
                                var genTime = genObj["unixMs"]?.Value<long>() ?? genObj["timestamp"]?.Value<long>() ?? 0;
                                
                                if (genTime > 0 && promptTime > 0)
                                {
                                    var timeDiff = Math.Abs(genTime - promptTime);
                                    if (timeDiff < bestTimeDiff)
                                    {
                                        bestTimeDiff = timeDiff;
                                        bestMatch = gen;
                                    }
                                }
                            }
                            
                            // If no time match, use order-based matching
                            if (bestMatch == null && absoluteIndex < generations.Count)
                            {
                                bestMatch = generations[absoluteIndex];
                            }
                            
                            if (bestMatch != null)
                            {
                                generationMap[i] = bestMatch;
                            }
                        }
                    }
                    
                    // Build markdown header
                    sb.AppendLine($"# Chat: {projectName} - {chatType}");
                    sb.AppendLine();
                    sb.AppendLine($"**Source:** aiService.prompts + aiService.generations from state.vscdb");
                    sb.AppendLine($"**Export Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"**Total Prompts:** {promptsToExport.Count}");
                    if (generations != null)
                    {
                        sb.AppendLine($"**Total Generations:** {generations.Count}");
                        sb.AppendLine($"**Matched Responses:** {generationMap.Count}");
                    }
                    if (threadNumber.HasValue)
                    {
                        sb.AppendLine($"**Thread Number:** {threadNumber.Value}");
                    }
                    if (!string.IsNullOrEmpty(sessionUuid))
                    {
                        sb.AppendLine($"**Session UUID:** {sessionUuid}");
                    }
                    sb.AppendLine($"**State DB Path:** {stateDbPath}");
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                    
                    // Export prompts with matching assistant responses
                    for (int idx = 0; idx < promptsToExport.Count; idx++)
                    {
                        var prompt = promptsToExport[idx];
                        var promptObj = JObject.FromObject(prompt);
                        
                        // Extract prompt text
                        var promptText = promptObj["text"]?.ToString() ?? 
                                        promptObj["prompt"]?.ToString() ?? 
                                        promptObj["message"]?.ToString() ?? 
                                        promptObj["content"]?.ToString();
                        
                        var commandType = promptObj["commandType"]?.ToString();
                        var promptTime = promptObj["unixMs"]?.Value<long>() ?? promptObj["timestamp"]?.Value<long>() ?? 0;
                        
                        if (!string.IsNullOrEmpty(promptText))
                        {
                            sb.AppendLine($"## USER (Prompt #{idx + 1})");
                            sb.AppendLine();
                            
                            if (!string.IsNullOrEmpty(commandType) && commandType != "unknown")
                            {
                                sb.AppendLine($"**Command Type:** {commandType}");
                                sb.AppendLine();
                            }
                            
                            if (promptTime > 0)
                            {
                                try
                                {
                                    var timestamp = promptTime < 1000000000000 ? promptTime * 1000 : promptTime;
                                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                                    sb.AppendLine($"**Time:** {date:yyyy-MM-dd HH:mm:ss}");
                                    sb.AppendLine();
                                }
                                catch { }
                            }
                            
                            sb.AppendLine(promptText);
                            sb.AppendLine();
                            sb.AppendLine("---");
                            sb.AppendLine();
                        }
                        
                        // Add matching assistant response if available
                        string? genText = null;
                        if (generationMap.TryGetValue(idx, out var generation))
                        {
                            var genObj = JObject.FromObject(generation);
                            genText = genObj["textDescription"]?.ToString() ?? 
                                     genObj["text"]?.ToString() ?? 
                                     genObj["content"]?.ToString() ?? "";
                            
                            // Check if this is actually the prompt text (common issue)
                            if (!string.IsNullOrEmpty(genText) && !string.IsNullOrEmpty(promptText))
                            {
                                if (genText.Trim().Equals(promptText.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    genText = null; // This is the prompt, not the response
                                }
                            }
                        }
                        
                        // NEW: ALWAYS try to get ASSISTANT response from cursorDiskKV table (bubbleId keys)
                        // This ensures we get responses even for prompts without matched generations
                        try
                        {
                            var bubbleResponse = GetAssistantResponseFromBubbles(stateDbPath, promptTime, absoluteIndex: threadStartIndex + idx, promptText: promptText);
                            if (!string.IsNullOrEmpty(bubbleResponse))
                            {
                                // If we already have genText, prefer bubble response if it's longer/more complete
                                if (string.IsNullOrEmpty(genText) || bubbleResponse.Length > genText.Length)
                                {
                                    genText = bubbleResponse;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug($"[ChatExportService] ConvertStateDbPromptsToMarkdown: Error getting bubble response for prompt #{idx + 1}: {ex.Message}");
                        }
                        
                        if (!string.IsNullOrEmpty(genText))
                        {
                            sb.AppendLine($"## ASSISTANT (Response #{idx + 1})");
                            sb.AppendLine();
                            
                            // Try to get timestamp from generation if available
                            long genTime = 0;
                            if (generationMap.TryGetValue(idx, out var gen))
                            {
                                var genObj = JObject.FromObject(gen);
                                genTime = genObj["unixMs"]?.Value<long>() ?? genObj["timestamp"]?.Value<long>() ?? 0;
                            }
                            
                            if (genTime == 0 && promptTime > 0)
                            {
                                genTime = promptTime; // Use prompt time as fallback
                            }
                            
                            if (genTime > 0)
                            {
                                try
                                {
                                    var timestamp = genTime < 1000000000000 ? genTime * 1000 : genTime;
                                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                                    sb.AppendLine($"**Time:** {date:yyyy-MM-dd HH:mm:ss}");
                                    sb.AppendLine();
                                }
                                catch { }
                            }
                            
                            sb.AppendLine(genText);
                            sb.AppendLine();
                            sb.AppendLine("---");
                            sb.AppendLine();
                        }
                    }
                    
                    sb.AppendLine();
                    sb.AppendLine($"**Total Messages Exported:** {promptsToExport.Count} prompts, {generationMap.Count} assistant responses");
                }
            }
            catch (Exception ex)
            {
                sb.Clear();
                sb.AppendLine($"# Chat Export Error: {projectName}");
                sb.AppendLine();
                sb.AppendLine($"**Error:** {ex.Message}");
                sb.AppendLine($"**State DB Path:** {stateDbPath}");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(ex.ToString());
                sb.AppendLine("```");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Groups prompts into threads based on heuristics (same logic as CursorDataService)
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
                    if (prompt is JObject jobj)
                    {
                        text = jobj["text"]?.ToString();
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

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return sanitized;
        }
        
        /// <summary>
        /// Get ASSISTANT response from cursorDiskKV table using bubbleId keys
        /// Based on GitHub tools: cursor-view, cursor-chat-export
        /// FIXED: Groups bubbles by session and matches correctly (USER bubble -> next ASSISTANT bubble)
        /// </summary>
        private string? GetAssistantResponseFromBubbles(string stateDbPath, long promptTime, int absoluteIndex, string? promptText = null)
        {
            Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Looking for response (promptTime={promptTime}, index={absoluteIndex}, textLength={promptText?.Length ?? 0})");
            
            // Try global storage first (where bubbles are stored)
            var globalStateDb = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cursor", "User", "globalStorage", "state.vscdb");
            
            if (!File.Exists(globalStateDb))
            {
                Logger.LogWarning($"[ChatExportService] GetAssistantResponseFromBubbles: Global state DB not found at {globalStateDb}");
                return null;
            }
            
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={globalStateDb};Mode=ReadOnly"))
                {
                    connection.Open();
                    
                    // Check if cursorDiskKV table exists
                    using (var checkTable = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='cursorDiskKV'", connection))
                    {
                        using (var reader = checkTable.ExecuteReader())
                        {
                            if (!reader.Read())
                                return null; // Table doesn't exist
                        }
                    }
                    
                    // Get all bubbles grouped by session (key format: bubbleId:sessionId:bubbleId)
                    var bubbleSessions = new Dictionary<string, List<(string key, JObject bubble, long? createdAt, int type)>>();
                    
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
                                    
                                    // Extract session ID from key (format: bubbleId:sessionId:bubbleId)
                                    var keyParts = key.Split(':');
                                    if (keyParts.Length < 2)
                                        continue;
                                    
                                    var sessionId = keyParts[1];
                                    
                                    if (!bubbleSessions.ContainsKey(sessionId))
                                        bubbleSessions[sessionId] = new List<(string, JObject, long?, int)>();
                                    
                                    var bubbleType = bubble["type"]?.Value<int>() ?? 0;
                                    var text = bubble["text"]?.ToString()?.Trim();
                                    
                                    if (string.IsNullOrEmpty(text))
                                        continue;
                                    
                                    // Get timestamp
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
                                    
                                    bubbleSessions[sessionId].Add((key, bubble, createdAt, bubbleType));
                                }
                                catch { }
                            }
                        }
                    }
                    
                    // Sort bubbles within each session by timestamp
                    foreach (var sessionId in bubbleSessions.Keys.ToList())
                    {
                        bubbleSessions[sessionId] = bubbleSessions[sessionId]
                            .OrderBy(b => b.createdAt ?? 0)
                            .ToList();
                    }
                    
                    // Try to match prompt with bubbles
                    // Strategy: Find USER bubble that matches prompt text, then get next ASSISTANT bubble
                    // IMPROVED: Try ALL sessions, not just the first match
                    string? bestResponse = null;
                    
                    foreach (var kvp in bubbleSessions)
                    {
                        var sessionId = kvp.Key;
                        var session = kvp.Value;
                        
                        // Find matching USER bubble by text similarity
                        int? matchingUserIndex = null;
                        
                        if (!string.IsNullOrEmpty(promptText))
                        {
                            for (int i = 0; i < session.Count; i++)
                            {
                                var bubble = session[i];
                                if (bubble.type == 1) // USER bubble
                                {
                                    var bubbleText = bubble.bubble["text"]?.ToString()?.Trim() ?? "";
                                    
                                    // Check if prompt text matches bubble text - improved matching for old chats
                                    if (bubbleText.Length > 0 && promptText.Length > 0)
                                    {
                                        // Normalize whitespace
                                        var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                        var bubbleNormalized = bubbleText.Replace("\r", "").Replace("\n", " ").Trim();
                                        
                                        var promptStart = promptNormalized.Length > 200 ? promptNormalized.Substring(0, 200) : promptNormalized;
                                        var bubbleStart = bubbleNormalized.Length > 200 ? bubbleNormalized.Substring(0, 200) : bubbleNormalized;
                                        
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
                                            matchingUserIndex = i;
                                            Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Matched prompt to USER bubble at index {i} (session: {sessionId.Substring(0, Math.Min(8, sessionId.Length))}...)");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        
                        // If we found a matching USER bubble, get ALL consecutive ASSISTANT bubbles
                        if (matchingUserIndex.HasValue)
                        {
                            var responseParts = new List<string>();
                            
                            for (int i = matchingUserIndex.Value + 1; i < session.Count; i++)
                            {
                                var bubble = session[i];
                                
                                // Stop if we hit another USER bubble (next conversation)
                                if (bubble.type == 1)
                                    break;
                                
                                // Collect ASSISTANT bubbles
                                if (bubble.type != 1) // ASSISTANT bubble
                                {
                                    var text = bubble.bubble["text"]?.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        // CRITICAL: Verify this is NOT the same as the prompt text
                                        var promptNormalized = (promptText ?? "").Replace("\r", "").Replace("\n", " ").Trim();
                                        var responseNormalized = text.Replace("\r", "").Replace("\n", " ").Trim();
                                        
                                        // If response matches prompt, skip it (it's not a real response)
                                        if (promptNormalized.Length > 0 && responseNormalized.Length > 0)
                                        {
                                            var promptStart = promptNormalized.Length > 100 ? promptNormalized.Substring(0, 100) : promptNormalized;
                                            var responseStart = responseNormalized.Length > 100 ? responseNormalized.Substring(0, 100) : responseNormalized;
                                            
                                            if (promptStart.Equals(responseStart, StringComparison.OrdinalIgnoreCase) ||
                                                (promptStart.Length > 50 && responseStart.Length > 50 && 
                                                 (promptStart.Contains(responseStart, StringComparison.OrdinalIgnoreCase) ||
                                                  responseStart.Contains(promptStart, StringComparison.OrdinalIgnoreCase))))
                                            {
                                                Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Skipping response that matches prompt text");
                                                continue; // Skip this, it's the prompt, not a response
                                            }
                                        }
                                        
                                        responseParts.Add(text);
                                        Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Collected ASSISTANT response part (length: {text.Length}, total parts: {responseParts.Count})");
                                    }
                                }
                            }
                            
                            // Combine all ASSISTANT response parts
                            if (responseParts.Count > 0)
                            {
                                var fullResponse = string.Join("\n\n", responseParts);
                                
                                // Keep first response found (we can improve this later to track best similarity)
                                if (string.IsNullOrEmpty(bestResponse))
                                {
                                    bestResponse = fullResponse;
                                    Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Found response (length: {fullResponse.Length})");
                                }
                            }
                        }
                    }
                    
                    // Return best response found across all sessions
                    if (!string.IsNullOrEmpty(bestResponse))
                    {
                        Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Returning best ASSISTANT response (length: {bestResponse.Length})");
                        return bestResponse;
                    }
                    
                    // Fallback: if no text match, try timestamp-based matching
                    if (promptTime > 0 && string.IsNullOrEmpty(bestResponse))
                    {
                        string? bestTimestampResponse = null;
                        long bestTimeDiff = long.MaxValue;
                        
                        foreach (var kvp in bubbleSessions)
                        {
                            var session = kvp.Value;
                            
                            // Find USER bubble closest to prompt time
                            var userBubbles = session.Where(b => b.type == 1 && b.createdAt.HasValue)
                                .OrderBy(b => Math.Abs(b.createdAt!.Value - promptTime))
                                .ToList();
                            
                            if (userBubbles.Count > 0)
                            {
                                var bestUserBubble = userBubbles[0];
                                var timeDiff = Math.Abs(bestUserBubble.createdAt!.Value - promptTime);
                                
                                // Only use if time difference is reasonable (within 1 hour)
                                if (timeDiff < 3600000) // 1 hour in milliseconds
                                {
                                    var userIndex = session.IndexOf(bestUserBubble);
                                    
                                    // Get ALL consecutive ASSISTANT bubbles after this USER bubble
                                    var responseParts = new List<string>();
                                    for (int i = userIndex + 1; i < session.Count; i++)
                                    {
                                        var bubble = session[i];
                                        
                                        // Stop if we hit another USER bubble
                                        if (bubble.type == 1)
                                            break;
                                        
                                        if (bubble.type != 1) // ASSISTANT
                                        {
                                            var text = bubble.bubble["text"]?.ToString()?.Trim();
                                            if (!string.IsNullOrEmpty(text))
                                            {
                                                responseParts.Add(text);
                                            }
                                        }
                                    }
                                    
                                    if (responseParts.Count > 0)
                                    {
                                        var timestampResponse = string.Join("\n\n", responseParts);
                                        
                                        // Keep best timestamp-based response
                                        if (timeDiff < bestTimeDiff)
                                        {
                                            bestTimestampResponse = timestampResponse;
                                            bestTimeDiff = timeDiff;
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(bestTimestampResponse))
                        {
                            Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: Returning timestamp-based response (time diff: {bestTimeDiff}ms)");
                            return bestTimestampResponse;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ChatExportService] GetAssistantResponseFromBubbles: Error getting response from bubbles");
            }
            
            Logger.LogDebug($"[ChatExportService] GetAssistantResponseFromBubbles: No response found");
            return null;
        }
    }

    public class ExportResult
    {
        public int SuccessCount { get; set; }
        public List<ExportError> Errors { get; } = new();
    }

    public class ExportError
    {
        public string ChatName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

