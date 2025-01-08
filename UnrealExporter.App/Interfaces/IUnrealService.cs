using UnrealExporter.App.Configs;
using UnrealExporter.App.Models;

namespace UnrealExporter.App.Interfaces
{
    public interface IUnrealService
    {
        void InitializeExport();
        Task<ExportResult> ExportAssetsAsync(List<string>? filesToExcludeFromExport);
    }
}