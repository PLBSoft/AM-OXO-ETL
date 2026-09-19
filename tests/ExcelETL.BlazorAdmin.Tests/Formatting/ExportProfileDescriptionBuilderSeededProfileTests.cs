using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.9 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): freezes the
// catalogue of §5 on the seeded default export profile, as stored and read back. A difference is either a
// defect or a change of the seeded profile -- in the second case, update §5 of the ticket along with this list.
public class ExportProfileDescriptionBuilderSeededProfileTests
{
    private static readonly (string Title, (string Text, bool IsFixed)[] Sentences)[] ExpectedCatalogue =
    [
        ("Classeur généré",
            [
                ("Le fichier contient, dans cet ordre : la feuille « Parents », la feuille « Enfants » et une feuille par type de tâche (règle « Tâches multiples »).", false),
                ("Chaque feuille commence par une ligne de titres ; les données commencent à la ligne 2.", true),
                ("Les points, applications, tableaux et types de tâches viennent du profil d'import utilisé avec ce profil d'export.", false),
            ]),
        ("Feuille Parents",
            [
                ("Une seule ligne : l'équipement.", true),
                ("Colonne A « Repère » : le repère de l'équipement.", false),
                ("Colonne B « Feuille » : le nom de la feuille du fichier source d'où vient la ligne.", false),
                ("Colonne C « Type Elément » : le type d'élément de l'équipement.", false),
                ("Colonne D « Zone » : la zone de l'équipement.", false),
                ("Colonne E « LOC2 » : toujours vide.", false),
                ("Colonne F « LOC3 » : toujours vide.", false),
                ("Colonne G « Désignation » : la désignation de l'équipement.", false),
                ("Colonne H « FLUIDE » : toujours vide.", false),
                ("Colonne I « RECURRENT » : toujours vide.", false),
                ("Colonne J « Tableaux » : les tableaux de l'équipement, séparés par une virgule.", false),
                ("Colonne K « SUPPRESSION » : toujours vide.", false),
                ("Colonne L « ADR Email » : toujours vide.", false),
                ("Colonne M « COMMENTAIRES » : toujours vide.", false),
                ("Colonne N « PROGRESS » : « O » si l'équipement est rattaché à l'application « PROGRESS », sinon vide.", false),
                ("Colonnes O à AJ : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « VISITE PRÉALABLE CHANTIER » (O), « PROCÉDURE MAD » (P), « AUTORISATION DÉPLATINAGES » (Q), « PROCÉDURE REL » (R), « AUTORISATION DE REMISE EN SERVICE » (S), « RÉCEPTION FINALE CHANTIER » (T), « PROLOCK VANNES » (U), « DEPROLOCK VANNES » (V), « ZÉRO ENERGIE EN PRESENCE EE (PS941) » (W), « POSE ÉTIQUETTES » (X), « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS » (Y), « CONTRÔLE ETANCHÉITÉS » (Z), « RECEPTION DEBUT MAD » (AA), « RÉCEPTION PLATINES/TAMPONS PLEINS » (AB), « RECEPTION DEBUT REL » (AC), « PLATINES / TAMPONS PLEINS » (AD), « SYNCHRONISATION INSTRUMENTATION » (AE), « SOUPAPE : CONSTAT ENCRASSEMENT » (AF), « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS » (AG), « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES » (AH), « PF : VALIDATION CONSTAT ENCRASSEMENT » (AI), « PF : ACCORD TRAVAUX FEU » (AJ).", false),
            ]),
        ("Feuille Enfants",
            [
                ("Une ligne par élément, dans l'ordre des feuilles ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS.", true),
                ("Colonne A « Numéro » : le repère de l'élément.", false),
                ("Colonne B « Feuille » : le nom de la feuille du fichier source d'où vient la ligne.", false),
                ("Colonne C « Type Elément » : le type d'élément de l'élément.", false),
                ("Colonne D « Zone » : la zone de l'élément.", false),
                ("Colonne E « LOC2 » : toujours vide.", false),
                ("Colonne F « LOC3 » : toujours vide.", false),
                ("Colonne G « ELEMENT PARENT » : le repère de l'équipement.", false),
                ("Colonne H « Désignation » : la désignation de l'élément.", false),
                ("Colonne I « Position à la pose » : la position à la pose (vide hors feuille ISOLEMENT).", false),
                ("Colonne J « POSITION A LA DEPOSE » : toujours vide.", false),
                ("Colonne K « PHASE PROCESS » : toujours vide.", false),
                ("Colonne L « REMARQUES » : toujours vide.", false),
                ("Colonne M « ETIQUETTE » : la couleur d'étiquette (vide si la feuille d'origine n'en fournit pas).", false),
                ("Colonne N « DIAMETRE INCH » : toujours vide.", false),
                ("Colonne O « SERIE LBS » : toujours vide.", false),
                ("Colonne P « NATURE JOINT » : toujours vide.", false),
                ("Colonne Q « BESOIN ECHAF » : toujours vide.", false),
                ("Colonne R « Tableaux » : les tableaux de l'élément, séparés par une virgule.", false),
                ("Colonne S « SUPPRESSION » : toujours vide.", false),
                ("Colonne T « PROGRESS » : « O » si l'élément est rattaché à l'application « PROGRESS », sinon vide.", false),
                ("Colonnes U à AJ : « X » si l'élément est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « PROLOCK VANNES » (U), « DEPROLOCK VANNES » (V), « ZÉRO ENERGIE EN PRESENCE EE (PS941) » (W), « POSE ÉTIQUETTES » (X), « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS » (Y), « CONTRÔLE ETANCHÉITÉS » (Z), « RECEPTION DEBUT MAD » (AA), « RÉCEPTION PLATINES/TAMPONS PLEINS » (AB), « RECEPTION DEBUT REL » (AC), « PLATINES / TAMPONS PLEINS » (AD), « SYNCHRONISATION INSTRUMENTATION » (AE), « SOUPAPE : CONSTAT ENCRASSEMENT » (AF), « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS » (AG), « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES » (AH), « PF : VALIDATION CONSTAT ENCRASSEMENT » (AI), « PF : ACCORD TRAVAUX FEU » (AJ).", false),
            ]),
        ("Feuilles par type de tâche (règle « Tâches multiples »)",
            [
                ("Une feuille est créée par type de tâche présent dans le fichier importé, nommée d'après le code du type (« TM_PROC_MAD », « TM_PROC_REL »…), par ordre alphabétique. Le nom « Tâches multiples » n'apparaît pas dans le fichier.", true),
                ("Chaque feuille contient une ligne par tâche de ce type, titres de section compris, dans l'ordre de la feuille PROCEDURE.", true),
                ("Colonne A « GUID » : toujours vide.", false),
                ("Colonne B « TYPE TACHE » : le code du type de tâche.", false),
                ("Colonne C « Repère TM » : le repère de l'équipement.", false),
                ("Colonne D « ZONE » : la zone de l'équipement.", false),
                ("Colonne E « LOC2 » : toujours vide.", false),
                ("Colonne F « LOC3 » : toujours vide.", false),
                ("Colonne G « TYPE ELEMENT CODE » : le type d'élément de l'équipement.", false),
                ("Colonne H « LOT » : toujours vide.", false),
                ("Colonne I « Ressource » : toujours vide.", false),
                ("Colonne J « Ligne » : le numéro de ligne de la tâche dans la feuille PROCEDURE.", false),
                ("Colonne K « Ordre » : l'ordre de la tâche (vide pour un titre de section).", false),
                ("Colonne L « Action » : l'action.", false),
                ("Colonne M « Acteur » : l'acteur.", false),
                ("Colonne N « Risques » : les risques.", false),
                ("Colonne O « Date de validation » : la date de validation, au format jj/mm/aaaa.", false),
                ("Colonne P « Colonne Travaux » : le libellé « colonne travaux » du type de tâche, défini dans le profil d'import.", false),
                ("Colonne Q « CRITERE » : « A faire », ou « Pour info » pour un titre de section.", true),
                ("Colonne R « SUPPRESSION » : toujours « N ».", false),
            ]),
    ];

    [Fact]
    public void SeededDefaultExportProfile_ProducesExactlyTheValidatedCatalogue()
    {
        var description = Describe(SeededExportProfile());

        description.Sections.Select(s => s.Title).Should().Equal(ExpectedCatalogue.Select(e => e.Title));
        foreach (var (section, expected) in description.Sections.Zip(ExpectedCatalogue))
        {
            section.Sentences.Select(s => (s.Text, s.IsFixed)).Should().Equal(expected.Sentences, $"section « {expected.Title} »");
            section.Ignored.Should().BeEmpty();
            section.Blocking.Should().BeEmpty();
        }
    }

    private static ExportProfile SeededExportProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileDescriptionBuilderSeededProfileTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId).GetAwaiter().GetResult()!;
    }
}
