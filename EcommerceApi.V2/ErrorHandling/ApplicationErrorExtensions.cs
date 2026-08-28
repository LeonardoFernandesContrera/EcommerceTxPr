using EcommerceTxPr.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.ErrorHandling
{
    public static class ApplicationErrorExtensions
    {
        public static ObjectResult ToProblemDetails(
            this ControllerBase controller,
            Error error)
        {
            var (statusCode, title) = error.Type switch
            {
                ErrorType.Validation => (
                    StatusCodes.Status400BadRequest,
                    "Validation error"),
                ErrorType.NotFound => (
                    StatusCodes.Status404NotFound,
                    "Resource not found"),
                ErrorType.Conflict => (
                    StatusCodes.Status409Conflict,
                    "Conflict"),
                ErrorType.Unavailable => (
                    StatusCodes.Status503ServiceUnavailable,
                    "Service unavailable"),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error")
            };

            var result = controller.Problem(
                detail: error.Message,
                instance: controller.HttpContext.Request.Path,
                statusCode: statusCode,
                title: title);

            if (result.Value is ProblemDetails problemDetails)
            {
                problemDetails.Extensions["code"] = error.Code;
            }

            return result;
        }
    }
}
