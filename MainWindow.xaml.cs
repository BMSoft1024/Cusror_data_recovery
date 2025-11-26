using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CursorBackup.Dialogs;
using CursorBackup.Models;
using CursorBackup.ViewModels;
using CursorBackup.Services;

namespace CursorBackup
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            
            // TODO: Automatic loading of current settings on startup
            this.Loaded += MainWindow_Loaded;
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Automatically load current settings on startup
            if (DataContext is MainViewModel viewModel)
            {
                // Delay to ensure UI is fully loaded
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        if (viewModel.LoadCurrentSettingsCommand?.CanExecute(null) == true)
                        {
                            viewModel.LoadCurrentSettingsCommand.Execute(null);
                        }
                    }));
            }
        }

        // Event handlers for preview buttons in MainWindow (left panel - Current Settings)
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button)
                {
                    var setting = button.Tag as CursorSettingItem;
                    if (setting != null)
                    {
                        var dialog = new SettingPreviewDialog(setting)
                        {
                            Owner = this
                        };
                        dialog.ShowDialog();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"PreviewButton_Click: Tag is not CursorSettingItem. Tag type: {button.Tag?.GetType().Name ?? "null"}");
                        MessageBox.Show("Failed to open preview: setting data not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewButton_Click error: {ex.Message}");
                MessageBox.Show($"Error opening preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewChatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button)
                {
                    var chatItem = button.Tag as ChatHistoryItem;
                    if (chatItem != null)
                    {
                        // Build source path for state.vscdb-based chats
                        string sourcePath = chatItem.SourcePath;
                        string description = $"Chat history: {chatItem.ChatName}";
                        
                        if (chatItem.IsFromStateDb && !string.IsNullOrEmpty(chatItem.StateDbPath))
                        {
                            // For state.vscdb-based chats, store info in Description
                            description += $"\nStateDbPath: {chatItem.StateDbPath}\nIsFromStateDb: true";
                            
                            // Build virtual path for state.vscdb chats
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
                                sourcePath = Path.Combine(Path.GetDirectoryName(chatItem.StateDbPath) ?? "", $"{chatName}_combined_prompts.json");
                            }
                        }
                        
                        var setting = new CursorSettingItem
                        {
                            Id = chatItem.ChatId,
                            Name = chatItem.ChatName,
                            Description = description,
                            Category = "Chat History",
                            SourcePath = sourcePath,
                            DestinationPath = sourcePath,
                            IsAvailable = chatItem.IsAvailable,
                            Type = SettingType.ChatHistory,
                            ProjectPath = chatItem.ProjectPath
                        };

                        var dialog = new SettingPreviewDialog(setting)
                        {
                            Owner = this
                        };
                        dialog.ShowDialog();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"PreviewChatButton_Click: Tag is not ChatHistoryItem. Tag type: {button.Tag?.GetType().Name ?? "null"}");
                        MessageBox.Show("Failed to open preview: chat data not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewChatButton_Click error: {ex.Message}");
                MessageBox.Show($"Error opening preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChatHistoriesExpander_Expanded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("[MainWindow] ChatHistoriesExpander_Expanded: Expander opened");
            // Force UI refresh when expander opens
            if (sender is Expander expander)
            {
                // Wait a bit for the expander to fully expand
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        var itemsControl = FindVisualChild<ItemsControl>(expander);
                        if (itemsControl != null)
                        {
                            itemsControl.UpdateLayout();
                            
                            // Log how many items are in the UI
                            var dataContext = itemsControl.DataContext;
                            Logger.LogInfo($"[MainWindow] ChatHistoriesExpander_Expanded: ItemsControl DataContext type: {dataContext?.GetType().Name ?? "null"}");
                            
                            if (DataContext is MainViewModel mainVM)
                            {
                                Logger.LogInfo($"[MainWindow] ChatHistoriesExpander_Expanded: MainViewModel.Projects.Count: {mainVM.Projects.Count}");
                                
                                // Check ItemsSource binding
                                Logger.LogInfo($"[MainWindow] ChatHistoriesExpander_Expanded: ItemsControl.ItemsSource: {itemsControl.ItemsSource?.GetType().Name ?? "null"}");
                                if (itemsControl.ItemsSource is System.Collections.IEnumerable enumerable)
                                {
                                    int count = 0;
                                    foreach (var item in enumerable) count++;
                                    Logger.LogInfo($"[MainWindow] ChatHistoriesExpander_Expanded: ItemsControl.ItemsSource has {count} items");
                                }
                                
                                // Count visual children (actual UI elements)
                                int visualChildrenCount = 0;
                                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(itemsControl); i++)
                                {
                                    visualChildrenCount++;
                                }
                                Logger.LogInfo($"[MainWindow] ChatHistoriesExpander_Expanded: ItemsControl has {visualChildrenCount} visual children");
                                
                                // Log first few projects
                                int projectCount = 0;
                                foreach (var project in mainVM.Projects.Take(5))
                                {
                                    Logger.LogInfo($"[MainWindow] ChatHistoriesExpander_Expanded: Project #{projectCount + 1}: '{project.ProjectName}' has {project.ChatHistories.Count} chat histories");
                                    projectCount++;
                                }
                            }
                        }
                        else
                        {
                            Logger.LogWarning("[MainWindow] ChatHistoriesExpander_Expanded: ItemsControl NOT found!");
                        }
                    }));
            }
        }

        private void ChatHistoriesExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("[MainWindow] ChatHistoriesExpander_Collapsed: Expander closed");
        }
        
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void DocumentationsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            // Handled by SettingsPanel UserControl
        }

        private void DocumentationsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            // Handled by SettingsPanel UserControl
        }
    }
}
