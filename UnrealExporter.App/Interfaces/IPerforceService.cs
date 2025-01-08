using Perforce.P4;

namespace UnrealExporter.App.Interfaces
{
    public interface IPerforceService
    {
        public string WorkspacePath { get; set; }
        public string SubmitMessage { get; set; }
        public ConnectionStatus ConnectionStatus { get; }


        public List<string>? GetWorkspaces();
        public void Connect(string workspace);
        public bool LogIn(string username, string password);
        public void Disconnect();
        public string[] GetUnrealProjectPathFromPerforce();
        public void AddFilesToPerforce(List<string> exportedFiles, string outputPath, bool exportMeshes, bool exportTextures);
        public void SubmitChanges();
    }
}