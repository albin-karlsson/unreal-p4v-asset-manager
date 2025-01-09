using UnrealExporter.App.Configs;

namespace UnrealExporter.App.Interfaces
{
    public interface IFileService
    {
        bool TextureConversionSuccessful { get; set; }
        List<string> ExportedFiles { get; set; }

        bool CheckExistingOutputDirectory();
        void ConvertTextures();
        (bool exportMeshes, bool exportTextures) GetSelectedFilesFileTypes(string[] selectedFiles);
        void MoveDirectories(string[] filesToExport);
        List<string> CheckDestinationDirectoryForExistingFiles();

        string[] GetExportedFiles();
    }
}