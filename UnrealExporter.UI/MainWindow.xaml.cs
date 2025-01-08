using Microsoft.Win32;
using Perforce.P4;
using System.ComponentModel;
using System.Data;
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
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Services;

namespace UnrealExporter.UI
{
    public partial class MainWindow : Window
    {
        private const string DEFAULT_UE_PATH = @"C:\Program Files\Epic Games\UE_5.3\Engine\Binaries\Win64\UnrealEditor.exe";

        private readonly IPerforceService _perforceService;
        private readonly IFileService _fileService;
        private readonly IUnrealService _unrealService;
        private readonly IAppConfig _appConfig;

        private string? _meshesSourceDirectory = null;
        private string? _texturesSourceDirectory = null;

        public PerforceService? PerforceManager { get; set; }
        public string SelectedUnrealProjectFile { get; set; }

        public MainWindow(IFileService fileService, IUnrealService unrealService, IPerforceService perforceService, IAppConfig appConfig)
        {
            _fileService = fileService;
            _unrealService = unrealService;
            _perforceService = perforceService;
            _appConfig = appConfig;

            InitializeComponent();
            ResetUI();
        }

        private bool ValidateExportRequirements()
        {
            var validationErrors = new List<string>();

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
                ShowError("Validation errors: " + Environment.NewLine + errorMessage);
                return false;
            }

            return true;
        }

        private void xboxUseDefaultUnrealEnginePath_Clicked(object sender, RoutedEventArgs e)
        {
            if (xboxUseDefaultUnrealEnginePath.IsChecked == true)
            {
                txtUnrealEnginePath.Text = DEFAULT_UE_PATH;
            }
            else
            {
                txtUnrealEnginePath.Text = "";
            }
        }

        private void btnGetPerforceWorkspaces_Click(object sender, RoutedEventArgs e)
        {
            cboxPerforceWorkspace.Items.Clear();

            string trimmedUsername = txtPerforceUsername.Text.Trim();
            string trimmedPassword = txtPerforcePassword.Password.Trim();

            if (trimmedUsername.Length > 0 && trimmedPassword.Length > 0)
            {
                bool isSuccessfulLogin = _perforceService.LogIn(trimmedUsername, trimmedPassword);

                if (!isSuccessfulLogin)
                {
                    ShowError("Failed to login to Perforce. Check your username and password and try again.");
                    return;
                }

                List<string>? workspaces = _perforceService.GetWorkspaces();

                if (workspaces != null)
                {
                    foreach (string workspace in workspaces)
                    {
                        cboxPerforceWorkspace.Items.Add(workspace);
                    }

                    ShowSuccess($"{workspaces.Count()} Perforce workspaces retrieved!");
                }
            }
            else
            {
                ShowError("No Perforce username or password specified.");
            }
        }

        private void SetSourceAndDestinationInputs(bool enabled)
        {
            btnBrowseMeshesSourceDirectory.IsEnabled = enabled;
            btnBrowseTexturesSourceDirectory.IsEnabled = enabled;

            btnBrowseDestinationDirectory.IsEnabled = enabled;

            txtDestinationDirectory.IsEnabled = enabled;
            txtMeshesSourceDirectory.IsEnabled = enabled;
            txtTexturesSourceDirectory.IsEnabled = enabled;
        }

