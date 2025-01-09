using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnrealExporter.App.Configs;
using UnrealExporter.App.Exceptions;
using UnrealExporter.App.Interfaces;

namespace UnrealExporter.App.Services;

public class FileService : IFileService
{
    private const string EXPORT_DIRECTORY = "C:/UnrealExport";
    private readonly string APP_DEPS = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Deps");
    private const string DDS_EXE = "_dds.exe";
    private const string TEX_CONV_EXE = "_textConv.exe";
    private const string TEX_DIAG_EXE = "_texdiag.exe";

    private readonly IAppConfig _appConfig;

    public bool TextureConversionSuccessful { get; set; }
    public List<string> ExportedFiles { get; set; } = new();

    public FileService(IAppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    public bool CheckExistingOutputDirectory()
    {
        if (Path.Exists(EXPORT_DIRECTORY))
        {
            return true;
        }

        return false;
    }

    public async Task ConvertTextures()
    {
        if (_appConfig.ExportTextures && _appConfig.ConvertTextures)
        {
            Console.WriteLine(@"Copying executables to C:\UnrealExport\Textures\");
            try
            {
                CopyFile(DDS_EXE);
                CopyFile(TEX_CONV_EXE);
                CopyFile(TEX_DIAG_EXE);

                await RunDDSExecutable();

                if (CheckForSuccess())
                {
                    // Remove PNGs and executables
                    CleanUp();

                    TextureConversionSuccessful = true;
                }
                else
                {
                    TextureConversionSuccessful = false;
                }
            }
            catch(ServiceException ex)
            {
                throw new ServiceException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }

    /// <summary>
    /// Checks the array of selected files, and if they are still containing dds files and/or meshes.
    /// </summary>
    /// <param name="selectedFiles"></param>
    /// <returns>An array of bools where the first bool indicates if meshes should be exported, and the second one if textures should be exported</returns>
    public (bool exportMeshes, bool exportTextures) GetSelectedFilesFileTypes(string[] selectedFiles)
    {
        bool selectedMeshes = selectedFiles.Any(f => Path.GetExtension(f).ToLower().Contains("fbx"));
        bool selectedTextures = selectedFiles.Any(f => Path.GetExtension(f).ToLower().Contains("dds") || Path.GetExtension(f).ToLower().Contains("png"));

        // Return a tuple with named values for meshes and textures
        return (selectedMeshes, selectedTextures);
    }

    private void CopyFile(string fileName)
    {
        try
        {
            string sourceFilePath = Path.Combine(APP_DEPS, fileName);
            string destinationFilePath = Path.Combine(Path.Combine(EXPORT_DIRECTORY, "Textures"), fileName);

            // Check if the source file exists before attempting to copy
            if (File.Exists(sourceFilePath))
            {
                File.Copy(sourceFilePath, destinationFilePath, true); // Overwrite if file exists
                Console.WriteLine($"{fileName} copied successfully to {destinationFilePath}");
            }
            else
            {
                Console.WriteLine($"Source file {sourceFilePath} does not exist.");
            }
        }
        catch (Exception ex)
        {
            throw new ServiceException(ex.Message);
        }
    }

    private async Task RunDDSExecutable()
    {
        try
        {
            string ddsPath = Path.Combine(Path.Combine(EXPORT_DIRECTORY, "Textures"), DDS_EXE);

            if (File.Exists(ddsPath))
            {
                Process process = new Process();
                process.StartInfo.FileName = ddsPath;
                process.StartInfo.WorkingDirectory = Path.Combine(EXPORT_DIRECTORY, "Textures");
                process.Start();

                if (process != null)
                {
                    await Task.Run(() => process.WaitForExit());
                }

                Console.WriteLine($"{DDS_EXE} executed successfully.");
            }
            else
            {
                throw new ServiceException($"{DDS_EXE} does not exist in the destination directory.");
            }
        }
        catch (Exception ex)
        {
            throw new ServiceException(ex.Message);
        }
    }

    // Method to delete all PNG files in the destination directory
    private void DeletePNGFiles()
    {
        try
        {
            var pngFiles = Directory.GetFiles(Path.Combine(EXPORT_DIRECTORY, "Textures"), "*.png");

            foreach (var file in pngFiles)
            {
                File.Delete(file);
                Console.WriteLine($"Deleted PNG file: {file}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    // Method to delete executables (_dds, _texConv, _texdiag)
    private void DeleteExecutables()
    {
        try
        {
            string[] executables = { DDS_EXE, TEX_CONV_EXE, TEX_DIAG_EXE };

            foreach (var exe in executables)
            {
                string exePath = Path.Combine(Path.Combine(EXPORT_DIRECTORY, "Textures"), exe);

                if (File.Exists(exePath))
                {
                    File.Delete(exePath);
                    Console.WriteLine($"Deleted executable: {exePath}");
                }
                else
                {
                    Console.WriteLine($"Executable {exePath} not found.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting executables: {ex.Message}");
        }
    }

    private void CleanUp()
    {
        DeletePNGFiles();
        DeleteExecutables();
    }

    public void MoveDirectories(string[] filesToExport)
    {
        try
        {
            List<string> sourceDirectories = new();

            if (_appConfig.ExportMeshes)
            {
                sourceDirectories.Add(Path.Combine(EXPORT_DIRECTORY, "Meshes"));
            }

            if (_appConfig.ExportTextures)
            {
                sourceDirectories.Add(Path.Combine(EXPORT_DIRECTORY, "Textures"));

            }

            if (!Directory.Exists(_appConfig.DestinationDirectory))
            {
                Directory.CreateDirectory(_appConfig.DestinationDirectory);
            }

            // Move the folders to the folder in Perforce
            foreach (var sourceDirectory in sourceDirectories)
            {
                MoveDirectory(filesToExport, sourceDirectory);
            }

            Console.WriteLine($"Moved folder(s) to {_appConfig.DestinationDirectory}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving directories: {ex.Message}");
        }
    }

    private bool CheckForSuccess()
    {
        try
        {
            // Get the number of .dds files
            var ddsFiles = Directory.GetFiles(Path.Combine(EXPORT_DIRECTORY, "Textures"), "*.dds");
            // Get the number of .png files
            var pngFiles = Directory.GetFiles(Path.Combine(EXPORT_DIRECTORY, "Textures"), "*.png");

            // Compare the counts of .dds and .png files
            if (ddsFiles.Length == pngFiles.Length || ddsFiles.Length > pngFiles.Length)
            {
                Console.WriteLine("Success: The number of .dds and .png files are equal or there are more .dds files than .png files.");
                return true;
            }
            else
            {
                Console.WriteLine("Failure: The number of .dds and .png files are not equal.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for success: {ex.Message}");
            return false; // Return false in case of an error
        }
    }

    // Method to remove the read-only attribute from all files in a directory
    private void RemoveReadOnlyAttributeFromFiles(string[] sourceFiles)
    {
        try
        {
            foreach (var sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string destinationFile = Path.Combine(_appConfig.DestinationDirectory, fileName);

                if (File.Exists(destinationFile) && _appConfig.OverwriteFiles)
                {
                    FileInfo fileInfo = new FileInfo(destinationFile);

                    if (fileInfo.IsReadOnly)
                    {
                        fileInfo.IsReadOnly = false;
                        Console.WriteLine($"Read-only attribute removed from: {fileInfo.FullName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing read-only attributes from files: {ex.Message}");
        }
    }

    private void RemoveFilesPartOfThisExport(string[] sourceFiles)
    {
        try
        {
            foreach (var sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string destinationFile = Path.Combine(_appConfig.DestinationDirectory, fileName);

                if (File.Exists(destinationFile) && _appConfig.OverwriteFiles)
                {
                    File.Delete(destinationFile);
                    Console.WriteLine($"Deleted file: {destinationFile}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing files: {ex.Message}");
        }
    }

    // Method to move a folder from source to destination
    private void MoveDirectory(string[] filesToExport, string sourceDirectory)
    {
        try
        {
            if (Directory.Exists(sourceDirectory))
            {
                string destinationPath;

                if (_appConfig.ExportMeshes && _appConfig.ExportTextures)
                {
                    destinationPath = Path.Combine(_appConfig.DestinationDirectory, Path.GetFileName(sourceDirectory));
                }
                else
                {
                    destinationPath = Path.Combine(_appConfig.DestinationDirectory);
                }

                if (Directory.Exists(destinationPath))
                {
                    RemoveReadOnlyAttributeFromFiles(filesToExport);
                    RemoveFilesPartOfThisExport(filesToExport);
                }

                foreach (var fileToExport in filesToExport)
                {
                    string destinationFile = Path.Combine(destinationPath, Path.GetFileName(fileToExport));

                    if (File.Exists(destinationFile))
                    {
                        continue;
                    }

                    Directory.Move(fileToExport, destinationFile);
                    ExportedFiles.Add(destinationFile);
                }

                Console.WriteLine($"Files moved to {destinationPath}");
            }
            else
            {
                Console.WriteLine($"Source directory {sourceDirectory} does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving directory {sourceDirectory}: {ex.Message}");
        }
    }

    public List<string> CheckDestinationDirectoryForExistingFiles()
    {
        List<string> filesToExclude = new();

        if(_appConfig.ExportMeshes && _appConfig.ExportTextures)
        {
            if (_appConfig.ExportMeshes)
            {
                string meshPath = Path.Combine(_appConfig.DestinationDirectory, "Meshes");
                if (Directory.Exists(meshPath))
                {
                    filesToExclude.AddRange(Directory.GetFiles(meshPath, "*.fbx", SearchOption.AllDirectories));
                }
            }
            if (_appConfig.ExportTextures)
            {
                string texturePath = Path.Combine(_appConfig.DestinationDirectory, "Textures");
                if (Directory.Exists(texturePath))
                {
                    filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.dds", SearchOption.AllDirectories));
                    filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.png", SearchOption.AllDirectories));
                }
            }
        }
        else
        {
            if (_appConfig.ExportMeshes)
            {
                string meshPath = _appConfig.DestinationDirectory;
                if (Directory.Exists(meshPath))
                {
                    filesToExclude.AddRange(Directory.GetFiles(meshPath, "*.fbx", SearchOption.AllDirectories));
                }
            }
            if (_appConfig.ExportTextures)
            {
                string texturePath = _appConfig.DestinationDirectory;
                if (Directory.Exists(texturePath))
                {
                    filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.dds", SearchOption.AllDirectories));
                    filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.png", SearchOption.AllDirectories));
                }
            }
        }


        return filesToExclude;
    }

    public string[] GetExportedFiles()
    {
        string[] filesToExport = Directory.GetFiles(EXPORT_DIRECTORY, "*.*", SearchOption.AllDirectories);

        return filesToExport;
    }
}
