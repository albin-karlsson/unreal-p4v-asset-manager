using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App.Models
{
    public class ExportConfiguration
    {
        public required string UnrealEnginePath { get; init; }
        public required string ProjectFile { get; init; }
        public string[] FilesToExclude { get; set; } = Array.Empty<string>(); 
        public required bool ExportMeshes { get; set; }
        public required bool ExportTextures { get; set; }
        public string? MeshesDirectory { get; init; }
        public string? TexturesDirectory { get; init; }
        public required string OutputDirectory { get; init; }
        public required bool OverwriteFiles { get; init; }
        public required bool ConvertTextures { get; init; }
    }
}
