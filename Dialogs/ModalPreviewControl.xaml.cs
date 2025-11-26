using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CursorBackup.Models;
using CursorBackup.Services;

namespace CursorBackup.Dialogs
{
    public partial class ModalPreviewControl : UserControl, INotifyPropertyChanged
    {
        private string _settingName = string.Empty;
        private string _description = string.Empty;
        private object? _previewContent;
        private bool _isLoading;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CloseRequested;

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

        public object? PreviewContent
        {
            get => _previewContent;
            set
            {
                _previewContent = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public ModalPreviewControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public async void LoadPreview(CursorSettingItem setting)
        {
            SettingName = setting.Name;
            Description = setting.Description;
            IsLoading = true;

            try
            {
                // Load preview content (reuse SettingPreviewDialog logic)
                var dialog = new SettingPreviewDialog(setting);
                
                // Wait a bit for dialog to load
                await System.Threading.Tasks.Task.Delay(100);
                
                // Get the content from dialog
                PreviewContent = dialog.PreviewContent;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ModalPreviewControl] LoadPreview: Error loading preview");
                PreviewContent = new TextBlock
                {
                    Text = $"Error loading content: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


