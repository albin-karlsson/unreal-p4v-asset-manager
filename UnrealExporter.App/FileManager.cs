using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App;

public class FileManager
{
    private string _perforceDestinationDirectory;
    private readonly string sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Deps");
    private readonly string ddsExecutable = "_dds.exe";
    private readonly string texConvExecutable = "_texConv.exe";
    private readonly string texDiagExecutable = "_texdiag.exe";
    private readonly string _outputFolder = "C:/UnrealExport";
    private readonly bool _overwriteFiles;
    private bool _exportMeshes;
    private bool _exportTextures;
    private bool _convertTextures;
    private string _texturesOutputDirectory;

    public bool TextureConversionSuccessful { get; set; }
    public List<string> ExportedFiles { get; set; } = new();

    public FileManager(string destinationDirectory, bool overwriteFiles, bool exportMeshes, bool exportTextures, bool convertTextures)
    {
        _overwriteFiles = overwriteFiles;
        _exportMeshes = exportMeshes;
        _exportTextures = exportTextures;
        _convertTextures = convertTextures;
        _texturesOutputDirectory = Path.Combine(_outputFolder, "Textures");
        _perforceDestinationDirectory = destinationDirectory;
    }

    public bool CheckExistingOutputDirectory()
    {
        if(Path.Exists(_outputFolder))
        {
            return true;
        }

        return false;
    }

    public void ConvertTextures()
    {
        if (_exportTextures && _convertTextures)
        {
            Console.WriteLine(@"Copying executables to C:\UnrealExport\Textures\");

            // Copy the executables
            CopyFile(ddsExecutable);
            CopyFile(texConvExecutable);
            CopyFile(texDiagExecutable);

            // Run the _dds.exe after copying
            Console.WriteLine("Running DDS conversion.");
            RunDDSExecutable();

            if (CheckForSuccess())
            {
                Console.WriteLine("DDS conversion successful.");
                // TODO: Really remove pngs?
                Console.WriteLine("Removing PNGs and executables.");
                CleanUp();

                TextureConversionSuccessful = true;
            }
            else
            {
                TextureConversionSuccessful = false;

                return;
            }
        }
    }

    /// <summary>
    /// Checks the array of selected files, and if they are still containing dds files and/or meshes.
    /// </summary>
    /// <param name="selectedFiles"></param>
    /// <returns>An array of bools where the first bool indicates if meshes should be exported, and the second one if textures should be exported</returns>
    public bool[] CheckSelectedFilesFiletypes(string[] selectedFiles)
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
            string sourceFilePath = Path.Combine(sourceDirectory, fileName);
            string destinationFilePath = Path.Combine(_texturesOutputDirectory, fileName);

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
            string ddsPath = Path.Combine(_texturesOutputDirectory, ddsExecutable);

            if (File.Exists(ddsPath))
            {
                Process process = new Process();
                process.StartInfo.FileName = ddsPath;
                process.StartInfo.WorkingDirectory = _texturesOutputDirectory;
                process.Start(); // Start the _dds.exe process
                process.WaitForExit(); // Optionally wait for the process to exit

                Console.WriteLine($"{ddsExecutable} executed successfully.");
            }
            else
            {
                Console.WriteLine($"{ddsExecutable} does not exist in the destination directory.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running {ddsExecutable}: {ex.Message}");
        }
    }

    // Method to delete all PNG files in the destination directory
    private void DeletePNGFiles()
    {
        try
        {
            var pngFiles = Directory.GetFiles(_texturesOutputDirectory, "*.png");

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
            string[] executables = { ddsExecutable, texConvExecutable, texDiagExecutable };

            foreach (var exe in executables)
            {
                string exePath = Path.Combine(_texturesOutputDirectory, exe);

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
    public void MoveDirectories(string[] filesToExport)
    {
        try
        {
            List<string> sourceDirectories = new();

            if(_exportMeshes)
            {
                sourceDirectories.Add(Path.Combine(_outputFolder, "Meshes"));
            }

            if(_exportTextures)
            {
                sourceDirectories.Add(Path.Combine(_outputFolder, "Textures"));

            }

            if (!Directory.Exists(_perforceDestinationDirectory))
            {
                Directory.CreateDirectory(_perforceDestinationDirectory);
            }

            // Move the folders to the folder in Perforce
            foreach (var sourceDirectory in sourceDirectories) 
            {
                MoveFolder(filesToExport, sourceDirectory, _perforceDestinationDirectory);
            }

            Console.WriteLine($"Moved folder(s) to {_perforceDestinationDirectory}");
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
            var ddsFiles = Directory.GetFiles(_texturesOutputDirectory, "*.dds");
            // Get the number of .png files
            var pngFiles = Directory.GetFiles(_texturesOutputDirectory, "*.png");

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
    private void RemoveReadOnlyAttributeFromFiles(string[] sourceFiles, string destinationFolder)
    {
        try
        {
            //// Get all files in the source folder (recursively)
            //var sourceFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories);

            foreach (var sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile); 
                string destinationFile = Path.Combine(destinationFolder, fileName);

                if (File.Exists(destinationFile) && _overwriteFiles)
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

    private void RemoveFilesPartOfThisExport(string[] sourceFiles, string destinationFolder)
    {
        try
        {
            //var sourceFiles = Directory.GetFiles(sourceFolder);

            foreach (var sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string destinationFile = Path.Combine(destinationFolder, fileName);

                if (File.Exists(destinationFile) && _overwriteFiles)
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
    private void MoveFolder(string[] filesToExport, string sourceFolder, string destinationFolder)
    {
        /*
         * TODO:
         * If only meshes are selected or only textures, Path.GetFileName should not be added
         */

        try
        {
            if (Directory.Exists(sourceFolder))
            {
                string destinationPath;

                if (_exportMeshes && _exportTextures)
                {
                    destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourceFolder));
                }
                else
                {
                    destinationPath = Path.Combine(destinationFolder);
                }               

                if (Directory.Exists(destinationPath))
                {
                    RemoveReadOnlyAttributeFromFiles(filesToExport, destinationPath);

                    RemoveFilesPartOfThisExport(filesToExport, destinationPath);
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
                Console.WriteLine($"Source folder {sourceFolder} does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving folder {sourceFolder}: {ex.Message}");
        }
    }

    public List<string> CheckDestinationFolderContent()
    {
        List<string> filesToExclude = new();
        // Check meshes
        if (_exportMeshes)
        {
            string meshPath = Path.Combine(_perforceDestinationDirectory, "Meshes");
            if (Directory.Exists(meshPath))
            {
                filesToExclude.AddRange(Directory.GetFiles(meshPath, "*.fbx", SearchOption.AllDirectories));
            }
        }
        // Check textures
        if (_exportTextures)
        {
            string texturePath = Path.Combine(_perforceDestinationDirectory, "Textures");
            if (Directory.Exists(texturePath))
            {
                filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.dds", SearchOption.AllDirectories));
                filesToExclude.AddRange(Directory.GetFiles(texturePath, "*.png", SearchOption.AllDirectories));
            }
        }

        return filesToExclude;
    }

    public string[] CheckFilesToExport()
    {
        string[] filesToExport = Directory.GetFiles(_outputFolder, "*.*", SearchOption.AllDirectories);

        return filesToExport;
    }
}
