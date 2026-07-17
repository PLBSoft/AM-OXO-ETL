# Tickets TDD — Pipeline d'extraction AM-OXO-ETL

*2026-07-16. Découpage à partir de `modele-domaine-import-profile-2026-07-16.md` et `spec-extraction-fichier-source-oxo-2026-07-16.md`. Périmètre : extraction `.xlsx` source → objet pivot en mémoire (+ rapport d'erreurs). L'écriture du `.xlsx` cible est **hors périmètre**, en attente du format exact côté client.*

> **Mise à jour 2026-07-16 (v6)** : C2 (ISOLEMENT) et C3 (PLATINES) débloqués suite aux réponses
> client sur les priorités 1 — `"VANNE"` confirmé absent de la base OXO (traité par la politique
> d'erreur non bloquante, aucune règle spécifique), `"PROLOCK VANNES"`/`"DEPROLOCK VANNES"`
> confirmés inconditionnels, variantes DEB/FIN de PLATINES tranchées (retour à `DEBUT`
> uniquement). A3 ajusté pour couvrir le nouveau champ `ImportProfile.EquipementTypeElementNom`
> (voir `modele-domaine-import-profile-2026-07-16.md` v2).

**Conventions à respecter** (voir `etat-des-lieux-technique.md`) : xUnit 2.9.3 + FluentAssertions 7.0.0 (jamais `Assert.*`) + Moq ; un projet de test par projet source, miroir dossier-par-dossier ; exceptions typées avec `ErrorCode` (`DomainValidationException`/`ApplicationValidationException` selon la couche) ; organisation par feature, pas par type technique ; `IDbContextFactory<T>` si persistance EF nécessaire.

**Fichiers de test réels disponibles** : `Dossier_de_MaD_IDL_-_C7401.xlsx`, `..._D8570_chgt_plateaux.xlsx`, `..._G6306B_REV.xlsx` — à utiliser comme fixtures d'intégration dès que possible plutôt que des fichiers synthétiques, pour éviter de valider contre des hypothèses au lieu de la réalité.

**Séquencement proposé** : les lots A→D sont strictement séquentiels (chaque lot dépend du précédent). Le lot E (persistance `ImportProfile`) peut être différé — proposition : démarrer avec un `ImportProfile` codé en dur en mémoire (Domain) pour valider les règles métier des lots A-D plus vite, brancher la persistance EF Core ensuite sans toucher au moteur.

---

## Lot A — Domain : primitives et modèle pivot (zéro dépendance)

### A1. Primitives de localisation et de transformation
**Tests d'abord** : construction et égalité structurelle (records) de `DirectCell`, `RepeatingBlockLocator`, `BlockFieldDefinition`, `TextTransform` (`RawValue`/`SubstringAfter`/`Concat`/`ConcatPart`), `ConditionalPointRule`/`ConditionOperator`. Valider les invariants de construction (ex. `Step > 0`, `RowOffsetEnd >= RowOffsetStart`, `ColumnRange` au format attendu) via `DomainValidationException` + `DomainErrorCode` dédié, cohérent avec la convention déjà en place.
**Dossier** : `src/ExcelETL.Domain/Extraction/Primitives/` (+ miroir tests).

### A2. Modèle pivot
**Tests d'abord** : construction de `EquipementPivot`, `IsolementPivot` (dont le champ `PositionALaPose`, voir modèle de domaine v2), `PointPivot`, `TacheMultiplePivot`, `ExtractionError`/`ExtractionErrorCode`, `ImportResult` (dont `HasErrors`).
**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/`.

### A3. `ImportProfile` / `SheetExtractionRule` (entités Domain, sans EF pour l'instant)
**Tests d'abord** : constructeurs qui valident (nom de feuille non vide, préfixe repère non vide, au moins une `SheetExtractionRule`), cohérent avec le style "entités riches" déjà en place (`AddSheet`, etc. dans le POC existant).

**Ajout (v6)** : `EquipementTypeElementNom` non vide/blanc → `DomainValidationException` sinon, même pattern que le nom de feuille et le préfixe repère (voir `modele-domaine-import-profile-2026-07-16.md` v2, §2.1). Ce champ porte la valeur `TypeElement.Nom` de l'Équipement parent (ex. `"MAD TRAVAUX"` pour un profil MAD) — **il ne doit jamais être une constante dans le code du service d'extraction**, uniquement lu depuis le profil actif.

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/`.

---

## Lot B — Application : moteur d'exécution générique

### B1. Abstraction `IWorkbookReader` (interface uniquement, Application)
Méthodes minimales : `string? ReadCellValue(string sheet, string range)`, `bool SheetExists(string sheet)`. Pas d'implémentation ici — juste l'interface + un `Mock<IWorkbookReader>` réutilisable pour tous les tests du lot B.

### B2. Évaluateur de `TextTransform`
**Tests d'abord** : `RawValue` retourne tel quel ; `SubstringAfter` retire le préfixe (cas nominal, cas préfixe absent → valeur inchangée ou erreur ? **à trancher pendant ce ticket**, proposer `ExtractionError` si le préfixe attendu est absent plutôt qu'un throw, cohérent avec 3.2 du modèle) ; `Concat` mixe littéraux et références de champs déjà extraits.

### B3. Moteur `RepeatingBlockLocator` (le cœur générique)
**Tests d'abord**, contre un `Mock<IWorkbookReader>` (pas de vrai fichier ici — c'est le rôle du lot D) :
- Lecture bloc par bloc avec le bon calcul de plage (`FirstBlockStartRow + i*Step + Offset`)
- Arrêt correct dès que le champ `StopFieldName` est vide (ne doit pas lire un bloc de trop)
- Cas pas=1 (PROCEDURE) et pas=3/7/8 (feuilles isolement) — paramétrer le même test générique avec `[Theory]`/`[InlineData]` pour les 5 valeurs de pas déjà confirmées, plutôt que dupliquer le test par feuille
- Bloc partiellement vide (un champ non-`StopField` vide alors que `StopField` est renseigné) → `ExtractionError`, bloc ignoré, lecture continue au bloc suivant (politique confirmée §3 du modèle de domaine)

