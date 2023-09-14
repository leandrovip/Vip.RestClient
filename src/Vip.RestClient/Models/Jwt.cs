using Newtonsoft.Json;

namespace Vip.RestClient;

public class Jwt : JwtBase
{
    #region Properties

    public JwtGeneric Content { get; private set; }

    #endregion

    #region Static Methods

    public static Jwt Parse(string token)
    {
        var data = Jwt<JwtGeneric>.Parse(token);
        return new Jwt
        {
            Header = data.Header,
            Payload = data.Payload,
            Signature = data.Signature,
            Content = data.Content,
            OriginalToken = token
        };
    }

    #endregion
}

public class Jwt<T> : JwtBase
{
    #region Properties

    public T Content { get; private set; }

    #endregion

    #region Static Methods

    public static Jwt<T> Parse(string token)
    {
        var jwt = ParseText(token);
        var data = new Jwt<T>
        {
            Header = jwt.Header,
            Payload = jwt.Payload,
            Signature = jwt.Signature,
            Content = JsonConvert.DeserializeObject<T>(jwt.Payload),
            OriginalToken = token,
        };

        return data;
    }

    #endregion
}