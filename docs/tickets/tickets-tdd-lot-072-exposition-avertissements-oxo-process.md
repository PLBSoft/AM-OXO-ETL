# Lot 072 — Exposition des avertissements non bloquants sur `POST /api/oxo/process`

## Contexte

Constat du 2026-09-12 (Simon) : lorsque l'app legacy envoie un fichier à `POST /api/oxo/process`
et que le traitement **réussit** (fichier cible généré, HTTP 200), les avertissements non
bloquants produits pendant l'extraction (`ImportResult.Errors` — ex. Lot 055 : « aucun Point
conditionnel créé pour la valeur X », Lot 032 : incohérence de TYPE dans les tâches multiples)
ne sont exposés nulle part dans la réponse HTTP. Confirmé en lisant `OxoController.Process`
(`src/ExcelETL.WebAPI/Controllers/OxoController.cs:104-129`) : le corps `ProblemDetails.Extensions
["errors"]` n'existe que sur le chemin de **rejet** (422, `Equipement is null`) ; le chemin de
succès ne renvoie qu'un `FileStreamResult` binaire, sans aucune métadonnée.

Déjà noté comme hors périmètre au Lot 055 (« exposing warnings in `POST /api/oxo/process`'s HTTP
response ... a legitimate separate lot, not touched here ») — c'est ce lot.

## Décision d'architecture (industrie : métadonnées attachées à une réponse binaire)

Un endpoint qui renvoie un fichier binaire en corps de réponse ne peut pas y attacher une
structure JSON en plus sans casser le contrat existant (le corps doit rester le flux binaire pur,
legacy le lit directement comme fichier à sauvegarder). La pratique standard pour ce cas précis
(cf. S3 `x-amz-*`, GitHub `Link` header, Azure `x-ms-*`) est :

1. **Des en-têtes HTTP légers** pour un indicateur immédiat, sans round-trip supplémentaire — ici
   `X-Warning-Count` (nombre) systématiquement présent, `X-Generated-File-Id` (GUID) quand
   l'archivage a réussi.
2. **Un lien vers une ressource de détail** déjà existante pour le contenu structuré complet — ici
   `GET /api/generated-files/{id}`, qui existe depuis le Lot 034 et sert déjà de mécanisme de
   corrélation (l'archive). Pas de nouveau mécanisme de corrélation à inventer : celui-ci est déjà
   en place et déjà utilisé par legacy (`/OXO/History`).

Ce choix reste cohérent avec la préférence KISS déjà exprimée par le client sur ce projet (Lot
065 : « no correlation ID, no a posteriori query — KISS ») : on ne crée rien de nouveau, on
réutilise l'archive existante et on y accroche la donnée qui manquait (`ImportResult.Errors`
n'était jusqu'ici jamais persisté sur `GeneratedFileRecord`, seul un statut agrégé
`Success`/`NonBlockingWarning`/`Rejected` l'était).

Sur le chemin de **rejet** (422), le corps expose déjà les erreurs inline (`Extensions["errors"]`)
— comportement conservé à l'identique, avec un ajout mineur : `ExtractedValue` (déjà porté par
`ExtractionError` depuis le Lot 055 mais jamais exposé dans ce corps) y est ajouté, additif, non
cassant. Les deux mêmes en-têtes (`X-Generated-File-Id`/`X-Warning-Count`) sont posés aussi sur ce
chemin, pour que legacy ait un mécanisme unique et systématique quel que soit le statut.

## Portée

- `GeneratedFileRecord` (Domain) apprend à porter la liste des avertissements associés à
  l'archive (nouveau type `GeneratedFileWarning`, snapshot minimal de `ExtractionError`).
- `ProcessOxoFileService` persiste cette liste à l'archivage (déjà systématique, succès comme
  rejet — Lot 034) et retourne l'identifiant de l'enregistrement archivé (`ProcessOxoFileResult`).
- `OxoController` pose les 2 en-têtes sur les 2 chemins (200 et 422), et enrichit le corps 422
  existant avec `ExtractedValue`.
- `GET /api/generated-files`/`GET /api/generated-files/{id}` exposent désormais `WarningCount` et
  `Warnings` (même forme que le corps 422).
- `/generated-files` (BlazorAdmin) affiche le compte d'avertissements par ligne, avec un détail
  repliable (même mécanisme `<details>` que partout ailleurs dans ce projet) — pour que
  l'information soit aussi visible côté admin, pas seulement côté M2M.

## Étapes TDD

### 072.1 — `GeneratedFileWarning` (Domain)

