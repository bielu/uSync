using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.UI.WebControls;

using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.IO.MediaPathSchemes;
using Umbraco.Core.Logging;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using Umbraco.Web.WebApi.Filters;

using uSync8.BackOffice.Configuration;
using uSync8.BackOffice.Hubs;
using uSync8.BackOffice.Services;
using uSync8.BackOffice.SyncHandlers;

namespace uSync8.BackOffice.Controllers
{
    [PluginController("uSync")]
    [UmbracoApplicationAuthorize(Constants.Applications.Settings)]
    public class uSyncUploadApiController : UmbracoAuthorizedApiController
    {
        private readonly uSyncService uSyncService;
        private readonly SyncFileService syncFileService;
        private readonly uSyncSettings settings;

        private readonly string tempRoot; 

        public uSyncUploadApiController(IGlobalSettings globalSettings,
            SyncFileService syncFileService,
            uSyncService uSyncService,
            uSyncConfig config)
        {
            tempRoot = Path.GetFullPath(Path.Combine(globalSettings.LocalTempPath, "uSync", "export"));

            this.uSyncService = uSyncService;
            this.settings = config.Settings;
            this.syncFileService = syncFileService;
        }

        [HttpGet]
        public bool GetApi() => true;


        [HttpPost]
        public async Task<SyncArchiveInfo> Upload()
        {
            if (!Request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            var archiveInfo = new SyncArchiveInfo()
            {
                Id = Guid.NewGuid()
            };

            var uploadFolder = GetTempPath(archiveInfo.Id);

            var provider = new CustomMultipartFormDataStreamProvider(uploadFolder);
            var result = await Request.Content.ReadAsMultipartAsync(provider);
            var filename = result.FileData.First().LocalFileName;

            if (filename == null) throw new FileNotFoundException();

            // do the work...
            var folder = UnzipFile(filename);

            archiveInfo.Folders = GetFolders(folder).Select(x => new SyncArchiveFolder()
            {
                Name = x,
                Selected = true
            }).ToList();

            return archiveInfo;
        }

        [HttpPost]
        public bool ApplyChanges(SyncArchiveInfo archiveInfo)
        {
            var archiveFolder = GetTempPath(archiveInfo.Id);
            var siteRoot = IOHelper.MapPath("~");

            if (!Directory.Exists(archiveFolder))
                throw new DirectoryNotFoundException("Cannot find archive on disk");

            foreach(var folder in archiveInfo.Folders.Where(x => x.Selected))
            {
                var path = Path.Combine(archiveFolder, folder.Name);
                var target = Path.Combine(siteRoot, folder.Name);

                EnsureSitePath(target, siteRoot);

                CopyFolder(path, target);
            }

            return true;
        }

        [HttpPost]
        public bool Clean(Guid id)
        {
            var folder = GetTempPath(id);
            if (Directory.Exists(folder))
            {
                SafeDeleteFolder(folder);
            }
            return true;
        }

        public void EnsureSitePath(string path, string root)
        {
            var resolved = Path.GetFullPath(path);
            if (!resolved.InvariantStartsWith(root))
            {
                throw new InvalidOperationException("Path outside of the Site");
            }
        }


        [HttpPost]
        public HttpResponseMessage Download(uSyncOptions options)
        {
            var tmpId = Guid.NewGuid().ToString();

            var tmpFolder = Path.Combine(tempRoot, tmpId);
            var uSyncTmp = Path.Combine(tmpFolder, "uSync", "v8");
            Directory.CreateDirectory(tmpFolder);

            var hubClient = new HubClientService(options.ClientId);

            var exportInfo = uSyncService.Export(uSyncTmp, new SyncHandlerOptions()
            {
                Group = options.Group
            }, hubClient.Callbacks());

            // if success 
            if (!exportInfo.Any(x => x.Change >= Core.ChangeType.Fail))
            {
                CopyExtraFolders(tmpFolder, hubClient.Callbacks());

                var fileName = $"uSyncExport_{options.Group}_{DateTime.Now:yyyy_MM_dd_HHmmss}.zip";

                hubClient.Callbacks()?.Update("Compressing Export", 1, 2);

                // return a zip of the folder
                var stream = ZipFolder(tmpFolder);
                if (stream != null)
                {
                    var response = new HttpResponseMessage
                    {
                        Content = new ByteArrayContent(stream.ToArray())
                        {
                            Headers =
                            {
                                ContentDisposition = new ContentDispositionHeaderValue("attachment")
                                {
                                    FileName = fileName
                                },
                                ContentType = new MediaTypeHeaderValue("application/x-compressed")
                            }
                        }
                    };

                    response.Headers.Add("x-filename", fileName);

                    hubClient.Callbacks()?.Update("Done", 1, 2);
                    return response;
                }
            }

            hubClient.Callbacks()?.Update("Failed to compress :( ", 1, 2);

            // return errors :( 
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }


        private string GetTempPath(Guid id, bool createIfMissing = true)
        {
            var path = Path.Combine(tempRoot, id.ToString());

            if (createIfMissing)
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        private void CopyExtraFolders(string rootFolder, uSyncCallbacks callbacks)
        {
            var root = IOHelper.MapPath("~");

            foreach (var info in settings.AdditionalFolders.Select((Folder, Index) => new { Folder, Index }))
            {
                var folderPath = IOHelper.MapPath(info.Folder);
                var target = GetRelativePath(root, folderPath); // e.g gets "/views"

                callbacks?.Update($"Copying {target} folder", info.Index, settings.AdditionalFolders.Length);

                CopyFolder(folderPath, Path.Combine(rootFolder, target));
            }
        }

        private MemoryStream ZipFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return null;

            var folder = new DirectoryInfo(folderPath);
            var files = folder.GetFiles("*.*", SearchOption.AllDirectories).ToList();

            Logger.Debug<uSyncUploadApiController>("ZipFolder: {folder} [{fileCount} files]", folderPath, files.Count);

            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var file in files)
                {
                    var relativeFilePath = GetRelativePath(folderPath, file.FullName);
                    // var relativeFilePath = file.FullName.Substring(fullPath.Length).TrimStart('\\');
                    Logger.Debug<uSyncUploadApiController>("Adding File : {relativeFilePath}", relativeFilePath);
                    archive.CreateEntryFromFile(file.FullName, relativeFilePath);
                }
            }

