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
        /// <returns>Uri</returns>
        public Uri GetBlobSasUri(string fileName)
        {
            Uri? sasUri = null;
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

                sasUri = blobClient.GenerateSasUri(sasBuilder);
            }

            return sasUri;
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

        /// <summary>
        /// Uploads a file's stream to the storage account container. This will overwrite a file
        /// that has the same name.
        /// </summary>
        /// <param name="imageMemoryStream">Memory stream object of the file being uploaded</param>
        /// <param name="fileName">The name of the file</param>
        /// <returns></returns>
        public async Task UploadFromStreamAsync(MemoryStream imageMemoryStream, string fileName)
        {
            BlobClient blobClient = new BlobClient(ConnectionString, ContainerName, fileName);
            imageMemoryStream.Position = 0;
            await blobClient.UploadAsync(imageMemoryStream, true);
        }
    }
}