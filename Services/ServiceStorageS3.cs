using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace ZuvoPetApiGatewayAWS.Services
{
    public class ServiceStorageS3
    {
        private string BucketName;
        private IAmazonS3 ClientS3;
        public ServiceStorageS3(IConfiguration configuration
        , IAmazonS3 clientS3)
        {
            this.BucketName = configuration.GetValue<string>
                ("AWS:BucketName");
            this.ClientS3 = clientS3;
        }
        public async Task<bool> UploadFileAsync(string fileName, Stream stream)
        {
            // Determinar el MIME type basado en la extensión
            string contentType = GetContentType(fileName);

            PutObjectRequest request = new PutObjectRequest
            {
                Key = fileName,
                BucketName = this.BucketName,
                InputStream = stream,
                ContentType = contentType // Añadir el Content-Type
            };

            PutObjectResponse response = await this.ClientS3.PutObjectAsync(request);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }

        private string GetContentType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        public async Task<bool> DeleteFileAsync
            (string fileName)
        {
            DeleteObjectResponse response = await
                this.ClientS3.DeleteObjectAsync
                (this.BucketName, fileName);
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // Add new method to download files from S3
        public async Task<(Stream Stream, string ContentType)?> DownloadFileAsync(string fileName)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = this.BucketName,
                    Key = fileName
                };

                var response = await this.ClientS3.GetObjectAsync(request);

                return (response.ResponseStream, response.Headers.ContentType);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // File not found
                return null;
            }
        }

        // Add method to get S3 URL for a file
        public string GetFileUrl(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            return $"https://{this.BucketName}.s3.amazonaws.com/{fileName}";
        }
    }
}