            // delete all the files in the folder now ?
            SafeDeleteFolder(folderPath);

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private string UnzipFile(string zipfile)
        {
            var folder = Path.GetDirectoryName(zipfile);

            using(var zip = ZipFile.OpenRead(zipfile))
            {
                foreach(var entry in zip.Entries)
                {
                    var dest = Path.Combine(folder, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, true);
                }
            }

            File.Delete(zipfile);

            return folder;
        }

        private string GetRelativePath(string root, string file)
        {
            var trimmedRoot = CleanPath(root, true);
            var filePath = CleanPath(file, false);

            if (filePath.Length > trimmedRoot.Length)
            {
                if (!filePath.StartsWith(trimmedRoot, StringComparison.InvariantCultureIgnoreCase))
                {
                    // not a file that belongs.
                    throw new ArgumentException($"File [{file}] is not in rootpath [{trimmedRoot}]", nameof(file));
                }

                return filePath.Substring(trimmedRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            throw new ArgumentException($"mismatch [{trimmedRoot}] [{file}]", nameof(file));
        }

        private string CleanPath(string path, bool trimEnd)
        {
            var cleanPath = Path.GetFullPath(
                    path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                        .Replace(new string(Path.DirectorySeparatorChar, 2), Path.DirectorySeparatorChar.ToString()));

            if (trimEnd)
                return cleanPath.TrimEnd(Path.DirectorySeparatorChar);

            return cleanPath;
        }

        private void SafeDeleteFolder(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch
            {
                // this delete can fail if the folder is locked. 
                // this isn't ideal, but not critical.
            }
        }

        private void CopyFolder(string source, string target)
        {
            foreach (var file in syncFileService.GetFiles(source, "*.*"))
            {
                if (syncFileService.FileExists(file))
                {
                    var targetFile = Path.Combine(target, Path.GetFileName(file));
                    syncFileService.CopyFile(file, targetFile);
                }
            }

            foreach (var folder in syncFileService.GetDirectories(source))
            {
                var targetPath =
                    Path.GetFullPath(
                        Path.Combine(target, GetRelativePath(source, folder)));

                CopyFolder(folder, targetPath);
            }
        }

        private IEnumerable<string> GetFolders(string rootFolder)
        {
            var containsuSync = false;

            var folders = new List<string>();
            
            foreach(var folder in Directory.GetDirectories(rootFolder))
            {

                var name = Path.GetFileName(folder);
                if (name.InvariantEquals("uSync"))
                {
                    var v8 = Path.Combine(folder, "v8");

                    if (Directory.Exists(v8))
                    {
                        containsuSync = true;
                        foreach (var subFolder in Directory.GetDirectories(v8))
                        {
                            folders.Add(subFolder.Substring(rootFolder.Length + 1));
                        }
                    }
                }
                else
                {
                    folders.Add(folder.Substring(rootFolder.Length + 1));
                }
            }

            if (!containsuSync)
            {
                throw new InvalidOperationException("Zip Archive doesn't contain a uSync/v8 folder, can't import it.");
            }

            return folders;
        }

    }

    public class SyncArchiveInfo
    {
        public Guid Id { get; set; }
        public List<SyncArchiveFolder> Folders { get; set; }
        
    }

    public class SyncArchiveFolder
    {
        public string Name { get; set; }
        public bool Selected { get; set; }
    }

    public class CustomMultipartFormDataStreamProvider : MultipartFormDataStreamProvider
    {
        public CustomMultipartFormDataStreamProvider(string path) : base(path) { }

        public override string GetLocalFileName(HttpContentHeaders headers)
        {
            return headers.ContentDisposition.FileName.Replace("\"", string.Empty);
        }
    }
}
