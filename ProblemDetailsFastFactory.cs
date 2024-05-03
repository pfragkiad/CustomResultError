using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CustomResultError;

//package for ProblemDetails: Microsoft.AspNetCore.Mvc
public class ProblemDetailsFastFactory
{
    public IResult BadRequest(string message)
    {
        return Results.BadRequest(new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Bad Request",
            Detail = message,
            Status = StatusCodes.Status400BadRequest
        });
    }
    public IResult Unauthorized(string message)
    {
        return Results.BadRequest(new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            Title = "Unauthorized",
            Detail = message,
            Status = StatusCodes.Status401Unauthorized
        });
    }

    public IResult Forbidden(string message)
    {
        return Results.NotFound(new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            Title = "Forbidden",
            Detail = message,
            Status = StatusCodes.Status403Forbidden
        });
    }

    public IResult NotFound(string message)
    {
        return Results.NotFound(new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Title = "Not Found",
            Detail = message,
            Status =  StatusCodes.Status404NotFound
        });
    }


    public IResult Create(string message, HttpStatusCode statusCode, string title, string rfcSection) =>
        Results.BadRequest(new ProblemDetails
        {
            Type = $"https://tools.ietf.org/html/rfc9110#section-{rfcSection}",
            Title = title,
            Detail = message,
            Status = (int)statusCode
        });
}
