# Modèle de domaine — Catalogue de primitives & gestion d'erreurs

*Synthèse de modélisation, 2026-07-16, faisant suite à `spec-extraction-fichier-source-oxo-2026-07-16.md` (6 feuilles maintenant spécifiées) et aux décisions produit actées ce jour (conditions Points, politique d'erreur, découplage extraction/écriture cible). Complète et affine le squelette de modèle pivot esquissé dans `ALPHA-OXO-ETL-EXCEL-synthese.md` §4.*

> **Mise à jour 2026-07-16 (v2)** : ajout du champ `ImportProfile.EquipementTypeElementNom`
> (section 2.1), identifié manquant lors de la clarification sur la valeur `"MAD TRAVAUX"`
> (vs l'hypothèse `"MAD"` infirmée) — voir `glossaire-ef6-legacy-AMAR-ModelCF-2026-07-16.md`.
> Le modèle initial n'avait pas anticipé la variation de `TypeElement.Nom` de l'Équipement
> parent entre un profil MAD et un futur profil REL ; cette variation n'était pas visible tant
> qu'aucune vraie valeur confirmée n'existait à comparer à l'hypothèse. Aucun autre changement
> de modèle à cette occasion — le catalogue de primitives (§1) et l'objet pivot (§2.2) restent
> inchangés.

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

### 1.4 `ConditionalPointRule`
Nouveau par rapport au squelette initial (qui ne prévoyait qu'un mapping direct variable→champ) — confirmé **égalité/inégalité stricte suffisante**, pas de moteur de conditions plus riche pour l'instant :

```csharp
public enum ConditionOperator { Equals, NotEquals }

public sealed record ConditionalPointRule(
    string SourceFieldName,       // ex. "TypeElement"
    ConditionOperator Operator,
    string ComparisonValue,        // ex. "SOUPAPE", "TUBING"
    string ColonneName);           // ex. "SOUPAPE : CONSTAT ENCRASSEMENT"
```
Une liste vide de `ConditionalPointRule` pour une feuille signifie "toujours créer le Point" (cas ISOLEMENT/PLATINES/ORIFICES/AUTRES JOINTS TOUCHES sur la plupart de leurs colonnes) ; une liste non vide signifie "créer seulement si une condition matche" (cas DIVERS, et le `≠ TUBING` d'AUTRES JOINTS TOUCHES).

⚠️ **Normalisation de la comparaison (ajouté 2026-07-16)** : la comparaison `ComparisonValue` doit être insensible à la casse **et** tolérante aux espaces de début/fin (`.Trim()`) — des cas réels ont été observés dans les 3 fichiers fixtures (espace de fin sur `"SOUPAPE "`, variante `"POINT DE FEU"` au lieu de `"POINT FEU"`). Le `Trim`+casse suffit pour le premier cas, pas pour le second (différence de mot, pas d'espacement) — ce second cas reste un échec de correspondance légitime, couvert par la politique d'erreur non bloquante du §3.2 ci-dessous.

### 1.5 Portée globale (broadcast) — déjà prévue, confirmée par l'usage de `loc1`
Le squelette initial prévoyait déjà `MappingScope { Single, BroadcastToAllRows }`. `loc1` (feuille DIVERS, `B6:E6`) en est la première utilisation concrète confirmée : valeur extraite une fois, appliquée à l'Equipement **et** à tous les Isolements du run. Aucune nouvelle primitive nécessaire — juste la confirmation que ce mécanisme est bien utilisé.

**Confirmé (2026-07-16)** : l'écart `ZONE 4`/`ZONE 3` observé entre les feuilles `Parents`/`Enfants` du fichier cible réel `OXO_TRAME_IMPORT_MAD.xlsx` est jugé non fiable (fichier de test déconnecté des fichiers source, probable erreur d'inattention) — la portée globale de `loc1` est conservée **sans exception**.

### Hors catalogue — cas volontairement non généralisé
La règle "ligne PROCEDURE sans `Ordre` ⇒ TacheMultiple factice déjà validée" (voir spec §1.2) **n'est pas une primitive** : c'est une règle métier ad hoc, propre à la feuille PROCEDURE, câblée en dur dans le service d'extraction de cette feuille plutôt que généralisée dans le catalogue. La généraliser maintenant pour un usage unique irait à l'encontre du principe déjà acté ("ne pas figer le catalogue à l'avance, éviter la sur-ingénierie").

---

## 2. Modèle de domaine — profil et sortie pivot

### 2.1 Profil d'import (persistance EF Core, cohérent avec le POC existant)

```csharp
public class ImportProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public string RepereePrefix { get; }                 // paramétrable, défaut "MAD-OXO-"
    public string EquipementTypeElementNom { get; }       // NOUVEAU (v2) — ex. "MAD TRAVAUX" pour un profil MAD
    public IReadOnlyList<SheetExtractionRule> SheetRules { get; }
}

public class SheetExtractionRule
{
    public string SheetName { get; }                  // nom de feuille paramétrable
    public RepeatingBlockLocator Locator { get; }
    public IReadOnlyList<ConditionalPointRule> PointRules { get; }
}
```

**`EquipementTypeElementNom` (NOUVEAU, v2)** : porte la valeur `TypeElement.Nom` à affecter à l'Équipement parent (feuille PROCEDURE) lors de l'extraction — ex. `"MAD TRAVAUX"` pour un profil MAD (confirmé en base OXO, voir glossaire). Pour un futur profil REL, ce sera la valeur correspondante côté REL, encore non confirmée (aucun fichier REL disponible à ce jour) — c'est précisément pour ce genre de variation entre profils que ce champ existe : **la valeur ne doit jamais être une constante dans le code du service d'extraction**, elle vient uniquement du profil actif, au même titre que `RepereePrefix`.

Constructeur : doit valider que `EquipementTypeElementNom` n'est pas vide/blanc, cohérent avec le style "entités riches qui valident" déjà en place (`ExtractionConfig`, etc., voir `etat-des-lieux-technique.md` §2) — même `DomainValidationException`/`DomainErrorCode` que les autres invariants d'`ImportProfile`.

### 2.2 Objet pivot (résultat d'extraction — Domain/Application, zéro dépendance ClosedXML)
C'est l'objet qui découple extraction et écriture cible (voir point 2 de l'échange précédent), et qui alimente aussi l'écran "tester profil" (JSON/tableau en lecture seule, déjà prévu dans la synthèse ALPHA-OXO-ETL §3) :

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

*Note (2026-07-16)* : `IsolementPivot.PositionALaPose` ajouté — extrait de la feuille ISOLEMENT (`H20:O21`, "Position MAD" côté source), destiné à alimenter la colonne cible `"POSITION A LA POSE"` du fichier `Enfants` (écriture cible hors périmètre du lot d'extraction, mais le champ doit exister dans le pivot dès maintenant).

---

## 3. Modèle d'erreurs — ⚠️ exception au principe "pas de Result pattern générique"

Vos réponses (égalité/inégalité suffit, extraire les valides + signaler les autres, rapport groupé) impliquent un **changement de convention par rapport à ce qui est établi ailleurs dans le projet**. `etat-des-lieux-technique.md` §2 pose "pas de Result pattern générique" avec une seule exception documentée (`IdentityOperationResult`, spécifique à `UserManager`). Le pipeline d'extraction en a désormais besoin d'une deuxième, pour une raison différente : accumuler des erreurs *par bloc* pendant qu'on continue à traiter les blocs suivants n'est pas compatible avec "lever une exception typée et arrêter" — ce n'est pas un défaut de conception, juste un vrai besoin de rapport de traitement par lot (batch import), distinct de la validation d'invariants métier sur une entité.

Proposition, à documenter comme deuxième exception explicite au principe :

```csharp
public enum ExtractionErrorCode { RequiredFieldMissing, UnparsableValue, UnrecognizedTypeElement, /* ... */ }

public sealed record ExtractionError(
    string Sheet,
    string BlockIdentifier,      // ex. repère de l'Isolement concerné, ou n° de ligne
    ExtractionErrorCode Code,
    string Message);             // technique et précis, cf. convention déjà actée ("Cellule C6 introuvable ou vide")
```

`ExtractionError` alimente directement le point de log **"Errors (exception type, merged cell coordinate that failed)"** déjà prévu dans le cahier des charges fonctionnel initial — même mécanisme, réutilisé pour le rapport d'extraction et pour les logs.

### 3.1 Granularité de la politique "extraire les valides, signaler le reste"
Votre réponse s'applique naturellement **au niveau d'un bloc Isolement/Point/TacheMultiple** : un bloc invalide est ignoré et journalisé, les autres continuent d'être traités. Mais un cas n'est pas couvert par les 3 réponses et mérite d'être tranché explicitement :

⚠️ **Si l'Équipement parent (feuille PROCEDURE) lui-même est invalide** (ex. repère `M2:O2` vide ou date de révision illisible) — tout le reste du fichier en dépend (repère composé, portée globale `loc1`, association aux Tableaux...). **Confirmé (2026-07-16)** : dans ce cas, le fichier entier est rejeté (une seule erreur bloquante, le reste du pipeline n'est pas exécuté). La politique "extraire les valides / signaler le reste" ne s'applique qu'*en dessous* du niveau Équipement — aux Isolements, Points et TachesMultiples.

### 3.2 Cas "TypeElement non reconnu par aucune `ConditionalPointRule`"
À distinguer d'une vraie erreur : si le type d'élément d'un Isolement ne correspond à aucune condition connue (ex. DIVERS avec une valeur hors `INSTRUMENTATION`/`ZERO ENERGIE`/`SOUPAPE`/`POINT FEU`, ou `"VANNE"` en feuille ISOLEMENT — voir glossaire, confirmé absent de la base OXO), l'Isolement reste **valide** — il est extrait normalement, simplement aucun Point n'est créé pour lui. Ce n'est pas un motif de rejet du bloc, mais ça mérite un **avertissement non bloquant** dans `Errors` (même liste, sévérité différente) pour que ça reste visible sans casser le traitement.

---

## 4. Prochaines étapes

1. ~~Vous confirmez (ou ajustez) le point 3.1~~ — **Confirmé (2026-07-16)** : Équipement invalide = rejet du fichier entier.
2. Découpage en tickets TDD → voir `tickets-tdd-extraction-2026-07-16.md`.
3. ~~Ajouter le champ manquant pour la valeur `TypeElement.Nom` de l'Équipement parent~~ — **Fait (v2, 2026-07-16)** : `ImportProfile.EquipementTypeElementNom`, voir §2.1.
