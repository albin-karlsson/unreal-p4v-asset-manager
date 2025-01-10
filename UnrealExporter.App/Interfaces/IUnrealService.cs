using UnrealExporter.App.Configs;

namespace UnrealExporter.App.Interfaces
{
    public interface IUnrealService
    {
        void InitializeExport();
        Task<bool> ExportAssetsAsync(List<string>? filesToExcludeFromExport);
    }
}