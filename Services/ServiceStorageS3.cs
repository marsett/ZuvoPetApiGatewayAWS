using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace ZuvoPetApiAWS.Services
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
        public async Task<bool> UploadFileAsync
        (string fileName, Stream stream)
        {
            PutObjectRequest request = new PutObjectRequest
            {
                Key = fileName,
                BucketName = this.BucketName,
                InputStream = stream
            };

            PutObjectResponse response = await
                this.ClientS3.PutObjectAsync(request);
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
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

    }
}
