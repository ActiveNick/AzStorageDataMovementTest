using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
#if FALLCREATORSUPDATE
using Microsoft.WindowsAzure.Storage.DataMovement;
#endif
using System.Diagnostics;
using Windows.Storage;

namespace AzStorageDataMovementTestUWP
{
    /// <summary>
    /// UWP Test Client app for Azure Blob Storage interactions using the Azure Storage Data Movement Library
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Set these in the inspector
        CloudStorageAccount StorageAccount;
        string ConnectionString = "";
        public string BlockBlobContainerName = "mediacontainerblockblob";  // The blob container where we read from and write to
        public string TestMediaFile = "mars_8k.jpg"; // The media file to upload or download

        public MainPage()
        {
            this.InitializeComponent();

            ConnectionString = txtConnString.Text;
            StorageAccount = CloudStorageAccount.Parse(ConnectionString);
        }

#region === UI BUTTON EVENT HANDLERS ===
        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            await BasicStorageBlockBlobUploadOperationsAsync();
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            await BasicStorageBlockBlobDownloadAsync(TestMediaFile, (bool)chkOverwrite.IsChecked);
        }

        private async void btnDownloadDM_Click(object sender, RoutedEventArgs e)
        {
            await StorageDataMovementBlockBlobDownloadAsync();
        }

        private async void btnDownloadProgress_Click(object sender, RoutedEventArgs e)
        {
            await StorageBlockBlobDownloadWithProgressTrackingAsync();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            lblResults.Text = "";
        }
#endregion

        void AddResult(string textline)
        {
            lblResults.Text += (textline + Environment.NewLine);
            Debug.WriteLine(textline);
        }

