
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Shared.Extensions;

public static class ObjectExtension
{
    
    public static string ToJsonString(this object o)
    {
        var options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(o, options);
    }
}