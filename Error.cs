global using ErrorString = CustomResultError.Error<string>;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CustomResultError;


public abstract class Error
{
    protected Error(string message)
    {
        Message = message;
        Details = [];
    }

    protected Error(string message, params string[] details)
    {
        Message = message;
        Details = details;
    }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("details")]
    public string[] Details { get; protected set; }

    public override string ToString() => Message;
}

public class Error<CodeType> : Error, IEquatable<Error<CodeType>>
{
    [JsonPropertyName("code")]
    public CodeType Code { get; }

    public Error(string message, CodeType code) : base(message)
    {
        Code = code;
    }
    public Error(string message, CodeType code, params string[] details) : base(message, details)
    {
        Code = code;
    }

    static bool IsNumericType(Type type)
    {
        return type.IsPrimitive && (
            type == typeof(int) ||
            type == typeof(uint) ||
            type == typeof(long) ||
            type == typeof(ulong) ||
            type == typeof(short) ||
            type == typeof(ushort) ||
            type == typeof(byte) ||
            type == typeof(sbyte) ||
            type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(decimal)
        );
    }


    protected string BuildDetailsString() =>
        string.Join(",\r\n", Details.Select(d => $"\"{d}\""));

    public virtual string ToJsonString()
    {
        string sCode = Code?.ToString() ?? "";


        if (sCode == "" || !IsNumericType(Code!.GetType()))
            sCode = $"\"{Code}\"";

        if (Details.Length == 0)
            return $$"""
                {
                    "code" : {{sCode}},
                    "message" : "{{Message}}"
                }
                """;

        string sDetails = BuildDetailsString();
        return $$"""
                    {
                        "code" : {{sCode}},
                        "message" : "{{Message}}",
                        "details" : [{{sDetails}}]
                    }
                    """;

    }

    public static Error<CodeType>? Parse(string text)
    {
        JsonObject? o = JsonNode.Parse(text)?.AsObject();
        if (o is null) return null;

        if (!o.ContainsKey("message") || !o.ContainsKey("code")) return null;

        string message = o["message"]!.ToString();
        CodeType code = o["code"]!.GetValue<CodeType>();

        if(o.ContainsKey("details"))
        {
            string[] details = o["details"]!
                .AsArray()
                .Where(n=>n is not null)
                .Select(n => n!.GetValue<string>()).ToArray();
            return new Error<CodeType>(message, code, details);
        }

        return new Error<CodeType>(message, code);
    }

    public bool Equals(Error<CodeType>? other)
    {
        if (other is null) return false;
        return Code!.Equals(other.Code);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Error<CodeType>);
    }

    public override int GetHashCode()
    {
        return Code!.GetHashCode();
    }

    public static bool operator ==(Error<CodeType>? left, Error<CodeType>? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;

        return Equals(left, right);
    }

    public static bool operator !=(Error<CodeType>? left, Error<CodeType>? right)
    {
        return !Equals(left, right);
    }



}
