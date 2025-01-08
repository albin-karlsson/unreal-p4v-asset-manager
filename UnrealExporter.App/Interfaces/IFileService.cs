using UnrealExporter.App.Models;

namespace UnrealExporter.App.Interfaces
{
    public interface IFileService
    {
        bool TextureConversionSuccessful { get; set; }
        List<string> ExportedFiles { get; set; }

        bool CheckExistingOutputDirectory();
        void ConvertTextures(ExportConfig exportConfig);
        bool[] SetSelectedFilesFileTypes(string[] selectedFiles);
        void MoveDirectories(string[] filesToExport, ExportConfig exportConfig);
        List<string> CheckDestinationDirectoryContent(ExportConfig exportConfig);

        string[] GetExportedFiles();
    }
}