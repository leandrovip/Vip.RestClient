using System;

namespace Vip.RestClient;

public class ResponseEvent
{
    #region Properties

    public DateTime Received { get; set; }
    public Uri Uri { get; set; }
    public string Content { get; set; }
    public bool Success { get; set; }
    public int StatusCode { get; set; }

    #endregion

    #region Methods

    public override string ToString()
    {
        return $"{Received:G} {Uri.PathAndQuery} [{StatusCode}] {Content}";
    }

    #endregion
}