#region === REQUIRES ONLY AZURE STORAGE LIBRARY ===
        /// <summary>
        /// Uploading a blob using standard Azure Storage library (no progress tracking)
        /// </summary>
        /// <returns></returns>
        private async Task BasicStorageBlockBlobUploadOperationsAsync()
        {
            try
            {
                AddResult("Testing BlockBlob Upload");

                // Create a blob client for interacting with the blob service.
                CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

                // Create a container for organizing blobs within the storage account.
                AddResult("1. Creating Container");
                CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
                try
                {
                    await container.CreateIfNotExistsAsync();
                }
                catch (StorageException)
                {
                    AddResult("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                    throw;
                }

                // To view the uploaded blob in a browser, you have two options. The first option is to use a Shared Access Signature (SAS) token to delegate 
                // access to the resource. See the documentation links at the top for more information on SAS. The second approach is to set permissions 
                // to allow public access to blobs in this container. Uncomment the line below to use this approach. Then you can view the image 
                // using: https://[InsertYourStorageAccountNameHere].blob.core.windows.net/democontainer/HelloWorld.png
                // await container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                // Get a BlockBlob reference for the file to upload to the newly created container
                AddResult("2. Uploading BlockBlob...");
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(TestMediaFile);

                var sw = Stopwatch.StartNew();
#if WINDOWS_UWP
                StorageFolder storageFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
                StorageFile sf = await storageFolder.GetFileAsync(TestMediaFile);
                await blockBlob.UploadFromFileAsync(sf);
#else
                await blockBlob.UploadFromFileAsync(Path.Combine(Application.streamingAssetsPath, TestMediaFile));
#endif
                sw.Stop();
                TimeSpan time = sw.Elapsed;

                AddResult(string.Format("3. File uploaded in {0}s", time.TotalSeconds.ToString()));

                AddResult("-- Upload Test Complete --");
            }
            catch (Exception ex)
            {
                // Woops!
                AddResult("Error: " + ex.ToString());
                AddResult("Error: " + ex.InnerException.ToString());
            }
        }

        /// <summary>
        /// Downloading a blob using standard Azure Storage library (no progress tracking)
        /// </summary>
        /// <returns></returns>
        public async Task BasicStorageBlockBlobDownloadAsync(string MediaFile, bool overwrite)
        {
            try
            {
                // Create a blob client for interacting with the blob service.
                CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

                // Create a container for organizing blobs within the storage account.
                AddResult("Opening Blob Container in Azure Storage.");
                CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
                try
                {
                    await container.CreateIfNotExistsAsync();
                }
                catch (StorageException)
                {
                    AddResult("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                    throw;
                }

                // Access a specific blob in the container 
                AddResult("Getting Specific Blob in Container.");

                // We assume the client app knows which asset to download by name
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(MediaFile);

                if (blockBlob != null)
                {
                    // Download a blob to your file system
                    string path = "";
                    AddResult(string.Format("Downloading Blob from {0}, please wait...", blockBlob.Uri.AbsoluteUri));
                    string fileName = MediaFile; // string.Format("CopyOf{0}", MediaFile);

                    bool fileExists = false;
#if WINDOWS_UWP
                    StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
                    StorageFile sf;
                    try
                    {
                        CreationCollisionOption collisionoption = (overwrite ? CreationCollisionOption.ReplaceExisting : CreationCollisionOption.FailIfExists);
                        sf = await storageFolder.CreateFileAsync(fileName, collisionoption);
                        fileExists = false; // if the file existed but we were allowed to overwrite it, let's treat it as if it didn't exist
                        path = sf.Path;
                    }
                    catch (Exception)
                    {
                        // The file already exists and we're not supposed to overwrite it
                        fileExists = true;
                        sf = await storageFolder.GetFileAsync(fileName); // Necessary to avoid a compilation error below
                    }
#else
                    path = Path.Combine(Application.temporaryCachePath, fileName);
                    fileExists = File.Exists(path);
#endif
                    if (fileExists)
                    {
                        if (overwrite)
                        {
                            AddResult(string.Format("Already exists. Deleting file {0}", fileName));
#if WINDOWS_UWP
                            // Nothing to do here in UWP, we already Replaced it when we created the StorageFile
#else
                            File.Delete(path);
#endif
                        }
                        else
                        {
                            AddResult(string.Format("File {0} already exists and overwriting is disabled. Download operation cancelled.", fileName));
                            return;
                        }
                    }
                    // Start the timer to measure performance
                    var sw = Stopwatch.StartNew();
#if WINDOWS_UWP
                    await blockBlob.DownloadToFileAsync(sf);
#else
                    await blockBlob.DownloadToFileAsync(path, FileMode.Create);
#endif
                    // Stop the timer and report back on completion + performance
                    sw.Stop();
                    TimeSpan time = sw.Elapsed;
                    AddResult(string.Format("Blob file downloaded to {0} in {1}s.", path, time.TotalSeconds.ToString()));
                }
                else
                {
                    AddResult(string.Format("File {0} not found in blob {1}.", MediaFile, blockBlob.Uri.AbsoluteUri));
                }
            }
            catch (Exception ex)
            {
                // Woops!
                AddResult(string.Format("Error while downloading file {0}.", MediaFile));
                AddResult("Error: " + ex.ToString());
                AddResult("Error: " + ex.InnerException.ToString());
            }
        }
        #endregion

        #region === REQUIRES ONLY AZURE STORAGE LIBRARY (WITH PROGRESS TRACKING) ===
        /// <summary>
        /// Download a blob using standard Azure Storage library (with progress tracking)
        /// </summary>
        /// <returns></returns>
        private async Task StorageBlockBlobDownloadWithProgressTrackingAsync()
        {
            try
            {
                AddResult("Testing BlockBlob Download with Progress Tracking");

                // Create a blob client for interacting with the blob service.
                CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

                // Create a container for organizing blobs within the storage account.
                AddResult("1. Opening Blob Container");
                CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
                try
                {
                    await container.CreateIfNotExistsAsync();
                }
                catch (StorageException)
                {
                    AddResult("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                    throw;
                }

                // Access a specific blob in the container 
                AddResult("2. Get Specific Blob in Container and its size");

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(TestMediaFile);
                int segmentSize = 1 * 1024 * 1024;//1 MB chunk

                if (blockBlob != null)
                {
                    // Obtain the size of the blob
                    await blockBlob.FetchAttributesAsync();
                    long blobSize = blockBlob.Properties.Length;
                    long blobLengthRemaining = blobSize;
                    long startPosition = 0;
                    AddResult("3. Blob size (bytes):" + blobLengthRemaining.ToString());

                    // Download a blob to your file system
                    string path;
                    AddResult(string.Format("4. Download Blob from {0}...", blockBlob.Uri.AbsoluteUri));
                    string fileName = string.Format("CopyOf{0}", TestMediaFile);

                    var sw = Stopwatch.StartNew();
#if WINDOWS_UWP
                    StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
                    StorageFile sf = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    path = sf.Path;
                    var fs = await sf.OpenAsync(FileAccessMode.ReadWrite);

                    do
                    {
                        long blockSize = Math.Min(segmentSize, blobLengthRemaining);
                        byte[] blobContents = new byte[blockSize];
                        using (MemoryStream ms = new MemoryStream())
                        {
                            await blockBlob.DownloadRangeToStreamAsync(ms, (long)startPosition, blockSize);
                            ms.Position = 0;
                            ms.Read(blobContents, 0, blobContents.Length);
                            fs.Seek((ulong)startPosition);
                            await fs.WriteAsync(blobContents.AsBuffer());
                            //await fs.WriteAsync
                        }
                        AddResult("Completed: " + ((float)startPosition / (float)blobSize).ToString("P"));
                        startPosition += blockSize;
                        blobLengthRemaining -= blockSize;
                    }
                    while (blobLengthRemaining > 0);
                    AddResult("Completed: 100.00%");
                    fs = null;
#else
                path = Path.Combine(Application.temporaryCachePath, fileName);
                //await blockBlob.DownloadToFileAsync(path, FileMode.Create);
#endif
                    sw.Stop();
                    TimeSpan time = sw.Elapsed;

                    AddResult(string.Format("5. Blob file downloaded to {0} in {1}s", path, time.TotalSeconds.ToString()));
                }

                AddResult("-- Download Test Complete --");
            }
            catch (Exception ex)
            {
                // Woops!
                AddResult("Error: " + ex.ToString());
                AddResult("Error: " + ex.InnerException.ToString());
            }
        }
#endregion

#region === REQUIRES AZURE STORAGE DATA MOVEMENT LIBRARY ===
        /// <summary>
        /// Downloading a blob using Azure Storage Data Movement library (with progress tracking)
        /// </summary>
        /// <returns></returns>
        private async Task StorageDataMovementBlockBlobDownloadAsync()
        {
            try
            {
#if FALLCREATORSUPDATE
                // Dirty check to see if we are running Fall Creators Update (FCU, build 16299)
                // otherwise we cannot make calls to the Azure Storage Data Movement library
                // Change the FALLCREATORSUPDATE precompiler directive in build settings

                AddResult("Downloading BlockBlob with ASDM Library");

                // Create a blob client for interacting with the blob service.
                CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

                // Create a container for organizing blobs within the storage account.
                AddResult("1. Opening Blob Container");
                CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
                try
                {
                    await container.CreateIfNotExistsAsync();
                }
                catch (StorageException)
                {
                    AddResult("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                    throw;
                }

                // Get a reference to the blob we want in the container 
                AddResult("2. Get Specific Blob in Container");

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(TestMediaFile);

                if (blockBlob != null)
                {
                    // Setup the number of the concurrent operations
                    TransferManager.Configurations.ParallelOperations = 64;
                    // Setup the transfer context and track the upload progress
                    SingleTransferContext context = new SingleTransferContext();
                    context.ProgressHandler = new Progress<TransferStatus>((progress) =>
                    {
                        AddResult("  Bytes downloaded: " + progress.BytesTransferred.ToString());
                    });

                    // Download a blob to your file system
                    string path;
                    AddResult(string.Format("3. Download Blob from {0}", blockBlob.Uri.AbsoluteUri));
                    string fileName = string.Format("CopyOf{0}", TestMediaFile);

                    var sw = Stopwatch.StartNew();
#if WINDOWS_UWP
                    // TO DO: UWP code is not complete and has not been tested yet. This is the old single operation code 
                    // I have only tested Data Movement in the Unity editor
                    StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
                    StorageFile sf = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    path = sf.Path;
                    Stream sfs = await sf.OpenStreamForWriteAsync();
                    //Stream sfs = await sf.OpenAsync(FileAccessMode.ReadWrite) as Stream;

                    // Download a local blob with progress updates
                    DownloadOptions dOptions = new DownloadOptions();
                    dOptions.DisableContentMD5Validation = true;  // TO DO: Need to test if MD5 works, currently disabled
                    await TransferManager.DownloadAsync(blockBlob, sfs, dOptions, context, CancellationToken.None);
                    //await blockBlob.DownloadToFileAsync(sf);
#else
            path = Path.Combine(Application.temporaryCachePath, fileName);

            // Download a local blob with progress updates
            DownloadOptions dOptions = new DownloadOptions();
            dOptions.DisableContentMD5Validation = true;  // TO DO: Need to test if MD5 works, currently disabled
            await TransferManager.DownloadAsync(blockBlob, path, dOptions, context, CancellationToken.None);
#endif
                    sw.Stop();
                    TimeSpan time = sw.Elapsed;

                    AddResult(string.Format("4. Blob file downloaded to {0} in {1}s", path, time.TotalSeconds.ToString()));

                    AddResult("-- Download Test Complete --");
                }
#else
                AddResult("Cannot use Azure Storage DMLib. You are currently not running Fall Creators Update. Change minimum target to 16299, add FALLCREATORSUPDATE in build conditional compilation symbols, and re-add DMLib DLL reference.");
#endif
            }
            catch (Exception ex)
            {
                // Woops!
                AddResult("Error: " + ex.ToString());
                AddResult("Error: " + ex.InnerException.ToString());
            }
        }

#endregion

        long GetWindowsBuildNumber()
        {
            string deviceFamilyVersion = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong major = (version & 0xFFFF000000000000L) >> 48;
            ulong minor = (version & 0x0000FFFF00000000L) >> 32;
            ulong build = (version & 0x00000000FFFF0000L) >> 16;
            ulong revision = (version & 0x000000000000FFFFL);
            var osVersion = $"{major}.{minor}.{build}.{revision}";
            Debug.WriteLine(osVersion);

            return (long)build;
        }
    }
}
