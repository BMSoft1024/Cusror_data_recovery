using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CursorBackup.Dialogs
{
    public partial class FolderBrowserDialog : Window
    {
        public string? SelectedPath { get; private set; }
        public string Description { get; set; } = "Select a folder";
        public string? InitialPath { get; set; }

        public FolderBrowserDialog()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += FolderBrowserDialog_Loaded;
            ContentRendered += FolderBrowserDialog_ContentRendered;
        }

        private void FolderBrowserDialog_ContentRendered(object? sender, EventArgs e)
        {
            // Fallback: if TreeView is still empty, load drives
            if (FolderTreeView.Items.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("ContentRendered: TreeView is empty, loading drives...");
                LoadDrives();
            }
        }

        private void FolderBrowserDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Always ensure drives are loaded
                if (!string.IsNullOrEmpty(InitialPath) && Directory.Exists(InitialPath))
                {
                    LoadInitialPath(InitialPath);
                }
                else if (!string.IsNullOrEmpty(InitialPath))
                {
                    // InitialPath provided but doesn't exist, try parent
                    var parentDir = Path.GetDirectoryName(InitialPath);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        LoadInitialPath(parentDir);
                    }
                    else
                    {
                        LoadDrives();
                    }
                }
                else
                {
                    // No initial path, just load drives normally
                    LoadDrives();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FolderBrowserDialog_Loaded: {ex.Message}");
                // Fallback: always load drives
                LoadDrives();
            }
        }

        private void LoadDrives()
        {
            try
            {
                FolderTreeView.Items.Clear();
                
                var drives = DriveInfo.GetDrives();
                System.Diagnostics.Debug.WriteLine($"Found {drives.Length} drives");
                
                foreach (var drive in drives)
                {
                    try
                    {
                        if (drive.IsReady)
                        {
                            var volumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
                            var item = new TreeViewItem
                            {
                                Header = $"{drive.Name} ({volumeLabel})",
                                Tag = drive.RootDirectory.FullName,
                                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                                IsExpanded = false,
                                IsSelected = false,
                                Focusable = true
                            };
                            var placeholder = new TreeViewItem
                            {
                                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                                Tag = null
                            };
                            item.Items.Add(placeholder); // Placeholder for lazy loading
                            item.Expanded += TreeViewItem_Expanded;
                            FolderTreeView.Items.Add(item);
                            System.Diagnostics.Debug.WriteLine($"Added drive: {drive.Name}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Drive {drive.Name} is not ready");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error adding drive {drive.Name}: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Total items in TreeView: {FolderTreeView.Items.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadDrives: {ex.Message}");
                MessageBox.Show($"Error loading drives: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;
            e.Handled = false; // Don't prevent default expansion behavior
            
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Tag == null)
            {
                item.Items.Clear();
                try
                {
                    var path = (string)item.Tag!;
                    System.Diagnostics.Debug.WriteLine($"Expanding: {path}");
                    
                    var dirs = Directory.GetDirectories(path);
                    System.Diagnostics.Debug.WriteLine($"Found {dirs.Length} directories");
                    
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            var subItem = new TreeViewItem
                            {
                                Header = dirInfo.Name,
                                Tag = dir,
                                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                                Focusable = true,
                                IsSelected = false
                            };
                            
                            try
                            {
                                if (Directory.GetDirectories(dir).Length > 0)
                                {
                                    var subPlaceholder = new TreeViewItem
                                    {
                                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                                        Tag = null
                                    };
                                    subItem.Items.Add(subPlaceholder);
                                }
                            }
                            catch { }
                            
                            subItem.Expanded += TreeViewItem_Expanded;
                            item.Items.Add(subItem);
                            System.Diagnostics.Debug.WriteLine($"Added subdirectory: {dirInfo.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error adding subdirectory: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error expanding directory: {ex.Message}");
                }
            }
        }


        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string? path = null;
            
            if (FolderTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string selectedPath)
            {
                path = selectedPath;
            }
            else if (!string.IsNullOrEmpty(SelectedPathTextBox.Text))
            {
                path = SelectedPathTextBox.Text;
            }
            
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                SelectedPath = path;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a valid folder", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LoadInitialPath(string initialPath)
        {
            FolderTreeView.Items.Clear();
            
            try
            {
                var dirInfo = new DirectoryInfo(initialPath);
                var currentPath = initialPath;
                var pathParts = new List<string>();
                
                // Build path hierarchy
                while (currentPath != null && Directory.Exists(currentPath))
                {
                    pathParts.Insert(0, currentPath);
                    currentPath = Path.GetDirectoryName(currentPath);
                }

                // Start from root drive
                if (pathParts.Count > 0)
                {
                    var rootPath = pathParts[0];
                    var rootDrive = new DriveInfo(Path.GetPathRoot(rootPath) ?? "C:\\");
                    
                    var rootItem = new TreeViewItem
                    {
                        Header = $"{rootDrive.Name} ({rootDrive.VolumeLabel})",
                        Tag = rootDrive.RootDirectory.FullName,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                        Focusable = true
                    };
                    
                    // Expand and load path
                    rootItem.IsExpanded = true;
                    ExpandPath(rootItem, pathParts);
                    
                    FolderTreeView.Items.Add(rootItem);
                    
                    // Select the final item - use Dispatcher to ensure UI is ready
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var selectedItem = FindItemByPath(rootItem, initialPath);
                        if (selectedItem != null)
                        {
                            selectedItem.IsSelected = true;
                            selectedItem.Focus();
                            selectedItem.BringIntoView();
                            SelectedPath = initialPath;
                            SelectedPathTextBox.Text = initialPath;
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    LoadDrives();
                }
            }
            catch
            {
                LoadDrives();
            }
        }

        private void ExpandPath(TreeViewItem item, List<string> pathParts)
        {
            if (pathParts.Count <= 1)
                return;

            var currentPath = (string)item.Tag!;
            var nextPath = pathParts[1];
            
            // Load children
            TreeViewItem_Expanded(item, new RoutedEventArgs());
            
            // Find and expand next item
            foreach (TreeViewItem child in item.Items)
            {
                if (child.Tag is string childPath && nextPath.StartsWith(childPath, StringComparison.OrdinalIgnoreCase))
                {
                    child.IsExpanded = true;
                    ExpandPath(child, pathParts.Skip(1).ToList());
                    break;
                }
            }
        }

        private TreeViewItem? FindItemByPath(TreeViewItem item, string targetPath)
        {
            if (item.Tag is string itemPath && itemPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            foreach (TreeViewItem child in item.Items)
            {
                var found = FindItemByPath(child, targetPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"SelectedItemChanged: NewValue = {e.NewValue?.GetType().Name ?? "null"}");
                
                // Try to get the selected item
                TreeViewItem? selectedItem = null;
                
                if (e.NewValue is TreeViewItem item)
                {
                    selectedItem = item;
                }
                else if (FolderTreeView.SelectedItem is TreeViewItem selected)
                {
                    selectedItem = selected;
                }
                
                if (selectedItem != null)
                {
                    System.Diagnostics.Debug.WriteLine($"SelectedItem Tag: {selectedItem.Tag}");
                    
                    if (selectedItem.Tag is string path)
                    {
                        System.Diagnostics.Debug.WriteLine($"Setting path to: {path}");
                        SelectedPath = path;
                        SelectedPathTextBox.Text = path;
                        return;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("No valid selection found");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SelectedItemChanged: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}

