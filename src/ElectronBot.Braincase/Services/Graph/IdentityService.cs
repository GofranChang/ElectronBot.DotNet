﻿using System.Configuration;
using System.Net.NetworkInformation;
using ElectronBot.Braincase.Contracts.Services;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Verdure.ElectronBot.Core.Helpers;

namespace ElectronBot.Braincase.Services;

public class IdentityService
{
    // For more information about using Identity, see
    // https://github.com/microsoft/TemplateStudio/blob/main/docs/UWP/services/identity.md
    //
    // Read more about Microsoft Identity Client here
    // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki
    // https://docs.microsoft.com/azure/active-directory/develop/v2-overview

    // TODO: Please create a ClientID following these steps and update the app.config IdentityClientId.
    // https://docs.microsoft.com/azure/active-directory/develop/quickstart-register-app
    private readonly string _clientId = ConfigurationManager.AppSettings["IdentityClientId"];

    private readonly string _redirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    private readonly string[] _graphScopes = new string[] { "user.read", "Tasks.ReadWrite" };

    private bool _integratedAuthAvailable;

    private IPublicClientApplication _client;
    private AuthenticationResult _authenticationResult;

    public event EventHandler LoggedIn;

    public event EventHandler LoggedOut;


    public void InitializeWithAadAndPersonalMsAccounts()
    {
        _integratedAuthAvailable = false;
        _client = PublicClientApplicationBuilder.Create(_clientId)
                                                .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                                                .WithRedirectUri(_redirectUri)
                                                .Build();
    }

    /// <summary>
    /// Attaches the token cache to the Public Client app.
    /// </summary>
    /// <returns>IAccount list of already signed-in users (if available)</returns>
    public async Task AttachTokenCacheAsync()
    {
        // Cache configuration and hook-up to public application. Refer to https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet/wiki/Cross-platform-Token-Cache#configuring-the-token-cache
        var storageProperties = new StorageCreationPropertiesBuilder(Constants.CacheFileName, MsalCacheHelper.UserRootDirectory).Build();
        var msalcachehelper = await MsalCacheHelper.CreateAsync(storageProperties);
        msalcachehelper.RegisterCache(_client.UserTokenCache);
    }

    public void InitializeWithPersonalMsAccount()
    {
        _integratedAuthAvailable = false;
        _client = PublicClientApplicationBuilder.Create(_clientId)
                                                .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
                                                .WithRedirectUri(_redirectUri)
                                                .Build();
    }

    public void InitializeWithAadMultipleOrgs(bool integratedAuth = false)
    {
        _integratedAuthAvailable = integratedAuth;
        _client = PublicClientApplicationBuilder.Create(_clientId)
                                                .WithAuthority(AadAuthorityAudience.AzureAdMultipleOrgs)
                                                .WithRedirectUri(_redirectUri)
                                                .Build();
    }

    public void InitializeWithAadSingleOrg(string tenant, bool integratedAuth = false)
    {
        _integratedAuthAvailable = integratedAuth;
        _client = PublicClientApplicationBuilder.Create(_clientId)
                                                .WithAuthority(AzureCloudInstance.AzurePublic, tenant)
                                                .WithRedirectUri(_redirectUri)
                                                .Build();
    }

    public bool IsLoggedIn() => _authenticationResult != null;

    public async Task<LoginResultType> LoginAsync()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return LoginResultType.NoNetworkAvailable;
        }

        try
        {
            var accounts = await _client.GetAccountsAsync();

            _authenticationResult = await _client.AcquireTokenInteractive(_graphScopes)
                                                 .WithAccount(accounts.FirstOrDefault())
                                                 .ExecuteAsync();

            LoggedIn?.Invoke(this, EventArgs.Empty);
            return LoginResultType.Success;
        }
        catch (MsalClientException ex)
        {
            if (ex.ErrorCode == "authentication_canceled")
            {
                return LoginResultType.CancelledByUser;
            }

            return LoginResultType.UnknownError;
        }
        catch (Exception)
        {
            return LoginResultType.UnknownError;
        }
    }

    public bool IsAuthorized()
    {
        // TODO: You can also add extra authorization checks here.
        // i.e.: Checks permisions of _authenticationResult.Account.Username in a database.
        return true;
    }

    public string GetAccountUserName()
    {
        return _authenticationResult?.Account?.Username;
    }

    public async Task LogoutAsync()
    {
        try
        {
            var accounts = await _client.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account != null)
            {
                await _client.RemoveAsync(account);
            }

            _authenticationResult = null;
            LoggedOut?.Invoke(this, EventArgs.Empty);
        }
        catch (MsalException)
        {
            // TODO: LogoutAsync can fail please handle exceptions as appropriate to your scenario
            // For more info on MsalExceptions see
            // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/exceptions
        }
    }

    public async Task<string> GetAccessTokenForGraphAsync() => await GetAccessTokenAsync(_graphScopes);

    private async Task<string> GetAccessTokenAsync(string[] scopes)
    {
        var acquireTokenSuccess = await AcquireTokenSilentAsync(scopes);
        if (acquireTokenSuccess)
        {
            return _authenticationResult.AccessToken;
        }
        else
        {
            try
            {
                // Interactive authentication is required
                var accounts = await _client.GetAccountsAsync();
                _authenticationResult = await _client.AcquireTokenInteractive(scopes)
                                                     .WithAccount(accounts.FirstOrDefault())
                                                     .ExecuteAsync();
                return _authenticationResult.AccessToken;
            }
            catch (MsalException)
            {
                // AcquireTokenSilent and AcquireTokenInteractive failed, the session will be closed.
                _authenticationResult = null;
                LoggedOut?.Invoke(this, EventArgs.Empty);
                return string.Empty;
            }
        }
    }

    public async Task<bool> AcquireTokenSilentAsync() => await AcquireTokenSilentAsync(_graphScopes);

    private async Task<bool> AcquireTokenSilentAsync(string[] scopes)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return false;
        }

        try
        {
            var accounts = await _client.GetAccountsAsync();
            if (accounts.Any())
            {
                _authenticationResult = await _client.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                                               .ExecuteAsync();

                return true;
            }
            return false;
        }
        catch (MsalUiRequiredException)
        {
            if (_integratedAuthAvailable)
            {
                try
                {
                    _authenticationResult = await _client.AcquireTokenByIntegratedWindowsAuth(scopes)
                                                         .ExecuteAsync();
                    return true;
                }
                catch (MsalUiRequiredException)
                {
                    // Interactive authentication is required
                    return false;
                }
            }
            else
            {
                // Interactive authentication is required
                return false;
            }
        }
        catch (MsalException)
        {
            // TODO: Silentauth failed, please handle this exception as appropriate to your scenario
            // For more info on MsalExceptions see
            // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/exceptions
            return false;
        }
    }
}
