using System;
using System.Net;
using System.Net.Http;

namespace Vip.RestClient;

public class ApiException : Exception
{
    #region Properties

    public Uri Resource { get; }
    public HttpStatusCode StatusCode { get; }
    public HttpResponseMessage Response { get; }

    #endregion

    #region Constructors

    private ApiException(Uri resource, HttpResponseMessage response)
        : base($"[{(int) response.StatusCode}] {resource} - {response.ReasonPhrase}")
    {
        StatusCode = response.StatusCode;
        Resource = resource;
        Response = response;
    }

    #endregion

    #region Static Methods

    internal static Exception FromResponse(Uri resource, HttpResponseMessage response)
    {
        return new ApiException(resource, response);
    }

    #endregion
}