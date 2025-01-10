using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UnrealExporter.App.Enums;

namespace UnrealExporter.UI
{
    public partial class PreviewWindow : Window
    {
        private ObservableCollection<FileItem> _fileItems = new ObservableCollection<FileItem>();
        private readonly string[] _allFiles;
        private readonly PreviewWindowType _windowType;

        public string[]? SelectedFiles { get; private set; }
        public string? SubmitMessage { get; private set; }
        public string? SelectedFile { get; private set; }

        public PreviewWindow(PreviewWindowType windowType, string[] files)
        {
            InitializeComponent();

            _windowType = windowType;
            _allFiles = files;

            ConfigureListViewColumns();

            if (windowType == PreviewWindowType.SelectUnrealProject)
            {
                HideSubmitMessage();
                InitializeSelectUnrealProject();
            }
            if (windowType == PreviewWindowType.SelectFilesToSubmit)
            {
                InitializeSelectFilesToSubmit();
            }
            else if (windowType == PreviewWindowType.PreviewSubmittedFiles)
            {
                HideSubmitMessage();
                InitializePreviewSubmittedFiles();
            }

            LoadAllFiles();
            lstPreview.ItemsSource = _fileItems;
        }

        private void ConfigureListViewColumns() 
        {
            var gridView = new GridView();

            if (_windowType == PreviewWindowType.SelectFilesToSubmit)
            {
                var checkboxColumn = new GridViewColumn
                {
                    Header = "Include",
                    Width = 60,
                    CellTemplate = (DataTemplate)FindResource("CheckBoxTemplate")
                };
                gridView.Columns.Add(checkboxColumn);
            }

            var fileNameColumn = new GridViewColumn
            {
                Header = "File name",
                DisplayMemberBinding = new System.Windows.Data.Binding("Name"),
                Width = double.NaN 
            };
            gridView.Columns.Add(fileNameColumn);

            lstPreview.View = gridView;
        }

        private void InitializePreviewSubmittedFiles()
        {
            lblList.Content = "Submitted files";
            btnCancel.Visibility = Visibility.Collapsed;    
            btnOK.Content = "OK";
        }

        private void InitializeSelectUnrealProject()
        {
            lblList.Content = "Select Unreal project";
            btnOK.Content = "Select";
        }

        private void InitializeSelectFilesToSubmit()
        {
            txtPlaceholder.Text = "[Wormhole] Exporting files from Unreal";
            lblList.Content = "Select files for submit";
            btnOK.Content = "Submit";
        }

        private void HideSubmitMessage()
        {
            lblSubmitMessage.Visibility = Visibility.Collapsed;
            txtSubmitMessage.Visibility = Visibility.Collapsed;
            txtPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void LoadAllFiles()
        {
            _fileItems.Clear();

            foreach (var file in _allFiles)
            {
                _fileItems.Add(new FileItem
                {
                    Name = Path.GetFileName(file),
                    FullName = file,
                    IsSelected = _windowType == PreviewWindowType.SelectFilesToSubmit
                });
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if(_windowType == PreviewWindowType.SelectUnrealProject)
            {
                SelectedFiles = _fileItems
                    .Where(item => item.IsSelected)
                    .Select(item => item.FullName ?? string.Empty)
                    .ToArray();

                if (string.IsNullOrWhiteSpace(txtSubmitMessage.Text))
                {
                    SubmitMessage = txtPlaceholder.Text;
                }
                else
                {
                    SubmitMessage = txtSubmitMessage.Text;
                }

                this.DialogResult = true;
                this.Close();
            }
            else if (_windowType == PreviewWindowType.SelectFilesToSubmit)
            {
                if(SelectedFile != null)
                {
                    this.DialogResult = true;
                    this.Close();
                }
            }
            else if(_windowType == PreviewWindowType.PreviewSubmittedFiles)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Hide the placeholder when the user focuses on the TextBox
            if (txtSubmitMessage.Text == "" || !string.IsNullOrWhiteSpace(txtSubmitMessage.Text))
            {
                txtPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Show the placeholder again when the user leaves the TextBox
            if (string.IsNullOrWhiteSpace(txtSubmitMessage.Text))
            {
                txtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void lstPreview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstPreview.SelectedItem is FileItem selectedItem)
            {
                SelectedFile = selectedItem.FullName;
            }
        }
    }

    public class FileItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string? Name { get; set; }

        public string? FullName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
