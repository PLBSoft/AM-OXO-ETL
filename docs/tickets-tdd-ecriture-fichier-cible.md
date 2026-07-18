# Tickets TDD — Lot I : écriture du fichier Excel cible

*Document vivant (pas de suffixe de date). Symétrique du Lot C-E côté import : le modèle pivot
(`ImportResult`/`EquipementPivot`/`IsolementPivot`/`PointPivot`/`TacheMultiplePivot`) est déjà
prêt et zéro-dépendance ClosedXML — ce lot ajoute la brique de génération en face de la brique de
lecture déjà terminée. Approximation volontaire de `OXO_TRAME_IMPORT_MAD.xlsx` (2 feuilles
`Parents`/`Enfants`, format non figé côté client) : on développe avec ce qu'on sait, on ne bloque
pas sur les 100% de spec.*

**Conventions respectées** (voir `etat-des-lieux-technique.md`) : xUnit 2.9.3 + FluentAssertions
7.0.0 + Moq ; un projet de test par projet source, miroir dossier-par-dossier ; exceptions
typées avec `ErrorCode` ; organisation par feature ; `IDbContextFactory<T>` pour la persistance EF.

**Hors périmètre explicite de ce lot** (voir décisions §ci-dessous, ne pas réouvrir sans
nouvelle demande) :
- Feuille Tâches Multiples — absente du profil de génération, pas un placeholder vide. Extension
  additive prévue dans un futur lot, une fois le pivot `TacheMultiplePivot` réellement consommé
  côté écriture.
- Écran Blazor de construction du profil d'export (futur "Lot J", symétrique au Lot F) — ce lot
  ne couvre que Domain/Application/Infrastructure.
- Exposition Web API / téléchargement M2M du fichier généré — dépend du Lot G (logging) et de la
  sécurité API Key, non traité ici.
- Réutilisation de `ClosedXmlGeneratorService` (POC legacy) — explicitement écarté, ce générateur
  consomme `ExtractionResult`/`ExtractedSheet`, pas le modèle pivot OXO.

---

## I1. Domain : primitives `ExportProfile` / `SheetGenerationRule` / définitions de colonnes

**Concepts** (nouveaux, distincts d'`ImportProfile`) :
- `ExportProfile` : `Nom`, liste ordonnée de `SheetGenerationRule`.
- `SheetGenerationRule` : `SheetName` (paramétrable), `PivotSource` (enum `Equipement`/`Isolement`
  — quelle collection du pivot alimente une ligne de cette feuille), liste ordonnée de
  `ColumnDefinition` (descriptives) + liste ordonnée de `PointColumnDefinition` (colonnes Points).
- `ColumnDefinition` : `Header` (titre 1ère ligne), `Source` (`PivotFieldRef?` — nullable :
  `null` = colonne conservée dans le schéma sans règle d'extraction pour l'instant, écrit une
  cellule vide sans erreur).
- `PointColumnDefinition` : `ColonneNom` (clé de correspondance avec `PointPivot.ColonneNom`),
  `Header` (titre 1ère ligne, peut différer de `ColonneNom`), `MarkValue` (valeur écrite si le
  Point existe pour la ligne — défaut `"X"`, paramétrable si besoin futur).

**Tests** :
- Construction et égalité structurelle (records) pour les 4 types.
- Invariants de construction : `SheetName` non vide/blanc, `Header` non vide/blanc (descriptif
  et Point), pas de doublon de `Header` au sein d'une même feuille, pas de doublon de
  `ColonneNom` au sein des `PointColumnDefinition` d'une même feuille → `DomainValidationException`
  + `DomainErrorCode` dédié, même pattern que côté import (Lot A1).
- `ColumnDefinition.Source = null` est un cas **valide**, pas une erreur de construction —
  test dédié qui vérifie qu'aucune exception n'est levée.

**Dossier** : `src/ExcelETL.Domain/Generation/Profile/` (+ miroir tests).

---

## I2. Domain : `PivotFieldRef` — sélecteur typé de champs (pas de réflexion)

**Concept** : enum couvrant les champs exposés par `EquipementPivot` et `IsolementPivot`
(`EquipementRepere`, `EquipementDesignation`, `EquipementTypeElementNom`, `IsolementRepere`,
`IsolementDesignation`, `IsolementTypeElementNom`, `IsolementPositionALaPose`,
`IsolementLocalisation`, ...) — extensible au fil des besoins réels, pas anticipé en bloc
(même philosophie que `ExtractionErrorCode`, 3 membres suffisants au départ côté import).

**Tests** :
- Un `PivotFieldResolver` (ou équivalent) qui, pour une valeur de `PivotFieldRef` compatible avec
  le type de pivot passé (`EquipementPivot` ou `IsolementPivot`), retourne la valeur `string`
  correcte — un test par champ existant.
- Cas d'incompatibilité (ex. `IsolementPositionALaPose` demandé sur une ligne `Equipement`) :
  comportement à trancher — exception typée à la construction du profil (validation croisée
  `PivotSource` ↔ `PivotFieldRef` utilisés) plutôt qu'une erreur silencieuse à l'exécution.
  Test dédié qui vérifie que cette incohérence est détectée **au chargement du profil**, pas au
  moment de générer un fichier.

**Dossier** : `src/ExcelETL.Domain/Generation/Fields/` (+ miroir tests).

---

## I3. Application : abstraction `IWorkbookWriter` + moteur de génération générique

**Concept** : symétrique de `IWorkbookReader`/`RepeatingBlockReader` côté lecture. Le moteur
(`SheetGenerationEngine` ou nom équivalent) prend un `ImportResult` (pivot) + un `ExportProfile`,
et produit — **sans dépendance ClosedXML à ce niveau** (couche Application) — une structure de
sortie intermédiaire (ex. liste de lignes de cellules par feuille) que l'Infrastructure se
chargera d'écrire réellement.

**Tests** :
- Pour une `SheetGenerationRule` de type `Equipement` : génère l'en-tête (ordre exact des
  `Header` descriptifs puis Points) et une ligne de données à partir d'un `EquipementPivot`
  synthétique + une liste de `PointPivot` correspondants (marque "X" uniquement pour les
  `ColonneNom` présents en Point pour ce repère parent).
