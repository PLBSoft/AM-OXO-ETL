# Modèle de domaine — Catalogue de primitives & gestion d'erreurs

*État courant du modèle pivot d'extraction (Domain/Application), à jour du code réellement
implémenté (Lots A-E). Complète `spec-extraction-fichier-source-oxo.md`.*

---

## 1. Catalogue de primitives (figé)

Le passage des 6 feuilles réelles à un catalogue générique fait ressortir **5 primitives**, pas plus — cohérent avec le principe acté de ne pas sur-ingénierer.

### 1.1 `DirectCell(sheet, range)`
Lecture directe d'une cellule ou plage fusionnée. Couvre toutes les lectures d'en-tête (`M2:O2`, `K6:T6`, `B6:E6`...).

### 1.2 `RepeatingBlockLocator`
La primitive centrale, unique pour les 6 feuilles (y compris PROCEDURE, cas particulier avec un pas de 1) :

```csharp
public sealed record RepeatingBlockLocator(
    string Sheet,
    int FirstBlockStartRow,
    int Step,
    string StopFieldName,          // ex. "Identification" (feuilles isolement) ou "Action" (PROCEDURE)
    IReadOnlyList<BlockFieldDefinition> Fields);

public sealed record BlockFieldDefinition(
    string Name,                   // ex. "Identification", "Designation", "TypeElement"
    string ColumnRange,             // ex. "B:E"
    int RowOffsetStart,              // relatif à FirstBlockStartRow + i*Step
    int RowOffsetEnd);
```

Pour le bloc `i` (i=0,1,2...), la plage réelle d'un champ = colonnes `ColumnRange`, lignes `[FirstBlockStartRow + i*Step + RowOffsetStart ; FirstBlockStartRow + i*Step + RowOffsetEnd]`.

**Condition d'arrêt** : dès que le champ nommé `StopFieldName` du bloc `i` est vide, on arrête — sans lire le bloc `i`.

Couvre : ISOLEMENT (pas 7), PLATINES/ORIFICES CAPACITES (pas 8), AUTRES JOINTS TOUCHES (pas 7), DIVERS (pas 3), **et** PROCEDURE (pas 1, un seul champ "bloc" = la ligne de TacheMultiple, arrêt sur `Action`/`C:L`).

### 1.3 Transformations de texte
```csharp
public abstract record TextTransform;
public sealed record RawValue : TextTransform;
public sealed record SubstringAfter(string Prefix) : TextTransform;
public sealed record Concat(IReadOnlyList<ConcatPart> Parts) : TextTransform;
public abstract record ConcatPart;
public sealed record Literal(string Text) : ConcatPart;
public sealed record FieldRef(string FieldName) : ConcatPart;
```
Couvre : préfixe repère (`SubstringAfter("MAD-OXO-")`, paramétrable), `Designation` (`Concat`), repère isolement composé (`Concat`).

*Note* : cette hiérarchie n'est référencée par aucun type de la chaîne `SheetExtractionRule →
RepeatingBlockLocator → BlockFieldDefinition` dans le modèle actuel — `BlockFieldDefinition` ne
porte pas de `TextTransform` associé. Ce n'est pas un champ manquant côté configuration, c'est
une caractéristique du modèle tel qu'implémenté.

### 1.4 `ConditionalPointRule` et `UnconditionalColonneNames`
Nouveau par rapport au squelette initial (qui ne prévoyait qu'un mapping direct variable→champ) — confirmé **égalité/inégalité stricte suffisante**, pas de moteur de conditions plus riche pour l'instant :

```csharp
public enum ConditionOperator { Equals, NotEquals }

public sealed record ConditionalPointRule(
    string SourceFieldName,       // ex. "TypeElement"
    ConditionOperator Operator,
    string ComparisonValue,        // ex. "SOUPAPE", "TUBING"
    string ColonneName);           // ex. "SOUPAPE : CONSTAT ENCRASSEMENT"
