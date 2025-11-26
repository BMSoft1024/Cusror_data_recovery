using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CursorBackup.Models;

namespace CursorBackup.ViewModels
{
    /// <summary>
    /// Base ViewModel for settings panels (both current and backup)
    /// </summary>
    public abstract class SettingsPanelViewModelBase : INotifyPropertyChanged
    {
        private bool _isBusy = false;
        private string _progressMessage = string.Empty;
        private double _progressValue = 0;

        protected SettingsPanelViewModelBase()
        {
            Settings = new ObservableCollection<CursorSettingItem>();
            Projects = new ObservableCollection<CursorProject>();
            Documentations = new ObservableCollection<DocumentationGroup>();
        }

        public ObservableCollection<CursorSettingItem> Settings { get; }
        public ObservableCollection<CursorProject> Projects { get; }
        public ObservableCollection<DocumentationGroup> Documentations { get; }

        public abstract ICommand LoadCommand { get; }
        public abstract ICommand SelectAllCommand { get; }
        public abstract ICommand DeselectAllCommand { get; }

        public abstract string PanelTitle { get; }
        public abstract string LoadButtonText { get; }
        public abstract string LoadButtonToolTip { get; }

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

        public virtual void SelectAll()
        {
            foreach (var setting in Settings.Where(s => s.IsAvailable))
            {
                setting.IsSelected = true;
            }
            
            foreach (var project in Projects)
            {
                project.IsSelected = true;
            }
            
            foreach (var docGroup in Documentations)
            {
                docGroup.IsSelected = true;
            }
        }

        public virtual void DeselectAll()
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

