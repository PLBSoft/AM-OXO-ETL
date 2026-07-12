using Bunit;
using ExcelETL.BlazorAdmin.Components.Account.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Account;

public class StatusMessageTests : BunitContext
{
    public StatusMessageTests() =>
        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext()));

    [Fact]
    public void StatusMessage_WithIsErrorTrue_UsesDangerStyle()
    {
        var cut = Render<StatusMessage>(p => p
            .Add(m => m.Message, "Tentative de connexion invalide.")
            .Add(m => m.IsError, true));

        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
    }

    [Fact]
    public void StatusMessage_WithIsErrorFalse_UsesSuccessStyle()
    {
        var cut = Render<StatusMessage>(p => p
            .Add(m => m.Message, "Opération réussie.")
            .Add(m => m.IsError, false));

        cut.Find(".alert").ClassList.Should().Contain("alert-success");
    }

    [Fact]
    public void StatusMessage_WithNoMessage_RendersNothing()
    {
        var cut = Render<StatusMessage>();

        cut.Markup.Should().BeEmpty();
    }
}

// Separate class: an in-circuit client-side navigation (e.g. clicking a NavLink to another page
// while already on an established interactive Server circuit) has no HttpContext to cascade --
// only the request that originally established the circuit does. Regression test for a
// NullReferenceException this previously caused (found while manually verifying Profile.razor,
// the first page reached this way that also renders StatusMessage).
public class StatusMessageWithoutHttpContextTests : BunitContext
{
    [Fact]
    public void StatusMessage_WithoutCascadingHttpContext_DoesNotThrow_AndDisplaysMessage()
    {
        var cut = Render<StatusMessage>(p => p
            .Add(m => m.Message, "Operation succeeded.")
            .Add(m => m.IsError, false));

        cut.Find(".alert").ClassList.Should().Contain("alert-success");
        cut.Markup.Should().Contain("Operation succeeded.");
    }
}
