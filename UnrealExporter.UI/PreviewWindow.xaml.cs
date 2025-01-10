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

        /// <summary>
        /// Configures the columns displayed in the ListView based on the window type.
        /// </summary>
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

        /// <summary>
        /// Initializes the window for previewing submitted files.
        /// </summary>
        private void InitializePreviewSubmittedFiles()
        {
            lblList.Content = "Submitted files";
            btnCancel.Visibility = Visibility.Collapsed;
            btnOK.Content = "OK";
        }

        /// <summary>
        /// Initializes the window for selecting an Unreal project.
        /// </summary>
        private void InitializeSelectUnrealProject()
        {
            lblList.Content = "Select Unreal project";
            btnOK.Content = "Select";
        }

        /// <summary>
        /// Initializes the window for selecting files to submit.
        /// </summary>
        private void InitializeSelectFilesToSubmit()
        {
            txtPlaceholder.Text = "[Wormhole] Exporting files from Unreal";
            lblList.Content = "Select files for submit";
            btnOK.Content = "Submit";
        }

        /// <summary>
        /// Hides the submit message controls.
        /// </summary>
        private void HideSubmitMessage()
        {
            lblSubmitMessage.Visibility = Visibility.Collapsed;
            txtSubmitMessage.Visibility = Visibility.Collapsed;
            txtPlaceholder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Loads all files into the ListView.
        /// </summary>
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

        /// <summary>
        /// Handles the OK button click event.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">Event arguments</param>
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (_windowType == PreviewWindowType.SelectUnrealProject)
            {
                if (SelectedFile != null)
                {
                    this.DialogResult = true;
                }
                else
                {
                    this.DialogResult = false;
                }
            }
            else if (_windowType == PreviewWindowType.SelectFilesToSubmit)
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

                if (SelectedFiles == null || SubmitMessage == null)
                {
                    this.DialogResult = false;
                }
                else
                {
                    this.DialogResult = true;
                }

                this.Close();
                return;
            }

            this.Close();
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">Event arguments</param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Handles the text box got focus event.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">Event arguments</param>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSubmitMessage.Text == "" || !string.IsNullOrWhiteSpace(txtSubmitMessage.Text))
            {
                txtPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handles the text box lost focus event.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">Event arguments</param>
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSubmitMessage.Text))
            {
                txtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handles the ListView selection changed event.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">Event arguments</param>
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

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}