using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Models;

namespace UnrealExporter.App;

public class UnrealManager : IUnrealService
{
    private string _unrealEnginePath;
    private string _projectFilePath;
    private bool _exportMeshes;
    private bool _exportTextures;
    private string? _meshesSourceDirectory;
    private string? _texturesSourceDirectory;
    private string _pythonScriptSourcePath; // Path of the original Python script
    private string _pythonScriptDestinationPath; // Destination path for the script in D:/
    string _outputFolder = "C:/UnrealExport";

    public UnrealManager()
    {
        
    }

    public UnrealManager(string unrealEnginePath, string projectFilePath, bool exportMeshes, bool exportTextures, string? meshesInputDirectory, string? texturesInputDirectory)
    {
        _unrealEnginePath = unrealEnginePath;
        _projectFilePath = projectFilePath;

        _exportMeshes = exportMeshes;
        _exportTextures = exportTextures;   

        _meshesSourceDirectory = meshesInputDirectory;
        _texturesSourceDirectory = texturesInputDirectory;   

        // Define source and destination paths for the Python script
        _pythonScriptSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "SC_UE_ExportAssetsFromUnreal.py");
        _pythonScriptDestinationPath = Path.Combine("D:/", "SC_UE_ExportAssetsFromUnreal.py");

        // Copy the Python script to D:/ if it doesn't already exist there or is outdated
        CopyPythonScriptToDestination();
    }

    private void RemovePythonScriptFile()
    {
        if(File.Exists(_pythonScriptDestinationPath))
        {
            File.Delete(_pythonScriptDestinationPath);
        }
    }

    // Method to copy the Python script to D:/
    private void CopyPythonScriptToDestination()
    {
        try
        {
            // Check if the file already exists in D:/ and if the source file is newer
            if (!File.Exists(_pythonScriptDestinationPath) || File.GetLastWriteTime(_pythonScriptSourcePath) > File.GetLastWriteTime(_pythonScriptDestinationPath))
            {
                File.Copy(_pythonScriptSourcePath, _pythonScriptDestinationPath, true);
                Console.WriteLine("Python script copied to D:/");
            }
            else
            {
                Console.WriteLine("Python script already exists and is up-to-date in D:/");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error copying Python script: " + ex.Message);
        }
    }

    public async Task<ExportResult> ExportAssetsAsync(ExportConfiguration exportConfig)
    {
        Console.WriteLine("Launching Unreal.");

        try
        {
            string unrealEditorPath = @"C:\Program Files\Epic Games\UE_5.3\Engine\Binaries\Win64\UnrealEditor-Cmd.exe";
            //string arguments = $"\"{unrealEditorPath}\" \"{_projectFilePath}\" -stdout -FullStdOutLogOutput -ExecutePythonScript=\"{_pythonScriptDestinationPath} {_outputFolder} ";
            string arguments = $"\"{unrealEditorPath}\" \"{_projectFilePath}\" -ExecutePythonScript=\"{_pythonScriptDestinationPath} {_outputFolder} ";
            arguments += _exportMeshes ? $"{_meshesSourceDirectory} " : "None ";
            arguments += _exportTextures ? $"{_texturesSourceDirectory} " : "None ";

            if (exportConfig.FilesToExclude.Any())
            {
                string tempFilePath = Path.GetTempFileName();
                File.WriteAllText(tempFilePath, JsonSerializer.Serialize(exportConfig.FilesToExclude));
                arguments += $"{tempFilePath}\"";
            }
            else
            {
                arguments += "None\"";
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C \"{arguments}\"",
                CreateNoWindow = false,
                UseShellExecute = false
            };

            Process? process = Process.Start(processStartInfo);
            if (process != null)
            {
                await Task.Run(() => process.WaitForExit());
            }

            Console.WriteLine("Unreal launched and script executed.");

            RemovePythonScriptFile();

            return new ExportResult { Success = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error launching Unreal Engine or running the script: " + ex.Message);

            return new ExportResult { Success = false };
        }
    }

    public void InitializeExport()
    {
        if(Directory.Exists(_outputFolder)) 
        {
            Directory.Delete(_outputFolder, true);
        }
    }
}
