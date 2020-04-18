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
            string azureStorageConnectionString = _configuration["AzureStorage:ConnectionString"];

            //Create instance of the client.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            CloudBlobClient blobclient = storageAccount.CreateCloudBlobClient();

            //Create a blob container
            CloudBlobContainer cloudBlobContainer = blobclient.GetContainerReference("skyscreenshots");
            await cloudBlobContainer.CreateIfNotExistsAsync();

            return View(await ListBlobsFlatListingAsync(cloudBlobContainer, null));
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

        private static async Task<List<string>> ListBlobsFlatListingAsync(CloudBlobContainer container, int? segmentSize)
        {
            var blobUrls = new List<string>();

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
                        blobUrls.Add(blob.Uri.ToString());
                    }

                // Get the continuation token and loop until it is null.
                continuationToken = resultSegment.ContinuationToken;

                } while (continuationToken != null);
            }
            catch (StorageException) { }

            return blobUrls;
        }
    }
}
