using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CursorBackup.Models;
using CursorBackup.Services;
using System.Windows;
using CursorBackup.Dialogs;

namespace CursorBackup.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CursorDataService _dataService;
        private readonly BackupService _backupService;
        private readonly ChatExportService _chatExportService;
        private readonly SettingsStorage _settingsStorage;
        private string _statusMessage = "Ready";
        private string _selectedBackupPath = string.Empty;
        private bool _isBusy = false;
        private string _progressMessage = string.Empty;
        private double _progressValue = 0;

        public MainViewModel()
        {
            _dataService = new CursorDataService();
            _backupService = new BackupService();
            _chatExportService = new ChatExportService();
            _settingsStorage = new SettingsStorage();
            
            // Create panel ViewModels
            CurrentSettingsPanelViewModel = new CurrentSettingsPanelViewModel(_dataService);
            BackupSettingsPanelViewModel = new BackupSettingsPanelViewModel(_dataService, _settingsStorage);
            
            // Keep collections for backward compatibility and footer actions
            Settings = CurrentSettingsPanelViewModel.Settings;
            Projects = CurrentSettingsPanelViewModel.Projects;
            Documentations = CurrentSettingsPanelViewModel.Documentations;
            LoadedSettings = BackupSettingsPanelViewModel.Settings;
            LoadedProjects = BackupSettingsPanelViewModel.Projects;
            LoadedDocumentations = BackupSettingsPanelViewModel.Documentations;

            // Commands for footer actions (Save, Merge, Export)
            SaveSelectedCommand = new RelayCommand(_ => SaveSelected(), _ => Settings.Any(s => s.IsSelected && s.IsAvailable));
            MergeSelectedCommand = new RelayCommand(_ => MergeSelected(), _ => LoadedSettings.Any(s => s.IsSelected && s.IsAvailable));
            ExportChatsCommand = new RelayCommand(_ => ExportChats(), _ => 
                LoadedSettings.Any(s => s.IsSelected && s.IsAvailable && s.Type == SettingType.ChatHistory) ||
                LoadedProjects.Any(p => p.ChatHistories.Any(c => c.IsSelected && c.IsAvailable)) ||
                Projects.Any(p => p.ChatHistories.Any(c => c.IsSelected && c.IsAvailable)));
            ConvertChatsCommand = new RelayCommand(_ => ConvertChats(), _ => LoadedSettings.Any(s => s.IsSelected && s.IsAvailable && s.Type == SettingType.ChatHistory));
            
            // Subscribe to collection changes to update command states
            Settings.CollectionChanged += (s, e) => CommandManager.InvalidateRequerySuggested();
            LoadedSettings.CollectionChanged += (s, e) => CommandManager.InvalidateRequerySuggested();
            Projects.CollectionChanged += (s, e) => CommandManager.InvalidateRequerySuggested();
            LoadedProjects.CollectionChanged += (s, e) => CommandManager.InvalidateRequerySuggested();
            
            // Subscribe to property changes in settings to update command states
            foreach (var setting in Settings)
            {
                setting.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CursorSettingItem.IsSelected))
                        CommandManager.InvalidateRequerySuggested();
                };
            }
            
            foreach (var setting in LoadedSettings)
            {
                setting.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CursorSettingItem.IsSelected))
                        CommandManager.InvalidateRequerySuggested();
                };
            }
            
            // Update SelectedBackupPath when backup panel loads
            BackupSettingsPanelViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BackupSettingsPanelViewModel.SelectedBackupPath))
                {
                    SelectedBackupPath = BackupSettingsPanelViewModel.SelectedBackupPath;
                }
            };
            
            // Subscribe to CurrentSettingsPanelViewModel collection changes
            CurrentSettingsPanelViewModel.Settings.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CursorSettingItem item in e.NewItems)
                    {
                        item.PropertyChanged += (sender, args) =>
                        {
                            if (args.PropertyName == nameof(CursorSettingItem.IsSelected))
                                CommandManager.InvalidateRequerySuggested();
                        };
                    }
                }
                CommandManager.InvalidateRequerySuggested();
            };
            
            // Subscribe to BackupSettingsPanelViewModel collection changes
            BackupSettingsPanelViewModel.Settings.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CursorSettingItem item in e.NewItems)
                    {
                        item.PropertyChanged += (sender, args) =>
                        {
                            if (args.PropertyName == nameof(CursorSettingItem.IsSelected))
                                CommandManager.InvalidateRequerySuggested();
                        };
                    }
                }
                CommandManager.InvalidateRequerySuggested();
            };
            
            BackupSettingsPanelViewModel.Projects.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CursorProject project in e.NewItems)
                    {
                        project.ChatHistories.CollectionChanged += (sender, args) => CommandManager.InvalidateRequerySuggested();
                        foreach (var chat in project.ChatHistories)
                        {
                            chat.PropertyChanged += (sender, args) =>
                            {
                                if (args.PropertyName == nameof(ChatHistoryItem.IsSelected))
                                    CommandManager.InvalidateRequerySuggested();
                            };
                        }
                    }
                }
                CommandManager.InvalidateRequerySuggested();
            };
            
            CurrentSettingsPanelViewModel.Projects.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CursorProject project in e.NewItems)
                    {
                        project.ChatHistories.CollectionChanged += (sender, args) => CommandManager.InvalidateRequerySuggested();
                        foreach (var chat in project.ChatHistories)
                        {
                            chat.PropertyChanged += (sender, args) =>
                            {
                                if (args.PropertyName == nameof(ChatHistoryItem.IsSelected))
                                    CommandManager.InvalidateRequerySuggested();
                            };
                        }
                    }
                }
                CommandManager.InvalidateRequerySuggested();
            };
        }

        // Panel ViewModels
        public CurrentSettingsPanelViewModel CurrentSettingsPanelViewModel { get; }
        public BackupSettingsPanelViewModel BackupSettingsPanelViewModel { get; }

        // Collections (references to panel ViewModels for backward compatibility)
        public ObservableCollection<CursorSettingItem> Settings { get; }
        public ObservableCollection<CursorProject> Projects { get; }
        public ObservableCollection<DocumentationGroup> Documentations { get; }
        public ObservableCollection<CursorSettingItem> LoadedSettings { get; }
        public ObservableCollection<CursorProject> LoadedProjects { get; }
        public ObservableCollection<DocumentationGroup> LoadedDocumentations { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string SelectedBackupPath
        {
            get => _selectedBackupPath;
            set
            {
                _selectedBackupPath = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set
            {
                _progressMessage = value;
                OnPropertyChanged();
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        // Commands (delegated to panel ViewModels or kept for footer actions)
        public ICommand LoadCurrentSettingsCommand => CurrentSettingsPanelViewModel.LoadCommand;
        public ICommand LoadFromBackupCommand => BackupSettingsPanelViewModel.LoadCommand;
        public ICommand SaveSelectedCommand { get; }
        public ICommand MergeSelectedCommand { get; }
        public ICommand SelectAllCommand => CurrentSettingsPanelViewModel.SelectAllCommand;
        public ICommand DeselectAllCommand => CurrentSettingsPanelViewModel.DeselectAllCommand;
        public ICommand SelectAllLoadedCommand => BackupSettingsPanelViewModel.SelectAllCommand;
        public ICommand DeselectAllLoadedCommand => BackupSettingsPanelViewModel.DeselectAllCommand;
        public ICommand ExportChatsCommand { get; }
        public ICommand ConvertChatsCommand { get; }

        private void LoadCurrentSettings()
        {
            Logger.LogInfo("[MainViewModel] LoadCurrentSettings: STARTED (OLD METHOD - should not be called)");
            try
            {
                StatusMessage = "Loading current settings...";
                Settings.Clear();
                Projects.Clear();

                var settings = _dataService.DiscoverCurrentSettings();
                foreach (var setting in settings)
                {
                    Settings.Add(setting);
                }

                var chatProjects = _dataService.DiscoverChatHistories();
                foreach (var project in chatProjects)
                {
                    Projects.Add(project);
                }

                var docGroups = _dataService.DiscoverDocumentationGroups();
                foreach (var group in docGroups)
                {
                    Documentations.Add(group);
                }

                StatusMessage = $"Loaded {Settings.Count} settings, {Projects.Count} projects, and {Documentations.Count} documentation groups";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
            }
        }

        private async void LoadFromBackup()
        {
            var lastPath = _settingsStorage.GetLastPath("LoadFromBackup", "");
            var dialog = new Dialogs.FolderBrowserDialog
            {
                Description = "Select backup folder to load settings from",
                Owner = System.Windows.Application.Current.MainWindow,
                InitialPath = lastPath
            };

            var result = dialog.ShowDialog();
            
            if (result == true && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                // Save last used path
                _settingsStorage.SetLastPath("LoadFromBackup", dialog.SelectedPath);
                
                try
                {
                    StatusMessage = "Loading settings from backup...";
                    LoadedSettings.Clear();
                    LoadedProjects.Clear();
                    SelectedBackupPath = dialog.SelectedPath;

                    var settings = _dataService.LoadSettingsFromBackup(dialog.SelectedPath);
                    foreach (var setting in settings)
                    {
                        LoadedSettings.Add(setting);
                    }

                    // Group chat histories by project for better display - merge projects with same name
                    var chatSettings = settings.Where(s => s.Type == SettingType.ChatHistory).ToList();
                    var projectsDict = new Dictionary<string, CursorProject>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var chat in chatSettings)
                    {
                        // Extract project name from chat name or path
                        var projectName = "Unknown";
                        var projectPath = chat.ProjectPath ?? "Unknown";
                        
                        // Try to extract project name from chat name (format: "Chat: ProjectName - ...")
                        if (chat.Name.StartsWith("Chat: ", StringComparison.OrdinalIgnoreCase))
                        {
                            var nameParts = chat.Name.Substring(6).Split(new[] { " - " }, StringSplitOptions.None);
                            if (nameParts.Length > 0)
                            {
                                projectName = nameParts[0].Trim();
                            }
                        }
                        
                        // If project path is not "Unknown", use it
                        if (projectPath != "Unknown")
                        {
                            var pathName = Path.GetFileName(projectPath) ?? "Unknown";
                            if (pathName != "Unknown")
                            {
                                projectName = pathName;
                            }
                        }
                        
                        // Use project name as key for merging (case-insensitive)
                        if (!projectsDict.ContainsKey(projectName))
                        {
                            projectsDict[projectName] = new CursorProject
                            {
                                ProjectPath = projectPath,
                                ProjectName = projectName
                            };
                        }
                        
                        var chatItem = new ChatHistoryItem
                        {
                            ChatId = chat.Id,
                            ChatName = chat.Name,
                            SourcePath = chat.SourcePath,
                            IsAvailable = chat.IsAvailable,
                            IsSelected = chat.IsSelected,
                            ProjectPath = projectPath
                        };
                        
                        // Check if chat already exists (by SourcePath) before adding
                        if (!projectsDict[projectName].ChatHistories.Any(ch => ch.SourcePath == chatItem.SourcePath))
                        {
                            projectsDict[projectName].ChatHistories.Add(chatItem);
                        }
                    }
                    
                    // Sort projects alphabetically
                    foreach (var project in projectsDict.Values.OrderBy(p => p.ProjectName))
                    {
                        LoadedProjects.Add(project);
                    }

                    StatusMessage = $"Loaded {LoadedSettings.Count} settings and {LoadedProjects.Count} projects from backup";
                    
                    if (LoadedSettings.Count == 0)
                    {
                        MessageBox.Show(
                            "No settings found in the selected folder. Please check if it contains Cursor backup files.",
                            "No Settings Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading backup: {ex.Message}";
                    MessageBox.Show(
                        $"Error loading backup: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            else if (result == false)
            {
                StatusMessage = "Backup loading cancelled";
            }
        }

        private async void SaveSelected()
        {
            var lastPath = _settingsStorage.GetLastPath("SaveSelected", "");
            var dialog = new Dialogs.FolderBrowserDialog
            {
                Description = "Select destination folder for backup",
                Owner = System.Windows.Application.Current.MainWindow,
                InitialPath = lastPath
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                // Save last used path
                _settingsStorage.SetLastPath("SaveSelected", dialog.SelectedPath!);
                
                try
                {
                    IsBusy = true;
                    ProgressValue = 0;
                    ProgressMessage = "Starting backup...";
                    StatusMessage = "Backing up selected settings...";
                    var selected = Settings.Where(s => s.IsSelected && s.IsAvailable).ToList();
                    
                    await Task.Run(() =>
                    {
                        var result = _backupService.BackupSettings(selected, dialog.SelectedPath!);
                        
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressValue = 100;
                            ProgressMessage = "Backup completed";
                            
                            if (result.Errors.Count == 0)
                            {
                                StatusMessage = $"Successfully backed up {result.SuccessCount} items";
                                MessageBox.Show(
                                    $"Successfully saved {result.SuccessCount} settings to:\n{dialog.SelectedPath}",
                                    "Backup Successful",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information
                                );
                            }
                            else
                            {
                                StatusMessage = $"Backed up {result.SuccessCount} items with {result.Errors.Count} errors";
                                var errorMsg = $"Backup completed: {result.SuccessCount} successful, {result.Errors.Count} errors.\n\nErrors:\n{string.Join("\n", result.Errors.Select(e => $"- {e.SettingName}: {e.ErrorMessage}"))}";
                                MessageBox.Show(
                                    errorMsg,
                                    "Backup Errors",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning
                                );
                            }
                        });
                    });
                    
                    await Task.Delay(500); // Show completion briefly
                    
                    IsBusy = false;
                    ProgressValue = 0;
                    ProgressMessage = string.Empty;
                }
                catch (Exception ex)
                {
                    IsBusy = false;
                    ProgressValue = 0;
                    ProgressMessage = string.Empty;
                    StatusMessage = $"Error during backup: {ex.Message}";
                    MessageBox.Show(
                        $"Error during backup:\n{ex.Message}",
                        "Backup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private async void MergeSelected()
        {
            if (string.IsNullOrEmpty(SelectedBackupPath))
            {
                MessageBox.Show(
                    "Please load settings from backup folder first!",
                    "No Backup Loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var selected = LoadedSettings.Where(s => s.IsSelected && s.IsAvailable).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one setting to merge!",
                    "No Settings Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            // Security confirmation dialog
            var confirmMsg = $"Are you sure you want to merge the selected {selected.Count} settings?\n\n" +
                           $"NOTE: This operation will NOT overwrite existing files, only add new ones.\n\n" +
                           $"Selected settings:\n" +
                           string.Join("\n", selected.Take(10).Select(s => $"  - {s.Name}")) +
                           (selected.Count > 10 ? $"\n  ... and {selected.Count - 10} more" : "");

            var confirmResult = MessageBox.Show(
                confirmMsg,
                "Security Confirmation - Merge",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No
            );

            if (confirmResult != MessageBoxResult.Yes)
            {
                StatusMessage = "Merge operation cancelled";
                return;
            }

            var lastPath = _settingsStorage.GetLastPath("MergeSelected", "");
            var dialog = new Dialogs.FolderBrowserDialog
            {
                Description = "Select target folder for merge",
                Owner = System.Windows.Application.Current.MainWindow,
                InitialPath = lastPath
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                // Save last used path
                _settingsStorage.SetLastPath("MergeSelected", dialog.SelectedPath!);
                
                try
                {
                    StatusMessage = "Merging settings...";
                    
                    var result = _backupService.MergeSettings(selected, dialog.SelectedPath!);

                    if (result.Conflicts.Count > 0)
                    {
                        var conflictMsg = $"{result.Conflicts.Count} conflicts found:\n" +
                                         string.Join("\n", result.Conflicts.Select(c => $"- {c.SettingName} ({c.ConflictType})")) +
                                         "\n\nThese files were NOT overwritten for safety.";
                        
                        MessageBox.Show(
                            conflictMsg,
                            "Merge Conflicts",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }

                    if (result.Errors.Count == 0 && result.Conflicts.Count == 0)
                    {
                        StatusMessage = $"Successfully merged {result.MergedCount} items";
                        MessageBox.Show(
                            $"Successfully merged {result.MergedCount} settings.",
                            "Merge Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    else
                    {
                        StatusMessage = $"Merged {result.MergedCount} items, {result.Conflicts.Count} conflicts, {result.Errors.Count} errors";
                    }

                    if (result.Errors.Count > 0)
                    {
                        var errorMsg = $"Errors:\n{string.Join("\n", result.Errors.Select(e => $"- {e.SettingName}: {e.ErrorMessage}"))}";
                        MessageBox.Show(
                            errorMsg,
                            "Merge Errors",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error during merge: {ex.Message}";
                    MessageBox.Show(
                        $"Error during merge: {ex.Message}",
                        "Merge Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private void SelectAll()
        {
            foreach (var setting in Settings.Where(s => s.IsAvailable))
            {
                setting.IsSelected = true;
            }
            
            // Also select all projects (which will auto-select their chat histories)
            foreach (var project in Projects)
            {
                project.IsSelected = true;
            }
        }

        private void DeselectAll()
        {
            foreach (var setting in Settings)
            {
                setting.IsSelected = false;
            }
            
            foreach (var project in Projects)
            {
                project.IsSelected = false;
            }
            
            foreach (var docGroup in Documentations)
            {
                docGroup.IsSelected = false;
            }
        }

        private void SelectAllLoaded()
        {
            foreach (var setting in LoadedSettings.Where(s => s.IsAvailable))
            {
                setting.IsSelected = true;
            }
            
            // Select all projects (which will auto-select their chat histories)
            foreach (var project in LoadedProjects)
            {
                project.IsSelected = true;
            }
        }

        private void DeselectAllLoaded()
        {
            foreach (var setting in LoadedSettings)
            {
                setting.IsSelected = false;
            }
            
            foreach (var project in LoadedProjects)
            {
                project.IsSelected = false;
            }
        }

        private void ExportChats()
        {
            // Collect chats from LoadedSettings (backup), LoadedProjects (backup), and Projects (current)
            var selectedChats = new List<CursorSettingItem>();
            
            // Debug: Count selected items
            var loadedSettingsCount = LoadedSettings.Count(s => s.IsSelected && s.IsAvailable && s.Type == SettingType.ChatHistory);
            var loadedProjectsChatCount = LoadedProjects.Sum(p => p.ChatHistories.Count(c => c.IsSelected && c.IsAvailable));
            var projectsChatCount = Projects.Sum(p => p.ChatHistories.Count(c => c.IsSelected && c.IsAvailable));
            
            System.Diagnostics.Debug.WriteLine($"ExportChats: LoadedSettings={loadedSettingsCount}, LoadedProjects={loadedProjectsChatCount}, Projects={projectsChatCount}");
            
            // From LoadedSettings (backup chats)
            selectedChats.AddRange(LoadedSettings.Where(s => s.IsSelected && s.IsAvailable && s.Type == SettingType.ChatHistory));
            
            // From LoadedProjects (backup chats) - convert ChatHistoryItem to CursorSettingItem
            foreach (var project in LoadedProjects)
            {
                foreach (var chatItem in project.ChatHistories.Where(c => c.IsSelected && c.IsAvailable))
                {
                    // Build source path for state.vscdb-based chats
                    string sourcePath = chatItem.SourcePath;
                    if (chatItem.IsFromStateDb && !string.IsNullOrEmpty(chatItem.StateDbPath))
                    {
                        // For state.vscdb-based chats, use a virtual path that indicates it's from state.vscdb
                        var chatName = chatItem.ChatName.Replace("Chat: ", "").Replace(" - ", "_");
                        if (chatName.Contains("Thread"))
                        {
                            sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}_thread.json");
                        }
                        else if (chatName.Contains("Session"))
                        {
                            sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}_session.json");
                        }
                        else
                        {
                            sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}.json");
                        }
                    }
                    
                    var chatSetting = new CursorSettingItem
                    {
                        Id = chatItem.ChatId,
                        Name = chatItem.ChatName,
                        Description = $"Chat history: {chatItem.ChatName}",
                        Category = "Chat History",
                        SourcePath = sourcePath,
                        DestinationPath = sourcePath,
                        IsAvailable = chatItem.IsAvailable,
                        IsSelected = chatItem.IsSelected,
                        Type = SettingType.ChatHistory,
                        ProjectPath = chatItem.ProjectPath ?? project.ProjectPath
                    };
                    
                    // Store state.vscdb info in Description for export service
                    if (chatItem.IsFromStateDb && !string.IsNullOrEmpty(chatItem.StateDbPath))
                    {
                        chatSetting.Description += $"\nStateDbPath: {chatItem.StateDbPath}\nIsFromStateDb: true";
                    }
                    
                    selectedChats.Add(chatSetting);
                }
            }
            
            // From Projects (current chats) - convert ChatHistoryItem to CursorSettingItem
            foreach (var project in Projects)
            {
                foreach (var chatItem in project.ChatHistories.Where(c => c.IsSelected && c.IsAvailable))
                {
                    // Build source path for state.vscdb-based chats
                    string sourcePath = chatItem.SourcePath;
                    if (chatItem.IsFromStateDb && !string.IsNullOrEmpty(chatItem.StateDbPath))
                    {
                        // For state.vscdb-based chats, use a virtual path that indicates it's from state.vscdb
                        var chatName = chatItem.ChatName.Replace("Chat: ", "").Replace(" - ", "_");
                        if (chatName.Contains("Thread"))
                        {
                            sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}_thread.json");
                        }
                        else if (chatName.Contains("Session"))
                        {
                            sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}_session.json");
                        }
                        else
                        {
                            sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}.json");
                        }
                    }
                    
                    var chatSetting = new CursorSettingItem
                    {
                        Id = chatItem.ChatId,
                        Name = chatItem.ChatName,
                        Description = $"Chat history: {chatItem.ChatName}",
                        Category = "Chat History",
                        SourcePath = sourcePath,
                        DestinationPath = sourcePath,
                        IsAvailable = chatItem.IsAvailable,
                        IsSelected = chatItem.IsSelected,
                        Type = SettingType.ChatHistory,
                        ProjectPath = chatItem.ProjectPath ?? project.ProjectPath
                    };
                    
                    // Store state.vscdb info in Description for export service
                    if (chatItem.IsFromStateDb && !string.IsNullOrEmpty(chatItem.StateDbPath))
                    {
                        chatSetting.Description += $"\nStateDbPath: {chatItem.StateDbPath}\nIsFromStateDb: true";
                    }
                    
                    selectedChats.Add(chatSetting);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"ExportChats: Total selected chats = {selectedChats.Count}");
            
            if (selectedChats.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one chat to export!",
                    "No Chats Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var lastPath = _settingsStorage.GetLastPath("ExportChats", "");
            var dialog = new Dialogs.FolderBrowserDialog
            {
                Description = "Select destination folder for chat export (in Markdown format)",
                Owner = System.Windows.Application.Current.MainWindow,
                InitialPath = lastPath
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                // Save last used path
                _settingsStorage.SetLastPath("ExportChats", dialog.SelectedPath!);
                
                try
                {
                    StatusMessage = "Exporting chats to Markdown format...";
                    var result = _chatExportService.ExportChatsToMarkdown(selectedChats, dialog.SelectedPath!);

                    if (result.Errors.Count == 0)
                    {
                        StatusMessage = $"Successfully exported {result.SuccessCount} chats";
                        MessageBox.Show(
                            $"Successfully exported {result.SuccessCount} chats in Markdown format.\n\n" +
                            $"Location: {dialog.SelectedPath}",
                            "Export Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    else
                    {
                        StatusMessage = $"Exported {result.SuccessCount} chats, {result.Errors.Count} errors";
                        var errorMsg = $"Exported {result.SuccessCount} chats.\n\n" +
                                      $"Errors:\n{string.Join("\n", result.Errors.Select(e => $"- {e.ChatName}: {e.ErrorMessage}"))}";
                        MessageBox.Show(
                            errorMsg,
                            "Export Partially Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error during export: {ex.Message}";
                    MessageBox.Show(
                        $"Error: {ex.Message}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private void ConvertChats()
        {
            // Same as export for now, but could be extended for other formats
            ExportChats();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}

