using Perforce.P4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UnrealExporter.App.Configs;
using UnrealExporter.App.Exceptions;
using UnrealExporter.App.Interfaces;

namespace UnrealExporter.App.Services;

public class PerforceService : IPerforceService
{
    private const string SERVER_URI = "ssl:perforce.tga.learnet.se:1666";

    private string? _workspace;
    private Server? _server;
    private Repository? _repository;

    private readonly IAppConfig _appConfig;

    public string? WorkspacePath { get; set; }

    public ConnectionStatus? ConnectionStatus { get { return _repository?.Connection.Status; } }

    public PerforceService(IAppConfig appConfig)
    {
        try
        {
            _server = new Server(new ServerAddress(SERVER_URI));
            _repository = new Repository(_server);
            _appConfig = appConfig;
        }
        catch (P4Exception ex)
        {
            throw new ServiceException("A Perforce related error ocurred: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<string>?> GetWorkspaces()
    {
        try
        {
            Options options = new();
            return await Task.Run(() => _repository?.GetClients(options)
                             ?.Where(c => c.OwnerName == _repository.Connection.UserName)
                             ?.Select(c => c.Name)
                             ?.ToList());
        }
        catch (P4Exception ex)
        {
            throw new ServiceException(ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public void Connect(string workspace)
    {
        try
        {
            _workspace = workspace;

            if(_repository == null)
            {
                throw new ServiceException("No Perforce repository found.");
            }

            Client? client = _repository!.GetClient(workspace);

            if(client == null)
            {
                throw new ServiceException($"No client found for workspace {workspace}.");
            }

            _repository!.Connection.Client = client;
            WorkspacePath = @$"C:\Users\{_repository.Connection.UserName}\Perforce\{_workspace}\";

            Sync();
        }
        catch (P4Exception ex)
        {
            throw new ServiceException(ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<bool> LogIn(string username, string password)
    {
        try
        {
            if (_repository == null)
            {
                throw new ServiceException("No Perforce repository found.");
            }

            _repository.Connection.UserName = username;

            if (_repository.Connection.Connect(null))
            {
                await Task.Run(() => _repository.Connection.Login(password));

                return true;
            }
            else
            {
                throw new ServiceException("Failed to connect to Perforce server.");
            }
        }
        catch(P4Exception ex)
        {
            throw;
        }
        catch (ServiceException ex)
        {
            throw new ServiceException(ex.Message);
        }
        catch(Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_repository == null)
            {
                throw new ServiceException("No Perforce repository found.");
            }
            else
            {
                _repository.Connection.Disconnect();
            }
        }
        catch(ServiceException ex)
        {
            throw new ServiceException(ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public string[] GetUnrealProjectPathFromPerforce()
    {
        try
        {
            if(WorkspacePath == null)
            {
                throw new ServiceException("No valid workspace path found.");
            }

            string[]? projectFiles = Directory.GetFiles(WorkspacePath, "*.uproject", SearchOption.AllDirectories);

            if (projectFiles.Length > 0)
            {
                return projectFiles;
            }

            return new string[0];
        }
        catch(ServiceException ex)
        {
            throw new ServiceException(ex.Message);
        }
        catch(Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    private void Sync()
    {
        try
        {
            if (_repository == null)
            {
                throw new ServiceException("No Perforce repository found.");
            }

            Options syncOptions = new Options(SyncFilesCmdFlags.None, -1);

            _repository.Connection.Client.SyncFiles(syncOptions, null);
        }
        catch(ServiceException ex)
        {
            throw new ServiceException("A Perforce related error ocurred: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public void AddFilesToPerforce(List<string> exportedFiles)
    {
        try
        {
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
            throw new ServiceException("A Perforce related error ocurred: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);

        }
    }

    private void AddOrEditFiles(FileSpec[] fileSpecs)
    {
        try
        {
            if(_repository == null)
            {
                throw new ServiceException("No Perforce repository found.");
            }

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
            throw new ServiceException("A Perforce related error ocurred: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public void SubmitChanges()
    {
        try
        {
            if (_repository == null)
            {
                throw new ServiceException("No Perforce repository found.");
            }

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
        }
        catch (P4Exception ex)
        {
            throw new ServiceException("A Perforce related error ocurred: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }
}
