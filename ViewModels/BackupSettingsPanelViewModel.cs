using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CursorBackup.Dialogs;
using CursorBackup.Models;
using CursorBackup.Services;

namespace CursorBackup.ViewModels
{
    /// <summary>
    /// ViewModel for the Backup Settings panel
    /// </summary>
    public class BackupSettingsPanelViewModel : SettingsPanelViewModelBase
    {
        private readonly CursorDataService _dataService;
        private readonly SettingsStorage _settingsStorage;
        private string _selectedBackupPath = string.Empty;

        public BackupSettingsPanelViewModel(CursorDataService dataService, SettingsStorage settingsStorage)
        {
            _dataService = dataService;
            _settingsStorage = settingsStorage;
            LoadCommand = new RelayCommand(_ => LoadFromBackup());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeselectAllCommand = new RelayCommand(_ => DeselectAll());
        }

        public override string PanelTitle => "Settings from Backup";
        public override string LoadButtonText => "Load from Backup Folder";
        public override string LoadButtonToolTip => "Load settings from backup folder";

        public override ICommand LoadCommand { get; }
        public override ICommand SelectAllCommand { get; }
        public override ICommand DeselectAllCommand { get; }

        public string SelectedBackupPath
        {
            get => _selectedBackupPath;
            set
            {
                _selectedBackupPath = value;
                OnPropertyChanged();
            }
        }

        private async void LoadFromBackup()
        {
            var lastPath = _settingsStorage.GetLastPath("LoadFromBackup", "");
            var dialog = new FolderBrowserDialog
            {
                Description = "Select backup folder to load settings from",
                Owner = Application.Current.MainWindow,
                InitialPath = lastPath
            };

            var result = dialog.ShowDialog();
            
            if (result == true && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                _settingsStorage.SetLastPath("LoadFromBackup", dialog.SelectedPath);
                
                try
                {
                    IsBusy = true;
                    ProgressValue = 0;
                    ProgressMessage = "Loading backup...";
                    
                    Settings.Clear();
                    Projects.Clear();
                    Documentations.Clear();
                    SelectedBackupPath = dialog.SelectedPath;

                    await Task.Run(() =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressValue = 20;
                            ProgressMessage = "Loading settings from backup...";
                        });
                        
                        var settings = _dataService.LoadSettingsFromBackup(dialog.SelectedPath);
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var setting in settings)
                            {
                                // Don't add documentation items to Settings - they go to Documentations
                                if (setting.Type != SettingType.Documentation)
                                {
                                    Settings.Add(setting);
                                }
                            }
                            ProgressValue = 50;
                            ProgressMessage = "Loading documentation...";
                        });
                        
                        // Also load documentation groups from backup
                        var globalStorageStateDbPath = Path.Combine(dialog.SelectedPath, "User", "globalStorage", "state.vscdb");
                        if (!File.Exists(globalStorageStateDbPath))
                        {
                            globalStorageStateDbPath = Path.Combine(dialog.SelectedPath, "globalStorage", "state.vscdb");
                        }
                        
                        if (File.Exists(globalStorageStateDbPath))
                        {
                            var docGroups = _dataService.DiscoverDocumentationGroupsFromBackup(globalStorageStateDbPath);
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (var group in docGroups)
                                {
                                    Documentations.Add(group);
                                }
                            });
                        }
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressValue = 60;
                            ProgressMessage = "Grouping chat histories...";
                        });

                        // Group chat histories by project for better display - merge projects with same name
                        var chatSettings = settings.Where(s => s.Type == SettingType.ChatHistory).ToList();
                        var projectsDict = new Dictionary<string, CursorProject>(StringComparer.OrdinalIgnoreCase);
                        
                        foreach (var chat in chatSettings)
                        {
                            var projectName = "Unknown";
                            var projectPath = chat.ProjectPath ?? "Unknown";
                            
                            if (chat.Name.StartsWith("Chat: ", StringComparison.OrdinalIgnoreCase))
                            {
                                var nameParts = chat.Name.Substring(6).Split(new[] { " - " }, StringSplitOptions.None);
                                if (nameParts.Length > 0)
                                {
                                    projectName = nameParts[0].Trim();
                                }
                            }
                            
                            if (projectPath != "Unknown")
                            {
                                var pathName = Path.GetFileName(projectPath) ?? "Unknown";
                                if (pathName != "Unknown")
                                {
                                    projectName = pathName;
                                }
                            }
                            
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
                            
                            if (!projectsDict[projectName].ChatHistories.Any(ch => ch.SourcePath == chatItem.SourcePath))
                            {
                                projectsDict[projectName].ChatHistories.Add(chatItem);
                            }
                        }
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var project in projectsDict.Values.OrderBy(p => p.ProjectName))
                            {
                                Projects.Add(project);
                            }
                            ProgressValue = 100;
                            ProgressMessage = "Backup loading completed";
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
                    MessageBox.Show(
                        $"Error loading backup: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }
    }
}