```

`SheetExtractionRule` porte également `UnconditionalColonneNames` (`IReadOnlyList<string>`,
requis non-null, peut être vide) — les `Colonne.Nom` créés pour **tout** Isolement de la
feuille, sans condition (ex. `"PROLOCK VANNES"`/`"DEPROLOCK VANNES"` d'ISOLEMENT). Distinct des
`ConditionalPointRule`, qui portent toujours une condition. Ce champ est apparu pendant
l'implémentation du Lot C (pas anticipé dans la conception initiale) et s'intègre proprement au
modèle.

⚠️ **Normalisation de la comparaison** : la comparaison `ComparisonValue` doit être insensible à la casse **et** tolérante aux espaces de début/fin (`.Trim()`) — des cas réels ont été observés dans les 3 fichiers fixtures (espace de fin sur `"SOUPAPE "`, variante `"POINT DE FEU"` au lieu de `"POINT FEU"`). Le `Trim`+casse suffit pour le premier cas, pas pour le second (différence de mot, pas d'espacement) — ce second cas reste un échec de correspondance légitime, couvert par la politique d'erreur non bloquante du §3.2 ci-dessous.

### 1.5 Portée globale (broadcast)
`loc1` (feuille DIVERS, `B6:E6`) : valeur extraite une fois, appliquée à l'Equipement **et** à
tous les Isolements du run. L'écart `ZONE 4`/`ZONE 3` observé entre les feuilles `Parents`/
`Enfants` du fichier cible réel `OXO_TRAME_IMPORT_MAD.xlsx` est jugé non fiable (fichier de test
déconnecté des fichiers source) — la portée globale de `loc1` est conservée **sans exception**.

### Hors catalogue — cas volontairement non généralisé
La règle "ligne PROCEDURE sans `Ordre` ⇒ TacheMultiple factice déjà validée" (voir spec §1.2) **n'est pas une primitive** : c'est une règle métier ad hoc, propre à la feuille PROCEDURE, câblée en dur dans le service d'extraction de cette feuille plutôt que généralisée dans le catalogue.

---

## 2. Modèle de domaine — profil et sortie pivot

### 2.1 Profil d'import (persistance EF Core)

```csharp
public class ImportProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public string RepereePrefix { get; }                 // paramétrable, défaut "MAD-OXO-"
    public string EquipementTypeElementNom { get; }       // "MAD TRAVAUX" — seule valeur utilisée
    public IReadOnlyList<SheetExtractionRule> SheetRules { get; }
}

public class SheetExtractionRule
{
    public string SheetName { get; }
    public RepeatingBlockLocator Locator { get; }
    public IReadOnlyList<string> UnconditionalColonneNames { get; }
    public IReadOnlyList<ConditionalPointRule> PointRules { get; }
}
```

**`EquipementTypeElementNom`** : porte la valeur `TypeElement.Nom` à affecter à l'Équipement
parent (feuille PROCEDURE) lors de l'extraction — `"MAD TRAVAUX"` (confirmé en base OXO, voir
glossaire). Il n'y a en réalité qu'une seule valeur possible pour ce champ : pas de "profil REL"
distinct, les tâches REL étant extraites du même fichier MAD (feuille PROCEDURE, alias
`TypeTacheMultiple.Code`, voir spec §1.3), pas un dossier séparé avec son propre Equipement
parent. Le champ reste néanmoins paramétrable dans le profil plutôt que codé en dur — **la
valeur ne doit jamais être une constante dans le code du service d'extraction**, elle vient
uniquement du profil actif, au même titre que `RepereePrefix`. Contrairement à `RepereePrefix`
(défaut `"MAD-OXO-"`), `EquipementTypeElementNom` n'a **pas** de valeur par défaut : il doit
toujours être fourni explicitement — un défaut suggérerait une valeur "correcte" pré-remplie,
ce qui reproduirait exactement l'anti-pattern qu'on cherche à éviter.

Constructeur : valide que `EquipementTypeElementNom` n'est pas vide/blanc
(`DomainValidationException`/`DomainErrorCode.ImportProfile_EmptyEquipementTypeElementNom`),
cohérent avec le style "entités riches qui valident" déjà en place. `Step` de
`RepeatingBlockLocator` est validé `> 0` selon le même principe.

### 2.2 Objet pivot (résultat d'extraction — Domain/Application, zéro dépendance ClosedXML)
C'est l'objet qui découple extraction et écriture cible, et qui alimente l'écran "tester profil" :

```csharp
public sealed record EquipementPivot(string Repere, string Designation, string TypeElementNom);
public sealed record IsolementPivot(string Repere, string Designation, string TypeElementNom, string PositionALaPose, /* ... */ string Localisation);
public sealed record PointPivot(string ColonneNom, string ParentRepere);
public sealed record TacheMultiplePivot(int? Ordre, string Action, string Acteur, string Risques, string TypeTacheMultipleCode, DateOnly? DateValidation, bool EstFactice);