Nouveau `src/ExcelETL.Domain/Archiving/GeneratedFileWarning.cs` :
`sealed record GeneratedFileWarning(string Sheet, string BlockIdentifier, string Code, string
Message, string? ExtractedValue = null)`. Simple snapshot, sans validation propre : construit
uniquement en interne à partir d'`ExtractionError` déjà validé (même raisonnement que
`GeneratedFileRecord` lui-même — pas une frontière utilisateur).

### 072.2 — `GeneratedFileRecord.Warnings` (Domain)

`GeneratedFileRecord` gagne `IReadOnlyList<GeneratedFileWarning> Warnings` (backing field privé
mutable + constructeur param optionnel en dernière position, `warnings = null` → `[]`, copié —
même motif que `PointRules`/`SheetRules` ailleurs dans ce projet). **Nécessite un constructeur
privé sans paramètre** pour la matérialisation EF Core : `Warnings` sera une navigation de
collection owned, et EF ne peut pas lier un paramètre de constructeur à une navigation de
collection (cf. note Lot E2/047 dans CLAUDE.md) — sans ce constructeur privé, EF échoue à
matérialiser tout l'agrégat dès que ce paramètre existe, pas seulement `Warnings`.

Tests (`GeneratedFileRecordTests`) : sans avertissement → liste vide (jamais `null`) ; avec des
avertissements → round-trip exact ; la liste passée en paramètre est copiée (pas partagée par
référence, comme `SheetExtractionRule.PointRules`).

### 072.3 — EF Core (Infrastructure)

