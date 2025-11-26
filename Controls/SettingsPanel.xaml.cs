using System;
using System.Windows;
using System.Windows.Controls;
using CursorBackup.Dialogs;
using CursorBackup.Models;

namespace CursorBackup.Controls
{
    /// <summary>
    /// Interaction logic for SettingsPanel.xaml
    /// </summary>
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();
        }

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
                            Owner = Window.GetWindow(this)
                        };
                        dialog.ShowDialog();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"PreviewButton_Click: Tag is not CursorSettingItem. Tag type: {button.Tag?.GetType().Name ?? "null"}");
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
                        var setting = new CursorSettingItem
                        {
                            Id = chatItem.ChatId,
                            Name = chatItem.ChatName,
                            Description = $"Chat history: {chatItem.ChatName}",
                            Category = "Chat History",
                            SourcePath = chatItem.SourcePath,
                            DestinationPath = chatItem.SourcePath,
                            IsAvailable = chatItem.IsAvailable,
                            Type = SettingType.ChatHistory
                        };

                        var dialog = new SettingPreviewDialog(setting)
                        {
                            Owner = Window.GetWindow(this)
                        };
                        dialog.ShowDialog();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"PreviewChatButton_Click: Tag is not ChatHistoryItem. Tag type: {button.Tag?.GetType().Name ?? "null"}");
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
            if (DocumentationsExpander != null)
            {
                DocumentationsExpander.IsExpanded = false;
            }
        }

        private void ChatHistoriesExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            // No action needed when collapsed
        }

        private void DocumentationsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (ChatHistoriesExpander != null)
            {
                ChatHistoriesExpander.IsExpanded = false;
            }
        }

        private void DocumentationsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            // No action needed when collapsed
        }
    }
}

