using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using SwiftDeploy.Services;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
            private readonly MongoDbService _mongo;
            public UserController(MongoDbService mongo)
            {
                _mongo = mongo;
            }

            [HttpPost]
            public IActionResult CreateUser(User user)
            {
                _mongo.Users.InsertOne(user);
                return Ok("User created");
            }
    }

}

