using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App.Interfaces
{
    public interface IAppConfig
    {
        bool ExportMeshes { get; set; }
        bool ExportTextures { get; set; }
        string DestinationDirectory { get; }
        bool ConvertTextures { get; set; }
        bool OverwriteFiles { get; set; }
        string UnrealEnginePath { get; set; }
        string UnrealProjectFile { get; set; }
        string MeshesSourceDirectory { get; set; }
        string TexturesSourceDirectory { get; set; }
        // Extra properties
        string SubmitMessage { get; set; }

        public void SetConfiguration(
            bool exportMeshes,
            bool exportTextures,
            string destinationDirectory,
            bool convertTextures,
            bool overwriteFiles,
            string unrealEnginePath,
            string unrealProjectFile,
            string meshesSourceDirectory,
            string texturesSourceDirectory);
    }
}
