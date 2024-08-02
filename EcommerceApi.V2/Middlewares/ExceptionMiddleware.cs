using System.Net;
using System.Text.Json;

namespace EcommerceApi.Middlewares
{
    public class ExceptionMiddleware 
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception e)
            {

                var exceptionDetails = new
                {
                    Type = "Internal Error",
                    Status = HttpStatusCode.InternalServerError,
                };

                await HandleError(context, HttpStatusCode.InternalServerError, exceptionDetails);
            }
        }

        private async Task HandleError(HttpContext httpContext, HttpStatusCode httpStatusCode, object exceptionDetails)
        {
            httpContext.Response.StatusCode = (int)httpStatusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(exceptionDetails));
        }

    }
}
