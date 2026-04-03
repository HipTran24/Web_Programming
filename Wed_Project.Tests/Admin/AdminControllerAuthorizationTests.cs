using Microsoft.AspNetCore.Authorization;
using Web_Project.Controllers;

namespace Web_Project.Tests.Admin;

public sealed class AdminControllerAuthorizationTests
{
    [Fact]
    public void AdminController_RequiresAdminRole()
    {
        var authorize = typeof(AdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
    }
}
