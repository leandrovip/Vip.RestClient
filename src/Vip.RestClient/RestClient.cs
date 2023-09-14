using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Vip.RestClient
{
    public class RestClient
    {
        #region Events

        public event EventHandler<ResponseEvent> ResponseDataReceived;
        public event EventHandler<HttpRequestMessage> BeforeSend;

        #endregion

        #region Fields

        private readonly HttpClient _httpClient;

        #endregion

        #region Properties

        public readonly Uri BaseUri;

        #endregion

        #region Constructors

        public RestClient(string baseUrl, HttpClientHandler clientHandler = null)
        {
            if (!baseUrl.EndsWith("/")) baseUrl += '/';
            BaseUri = new Uri(baseUrl);

            if (clientHandler == null) clientHandler = new HttpClientHandler();
            clientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            _httpClient = new HttpClient(clientHandler);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

        #endregion

        #region Client Methods

        #region GET

        public async Task<Response<T>> GetAsync<T>(string endpoint)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(HttpMethod.Get, uri);

            return await SendMessageAsync<T>(uri, message);
        }

        #endregion

        #region POST

        public async Task<Response> PostAsync(string endpoint)
        {
            var uri = new Uri(BaseUri, endpoint);

            return await PostAsync(uri, null);
        }

        public async Task<Response> PostAsync(string endpoint, object value)
        {
            var uri = new Uri(BaseUri, endpoint);
            var content = SerializeContent(value);

            return await PostAsync(uri, content);
        }

        public async Task<Response<T>> PostAsync<T>(string endpoint, HttpContent content)
        {
            var uri = new Uri(BaseUri, endpoint);
            return await PostAsync<T>(uri, content);
        }

        public async Task<Response<T>> PostAsync<T>(string endpoint, object value)
        {
            var uri = new Uri(BaseUri, endpoint);

            using var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Content = SerializeContent(value);

            return await SendMessageAsync<T>(uri, message);
        }

        private async Task<Response<T>> PostAsync<T>(Uri uri, HttpContent content)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, uri);
            if (content is not null) message.Content = content;

            return await SendMessageAsync<T>(uri, message);
        }

        private async Task<Response> PostAsync(Uri uri, HttpContent content)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, uri);
            if (content is not null) message.Content = content;

            return await SendMessageAsync(message);
        }

        #endregion

        #region PUT

        public async Task<Response<T>> PutAsync<T>(string endpoint, object value)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(HttpMethod.Put, uri);
            message.Content = SerializeContent(value);

            return await SendMessageAsync<T>(uri, message);
        }

        public async Task<Response> PutAsync(string endpoint, object value)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(HttpMethod.Put, uri);
            message.Content = SerializeContent(value);

            return await SendMessageAsync(message);
        }

        #endregion

        #region PATCH

        public async Task<Response<T>> PatchAsync<T>(string endpoint, object value)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(new HttpMethod("PATCH"), uri);
            message.Content = SerializeContent(value);

            return await SendMessageAsync<T>(uri, message);
        }

        public async Task<Response> PatchAsync(string endpoint, object value)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(new HttpMethod("PATCH"), uri);
            message.Content = SerializeContent(value);

            return await SendMessageAsync(message);
        }

        #endregion

        #region DELETE

        public async Task<Response> DeleteAsync(string endpoint)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(HttpMethod.Delete, uri);

            return await SendMessageAsync(message);
        }

        public async Task<Response<T>> DeleteAsync<T>(string endpoint)
        {
            var uri = new Uri(BaseUri, endpoint);
            using var message = new HttpRequestMessage(HttpMethod.Delete, uri);

            return await SendMessageAsync<T>(uri, message);
        }

        #endregion

        #region OPTIONS

        public async Task<Response> OptionsAsync(string endpoint, object value) => await OptionsAsync(endpoint, Helper.BuildParams(value));

        public async Task<Response> OptionsAsync(string endpoint, IEnumerable<(string, string)> headers)
        {
            var pairs = headers.Select(p => new KeyValuePair<string, string>(p.Item1, p.Item2));
            return await OptionsAsync(endpoint, pairs);
        }

        public async Task<Response> OptionsAsync(string endpoint, IEnumerable<KeyValuePair<string, string>> headers)
        {
            var uri = new Uri(BaseUri, endpoint);
            var message = new HttpRequestMessage(HttpMethod.Options, uri);

            foreach (var pair in headers) message.Headers.Add(pair.Key, pair.Value);

            return await SendMessageAsync(message);
        }

        #endregion

        #endregion

        #region Public Methods

        public void ConfigureHttpClient(Action<HttpClient> client) => client(_httpClient);

        public void SetAuthorization(string auth) => SetHeader("Authorization", auth);

        public void SetAuthorizationBearer(string token) => SetHeader("Authorization", "Bearer " + token);

        public void SetHeader(string key, string value)
        {
            lock (_httpClient)
            {
                _httpClient.DefaultRequestHeaders.Remove(key);
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }
        }

        public void RemoveAuthorization()
        {
            lock (_httpClient)
            {
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
            }
        }

        #endregion

        #region Private Methods

        private async Task<Response> SendMessageAsync(HttpRequestMessage message)
        {
            BeforeSend?.Invoke(this, message);
            var start = DateTime.Now;
            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
            return Response.Build(response, start);
        }

        private async Task<Response<T>> SendMessageAsync<T>(Uri uri, HttpRequestMessage message)
        {
            BeforeSend?.Invoke(this, message);
            var start = DateTime.Now;
            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
            return await GetResponseAsync<T>(uri, response, start);
        }

        private HttpContent SerializeContent(object value)
        {
            var jsonValue = JsonConvert.SerializeObject(value);
            return new StringContent(jsonValue, Encoding.UTF8, "application/json");
        }

        private async Task<Response<T>> GetResponseAsync<T>(Uri uri, HttpResponseMessage response, DateTime start)
        {
            T data = default;
            var binary = false;

            string content = null;
            if (typeof(T) == typeof(byte[]))
            {
                data = (T) (object) await response.Content.ReadAsByteArrayAsync();
                binary = true;
            }
            else if (typeof(T) == typeof(Stream))
            {
                data = (T) (object) await response.Content.ReadAsStreamAsync();
                binary = true;
            }
            else
            {
                content = await response.Content.ReadAsStringAsync();
            }

            string errorData = null;
            var contentHeaders = response.Content.Headers;

            ResponseDataReceived?.Invoke(this, new ResponseEvent
            {
                Received = DateTime.UtcNow,
                Uri = uri,
                Success = response.IsSuccessStatusCode,
                StatusCode = (int) response.StatusCode,
                Content = content,
            });

            if (!response.IsSuccessStatusCode)
            {
                errorData = content;
            }
            else if (!binary)
            {
                if (content.StartsWith("%7B"))
                    content = WebUtility.UrlDecode(content);

                if (typeof(T) == typeof(string))
                {
                    data = (T) (object) content;
                }
                else if (typeof(T) == typeof(Jwt))
                {
                    if (content.Contains("\""))
                        data = (T) (object) Jwt.Parse(content.Replace("\"", ""));
                    else
                        data = (T) (object) Jwt.Parse(content);
                }
                else
                {
                    data = JsonConvert.DeserializeObject<T>(content);
                }
            }

            return Response<T>.Build(response, contentHeaders, data, errorData, start);
        }

        #endregion
    }
}