### B4. Évaluateur `ConditionalPointRule`
**Tests d'abord** : `Equals`/`NotEquals` sur une valeur de champ déjà extraite ; liste vide de règles = toujours créer le Point ; valeur ne matchant aucune règle = pas de Point créé + avertissement non bloquant ajouté à `ImportResult.Errors` (§3.2 du modèle de domaine — bien vérifier que ce n'est **pas** un rejet de bloc). Comparaison insensible à la casse et `.Trim()` (voir spec §7).

---

## Lot C — Application : services d'extraction par feuille (règles métier)

*Chaque ticket teste sa feuille contre les 3 fichiers réels (fixtures d'intégration) en plus des tests unitaires contre `Mock<IWorkbookReader>`. Un service par feuille, dans `Extraction/{NomFeuille}/`.*

### C1. PROCEDURE — Équipement + TachesMultiples
**Tests d'abord** :
- Extraction nominale de `nomMAD` (retrait préfixe `MAD-OXO-`), `Designation` (`"Rév {P2:Q2} du {R2:T2}"`, date en `dd/mm/aaaa`)
- `EquipementPivot.TypeElementNom` = `profile.EquipementTypeElementNom` (ex. `"MAD TRAVAUX"`) — **jamais une constante dans le service**. Test explicite avec deux profils portant des valeurs `EquipementTypeElementNom` différentes, pour vérifier que le service restitue bien la valeur du profil et non une valeur codée en dur (garde-fou architecture, voir modèle de domaine v2 §2.1)
- Bloc TacheMultiple ligne par ligne (pas=1), arrêt sur `C:L` vide
- **Règle `Ordre`/ligne factice** (§1.2 spec) : `B` renseigné → `Ordre` = valeur ; `B` vide → `TacheMultiple` factice créée avec `EstFactice = true` et déjà validée — **ne pas confondre avec une erreur**
- Alias `TypeTacheMultiple.Code` (`R9` → `TM_PROC_MAD`/`TM_PROC_REL`) — **valeur distincte** de `TypeElementNom` de l'Équipement parent, ne pas fusionner les deux dans le code
- **Rejet fichier entier** si `M2:O2` vide/invalide ou date de révision illisible (§3.1 confirmé) — ce test doit vérifier qu'aucune extraction des 5 autres feuilles n'est tentée dans ce cas (voir orchestrateur, lot D)

### C2. ISOLEMENT
**Tests d'abord** : pas=7, repère composé `{K6:T6}-{Identification}`.

Points créés, **sans condition sur `TypeElement`** (confirmé 2026-07-16) :
- `"PROLOCK VANNES"` — pour tout Isolement extrait de la feuille
- `"DEPROLOCK VANNES"` — pour tout Isolement extrait de la feuille
- `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` — conditionné à `TypeElement = "ZERO ENERGIE"` (seule condition de cette feuille)

Nouveau champ à extraire : `H20:O21` (Position MAD) → `IsolementPivot.PositionALaPose`, destiné à alimenter la colonne cible `"POSITION A LA POSE"` (écriture cible hors périmètre de ce lot, mais le champ doit être présent dans le pivot dès ce ticket).

**Test explicite requis (cas `"VANNE"`)** : un Isolement avec `TypeElement = "VANNE"` (valeur absente du référentiel OXO — confirmé 2026-07-16, voir glossaire) doit être extrait normalement (pas de rejet de bloc), ne déclencher aucun des 3 Points ci-dessus (aucune règle ne matche), et produire un avertissement non bloquant dans `ImportResult.Errors` — comportement identique à celui déjà prévu pour C4/C6 sur les types non reconnus. Utiliser la fixture réelle `D8570` qui contient ce cas plutôt qu'une donnée synthétique.

### C3. PLATINES
**Tests d'abord** : pas=8, types réels `"PLATINE"`/`"TAMPON PLEIN"` (confirmés en base OXO, `Code` `PT`/`TP`).

Points créés — **variantes `DEBUT` uniquement, confirmé (2026-07-16, retour à la spécification initiale)** :
- `"POSE ÉTIQUETTES"`
- `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`
- `"CONTRÔLE ETANCHÉITÉS"`
- `"RECEPTION DEBUT MAD"`
- `"RÉCEPTION PLATINES/TAMPONS PLEINS"`
- `"RECEPTION DEBUT REL"`
- `"PLATINES / TAMPONS PLEINS"`

Les variantes `FIN` (`"FIN MAD Réception Platines/Tampons pleins"`, etc.) restent **volontairement exclues** — ne pas les créer, ne pas écrire de test qui les couvrirait comme cas nominal. Un commentaire dans le code de test (et dans le profil documenté) doit rappeler que cette décision est une position business assumée malgré un écart observé dans un fichier cible de test, pas un oubli.

### C4. ORIFICES CAPACITES
**Tests d'abord** : pas=8, type réel `"TROU D'HOMME"` (seule valeur observée, confirmée en base OXO), mêmes 4 Colonnes de Points systématiques (pas de condition).

### C5. AUTRES JOINTS TOUCHES
**Tests d'abord** : pas=7 (confirmé, pas 3), types réels `"TUYAUTERIE"` (défaut) et `"TUBING"` (exclusion) — condition `POSE ÉTIQUETTES` créé seulement si `TypeElement ≠ "TUBING"`.

### C6. DIVERS
**Tests d'abord** : pas=3, `loc1` lu en `B6:E6` (valeur brute, pas de transformation), 4 conditions de Points sur `TypeElement` (`INSTRUMENTATION`/`ZERO ENERGIE`/`SOUPAPE` ×2/`POINT FEU` ×3) — bien utiliser la littérale `"POINT FEU"` (pas `"POINT DE FEU"`) conformément à la décision client. ⚠️ Prévoir un test avec la variante réelle `"POINT DE FEU"` (observée dans un fichier fixture) pour documenter explicitement le comportement attendu (aucun Point créé, avertissement non bloquant, pas d'erreur) plutôt que de découvrir ce cas en production.

