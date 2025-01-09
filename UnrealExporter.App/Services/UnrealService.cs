using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnrealExporter.App.Configs;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Models;

namespace UnrealExporter.App.Services;

public class UnrealService : IUnrealService
{
    private const string EXPORT_DIRECTORY = "C:/UnrealExport";
    private readonly string PYTHON_SCRIPT_DESTINATION_PATH = Path.Combine("D:/", "SC_UE_ExportAssetsFromUnreal.py");
    private readonly string PYTHON_SCRIPT_SOURCE_PATH = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "SC_UE_ExportAssetsFromUnreal.py");

    private readonly IAppConfig _appConfig;

    public UnrealService(IAppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    private void RemovePythonScriptFile()
    {
        if (File.Exists(PYTHON_SCRIPT_DESTINATION_PATH))
        {
            File.Delete(PYTHON_SCRIPT_DESTINATION_PATH);
        }
    }

    /// <summary>
    /// Copy the Python script that exports assets from Unreal to "D:/".
    /// </summary>
    private void CopyPythonScriptToDestination()
    {
        try
        {
            if (!File.Exists(PYTHON_SCRIPT_DESTINATION_PATH) || File.GetLastWriteTime(PYTHON_SCRIPT_SOURCE_PATH) > File.GetLastWriteTime(PYTHON_SCRIPT_DESTINATION_PATH))
            {
                File.Copy(PYTHON_SCRIPT_SOURCE_PATH, PYTHON_SCRIPT_DESTINATION_PATH, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error copying Python script: " + ex.Message);
        }
    }

    /// <summary>
    /// Runs the script that exports meshes and textures from Unreal. 
    /// </summary>
    /// <param name="filesToExcludeFromExport">A list of files that should be excluded from the export, to speed up the process</param>
    /// <returns>An ExportResult object containing a property if it was successful or not</returns>
    public async Task<ExportResult> ExportAssetsAsync(List<string>? filesToExcludeFromExport)
    {
        CopyPythonScriptToDestination();

        try
        {
            string directoryPath = Path.GetDirectoryName(_appConfig.UnrealEnginePath)!;
            string unrealEditorPath = Path.Combine(directoryPath, "UnrealEditor-Cmd.exe");
            string arguments = $"\"{unrealEditorPath}\" \"{_appConfig.UnrealProjectFile}\" -stdout -FullStdOutLogOutput -ExecutePythonScript=\"{PYTHON_SCRIPT_DESTINATION_PATH} {EXPORT_DIRECTORY} ";
            arguments += _appConfig.ExportMeshes ? $"{_appConfig.MeshesSourceDirectory} " : "None ";
            arguments += _appConfig.ExportTextures ? $"{_appConfig.TexturesSourceDirectory} " : "None ";

            if (filesToExcludeFromExport?.Any() == true)
            {
                string tempFilePath = Path.GetTempFileName();
                File.WriteAllText(tempFilePath, JsonSerializer.Serialize(filesToExcludeFromExport));
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
        if (Directory.Exists(EXPORT_DIRECTORY))
        {
            Directory.Delete(EXPORT_DIRECTORY, true);
        }
    }
}
