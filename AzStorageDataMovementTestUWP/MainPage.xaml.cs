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
using Microsoft.WindowsAzure.Storage.DataMovement;
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
            await BasicStorageBlockBlobDownloadAsync();
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
        private async Task BasicStorageBlockBlobDownloadAsync()
        {
            try
            {
                AddResult("Testing BlockBlob Download");

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
                AddResult("2. Get Specific Blob in Container");

                // NOTE: The following code isn't needed for now because we assume the client app knows which asset to
                // download by name, so there is no need to iterate through all the blobs.
                //CloudBlockBlob blockBlob = null;
                //BlobContinuationToken token = null;
                //BlobResultSegment list = await container.ListBlobsSegmentedAsync(token);
                //foreach (IListBlobItem blob in list.Results)
                //{
                //    // Blob type will be CloudBlockBlob, CloudPageBlob or CloudBlobDirectory
                //    // Use blob.GetType() and cast to appropriate type to gain access to properties specific to each type
                //    WriteLine(string.Format("- {0} (type: {1})", blob.Uri, blob.GetType()));

                //    // This next line doesn't work, need to check for the name on the specific blob type
                //    if (blob == TestMediaFile)
                //    {
                //        blockBlob = (CloudBlockBlob)blob;
                //    }
                //}

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(TestMediaFile);

                if (blockBlob != null)
                {
                    // Download a blob to your file system
                    string path;
                    AddResult(string.Format("3. Download Blob from {0}...", blockBlob.Uri.AbsoluteUri));
                    string fileName = string.Format("CopyOf{0}", TestMediaFile);

                    var sw = Stopwatch.StartNew();
#if WINDOWS_UWP
                    StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
                    StorageFile sf = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    path = sf.Path;
                    await blockBlob.DownloadToFileAsync(sf);
#else
            path = Path.Combine(Application.temporaryCachePath, fileName);
        await blockBlob.DownloadToFileAsync(path, FileMode.Create);
#endif
                    sw.Stop();
                    TimeSpan time = sw.Elapsed;

                    AddResult(string.Format("4. Blob file downloaded to {0} in {1}s", path, time.TotalSeconds.ToString()));

                    //WriteLine("File written to " + path);

                    //// Clean up after the demo 
                    //WriteLine("5. Delete block Blob");
                    //await blockBlob.DeleteAsync();

                    //// When you delete a container it could take several seconds before you can recreate a container with the same
                    //// name - hence to enable you to run the demo in quick succession the container is not deleted. If you want 
                    //// to delete the container uncomment the line of code below. 
                    //WriteLine("6. Delete Container -- Note that it will take a few seconds before you can recreate a container with the same name");
                    //await container.DeleteAsync();
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
            }
            catch (Exception ex)
            {
                // Woops!
                AddResult("Error: " + ex.ToString());
                AddResult("Error: " + ex.InnerException.ToString());
            }
        }
        #endregion
    }
}
