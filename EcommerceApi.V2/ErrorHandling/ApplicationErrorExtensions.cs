using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.ErrorHandling
{
    public static class ApplicationErrorExtensions
    {
        public static ObjectResult ToProblemDetails(
            this ControllerBase controller,
            Error error)
        {
            if (error.Code == CustomerErrors.NotFound.Code)
            {
                var result = controller.Problem(
                    detail: error.Message,
                    instance: controller.HttpContext.Request.Path,
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Customer not found");

                if (result.Value is ProblemDetails problemDetails)
                {
                    problemDetails.Extensions["code"] = error.Code;
                }

                return result;
            }

            return controller.Problem(
                detail: "An unexpected error occurred while processing the request.",
                instance: controller.HttpContext.Request.Path,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }
}
