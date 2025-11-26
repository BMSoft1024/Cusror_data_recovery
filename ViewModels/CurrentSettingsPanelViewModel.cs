using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CursorBackup.Models;
using CursorBackup.Services;
using static CursorBackup.Services.Logger;

namespace CursorBackup.ViewModels
{
    /// <summary>
    /// ViewModel for the Current Settings panel
    /// </summary>
    public class CurrentSettingsPanelViewModel : SettingsPanelViewModelBase
    {
        private readonly CursorDataService _dataService;

        public CurrentSettingsPanelViewModel(CursorDataService dataService)
        {
            _dataService = dataService;
            LoadCommand = new RelayCommand(_ => LoadCurrentSettings());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeselectAllCommand = new RelayCommand(_ => DeselectAll());
        }

        public override string PanelTitle => "Current Cursor Settings";
        public override string LoadButtonText => "Load Current Settings";
        public override string LoadButtonToolTip => "Load current Cursor settings: global settings, keybindings, chat histories";

        public override ICommand LoadCommand { get; }
        public override ICommand SelectAllCommand { get; }
        public override ICommand DeselectAllCommand { get; }

        private async void LoadCurrentSettings()
        {
            Logger.LogInfo("[CurrentSettingsPanelViewModel] LoadCurrentSettings: STARTED");
            try
            {
                IsBusy = true;
                ProgressValue = 0;
                ProgressMessage = "Starting load...";
                
                // Update MainViewModel IsBusy too
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                {
                    mainVM.IsBusy = true;
                    mainVM.ProgressValue = 0;
                    mainVM.ProgressMessage = "Starting load...";
                }
                
                Logger.LogInfo("[CurrentSettingsPanelViewModel] LoadCurrentSettings: Clearing collections");
                Settings.Clear();
                Projects.Clear();
                Documentations.Clear();

                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressValue = 10;
                        ProgressMessage = "Discovering settings...";
                    });
                    
                    var settings = _dataService.DiscoverCurrentSettings();
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var setting in settings)
                        {
                            Settings.Add(setting);
                        }
                        ProgressValue = 40;
                        ProgressMessage = "Discovering chat histories...";
                        
                        // Update MainViewModel progress
                        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                        {
                            mainVM.ProgressValue = 40;
                            mainVM.ProgressMessage = "Discovering chat histories...";
                        }
                    });
                    
                    var chatProjects = _dataService.DiscoverChatHistories();
                    Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Discovered {chatProjects.Count} chat projects");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Adding {chatProjects.Count} projects to UI collection");
                        foreach (var project in chatProjects)
                        {
                            Projects.Add(project);
                            Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Added project '{project.ProjectName}' with {project.ChatHistories.Count} chat histories");
                            foreach (var chat in project.ChatHistories)
                            {
                                Logger.LogDebug($"[CurrentSettingsPanelViewModel] LoadCurrentSettings:   - Chat: {chat.ChatName} (Available: {chat.IsAvailable})");
                            }
                        }
                        Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Total projects in UI collection: {Projects.Count}");
                        Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Projects collection type: {Projects.GetType().Name}");
                        Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Projects is ObservableCollection: {Projects is System.Collections.ObjectModel.ObservableCollection<CursorProject>}");
                        
                        // Log Curor_data_recovery project specifically
                        var curorProject = Projects.FirstOrDefault(p => p.ProjectName.Contains("Curor_data_recovery", StringComparison.OrdinalIgnoreCase));
                        if (curorProject != null)
                        {
                            Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Found Curor_data_recovery project with {curorProject.ChatHistories.Count} chat histories");
                            foreach (var chat in curorProject.ChatHistories)
                            {
                                Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings:   - Chat: {chat.ChatName} (Available: {chat.IsAvailable})");
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Curor_data_recovery project NOT found in Projects collection!");
                        }
                        
                        // Force UI update
                        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                        
                        // Auto-expand Chat Histories section if we have projects
                        if (Projects.Count > 0)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var mainWindow = Application.Current.MainWindow;
                                if (mainWindow != null)
                                {
                                    var expander = mainWindow.FindName("ChatHistoriesExpander") as Expander;
                                    if (expander != null)
                                    {
                                        Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Found ChatHistoriesExpander, IsExpanded={expander.IsExpanded}");
                                        if (!expander.IsExpanded)
                                        {
                                            expander.IsExpanded = true;
                                            Logger.LogInfo("[CurrentSettingsPanelViewModel] LoadCurrentSettings: Auto-expanded Chat Histories section");
                                            
                                            // Force UI update after expanding
                                            expander.UpdateLayout();
                                            
                                            // Check ItemsControl
                                            var itemsControl = FindItemsControlInExpander(expander);
                                            if (itemsControl != null)
                                            {
                                                Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: ItemsControl found, ItemsSource type: {itemsControl.ItemsSource?.GetType().Name ?? "null"}");
                                                if (itemsControl.ItemsSource is System.Collections.IEnumerable enumerable)
                                                {
                                                    int count = 0;
                                                    foreach (var item in enumerable) count++;
                                                    Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: ItemsControl.ItemsSource has {count} items");
                                                }
                                            }
                                            else
                                            {
                                                Logger.LogWarning("[CurrentSettingsPanelViewModel] LoadCurrentSettings: ItemsControl NOT found in Expander!");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning("[CurrentSettingsPanelViewModel] LoadCurrentSettings: ChatHistoriesExpander NOT found!");
                                    }
                                }
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                        
                        ProgressValue = 70;
                        ProgressMessage = "Discovering documentation...";
                        
                        // Update MainViewModel progress
                        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                        {
                            mainVM.ProgressValue = 70;
                            mainVM.ProgressMessage = "Discovering documentation...";
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal);
                    
                    var docGroups = _dataService.DiscoverDocumentationGroups();
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var docGroup in docGroups)
                        {
                            Documentations.Add(docGroup);
                        }
                        Logger.LogInfo($"[CurrentSettingsPanelViewModel] LoadCurrentSettings: Added {docGroups.Count} documentation groups");
                        ProgressValue = 100;
                        ProgressMessage = "Loading completed";
                        
                        // Update MainViewModel progress
                        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                        {
                            mainVM.ProgressValue = 100;
                            mainVM.ProgressMessage = "Loading completed";
                        }
                    });
                });
                
                await Task.Delay(500); // Show completion briefly
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[CurrentSettingsPanelViewModel] LoadCurrentSettings: ERROR occurred");
                IsBusy = false;
                ProgressValue = 0;
                ProgressMessage = string.Empty;
                MessageBox.Show(
                    $"Error loading settings:\n{ex.Message}",
                    "Loading Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsBusy = false;
                
                // Update MainViewModel IsBusy too
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                {
                    mainVM.IsBusy = false;
                    mainVM.ProgressValue = 0;
                    mainVM.ProgressMessage = string.Empty;
                }
                
                Logger.LogInfo("[CurrentSettingsPanelViewModel] LoadCurrentSettings: COMPLETED");
            }
        }
        
        private ItemsControl? FindItemsControlInExpander(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ItemsControl itemsControl)
                    return itemsControl;
                var found = FindItemsControlInExpander(child);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

