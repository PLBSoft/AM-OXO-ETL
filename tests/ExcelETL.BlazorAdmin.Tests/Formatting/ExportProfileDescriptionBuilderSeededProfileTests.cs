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
                ("Colonne E « Désignation » : la désignation de l'équipement.", false),
                ("Colonne F « Tableaux » : les tableaux de l'équipement, séparés par une virgule.", false),
                ("Colonne G « PROGRESS » : « O » si l'équipement est rattaché à l'application « PROGRESS », sinon vide.", false),
                ("Colonnes H à AE : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide :", false), ("- H « VISITE PRÉALABLE CHANTIER »", false), ("- I « PROCÉDURE MAD »", false), ("- J « AUTORISATION DÉPLATINAGES »", false), ("- K « PROCÉDURE REL »", false), ("- L « AUTORISATION DE REMISE EN SERVICE »", false), ("- M « RÉCEPTION FINALE CHANTIER »", false), ("- N « POSE ÉTIQUETTES »", false), ("- O « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS »", false), ("- P « CONTRÔLE ETANCHÉITÉS »", false), ("- Q « DEBUT REL PLATINES/TAMPONS PLEINS »", false), ("- R « FIN REL PLATINES/TAMPONS PLEINS »", false), ("- S « AUTORISATION DE TRAVAUX »", false), ("- T « SOUPAPE : CONSTAT ENCRASSEMENT »", false), ("- U « PROLOCK VANNES »", false), ("- V « DEPROLOCK VANNES »", false), ("- W « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS »", false), ("- X « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES »", false), ("- Y « ZÉRO ENERGIE EN PRESENCE EE (PS941) »", false), ("- Z « PF : VALIDATION CONSTAT ENCRASSEMENT »", false), ("- AA « PF : ACCORD TRAVAUX FEU »", false), ("- AB « DEBUT MAD RÉCEPTION PLATINES/TAMPONS PLEINS »", false), ("- AC « FIN MAD RÉCEPTION PLATINES/TAMPONS PLEINS »", false), ("- AD « VALIDATION FIN DE TRAVAUX »", false), ("- AE « SYNCHRONISATION INSTRUMENTATION »", false),
            ]),
        ("Feuille Enfants",
            [
                ("Une ligne par élément, dans l'ordre des feuilles ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS.", true),
                ("Colonne A « Numéro » : le repère de l'élément.", false),
                ("Colonne B « Feuille » : le nom de la feuille du fichier source d'où vient la ligne.", false),
                ("Colonne C « Type Elément » : le type d'élément de l'élément.", false),
                ("Colonne D « Zone » : la zone de l'élément.", false),
                ("Colonne E « ELEMENT PARENT » : le repère de l'équipement.", false),
                ("Colonne F « Désignation » : la désignation de l'élément.", false),
                ("Colonne G « Position à la pose » : la position à la pose (vide hors feuille ISOLEMENT).", false),
                ("Colonne H « ETIQUETTE » : la couleur d'étiquette (vide si la feuille d'origine n'en fournit pas).", false),
                ("Colonne I « Tableaux » : les tableaux de l'élément, séparés par une virgule.", false),
                ("Colonne J « PROGRESS » : « O » si l'élément est rattaché à l'application « PROGRESS », sinon vide.", false),
                ("Colonnes K à Z : « X » si l'élément est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide :", false), ("- K « PROLOCK VANNES »", false), ("- L « DEPROLOCK VANNES »", false), ("- M « ZÉRO ENERGIE EN PRESENCE EE (PS941) »", false), ("- N « POSE ÉTIQUETTES »", false), ("- O « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS »", false), ("- P « CONTRÔLE ETANCHÉITÉS »", false), ("- Q « FIN MAD RÉCEPTION PLATINES/TAMPONS PLEINS »", false), ("- R « DEBUT REL PLATINES/TAMPONS PLEINS »", false), ("- S « FIN REL PLATINES/TAMPONS PLEINS »", false), ("- T « SYNCHRONISATION INSTRUMENTATION »", false), ("- U « SOUPAPE : CONSTAT ENCRASSEMENT »", false), ("- V « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS »", false), ("- W « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES »", false), ("- X « PF : VALIDATION CONSTAT ENCRASSEMENT »", false), ("- Y « PF : ACCORD TRAVAUX FEU »", false), ("- Z « DEBUT MAD RÉCEPTION PLATINES/TAMPONS PLEINS »", false),
            ]),
        ("Feuilles par type de tâche (règle « Tâches multiples »)",
            [
                ("Une feuille est créée par type de tâche présent dans le fichier importé, nommée d'après le code du type (« TM_PROC_MAD », « TM_PROC_REL »…), par ordre alphabétique. Le nom « Tâches multiples » n'apparaît pas dans le fichier.", true),
                ("Chaque feuille contient une ligne par tâche de ce type, titres de section compris, dans l'ordre de la feuille PROCEDURE.", true),
                ("Colonne A « GUID » : toujours vide.", false),
                ("Colonne B « TYPE TACHE » : le code du type de tâche.", false),
                ("Colonne C « Repère TM » : le repère de l'équipement.", false),
                ("Colonne D « ZONE » : la zone de l'équipement.", false),
                ("Colonne E « TYPE ELEMENT CODE » : le type d'élément de l'équipement.", false),
                ("Colonne F « Ligne » : le numéro de ligne de la tâche dans la feuille PROCEDURE.", false),
                ("Colonne G « Ordre » : l'ordre de la tâche (vide pour un titre de section).", false),
                ("Colonne H « Action » : l'action.", false),
                ("Colonne I « Acteur » : l'acteur.", false),
                ("Colonne J « Risques » : les risques.", false),
                ("Colonne K « Date de validation » : la date de validation, au format jj/mm/aaaa.", false),
                ("Colonne L « Colonne Travaux » : le libellé « colonne travaux » du type de tâche, défini dans le profil d'import.", false),
                ("Colonne M « CRITERE » : « A faire », ou « Pour info » pour un titre de section.", true),
                ("Colonne N « SUPPRESSION » : toujours « N ».", false),
            ]),
    ];

    [Fact]
    public void SeededDefaultExportProfile_ProducesExactlyTheValidatedCatalogue()
    {
        var description = Describe(SeededExportProfile());

        description.Sections.Select(s => s.Title).Should().Equal(ExpectedCatalogue.Select(e => e.Title));
        foreach (var (section, expected) in description.Sections.Zip(ExpectedCatalogue))
        {
            section.Sentences.SelectMany(s => s.Lines().Select(line => (line, s.IsFixed))).Should().Equal(expected.Sentences, $"section « {expected.Title} »");
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