---

## Lot D — Application : orchestration du pipeline complet

### D1. `ImportPipelineOrchestrator` (ou nom similaire)
**Tests d'abord**, orchestrant C1→C6 :
1. Exécute PROCEDURE en premier ; si échec → retourne immédiatement un `ImportResult` avec une seule `ExtractionError` bloquante, **sans exécuter les 5 autres feuilles** (vérifier par `Mock` que les services des 5 autres feuilles ne sont jamais appelés dans ce cas — test de comportement, pas seulement de résultat)
2. Sinon, exécute ISOLEMENT/PLATINES/ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS, agrège tous les `IsolementPivot`/`PointPivot`/`TacheMultiplePivot`/`ExtractionError`
3. Applique le broadcast `loc1` (extrait dans DIVERS) à l'`EquipementPivot` et à **tous** les `IsolementPivot` du run
4. Retourne l'`ImportResult` final

### D2. Tests d'intégration contre les 3 fichiers réels
`ImportPipelineOrchestratorIntegrationTests` — un test par fichier (C7401, D8570, G6306B), assertions sur : nombre d'Isolements extraits par feuille, quelques valeurs connues (ex. repère, `loc1` appliqué partout), absence d'erreurs bloquantes sur ces 3 fichiers a priori valides. Sert aussi de garde-fou de non-régression pour la suite (écriture du fichier cible, futurs profils). Le fichier `D8570` doit spécifiquement vérifier la présence d'un avertissement non bloquant pour son isolement `"VANNE"` (voir C2).

---

## Lot E — Infrastructure (peut démarrer en parallèle du lot C une fois le lot B stable)

### E1. `ClosedXmlWorkbookReader : IWorkbookReader`
Implémentation réelle avec ClosedXML. Tests contre de petits classeurs `.xlsx` construits en mémoire dans le test (pas de mock ici, comportement réel de ClosedXML sur cellules fusionnées).

### E2. (Différé, optionnel pour démarrer plus vite) Persistance EF Core d'`ImportProfile`
`ExtractionProfileConfiguration`, repository via `IDbContextFactory`, migration — même pattern que l'existant (`ExtractionConfigRepository`). Peut être fait après validation complète des lots A-D avec un profil codé en dur, pour ne pas bloquer sur la persistance pendant qu'on stabilise les règles métier.

---

## Ce que ce découpage ne couvre pas (rappel)
- Écriture du fichier `.xlsx` cible (bloqué sur le format client)
- Écran Blazor de construction/test de profil (bouton "tester sur fichier exemple")
- Logs applicatifs (upload, egress, hash) — hors périmètre extraction pure
