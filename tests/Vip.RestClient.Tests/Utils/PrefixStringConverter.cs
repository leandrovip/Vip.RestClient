using System;
using Newtonsoft.Json;

namespace Vip.RestClient.Tests.Utils;

internal sealed class PrefixStringConverter : JsonConverter
{
    #region Métodos

    public override bool CanConvert(Type objectType) => objectType == typeof(string);
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue("written:" + value);
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => "converted:" + (string) reader.Value;

    #endregion
}