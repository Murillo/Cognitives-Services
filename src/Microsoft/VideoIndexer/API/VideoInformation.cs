using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VideoIndexer.Api
{
    public class VideoInformation
    {
        private readonly int _timeWaiting = 10000;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _location;
        private readonly string _accountId;
        private string _videoAccessToken;
        private string _videoId;

        public VideoInformation(string apiKey, string apiUrl, string location, string accountId)
        {
            _apiKey = apiKey;
            _apiUrl = apiUrl;
            _location = location;
            _accountId = accountId;
        }

        public string GetPlayerWidgetUrl()
        {
            var handler = new HttpClientHandler(); 
            handler.AllowAutoRedirect = false; 
            using (var client = new HttpClient(handler))
            {
                var playerWidgetRequestResult = client.GetAsync($"{_apiUrl}/{_location}/Accounts/{_accountId}/Videos/{_videoId}/PlayerWidget?accessToken={_videoAccessToken}").Result;
                return playerWidgetRequestResult.Headers.Location.ToString();
            }
        }

        public void Run(string videoUrl)
        {
            var handler = new HttpClientHandler(); 
            handler.AllowAutoRedirect = false; 
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey); 

                // obtain account access token
                var accountAccessTokenRequestResult = client.GetAsync($"{_apiUrl}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true").Result;
                var accountAccessToken = accountAccessTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");
                client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

                // upload a video
                var content = new MultipartFormDataContent();
                var uploadRequestResult = client.PostAsync($"{_apiUrl}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name=some_name&description=some_description&privacy=private&partition=some_partition&videoUrl={videoUrl}", content).Result;
                var uploadResult = uploadRequestResult.Content.ReadAsStringAsync().Result;

                // get the video id from the upload result
                var returnInfos = JsonConvert.DeserializeObject<dynamic>(uploadResult);
                var returnInfosString = returnInfos.ToString();

                _videoId = returnInfos["id"].ToString(); 
                WaitingProcess();
                // return returnInfos["id"].ToString();
            }
        }

        private void WaitingProcess()
        {
            while (true)
            {
                Thread.Sleep(_timeWaiting);
                var processingState = StatusProcess();
                if (processingState != "Uploaded" && processingState != "Processing")
                    break;
            }
        }
        
        private string StatusProcess()
        {
            var handler = new HttpClientHandler(); 
            handler.AllowAutoRedirect = false; 
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                var videoTokenRequestResult = client.GetAsync($"{_apiUrl}/auth/{_location}/Accounts/{_accountId}/Videos/{_videoId}/AccessToken?allowEdit=true").Result;
                _videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

                client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

                var videoGetIndexRequestResult = client.GetAsync($"{_apiUrl}/{_location}/Accounts/{_accountId}/Videos/{_videoId}/Index?accessToken={_videoAccessToken}&language=English").Result;
                var videoGetIndexResult = videoGetIndexRequestResult.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult)["state"].ToString();
            }
        }
    }
}