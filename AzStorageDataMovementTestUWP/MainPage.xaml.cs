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

        private async Task BasicStorageBlockBlobUploadOperationsAsync()
        {
            Debug.WriteLine("Testing BlockBlob Upload");

            // Create a blob client for interacting with the blob service.
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

            // Create a container for organizing blobs within the storage account.
            Debug.WriteLine("1. Creating Container");
            CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (StorageException)
            {
                Debug.WriteLine("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                throw;
            }

            // To view the uploaded blob in a browser, you have two options. The first option is to use a Shared Access Signature (SAS) token to delegate 
            // access to the resource. See the documentation links at the top for more information on SAS. The second approach is to set permissions 
            // to allow public access to blobs in this container. Uncomment the line below to use this approach. Then you can view the image 
            // using: https://[InsertYourStorageAccountNameHere].blob.core.windows.net/democontainer/HelloWorld.png
            // await container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            // Get a BlockBlob reference for the file to upload to the newly created container
            Debug.WriteLine("2. Uploading BlockBlob");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(TestMediaFile);

#if WINDOWS_UWP
            StorageFolder storageFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
            StorageFile sf = await storageFolder.GetFileAsync(TestMediaFile);
            await blockBlob.UploadFromFileAsync(sf);
#else
        await blockBlob.UploadFromFileAsync(Path.Combine(Application.streamingAssetsPath, TestMediaFile));
#endif

            Debug.WriteLine("-- Upload Test Complete --");
        }

        private async Task BasicStorageBlockBlobDownloadAsync()
        {
            Debug.WriteLine("Testing BlockBlob Download");

            // Create a blob client for interacting with the blob service.
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

            // Create a container for organizing blobs within the storage account.
            Debug.WriteLine("1. Opening Blob Container");
            CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (StorageException)
            {
                Debug.WriteLine("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                throw;
            }

            // Access a specific blob in the container 
            Debug.WriteLine("2. Get Specific Blob in Container");

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
                Debug.WriteLine(string.Format("3. Download Blob from {0}", blockBlob.Uri.AbsoluteUri));
                string fileName = string.Format("CopyOf{0}", TestMediaFile);

#if WINDOWS_UWP
                StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
                StorageFile sf = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                path = sf.Path;
                await blockBlob.DownloadToFileAsync(sf);
#else
            path = Path.Combine(Application.temporaryCachePath, fileName);
        await blockBlob.DownloadToFileAsync(path, FileMode.Create);
#endif
                Debug.WriteLine(string.Format("4. Blob file downloaded to {0}", path));

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

            Debug.WriteLine("-- Download Test Complete --");
        }

        private async Task StorageDataMovementBlockBlobDownloadAsync()
        {
            Debug.WriteLine("Downloading BlockBlob with ASDM Library");

            // Create a blob client for interacting with the blob service.
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

            // Create a container for organizing blobs within the storage account.
            Debug.WriteLine("1. Opening Blob Container");
            CloudBlobContainer container = blobClient.GetContainerReference(BlockBlobContainerName);
            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (StorageException)
            {
                Debug.WriteLine("If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                throw;
            }

            // Get a reference to the blob we want in the container 
            Debug.WriteLine("2. Get Specific Blob in Container");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(TestMediaFile);

            if (blockBlob != null)
            {
                // Setup the number of the concurrent operations
                TransferManager.Configurations.ParallelOperations = 64;
                // Setup the transfer context and track the upload progress
                SingleTransferContext context = new SingleTransferContext();
                context.ProgressHandler = new Progress<TransferStatus>((progress) =>
                {
                    Debug.WriteLine("  Bytes downloaded: " + progress.BytesTransferred.ToString());
                });

                // Download a blob to your file system
                string path;
                Debug.WriteLine(string.Format("3. Download Blob from {0}", blockBlob.Uri.AbsoluteUri));
                string fileName = string.Format("CopyOf{0}", TestMediaFile);

#if WINDOWS_UWP
                // TO DO: UWP code is not complete and has not been tested yet. This is the old single operation code 
                // I have only tested Data Movement in the Unity editor
                StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
                StorageFile sf = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                path = sf.Path;
                Stream sfs = await sf.OpenStreamForWriteAsync();

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
                Debug.WriteLine(string.Format("4. Blob file downloaded with ADSM to {0}", path));
            }
        }
    }
}
