using ANPT.Application.Models;
using ANPT.Application.Services;
using ANPT.Domain.Enums;

namespace ANPT.Tests.Application;

public class CurrentUserServiceTests
{
    [Fact]
    public void Initially_NotAuthenticated()
    {
        var svc = new CurrentUserService();
        Assert.False(svc.IsAuthenticated);
        Assert.Null(svc.UserId);
        Assert.Null(svc.Username);
        Assert.Null(svc.Role);
        Assert.Null(svc.User);
    }

    [Fact]
    public void SetUser_CreatesAuthenticatedSession()
    {
        var svc = new CurrentUserService();
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Username = "analyst1",
            DisplayName = "Analyst One",
            Role = UserRole.Analyst
        };

        svc.SetUser(user);

        Assert.True(svc.IsAuthenticated);
        Assert.Equal(user.UserId, svc.UserId);
        Assert.Equal("analyst1", svc.Username);
        Assert.Equal("Analyst One", svc.DisplayName);
        Assert.Equal(UserRole.Analyst, svc.Role);
        Assert.True(svc.User!.IsAnalyst);
        Assert.False(svc.User.IsAdmin);
    }

    [Fact]
    public void Clear_RemovesSession()
    {
        var svc = new CurrentUserService();
        svc.SetUser(new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin",
            Role = UserRole.Admin
        });

        svc.Clear();

        Assert.False(svc.IsAuthenticated);
        Assert.Null(svc.User);
        Assert.Null(svc.Username);
    }

    [Fact]
    public void Roles_AdminFlags()
    {
        var admin = new AuthenticatedUser { UserId = Guid.NewGuid(), Username = "a", DisplayName = "A", Role = UserRole.Admin };
        Assert.True(admin.IsAdmin);
        Assert.True(admin.IsAnalyst);
        Assert.True(admin.IsViewer);

        var analyst = new AuthenticatedUser { UserId = Guid.NewGuid(), Username = "b", DisplayName = "B", Role = UserRole.Analyst };
        Assert.False(analyst.IsAdmin);
        Assert.True(analyst.IsAnalyst);

        var viewer = new AuthenticatedUser { UserId = Guid.NewGuid(), Username = "c", DisplayName = "C", Role = UserRole.Viewer };
        Assert.False(viewer.IsAdmin);
        Assert.False(viewer.IsAnalyst);
        Assert.True(viewer.IsViewer);
    }
}
