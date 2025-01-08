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
using UnrealExporter.App;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Models;

namespace UnrealExporter.UI
{
    public partial class MainWindow : Window
    {
        private const string DEFAULT_UE_PATH = @"C:\Program Files\Epic Games\UE_5.3\Engine\Binaries\Win64\UnrealEditor.exe";

        private readonly IPerforceService _perforceService;
        private readonly IFileService _fileService;
        private readonly IUnrealService _unrealService;

        private string? _meshesSourceDirectory = null;
        private string? _texturesSourceDirectory = null;

        public PerforceManager? PerforceManager { get; set; }
        public string SelectedUnrealProjectFile { get; set; }

        public MainWindow(IFileService fileService, IUnrealService unrealService, IPerforceService perforceService)
        {
            _fileService = fileService;
            _unrealService = unrealService;
            _perforceService = perforceService;

            InitializeComponent();
            InitializeDefaultValues();
        }

        private void InitializeDefaultValues()
        {
            txtUnrealEnginePath.Text = DEFAULT_UE_PATH;
            SetUIState(false);
        }

        private bool ValidateExportRequirements()
        {
            var validationErrors = new List<string>();

            if (!(xboxExportMeshes.IsChecked ?? false) && !(xboxExportTextures.IsChecked ?? false))
            {
                validationErrors.Add("Please select to export either meshes, textures, or both.");
            }

            if (xboxExportMeshes.IsChecked ?? false && string.IsNullOrEmpty(txtUnrealMeshesDirectory.Text))
            {
                validationErrors.Add("Meshes export selected but no Unreal meshes directory specified.");
            }

            if (xboxExportTextures.IsChecked ?? false && string.IsNullOrEmpty(txtUnrealTexturesDirectory.Text))
            {
                validationErrors.Add("Textures export selected but no Unreal textures directory specified.");
            }

            if (string.IsNullOrEmpty(txtOutputDirectory.Text))
            {
                validationErrors.Add("No output directory specified.");
            }

            if (validationErrors.Any())
            {
                // TODO: Add a better error window
                ShowError("Validation errors.");
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
                PerforceManager = new();

                bool isSuccessfulLogin = _perforceService.LogIn(trimmedUsername, trimmedPassword);

                if (!isSuccessfulLogin)
                {
                    MessageBox.Show($"Failed to login to Perforce. Check your username and password and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                List<string>? workspaces = _perforceService.GetWorkspaces();

                if (workspaces != null)
                {
                    foreach (string workspace in workspaces)
                    {
                        cboxPerforceWorkspace.Items.Add(workspace);
                    }

                    MessageBox.Show($"{workspaces.Count()} Perforce workspaces retrieved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show($"No Perforce username or password specified.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void SetUIState(bool enabled)
        {
            btnExportAssets.IsEnabled = enabled;
        }

        private async void btnExportAssets_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateExportRequirements())
                    return;

                SetUIState(false);

                var exportConfig = new ExportConfiguration
                {
                    UnrealEnginePath = txtUnrealEnginePath.Text.Trim(),
                    ProjectFile = SelectedUnrealProjectFile,
                    ExportMeshes = xboxExportMeshes.IsChecked ?? false,
                    ExportTextures = xboxExportTextures.IsChecked ?? false,
                    MeshesDirectory = _meshesSourceDirectory,
                    TexturesDirectory = _texturesSourceDirectory,
                    OutputDirectory = txtOutputDirectory.Text,
                    OverwriteFiles = xboxOverwriteFiles.IsChecked ?? false,
                    ConvertTextures = xboxConvertTexturesToDDS.IsChecked ?? false
                };

                var exportResult = await _unrealService.ExportAssetsAsync(exportConfig);

                if (!exportResult.Success)
                {
                    ShowError("Error exporting from Unreal.");

                    return;
                }

                PreviewWindow previewWindow = new(exportResult.ExportedFiles);

                if ((bool)previewWindow.ShowDialog()!)
                {
                    ProcessSelectedFiles(previewWindow.FilesToExport, previewWindow.SubmitMessage);

                    ConfirmationWindow confirmationWindow = new(exportResult.ExportedFiles, previewWindow.SubmitMessage);
                    confirmationWindow.Show();
                }
            }
            catch (Exception ex)
            {
                ShowError("An unexpected error occurred.");
            }
            finally
            {
                SetUIState(true);
                ResetUI();
            }
        }

        private void ProcessSelectedFiles(string[] selectedFiles, string submitMessage)
        {

        }

        private void ShowError(string errorMessage, string errorCaption = "Error")
        {
            MessageBox.Show(errorMessage, errorCaption, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowWarning(string warningMessage, string warningCaption = "Warning")
        {
            MessageBox.Show(warningMessage, warningCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ResetUI()
        {
            // Reset text inputs
            txtPerforceUsername.Text = string.Empty;
            txtPerforcePassword.Password = string.Empty;
            txtUnrealEnginePath.Text = string.Empty;
            txtUnrealMeshesDirectory.Text = string.Empty;
            txtUnrealTexturesDirectory.Text = string.Empty;
            txtOutputDirectory.Text = string.Empty;

            // Reset combobox
            cboxPerforceWorkspace.Items.Clear();
            cboxPerforceWorkspace.SelectedIndex = -1;

            // Reset checkboxes
            xboxUseDefaultUnrealEnginePath.IsChecked = false;
            xboxOverwriteFiles.IsChecked = false;
            xboxConvertTexturesToDDS.IsChecked = false;
            xboxExportMeshes.IsChecked = false;
            xboxExportTextures.IsChecked = false;

            // Reset export button to default state
            btnExportAssetsContent.Content = "Export assets";
            btnExportAssets.IsEnabled = true;

            // Reset DDS conversion checkbox state
            xboxConvertTexturesToDDS.IsEnabled = false;
        }
        private void btnBrowseUnrealEnginePath_Click(object sender, RoutedEventArgs e)
        {
            // Open a file dialog and let the user browse to the UnrealEditor.exe
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Unreal Editor Executable|UnrealEditor.exe", // Filter for Unreal Editor executable
                Title = "Select UnrealEditor.exe"
            };

            // Show the dialog and check if the user selected a file
            if (openFileDialog.ShowDialog() == true)
            {
                // Set the text of txtUnrealEnginePath to the selected file path
                txtUnrealEnginePath.Text = openFileDialog.FileName;
            }
        }

        private void btnBrowseUnrealSource_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUnrealProjectFile == null)
            {
                if (PerforceManager == null)
                {
                    MessageBox.Show("No version controlled Unreal project can be found. Select a Perforce workspace and try again.", "Error", MessageBoxButton.OK);

                    return;
                }
                else
                {
                    string[] projectFiles = PerforceManager!.GetUnrealProjectPathFromPerforce();

                    if (projectFiles.Length == 0)
                    {
                        MessageBox.Show("No Unreal projects found in Perforce workspace.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (projectFiles.Length > 1)
                    {
                        // Show project selection dialog
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

            bool? result = folderBrowserDialog.ShowDialog();
            if ((bool)result!)
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
                        txtUnrealMeshesDirectory.Text = _meshesSourceDirectory;
                    }
                    else if (inputType == "textures")
                    {
                        _texturesSourceDirectory = relativePath;
                        txtUnrealTexturesDirectory.Text = _texturesSourceDirectory;
                    }
                }
                else
                {
                    // Fallback to full path if "Content" is not found
                    MessageBox.Show("No content folder found in Unreal project.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void cboxPerforceWorkspace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selectedWorkspace = cboxPerforceWorkspace.SelectedItem?.ToString();

            if (!string.IsNullOrEmpty(selectedWorkspace))
            {
                PerforceManager!.Connect(selectedWorkspace);
            }

            // Reset UI and selected unreal project if workspace changes
            SelectedUnrealProjectFile = null!;
            txtOutputDirectory.Text = "";
            txtUnrealMeshesDirectory.Text = "";
            txtUnrealTexturesDirectory.Text = "";
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

        private void btnBrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (PerforceManager == null)
            {
                MessageBox.Show("No version controlled Unreal project can be found. Select a Perforce workspace and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            var folderBrowserDialog = new OpenFolderDialog
            {
                InitialDirectory = System.IO.Path.GetDirectoryName(PerforceManager.WorkspacePath)
            };

            // Show dialog and check if the user selects a folder
            bool? result = folderBrowserDialog.ShowDialog();

            if ((bool)result!)
            {
                if (!folderBrowserDialog.FolderName.Contains(PerforceManager.WorkspacePath))
                {
                    MessageBox.Show("The output directory doesn't seem to be in the current Perforce workspace.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }
                else
                {
                    txtOutputDirectory.Text = folderBrowserDialog.FolderName;
                }
            }
        }
    }
}

//    public partial class MainWindow : Window
//    {
//        private const string DEFAULT_UE_PATH = @"C:\Program Files\Epic Games\UE_5.3\Engine\Binaries\Win64\UnrealEditor.exe";
//        private readonly IPerforceService _perforceService;
//        private readonly IFileService _fileService;
//        private readonly IUnrealService _unrealService;

//        private string? _meshesSourceDirectory;
//        private string? _texturesSourceDirectory;

//        public string SelectedUnrealProjectFile { get; private set; }

//        public MainWindow(
//            IPerforceService perforceService,
//            IFileService fileService,
//            IUnrealService unrealService)
//        {
//            _perforceService = perforceService;
//            _fileService = fileService;
//            _unrealService = unrealService;

//            InitializeComponent();
//            InitializeDefaultValues();
//        }

//        private void InitializeDefaultValues()
//        {
//            txtUnrealEnginePath.Text = DEFAULT_UE_PATH;
//            btnExportAssets.IsEnabled = false;
//        }

//        private async Task<bool> ValidateExportRequirements()
//        {
//            var validationErrors = new List<string>();

//            if (!(xboxExportMeshes.IsChecked ?? false) && !(xboxExportTextures.IsChecked ?? false))
//            {
//                validationErrors.Add("Please select to export either meshes, textures, or both.");
//            }

//            if (xboxExportMeshes.IsChecked ?? false && string.IsNullOrEmpty(txtUnrealMeshesDirectory.Text))
//            {
//                validationErrors.Add("Meshes export selected but no Unreal meshes directory specified.");
//            }

//            if (xboxExportTextures.IsChecked ?? false && string.IsNullOrEmpty(txtUnrealTexturesDirectory.Text))
//            {
//                validationErrors.Add("Textures export selected but no Unreal textures directory specified.");
//            }

//            if (string.IsNullOrEmpty(txtOutputDirectory.Text))
//            {
//                validationErrors.Add("No output directory specified.");
//            }

//            if (validationErrors.Any())
//            {
//                await ShowError("Validation Errors", string.Join(Environment.NewLine, validationErrors));
//                return false;
//            }

//            return true;
//        }

//        private async Task<bool> HandlePerforceLogin()
//        {
//            try
//            {
//                var credentials = new PerforceCredentials(
//                    txtPerforceUsername.Text.Trim(),
//                    txtPerforcePassword.Password.Trim()
//                );

//                if (!credentials.IsValid())
//                {
//                    await ShowError("Login Error", "Please provide both username and password.");
//                    return false;
//                }

//                var success = await _perforceService.LoginAsync(credentials);
//                if (!success)
//                {
//                    await ShowError("Login Error", "Failed to login to Perforce. Please check your credentials.");
//                    return false;
//                }

//                return true;
//            }
//            catch (Exception ex)
//            {
//                await ShowError("Perforce Error", "An error occurred during Perforce login.");
//                return false;
//            }
//        }

//        private void xboxUseDefaultUnrealEnginePath_Clicked(object sender, RoutedEventArgs e)
//        {
//            if (xboxUseDefaultUnrealEnginePath.IsChecked == true)
//            {
//                txtUnrealEnginePath.Text = DEFAULT_UE_PATH;
//            }
//            else
//            {
//                txtUnrealEnginePath.Text = "";
//            }
//        }

//        private void btnBrowseUnrealEnginePath_Click(object sender, RoutedEventArgs e)
//        {
//            OpenFileDialog openFileDialog = new OpenFileDialog
//            {
//                Filter = "Unreal Editor Executable|UnrealEditor.exe",
//                Title = "Select UnrealEditor.exe"
//            };

//            if (openFileDialog.ShowDialog() == true)
//            {
//                txtUnrealEnginePath.Text = openFileDialog.FileName;
//            }
//        }

//        private async void btnExportAssets_Click(object sender, RoutedEventArgs e)
//        {
//            try
//            {
//                if (!await ValidateExportRequirements())
//                    return;

//                SetUIState(false); // Disable UI during export

//                var exportConfig = new ExportConfiguration
//                {
//                    UnrealEnginePath = txtUnrealEnginePath.Text.Trim(),
//                    ProjectFile = SelectedUnrealProjectFile,
//                    ExportMeshes = xboxExportMeshes.IsChecked ?? false,
//                    ExportTextures = xboxExportTextures.IsChecked ?? false,
//                    MeshesDirectory = _meshesSourceDirectory,
//                    TexturesDirectory = _texturesSourceDirectory,
//                    OutputDirectory = txtOutputDirectory.Text,
//                    OverwriteFiles = xboxOverwriteFiles.IsChecked ?? false,
//                    ConvertTextures = xboxConvertTexturesToDDS.IsChecked ?? false
//                };

//                var result = await _unrealService.ExportAssetsAsync(exportConfig);

//                if (!result.Success)
//                {
//                    await ShowError("Export Error", result.ErrorMessage);

//                    return;
//                }

//                var previewWindow = new PreviewWindow(result.ExportedFiles);
//                if (await ShowPreviewDialog(previewWindow))
//                {
//                    await ProcessSelectedFiles(previewWindow.FilesToExport, previewWindow.SubmitMessage);
//                    await ShowSuccess("Export Complete", "Assets have been successfully exported and submitted.");
//                }
//            }
//            catch (Exception ex)
//            {
//                await ShowError("Export Error", "An unexpected error occurred during export.");
//            }
//            finally
//            {
//                SetUIState(true);
//                ResetUI();
//            }
//        }

//        private async Task ProcessSelectedFiles(string[] selectedFiles, string submitMessage)
//        {
//            if (!selectedFiles.Any())
//                return;

//            var fileTypes = _fileService.CheckSelectedFilesFiletypes(selectedFiles);
//            await _fileService.MoveDirectoriesAsync(selectedFiles);

//            await _perforceService.SubmitFilesAsync(
//                _fileService.ExportedFiles,
//                txtOutputDirectory.Text,
//                submitMessage,
//                fileTypes
//            );
//        }

//        private void SetUIState(bool enabled)
//        {
//            btnExportAssets.IsEnabled = enabled;
//        }

//        private static async Task ShowError(string title, string message)
//        {
//            await Application.Current.Dispatcher.InvokeAsync(() =>
//                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
//        }

//        private static async Task ShowSuccess(string title, string message)
//        {
//            await Application.Current.Dispatcher.InvokeAsync(() =>
//                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
//        }

//        // Interface definitions
//        public interface IPerforceService
//        {
//            Task<bool> LoginAsync(PerforceCredentials credentials);
//            Task<string[]> GetWorkspacesAsync();
//            Task SubmitFilesAsync(string[] files, string outputDirectory, string submitMessage, bool[] fileTypes);
//        }

//        public interface IFileService
//        {
//            bool[] CheckSelectedFilesFiletypes(string[] files);
//            Task MoveDirectoriesAsync(string[] files);
//            string[] ExportedFiles { get; }
//        }

//        public interface IUnrealService
//        {
//            Task<ExportResult> ExportAssetsAsync(ExportConfiguration config);
//        }

//        // Additional helper classes
//        public record PerforceCredentials(string Username, string Password)
//        {
//            public bool IsValid() => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
//        }

//        private void ResetUI()
//        {
//            try
//            {
//                // Reset text inputs
//                txtPerforceUsername.Text = string.Empty;
//                txtPerforcePassword.Password = string.Empty;
//                txtUnrealEnginePath.Text = DEFAULT_UE_PATH; // Use the constant instead of empty
//                txtUnrealMeshesDirectory.Text = string.Empty;
//                txtUnrealTexturesDirectory.Text = string.Empty;
//                txtOutputDirectory.Text = string.Empty;

//                // Reset combobox
//                cboxPerforceWorkspace.Items.Clear();
//                cboxPerforceWorkspace.SelectedIndex = -1;

//                // Reset checkboxes with null-safe assignments
//                xboxUseDefaultUnrealEnginePath.IsChecked = false;
//                xboxOverwriteFiles.IsChecked = false;
//                xboxConvertTexturesToDDS.IsChecked = false;
//                xboxExportMeshes.IsChecked = false;
//                xboxExportTextures.IsChecked = false;

//                // Reset export button
//                if (btnExportAssetsContent != null)
//                {
//                    btnExportAssetsContent.Content = "Export assets";
//                }
//                btnExportAssets.IsEnabled = false; // Keep disabled until requirements are met

//                // Reset DDS conversion checkbox
//                xboxConvertTexturesToDDS.IsEnabled = false;

//                // Reset source directories
//                _meshesSourceDirectory = null;
//                _texturesSourceDirectory = null;

//                // Reset selected project file
//                SelectedUnrealProjectFile = string.Empty;
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"Error resetting UI: {ex.Message}", "Error",
//                    MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        private async Task<bool> ShowPreviewDialog(PreviewWindow previewWindow)
//        {
//            return await Application.Current.Dispatcher.InvokeAsync(() =>
//            {
//                try
//                {
//                    bool? dialogResult = previewWindow.ShowDialog();
//                    return dialogResult ?? false;
//                }
//                catch (Win32Exception ex) when (ex.NativeErrorCode == 0x8)
//                {
//                    MessageBox.Show("A memory error occurred when trying to preview the files for export.",
//                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                    return false;
//                }
//                catch (Exception ex)
//                {
//                    MessageBox.Show($"An error occurred during preview: {ex.Message}",
//                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                    return false;
//                }
//            });
//        }

//        private bool ValidateWorkspaceSelection()
//        {
//            if (cboxPerforceWorkspace.SelectedItem == null)
//            {
//                MessageBox.Show("Please select a Perforce workspace.",
//                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
//                return false;
//            }
//            return true;
//        }

//        protected override void OnClosed(EventArgs e)
//        {
//            base.OnClosed(e);

//            try
//            {
//                if (_perforceService is IDisposable disposableService)
//                {
//                    disposableService.Dispose();
//                }
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
//            }
//        }

//        public record ExportConfiguration
//        {
//            public required string UnrealEnginePath { get; init; }
//            public required string ProjectFile { get; init; }
//            public required bool ExportMeshes { get; init; }
//            public required bool ExportTextures { get; init; }
//            public string? MeshesDirectory { get; init; }
//            public string? TexturesDirectory { get; init; }
//            public required string OutputDirectory { get; init; }
//            public required bool OverwriteFiles { get; init; }
//            public required bool ConvertTextures { get; init; }
//        }

//        public record ExportResult
//        {
//            public bool Success { get; init; }
//            public string? ErrorMessage { get; init; }
//            public string[] ExportedFiles { get; init; } = Array.Empty<string>();
//        }
//    }
//}