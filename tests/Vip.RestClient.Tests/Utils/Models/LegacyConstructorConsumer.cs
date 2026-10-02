using Vip.RestClient;

namespace Vip.RestClient.Tests.Utils.Models;

internal sealed class LegacyConstructorConsumer : ClientApi
{
    public LegacyConstructorConsumer() : base(null, null, null) { }
}
