using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CustomResultError;
public enum ValidatorLogTypeIfError
{
    Error,
    Warning,
    Critical
}
public static class Validator
{
    public static Error<CodeType> Fail<CodeType>(ILogger logger, Error<CodeType> error,
        ValidatorLogTypeIfError logTypeIfError = ValidatorLogTypeIfError.Error)
    {
        switch (logTypeIfError)
        {
            case ValidatorLogTypeIfError.Error:
                logger?.LogError(error.Message);
                break;
            case ValidatorLogTypeIfError.Warning:
                logger?.LogWarning(error.Message);
                break;
            case ValidatorLogTypeIfError.Critical:
                logger?.LogCritical(error.Message);
                break;
        }
        return error;
    }

    public static Error<CodeType> Fail<CodeType>(
       ILogger? logger,
       CodeType errorCode,
       string errorMessageTemplate,
       ValidatorLogTypeIfError logTypeIfError = ValidatorLogTypeIfError.Error,
       params object?[] messageArgs)
    {
        switch (logTypeIfError)
        {
            case ValidatorLogTypeIfError.Error:
                logger?.LogError(errorMessageTemplate, messageArgs);
                break;
            case ValidatorLogTypeIfError.Warning:
                logger?.LogWarning(errorMessageTemplate, messageArgs);
                break;
            case ValidatorLogTypeIfError.Critical:
                logger?.LogCritical(errorMessageTemplate, messageArgs);
                break;
        }
        return new Error<CodeType>(message: GetFormattedString(errorMessageTemplate, messageArgs), code: errorCode);
    }

    public static Error<CodeType> Fail<CodeType>(
   ILogger? logger,
   CodeType errorCode,
   string errorMessageTemplate,
   params object?[] messageArgs)
    {
        logger?.LogError(errorMessageTemplate, messageArgs);
        return new Error<CodeType>(message: GetFormattedString(errorMessageTemplate, messageArgs), code: errorCode);
    }



    public static Error<CodeType>? Validate<TValue, CodeType>(
        TValue value,
        Func<TValue, bool> validateFunction,
        ILogger? logger,
        CodeType errorCode,
        string errorMessageTemplate,
        ValidatorLogTypeIfError logTypeIfError = ValidatorLogTypeIfError.Error,
        params object?[] messageArgs)
    {
        if (!validateFunction(value))
        {
            switch (logTypeIfError)
            {
                case ValidatorLogTypeIfError.Error:
                    logger?.LogError(errorMessageTemplate, messageArgs);
                    break;
                case ValidatorLogTypeIfError.Warning:
                    logger?.LogWarning(errorMessageTemplate, messageArgs);
                    break;
                case ValidatorLogTypeIfError.Critical:
                    logger?.LogCritical(errorMessageTemplate, messageArgs);
                    break;
            }

            return new Error<CodeType>(message: GetFormattedString(errorMessageTemplate, messageArgs), code: errorCode);
        }
        return null;
    }

    public static Error<CodeType>? Validate<TValue, CodeType>(
        TValue value,
        Func<TValue, bool> validateFunction,
        ILogger? logger,
        CodeType errorCode,
        string errorMessageTemplate,
        params object?[] messageArgs)
    {
        if (!validateFunction(value))
        {
            logger?.LogError(errorMessageTemplate, messageArgs);
            return new Error<CodeType>(message: GetFormattedString(errorMessageTemplate, messageArgs), code: errorCode);
        }
        return null;
    }



    //TODO: Document GetFormattedString and ProblemDetailsFastFactory

    //
    /// <summary>
    /// Converts named formatted strings to numbered. E.g. "{sp} for the {ve} must not be negative." -> "{0} for the {1} must not be negative."
    /// </summary>
    /// <param name="messageTemplate"></param>
    /// <returns></returns>
    private static string GetFormattedString(string messageTemplate)
    {
        //var matches = Regex.Matches(messageTemplate, @"\{(\w+)\}");
        //var matches = FormattedStringRegex().Matches(messageTemplate);
        MatchCollection matches = _formattedStringRegex.Matches(messageTemplate);

        string formattedString = messageTemplate;
        int i = 0;
        foreach (Match match in matches.Cast<Match>())
            formattedString = formattedString.Replace(match.Value, $"{{{i++}}}");

        return formattedString;
    }

    static Regex _formattedStringRegex;
    static Validator()
    {
        _formattedStringRegex = new
            (@"\{(\w+)\}", RegexOptions.Compiled);
    }

    public static string GetFormattedString(string messageTemplate, params object?[] messageArgs) =>
        string.Format(GetFormattedString(messageTemplate), messageArgs);

    //[GeneratedRegex(@"\{(\w+)\}")]
    //private static partial Regex FormattedStringRegex();


    public static async Task WriteErrorAsync<CodeType>(this HttpResponse response, Error<CodeType> error)
    {
        response.StatusCode = (int)HttpStatusCode.BadRequest;
        await response.WriteAsync(error.ToJsonString());
    }

}
