using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Drawing;

namespace ZuvoPetApiGatewayAWS.Helpers
{
    public class HelperSecretsManager
    {
        public static async Task<string> GetSecretAsync()
        {
            string secretName = "secretszuvopet";
            string region = "us-east-1";

            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            GetSecretValueRequest request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT",
            };

            GetSecretValueResponse response;
            response = await client.GetSecretValueAsync(request);
            string secret = response.SecretString;
            return secret;
        }
    }
}
