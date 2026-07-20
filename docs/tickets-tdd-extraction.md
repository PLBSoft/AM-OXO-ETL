# Tickets TDD — Pipeline d'extraction AM-OXO-ETL

*Découpage à partir de `modele-domaine-import-profile.md` et `spec-extraction-fichier-source-oxo.md`. Périmètre : extraction `.xlsx` source → objet pivot en mémoire (+ rapport d'erreurs). L'écriture du `.xlsx` cible reste **hors périmètre**, en attente du format exact côté client.*

**Statut** : Lots A, B, C, D, E **terminés et codés**. Ce document sert désormais de référence
sur les règles métier et les garde-fous à préserver (ex. non-régression sur les tests décrits
ci-dessous), plus que de plan d'exécution à venir.

**Conventions respectées** (voir `etat-des-lieux-technique.md`) : xUnit 2.9.3 + FluentAssertions 7.0.0 (jamais `Assert.*`) + Moq ; un projet de test par projet source, miroir dossier-par-dossier ; exceptions typées avec `ErrorCode` (`DomainValidationException`/`ApplicationValidationException` selon la couche) ; organisation par feature, pas par type technique ; `IDbContextFactory<T>` si persistance EF nécessaire.

**Fichiers de test réels utilisés en fixtures d'intégration** : `Dossier_de_MaD_IDL_-_C7401.xlsx`, `..._D8570_chgt_plateaux.xlsx`, `..._G6306B_REV.xlsx`.

---

## Lot A — Domain : primitives et modèle pivot (zéro dépendance) ✅ terminé

### A1. Primitives de localisation et de transformation
**Tests** : construction et égalité structurelle (records) de `DirectCell`, `RepeatingBlockLocator`, `BlockFieldDefinition`, `TextTransform` (`RawValue`/`SubstringAfter`/`Concat`/`ConcatPart`), `ConditionalPointRule`/`ConditionOperator`. Invariants de construction (`Step > 0`, `RowOffsetEnd >= RowOffsetStart`, `ColumnRange` au format attendu) via `DomainValidationException` + `DomainErrorCode` dédié.
**Dossier** : `src/ExcelETL.Domain/Extraction/Primitives/` (+ miroir tests).

### A2. Modèle pivot
**Tests** : construction de `EquipementPivot`, `IsolementPivot` (dont le champ `PositionALaPose`), `PointPivot`, `TacheMultiplePivot`, `ExtractionError`/`ExtractionErrorCode`, `ImportResult` (dont `HasErrors`).
**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/`.

### A3. `ImportProfile` / `SheetExtractionRule` (entités Domain)
**Tests** : constructeurs qui valident (nom de feuille non vide, préfixe repère non vide, au moins une `SheetExtractionRule`).

`EquipementTypeElementNom` non vide/blanc → `DomainValidationException` sinon, même pattern que le nom de feuille et le préfixe repère (voir modèle de domaine §2.1). Ce champ porte la valeur `TypeElement.Nom` de l'Équipement parent (`"MAD TRAVAUX"`) — **garde-fou à préserver dans le temps** : il ne doit jamais devenir une constante dans le code du service d'extraction, uniquement lu depuis le profil actif. Pas de valeur par défaut pour ce champ (contrairement à `RepereePrefix`) : il doit toujours être fourni explicitement par le profil, jamais pré-rempli.

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/`.

---

## Lot B — Application : moteur d'exécution générique ✅ terminé

### B1. Abstraction `IWorkbookReader`
Méthodes : `string? ReadCellValue(string sheet, string range)`, `bool SheetExists(string sheet)`.

### B2. Évaluateur de `TextTransform`
`RawValue` retourne tel quel ; `SubstringAfter` retire le préfixe ; `Concat` mixe littéraux et références de champs déjà extraits.

### B3. Moteur `RepeatingBlockLocator`
- Lecture bloc par bloc avec le bon calcul de plage (`FirstBlockStartRow + i*Step + Offset`)
- Arrêt correct dès que le champ `StopFieldName` est vide (ne doit pas lire un bloc de trop)
- Couvre pas=1 (PROCEDURE) et pas=3/7/8 (feuilles isolement)
- Bloc partiellement vide (un champ non-`StopField` vide alors que `StopField` est renseigné) → `ExtractionError`, bloc ignoré, lecture continue au bloc suivant

### B4. Évaluateur `ConditionalPointRule`
`Equals`/`NotEquals` sur une valeur de champ déjà extraite ; liste vide de règles = toujours créer le Point ; valeur ne matchant aucune règle = pas de Point créé + avertissement non bloquant ajouté à `ImportResult.Errors` (pas un rejet de bloc). Comparaison insensible à la casse et `.Trim()`.

---

## Lot C — Application : services d'extraction par feuille ✅ terminé

*Chaque service est testé contre les 3 fichiers réels (fixtures d'intégration) en plus des tests unitaires contre `Mock<IWorkbookReader>`. Un service par feuille, dans `Extraction/{NomFeuille}/`.*

### C1. PROCEDURE — Équipement + TachesMultiples
Points couverts :
- Extraction de `nomMAD` (retrait préfixe `MAD-OXO-`), `Designation` (`"Rév {P2:Q2} du {R2:T2}"`, date en `dd/mm/aaaa`)
- `EquipementPivot.TypeElementNom` = `profile.EquipementTypeElementNom` — **jamais une constante dans le service**. Test explicite avec deux profils portant des valeurs `EquipementTypeElementNom` différentes, pour vérifier que le service restitue bien la valeur du profil (garde-fou anti-hardcoding, à préserver dans le temps).
- Bloc TacheMultiple ligne par ligne (pas=1), arrêt sur `C:L` vide
- **Règle `Ordre`/ligne factice** : `B` renseigné → `Ordre` = valeur ; `B` vide → `TacheMultiple` factice créée avec `EstFactice = true` et déjà validée — ne pas confondre avec une erreur
- Alias `TypeTacheMultiple.Code` (`R9` → `TM_PROC_MAD`/`TM_PROC_REL`) — valeur distincte de `TypeElementNom` de l'Équipement parent, ne pas fusionner les deux dans le code
- **Cas `R9 = "REL"`** : les tâches REL sont des lignes du même bloc répétitif PROCEDURE (pas=1), dans le même fichier MAD — pas de fichier Excel de dossier REL distinct. À vérifier que ce chemin (`TM_PROC_REL`) est bien exercé par au moins un test réel (fixture ou test unitaire dédié), pas seulement théoriquement supporté par l'alias.
- **Rejet fichier entier** si `M2:O2` vide/invalide ou date de révision illisible — vérifie qu'aucune extraction des 5 autres feuilles n'est tentée dans ce cas (orchestrateur, Lot D)

### C2. ISOLEMENT
Pas=7, repère composé `{K6:T6}-{Identification}`.

Points créés, **sans condition sur `TypeElement`** :
- `"PROLOCK VANNES"` — pour tout Isolement extrait de la feuille
- `"DEPROLOCK VANNES"` — pour tout Isolement extrait de la feuille
- `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` — conditionné à `TypeElement = "ZERO ENERGIE"` (seule condition de cette feuille)

Champ `H20:O21` (Position MAD) → `IsolementPivot.PositionALaPose`, destiné à alimenter la colonne cible `"POSITION A LA POSE"`.

**Cas `"VANNE"`** : un Isolement avec `TypeElement = "VANNE"` (valeur absente du référentiel OXO) est extrait normalement (pas de rejet de bloc), ne déclenche aucun des 3 Points ci-dessus, et produit un avertissement non bloquant dans `ImportResult.Errors`. Testé sur la fixture réelle `D8570` qui contient ce cas.

### C3. PLATINES / C4. ORIFICES CAPACITES
**Implémentation réelle : un seul service partagé**, `UnconditionalIsolementSheetExtractionService`
(`src/ExcelETL.Application/Extraction/Oxo/`) — renommé depuis `PlatinesExtractionService` une fois
confirmé que les deux feuilles ont une structure byte-identique (même `K6:U6`, même pas, mêmes
offsets), instancié une seule fois et appelé deux fois avec un `SheetExtractionRule` différent (un
par feuille). Ce document conserve la description business par feuille ci-dessous (C3/C4 restent
deux règles métier distinctes, seule leur implémentation technique a fusionné) — voir `CLAUDE.md`
("Lot C3/C4") pour le détail du renommage.

Pas=8 pour les deux feuilles.

**C3 — PLATINES**, types réels `"PLATINE"`/`"TAMPON PLEIN"` (confirmés en base OXO, `Code` `PT`/`TP`).

Points créés — **variantes `DEBUT` uniquement** : `"POSE ÉTIQUETTES"`, `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`, `"RECEPTION DEBUT MAD"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"`, `"RECEPTION DEBUT REL"`, `"PLATINES / TAMPONS PLEINS"`.

Les variantes `FIN` restent **volontairement exclues** — position business assumée malgré un écart observé dans un fichier cible de test, pas un oubli. Ne pas les ajouter sans nouvelle décision client explicite.

**C4 — ORIFICES CAPACITES**, type réel `"TROU D'HOMME"` (seule valeur observée, confirmée en base OXO), mêmes 4 Colonnes de Points systématiques (pas de condition).

### C5. AUTRES JOINTS TOUCHES
Pas=7, types réels `"TUYAUTERIE"` (défaut) et `"TUBING"` (exclusion) — condition `POSE ÉTIQUETTES` créé seulement si `TypeElement ≠ "TUBING"`.

### C6. DIVERS
Pas=3, `loc1` lu en `B6:E6` (valeur brute, pas de transformation), 4 conditions de Points sur `TypeElement` (`INSTRUMENTATION`/`ZERO ENERGIE`/`SOUPAPE` ×2/`POINT FEU` ×3) — littérale `"POINT FEU"` (pas `"POINT DE FEU"`), conformément à la décision client. Un test avec la variante réelle `"POINT DE FEU"` (observée dans un fichier fixture) documente le comportement attendu (aucun Point créé, avertissement non bloquant, pas d'erreur).

---

## Lot D — Application : orchestration du pipeline complet ✅ terminé

### D1. `ImportPipelineOrchestrator`
Orchestre C1→C6 :
1. Exécute PROCEDURE en premier ; si échec → retourne immédiatement un `ImportResult` avec une seule `ExtractionError` bloquante, sans exécuter les 5 autres feuilles
2. Sinon, exécute ISOLEMENT/PLATINES/ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS, agrège tous les `IsolementPivot`/`PointPivot`/`TacheMultiplePivot`/`ExtractionError`
3. Applique le broadcast `loc1` (extrait dans DIVERS) à l'`EquipementPivot` et à **tous** les `IsolementPivot` du run
4. Retourne l'`ImportResult` final

### D2. Tests d'intégration contre les 3 fichiers réels
Un test par fichier (C7401, D8570, G6306B), assertions sur le nombre d'Isolements extraits par feuille, quelques valeurs connues (repère, `loc1` appliqué partout), absence d'erreurs bloquantes sur ces 3 fichiers a priori valides. Le fichier `D8570` vérifie spécifiquement la présence d'un avertissement non bloquant pour son isolement `"VANNE"`.

---

## Lot E — Infrastructure : persistance `ImportProfile` ✅ terminé

### E1. `ClosedXmlWorkbookReader : IWorkbookReader`
Implémentation réelle avec ClosedXML.

### E2. Persistance EF Core d'`ImportProfile`

**Interface `IImportProfileStore` (Application layer)**

```csharp
public interface IImportProfileStore
{
    Task<IReadOnlyList<ImportProfile>> GetAllAsync(CancellationToken ct = default);
    Task<ImportProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(ImportProfile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```
**Dossier** : `src/ExcelETL.Application/Extraction/IImportProfileStore.cs`.

**`EfImportProfileStore` (Infrastructure layer)** — contre le vrai provider EF Core InMemory (jamais mocké) :
- CRUD complet, y compris persistance des collections imbriquées (`SheetExtractionRule`, `ConditionalPointRule`, `UnconditionalColonneNames`)
- `EquipementTypeElementNom` bien persisté et relu (garde-fou anti-hardcoding, à préserver — un oubli de mapping EF Core sur ce champ pourrait silencieusement le laisser `null` en base)
- Configuration EF Core (`ImportProfileConfiguration`, `SheetExtractionRuleConfiguration`) dans `src/ExcelETL.Infrastructure/Persistence/Configurations/`, migration associée

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Repositories/EfImportProfileStore.cs`.

---

## Ce que ce découpage ne couvre pas (rappel)
- Écriture du fichier `.xlsx` cible (bloqué sur le format client)
- Logs applicatifs upload/egress/hash — toujours hors périmètre de ce document (concernent le jour où le pipeline OXO sera exposé en Web API). Le logging **extraction** (start/end du run, chaque `ExtractionError`) est en revanche couvert : voir Lot G dans `CLAUDE.md`, qui réutilise le sink Serilog → `SystemLogs` déjà en place plutôt qu'une nouvelle table dédiée.
- Retrait du POC (`ExtractionConfig`/`Mappings.razor`/`UploadTest.razor`) — décision actée, pas encore exécutée
