using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnrealExporter.App.Interfaces;

namespace UnrealExporter.App.Configs
{
    public class AppConfig : IAppConfig
    {
        public bool ExportMeshes { get; set; }
        public bool ExportTextures { get; set; }
        public required string DestinationDirectory { get; set; }
        public bool ConvertTextures { get; set; }
        public bool OverwriteFiles { get; set; }
        public string UnrealEnginePath { get; set; }
        public string UnrealProjectFile { get; set; }
        public string MeshesSourceDirectory { get; set; }
        public string TexturesSourceDirectory { get; set; }
        // Extra properties
        public string SubmitMessage { get; set; }

        public void SetConfiguration(
            bool exportMeshes,
            bool exportTextures,
            string destinationDirectory,
            bool convertTextures,
            bool overwriteFiles,
            string unrealEnginePath,
            string unrealProjectFile,
            string meshesSourceDirectory,
            string texturesSourceDirectory)
        {
            ExportMeshes = exportMeshes;
            ExportTextures = exportTextures;
            DestinationDirectory = destinationDirectory;
            ConvertTextures = convertTextures;
            OverwriteFiles = overwriteFiles;
            UnrealEnginePath = unrealEnginePath;
            UnrealProjectFile = unrealProjectFile;
            MeshesSourceDirectory = meshesSourceDirectory;
            TexturesSourceDirectory = texturesSourceDirectory;
        }
    }
}
