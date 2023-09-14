namespace Vip.RestClient.Test.Models;

public class Response
{
    #region Properties

    public Args args { get; set; }
    public Headers headers { get; set; }
    public string origin { get; set; }
    public string url { get; set; }
    public Data json { get; set; }

    #endregion
}