namespace CustomResultError;

public class ExceptionError : Error<Exception>
{
    private readonly string? _domain;

    public ExceptionError(
        Exception exception,
        string? domain = null) : base(exception.Message, exception)
    {
        _domain = domain;

        var e = exception;
        List<string> details = [];
        while (e.InnerException is not null)
        {
            details.Add(e.InnerException.Message);
            e = e.InnerException;
        }
        Details = [.. details];
    }

    //Serialization is useful for AOT only.
    public override string ToJsonString()
    {
        string sDomain = string.IsNullOrWhiteSpace(_domain) ? "" : $"{_domain}.";

        string sCode = $"{sDomain}{Code.GetType().Name}";

        if (Details.Length == 0)
            return $$"""
            { 
                "code" : "{{sCode}}",
                "message" : "{{Message}}"
            }
            """;

        string sDetails = BuildDetailsString();
        return $$"""
                    {
                        "code" : "{{sCode}}",
                        "message" : "{{Message}}",
                        "details" : [{{sDetails}}]
                    }
                    """;
    }

}
