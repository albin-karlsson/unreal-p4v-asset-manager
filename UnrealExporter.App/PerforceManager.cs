using Perforce.P4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UnrealExporter.App.Interfaces;

namespace UnrealExporter.App;

public class PerforceManager : IPerforceService
{
    private const string SERVER_URI = "ssl:perforce.tga.learnet.se:1666";

    private string _workspace;
    private Server _server;
    private Repository _repository;

    public string WorkspacePath { get; set; }
    public string SubmitMessage { get; set; }

    public ConnectionStatus ConnectionStatus { get { return  _repository.Connection.Status; } }

    public PerforceManager()
    {
        _server = new Server(new ServerAddress(SERVER_URI));
        _repository = new Repository(_server);
    }

    public List<string>? GetWorkspaces()
    {
        try
        {
            Options options = new();

            IList<Client> clients = _repository.GetClients(options)
                                               .Where(c => c.OwnerName == _repository.Connection.UserName)
                                               .ToList();

            if (clients != null && clients.Count > 0)
            {
                Console.WriteLine($"Found {clients.Count} workspaces:");

                List<string> workspaces = new();

                foreach (var client in clients)
                {
                    workspaces.Add(client.Name);
                }

                return workspaces;
            }

            Console.WriteLine("No workspaces found.");
            return null;
        }
        catch (P4Exception ex)
        {
            Console.WriteLine($"Perforce error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error: {ex.Message}");
            return null;
        }
    }

    public void Connect(string workspace)
    {
        _workspace = workspace;

        Client client = _repository.GetClient(workspace);

         _repository.Connection.Client = client;
        WorkspacePath = @$"C:\Users\{ _repository.Connection.UserName}\Perforce\{_workspace}\";

        // Sync files
        Sync();
    }

    public bool LogIn(string username, string password)
    {
        Console.WriteLine("Attempting to login to Perforce.");

            _repository.Connection.UserName = username;

        if ( _repository.Connection.Connect(null))
        {
            Console.WriteLine("Connected to Perforce.");

                _repository.Connection.Login(password);

            return true;
        }

        return false;

    }

    public void Disconnect()
    {
         _repository.Connection.Disconnect();
    }

    public string[] GetUnrealProjectPathFromPerforce()
    {
        string[] projectFiles = Directory.GetFiles(WorkspacePath, "*.uproject", SearchOption.AllDirectories);

        if (projectFiles.Length > 0)
        {
            return projectFiles;
        }

        return new string[0];
    }

    private void Sync()
    {
        Console.WriteLine("Syncing Perforce.");

        Options syncOptions = new Options(SyncFilesCmdFlags.None, -1);

        // TODO: Return the error message to the UI
        try
        {
             _repository.Connection.Client.SyncFiles(syncOptions, null);
        }
        catch(Exception e)
        {

        }
    }

    public void AddFilesToPerforce(List<string> exportedFiles, string outputPath, bool exportMeshes, bool exportTextures)
    {
        try
        {
            string exportPathRoot = outputPath;

            string meshesExportPath = exportMeshes && exportTextures
                ? Path.Combine(outputPath, "Meshes")
                : outputPath;

            string texturesExportPath = exportMeshes && exportTextures
                ? Path.Combine(outputPath, "Textures")
                : outputPath;

            string[] meshFilePaths = Directory.GetFiles(meshesExportPath);
            string[] textureFilePaths = Directory.GetFiles(texturesExportPath);

            List<string> allFilePaths = meshFilePaths.Concat(textureFilePaths).ToList();

            for (int i = allFilePaths.Count - 1; i >= 0; i--)
            {
                var filePath = allFilePaths[i];
                if (!exportedFiles.Contains(filePath))
                {
                    allFilePaths.RemoveAt(i); 
                }
            }

            string[] filePaths = allFilePaths.ToArray();

            if (filePaths.Length == 0)
            {
                Console.WriteLine("No files found in the specified directory.");
                return;
            }

            List<FileSpec> fileSpecs = new List<FileSpec>();
            foreach (string filePath in filePaths)
            {
                fileSpecs.Add(new FileSpec(new LocalPath(filePath)));
            }

            // Step 1: Add the files
            AddOrEditFiles(fileSpecs.ToArray());
            SubmitChanges();
        }
        catch (P4Exception ex)
        {
            Console.WriteLine($"Perforce error: {ex.Message}");
            Console.WriteLine($"Error code: {ex.ErrorCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error: {ex.Message}");
        }
    }

    private void AddOrEditFiles(FileSpec[] fileSpecs)
    {
        try
        {
            List<FileSpec> filesToAdd = new List<FileSpec>();
            List<FileSpec> filesToEdit = new List<FileSpec>();
            //List<FileSpec> filesToDelete = new List<FileSpec>();

            foreach (var fileSpec in fileSpecs)
            {
                // Check if the file exists in Perforce
                Options options = new();
                IList<FileMetaData> fileMetaDataList = _repository.GetFileMetaData(options, new FileSpec[] { fileSpec });

                if (fileMetaDataList == null || fileMetaDataList.Count == 0 || fileMetaDataList[0].HeadAction == FileAction.Delete)
                {
                    filesToAdd.Add(fileSpec);
                }
                else
                {
                    // If the file exists, mark it for editing
                    filesToEdit.Add(fileSpec);
                }
            }

            // Add new files
            if (filesToAdd.Count > 0)
            {
                Console.WriteLine($"Adding {filesToAdd.Count} new files to Perforce...");
                Options addOptions = new Options();
                _repository.Connection.Client.AddFiles(addOptions, filesToAdd.ToArray());
            }

            // Edit existing files
            if (filesToEdit.Count > 0)
            {
                Console.WriteLine($"Marking {filesToEdit.Count} existing files for edit in Perforce...");
                Options editOptions = new Options();
                _repository.Connection.Client.EditFiles(editOptions, filesToEdit.ToArray());
            }

            Console.WriteLine($"Processed {fileSpecs.Length} files in total.");
        }
        catch (P4Exception ex)
        {
            Console.WriteLine($"Perforce error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error: {ex.Message}");
        }
    }

    public void SubmitChanges()
    {
        try
        {
            Console.WriteLine("Submitting changes to Perforce...");

            Options options = new();

            FileSpec fileSpec = new FileSpec(new DepotPath("//..."), null);  // This pattern matches all files
            List<FileSpec> fileSpecList = new List<FileSpec>();
            fileSpecList.Add(fileSpec);

            IList<Perforce.P4.File> openedFiles = _repository.GetOpenedFiles(fileSpecList, options);

            if (openedFiles.Count == 0)
            {
                Console.WriteLine("No pending changes to submit.");
                return;
            }

            var changelist = new Changelist();
            changelist.Description = SubmitMessage;

            foreach (var file in openedFiles)
            {
                changelist.Files.Add(file);
            }

            changelist.OwnerName =  _repository.Connection.UserName;
            changelist.ClientId = _workspace;
            changelist.initialize( _repository.Connection);

            changelist = _repository.CreateChangelist(changelist);

            // Submit the changelist
            changelist.Submit(new Options());

            Console.WriteLine($"Changes submitted successfully.");
        }
        catch (P4Exception ex)
        {
            Console.WriteLine($"Perforce submit error: {ex.Message}");
            Console.WriteLine($"Error code: {ex.ErrorCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error during submit: {ex.Message}");
        }
    }
}
