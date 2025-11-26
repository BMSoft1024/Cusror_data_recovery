using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace CursorBackup.Models
{
    /// <summary>
    /// Represents a Cursor project with its chat histories
    /// </summary>
    public class CursorProject : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string ProjectPath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public ObservableCollection<ChatHistoryItem> ChatHistories { get; set; } = new();
        
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                // When project is selected/deselected, select/deselect all its chat histories
                foreach (var chat in ChatHistories.Where(c => c.IsAvailable))
                {
                    chat.IsSelected = value;
                }
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ChatHistoryItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isAvailable;

        public string ChatId { get; set; } = string.Empty;
        public string ChatName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public bool IsFromStateDb { get; set; } = false;
        public string? StateDbPath { get; set; }
        public string? ProjectPath { get; set; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a documentation group with its documentation items
    /// </summary>
    public class DocumentationGroup : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string GroupName { get; set; } = string.Empty;
        public ObservableCollection<CursorSettingItem> Documentations { get; set; } = new();
        
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                // When group is selected/deselected, select/deselect all its documentations
                foreach (var doc in Documentations.Where(d => d.IsAvailable))
                {
                    doc.IsSelected = value;
                }
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

