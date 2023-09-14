using System.Net.Http;

namespace Vip.RestClient;

public class UnsuccessfulStatusCodeException : HttpRequestException
{
    #region Properties

    public Response Response { get; }

    #endregion

    #region Constructor

    public UnsuccessfulStatusCodeException(Response response) : base($"[{response.RequestMessage.Method}] {response.RequestMessage.RequestUri} [{(int) response.StatusCode}] failed with {response.ReasonPhrase}")
    {
        Response = response;
    }

    #endregion
}

public class UnsuccessfulStatusCodeException<T> : HttpRequestException
{
    #region Properties

    public Response Response { get; }
    public T ErrorInformation { get; }

    #endregion

    #region Constructor

    public UnsuccessfulStatusCodeException(Response response, T error) : base($"[{response.RequestMessage.Method}] {response.RequestMessage.RequestUri} [{(int) response.StatusCode}] failed with {response.ReasonPhrase}")
    {
        Response = response;
        ErrorInformation = error;
    }

    #endregion
}