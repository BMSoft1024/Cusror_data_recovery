using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Data.SQLite;
using CursorBackup.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CursorBackup.Services;

namespace CursorBackup.Dialogs
{
    public partial class SettingPreviewDialog : Window, INotifyPropertyChanged
    {
        private readonly CursorSettingItem _setting;
        private string _settingName = string.Empty;
        private string _description = string.Empty;
        private string _sourcePath = string.Empty;
        private string _destinationPath = string.Empty;
        private object? _previewContent;

        public string SettingName 
        { 
            get => _settingName;
            set
            {
                _settingName = value;
                OnPropertyChanged();
            }
        }

        public string Description 
        { 
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string SourcePath 
        { 
            get => _sourcePath;
            set
            {
                _sourcePath = value;
                OnPropertyChanged();
            }
        }

        public string DestinationPath 
        { 
            get => _destinationPath;
            set
            {
                _destinationPath = value;
                OnPropertyChanged();
            }
        }

        public object? PreviewContent 
        { 
            get => _previewContent;
            set
            {
                _previewContent = value;
                OnPropertyChanged();
            }
        }

        public SettingPreviewDialog(CursorSettingItem setting)
        {
            _setting = setting;
            InitializeComponent();
            DataContext = this;
            LoadPreviewAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void LoadPreviewAsync()
        {
            SettingName = _setting.Name;
            Description = _setting.Description;
            SourcePath = _setting.SourcePath;
            DestinationPath = _setting.DestinationPath;

            // Show loading indicator
            PreviewContent = new TextBlock
            {
                Text = "Loading in progress...",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            try
            {
                // Load content asynchronously - but create UI elements on main thread
                object? content = null;
                
                // For chat history, we need to do heavy work async but create UI on main thread
                if (_setting.Type == SettingType.ChatHistory)
                {
                    // Load chat preview - this does heavy DB work
                    // Use Dispatcher to ensure UI elements are created on main thread
                    var task = System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                    {
                        return LoadChatPreview(_setting.SourcePath);
                    }, System.Windows.Threading.DispatcherPriority.Background);
                    
                    content = await task;
                }
                else
                {
                    // For other types, load synchronously (they're fast)
                    switch (_setting.Type)
                    {
                        case SettingType.GlobalSettings:
                        case SettingType.Keybindings:
                        case SettingType.Rules:
                            content = LoadJsonPreview(_setting.SourcePath);
                            break;
                        case SettingType.StateDatabase:
                            if (File.Exists(_setting.SourcePath))
                            {
                                content = LoadStateDatabasePreview(_setting.SourcePath);
                            }
                            else
                            {
                                content = new TextBlock
                                {
                                    Text = "State database file not found",
                                    Foreground = System.Windows.Media.Brushes.Red,
                                    TextWrapping = TextWrapping.Wrap
                                };
                            }
                            break;
                        case SettingType.ExtensionsList:
                            content = LoadExtensionsPreview(_setting.SourcePath);
                            break;
                        case SettingType.GlobalStorage:
                        case SettingType.WorkspaceSettings:
                            if (Directory.Exists(_setting.SourcePath))
                            {
                                content = LoadDirectoryPreview(_setting.SourcePath);
                            }
                            else
                            {
                                content = new TextBlock
                                {
                                    Text = "Directory not found",
                                    Foreground = System.Windows.Media.Brushes.Red,
                                    TextWrapping = TextWrapping.Wrap
                                };
                            }
                            break;
                        case SettingType.Documentation:
                            content = LoadDocumentationPreview(_setting);
                            break;
                        case SettingType.Other:
                            // Check if it's a file or directory
                            if (File.Exists(_setting.SourcePath))
                            {
                                if (_setting.SourcePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    content = LoadJsonPreview(_setting.SourcePath);
                                }
                                else
                                {
                                    content = new TextBlock
                                    {
                                        Text = $"File: {Path.GetFileName(_setting.SourcePath)}\n" +
                                               $"Size: {GetFileSize(_setting.SourcePath)}\n" +
                                               $"Last modified: {GetLastModified(_setting.SourcePath)}",
                                        Foreground = System.Windows.Media.Brushes.White,
                                        TextWrapping = TextWrapping.Wrap
                                    };
                                }
                            }
                            else if (Directory.Exists(_setting.SourcePath))
                            {
                                content = LoadDirectoryPreview(_setting.SourcePath);
                            }
                            else
                            {
                                content = new TextBlock
                                {
                                    Text = "File or directory not found",
                                    Foreground = System.Windows.Media.Brushes.Red,
                                    TextWrapping = TextWrapping.Wrap
                                };
                            }
                            break;
                        default:
                            content = new TextBlock
                            {
                                Text = $"Preview not available for {_setting.Type}",
                                Foreground = System.Windows.Media.Brushes.White
                            };
                            break;
                    }
                }

                // Update UI on main thread
                PreviewContent = content ?? new TextBlock
                {
                    Text = "Failed to load content",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            catch (Exception ex)
            {
                PreviewContent = new TextBlock
                {
                    Text = $"Error loading preview: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            
            // Ensure UI is updated
            if (PreviewContent == null)
            {
                PreviewContent = new TextBlock
                {
                    Text = "No preview available",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            
            // Force UI update
            UpdateLayout();
        }

        private object LoadJsonPreview(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new TextBlock
                {
                    Text = "File not found",
                    Foreground = System.Windows.Media.Brushes.Red
                };
            }

            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent);

                if (settings == null || settings.Count == 0)
                {
                    return new TextBox
                    {
                        Text = jsonContent,
                        IsReadOnly = true,
                        Background = System.Windows.Media.Brushes.Transparent,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 11,
                        TextWrapping = TextWrapping.NoWrap,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                }

                // Create Cursor-style settings UI
                var categories = new Dictionary<string, List<(string Key, string Description, object Value)>>();

                foreach (var kvp in settings)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;
                    
                    // Determine category from key prefix
                    string category = "General";
                    if (key.Contains("."))
                    {
                        var prefix = key.Split('.')[0];
                        category = prefix switch
                        {
                            "terminal" => "Terminal",
                            "editor" => "Editor",
                            "workbench" => "Workbench",
                            "files" => "Files",
                            "explorer" => "Explorer",
                            "git" => "Git",
                            "cursor" => "Cursor",
                            "github" => "GitHub",
                            "php" => "PHP",
                            "dart" => "Dart",
                            "playwright" => "Playwright",
                            "update" => "Update",
                            "window" => "Window",
                            "security" => "Security",
                            "redhat" => "Red Hat",
                            "diffEditor" => "Diff Editor",
                            _ => "Other"
                        };
                    }
                    else if (key.StartsWith("[") && key.EndsWith("]"))
                    {
                        category = "Language Specific";
                    }

                    if (!categories.ContainsKey(category))
                    {
                        categories[category] = new List<(string, string, object)>();
                    }

                    categories[category].Add((key, GetSettingDescription(key), value));
                }

                // Create UI
                var stackPanel = new StackPanel();
                foreach (var category in categories.OrderBy(c => c.Key))
                {
                    var categoryBorder = new Border
                    {
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(0, 15, 0, 15),
                        Margin = new Thickness(0, 0, 0, 20)
                    };

                    var categoryStack = new StackPanel();
                    
                    var categoryTitle = new TextBlock
                    {
                        Text = category.Key,
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    categoryStack.Children.Add(categoryTitle);

                    foreach (var setting in category.Value.OrderBy(s => s.Key))
                    {
                        var key = setting.Key;
                        var description = setting.Description;
                        var value = setting.Value;
                        
                        var settingGrid = new Grid
                        {
                            Margin = new Thickness(0, 0, 0, 15)
                        };
                        settingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        settingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var keyStack = new StackPanel();
                        var keyText = new TextBlock
                        {
                            Text = key,
                            FontSize = 13,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204)),
                            Margin = new Thickness(0, 0, 0, 5)
                        };
                        var descText = new TextBlock
                        {
                            Text = description,
                            FontSize = 11,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            TextWrapping = TextWrapping.Wrap
                        };
                        keyStack.Children.Add(keyText);
                        keyStack.Children.Add(descText);

                        Grid.SetColumn(keyStack, 0);
                        settingGrid.Children.Add(keyStack);

                        var valueControl = CreateValueControl(key, value);
                        if (valueControl != null)
                        {
                            Grid.SetColumn(valueControl, 1);
                            settingGrid.Children.Add(valueControl);
                        }

                        categoryStack.Children.Add(settingGrid);
                    }

                    categoryBorder.Child = categoryStack;
                    stackPanel.Children.Add(categoryBorder);
                }

                var scrollViewer = new ScrollViewer
                {
                    Content = stackPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                return scrollViewer;
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error reading file: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private string GetSettingDescription(string key)
        {
            // Common setting descriptions
            var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "terminal.explorerKind", "Terminal explorer kind" },
                { "terminal.integrated.rightClickBehavior", "Right-click behavior in terminal" },
                { "terminal.integrated.shellIntegration.history", "Shell integration history size" },
                { "terminal.integrated.smoothScrolling", "Enable smooth scrolling in terminal" },
                { "terminal.integrated.suggest.enabled", "Enable terminal suggestions" },
                { "editor.defaultFormatter", "Default formatter for editor" },
                { "editor.suggest.selectionMode", "Suggestion selection mode" },
                { "explorer.confirmDelete", "Confirm before deleting files" },
                { "explorer.confirmDragAndDrop", "Confirm before drag and drop" },
                { "workbench.colorTheme", "Color theme for workbench" },
                { "cursor.general.gitGraphIndexing", "Git graph indexing setting" },
                { "cursor.composer.shouldChimeAfterChatFinishes", "Play chime after chat finishes" },
                { "github.copilot.enable", "Enable GitHub Copilot" },
                { "github.copilot.selectedCompletionModel", "Selected Copilot completion model" }
            };

            if (descriptions.TryGetValue(key, out var desc))
                return desc;

            // Generate description from key
            return key.Replace(".", " ").Replace("_", " ");
        }

        private UIElement? CreateValueControl(string key, object value)
        {
            try
            {
                if (value == null)
                    return new TextBlock { Text = "null", Foreground = System.Windows.Media.Brushes.Gray };

                var valueType = value.GetType();
                var valueStr = value.ToString() ?? "";

                // Boolean - Toggle switch style
                if (value is bool boolValue)
                {
                    var border = new Border
                    {
                        Width = 40,
                        Height = 20,
                        CornerRadius = new CornerRadius(10),
                        Background = boolValue 
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)),
                        BorderThickness = new Thickness(0)
                    };
                    return border;
                }

                // String or number - Text display
                if (value is string || valueType.IsPrimitive || value is decimal)
                {
                    var textBlock = new TextBlock
                    {
                        Text = valueStr.Length > 50 ? valueStr.Substring(0, 50) + "..." : valueStr,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 11,
                        MaxWidth = 300,
                        TextWrapping = TextWrapping.Wrap
                    };
                    return textBlock;
                }

                // Array or object - Show type
                if (value is Newtonsoft.Json.Linq.JArray || value is Newtonsoft.Json.Linq.JObject)
                {
                    var textBlock = new TextBlock
                    {
                        Text = value is Newtonsoft.Json.Linq.JArray arr ? $"Array ({arr.Count} items)" : "Object",
                        Foreground = System.Windows.Media.Brushes.White,
                        FontSize = 11
                    };
                    return textBlock;
                }

                return new TextBlock { Text = valueStr, Foreground = System.Windows.Media.Brushes.White };
            }
            catch
            {
                return new TextBlock { Text = value?.ToString() ?? "null", Foreground = System.Windows.Media.Brushes.Gray };
            }
        }

        private object LoadChatPreview(string filePath)
        {
            // FIRST: Check if this is a state.vscdb-based chat by checking Description
            // This is the most reliable way since the filePath might be virtual
            bool isStateDbChat = false;
            string? stateDbPath = null;
            string? workspaceFolder = null;
            
            // Check Description for IsFromStateDb flag
            if (_setting != null && !string.IsNullOrEmpty(_setting.Description))
            {
                if (_setting.Description.Contains("IsFromStateDb: true", StringComparison.OrdinalIgnoreCase))
                {
                    isStateDbChat = true;
                    
                    // Extract StateDbPath from Description
                    var lines = _setting.Description.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Trim().StartsWith("StateDbPath:", StringComparison.OrdinalIgnoreCase))
                        {
                            stateDbPath = line.Substring("StateDbPath:".Length).Trim();
                            workspaceFolder = Path.GetDirectoryName(stateDbPath);
                            break;
                        }
                    }
                }
            }
            
            // SECOND: Check filePath patterns (fallback)
            if (!isStateDbChat)
            {
                isStateDbChat = filePath.Contains("_combined_prompts", StringComparison.OrdinalIgnoreCase) ||
                               filePath.Contains("_thread", StringComparison.OrdinalIgnoreCase) ||
                               filePath.Contains("_session", StringComparison.OrdinalIgnoreCase);
            }
            
            // THIRD: If still not determined, check if file doesn't exist but ProjectPath has state.vscdb
            if (!isStateDbChat && !File.Exists(filePath) && _setting != null && !string.IsNullOrEmpty(_setting.ProjectPath))
            {
                var potentialStateDb = Path.Combine(_setting.ProjectPath, "state.vscdb");
                if (File.Exists(potentialStateDb))
                {
                    isStateDbChat = true;
                    stateDbPath = potentialStateDb;
                    workspaceFolder = _setting.ProjectPath;
                }
            }
            
            // If it's a state.vscdb chat, load from database
            if (isStateDbChat)
            {
                // If we don't have stateDbPath yet, try to determine it
                if (string.IsNullOrEmpty(stateDbPath))
                {
                    // Try to get from ProjectPath
                    if (_setting != null && !string.IsNullOrEmpty(_setting.ProjectPath))
                    {
                        workspaceFolder = _setting.ProjectPath;
                        stateDbPath = Path.Combine(workspaceFolder, "state.vscdb");
                    }
                    // Try to get from filePath directory
                    else if (!string.IsNullOrEmpty(filePath))
                    {
                        workspaceFolder = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(workspaceFolder))
                        {
                            stateDbPath = Path.Combine(workspaceFolder, "state.vscdb");
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(stateDbPath) || !File.Exists(stateDbPath))
                {
                    return new TextBlock
                    {
                        Text = $"State database not found: {stateDbPath ?? "unknown"}\n\n" +
                               $"ProjectPath: {_setting?.ProjectPath ?? "null"}\n" +
                               $"SourcePath: {filePath}\n" +
                               $"Description: {_setting?.Description ?? "null"}",
                        Foreground = System.Windows.Media.Brushes.Red,
                        TextWrapping = TextWrapping.Wrap
                    };
                }
                
                // Load from state.vscdb
                return LoadStateDbPromptsPreview(stateDbPath, filePath);
            }
            
            // Regular chat file
            if (!File.Exists(filePath))
            {
                return new TextBlock
                {
                    Text = $"Chat file not found: {filePath}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }

            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var chat = JsonConvert.DeserializeObject<dynamic>(jsonContent);

                var sb = new StringBuilder();
                sb.AppendLine("=== CHAT PREVIEW ===\n");

                if (chat?.sessionId != null)
                {
                    sb.AppendLine($"Session ID: {chat.sessionId}");
                }

                if (chat?.creationDate != null)
                {
                    sb.AppendLine($"Created: {chat.creationDate}");
                }

                // Try different chat formats
                var messages = new List<(string Role, string Content, string? Timestamp)>();

                // Format 1: requests array with message objects (CURSOR CHAT SESSION FORMAT)
                if (chat?.requests != null)
                {
                    var requests = chat.requests as Newtonsoft.Json.Linq.JArray;
                    if (requests != null)
                    {
                        foreach (var request in requests)
                        {
                            // USER MESSAGE: request.message.text or request.message.parts[].text
                            string? userContent = null;
                            if (request?["message"] != null)
                            {
                                var message = request["message"];
                                
                                // Check if message is a string
                                if (message.Type == Newtonsoft.Json.Linq.JTokenType.String)
                                {
                                    userContent = message.ToString();
                                }
                                // Or if it's an object with text/parts
                                else if (message.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                                {
                                    userContent = message["text"]?.ToString();
                                    
                                    // If no text, try parts array
                                    if (string.IsNullOrEmpty(userContent) && message["parts"] != null)
                                    {
                                        var parts = message["parts"] as Newtonsoft.Json.Linq.JArray;
                                        if (parts != null && parts.Count > 0)
                                        {
                                            var contentParts = new List<string>();
                                            foreach (var part in parts)
                                            {
                                                if (part["text"] != null)
                                                {
                                                    contentParts.Add(part["text"].ToString());
                                                }
                                                else if (part.Type == Newtonsoft.Json.Linq.JTokenType.String)
                                                {
                                                    contentParts.Add(part.ToString());
                                                }
                                            }
                                            userContent = string.Join("\n", contentParts);
                                        }
                                    }
                                }
                            }
                            
                            // Add USER message if found
                            if (!string.IsNullOrEmpty(userContent))
                            {
                                var timestamp = request?["timestamp"]?.ToString();
                                messages.Add(("USER", userContent, timestamp));
                            }
                            
                            // ASSISTANT RESPONSE: request.response[].value or request.response[].message.text
                            if (request?["response"] != null)
                            {
                                var responses = request["response"] as Newtonsoft.Json.Linq.JArray;
                                if (responses != null && responses.Count > 0)
                                {
                                    var assistantParts = new List<string>();
                                    
                                    foreach (var response in responses)
                                    {
                                        string? respContent = null;
                                        
                                        // Format 1: response.value (MOST COMMON in Cursor chat sessions)
                                        if (response["value"] != null)
                                        {
                                            respContent = response["value"]?.ToString();
                                        }
                                        // Format 2: response.message (can be string or object)
                                        else if (response["message"] != null)
                                        {
                                            var respMessage = response["message"];
                                            if (respMessage.Type == Newtonsoft.Json.Linq.JTokenType.String)
                                            {
                                                respContent = respMessage.ToString();
                                            }
                                            else if (respMessage.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                                            {
                                                respContent = respMessage["text"]?.ToString() ?? respMessage["content"]?.ToString();
                                            }
                                        }
                                        // Format 3: response.text
                                        else if (response["text"] != null)
                                        {
                                            respContent = response["text"]?.ToString();
                                        }
                                        // Format 4: response.content
                                        else if (response["content"] != null)
                                        {
                                            respContent = response["content"]?.ToString();
                                        }
                                        
                                        if (!string.IsNullOrEmpty(respContent))
                                        {
                                            assistantParts.Add(respContent);
                                        }
                                    }
                                    
                                    // Add ASSISTANT response if found
                                    if (assistantParts.Count > 0)
                                    {
                                        var assistantContent = string.Join("\n", assistantParts);
                                        var timestamp = request?["timestamp"]?.ToString();
                                        messages.Add(("ASSISTANT", assistantContent, timestamp));
                                    }
                                }
                            }
                        }
                    }
                }
                // Format 2: messages array
                else if (chat?.messages != null)
                {
                    var msgArray = chat.messages as Newtonsoft.Json.Linq.JArray;
                    if (msgArray != null)
                    {
                        foreach (var msg in msgArray)
                        {
                            var role = msg?["role"]?.ToString() ?? "unknown";
                            var content = msg?["content"]?.ToString() ?? "";
                            var timestamp = msg?["timestamp"]?.ToString();
                            messages.Add((role, content, timestamp));
                        }
                    }
                }
                // Format 3: Direct message properties
                else if (chat?.role != null)
                {
                    var role = chat.role?.ToString() ?? "unknown";
                    var content = chat.content?.ToString() ?? "";
                    var timestamp = chat.timestamp?.ToString();
                    messages.Add((role, content, timestamp));
                }

                if (messages.Count > 0)
                {
                    sb.AppendLine($"\nTotal Messages: {messages.Count}");
                    sb.AppendLine("\n--- Conversation ---\n");

                    int displayCount = Math.Min(messages.Count, 20);
                    for (int i = 0; i < displayCount; i++)
                    {
                        var (role, content, timestamp) = messages[i];
                        sb.AppendLine($"### {role.ToUpperInvariant()}");
                        if (!string.IsNullOrEmpty(timestamp))
                        {
                            sb.AppendLine($"*{timestamp}*");
                        }
                        sb.AppendLine();
                        
                        // Format content nicely (preserve markdown-like formatting)
                        var formattedContent = content;
                        if (formattedContent.Length > 500)
                        {
                            formattedContent = formattedContent.Substring(0, 500) + "\n... (truncated)";
                        }
                        sb.AppendLine(formattedContent);
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }

                    if (messages.Count > displayCount)
                    {
                        sb.AppendLine($"\n... and {messages.Count - displayCount} more messages");
                    }
                }
                else
                {
                    sb.AppendLine("No messages found in chat file.");
                    sb.AppendLine("\nRaw JSON structure:");
                    var jsonStr = JsonConvert.SerializeObject(chat, Formatting.Indented);
                    sb.AppendLine(jsonStr.Substring(0, Math.Min(1000, jsonStr.Length)));
                }

                return new TextBox
                {
                    Text = sb.ToString(),
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error reading chat file: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private object LoadDirectoryPreview(string dirPath)
        {
            if (!Directory.Exists(dirPath))
            {
                return new TextBlock
                {
                    Text = "Directory not found",
                    Foreground = System.Windows.Media.Brushes.Red
                };
            }

            try
            {
                var files = Directory.GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly);
                var dirs = Directory.GetDirectories(dirPath, "*", SearchOption.TopDirectoryOnly);

                var sb = new StringBuilder();
                sb.AppendLine("=== DIRECTORY CONTENTS ===\n");
                sb.AppendLine($"Files: {files.Length}");
                sb.AppendLine($"Directories: {dirs.Length}\n");

                if (files.Length > 0)
                {
                    sb.AppendLine("Files:");
                    foreach (var file in files.Take(20))
                    {
                        var fileInfo = new FileInfo(file);
                        sb.AppendLine($"  - {fileInfo.Name} ({FormatFileSize(fileInfo.Length)})");
                    }
                    if (files.Length > 20)
                    {
                        sb.AppendLine($"  ... and {files.Length - 20} more files");
                    }
                }

                if (dirs.Length > 0)
                {
                    sb.AppendLine("\nDirectories:");
                    foreach (var dir in dirs.Take(20))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        sb.AppendLine($"  - {dirInfo.Name}/");
                    }
                    if (dirs.Length > 20)
                    {
                        sb.AppendLine($"  ... and {dirs.Length - 20} more directories");
                    }
                }

                return new TextBox
                {
                    Text = sb.ToString(),
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.NoWrap,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error reading directory: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private string FormatJson(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string GetFileSize(string filePath)
        {
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                return FormatFileSize(fileInfo.Length);
            }
            return "Unknown";
        }

        private string GetLastModified(string filePath)
        {
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                return fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            return "Unknown";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private object LoadExtensionsPreview(string extensionsPath)
        {
            if (!Directory.Exists(extensionsPath))
            {
                return new TextBlock
                {
                    Text = "Extensions directory not found",
                    Foreground = System.Windows.Media.Brushes.Red
                };
            }

            try
            {
                // First, try to find extensions.json file
                var extensionsJsonPath = Path.Combine(extensionsPath, "extensions.json");
                if (File.Exists(extensionsJsonPath))
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(extensionsJsonPath);
                        var extensions = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                        
                        if (extensions != null)
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("=== INSTALLED EXTENSIONS ===\n");
                            
                            // Handle different JSON formats
                            if (extensions is Newtonsoft.Json.Linq.JArray array)
                            {
                                foreach (var ext in array.Take(50))
                                {
                                    var name = ext?["name"]?.ToString() ?? ext?["identifier"]?.ToString() ?? "Unknown";
                                    var version = ext?["version"]?.ToString() ?? "";
                                    sb.AppendLine($"- {name} {version}");
                                }
                            }
                            else if (extensions is Newtonsoft.Json.Linq.JObject obj)
                            {
                                foreach (var prop in obj.Properties().Take(50))
                                {
                                    sb.AppendLine($"- {prop.Name}: {prop.Value}");
                                }
                            }
                            
                            return new TextBox
                            {
                                Text = sb.ToString(),
                                IsReadOnly = true,
                                Background = System.Windows.Media.Brushes.Transparent,
                                Foreground = System.Windows.Media.Brushes.White,
                                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                                FontSize = 11,
                                TextWrapping = TextWrapping.NoWrap,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                            };
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, continue to directory listing
                    }
                }
                
                // If no extensions.json or parsing failed, list extension directories
                var extensionDirs = Directory.GetDirectories(extensionsPath);
                var sb2 = new StringBuilder();
                sb2.AppendLine("=== INSTALLED EXTENSIONS (from directories) ===\n");
                sb2.AppendLine($"Total extensions: {extensionDirs.Length}\n");
                
                foreach (var extDir in extensionDirs.OrderBy(d => Path.GetFileName(d)).Take(100))
                {
                    var extName = Path.GetFileName(extDir);
                    var packageJson = Path.Combine(extDir, "package.json");
                    
                    if (File.Exists(packageJson))
                    {
                        try
                        {
                            var packageContent = File.ReadAllText(packageJson);
                            var package = JsonConvert.DeserializeObject<dynamic>(packageContent);
                            var displayName = package?["displayName"]?.ToString() ?? package?["name"]?.ToString() ?? extName;
                            var version = package?["version"]?.ToString() ?? "?";
                            sb2.AppendLine($"- {displayName} (v{version})");
                        }
                        catch
                        {
                            sb2.AppendLine($"- {extName}");
                        }
                    }
                    else
                    {
                        sb2.AppendLine($"- {extName}");
                    }
                }
                
                if (extensionDirs.Length > 100)
                {
                    sb2.AppendLine($"\n... and {extensionDirs.Length - 100} more extensions");
                }
                
                return new TextBox
                {
                    Text = sb2.ToString(),
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.NoWrap,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error reading extensions: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private object LoadStateDatabasePreview(string dbPath)
        {
            try
            {
                var dbInfo = new FileInfo(dbPath);
                var sb = new StringBuilder();
                sb.AppendLine("=== STATE DATABASE INFO ===\n");
                sb.AppendLine($"File: {Path.GetFileName(dbPath)}");
                sb.AppendLine($"Size: {FormatFileSize(dbInfo.Length)}");
                sb.AppendLine($"Last modified: {dbInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n");

                // Try to query database for table info
                try
                {
                    using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name LIMIT 20";
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    sb.AppendLine("=== TABLES ===");
                                    var tables = new List<string>();
                                    while (reader.Read())
                                    {
                                        tables.Add(reader.GetString(0));
                                    }
                                    foreach (var table in tables)
                                    {
                                        sb.AppendLine($"- {table}");
                                    }
                                    if (tables.Count >= 20)
                                    {
                                        sb.AppendLine("... (and more)");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    sb.AppendLine($"\nNote: Could not query database (may be locked): {dbEx.Message}");
                }

                return new TextBox
                {
                    Text = sb.ToString(),
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.NoWrap,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error reading database: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private object LoadStateDbPromptsPreview(string stateDbPath, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                
                // Determine chat type from file path OR chat name
                var chatType = "Combined Prompts";
                int? threadNumber = null;
                string? sessionUuid = null;
                
                // First, try to extract from chat name (more reliable)
                if (_setting != null && !string.IsNullOrEmpty(_setting.Name))
                {
                    var chatName = _setting.Name;
                    
                    // Try to extract thread number from chat name (e.g., "Chat: experimental - Thread 1 (7 prompts)")
                    var threadMatch = System.Text.RegularExpressions.Regex.Match(chatName, @"Thread\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (threadMatch.Success && int.TryParse(threadMatch.Groups[1].Value, out var threadNum))
                    {
                        threadNumber = threadNum;
                        chatType = $"Thread {threadNum}";
                    }
                    // Try to extract session UUID from chat name
                    else
                    {
                        var sessionMatch = System.Text.RegularExpressions.Regex.Match(chatName, @"Session\s+([a-f0-9-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (sessionMatch.Success)
                        {
                            sessionUuid = sessionMatch.Groups[1].Value;
                            chatType = $"Session {sessionUuid.Substring(0, Math.Min(8, sessionUuid.Length))}...";
                        }
                    }
                }
                
                // Fallback: try to extract from file path
                if (!threadNumber.HasValue && string.IsNullOrEmpty(sessionUuid))
                {
                    if (filePath.Contains("_thread", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var threadMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"[Tt]hread[\s_]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (threadMatch.Success && int.TryParse(threadMatch.Groups[1].Value, out var threadNum))
                        {
                            threadNumber = threadNum;
                            chatType = $"Thread {threadNum}";
                        }
                    }
                    else if (filePath.Contains("_session", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var sessionMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"_session_([a-f0-9-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (sessionMatch.Success)
                        {
                            sessionUuid = sessionMatch.Groups[1].Value;
                            chatType = $"Session {sessionUuid.Substring(0, Math.Min(8, sessionUuid.Length))}...";
                        }
                    }
                }
                
                sb.AppendLine($"=== {chatType.ToUpper()} FROM STATE.VSCDB ===\n");
                
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
                                return new TextBlock
                                {
                                    Text = "aiService.prompts not found in state.vscdb",
                                    Foreground = System.Windows.Media.Brushes.Red
                                };
                            }
                            
                            var promptsJson = reader.GetString(0);
                            if (string.IsNullOrEmpty(promptsJson))
                            {
                                return new TextBlock
                                {
                                    Text = "aiService.prompts is empty in state.vscdb",
                                    Foreground = System.Windows.Media.Brushes.Red
                                };
                            }
                            
                            prompts = JsonConvert.DeserializeObject<List<dynamic>>(promptsJson);
                            if (prompts == null || prompts.Count == 0)
                            {
                                return new TextBlock
                                {
                                    Text = "No prompts found in aiService.prompts",
                                    Foreground = System.Windows.Media.Brushes.Red
                                };
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
                    List<dynamic> promptsToDisplay;
                    int threadStartIndex = 0;
                    if (threadNumber.HasValue)
                    {
                        // Display only the specified thread
                        var threads = GroupPromptsIntoThreads(prompts);
                        if (threadNumber.Value < 1 || threadNumber.Value > threads.Count)
                        {
                            return new TextBlock
                            {
                                Text = $"Thread {threadNumber.Value} not found. Available threads: 1-{threads.Count}",
                                Foreground = System.Windows.Media.Brushes.Red
                            };
                        }
                        promptsToDisplay = threads[threadNumber.Value - 1];
                        // Calculate start index for this thread
                        for (int i = 0; i < threadNumber.Value - 1; i++)
                        {
                            threadStartIndex += threads[i].Count;
                        }
                        sb.AppendLine($"**Thread Number:** {threadNumber.Value}");
                    }
                    else if (!string.IsNullOrEmpty(sessionUuid))
                    {
                        // For sessions, display all prompts (no direct mapping yet)
                        promptsToDisplay = prompts;
                        sb.AppendLine($"**Session UUID:** {sessionUuid}");
                    }
                    else
                    {
                        // Display all prompts (combined)
                        promptsToDisplay = prompts;
                    }
                    
                    // Match generations to prompts (using absolute index from ALL prompts, like Python script)
                    // The Python script's match_generations_to_prompts creates a map: prompt_index -> generation
                    // where prompt_index is the index in ALL prompts, not thread-relative
                    var generationMap = new Dictionary<int, dynamic>(); // Key: absolute prompt index in ALL prompts
                    if (generations != null && generations.Count > 0 && prompts != null)
                    {
                        // Match ALL prompts to generations (not just thread prompts)
                        // This matches the Python script: match_generations_to_prompts(prompts, generations)
                        for (int absoluteIdx = 0; absoluteIdx < prompts.Count; absoluteIdx++)
                        {
                            var prompt = prompts[absoluteIdx];
                            JObject promptObj;
                            if (prompt is JObject existingPromptObj)
                            {
                                promptObj = existingPromptObj;
                            }
                            else
                            {
                                promptObj = JObject.FromObject(prompt);
                            }
                            
                            long promptTime = 0;
                            if (promptObj["unixMs"] != null && promptObj["unixMs"].Type != JTokenType.Null)
                            {
                                try
                                {
                                    promptTime = promptObj["unixMs"].Value<long>();
                                }
                                catch { }
                            }
                            else if (promptObj["timestamp"] != null && promptObj["timestamp"].Type != JTokenType.Null)
                            {
                                try
                                {
                                    promptTime = promptObj["timestamp"].Value<long>();
                                }
                                catch { }
                            }
                            
                            dynamic? bestMatch = null;
                            long bestTimeDiff = long.MaxValue;
                            
                            // Try to match by timestamp proximity (like Python script)
                            foreach (var gen in generations)
                            {
                                JObject genObj;
                                if (gen is JObject existingGenObj)
                                {
                                    genObj = existingGenObj;
                                }
                                else
                                {
                                    genObj = JObject.FromObject(gen);
                                }
                                
                                long genTime = 0;
                                if (genObj["unixMs"] != null && genObj["unixMs"].Type != JTokenType.Null)
                                {
                                    try
                                    {
                                        genTime = genObj["unixMs"].Value<long>();
                                    }
                                    catch { }
                                }
                                else if (genObj["timestamp"] != null && genObj["timestamp"].Type != JTokenType.Null)
                                {
                                    try
                                    {
                                        genTime = genObj["timestamp"].Value<long>();
                                    }
                                    catch { }
                                }
                                
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
                            
                            // If no time match, use order-based matching (like Python script: if best_match is None and i < len(generations))
                            if (bestMatch == null && absoluteIdx < generations.Count)
                            {
                                bestMatch = generations[absoluteIdx];
                            }
                            
                            if (bestMatch != null)
                            {
                                // Store with absolute index (like Python script: prompt_to_generation[i] = best_match)
                                generationMap[absoluteIdx] = bestMatch;
                            }
                        }
                    }
                    
                    sb.AppendLine($"**Total Prompts:** {promptsToDisplay.Count}");
                    if (generations != null)
                    {
                        sb.AppendLine($"**Total Generations:** {generations.Count}");
                        sb.AppendLine($"**Matched Responses:** {generationMap.Count}");
                    }
                    sb.AppendLine($"**Source:** {stateDbPath}");
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                    
                    // Display ALL prompts with matching assistant responses (NO TRUNCATION)
                    // IMPORTANT: Also try to get responses for prompts that don't have matched generations
                    for (int idx = 0; idx < promptsToDisplay.Count; idx++)
                    {
                        var prompt = promptsToDisplay[idx];
                        JObject promptObj;
                        if (prompt is JObject existingPromptObj)
                        {
                            promptObj = existingPromptObj;
                        }
                        else
                        {
                            promptObj = JObject.FromObject(prompt);
                        }
                        
                        var promptText = promptObj["text"]?.ToString() ?? 
                                        promptObj["prompt"]?.ToString() ?? 
                                        promptObj["message"]?.ToString() ?? 
                                        promptObj["content"]?.ToString();
                        
                        var commandType = promptObj["commandType"]?.ToString();
                        
                        // Get timestamp
                        long promptTime = 0;
                        if (promptObj["unixMs"] != null && promptObj["unixMs"].Type != JTokenType.Null)
                        {
                            try
                            {
                                promptTime = promptObj["unixMs"].Value<long>();
                            }
                            catch { }
                        }
                        else if (promptObj["timestamp"] != null && promptObj["timestamp"].Type != JTokenType.Null)
                        {
                            try
                            {
                                promptTime = promptObj["timestamp"].Value<long>();
                            }
                            catch { }
                        }
                        
                        if (!string.IsNullOrEmpty(promptText))
                        {
                            sb.AppendLine($"### USER (Prompt #{idx + 1})");
                            
                            if (!string.IsNullOrEmpty(commandType) && commandType != "unknown")
                            {
                                sb.AppendLine($"**Command Type:** {commandType}");
                            }
                            
                            if (promptTime > 0)
                            {
                                try
                                {
                                    var timestamp = promptTime < 1000000000000 ? promptTime * 1000 : promptTime;
                                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                                    sb.AppendLine($"**Time:** {date:yyyy-MM-dd HH:mm:ss}");
                                }
                                catch { }
                            }
                            
                            sb.AppendLine();
                            
                            // NO TRUNCATION - show full prompt text
                            sb.AppendLine(promptText);
                            sb.AppendLine();
                            sb.AppendLine("---");
                            sb.AppendLine();
                        }
                        
                        // Add matching assistant response if available
                        // Use absolute index to look up in generationMap (like Python script: absolute_idx in generations_map)
                        int absoluteIdx = threadStartIndex + idx;
                        
                        // FIRST: Try to get response from bubbles (most reliable for recent chats)
                        string? assistantResponse = null;
                        try
                        {
                            assistantResponse = GetAssistantResponseFromBubbles(stateDbPath, promptTime, absoluteIdx, promptText);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug($"[SettingPreviewDialog] LoadStateDbPromptsPreview: Error getting bubble response for prompt #{idx + 1}: {ex.Message}");
                        }
                        
                        // SECOND: If no bubble response, try generationMap
                        if (string.IsNullOrEmpty(assistantResponse) && generationMap.TryGetValue(absoluteIdx, out var generation))
                        {
                            JObject genObj;
                            if (generation is JObject existingGenObj)
                            {
                                genObj = existingGenObj;
                            }
                            else
                            {
                                genObj = JObject.FromObject(generation);
                            }
                            
                            // IMPORTANT: Based on analysis, generations[i].textDescription contains the USER prompt text,
                            // NOT the ASSISTANT response! The actual ASSISTANT response might be in generation[i+1].textDescription
                            // or might not be stored in state.vscdb at all.
                            // 
                            // For now, we'll check if textDescription matches the current prompt - if it does, it's the prompt, not the response.
                            // We'll look for the next generation's textDescription as a potential response, or skip if it matches the next prompt.
                            
                            var genTextDesc = genObj["textDescription"]?.ToString() ?? "";
                            var currentPromptText = promptText?.Trim() ?? "";
                            
                            // Check if this generation's textDescription is actually the USER prompt (common case)
                            bool isPromptText = !string.IsNullOrEmpty(genTextDesc) && 
                                               !string.IsNullOrEmpty(currentPromptText) &&
                                               genTextDesc.Trim().Equals(currentPromptText, StringComparison.OrdinalIgnoreCase);
                            
                            string? genText = null;
                            
                            if (isPromptText)
                            {
                                // This generation's textDescription is the prompt, not the response
                                // Try to get the actual response from the next generation, or mark as no response available
                                int nextGenIdx = absoluteIdx + 1;
                                if (nextGenIdx < generations.Count && generationMap.ContainsKey(nextGenIdx))
                                {
                                    // Don't use next generation if it's already paired with next prompt
                                    // For now, we'll indicate that the response is not available in this format
                                    genText = null;
                                }
                                else
                                {
                                    genText = null; // Response not available in this generation structure
                                }
                            }
                            else
                            {
                                // textDescription doesn't match the prompt, might be the response
                                genText = genTextDesc;
                            }
                            
                            // Fallback: try other fields
                            if (string.IsNullOrEmpty(genText))
                            {
                                genText = genObj["text"]?.ToString() ?? 
                                         genObj["content"]?.ToString() ?? 
                                         genObj["message"]?.ToString() ?? "";
                            }
                            
                            // If still empty, try nested structures
                            if (string.IsNullOrEmpty(genText))
                            {
                                if (genObj["message"] != null && genObj["message"].Type == JTokenType.Object)
                                {
                                    var msgObj = genObj["message"] as JObject;
                                    genText = msgObj?["text"]?.ToString() ?? 
                                             msgObj?["content"]?.ToString() ?? "";
                                }
                                
                                if (string.IsNullOrEmpty(genText) && genObj["parts"] != null)
                                {
                                    var parts = genObj["parts"] as JArray;
                                    if (parts != null && parts.Count > 0)
                                    {
                                        var textParts = new List<string>();
                                        foreach (var part in parts)
                                        {
                                            if (part["text"] != null)
                                            {
                                                textParts.Add(part["text"].ToString());
                                            }
                                            else if (part.Type == JTokenType.String)
                                            {
                                                textParts.Add(part.ToString());
                                            }
                                        }
                                        genText = string.Join("\n", textParts);
                                    }
                                }
                            }
                            
                            // CRITICAL: Always check if genText matches the prompt text (regardless of source)
                            // If it does, it's the user prompt, NOT an AI response!
                            if (!string.IsNullOrEmpty(genText) && !string.IsNullOrEmpty(promptText))
                            {
                                var genNormalized = genText.Replace("\r", "").Replace("\n", " ").Trim();
                                var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                
                                // Check if they match (exact or significant overlap)
                                if (genNormalized.Equals(promptNormalized, StringComparison.OrdinalIgnoreCase) ||
                                    (genNormalized.Length > 50 && promptNormalized.Length > 50 &&
                                     (genNormalized.Contains(promptNormalized, StringComparison.OrdinalIgnoreCase) ||
                                      promptNormalized.Contains(genNormalized, StringComparison.OrdinalIgnoreCase))))
                                {
                                    Logger.LogDebug($"[SettingPreviewDialog] LoadStateDbPromptsPreview: genText matches prompt text, skipping (length: {genText.Length})");
                                    genText = null; // This is the prompt, not a response
                                }
                            }
                            
                            // Use genText from generation if available
                            if (!string.IsNullOrEmpty(genText))
                            {
                                // If we already have a bubble response, prefer the longer one
                                if (!string.IsNullOrEmpty(assistantResponse) && assistantResponse.Length > genText.Length)
                                {
                                    genText = assistantResponse;
                                }
                            }
                            else
                            {
                                // No genText, use bubble response if available
                                genText = assistantResponse;
                            }
                            
                            // CRITICAL: Also check if final genText matches prompt (bubble response might be wrong too)
                            if (!string.IsNullOrEmpty(genText) && !string.IsNullOrEmpty(promptText))
                            {
                                var genNormalized = genText.Replace("\r", "").Replace("\n", " ").Trim();
                                var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                
                                if (genNormalized.Equals(promptNormalized, StringComparison.OrdinalIgnoreCase) ||
                                    (genNormalized.Length > 50 && promptNormalized.Length > 50 &&
                                     (genNormalized.Contains(promptNormalized, StringComparison.OrdinalIgnoreCase) ||
                                      promptNormalized.Contains(genNormalized, StringComparison.OrdinalIgnoreCase))))
                                {
                                    Logger.LogDebug($"[SettingPreviewDialog] LoadStateDbPromptsPreview: Final genText matches prompt text, skipping (length: {genText.Length})");
                                    genText = null; // This is the prompt, not a response
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(genText))
                            {
                                sb.AppendLine($"### ASSISTANT (Response #{idx + 1})");
                                
                                // Get generation timestamp
                                long genTime = 0;
                                if (genObj["unixMs"] != null && genObj["unixMs"].Type != JTokenType.Null)
                                {
                                    try
                                    {
                                        genTime = genObj["unixMs"].Value<long>();
                                    }
                                    catch { }
                                }
                                else if (genObj["timestamp"] != null && genObj["timestamp"].Type != JTokenType.Null)
                                {
                                    try
                                    {
                                        genTime = genObj["timestamp"].Value<long>();
                                    }
                                    catch { }
                                }
                                
                                if (genTime > 0)
                                {
                                    try
                                    {
                                        var timestamp = genTime < 1000000000000 ? genTime * 1000 : genTime;
                                        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                                        sb.AppendLine($"**Time:** {date:yyyy-MM-dd HH:mm:ss}");
                                    }
                                    catch { }
                                }
                                
                                sb.AppendLine();
                                
                                // NO TRUNCATION - show full response text
                                sb.AppendLine(genText);
                                sb.AppendLine();
                                sb.AppendLine("---");
                                sb.AppendLine();
                            }
                        }
                        else if (!string.IsNullOrEmpty(assistantResponse))
                        {
                            // CRITICAL: Check if assistantResponse matches prompt (shouldn't happen, but safety check)
                            bool isPromptMatch = false;
                            if (!string.IsNullOrEmpty(promptText))
                            {
                                var respNormalized = assistantResponse.Replace("\r", "").Replace("\n", " ").Trim();
                                var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                
                                if (respNormalized.Equals(promptNormalized, StringComparison.OrdinalIgnoreCase) ||
                                    (respNormalized.Length > 50 && promptNormalized.Length > 50 &&
                                     (respNormalized.Contains(promptNormalized, StringComparison.OrdinalIgnoreCase) ||
                                      promptNormalized.Contains(respNormalized, StringComparison.OrdinalIgnoreCase))))
                                {
                                    Logger.LogDebug($"[SettingPreviewDialog] LoadStateDbPromptsPreview: assistantResponse matches prompt text, skipping (length: {assistantResponse.Length})");
                                    isPromptMatch = true;
                                }
                            }
                            
                            if (!isPromptMatch)
                            {
                                // We have a bubble response but no generation match - display it
                                sb.AppendLine($"### ASSISTANT (Response #{idx + 1})");
                                
                                if (promptTime > 0)
                                {
                                    try
                                    {
                                        var timestamp = promptTime < 1000000000000 ? promptTime * 1000 : promptTime;
                                        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                                        sb.AppendLine($"**Time:** {date:yyyy-MM-dd HH:mm:ss}");
                                    }
                                    catch { }
                                }
                                
                                sb.AppendLine();
                                sb.AppendLine(assistantResponse);
                                sb.AppendLine();
                                sb.AppendLine("---");
                                sb.AppendLine();
                            }
                        }
                    }
                }
                
                return new TextBox
                {
                    Text = sb.ToString(),
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error reading state.vscdb: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // For combined prompts, open the workspace folder instead
                // Check if this is a state.vscdb-based chat (combined prompts, threads, or sessions)
                if (_setting.SourcePath.Contains("_combined_prompts", StringComparison.OrdinalIgnoreCase) ||
                    _setting.SourcePath.Contains("_thread_", StringComparison.OrdinalIgnoreCase) ||
                    _setting.SourcePath.Contains("_session_", StringComparison.OrdinalIgnoreCase))
                {
                    var workspaceFolder = Path.GetDirectoryName(_setting.SourcePath);
                    if (!string.IsNullOrEmpty(workspaceFolder) && Directory.Exists(workspaceFolder))
                    {
                        Process.Start("explorer.exe", $"\"{workspaceFolder}\"");
                        return;
                    }
                }
                
                if (File.Exists(_setting.SourcePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{_setting.SourcePath}\"");
                }
                else if (Directory.Exists(_setting.SourcePath))
                {
                    Process.Start("explorer.exe", $"\"{_setting.SourcePath}\"");
                }
                else
                {
                    MessageBox.Show(
                        "File or directory not found.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening in Explorer: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// Loads documentation preview with URL and metadata
        /// </summary>
        private object LoadDocumentationPreview(CursorSettingItem setting)
        {
            try
            {
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(10)
                };

                // Extract URL from description
                var description = setting.Description ?? "";
                var urlMatch = System.Text.RegularExpressions.Regex.Match(description, @"https?://[^\s\n]+");
                var url = urlMatch.Success ? urlMatch.Value : "";

                // Title
                var titleBlock = new TextBlock
                {
                    Text = setting.Name,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(titleBlock);

                // URL section
                if (!string.IsNullOrEmpty(url))
                {
                    var urlLabel = new TextBlock
                    {
                        Text = "URL:",
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.LightGray,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    stackPanel.Children.Add(urlLabel);

                    var urlBlock = new TextBlock
                    {
                        Text = url,
                        Foreground = System.Windows.Media.Brushes.LightBlue,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 15),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    urlBlock.MouseLeftButtonDown += (s, e) =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error opening URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    };
                    stackPanel.Children.Add(urlBlock);
                }

                // Description
                if (!string.IsNullOrEmpty(setting.Description))
                {
                    var descLabel = new TextBlock
                    {
                        Text = "Details:",
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.LightGray,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    stackPanel.Children.Add(descLabel);

                    var descBlock = new TextBlock
                    {
                        Text = setting.Description,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    stackPanel.Children.Add(descBlock);
                }

                // Source path
                if (!string.IsNullOrEmpty(setting.SourcePath))
                {
                    var sourceLabel = new TextBlock
                    {
                        Text = "Source:",
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.LightGray,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    stackPanel.Children.Add(sourceLabel);

                    var sourceBlock = new TextBlock
                    {
                        Text = setting.SourcePath,
                        Foreground = System.Windows.Media.Brushes.LightGray,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    stackPanel.Children.Add(sourceBlock);
                }

                // Load full documentation data from state.vscdb if available
                if (File.Exists(setting.SourcePath) && setting.SourcePath.EndsWith("state.vscdb"))
                {
                    try
                    {
                        var docData = LoadDocumentationDataFromStateDb(setting.SourcePath, url);
                        if (docData != null)
                        {
                            var dataLabel = new TextBlock
                            {
                                Text = "Stored Data:",
                                FontWeight = FontWeights.Bold,
                                Foreground = System.Windows.Media.Brushes.LightGray,
                                Margin = new Thickness(0, 10, 0, 5)
                            };
                            stackPanel.Children.Add(dataLabel);

                            var dataBlock = new TextBlock
                            {
                                Text = docData,
                                Foreground = System.Windows.Media.Brushes.White,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 11,
                                Margin = new Thickness(0, 0, 0, 10)
                            };
                            stackPanel.Children.Add(dataBlock);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading documentation data: {ex.Message}");
                    }
                }

                var scrollViewer = new ScrollViewer
                {
                    Content = stackPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

                return scrollViewer;
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = $"Error loading documentation preview: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        /// <summary>
        /// Loads documentation data from state.vscdb
        /// </summary>
        private string? LoadDocumentationDataFromStateDb(string stateDbPath, string url)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={stateDbPath}"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Find the key that contains this URL
                        cmd.CommandText = @"
                            SELECT key, value 
                            FROM cursorDiskKV 
                            WHERE value LIKE @url
                            LIMIT 5";
                        cmd.Parameters.AddWithValue("@url", $"%{url}%");
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            var results = new StringBuilder();
                            while (reader.Read())
                            {
                                var key = reader.GetString(0);
                                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                                
                                if (!string.IsNullOrEmpty(value))
                                {
                                    results.AppendLine($"Key: {key}");
                                    results.AppendLine($"Value length: {value.Length} characters");
                                    
                                    // Try to parse as JSON and show structure
                                    try
                                    {
                                        var json = JsonConvert.DeserializeObject<dynamic>(value);
                                        if (json != null)
                                        {
                                            var jsonStr = JsonConvert.SerializeObject(json, Formatting.Indented);
                                            if (jsonStr.Length > 1000)
                                            {
                                                jsonStr = jsonStr.Substring(0, 1000) + "... (truncated)";
                                            }
                                            results.AppendLine($"JSON structure:\n{jsonStr}");
                                        }
                                    }
                                    catch
                                    {
                                        // Not JSON, show preview
                                        var preview = value.Length > 500 ? value.Substring(0, 500) + "..." : value;
                                        results.AppendLine($"Content preview:\n{preview}");
                                    }
                                    
                                    results.AppendLine();
                                }
                            }
                            
                            return results.Length > 0 ? results.ToString() : null;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
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
        
        /// <summary>
        /// Get ASSISTANT response from cursorDiskKV table using bubbleId keys
        /// Based on GitHub tools: cursor-view, cursor-chat-export
        /// FIXED: Groups bubbles by session and matches correctly (USER bubble -> next ASSISTANT bubble)
        /// </summary>
        private string? GetAssistantResponseFromBubbles(string stateDbPath, long promptTime, int promptIndex, string? promptText = null)
        {
            Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Looking for response (promptTime={promptTime}, index={promptIndex}, textLength={promptText?.Length ?? 0})");
            
            // Try global storage first (where bubbles are stored)
            var globalStateDb = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cursor", "User", "globalStorage", "state.vscdb");
            
            if (!File.Exists(globalStateDb))
            {
                Logger.LogWarning($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Global state DB not found at {globalStateDb}");
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
                    foreach (var session in bubbleSessions.Values)
                    {
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
                                    
                                    // Check if prompt text matches bubble text - improved matching
                                    if (bubbleText.Length > 0 && promptText.Length > 0)
                                    {
                                        // Normalize whitespace
                                        var promptNormalized = promptText.Replace("\r", "").Replace("\n", " ").Trim();
                                        var bubbleNormalized = bubbleText.Replace("\r", "").Replace("\n", " ").Trim();
                                        
                                        // Use longer comparison (200 chars) for better accuracy
                                        var promptStart = promptNormalized.Length > 200 ? promptNormalized.Substring(0, 200) : promptNormalized;
                                        var bubbleStart = bubbleNormalized.Length > 200 ? bubbleNormalized.Substring(0, 200) : bubbleNormalized;
                                        
                                        // Try exact match first
                                        if (promptStart.Equals(bubbleStart, StringComparison.OrdinalIgnoreCase))
                                        {
                                            matchingUserIndex = i;
                                            Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Found exact match at bubble index {i}");
                                            break;
                                        }
                                        
                                        // Try contains match (both directions)
                                        if (promptStart.Length > 50 && bubbleStart.Length > 50)
                                        {
                                            if (promptStart.Contains(bubbleStart, StringComparison.OrdinalIgnoreCase) ||
                                                bubbleStart.Contains(promptStart, StringComparison.OrdinalIgnoreCase))
                                            {
                                                matchingUserIndex = i;
                                                Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Found contains match at bubble index {i}");
                                                break;
                                            }
                                        }
                                        
                                        // Try word-based similarity
                                        var promptWords = promptStart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                        var bubbleWords = bubbleStart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                        
                                        if (promptWords.Length >= 3 && bubbleWords.Length >= 3)
                                        {
                                            var matchingWords = promptWords.Intersect(bubbleWords, StringComparer.OrdinalIgnoreCase).Count();
                                            var totalWords = Math.Max(promptWords.Length, bubbleWords.Length);
                                            var similarity = (double)matchingWords / totalWords;
                                            
                                            // If 60% of words match, consider it a match
                                            if (similarity >= 0.6)
                                            {
                                                matchingUserIndex = i;
                                                Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Found similarity match ({similarity:P0}) at bubble index {i}");
                                                break;
                                            }
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
                                                Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Skipping response that matches prompt text");
                                                continue; // Skip this, it's the prompt, not a response
                                            }
                                        }
                                        
                                        responseParts.Add(text);
                                        Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Collected ASSISTANT response part (length: {text.Length}, total parts: {responseParts.Count})");
                                    }
                                }
                            }
                            
                            // Combine all ASSISTANT response parts
                            if (responseParts.Count > 0)
                            {
                                var fullResponse = string.Join("\n\n", responseParts);
                                Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: Returning combined ASSISTANT response ({responseParts.Count} parts, total length: {fullResponse.Length})");
                                return fullResponse;
                            }
                        }
                    }
                    
                    // Fallback: if no text match, try timestamp-based matching
                    if (promptTime > 0)
                    {
                        foreach (var session in bubbleSessions.Values)
                        {
                            // Find USER bubble closest to prompt time
                            var userBubbles = session.Where(b => b.type == 1 && b.createdAt.HasValue)
                                .OrderBy(b => Math.Abs(b.createdAt!.Value - promptTime))
                                .ToList();
                            
                            if (userBubbles.Count > 0)
                            {
                                var bestUserBubble = userBubbles[0];
                                var userIndex = session.IndexOf(bestUserBubble);
                                
                                // Get next ASSISTANT bubble after this USER bubble
                                for (int i = userIndex + 1; i < session.Count; i++)
                                {
                                    var bubble = session[i];
                                    if (bubble.type != 1) // ASSISTANT
                                    {
                                        var text = bubble.bubble["text"]?.ToString()?.Trim();
                                        if (!string.IsNullOrEmpty(text))
                                            return text;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[SettingPreviewDialog] GetAssistantResponseFromBubbles: Error getting response from bubbles");
            }
            
            Logger.LogDebug($"[SettingPreviewDialog] GetAssistantResponseFromBubbles: No response found");
            return null;
        }
    }
}

