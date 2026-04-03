using Microsoft.AspNetCore.Authorization;
using Web_Project.Controllers;

namespace Web_Project.Tests.Admin;

public sealed class UserPortalAuthorizationTests
{
    [Theory]
    [InlineData(typeof(DashboardController))]
    [InlineData(typeof(ProfileController))]
    public void UserPortalControllers_RequireUserOnlyPolicy(Type controllerType)
    {
        var authorize = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("UserOnly", authorize!.Policy);
    }
}
