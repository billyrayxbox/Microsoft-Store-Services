﻿//-----------------------------------------------------------------------------
// UserStoreId.cs
//
// Xbox Advanced Technology Group (ATG)
// Copyright (C) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License file under the project root for
// license information.
//-----------------------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.StoreServices
{
    /// <summary>
    /// Enum of the different UserStoreID types
    /// </summary>
    public enum UserStoreIdType
    {
        UserCollectionsId = 0,
        UserPurchaseId,
        Unknown
    }

    /// <summary>
    /// The different Audience values that determine which type of USerAccessToken is being used
    /// </summary>
    public class UserStoreIdAudiences
    {
        //  These are the audience values for each access token type
        public const string UserCollectionsId = "https://collections.mp.microsoft.com/v6.0/keys";
        public const string UserPurchaseId    = "https://purchase.mp.microsoft.com/v6.0/keys";
    }

    /// <summary>
    /// Extracts useful claims info from a UserStoreId (general term for either a UserCollectionsId
    /// or UserPurchaseId) and provides utility function to refresh an expired key
    /// </summary>
    public class UserStoreId
    {
        /// <summary>
        /// Can be overridden with an HttpClientFactory.CreateClient() if used by your service.
        /// Ex: UserStoreId.CreateHttpClientFunc = httpClientFactory.CreateClient;
        /// </summary>
        public static Func<HttpClient> CreateHttpClientFunc = () => new HttpClient();

        /// <summary>
        /// URI that allows this token to be refreshed once it expires.
        /// </summary>
        public string RefreshUri { get; set; }

        /// <summary>
        /// Identifies which type of UserStoreId this is based on the audience value
        /// UserCollectionsId (https://collections.mp.microsoft.com/v6.0/keys) or
        /// UserPurchaseId (https://purchase.mp.microsoft.com/v6.0/keys).
        /// </summary>
        public UserStoreIdType KeyType { get; set; }

        /// <summary>
        /// UTC date time when the key will expire and need to be refreshed.
        /// </summary>
        public DateTime Expires { get; set; }
        
        /// <summary>
        /// The UserStoreId that was used to generate this object and would be
        /// used as the beneficiary in a b2b call for authentication.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Creates a UserStoreId based on the storeIdKey JWT's claims
        /// </summary>
        /// <param name="storeIdKey">JWT representing the UserStoreId</param>
        public UserStoreId(string storeIdKey)
        {
            UpdateKeyInformation(storeIdKey);
        }

        private void UpdateKeyInformation(string storeIdKey)
        {
            //  We can use the values in the payload to know how and when to 
            //  refresh it.
            UserStoreIdClaims keyClaims = JsonConvert.DeserializeObject<UserStoreIdClaims>(Jose.JWT.Payload(storeIdKey));
            RefreshUri = keyClaims.RefreshUri;
            Expires = keyClaims.ExpiresOn;
            Key = storeIdKey;

            if (keyClaims.Audience == UserStoreIdAudiences.UserCollectionsId)
            {
                KeyType = UserStoreIdType.UserCollectionsId;
            }
            else if (keyClaims.Audience == UserStoreIdAudiences.UserPurchaseId)
            {
                KeyType = UserStoreIdType.UserPurchaseId;
            }
            else
            {
                KeyType = UserStoreIdType.Unknown;
            }
        }

        /// <summary>
        /// Uses the RefreshURI to generate a new UserStoreId key for this user once the current one is expired
        /// </summary>
        /// <param name="expiredStoreId">The UserStoreId that we want to refresh</param>
        /// <param name="serviceToken">AAD token generated by your service with the audience of https://onestore.microsoft.com </param>
        /// <param name="httpClient">HttpClient to be used to make the refresh call</param>
        /// <returns>A new UserStoreId for the same store user with new expire date</returns>
        public async Task<bool> RefreshStoreId(string serviceToken)
        {
            var refreshRequest = new UserStoreIdRefreshRequest()
            {
                ServiceToken = serviceToken,
                UserStoreId = Key
            };

            var requestBodyString = JsonConvert.SerializeObject(refreshRequest);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, RefreshUri)
            {
                Content = new StringContent(requestBodyString, Encoding.UTF8, "application/json")
            };

            //  call the refresh
            var httpClient = CreateHttpClientFunc();
            var httpResponse = await httpClient.SendAsync(httpRequest);
            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<UserStoreIdRefreshResponse>(responseBody);
            UpdateKeyInformation(response.Key);

            return true;
        }
    }
}
