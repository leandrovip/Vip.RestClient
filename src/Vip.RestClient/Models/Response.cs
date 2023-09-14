using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Vip.RestClient;

public class Response
{
    #region Properties

    public HttpResponseHeaders Headers { get; protected set; }
    public HttpContentHeaders ContentHeaders { get; protected set; }
    public HttpRequestMessage RequestMessage { get; protected set; }
    public bool IsSuccessStatusCode { get; protected set; }
    public string ReasonPhrase { get; protected set; }
    public HttpStatusCode StatusCode { get; protected set; }
    public string ErrorResponseData { get; protected set; }
    public TimeSpan Duration { get; protected set; }

    #endregion

    #region Methods

    public T ParseErrorResponseData<T>()
    {
        return JsonConvert.DeserializeObject<T>(ErrorResponseData);
    }

    public bool TryParseErrorResponseData<TError>(out TError err)
    {
        try
        {
            err = ParseErrorResponseData<TError>();
            return true;
        }
        catch
        {
            err = default;
            return false;
        }
    }

    public void EnsureSuccessStatusCode()
    {
        if (IsSuccessStatusCode) return;
        throw new UnsuccessfulStatusCodeException(this);
    }

    public void EnsureSuccessStatusCode<TError>() where TError : class
    {
        if (IsSuccessStatusCode) return;

        TryParseErrorResponseData(out TError err);
        throw new UnsuccessfulStatusCodeException<TError>(this, err);
    }

    #endregion

    #region Static Methods

    public static Response Build(HttpResponseMessage response, DateTime start)
    {
        string errorData = null;
        if (!response.IsSuccessStatusCode) errorData = response.Content.ReadAsStringAsync().Result;

        return new Response
        {
            Headers = response.Headers,
            ContentHeaders = response.Content.Headers,
            RequestMessage = response.RequestMessage,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            ReasonPhrase = response.ReasonPhrase,
            StatusCode = response.StatusCode,
            ErrorResponseData = errorData,
            Duration = DateTime.Now - start,
        };
    }

    #endregion
}

public class Response<T> : Response
{
    #region Properties

    public T Data { get; private set; }

    #endregion

    #region Static Methods

    public static Response<T> Build(HttpResponseMessage response, HttpContentHeaders headers, T data, string errorData, DateTime start)
    {
        return new Response<T>
        {
            Data = data,
            Headers = response.Headers,
            ContentHeaders = headers,
            RequestMessage = response.RequestMessage,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            ReasonPhrase = response.ReasonPhrase,
            StatusCode = response.StatusCode,
            ErrorResponseData = errorData,
            Duration = DateTime.Now - start,
        };
    }

    #endregion
}