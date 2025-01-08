using Perforce.P4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App;

public class PerforceManager
{
    string _serverUri = "ssl:perforce.tga.learnet.se:1666";
    string _workspace;

    private Server _server;
    private Repository _repository;
    private Connection _connection;

    public string WorkspacePath { get; set; }
    public string SubmitMessage { get; set; }

    public PerforceManager()
    {
        _server = new Server(new ServerAddress(_serverUri));
        _repository = new Repository(_server);
        _connection = _repository.Connection;
    }

    public List<string>? GetWorkspaces()
    {
        Console.WriteLine("Retrieving workspaces...");

        try
        {
            // Define an option to filter the workspaces by user
            Options options = new();

            // Retrieve the list of clients (workspaces) for the user
            IList<Client> clients = _repository.GetClients(options)
                                               .Where(c => c.OwnerName == _connection.UserName)
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

        // Get the client workspace
        Client client = _repository.GetClient(workspace);

        // Set the client's workspace in the connection
        _connection.Client = client;

        WorkspacePath = @$"C:\Users\{_connection.UserName}\Perforce\{_workspace}\";

        // Sync files
        Sync();
    }

    public bool LogIn(string username, string password)
    {
        Console.WriteLine("Attempting to login to Perforce.");

        try
        {
            // Open a connection to Perforce
            _connection.UserName = username;
            //_connection.Client = new Client { Name = _workspace };

            if (_connection.Connect(null))
            {
                Console.WriteLine("Connected to Perforce.");

                // Log in to the server
                _connection.Login(password);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public void Disconnect()
    {
        _connection.Disconnect();
    }

    public string[] GetUnrealProjectPathFromPerforce()
    {
        Console.WriteLine("Getting Unreal project path from Perforce.");

        // Define the path to the specific file you're interested in
        // Search for .uproject files in the directory
        string[] projectFiles = Directory.GetFiles(WorkspacePath, "*.uproject", SearchOption.AllDirectories);

        // Check if any .uproject file was found
        if (projectFiles.Length > 0)
        {
            // Extract the directory from the file path
            return projectFiles;
        }

        // If no .uproject files are found, return an empty array
        return new string[0];
    }

    private void Sync()
    {
        Console.WriteLine("Syncing Perforce.");

        Options syncOptions = new Options(SyncFilesCmdFlags.None, -1);

        // TODO: Return the error message to the UI
        try
        {
            _connection.Client.SyncFiles(syncOptions, null);
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

            changelist.OwnerName = _connection.UserName;
            changelist.ClientId = _workspace;
            changelist.initialize(_connection);

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
