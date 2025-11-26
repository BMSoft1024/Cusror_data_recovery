using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CursorBackup.Models
{
    /// <summary>
    /// Represents a single Cursor setting item with its availability status
    /// </summary>
    public class CursorSettingItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isAvailable;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        
        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
        
        public SettingType Type { get; set; }
        public string? ProjectPath { get; set; } // For project-specific settings

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum SettingType
    {
        GlobalSettings,
        Keybindings,
        StateDatabase,
        GlobalStorage,
        LanguagePacks,
        WorkspaceSettings,
        ChatHistory,
        ExtensionsList,
        Rules,
        Documentation,
        Other
    }
}

