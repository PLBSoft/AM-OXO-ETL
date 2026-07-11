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
