using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App
{
    public class AppConfig
    {
        public bool ExportMeshes { get; set; }
        public bool ExportTextures { get; set; }
        public required string DestinationDirectory { get; init; }
        public bool ConvertTextures { get; set; }
        public bool OverwriteFiles { get; set; }
        public List<string>? FilesToExcludeFromExport { get; set; }

        public string UnrealEnginePath { get; set; }
        public string UnrealProjectFile { get; set; }
        public string MeshesSourceDirectory { get; set; }
        public string TexturesSourceDirectory { get; set; }
        public string WorkspacePath { get; set; }
        public string SubmitMessage { get; set; }
    }
}
