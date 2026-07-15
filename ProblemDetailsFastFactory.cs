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
        return Results.Problem(
            detail: message,
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.2"
        );
    }

    public IResult Forbidden(string message)
    {
        return Results.Problem(
            detail: message,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.4"
        );
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
        Results.Problem(
            detail: message,
            statusCode: (int)statusCode,
            title: title,
            type: $"https://tools.ietf.org/html/rfc9110#section-{rfcSection}"
        );
}
