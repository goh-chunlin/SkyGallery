using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using sky_gallery.Models;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace sky_gallery.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            return View(await GetImageInfosAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Index(List<IFormFile> files, string authorName, string password)
        {
            if (password != _configuration["FileUploadPassword"])
            {
                TempData["message"] = $"Sorry, you entered a wrong password: { password }";
            } 
            else 
            {
                int fileCount = 0;

                foreach(var file in files)
                {
                    try
                    {
                        StreamReader streamReader = new StreamReader(file.OpenReadStream());
                        
                        string azureStorageConnectionString = _configuration["AzureStorage:ConnectionString"];

                        //Create instance of the client.
                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
                        CloudBlobClient blobclient = storageAccount.CreateCloudBlobClient();

                        CloudBlobContainer cloudBlobContainer = blobclient.GetContainerReference("skyscreenshots");
                        var blob = cloudBlobContainer.GetBlockBlobReference(file.FileName + ".jpg");

                        if (!string.IsNullOrWhiteSpace(authorName))
                        {
                            blob.Metadata.Add("Author", authorName);
                        }

                        await blob.UploadFromStreamAsync(streamReader.BaseStream);

                        fileCount++;
                    }
                    catch (Exception) {}
                }

                if (fileCount == 0) 
                {
                    TempData["message"] = $"No images are uploaded.";
                } 
                else if (fileCount == 1) 
                {
                    TempData["message"] = $"{ fileCount } image is uploaded.";
                }
                else if (fileCount >= 2) 
                {
                    TempData["message"] = $"{ fileCount } images are uploaded.";
                }
                
            }

            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        private async Task<List<GalleryImageInfo>> GetImageInfosAsync()
        {
            string azureStorageConnectionString = _configuration["AzureStorage:ConnectionString"];

            //Create instance of the client.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            CloudBlobClient blobclient = storageAccount.CreateCloudBlobClient();

            //Create a blob container
            CloudBlobContainer cloudBlobContainer = blobclient.GetContainerReference("skyscreenshots");
            await cloudBlobContainer.CreateIfNotExistsAsync();

            var imageInfos = await ListBlobsFlatListingAsync(cloudBlobContainer, null);
            imageInfos.Reverse();

            return imageInfos;
        }

        private static async Task<List<GalleryImageInfo>> ListBlobsFlatListingAsync(CloudBlobContainer container, int? segmentSize)
        {
            var blobInfos = new List<GalleryImageInfo>();

            BlobContinuationToken continuationToken = null;
            CloudBlob blob;

            try
            {
                // Call the listing operation and enumerate the result segment.
                // When the continuation token is null, the last segment has been returned
                // and execution can exit the loop.
                do
                {
                    BlobResultSegment resultSegment = await container.ListBlobsSegmentedAsync(string.Empty,
                        true, BlobListingDetails.Metadata, segmentSize, continuationToken, null, null);

                    foreach (var blobItem in resultSegment.Results)
                    {
                        // A flat listing operation returns only blobs, not virtual directories.
                        blob = (CloudBlob)blobItem;

                        // Write out some blob properties.
                        if (blob.Metadata.ContainsKey("Author"))
                        {
                            blobInfos.Add(new GalleryImageInfo{ ImageUrl = blob.Uri.ToString(), AuthorName = blob.Metadata["Author"] });
                        } 
                        else 
                        {
                            blobInfos.Add(new GalleryImageInfo{ ImageUrl = blob.Uri.ToString() });
                        }
                        
                    }

                // Get the continuation token and loop until it is null.
                continuationToken = resultSegment.ContinuationToken;

                } while (continuationToken != null);
            }
            catch (StorageException) { }

            return blobInfos;
        }
    }
}
