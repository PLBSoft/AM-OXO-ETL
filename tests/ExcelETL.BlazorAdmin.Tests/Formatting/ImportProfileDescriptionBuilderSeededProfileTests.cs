using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.11 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): freezes the
// catalogue of §5 on the seeded default profile, as stored and read back. A difference is either a defect
// or a change of the seeded profile -- in the second case, update §5 of the ticket along with this list.
public class ImportProfileDescriptionBuilderSeededProfileTests
{
    private static readonly (string Title, (string Text, bool IsFixed)[] Sentences)[] ExpectedCatalogue =
    [
        ("Paramètres généraux",
            [
                ("Le repère de l'équipement est lu dans la feuille PROCEDURE. Il doit commencer par « OXO- » (majuscules comprises), qui est retiré ; sinon le fichier entier est refusé.", false),
                ("L'équipement est créé avec le type d'élément « MAD TRAVAUX ».", false),
                ("L'équipement est coché dans les 3 colonnes « TRAVAUX COMPLET », « TRAVAUX DETAIL », « VISITE PRÉALABLE CHANTIER ». L'équipement et tous ses éléments sont rattachés à ces 3 tableaux.", false),
                ("L'équipement et tous ses éléments sont rattachés à l'application « PROGRESS ».", false),
                ("Une tâche de type « TM_PROC_MAD » est écrite dans la colonne travaux « Procédure MAD ».", false),
                ("Une tâche de type « TM_PROC_REL » est écrite dans la colonne travaux « Procédure REL ».", false),
            ]),
        ("Feuille PROCEDURE",
            [
                ("En-tête : le repère de l'équipement (« nomMAD ») est lu en M2:O2, préfixe « OXO- » retiré.", false),
                ("En-tête : la révision (« revision ») est lue en P2:Q2.", false),
                ("En-tête : la date de révision (« dateRev ») est lue en R2:T2, au format « dd/MM/yyyy ».", false),
                ("La désignation de l'équipement suit le modèle « Rév {revision} du {dateRev} », où {revision} et {dateRev} sont remplacés par les valeurs lues ci-dessus.", false),
                ("Une tâche est lue par ligne à partir de la ligne 9. La lecture s'arrête à la première ligne dont l'action est vide.", false),
                ("Pour la première tâche : action en C9:L9, ordre en B9, acteur en M9:N9, risques en O9:Q9, type en R9, date de validation en T9:U9.", false),
                ("Une date de révision illisible fait refuser le fichier entier.", true),
                ("Un type « MAD » devient « TM_PROC_MAD », un type « REL » devient « TM_PROC_REL ».", true),
                ("Une ligne sans ordre est un titre de section, pas une tâche à réaliser.", true),
            ]),
        ("Feuille ISOLEMENT",
            [
                ("Un élément est lu toutes les 7 lignes à partir de la ligne 19. La lecture s'arrête au premier bloc dont l'identifiant est vide.", false),
                ("Pour le premier élément : identifiant en B19:E20, désignation en H18:U19, position à la pose en H20:O21, type d'élément en B22:E23, indicateur zéro énergie en V18:V19.", false),
                ("Le repère de l'élément est la cellule K6:T6, un tiret, puis l'identifiant.", true),
                ("Chaque élément est coché dans les 2 colonnes « PROLOCK VANNES », « DEPROLOCK VANNES ».", false),
                ("Si l'indicateur zéro énergie contient « ZERO ENERGIE », l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) ». Toute autre valeur non vide donne un avertissement.", false),
                ("Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.", false),
            ]),
        ("Feuille PLATINES",
            [
                ("Un élément est lu toutes les 8 lignes à partir de la ligne 17. La lecture s'arrête au premier bloc dont l'identifiant est vide.", false),
                ("Pour le premier élément : identifiant en B17:E18, désignation en H16:V17, type d'élément en B20:E22.", false),
                ("Le repère de l'élément est la cellule K6:U6, un tiret, puis l'identifiant.", true),
                ("Chaque élément est coché dans les 5 colonnes « POSE ÉTIQUETTES », « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS », « RÉCEPTION PLATINES/TAMPONS PLEINS », « PLATINES / TAMPONS PLEINS ».", false),
                ("Si la cellule H19:N19 ou H20:N20 contient « DEBUT MAD », l'élément est coché dans la colonne « RECEPTION DEBUT MAD ».", false),
                ("Si la cellule H19:N19 ou H20:N20 contient « DEBUT REL », l'élément est coché dans la colonne « RECEPTION DEBUT REL ».", false),
                ("La couleur d'étiquette est lue en H18:N18. Couleurs acceptées : « ROUGE », « BLANC », « JAUNE », « VERT », « BLEUE ». Une autre valeur est ignorée, avec un avertissement.", false),
            ]),
        ("Feuille ORIFICES CAPACITES",
            [
                ("Un élément est lu toutes les 8 lignes à partir de la ligne 17. La lecture s'arrête au premier bloc dont l'identifiant est vide.", false),
                ("Pour le premier élément : identifiant en B17:E18, désignation en H16:V17, type d'élément en B20:E22.", false),
                ("Le repère de l'élément est la cellule K6:U6, un tiret, puis l'identifiant.", true),
                ("Chaque élément est coché dans les 4 colonnes « POSE ÉTIQUETTES », « RÉCEPTION PLATINES/TAMPONS PLEINS », « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS ».", false),
                ("La couleur d'étiquette est lue en H18:N18. Couleurs acceptées : « ROUGE », « BLANC ». Une autre valeur est ignorée, avec un avertissement.", false),
            ]),
        ("Feuille AUTRES JOINTS TOUCHES",
            [
                ("En-tête : le repère de l'équipement (« repereEcho ») est lu en N6. Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.", false),
                ("Un élément est lu toutes les 7 lignes à partir de la ligne 17. La lecture s'arrête au premier bloc dont l'identifiant est vide.", false),
                ("Pour le premier élément : identifiant en B17:E18, désignation en F16:Y17, type d'élément en B20:E21.", false),
                ("Chaque élément est coché dans les 2 colonnes « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS ».", false),
                ("Si le type d'élément n'est pas « TUBING », l'élément est coché dans la colonne « POSE ÉTIQUETTES ».", false),
                ("Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.", false),
                ("La couleur d'étiquette de chaque élément est toujours « BLEUE ».", false),
            ]),
        ("Feuille DIVERS",
            [
                ("En-tête : le repère de l'équipement (« repereEcho ») est lu en N6. Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.", false),
                ("Un élément est lu toutes les 3 lignes à partir de la ligne 9. La lecture s'arrête au premier bloc dont l'identifiant est vide.", false),
                ("Pour le premier élément : type d'élément en B9:G11, identifiant en H9:K11, désignation en L9:V11.", false),
                ("La zone lue en B6:E6 est appliquée à l'équipement et à tous les éléments du fichier.", true),
                ("Si le type d'élément est « INSTRUMENTATION », l'élément est coché dans la colonne « SYNCHRONISATION INSTRUMENTATION ».", false),
                ("Si le type d'élément est « ZERO ENERGIE », l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) ».", false),
                ("Si le type d'élément est « SOUPAPE », l'élément est coché dans les 2 colonnes « SOUPAPE : CONSTAT ENCRASSEMENT », « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS ».", false),
                ("Si le type d'élément est « POINT DE FEU », l'élément est coché dans les 3 colonnes « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES », « PF : VALIDATION CONSTAT ENCRASSEMENT », « PF : ACCORD TRAVAUX FEU ».", false),
                ("Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.", false),
            ]),
    ];

    [Fact]
    public void SeededDefaultProfile_ProducesExactlyTheValidatedCatalogue()
    {
        var description = Describe(LoadSeededDefaultProfile());

        description.Sections.Select(s => s.Title).Should().Equal(ExpectedCatalogue.Select(e => e.Title));
        foreach (var (section, expected) in description.Sections.Zip(ExpectedCatalogue))
        {
            section.Sentences.Select(s => (s.Text, s.IsFixed)).Should().Equal(expected.Sentences, $"section « {expected.Title} »");
            section.Ignored.Should().BeEmpty();
            section.Blocking.Should().BeEmpty();
        }
    }

    private static ImportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileDescriptionBuilderSeededProfileTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId).GetAwaiter().GetResult()!;
    }
}
