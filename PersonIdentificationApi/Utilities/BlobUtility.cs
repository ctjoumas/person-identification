namespace PersonIdentificationApi.Utilities
{
    using Azure.Storage.Blobs;
    using Azure.Storage.Sas;

    public class BlobUtility
    {
        public string? ConnectionString { get; set; }
        public string? ContainerName { get; set; }

        /// <summary>
        /// Gets the SAS URI of the file specified. If the file does not exist, a null URI is returned.
        /// </summary>
        /// <param name="fileName">Name of the image in the storage account container</param>
        /// <returns></returns>
        public Uri GetBlobUri(string fileName)
        {
            Uri? uri = null;
            BlobClient blobClient = new BlobClient(ConnectionString, ContainerName, fileName);

            if (blobClient.Exists())
            {
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = ContainerName,
                    BlobName = fileName,
                    Resource = "b",
                };

                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddDays(1);
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                uri = blobClient.GenerateSasUri(sasBuilder);
            }

            return uri;
        }

        public async Task<byte[]> DownloadBlobStreamAsync(string sasUrl)
        {
            var blobClient = new BlobClient(new Uri(sasUrl));

            byte[] blobContent;
            using (var memoryStream = new MemoryStream())
            {
                await blobClient.DownloadToAsync(memoryStream);
                blobContent = memoryStream.ToArray();
            }

            return blobContent;
        }
    }
}