`GeneratedFileRecordConfiguration` : `OwnsMany(r => r.Warnings, w => { w.ToTable
("GeneratedFileRecordWarnings"); w.WithOwner().HasForeignKey("GeneratedFileRecordId"); w.Property
(x => x.Sheet).HasMaxLength(200); ... })` — table owned classique, même convention que toutes les
autres collections owned de ce projet (pas de mapping JSON `.ToJson()`, jamais utilisé ailleurs
ici — cohérence avec l'existant plutôt que nouveauté). Migration `dotnet ef migrations add
AddWarningsToGeneratedFileRecord --project src/ExcelETL.Infrastructure --startup-project
src/ExcelETL.WebAPI --context ExcelEtlDbContext` (jamais écrite à la main). Non appliquée à un
vrai SQL Server dans ce lot (base de dev jetable, convention déjà établie).

Tests (`EfGeneratedFileArchiveStoreTests`) : round-trip complet d'un enregistrement avec 2-3
avertissements via le vrai provider EF Core InMemory ; un enregistrement sans avertissement
persiste et relit une liste vide.

### 072.4 — `ProcessOxoFileService`/`ProcessOxoFileResult` (Application)

`ProcessOxoFileResult` gagne `Guid? ArchivedRecordId` (dernier paramètre, `null` si l'archivage a
échoué — best-effort, cf. Lot 034 — ou n'a simplement pas encore eu lieu). `TryArchiveAsync`
retourne ce `Guid?` (le `Guid.NewGuid()` déjà miné en interne, jamais retourné aujourd'hui) au
lieu de `void` ; `null` sur l'exception déjà catchée (le `catch` existant ne change pas de
comportement, juste sa valeur de retour). `GeneratedFileRecord` est construit avec `warnings:
importResult.Errors.Select(e => new GeneratedFileWarning(e.Sheet, e.BlockIdentifier,
e.Code.ToString(), e.Message, e.ExtractedValue)).ToList()` — pour les 3 statuts (Success/
NonBlockingWarning/Rejected), pas seulement les 2 premiers : un enregistrement `Rejected` porte
déjà ses erreurs dans le corps 422 aujourd'hui, les persister aussi dans l'archive les rend
consultables après coup via `GET /api/generated-files/{id}`, cohérent avec la philosophie de
traçabilité du Lot 034.

Tests (`ProcessOxoFileServiceTests`) : cas accepté sans erreur → `ArchivedRecordId` non nul,
`Warnings` sauvegardé vide ; cas avec avertissements non bloquants → `Warnings` sauvegardé avec
le bon contenu (Sheet/BlockIdentifier/Code/Message/ExtractedValue) ; cas rejeté → `Warnings`
sauvegardé aussi ; cas où l'archivage échoue (mock qui lève) → `ArchivedRecordId` reste `null`,
le flux généré est quand même retourné (non-régression du comportement best-effort existant).

### 072.5 — `OxoController` (WebAPI)

Sur les 2 chemins (200 et 422) : `Response.Headers["X-Warning-Count"] =
result.ImportResult.Errors.Count.ToString(CultureInfo.InvariantCulture);` toujours posé (0 si
vide) ; `Response.Headers["X-Generated-File-Id"] = result.ArchivedRecordId.Value.ToString();`
posé seulement si `ArchivedRecordId is not null` (jamais posé avec une valeur vide — absence de
l'en-tête, pas une valeur `null`/vide, si l'archivage a échoué). Le corps 422 existant gagne
`ExtractedValue` dans la projection anonyme (`Extensions["errors"]`).

Tests (`OxoProcessEndpointTests`) : succès sans avertissement → `X-Warning-Count: 0`,
`X-Generated-File-Id` présent et correspondant à l'enregistrement réellement archivé ; succès
avec avertissements (fixture réelle C7401, qui en porte déjà un — Lot 032) → `X-Warning-Count`
égal au compte réel, en-tête cohérent avec `GET /api/generated-files/{id}` interrogé ensuite ;
rejet → mêmes 2 en-têtes présents, corps 422 inchangé pour Sheet/BlockIdentifier/Code/Message +
`ExtractedValue` ajouté ; échec d'archivage simulé (mock `IGeneratedFileWriter` qui lève, test déjà
existant réutilisé) → `X-Generated-File-Id` absent de la réponse, `X-Warning-Count` quand même
présent (ne dépend pas de l'archivage).

### 072.6 — `GET /api/generated-files` (WebAPI)

`GeneratedFileSummaryResponse` gagne `int WarningCount` et `IReadOnlyList<GeneratedFileWarningResponse>
Warnings` (nouveau contrat `GeneratedFileWarningResponse(string Sheet, string BlockIdentifier,
string Code, string Message, string? ExtractedValue)`, même forme que le corps 422 pour qu'un
même client puisse réutiliser un seul type de désérialisation). `GeneratedFilesController.ToSummary`
mappe `record.Warnings` — présent identiquement sur `Search` et `GetById` (une seule forme de DTO,
pas de version « résumé » vs « détail » différente — le volume de cette archive reste faible,
pas de raison de complexifier).

Tests (`GeneratedFilesEndpointTests`) : `GetById` avec avertissements → `WarningCount`/`Warnings`
corrects ; `Search` d'une liste avec plusieurs enregistrements → chacun porte ses propres
avertissements sans mélange ; enregistrement sans avertissement → `WarningCount == 0`,
`Warnings` vide (jamais `null`).

### 072.7 — `/generated-files` (BlazorAdmin)

Nouvelle colonne « Avertissements » (QuickGrid, triable sur `WarningCount`) : badge
`bg-warning text-dark` avec le compte si `> 0`, `bg-secondary`/`0` sinon — même palette de badges
que la colonne Statut existante. Dans la cellule, un `<details>` replié par défaut (même
convention que partout dans ce projet — R3, Lot 033, etc.) liste chaque avertissement
(Sheet · BlockIdentifier · Message). Même traitement sur la carte mobile. Nouvelles clés resx
`GeneratedFiles_WarningsColumn`/`GeneratedFiles_NoWarnings`/`GeneratedFiles_WarningsSummary`
(EN/FR).

Tests (`GeneratedFilesTests`) : badge visible avec le bon compte ; détail absent du DOM tant que
replié (vraie absence, pas `display:none` — même exigence que partout ailleurs dans ce projet) ;
détail affiché après clic ; carte mobile équivalente.

## Non-régression

Tout le contrat HTTP existant reste inchangé : codes de statut (200/422/400/404), forme du corps
`FileStreamResult`, forme préexistante du corps 422 (les 4 champs déjà présents ne changent pas de
nom/type, `ExtractedValue` est additif). `legacy/ExcelProcessingClientService` n'est pas modifié
par ce lot (aucun champ requis nouveau côté requête) — les nouveaux en-têtes/champs sont purs
ajouts, ignorables par un client qui ne les lit pas encore.

## Hors périmètre

- Toute modification du format `ProblemDetails` lui-même (RFC 7807 respecté à l'identique).
- Un flag de sévérité par avertissement au-delà de ce qu'`ExtractionErrorCode` porte déjà.
- Une politique de purge de `GeneratedFileRecordWarnings` (même absence de politique que le reste
  de l'archive, Lot 034).
- Migration appliquée à un environnement déjà seedé (base de dev jetable).
- Toute modification du câblage legacy autre que la lecture, optionnelle, des nouveaux en-têtes/
  champs — objet du prompt séparé remis à la session legacy une fois ce lot terminé.
