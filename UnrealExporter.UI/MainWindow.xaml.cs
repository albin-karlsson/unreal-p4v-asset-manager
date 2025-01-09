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
using UnrealExporter.App.Exceptions;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Services;

namespace UnrealExporter.UI
{
    public partial class MainWindow : Window
    {
        private const string DEFAULT_UE_5_3_PATH = @"C:\Program Files\Epic Games\UE_5.3\Engine\Binaries\Win64\UnrealEditor.exe";

        private readonly IPerforceService _perforceService;
        private readonly IFileService _fileService;
        private readonly IUnrealService _unrealService;
        private readonly IAppConfig _appConfig;

        private string? _meshesSourceDirectory = null;
        private string? _texturesSourceDirectory = null;
        private string? _selectedUnrealProjectFile = null;

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
            try
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
                    throw new Exception("Validation errors: " + Environment.NewLine + errorMessage);
                }

                return true;
            }
            catch (Exception ex) 
            {
                ShowError(ex.Message);
                return false;
            }
        }

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

        private void btnGetPerforceWorkspaces_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cboxPerforceWorkspace.Items.Clear();

                string trimmedUsername = txtPerforceUsername.Text.Trim();
                string trimmedPassword = txtPerforcePassword.Password.Trim();

                if (trimmedUsername.Length > 0 && trimmedPassword.Length > 0)
                {
                    bool isSuccessfulLogin = _perforceService.LogIn(trimmedUsername, trimmedPassword);

                    if (!isSuccessfulLogin)
                    {
                        throw new ServiceException("Failed to login to Perforce. Check your username and password and try again.");
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
                    throw new Exception("No Perforce username or password specified.");
                }
            }
            catch (Exception ex) {
                ShowError(ex.Message);
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
                    _selectedUnrealProjectFile!,
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
                    throw new ServiceException("Error exporting from Unreal.");
                }

                string[] exportedFiles = _fileService.GetExportedFiles();

                if (!exportedFiles.Any())
                {
                    throw new ServiceException("No files found to export");
                }

                if (_appConfig.ExportTextures)
                {
                    _fileService.ConvertTextures();

                    if (!_fileService.TextureConversionSuccessful)
                    {
                        throw new ServiceException("DDS conversion failed! Export canceled. Check file names and try again.");
                    }
                }

                PreviewWindow previewWindow = new(exportedFiles);

                if ((bool)previewWindow.ShowDialog()!)
                {
                    if (previewWindow.SelectedFiles == null || !previewWindow.SelectedFiles!.Any())
                    {
                        throw new Exception("No files selected for submit");
                    }

                    if (previewWindow.SubmitMessage == null)
                    {
                        throw new Exception("No valid submit message found");
                    }

                    _appConfig.SubmitMessage = previewWindow.SubmitMessage!;

                    var (exportMeshes, exportTextures) = _fileService.GetSelectedFilesFileTypes(previewWindow.SelectedFiles!);
                    _appConfig.ExportMeshes = exportMeshes;
                    _appConfig.ExportTextures = exportTextures;

                    _fileService.MoveDirectories(previewWindow.SelectedFiles!);

                    _perforceService.AddFilesToPerforce(_fileService.ExportedFiles);
                    _perforceService.Disconnect();

                    ConfirmationWindow confirmationWindow = new(_fileService.ExportedFiles, _appConfig.SubmitMessage); 
                    confirmationWindow.Show();
                }
            }
            catch (Exception ex)
            {
                ShowError($"An unexpected error occurred:" + Environment.NewLine + ex.Message);
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
            txtUnrealEnginePath.Text = DEFAULT_UE_5_3_PATH;
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
                            var selectionWindow = new SelectionWindow(projectFiles);
                            bool? result = selectionWindow.ShowDialog();

                            if (result == true)
                            {
                                if (selectionWindow.SelectedProject != null)
                                {
                                    _selectedUnrealProjectFile = selectionWindow.SelectedProject;
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
}