public sealed class ImportResult
{
    public EquipementPivot? Equipement { get; }
    public IReadOnlyList<IsolementPivot> Isolements { get; }
    public IReadOnlyList<PointPivot> Points { get; }
    public IReadOnlyList<TacheMultiplePivot> TachesMultiples { get; }
    public IReadOnlyList<ExtractionError> Errors { get; }
    public bool HasErrors => Errors.Count > 0;
}
```

`IsolementPivot.PositionALaPose` — extrait de la feuille ISOLEMENT (`H20:O21`, "Position MAD"
côté source), alimente la colonne cible `"POSITION A LA POSE"` du fichier `Enfants` (écriture
cible toujours hors périmètre du pipeline d'extraction).

---

## 3. Modèle d'erreurs — exception au principe "pas de Result pattern générique"

`etat-des-lieux-technique.md` §2 pose "pas de Result pattern générique" avec une seule exception
documentée (`IdentityOperationResult`, spécifique à `UserManager`). Le pipeline d'extraction en
a une deuxième, pour une raison différente : accumuler des erreurs *par bloc* pendant qu'on
continue à traiter les blocs suivants n'est pas compatible avec "lever une exception typée et
arrêter" — ce n'est pas un défaut de conception, juste un vrai besoin de rapport de traitement
par lot (batch import), distinct de la validation d'invariants métier sur une entité.

```csharp
public enum ExtractionErrorCode { RequiredFieldMissing, UnparsableValue, UnrecognizedTypeElement }

public sealed record ExtractionError(
    string Sheet,
    string BlockIdentifier,      // ex. repère de l'Isolement concerné, ou n° de ligne
    ExtractionErrorCode Code,
    string Message);             // technique et précis ("Cellule C6 introuvable ou vide")
```

`ExtractionError` alimente directement le point de log "Errors (exception type, merged cell
coordinate that failed)" du cahier des charges fonctionnel initial.

*Note* : `ExtractionErrorCode` n'a à ce jour que ces 3 membres — volontaire, d'autres seront
ajoutés au fil des besoins réels plutôt qu'anticipés.

### 3.1 Granularité de la politique "extraire les valides, signaler le reste"
Au niveau d'un bloc Isolement/Point/TacheMultiple : un bloc invalide est ignoré et journalisé,
les autres continuent d'être traités. Exception : **si l'Équipement parent (feuille PROCEDURE)
lui-même est invalide** (ex. repère `M2:O2` vide ou date de révision illisible), le fichier
entier est rejeté (une seule erreur bloquante, le reste du pipeline n'est pas exécuté) — tout le
reste du fichier en dépend (repère composé, portée globale `loc1`, association aux Tableaux).

### 3.2 Cas "TypeElement non reconnu par aucune règle"
Si le type d'élément d'un Isolement ne correspond à aucune condition connue (ex. `"VANNE"` en
feuille ISOLEMENT, confirmé absent de la base OXO — voir glossaire), l'Isolement reste
**valide** : il est extrait normalement, simplement aucun Point n'est créé pour lui. Ce n'est
pas un motif de rejet du bloc, mais ça produit un **avertissement non bloquant** dans `Errors`
(même liste, sévérité différente) pour rester visible sans casser le traitement.

---

## 4. Persistance (`IImportProfileStore`)

Abstraction Application, découplée d'EF Core :

```csharp
public interface IImportProfileStore
{
    Task<IReadOnlyList<ImportProfile>> GetAllAsync(CancellationToken ct = default);
    Task<ImportProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(ImportProfile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

Implémentation réelle `EfImportProfileStore` (Infrastructure), même pattern que les autres
repositories du projet (`IDbContextFactory<T>`, configuration EF Core dédiée).
