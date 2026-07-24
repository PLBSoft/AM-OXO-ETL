using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Account;
using ExcelETL.BlazorAdmin.Components.Account.Pages;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.BlazorAdmin.Tests.Account;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 030 (30.6): generic structural guard-rail over every form-floating field in the app, rather
// than one hand-picked field per page (the original bug -- ExportProfileEditor's "Colonne name"
// field -- already satisfied both checks below individually; the real cause turned out to be
// app.css making the placeholder visible instead of Bootstrap's default transparent, see the
// app.css comment removed in this same lot). This test still guards the two DOM-level requirements
// bUnit *can* verify (it doesn't compute real layout/CSS) so a future field added anywhere in the
// audited pages can't silently violate them again.
internal static class FormFloatingStructureAssertions
{
    public static void AssertAllFormFloatingFieldsAreStructurallyValid<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        var containers = cut.FindAll("div.form-floating");
        containers.Should().NotBeEmpty(
            "this page/state is expected to render at least one form-floating field for this audit to be meaningful");

        foreach (var container in containers)
        {
            var children = container.Children.ToList();
            var control = children.FirstOrDefault(e => e.TagName is "INPUT" or "SELECT");
            var label = children.FirstOrDefault(e => e.TagName == "LABEL");

            control.Should().NotBeNull($"form-floating container '{container.OuterHtml}' must wrap an input or select");
            label.Should().NotBeNull($"form-floating container '{container.OuterHtml}' must have an associated label");

            children.IndexOf(control!).Should().BeLessThan(
                children.IndexOf(label!),
                $"the input/select #{control!.GetAttribute("id")} must precede its label in the DOM for form-floating's CSS to work");

            // Bootstrap's :placeholder-shown mechanics only apply to <input>/<textarea> -- a
            // <select> is always rendered in the floated position regardless of a placeholder
            // attribute (which <select> doesn't even support), so it's exempt from this check.
            if (control.TagName == "INPUT")
            {
                control.GetAttribute("placeholder").Should().NotBeNullOrEmpty(
                    $"input #{control.GetAttribute("id")} needs a non-empty placeholder for form-floating's :placeholder-shown mechanics");
            }
        }
    }
}

public class ImportProfileEditorFormFloatingAuditTests : BunitContext
{
    public ImportProfileEditorFormFloatingAuditTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorFormFloatingAuditTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void NewProfileForm_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });

    [Fact]
    public void SheetRuleAndBlockFieldEditMode_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("7");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();
        cut.Find("#add-sheet-rule-button").Click();

        // Enter edit mode for the sheet rule (renders the edit-0- prefixed SheetRuleForm instance)
        // and, inside it, for the one block field it holds (renders BlockFieldForm's own edit-mode
        // instance too) -- both must still satisfy the same structural rules under a prefix.
        cut.Find("#modify-sheet-rule-button-0").Click();
        cut.Find("#edit-0-modify-block-field-button-0").Click();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });
}

public class ExportProfileEditorFormFloatingAuditTests : BunitContext
{
    public ExportProfileEditorFormFloatingAuditTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorFormFloatingAuditTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void NewProfileForm_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });

    [Fact]
    public void SheetRuleAndColumnEditMode_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-name-input").Change("Parents");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));
        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();
        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();
        cut.Find("#application-column-nom-input").Change("PROGRESS");
        cut.Find("#application-column-header-input").Change("PROGRESS");
        cut.Find("#add-application-column-definition-button").Click();
        cut.Find("#add-sheet-generation-rule-button").Click();

        // Enter edit mode for the sheet rule and, inside it, for the one column it holds -- both
        // must still satisfy the same structural rules under a prefix.
        cut.Find("#modify-sheet-generation-rule-button-0").Click();
        cut.Find("#edit-0-modify-column-definition-button-0").Click();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });
}

public class ProfileFormFloatingAuditTests : BunitContext
{
    public ProfileFormFloatingAuditTests()
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(r => r.GetByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile("user-1", "alice@example.com", "alice@example.com", "Alice", "Smith"));

        Services.AddSingleton(userRepositoryMock.Object);
        Services.AddLocalization();

        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("alice@example.com");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext()));
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void ProfilePage_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });
}

public class LoginFormFloatingAuditTests : BunitContext
{
    public LoginFormFloatingAuditTests()
    {
        var userManagerMock = IdentityMocks.CreateUserManagerMock();
        var signInManagerMock = IdentityMocks.CreateSignInManagerMock(userManagerMock.Object);

        Services.AddSingleton(signInManagerMock.Object);
        Services.AddSingleton<ILogger<Login>>(NullLogger<Login>.Instance);
        Services.AddScoped<IdentityRedirectManager>();
        Services.AddLocalization();

        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext()));
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void LoginPage_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<Login>();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });
}

public class RegisterFormFloatingAuditTests : BunitContext
{
    public RegisterFormFloatingAuditTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        userStoreMock
            .Setup(s => s.SetUserNameAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userStoreMock
            .As<IUserEmailStore<ApplicationUser>>()
            .Setup(s => s.SetEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userManagerMock = IdentityMocks.CreateUserManagerMock(userStoreMock.Object);
        var signInManagerMock = IdentityMocks.CreateSignInManagerMock(userManagerMock.Object);

        Services.AddSingleton(userManagerMock.Object);
        Services.AddSingleton(userStoreMock.Object);
        Services.AddSingleton(signInManagerMock.Object);
        Services.AddSingleton<ILogger<Register>>(NullLogger<Register>.Instance);
        Services.AddScoped<IdentityRedirectManager>();
        Services.AddLocalization();

        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext()));
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void RegisterPage_AllFormFloatingFields_AreStructurallyValid() => WithCulture("en-US", () =>
    {
        var cut = Render<Register>();

        FormFloatingStructureAssertions.AssertAllFormFloatingFieldsAreStructurallyValid(cut);
    });
}
