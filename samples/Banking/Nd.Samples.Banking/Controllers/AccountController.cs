using Microsoft.AspNetCore.Mvc;

namespace Nd.Samples.Banking.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        public AccountController()
        {
        }

        [HttpPost]
        public IActionResult CreateAccount()
        {
            return Ok(CreateAccount());
        }
    }
}
