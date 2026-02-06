using Steamworks;
using Steamworks.Data;
using Barotrauma.Steam;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System;
using Steamworks.Ugc;

namespace Barotrauma
{
    partial class LuaCsSteam
    {
        private struct WorkshopItemDownload
        {
            public Steamworks.Ugc.Item Item;
            public string Destination;
            public LuaCsAction Callback;
        }

        double lastTimeChecked = 0;
        List<WorkshopItemDownload> itemsBeingDownloaded = new List<WorkshopItemDownload>();

        public LuaCsSteam()
        {
            
        }

        private static void CopyFolder(string sourceDirName, string destDirName, bool copySubDirs, bool overwriteExisting = false)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new System.IO.DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            IEnumerable<DirectoryInfo> dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            IEnumerable<FileInfo> files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                if (!overwriteExisting && File.Exists(tempPath)) { continue; }
                file.CopyTo(tempPath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    CopyFolder(subdir.FullName, tempPath, copySubDirs, overwriteExisting);
                }
            }
        }

        private async void DownloadWorkshopItemAsync(WorkshopItemDownload download, bool startDownload = false)
        {
            if (startDownload)
            {
                SteamManager.Workshop.NukeDownload(download.Item);
                SteamUGC.Download(download.Item.Id, true);
                itemsBeingDownloaded.Add(download);
            }

            if (download.Item.IsInstalled && Directory.Exists(download.Item.Directory))
            {
                if (download.Callback != null)
                {
                    download.Callback(download.Item);
                }
                
                itemsBeingDownloaded.Remove(download);
                CopyFolder(download.Item.Directory, download.Destination, true, true);
                return;
            }
        }


        public async void DownloadWorkshopItem(ulong id, string destination, LuaCsAction callback)
        {
            if (!LuaCsFile.IsPathAllowedException(destination)) { return; }

            Option<Steamworks.Ugc.Item> itemOption = await SteamManager.Workshop.GetItem(id);

            if (itemOption.TryUnwrap(out Steamworks.Ugc.Item item))
            {
                DownloadWorkshopItemAsync(new WorkshopItemDownload()
                {
                    Item = item,
                    Destination = destination,
                    Callback = callback
                }, true);
            }
            else
            {
                throw new Exception($"Tried to download invalid workshop item {id}.");
            }
        }

        public void DownloadWorkshopItem(Steamworks.Ugc.Item item, string destination, LuaCsAction callback)
        {
            DownloadWorkshopItemAsync(new WorkshopItemDownload()
            {
                Item = item,
                Destination = destination,
                Callback = callback
            }, true);
        }

        public async void GetWorkshopItem(UInt64 id, LuaCsAction callback)
        {
            Option<Steamworks.Ugc.Item> itemOption = await SteamManager.Workshop.GetItem(id);

            if (itemOption.TryUnwrap(out Steamworks.Ugc.Item item)) 
            {
                callback(item);
            }
            else
            {
                callback(null);
            }
        }

        public void Update()
        {
            if (itemsBeingDownloaded.Count > 0 && Timing.TotalTime > lastTimeChecked) // SteamUGC.OnDownloadItemResult for some reason doesn't work, so i need to do this stupid thing.
            {
                foreach (var item in itemsBeingDownloaded.ToArray())
                {
                    DownloadWorkshopItemAsync(item);
                }

                lastTimeChecked = Timing.TotalTime + 15;
            }
        }
    }
}
