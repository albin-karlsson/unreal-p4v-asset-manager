using Microsoft.Win32;
using Perforce.P4;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UnrealExporter.App.Configs;
using UnrealExporter.App.Enums;
using UnrealExporter.App.Exceptions;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Services;

namespace UnrealExporter.UI
{
    /// <summary>
    /// Main window for the Unreal Asset Exporter application.
    /// Handles user interface interactions for exporting assets from Unreal Engine to Perforce.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string DEFAULT_UE_5_3_PATH = @"C:\Program Files\Epic Games\UE_5.3\Engine\Binaries\Win64\UnrealEditor.exe";

        private readonly IPerforceService _perforceService;
        private readonly IFileService _fileService;
        private readonly IUnrealService _unrealService;
        private readonly IAppConfig _appConfig;

        private string? _meshesSourceDirectory = null;
        private string? _texturesSourceDirectory = null;
        private string? _selectedUnrealProjectFile = null;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }
        private bool _isLoading;

        private string _loadingText = "Please wait...";
        public string WaitText
        {
            get { return _loadingText; }
            set
            {
                _loadingText = value;
                OnPropertyChanged(nameof(WaitText));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow(IFileService fileService, IUnrealService unrealService, IPerforceService perforceService, IAppConfig appConfig)
        {
            _fileService = fileService;
            _unrealService = unrealService;
            _perforceService = perforceService;
            _appConfig = appConfig;

            InitializeComponent();
            this.DataContext = this;
            ResetUI();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Validates all requirements before starting the export process.
        /// </summary>
        /// <returns>True if all requirements are met, false otherwise</returns>
        private bool ValidateExportRequirements()
        {
            try
            {
                var validationErrors = new List<string>();

                if(_perforceService.ConnectionStatus == null || _perforceService.ConnectionStatus == ConnectionStatus.Disconnected)
                {
                    validationErrors.Add("Not connected to Perforce.");
                }
                else if(string.IsNullOrEmpty(cboxPerforceWorkspace.SelectedItem?.ToString()))
                {
                    validationErrors.Add("No selected Perforce workspace.");
                }

                if (!(xboxExportMeshes.IsChecked ?? false) && !(xboxExportTextures.IsChecked ?? false))
                {
                    validationErrors.Add("Please select to export either meshes, textures, or both.");
                }

                if (xboxExportMeshes.IsChecked == true && string.IsNullOrEmpty(txtMeshesSourceDirectory.Text))
                {
                    validationErrors.Add("Meshes export selected but no Unreal meshes directory specified.");
                }

                if (xboxExportTextures.IsChecked == true && string.IsNullOrEmpty(txtTexturesSourceDirectory.Text))
                {
                    validationErrors.Add("Textures export selected but no Unreal textures directory specified.");
                }

                if (string.IsNullOrEmpty(txtDestinationDirectory.Text))
                {
                    validationErrors.Add("No output directory specified.");
                }

                if (validationErrors.Any())
                {
                    string errorMessage = string.Join(Environment.NewLine, validationErrors);
                    throw new ValidationException("Validation errors: " + Environment.NewLine + errorMessage);
                }

                return true;
            }
            catch (Exception ex) 
            {
                ShowError(ex.Message);
                return false;
            }
        }
        /// <summary>
        /// Handles clicking the default Unreal Engine path checkbox.
        /// </summary>
        private void xboxUseDefaultUnrealEnginePath_Clicked(object sender, RoutedEventArgs e)
        {
            if (xboxUseDefaultUnrealEnginePath.IsChecked == true)
            {
                txtUnrealEnginePath.Text = DEFAULT_UE_5_3_PATH;
            }
            else
            {
                txtUnrealEnginePath.Text = "";
            }
        }
        /// <summary>
        /// Retrieves and populates the list of available Perforce workspaces for the current user.
        /// </summary>
        private async void btnGetPerforceWorkspaces_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetSourceAndDestinationInputs(false);

                cboxPerforceWorkspace.Items.Clear();

                string trimmedUsername = txtPerforceUsername.Text.Trim();
                string trimmedPassword = txtPerforcePassword.Password.Trim();

                if (trimmedUsername.Length > 0 && trimmedPassword.Length > 0)
                {
                    IsLoading = true;
                    WaitText = "Logging in to Perforce...";

                    bool isSuccessfulLogin = await _perforceService.LogIn(trimmedUsername, trimmedPassword);

                    if (!isSuccessfulLogin)
                    {
                        throw new ServiceException("Failed to login to Perforce. Check your username and password and try again.");
                    }

                    List<string>? workspaces = await _perforceService.GetWorkspaces();

                    if (workspaces != null)
                    {
                        foreach (string workspace in workspaces)
                        {
                            cboxPerforceWorkspace.Items.Add(workspace);
                        }

                        IsLoading = false;

                        ShowSuccess($"{workspaces.Count()} Perforce workspaces retrieved.");
                    }
                }
                else
                {
                    throw new Exception("No Perforce username or password specified.");
                }
            }
            catch(P4Exception ex)
            {
                ResetUI();
                ShowError(ex.Message);
            }
            catch (Exception ex) 
            {
                ShowError(ex.Message);
            }
            finally
            {
                if(IsLoading)
                {
                    IsLoading = false;
                }
            }
        }
        /// <summary>
        /// Enables or disables source and destination directory input controls.
        /// </summary>
        /// <param name="enabled">Whether the controls should be enabled</param>
        private void SetSourceAndDestinationInputs(bool enabled)
        {
            btnBrowseMeshesSourceDirectory.IsEnabled = enabled;
            btnBrowseTexturesSourceDirectory.IsEnabled = enabled;
            btnBrowseDestinationDirectory.IsEnabled = enabled;

            txtDestinationDirectory.IsEnabled = enabled;
            txtMeshesSourceDirectory.IsEnabled = enabled;
            txtTexturesSourceDirectory.IsEnabled = enabled;

            if(!enabled)
            {
                txtDestinationDirectory.Text = "";
                txtMeshesSourceDirectory.Text = "";
                txtTexturesSourceDirectory.Text = "";
            }
        }
        /// <summary>
        /// Handles the main export assets operation, including validation, export, and Perforce submission.
        /// </summary>
        private async void btnExportAssets_Click(object sender, RoutedEventArgs e)
        {
            bool shouldResetUI = true;

            try
            {
                if (!ValidateExportRequirements())
                {
                    shouldResetUI = false;
                    return;
                }


                btnExportAssets.IsEnabled = false;

                _appConfig.SetConfiguration(
                    xboxExportMeshes.IsChecked ?? false,
                    xboxExportTextures.IsChecked ?? false,
                    txtDestinationDirectory.Text,
                    xboxConvertTexturesToDDS.IsChecked ?? false,
                    xboxOverwriteFiles.IsChecked ?? false,
                    txtUnrealEnginePath.Text,
                    _selectedUnrealProjectFile!,
                    _meshesSourceDirectory!,
                    _texturesSourceDirectory!
                );

                List<string>? filesToExcludeFromExport = null; 

                if (!_appConfig.OverwriteFiles) 
                {
                    filesToExcludeFromExport = _fileService.CheckDestinationDirectoryForExistingFiles();
                }

                var exportResult = await ProcessUnrealExport(filesToExcludeFromExport);

                if (!exportResult)
                {
                    throw new ServiceException("Error exporting from Unreal.");
                }

                string[] exportedFiles = _fileService.GetExportedFiles();

                if (!exportedFiles.Any())
                {
                    throw new ServiceException("No files found to export");
                }

                if (_appConfig.ExportTextures && _appConfig.ConvertTextures)
                {
                    await ProcessTextureConversion();
                    exportedFiles = _fileService.GetExportedFiles();
                }

                PreviewWindow previewWindow = new(PreviewWindowType.SelectFilesToSubmit, exportedFiles);

                if ((bool)previewWindow.ShowDialog()!)
                {
                    if (previewWindow.SelectedFiles == null || !previewWindow.SelectedFiles!.Any())
                    {
                        throw new Exception("No files selected for submit.");
                    }

                    if (string.IsNullOrEmpty(previewWindow.SubmitMessage?.Trim()))
                    {
                        throw new Exception("No valid submit message found.");
                    }

                    _appConfig.SubmitMessage = previewWindow.SubmitMessage!;

                    ProcessFileSelection(previewWindow);

                    _fileService.MoveDirectories(previewWindow.SelectedFiles!);

                    _perforceService.AddFilesToPerforce(_fileService.ExportedFiles);
                    _perforceService.Disconnect();

                    PreviewWindow confirmationWindow = new(PreviewWindowType.PreviewSubmittedFiles, _fileService.ExportedFiles.ToArray());
                    confirmationWindow.Show();
                }
                else
                {
                    throw new Exception("The submit process was canceled or encountered an error selecting the files for submit.");
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                if (shouldResetUI)
                {
                    ResetUI();
                }
            }
        }
        /// <summary>
        /// Processes the file selection from the preview window and updates configurations accordingly.
        /// </summary>
        /// <param name="previewWindow">Preview window containing selected files</param>
        private void ProcessFileSelection(PreviewWindow previewWindow)
        {

            var (exportMeshes, exportTextures) = _fileService.GetSelectedFileTypes(previewWindow.SelectedFiles!);

            if (exportMeshes != _appConfig.ExportMeshes || exportTextures != _appConfig.ExportTextures)
            {
                if (!exportMeshes || !exportTextures)
                {
                    var (subfolder, message) = !exportMeshes
                ? ("Textures", "No more meshes found after selecting files.")
                : ("Meshes", "No more textures found after selecting files.");

                    string updatedPath = System.IO.Path.Combine(_appConfig.DestinationDirectory, subfolder);
                    var result = MessageBox.Show(
                        $"{message}{Environment.NewLine}Do you want to change destination directory to:{Environment.NewLine}{updatedPath}",
                        "Confirm action",
                        MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.Yes)
                    {
                        _appConfig.DestinationDirectory = updatedPath;
                    }
                }

                _appConfig.ExportMeshes = exportMeshes;
                _appConfig.ExportTextures = exportTextures;
            }
        }
        /// <summary>
        /// Processes the export of assets from Unreal Engine.
        /// </summary>
        /// <param name="filesToExcludeFromExport">Optional list of files to exclude from export</param>
        /// <returns>True if export was successful, false otherwise</returns>
        private async Task<bool> ProcessUnrealExport(List<string>? filesToExcludeFromExport)
        {
            try
            {
                _unrealService.InitializeExport();
                IsLoading = true;
                WaitText = "Exporting assets from Unreal...";

                var exportResult = await _unrealService.ExportAssetsAsync(filesToExcludeFromExport);

                return exportResult;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                IsLoading = false; 
            }
        }
        /// <summary>
        /// Processes texture conversion to DDS format.
        /// </summary>
        private async Task ProcessTextureConversion()
        {
            try
            {
                IsLoading = true;
                WaitText = "Converting textures...";

                await _fileService.ConvertTextures();

                if (!_fileService.TextureConversionSuccessful)
                {
                    throw new ServiceException("DDS conversion failed! Export canceled. Check file names and try again.");
                }
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        /// <param name="errorMessage">Message to display</param>
        /// <param name="errorCaption">Caption for the error dialog</param>
        private void ShowError(string errorMessage, string errorCaption = "Error")
        {
            if(IsLoading)
            {
                IsLoading = false;
            }

            MessageBox.Show(errorMessage, errorCaption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        /// <summary>
        /// Displays a success message to the user.
        /// </summary>
        /// <param name="successMessage">Message to display</param>
        /// <param name="successCaption">Caption for the success dialog</param>
        private void ShowSuccess(string successMessage, string successCaption = "Success")
        {
            MessageBox.Show(successMessage, successCaption, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        /// <summary>
        /// Resets all UI elements to their default state.
        /// </summary>
        private void ResetUI()
        {
            // Reset text inputs
            txtPerforceUsername.Text = string.Empty;
            txtPerforcePassword.Password = string.Empty;
            txtMeshesSourceDirectory.Text = string.Empty;
            txtTexturesSourceDirectory.Text = string.Empty;
            txtDestinationDirectory.Text = string.Empty;

            // Reset combobox
            cboxPerforceWorkspace.Items.Clear();
            cboxPerforceWorkspace.SelectedIndex = -1;

            // Reset checkboxes
            xboxOverwriteFiles.IsChecked = false;
            xboxConvertTexturesToDDS.IsChecked = false;
            xboxExportMeshes.IsChecked = false;
            xboxExportTextures.IsChecked = false;

            // Reset export button to default state
            btnExportAssets.IsEnabled = true;

            // Reset DDS conversion checkbox state
            xboxConvertTexturesToDDS.IsEnabled = false;

            // Set default values
            txtUnrealEnginePath.Text = DEFAULT_UE_5_3_PATH;
            xboxUseDefaultUnrealEnginePath.IsChecked = true;
            IsLoading = false;
            SetSourceAndDestinationInputs(false);
        }
        /// <summary>
        /// Opens a file dialog for selecting the Unreal Engine executable.
        /// </summary>
        private void btnBrowseUnrealEnginePath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Unreal Editor Executable|UnrealEditor.exe",
                Title = "Select UnrealEditor.exe"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtUnrealEnginePath.Text = openFileDialog.FileName;
            }
        }
        /// <summary>
        /// Opens a folder dialog for selecting Unreal Engine source directories.
        /// </summary>
        private void btnBrowseUnrealSource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedUnrealProjectFile == null)
                {
                    if (_perforceService.ConnectionStatus == null || _perforceService.ConnectionStatus == ConnectionStatus.Disconnected)
                    {
                        throw new ServiceException("Perforce not connected.");
                    }
                    else
                    {
                        string[] projectFiles = _perforceService.GetUnrealProjectPathFromPerforce();

                        if (projectFiles.Length == 0)
                        {
                            throw new ServiceException("No Unreal projects found in Perforce workspace.");
                        }

                        if (projectFiles.Length > 1)
                        {
                            var selectionWindow = new PreviewWindow(PreviewWindowType.SelectUnrealProject, projectFiles);
                            bool? result = selectionWindow.ShowDialog();

                            if (result == true)
                            {
                                if (selectionWindow.SelectedFile != null)
                                {
                                    _selectedUnrealProjectFile = selectionWindow.SelectedFile;
                                }
                            }
                            else
                            {
                                throw new Exception("Error selecting Unreal project file.");
                            }
                        }
                        else
                        {
                            _selectedUnrealProjectFile = projectFiles[0];
                        }
                    }
                }

                Button currentButton = (Button)sender;

                if (currentButton.Name.ToLower().Contains("meshes"))
                {
                    SelectSourceDirectory("meshes");
                }
                else
                {
                    SelectSourceDirectory("textures");
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }
        /// <summary>
        /// Handles the selection of source directories for meshes or textures.
        /// </summary>
        /// <param name="inputType">Type of input ("meshes" or "textures")</param>
        private void SelectSourceDirectory(string inputType)
        {
            try
            {
                var folderBrowserDialog = new OpenFolderDialog
                {
                    InitialDirectory = System.IO.Path.GetDirectoryName(_selectedUnrealProjectFile)
                };

                if ((bool)folderBrowserDialog.ShowDialog()!)
                {
                    string fullPath = folderBrowserDialog.FolderName;
                    string contentPath = "\\Content\\";
                    int contentIndex = fullPath.IndexOf(contentPath, StringComparison.OrdinalIgnoreCase);

                    if (contentIndex != -1)
                    {
                        string relativePath = fullPath.Substring(contentIndex + contentPath.Length);

                        if (inputType == "meshes")
                        {
                            _meshesSourceDirectory = relativePath;
                            txtMeshesSourceDirectory.Text = _meshesSourceDirectory;
                        }
                        else if (inputType == "textures")
                        {
                            _texturesSourceDirectory = relativePath;
                            txtTexturesSourceDirectory.Text = _texturesSourceDirectory;
                        }
                    }
                    else
                    {
                        throw new Exception("No content folder found in Unreal project.");
                    }
                }
            }
            catch(Exception ex)
            {
                ShowError(ex.Message);
            }
        }
        /// <summary>
        /// Handles the selection change of Perforce workspace.
        /// </summary>
        private void cboxPerforceWorkspace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                string? selectedWorkspace = cboxPerforceWorkspace.SelectedItem?.ToString();

                if (!string.IsNullOrEmpty(selectedWorkspace))
                {
                    _perforceService.Connect(selectedWorkspace);

                    SetSourceAndDestinationInputs(true);

                    // Reset part of UI and selected Unreal project if workspace changes
                    _selectedUnrealProjectFile = null!;
                    txtDestinationDirectory.Text = "";
                    txtMeshesSourceDirectory.Text = "";
                    txtTexturesSourceDirectory.Text = "";
                }
            }
            catch(Exception ex)
            {
                ShowError(ex.Message);
            }
        }
        /// <summary>
        /// Handles the export textures checkbox state change.
        /// </summary>
        private void xboxExportTextures_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)xboxExportTextures.IsChecked!)
            {
                xboxConvertTexturesToDDS.IsEnabled = true;
            }
            else
            {
                xboxConvertTexturesToDDS.IsChecked = false;
                xboxConvertTexturesToDDS.IsEnabled = false;
            }
        }
        /// <summary>
        /// Opens a folder dialog for selecting the destination directory.
        /// </summary>
        private void btnBrowseDestinationDirectory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_perforceService.ConnectionStatus == null || _perforceService.ConnectionStatus == ConnectionStatus.Disconnected)
                {
                    throw new ServiceException("Perforce not connected.");
                }

                var folderBrowserDialog = new OpenFolderDialog
                {
                    InitialDirectory = System.IO.Path.GetDirectoryName(_perforceService.WorkspacePath)
                };

                bool? result = folderBrowserDialog.ShowDialog();

                if ((bool)result!)
                {
                    if (_perforceService.WorkspacePath == null)
                    {
                        throw new ServiceException("No valid Perforce Workspace found.");
                    }

                    if (!folderBrowserDialog.FolderName.Contains(_perforceService.WorkspacePath))
                    {
                        ShowError("The destination directory doesn't seem to be in the current Perforce workspace.");
                        return;
                    }
                    else
                    {
                        txtDestinationDirectory.Text = folderBrowserDialog.FolderName;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }
    }
    /// <summary>
    /// Converter for converting boolean values to Visibility enum values.
    /// </summary>
    public class BoolToVis : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value.
        /// </summary>
        /// <param name="value">The boolean value to convert</param>
        /// <param name="targetType">The type of the target property</param>
        /// <param name="parameter">Optional conversion parameter</param>
        /// <param name="culture">The culture to use for conversion</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return booleanValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        /// <summary>
        /// Converts a Visibility value back to a boolean value.
        /// Not implemented.
        /// </summary>
        /// <exception cref="NotImplementedException">This conversion is not implemented</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
