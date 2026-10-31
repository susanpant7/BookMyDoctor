using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Users;

// Patients, managed by the admin.
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<User>> GetAll() => Ok(_userService.GetAll());

    [HttpPost]
    public ActionResult<User> Create(CreateUserRequest request) => Ok(_userService.Create(request));

    [HttpPost("load-defaults")]
    public ActionResult<LoadDefaultsResult> LoadDefaults(LoadDefaultUsersRequest request) =>
        Ok(_userService.LoadDefaults(request.Password));

    [HttpPut("{id:guid}")]
    public ActionResult<User> Update(Guid id, UpdateUserRequest request) => Ok(_userService.Update(id, request));

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        _userService.Delete(id);
        return NoContent();
    }
}
