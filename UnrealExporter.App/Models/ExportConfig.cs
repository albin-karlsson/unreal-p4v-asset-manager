using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App.Models
{
    public class ExportConfig
    {
        public required string UnrealEnginePath { get; init; }
        public required string UnrealProjectFile { get; init; }
        public List<string>? FilesToExcludeFromExport { get; set; }
        public required bool ExportMeshes { get; set; }
        public required bool ExportTextures { get; set; }
        public string? MeshesSourceDirectory { get; init; }
        public string? TexturesSourceDirectory { get; init; }
        public required string DestinationDirectory { get; init; }
        public required bool OverwriteFiles { get; init; }
        public required bool ConvertTextures { get; init; }
    }
}
