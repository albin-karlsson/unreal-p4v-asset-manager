using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace UnrealExporter.UI
{
    public partial class PreviewWindow : Window
    {
        private ObservableCollection<FileItem> _fileItems = new ObservableCollection<FileItem>();
        private string[] _allFiles;

        public string[] FilesToExport { get; private set; }
        public string SubmitMessage { get; private set; }

        public PreviewWindow(string[] filesToExport)
        {
            InitializeComponent();

            _allFiles = filesToExport;

            LoadAllFiles(); 

            lstPreview.ItemsSource = _fileItems;

            txtPlaceholder.Text = "[Wormhole] Exporting files from Unreal";
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
                    IsSelected = true
                });
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            FilesToExport = _fileItems.Where(item => item.IsSelected)
                .Select(item => item.FullName)
                .ToArray()!;

            if(string.IsNullOrWhiteSpace(txtSubmitMessage.Text)) 
            {
                SubmitMessage = txtPlaceholder.Text;
            }
            else
            {
                SubmitMessage = txtSubmitMessage.Text;
            }

            DialogResult = true;
            this.Close();
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
