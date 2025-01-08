using Perforce.P4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UnrealExporter.App.Configs;
using UnrealExporter.App.Interfaces;

namespace UnrealExporter.App.Services;

public class PerforceService : IPerforceService
{
    private const string SERVER_URI = "ssl:perforce.tga.learnet.se:1666";

    private string _workspace;
    private Server _server;
    private Repository _repository;

    private readonly IAppConfig _appConfig;

    public string WorkspacePath { get; set; }

    public ConnectionStatus ConnectionStatus { get { return _repository.Connection.Status; } }

    public PerforceService(IAppConfig appConfig)
    {
        _server = new Server(new ServerAddress(SERVER_URI));
        _repository = new Repository(_server);
        _appConfig = appConfig;
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
        WorkspacePath = @$"C:\Users\{_repository.Connection.UserName}\Perforce\{_workspace}\";

        Sync();
    }

    public bool LogIn(string username, string password)
    {
        try
        {
            Console.WriteLine("Attempting to login to Perforce.");

            _repository.Connection.UserName = username;

            if (_repository.Connection.Connect(null))
            {
                Console.WriteLine("Connected to Perforce.");

                _repository.Connection.Login(password);

                return true;
            }

            return false;
        }
       catch
       {
            return false;
       }

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
        catch (Exception e)
        {

        }
    }

    public void AddFilesToPerforce(List<string> exportedFiles)
    {
        try
        {
            //string exportPathRoot = appConfig.DestinationDirectory; 

            //string meshesExportPath = appConfig.ExportMeshes && appConfig.ExportTextures
            //    ? Path.Combine(exportPathRoot, "Meshes")
            //    : exportPathRoot;

            //string texturesExportPath = appConfig.ExportMeshes && appConfig.ExportTextures
            //    ? Path.Combine(exportPathRoot, "Textures")
            //    : exportPathRoot;

            //string[] meshFilePaths = Directory.GetFiles(meshesExportPath);
            //string[] textureFilePaths = Directory.GetFiles(texturesExportPath);

            //List<string> allFilePaths = meshFilePaths.Concat(textureFilePaths).ToList();

            //for (int i = allFilePaths.Count - 1; i >= 0; i--)
            //{
            //    var filePath = allFilePaths[i];
            //    if (!exportedFiles.Contains(filePath))
            //    {
            //        allFilePaths.RemoveAt(i);
            //    }
            //}

            //string[] filePaths = allFilePaths.ToArray();

            //if (filePaths.Length == 0)
            //{
            //    Console.WriteLine("No files found in the specified directory.");
            //    return;
            //}

            List<FileSpec> fileSpecs = new List<FileSpec>();
            foreach (string exportedFile in exportedFiles)
            {
                fileSpecs.Add(new FileSpec(new LocalPath(exportedFile)));
            }

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

            foreach (var fileSpec in fileSpecs)
            {
                Options options = new();
                IList<FileMetaData> fileMetaDataList = _repository.GetFileMetaData(options, new FileSpec[] { fileSpec });

                if (fileMetaDataList == null || fileMetaDataList.Count == 0 || fileMetaDataList[0].HeadAction == FileAction.Delete)
                {
                    filesToAdd.Add(fileSpec);
                }
                else
                {
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
            changelist.Description = _appConfig.SubmitMessage;

            foreach (var file in openedFiles)
            {
                changelist.Files.Add(file);
            }

            changelist.OwnerName = _repository.Connection.UserName;
            changelist.ClientId = _workspace;
            changelist.initialize(_repository.Connection);

            changelist = _repository.CreateChangelist(changelist);

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