- Pour une `SheetGenerationRule` de type `Isolement` : une ligne par `IsolementPivot`, même
  logique de marquage Points.
- `ColumnDefinition.Source = null` → cellule vide dans la ligne générée, pas d'exception.
- `ImportResult.Equipement is null` (fichier rejeté en amont) : le moteur ne doit rien générer
  pour la feuille `Equipement` — comportement à trancher explicitement (feuille absente vs
  feuille présente avec 0 ligne) et testé en conséquence.

**Dossier** : `src/ExcelETL.Application/Generation/` (+ miroir tests).

---

## I4. Infrastructure : `ClosedXmlWorkbookWriter : IWorkbookWriter`

**Concept** : implémentation réelle ClosedXML, nouvelle, indépendante de
`ClosedXmlGeneratorService` (POC). Écrit les feuilles dans l'ordre du profil, en-têtes en 1ère
ligne, une feuille par `SheetGenerationRule`.

**Tests** — contre la vraie librairie ClosedXML (jamais mockée, même convention que E1 côté
import) :
- Classeur généré en mémoire, relu ensuite via ClosedXML : noms de feuilles corrects et dans le
  bon ordre, en-têtes en ligne 1 dans l'ordre attendu, valeurs de cellules correctes pour un jeu
  de données synthétique simple.
- Convention de nommage du fichier de sortie (`MAD_{Equipement.Nom}_{AAAAMMDDHHmmss}.xlsx`,
  déjà actée dans `spec-extraction-fichier-source-oxo.md`) : test dédié sur la fonction de
  construction du nom, séparée de l'écriture elle-même.

**Dossier** : `src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookWriter.cs` (+ miroir tests).

---

## I5. Tests d'intégration bout-en-bout : import → pivot → génération cible (3 fixtures réelles)

**Tests** — un test par fichier (C7401, D8570, G6306B), pipeline complet : lecture via
`ClosedXmlWorkbookReader` → `ImportPipelineOrchestrator` (Lot D, déjà terminé) → génération via
`ClosedXmlWorkbookWriter` + un `ExportProfile` de test approximant `OXO_TRAME_IMPORT_MAD.xlsx`
(2 feuilles `Parents`/`Enfants`, colonnes descriptives connues mappées, colonnes non mappées
laissées à `Source = null`, pas de feuille Tâches Multiples) :
- Structure du fichier généré conforme au profil (feuilles, en-têtes).
- Quelques valeurs connues (repère, `loc1` broadcast) retrouvées dans les cellules générées.
- **Cas D8570/`"VANNE"`** : l'isolement avec `TypeElementNom` non reconnu (avertissement non
  bloquant côté extraction) doit tout de même apparaître comme une ligne normale dans la feuille
  `Enfants` générée — vérifie que la politique d'erreur non bloquante se propage correctement
  jusqu'à l'écriture, pas seulement jusqu'à `ImportResult`.

**Dossier** : `tests/ExcelETL.Infrastructure.Tests/Excel/` (miroir de
`ImportPipelineOrchestratorIntegrationTests.cs`, Lot D2).

---

## I6. Infrastructure : persistance EF Core d'`ExportProfile`

**Concept** — symétrique du Lot E2 côté import :

```csharp
public interface IExportProfileStore
{
    Task<IReadOnlyList<ExportProfile>> GetAllAsync(CancellationToken ct = default);
    Task<ExportProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(ExportProfile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**Tests** — contre le vrai provider EF Core InMemory (jamais mocké), même convention que
`EfImportProfileStore` :
- CRUD complet, y compris persistance des collections imbriquées (`SheetGenerationRule`,
  `ColumnDefinition`, `PointColumnDefinition`).
- `ColumnDefinition.Source = null` bien persisté et relu comme `null` (pas de valeur par défaut
  silencieuse qui romprait le principe "colonne non mappée = vide, pas d'erreur").

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Repositories/EfExportProfileStore.cs`
(+ configuration EF Core, migration associée).

---

## Ordre recommandé

1. **I1 → I2** (Domain, zéro dépendance, peut démarrer immédiatement en parallèle du Lot G)
2. **I3** (Application, dépend de I1/I2)
3. **I4** (Infrastructure writer, dépend de I3)
4. **I5** (intégration bout-en-bout contre les 3 fixtures réelles, dépend de I4)
5. **I6** (persistance EF, indépendant de I3/I4/I5 — peut être fait en parallèle dès I1 terminé)

## Non couvert / à trancher pendant le développement

- Comportement exact quand `ImportResult.Equipement is null` (voir I3) — décision à prendre au
  moment d'écrire ce ticket, pas figée ici.
- `MarkValue` des colonnes Points : `"X"` par défaut, à confirmer si une valeur différente est
  un jour nécessaire (pas anticipé, ajouté seulement si un besoin réel apparaît).
- Le format exact de `OXO_TRAME_IMPORT_MAD.xlsx` n'étant pas figé côté client, l'`ExportProfile`
  de test (I5) est une **approximation de travail** — à ajuster sans réouvrir l'architecture le
  jour où le client confirme la structure définitive (colonnes descriptives manquantes, Tâches
  Multiples).