        private async void btnExportAssets_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateExportRequirements())
                    return;

                btnExportAssets.IsEnabled = false;

                _appConfig.SetConfiguration(
                    xboxExportMeshes.IsChecked ?? false,
                    xboxExportTextures.IsChecked ?? false,
                    txtDestinationDirectory.Text,
                    xboxConvertTexturesToDDS.IsChecked ?? false,
                    xboxOverwriteFiles.IsChecked ?? false,
                    txtUnrealEnginePath.Text,
                    SelectedUnrealProjectFile,
                    _meshesSourceDirectory!,
                    _texturesSourceDirectory!
                );

                List<string>? filesToExcludeFromExport = null; 

                if (!_appConfig.OverwriteFiles) 
                {
                    filesToExcludeFromExport = _fileService.CheckDestinationDirectoryForExistingFiles();
                }

                _unrealService.InitializeExport();

                var exportResult = await _unrealService.ExportAssetsAsync(filesToExcludeFromExport);

                if (!exportResult.Success)
                {
                    ShowError("Error exporting from Unreal.");

                    return;
                }

                string[] exportedFiles = _fileService.GetExportedFiles();

                if (!exportedFiles.Any())
                {
                    ShowError("No files found to export.");
                    ResetUI();
                    return;
                }

                if (_appConfig.ExportTextures)
                {
                    _fileService.ConvertTextures();

                    if (!_fileService.TextureConversionSuccessful)
                    {
                        ShowError("DDS conversion failed! Export canceled. Check file names and try again.");
                        ResetUI();
                        return;
                    }
                }

                PreviewWindow previewWindow = new(exportedFiles);

                if ((bool)previewWindow.ShowDialog()!)
                {
                    if (!previewWindow.SelectedFiles.Any())
                    {
                        ShowError("No files selected for submit.");
                    }

                    _appConfig.SubmitMessage = previewWindow.SubmitMessage;

                    var (exportMeshes, exportTextures) = _fileService.GetAndSetSelectedFilesFileTypes(previewWindow.SelectedFiles);
                    _appConfig.ExportMeshes = exportMeshes;
                    _appConfig.ExportTextures = exportTextures;

                    _fileService.MoveDirectories(previewWindow.SelectedFiles);

                    _perforceService.AddFilesToPerforce(_fileService.ExportedFiles);
                    _perforceService.Disconnect();

                    ConfirmationWindow confirmationWindow = new(_fileService.ExportedFiles, _appConfig.SubmitMessage); 
                    confirmationWindow.Show();
                }
            }
            catch (Exception ex)
            {
                ShowError("An unexpected error occurred.");
            }
            finally
            {
                ResetUI();
            }
        }

        private void ShowError(string errorMessage, string errorCaption = "Error")
        {
            MessageBox.Show(errorMessage, errorCaption, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccess(string successMessage, string successCaption = "Success")
        {
            MessageBox.Show(successMessage, successCaption, MessageBoxButton.OK, MessageBoxImage.Information);
        }

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
            txtUnrealEnginePath.Text = DEFAULT_UE_PATH;
            xboxUseDefaultUnrealEnginePath.IsChecked = true;
            SetSourceAndDestinationInputs(false);
        }

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

        private void btnBrowseUnrealSource_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUnrealProjectFile == null)
            {
                if (_perforceService.ConnectionStatus == ConnectionStatus.Disconnected)
                {
                    ShowError("Perforce not connected.");
                    return;
                }
                else
                {
                    string[] projectFiles = _perforceService.GetUnrealProjectPathFromPerforce();

                    if (projectFiles.Length == 0)
                    {
                        MessageBox.Show("No Unreal projects found in Perforce workspace.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (projectFiles.Length > 1)
                    {
                        var selectionWindow = new SelectionWindow(projectFiles);
                        bool? result = selectionWindow.ShowDialog();

                        if (result == true)
                        {
                            if (selectionWindow.SelectedProject != null)
                            {
                                SelectedUnrealProjectFile = selectionWindow.SelectedProject;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        SelectedUnrealProjectFile = projectFiles[0];
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

        private void SelectSourceDirectory(string inputType)
        {
            var folderBrowserDialog = new OpenFolderDialog
            {
                InitialDirectory = System.IO.Path.GetDirectoryName(SelectedUnrealProjectFile)
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
                    ShowError("No content folder found in Unreal project.");
                }
            }
        }

        private void cboxPerforceWorkspace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selectedWorkspace = cboxPerforceWorkspace.SelectedItem?.ToString();

            if (!string.IsNullOrEmpty(selectedWorkspace))
            {
                _perforceService.Connect(selectedWorkspace);
            }

            SetSourceAndDestinationInputs(true);

            // Reset UI and selected Unreal project if workspace changes
            SelectedUnrealProjectFile = null!;
            txtDestinationDirectory.Text = "";
            txtMeshesSourceDirectory.Text = "";
            txtTexturesSourceDirectory.Text = "";
        }

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

        private void btnBrowseDestinationDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (_perforceService.ConnectionStatus == ConnectionStatus.Disconnected)
            {
                ShowError("Perforce not connected.");
                return;
            }

            var folderBrowserDialog = new OpenFolderDialog
            {
                InitialDirectory = System.IO.Path.GetDirectoryName(_perforceService.WorkspacePath)
            };

            // Show dialog and check if the user selects a folder
            bool? result = folderBrowserDialog.ShowDialog();

            if ((bool)result!)
            {
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

    }
}

//private async void btnExportAssets_Click(object sender, RoutedEventArgs e)
//{
//    string? selectedWorkspace = cboxPerforceWorkspace.SelectedItem.ToString();

//    if (!(bool)xboxExportMeshes.IsChecked! && !(bool)xboxExportTextures.IsChecked!)
//    {
//        MessageBox.Show("Please select to export either meshes, textures, or both, and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); ;
//        return;
//    }

//    if (selectedWorkspace != null && PerforceManager != null && !string.IsNullOrEmpty(txtUnrealEnginePath.Text))
//    {
//        string unrealEnginePath = txtUnrealEnginePath.Text.Trim();

//        bool exportMeshes = (bool)xboxExportMeshes.IsChecked!;
//        bool exportTextures = (bool)xboxExportTextures.IsChecked!;

//        List<string> errors = new();

//        if (exportMeshes && string.IsNullOrEmpty(txtUnrealMeshesDirectory.Text))
//        {
//            errors.Add("Attempting to export meshes but no Unreal meshes directory specified.");
//        }
//        if (exportTextures && string.IsNullOrEmpty(txtUnrealTexturesDirectory.Text))
//        {
//            errors.Add("Attempting to export textures but no Unreal textures directory specified.");
//        }
//        if (string.IsNullOrEmpty(txtOutputDirectory.Text))
//        {
//            errors.Add("No output directory specified.");
//        }
//        if (errors.Count > 0)
//        {
//            string errorMessage = string.Join(Environment.NewLine, errors);
//            MessageBox.Show(errorMessage, "Errors", MessageBoxButton.OK, MessageBoxImage.Error);
//            return;
//        }

//        PerforceManager.Connect(selectedWorkspace);

//        bool overwriteFiles = (bool)xboxOverwriteFiles.IsChecked!;
//        bool convertTextures = (bool)xboxConvertTexturesToDDS.IsChecked!;

//        FileManager fileManager = new(txtOutputDirectory.Text, overwriteFiles, exportMeshes, exportTextures, convertTextures);

//        List<string> filesToExclude = new();

//        if (!overwriteFiles)
//        {
//            filesToExclude = fileManager.CheckDestinationFolderContent();
//        }

//        UnrealManager unrealManager = new(unrealEnginePath, SelectedUnrealProjectFile, exportMeshes, exportTextures, _meshesSourceDirectory, _texturesSourceDirectory);
//        unrealManager.InitializeExport();

//        await unrealManager.LaunchUnrealAndRunScriptAsync(filesToExclude);

//        if (!fileManager.CheckExistingOutputDirectory())
//        {
//            MessageBox.Show("No files found to export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            ResetUi();
//            return;
//        }

//        if (exportTextures)
//        {
//            fileManager.ConvertTextures();

//            if (!fileManager.TextureConversionSuccessful)
//            {
//                MessageBox.Show("DDS conversion failed! Export canceled. Check file names and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                ResetUi();
//                return;
//            }
//        }

//        PreviewWindow previewWindow = new(fileManager.CheckFilesToExport());

//        try
//        {
//            if ((bool)previewWindow.ShowDialog()!)
//            {
//                if (previewWindow.FilesToExport.Any())
//                {
//                    string[] selectedFiles = previewWindow.FilesToExport;

//                    bool[] exportedFileTypes = fileManager.CheckSelectedFilesFiletypes(selectedFiles);
//                    fileManager.MoveDirectories(selectedFiles);

//                    PerforceManager.SubmitMessage = previewWindow.SubmitMessage;
//                    PerforceManager.AddFilesToPerforce(fileManager.ExportedFiles, txtOutputDirectory.Text, exportedFileTypes[0], exportedFileTypes[1]);
//                    PerforceManager.Disconnect();

//                    var confirmationWindow = new ConfirmationWindow(fileManager.ExportedFiles, previewWindow.SubmitMessage);
//                    confirmationWindow.ShowDialog();

//                    ResetUi();
//                }
//                else
//                {
//                    MessageBox.Show("No files selected to submit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                    ResetUi();
//                    return;
//                }
//            }
//            else
//            {
//                MessageBox.Show("Export canceled.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
//                ResetUi();
//                return;
//            }
//        }
//        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x8)
//        {
//            MessageBox.Show("A memory error ocurred when trying to preview the files for export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            ResetUi();
//            return;
//        }
//    }
//}