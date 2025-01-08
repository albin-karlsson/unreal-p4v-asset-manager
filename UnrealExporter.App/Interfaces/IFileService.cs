using UnrealExporter.App.Configs;

namespace UnrealExporter.App.Interfaces
{
    public interface IFileService
    {
        bool TextureConversionSuccessful { get; set; }
        List<string> ExportedFiles { get; set; }

        bool CheckExistingOutputDirectory();
        void ConvertTextures(AppConfig appConfig);
        bool[] GetAndSetSelectedFilesFileTypes(string[] selectedFiles);
        void MoveDirectories(string[] filesToExport, AppConfig appConfig);
        List<string> CheckDestinationDirectoryContent(AppConfig appConfig);

        string[] GetExportedFiles();
    }
}