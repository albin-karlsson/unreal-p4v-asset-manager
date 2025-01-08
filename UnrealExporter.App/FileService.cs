using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Models;

namespace UnrealExporter.App;

public class FileService : IFileService
{
    private const string EXPORT_DIRECTORY = "C:/UnrealExport";
    private readonly string APP_DEPS = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Deps");
    private const string DDS_EXE = "_dds.exe";
    private const string TEX_CONV_EXE = "_textConv.exe";
    private const string TEX_DIAG_EXE = "_texdiag.exe";

    private bool _exportMeshes;
    private bool _exportTextures;

    public bool TextureConversionSuccessful { get; set; }
    public List<string> ExportedFiles { get; set; } = new();

    public FileService()
    {

    }

    public bool CheckExistingOutputDirectory()
    {
        if(Path.Exists(EXPORT_DIRECTORY))
        {
            return true;
        }

        return false;
    }

    public void ConvertTextures(ExportConfig exportConfig)
    {
        if (exportConfig.ExportTextures && exportConfig.ConvertTextures)
        {
            Console.WriteLine(@"Copying executables to C:\UnrealExport\Textures\");

            CopyFile(DDS_EXE);
            CopyFile(TEX_CONV_EXE);
            CopyFile(TEX_DIAG_EXE);

            RunDDSExecutable();

            if (CheckForSuccess())
            {
                Console.WriteLine("DDS conversion successful.");
                Console.WriteLine("Removing PNGs and executables.");
                CleanUp();

                TextureConversionSuccessful = true;
            }
            else
            {
                TextureConversionSuccessful = false;
            }
        }
    }

    /// <summary>
    /// Checks the array of selected files, and if they are still containing dds files and/or meshes.
    /// </summary>
    /// <param name="selectedFiles"></param>
    /// <returns>An array of bools where the first bool indicates if meshes should be exported, and the second one if textures should be exported</returns>
    public bool[] SetSelectedFilesFileTypes(string[] selectedFiles)
    {
        _exportMeshes = selectedFiles.Any(f => Path.GetExtension(f).ToLower().Contains("fbx"));
        _exportTextures = selectedFiles.Any(f => Path.GetExtension(f).ToLower().Contains("dds") || Path.GetExtension(f).ToLower().Contains("png"));

        // Return a 1D array with two boolean values: one for meshes and one for textures
        return new bool[] { _exportMeshes, _exportTextures };
    }

    // Method to copy a file from the source directory to the destination directory
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
            Console.WriteLine($"Error copying {fileName}: {ex.Message}");
        }
    }

    // Method to run the _dds.exe executable
    private void RunDDSExecutable()
    {
        try
        {
            string ddsPath = Path.Combine(Path.Combine(EXPORT_DIRECTORY, "Textures"), DDS_EXE);

            if (File.Exists(ddsPath))
            {
                Process process = new Process();
                process.StartInfo.FileName = ddsPath;
                process.StartInfo.WorkingDirectory = Path.Combine(EXPORT_DIRECTORY, "Textures");
                process.Start(); // Start the _dds.exe process
                process.WaitForExit(); // Optionally wait for the process to exit

                Console.WriteLine($"{DDS_EXE} executed successfully.");
            }
            else
            {
                Console.WriteLine($"{DDS_EXE} does not exist in the destination directory.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running {DDS_EXE}: {ex.Message}");
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
            Console.WriteLine($"Error deleting PNG files: {ex.Message}");
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

    // Cleanup method to delete PNG files and executables after running _dds
    private void CleanUp()
    {
        // Delete all PNG files
        DeletePNGFiles();

        // Delete executables (_dds, _texConv, _texdiag)
        DeleteExecutables();
    }

    // Method to move the folders to the workspace folder
    public void MoveDirectories(string[] filesToExport, ExportConfig exportConfig)
    {
        try
        {
            List<string> sourceDirectories = new();

            if(_exportMeshes)
            {
                sourceDirectories.Add(Path.Combine(EXPORT_DIRECTORY, "Meshes"));
            }

            if(_exportTextures)
            {
                sourceDirectories.Add(Path.Combine(EXPORT_DIRECTORY, "Textures"));

            }

            if (!Directory.Exists(exportConfig.DestinationDirectory))
            {
                Directory.CreateDirectory(exportConfig.DestinationDirectory);
            }

            // Move the folders to the folder in Perforce
            foreach (var sourceDirectory in sourceDirectories) 
            {
                MoveDirectory(filesToExport, sourceDirectory, exportConfig);
            }

            Console.WriteLine($"Moved folder(s) to {exportConfig.DestinationDirectory}");
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
    private void RemoveReadOnlyAttributeFromFiles(string[] sourceFiles, ExportConfig exportConfig)
    {
        try
        {
            foreach (var sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile); 
                string destinationFile = Path.Combine(exportConfig.DestinationDirectory, fileName);

                if (File.Exists(destinationFile) && exportConfig.OverwriteFiles)
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

    private void RemoveFilesPartOfThisExport(string[] sourceFiles, ExportConfig exportConfig)
    {
        try
        {
            //var sourceFiles = Directory.GetFiles(sourceFolder);

            foreach (var sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string destinationFile = Path.Combine(exportConfig.DestinationDirectory, fileName);

                if (File.Exists(destinationFile) && exportConfig.OverwriteFiles)
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
    private void MoveDirectory(string[] filesToExport, string sourceDirectory, ExportConfig exportConfig)
    {
        try
        {
            if (Directory.Exists(sourceDirectory))
            {
                string destinationPath;

                if (_exportMeshes && _exportTextures)
                {
                    destinationPath = Path.Combine(exportConfig.DestinationDirectory, Path.GetFileName(sourceDirectory));
                }
                else
                {
                    destinationPath = Path.Combine(exportConfig.DestinationDirectory);
                }               

                if (Directory.Exists(destinationPath))
                {
                    RemoveReadOnlyAttributeFromFiles(filesToExport, exportConfig);

                    RemoveFilesPartOfThisExport(filesToExport, exportConfig);
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

    public List<string> CheckDestinationDirectoryContent(ExportConfig exportConfig)
    {
        List<string> filesToExclude = new();

        if (_exportMeshes)
        {
            string meshPath = Path.Combine(exportConfig.DestinationDirectory, "Meshes");
            if (Directory.Exists(meshPath))
            {
                filesToExclude.AddRange(Directory.GetFiles(meshPath, "*.fbx", SearchOption.AllDirectories));
            }
        }
        if (_exportTextures)
        {
            string texturePath = Path.Combine(exportConfig.DestinationDirectory, "Textures");
            if (Directory.Exists(texturePath))
            {
                filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.dds", SearchOption.AllDirectories));
                filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.png", SearchOption.AllDirectories));
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
