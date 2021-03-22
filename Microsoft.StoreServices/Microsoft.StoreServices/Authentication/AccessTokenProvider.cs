﻿//-----------------------------------------------------------------------------
// AccessTokenProvider.cs
//
// Xbox Advanced Technology Group (ATG)
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.StoreServices
{
    /// <summary>
    /// Access tokens are generated by your service and either used as the Authorization 
    /// header for calls to the Microsoft.Store services, or sent to the client to
    /// generate a UserStoreId.  The specified Audience of the access token will
    /// determine what it is used for:
    /// Service Access Token -     https://onestore.microsoft.com - Used as the bearer token
    ///                            in the Authorization header of service-to-service calls. 
    /// Purchase Access Token -    https://onestore.microsoft.com/b2b/keys/create/purchase
    ///                            Sent to the client to generate a UserPurchaseId
    /// Collections Access Token - https://onestore.microsoft.com/b2b/keys/create/collections
    ///                            Sent to the client to generate a UserCollectionsId
    /// </summary>
    public class AccessTokenProvider : IAccessTokenProvider
    {
        /// <summary>
        /// This can be overwritten with an HttpClientFactory.CreateClient() for better performance
        /// </summary>
        public static Func<HttpClient> CreateHttpClientFunc = () => new HttpClient();

        protected string _tenantId;
        protected string _clientId;
        protected string _clientSecret;

        public AccessTokenProvider(string tenantId, string clientId, string clientSecret)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException($"{nameof(_tenantId)} required", nameof(_tenantId));
            }
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException($"{nameof(_clientId)} required", nameof(_clientId));
            }
            if (string.IsNullOrEmpty(clientSecret))
            {
                throw new ArgumentException($"{nameof(_clientSecret)} required", nameof(_clientSecret));
            }

            _tenantId = tenantId;
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        public Task<AccessToken> GetServiceAccessTokenAsync()
        {
            return CreateAccessTokenAsync(AccessTokenTypes.Service);
        }

        public Task<AccessToken> GetCollectionsAccessTokenAsync()
        {
            return CreateAccessTokenAsync(AccessTokenTypes.Collections);
        }

        public Task<AccessToken> GetPurchaseAccessTokenAsync()
        {
            return CreateAccessTokenAsync(AccessTokenTypes.Purchase);
        }

        /// <summary>
        /// Stand alone API to retrieve the AccessToken for the credentials and Audience
        /// provided.
        /// NOTE, this function does not cache the token if called directly.  To use
        /// the built-in ServerCache use GetAccessTokenAsync().
        /// </summary>
        /// <param name="audience"></param>
        /// <returns>Access token, otherwise Exception will be thrown</returns>
        protected virtual async Task<AccessToken> CreateAccessTokenAsync(string audience)
        {
            //  Validate we have the needed values
            if (string.IsNullOrEmpty(audience))
            {
                throw new ArgumentException($"{nameof(audience)} required", nameof(audience));
            }

            var requestUri = $"https://login.microsoftonline.com/{_tenantId}/oauth2/token";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri.ToString());
            var requestBody = $"grant_type=client_credentials&client_id={_clientId}" +
                              $"&client_secret={_clientSecret}" +
                              $"&resource={audience}";
            httpRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            // Post the request and wait for the response
            var httpClient = CreateHttpClientFunc();
            using (var httpResponse = await httpClient.SendAsync(httpRequest))
            {
                string responseBody = await httpResponse.Content.ReadAsStringAsync();

                if (httpResponse.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<AccessToken>(responseBody);
                }
                else
                {
                    throw new StoreServicesHttpResponseException($"Unable to acquire access token for {audience} : {httpResponse.ReasonPhrase}", httpResponse);
                }
            }
        }
    }
}