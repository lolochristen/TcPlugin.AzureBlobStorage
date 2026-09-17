using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace TcPlugin.AzureBlobStorage.Authentication
{
    /// <summary>
    ///     Credential shared by all Azure clients. Delegates to an inner credential that can be switched to an
    ///     interactive login without recreating the clients that already use it.
    /// </summary>
    public class AzureCredentialProvider : TokenCredential
    {
        private static readonly string[] ManagementScopes = { "https://management.azure.com/.default" };

        private TokenCredential _inner = CreateDefaultCredential();
        private bool _isInteractive;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return _inner.GetToken(requestContext, cancellationToken);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return _inner.GetTokenAsync(requestContext, cancellationToken);
        }

        /// <param name="tenantId">Tenant to request the token for, or null for the home tenant.</param>
        public AccessToken GetManagementToken(string tenantId = null)
        {
            return GetToken(new TokenRequestContext(ManagementScopes, tenantId: tenantId), CancellationToken.None);
        }

        /// <returns>false if the interactive login is already in use.</returns>
        public bool TrySwitchToInteractiveLogin()
        {
            if (_isInteractive)
            {
                return false;
            }

            _inner = new InteractiveBrowserCredential();
            _isInteractive = true;
            return true;
        }

        private static TokenCredential CreateDefaultCredential()
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCodeCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeInteractiveBrowserCredential = false
            });
        }
    }
}
