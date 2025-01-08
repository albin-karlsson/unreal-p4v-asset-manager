namespace UnrealExporter.App.Interfaces
{
    public interface IFileService
    {
        bool TextureConversionSuccessful { get; set; }
        List<string> ExportedFiles { get; set; }

        bool CheckExistingOutputDirectory();
        void ConvertTextures();
        bool[] CheckSelectedFilesFiletypes(string[] selectedFiles);
        void MoveDirectories(string[] filesToExport);
        List<string> CheckDestinationFolderContent();

        string[] CheckFilesToExport();
    }
}