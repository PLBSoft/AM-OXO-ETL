using System.Globalization;
using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Profile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): shared by the
// ImportProfileDescriptionBuilder*Tests files -- a real .resx-backed localizer, the fr-FR culture the
// catalogue is written in, and minimal hand-built rules/profiles.
internal static class DescriptionTestSupport
{
    private static readonly IStringLocalizer<BlazorAdminMessages> Localizer = new ServiceCollection()
        .AddLogging()
        .AddLocalization()
        .BuildServiceProvider()
        .GetRequiredService<IStringLocalizer<BlazorAdminMessages>>();

    public static ProfileDescription Describe(ImportProfile profile)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
        try
        {
            return ImportProfileDescriptionBuilder.Build(profile, Localizer);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    // Lot 079: same localizer and culture for the export description.
    public static ProfileDescription Describe(ExportProfile profile)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
        try
        {
            return ExportProfileDescriptionBuilder.Build(profile, Localizer);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    public static SheetGenerationRule ExportRule(
        string sheetName,
        PivotSource pivotSource,
        IReadOnlyList<ColumnDefinition>? columns = null,
        IReadOnlyList<PointColumnDefinition>? pointColumns = null,
        IReadOnlyList<ApplicationColumnDefinition>? applicationColumns = null,
        IReadOnlyList<ConstantColumnDefinition>? constantColumns = null) =>
        new(sheetName, pivotSource, columns ?? [], pointColumns ?? [], applicationColumns ?? [], constantColumns);

    public static ExportProfile ExportProfile(params SheetGenerationRule[] sheetRules) =>
        new("Profil d'export de test", sheetRules);

    public static IReadOnlyList<string> Texts(this ProfileDescriptionSection section) =>
        [.. section.Sentences.Select(s => s.Text)];

    public static ProfileDescriptionSection SheetSection(this ProfileDescription description, string sheetName) =>
        description.Sections.Single(s => s.Title == $"Feuille {sheetName}");

    public static SheetExtractionRule Rule(
        string sheetName,
        int firstBlockStartRow = 17,
        int step = 7,
        IReadOnlyList<BlockFieldDefinition>? fields = null,
        string? stopFieldName = null,
        IReadOnlyList<ConditionalPointRule>? pointRules = null,
        IReadOnlyList<string>? unconditionalColonneNames = null,
        IReadOnlyList<HeaderFieldRule>? headerFields = null,
        IReadOnlyList<HeaderCompositeRule>? headerComposites = null,
        string? zeroEnergieExpectedValue = null,
        IReadOnlyList<FieldPresencePointRule>? fieldPresencePointRules = null,
        BlockFieldDefinition? couleurEtiquetteCell = null,
        string? defaultCouleurEtiquette = null,
        IReadOnlyList<string>? allowedCouleursEtiquette = null,
        bool warnWhenNoConditionalPoint = false)
    {
        // Lot 084.1: a point rule may only read a field of its own block -- by default, declare one
        // block field per source field the rules read.
        fields ??=
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1),
            .. (pointRules ?? []).Select(r => r.SourceFieldName).Distinct()
                .Where(name => name != "Identification")
                .Select(name => new BlockFieldDefinition(name, "B:E", 3, 4))
        ];
        return new SheetExtractionRule(
            sheetName,
            new RepeatingBlockLocator(sheetName, firstBlockStartRow, step, stopFieldName ?? fields[0].Name, fields),
            pointRules ?? [],
            unconditionalColonneNames ?? [],
            headerFields ?? [],
            headerComposites ?? [],
            zeroEnergieExpectedValue,
            fieldPresencePointRules,
            couleurEtiquetteCell,
            defaultCouleurEtiquette,
            allowedCouleursEtiquette,
            warnWhenNoConditionalPoint);
    }

    public static ImportProfile Profile(
        IReadOnlyList<SheetExtractionRule>? sheetRules = null,
        string reperePrefix = "OXO-",
        string equipementTypeElementNom = "MAD TRAVAUX",
        IReadOnlyList<string>? defaultTableaux = null,
        IReadOnlyList<string>? defaultApplicationNames = null,
        IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null) =>
        new(
            "Profil de test", reperePrefix, equipementTypeElementNom,
            defaultTableaux ?? [], defaultApplicationNames ?? [],
            sheetRules ?? [Rule("MA FEUILLE")],
            tacheMultipleTypeLabels);
}
