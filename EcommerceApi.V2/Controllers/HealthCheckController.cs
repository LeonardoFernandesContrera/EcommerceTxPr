using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers
{
    [ApiController]
    [Route("HealthCheck")]
    public class HealthCheckController : Controller
    {
        [HttpGet]
        public IActionResult HealthCheck()
        {
            return Ok("Sistema esta funcionando");
        }
    }
}
