using System.Text.Json.Serialization;

namespace CustomResultError;

[JsonSerializable(typeof(ErrorString))]
[JsonSerializable(typeof(ExceptionError))]
public partial class ErrorSerializerContext : JsonSerializerContext
{
}
