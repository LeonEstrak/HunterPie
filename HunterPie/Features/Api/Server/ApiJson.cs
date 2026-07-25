using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace HunterPie.Features.Api.Server;

/// <summary>
/// Shared JSON serialization settings for the API: camelCase property names,
/// enums as strings, null members omitted.
/// </summary>
internal static class ApiJson
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new StringEnumConverter() }
    };

    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
}
