# Tickets TDD — Lot M : seed des profils d'import/export par défaut

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Dépend
entièrement des Lots E (`IImportProfileStore`/`EfImportProfileStore`) et I
(`IExportProfileStore`/`EfExportProfileStore`), tous deux terminés — ce lot n'ajoute aucune
capacité de persistance nouvelle, seulement des données de référence semées au démarrage.*

**Demande client** : les profils d'import et d'export standards OXO doivent être présents par
défaut à chaque déploiement de la solution, exactement comme les comptes admin créés par
`IdentitySeeder` — pas une étape manuelle post-déploiement.

**Conventions déjà en place à respecter** (voir `etat-des-lieux-technique.md` §4-5) :
- `IdentitySeeder` (scoped, exécuté à chaque démarrage `BlazorAdmin`, idempotent) est le patron de
  référence pour ce lot — même style de composant, même emplacement conceptuel (Infrastructure),
  même mécanisme d'activation/désactivation par config pour les tests d'intégration
  (`IdentitySeeding:Enabled`, voir `WebApplicationFactory<Program>` dans les tests WebAPI).
- Accès EF Core exclusivement via `IDbContextFactory<T>`, jamais de `DbContext` scoped injecté
  directement dans une méthode métier (sauf le cas Identity déjà documenté, qui ne concerne pas ce
  lot).
- xUnit 2.9.3 + FluentAssertions 7.0.0 (jamais `Assert.*`) + Moq ; repository/store adossé à un
  `DbContext` → testé contre le **vrai provider EF Core InMemory**, jamais mocké (même pattern que
  `EfImportProfileStoreTests`/`EfExportProfileStoreTests`).
- Entités Domain riches déjà validées à la construction (`ImportProfile`, `SheetExtractionRule`,
  `ExportProfile`, `SheetGenerationRule`) — le seeder construit les vrais objets Domain, il ne
  contourne aucune validation existante.

---

## M1. `DefaultProfileSeeder` (Infrastructure) — construction et persistance idempotente

**Comportement attendu**, miroir de `IdentitySeeder` :

**Décisions actées avec le client** : seeding **uniquement côté `BlazorAdmin`** (seul host
propriétaire de la persistance des profils aujourd'hui, symétrique de `IdentitySeeder`) ; le
seeder **ne réécrase jamais** un profil existant, même modifié par un admin — idempotence stricte,
identique au comportement `IdentitySeeder` sur les comptes.

1. Composant scoped, exécuté à chaque démarrage de `BlazorAdmin` (`Program.cs`, à la suite de
   l'appel à `IdentitySeeder`), désactivable par un flag de config (`ProfileSeeding:Enabled`,
   même mécanisme que `IdentitySeeding:Enabled`) pour ne pas exiger un vrai SQL Server dans les
   tests d'intégration WebAPI/BlazorAdmin existants.
2. Vérifie l'existence du profil d'import par défaut et du profil d'export par défaut **via un
   identifiant stable** (`Guid` constant connu du seeder — ex. `DefaultProfileSeeder.ImportProfileId`/
   `ExportProfileId`, sur le modèle d'une constante de classe, jamais généré dynamiquement) plutôt
   que par recherche sur le nom seul — un nom est modifiable par un admin, un identifiant stable
   ne l'est pas.
3. Si absent (recherche par cet identifiant stable via `GetByIdAsync`) → construit et persiste
   l'objet Domain réel (`IImportProfileStore.SaveAsync`, `IExportProfileStore.SaveAsync` — les
   deux font déjà de l'upsert par `Id`, voir Lots E2/I6).
4. Si présent → **ne touche à rien**, quel que soit son contenu actuel (comportement
   `IdentitySeeder` : un compte déjà existant n'est pas réinitialisé). Un profil par défaut
   modifié par un admin après coup reste modifié ; le seeder ne l'écrase jamais silencieusement à
   chaque redémarrage — décision explicitement confirmée par le client.

**Dossier proposé** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs` (nouveau
sous-dossier `Seeding/`, cohérent avec l'organisation par feature déjà en place — `IdentitySeeder`
reste dans `Identity/` car antérieur à cette convention, pas à déplacer dans ce lot).

**Tests** (contre EF Core InMemory réel, pas de mock) :
- Base vide → les deux profils sont créés, avec les valeurs exactes attendues (voir M2/M3).
- Seeder exécuté deux fois de suite → un seul profil de chaque type en base (pas de doublon),
  assertion sur `GetAllAsync().Count`.
- Profil par défaut déjà présent mais **modifié** (ex. nom changé par un admin) → le seeder ne le
  réécrase pas ; relance du seeder puis `GetByIdAsync` renvoie toujours la version modifiée.
- Flag `ProfileSeeding:Enabled = false` → aucun profil créé, même sur base vide (test d'intégration
  minimal, cohérent avec le mécanisme `IdentitySeeding:Enabled` existant).

---

## M2. Contenu du profil d'import par défaut

Construit avec les 3 champs racine déjà connus :
- `Name` = **`"Profil OXO standard"`** (validé avec le client).
- `RepereePrefix` = `ImportProfile.DefaultReperePrefix` (déjà `"MAD-OXO-"` dans le Domain — ne pas
  redupliquer cette constante dans le seeder, la référencer).
- `EquipementTypeElementNom` = `"MAD TRAVAUX"` (seule valeur confirmée en base OXO, voir
  `glossaire-ef6-legacy-AMAR-ModelCF.md`) — **seul champ du profil pour lequel une valeur métier
  est codée en dur dans ce ticket**, ce qui est cohérent avec le modèle de domaine
  (`EquipementTypeElementNom` n'a justement pas de défaut interne, voir
  `modele-domaine-import-profile.md` §2.1 — c'est au seeder de la fournir).

**Source de vérité pour les coordonnées** : `spec-extraction-fichier-source-oxo.md` (document
vivant, toutes les incertitudes tranchées). Coordonnées ci-dessous transcrites directement depuis
ce document — Claude Code doit néanmoins vérifier rapidement, en lisant les services
d'extraction existants et leurs tests, que les **noms de champ littéraux** utilisés dans le code
(`BlockFieldDefinition.Name`, `ConditionalPointRule.SourceFieldName`) correspondent bien à ceux
supposés ci-dessous (`"TypeElement"` est confirmé par le modèle de domaine, les autres noms de
champ — `"Identification"`, `"Designation"`, etc. — sont une hypothèse de nommage cohérente avec
la spec, à confirmer contre le code réel avant de figer le seeder). Ce n'est plus un blocage,
juste un point de contrôle rapide avant implémentation.

6 `SheetExtractionRule` :

1. **`"PROCEDURE"`** — bloc répétitif Tâches multiples à partir de la ligne 9, pas = 1, arrêt
   quand `C:L` est vide. Champs de bloc : `B` (Ordre), `C:L` (Action), `M:N` (Acteur), `O:Q`
   (Risques), `R` (alias `TypeTacheMultiple.Code`), `T:U` (DateValidation) — les 6 littéraux
   confirmés mot pour mot contre `ProcedureExtractionServiceTests.cs` (`CreateSheetRule`). Pas de
   `UnconditionalColonneNames`/`ConditionalPointRule` au sens Isolement — les Points de cette
   feuille sont liés aux `Tableau` `"TRAVAUX COMPLET"`/`"TRAVAUX DETAIL"`, un mécanisme distinct
   des `Colonne.Nom` par Isolement. **Confirmé hardcodé, indépendant du profil** :
   `ProcedureExtractionService.cs:25-26,64-68` construit ces deux `PointPivot` de façon
   inconditionnelle à partir de deux `private const string` du service — aucun
   `SheetExtractionRule.PointRules`/`UnconditionalColonneNames` n'intervient. **De même pour les
   cellules d'en-tête** `M2:O2`/`P2:Q2`/`R2:T2` (repère, numéro de révision, date de révision) :
   `ProcedureExtractionService.cs:38,53,60` les lit en dur (littéraux de cellule passés directement
   à `ReadCellValue`), pas via `sheetRule.Locator` ou tout autre champ porté par le profil. **Pour
   le seeder : le `SheetExtractionRule` de PROCEDURE n'a besoin d'exprimer que le
   `RepeatingBlockLocator` des 6 champs de bloc ci-dessus — ni les 3 cellules d'en-tête, ni les 2
   Points TRAVAUX ne se configurent via le profil, ils n'ont pas leur place dans les champs
   `PointRules`/`UnconditionalColonneNames` de la règle.**
2. **`"ISOLEMENT"`** — 1er enregistrement Identification en `B19:E20`, pas = 7. Repère composé
   `{K6:T6}-{Identification}`. `UnconditionalColonneNames` = `["PROLOCK VANNES", "DEPROLOCK VANNES"]`.
   `ConditionalPointRule` = `[(TypeElement, Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")]`.
3. **`"PLATINES"`** — 1er enregistrement Identification en `B17:E18`, pas = 8.
   `UnconditionalColonneNames` = `["POSE ÉTIQUETTES", "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS", "RECEPTION DEBUT MAD", "RÉCEPTION PLATINES/TAMPONS PLEINS", "RECEPTION DEBUT REL", "PLATINES / TAMPONS PLEINS"]`
   (variantes `DEBUT` uniquement, `FIN` volontairement exclues). Pas de `ConditionalPointRule`.
4. **`"ORIFICES CAPACITES"`** — 1er enregistrement Identification en `B17:E18`, pas = 8.
   `UnconditionalColonneNames` = `["POSE ÉTIQUETTES", "RÉCEPTION PLATINES/TAMPONS PLEINS", "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"]`.
   Pas de `ConditionalPointRule`.
5. **`"AUTRES JOINTS TOUCHES"`** — 1er enregistrement Identification en `B17:E18`, pas = 7.
   `UnconditionalColonneNames` = `["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"]`.
   `ConditionalPointRule` = `[(TypeElement, NotEquals, "TUBING", "POSE ÉTIQUETTES")]`.
6. **`"DIVERS"`** — `loc1` en `B6:E6` (broadcast). **Confirmé : ce n'est pas un `BlockFieldDefinition`**
   — `DiversExtractionService.cs:35` lit `B6:E6` en dur via un `ReadCellValue` isolé, hors de tout
   `RepeatingBlockLocator`, exactement comme la cellule-écho du repère Équipement (`N6`, elle-même
   déjà documentée comme divergente de la spec pour AUTRES JOINTS TOUCHES/DIVERS). Pour le seeder :
   `loc1` n'a donc pas de place dans les champs du `SheetExtractionRule` de DIVERS — c'est une
   connaissance métier interne au service, rien à porter par le profil. 1er enregistrement
   Identification en `H9:K11`, pas = 3. Pas de `UnconditionalColonneNames`. `ConditionalPointRule` =
   `[(TypeElement, Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),`
   `(TypeElement, Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE"),`
   `(TypeElement, Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),`
   `(TypeElement, Equals, "SOUPAPE", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),`
   `(TypeElement, Equals, "POINT FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),`
   `(TypeElement, Equals, "POINT FEU", "PF : VALIDATION CONSTAT ENCRASSEMENT"),`
   `(TypeElement, Equals, "POINT FEU", "PF : ACCORD TRAVAUX FEU")]`
   (littéral `"POINT FEU"`, jamais `"POINT DE FEU"` — décision client tranchée, voir spec §6).

**Tests** (Infrastructure, contre les 3 fixtures réelles) :
- Non-régression : exécuter `ImportPipelineOrchestrator` avec le profil **seedé** (récupéré via
  `IImportProfileStore`, pas construit inline dans le test) contre les 3 fichiers réels → mêmes
  assertions que `ImportPipelineOrchestratorIntegrationTests` existants (nombre d'Isolements par
  feuille, `loc1` broadcasté, avertissement non bloquant `"VANNE"` sur D8570). Si ces tests
  passent avec le profil seedé aussi bien qu'avec le profil in-memory des tests existants, la
  transcription des coordonnées est confirmée correcte.

---

## M3. Contenu du profil d'export par défaut

2 `SheetGenerationRule` :
- **`"Parents"`**, `PivotSource = Equipement` — colonnes descriptives (Repère, `TypeElement.Nom`,
  Zone/Loc2/Loc3, Désignation) + colonnes Points connues du catalogue Équipement.
- **`"Enfants"`**, `PivotSource = Isolement` — colonnes descriptives (Numéro, Type, Zone, Élément
  Parent, Désignation, Position à la pose/dépose) + colonnes Points connues du catalogue
  Isolement (mêmes noms de `Colonne` que ceux produits par les `SheetExtractionRule` du profil
  d'import M2 — cohérence à vérifier explicitement par un test, voir ci-dessous).

**Décision actée** : profil d'export **minimal** — uniquement les colonnes descriptives déjà
extraites aujourd'hui (Repère, `TypeElement.Nom`, Zone (`loc1`), Désignation, Position à la pose
côté Enfants) et les colonnes Points effectivement produites par le profil d'import M2. Les
colonnes descriptives **non encore mappées** (`FLUIDE`, `RECURRENT`, `PROGRESS`, `SUPPRESSION`,
`ADR Email`, `COMMENTAIRES` côté Parents ; `PHASE PROCESS`, `REMARQUES`, `ETIQUETTE`,
`DIAMETRE INCH`, `SERIE LBS`, `NATURE JOINT`, `BESOIN ECHAF` côté Enfants — voir
`spec-extraction-fichier-source-oxo.md` §"Fichier cible" et §9) **ne sont pas incluses** dans le
profil par défaut. Un admin les ajoutera une à une via `/export-profiles/{id}/edit` (avec
`Source = null`, "non mappée pour l'instant") le jour où une règle d'extraction existera pour
elles — cohérent avec le principe déjà en place de ne jamais préremplir un champ dont la valeur
n'est pas encore fournie par le profil d'import correspondant.

**Tests** :
- Non-régression : `SheetGenerationEngine` + profil d'export **seedé** contre un `ImportResult`
  réel (une des 3 fixtures, via le pipeline d'import déjà validé) → en-têtes de colonnes générés
  correspondent exactement aux noms attendus du fichier cible (`spec-extraction-fichier-source-oxo.md`).
- Cohérence croisée import/export : chaque `Colonne.Nom` référencé par une `PointColumnDefinition`
  du profil d'export existe bien parmi les `UnconditionalColonneNames`/`ConditionalPointRule.ColonneName`
  du profil d'import correspondant — test dédié qui compare les deux profils seedés entre eux,
  pour détecter tout écart de nommage entre les deux catalogues qui laisserait une colonne
  générée toujours vide en pratique.

---

## Hors périmètre explicite de ce lot
- Bouton Blazor "réinitialiser aux profils par défaut" (pas demandé ; à discuter séparément si
  utile une fois ce lot livré).
- Migration des profils déjà en base sur un environnement existant (ce lot concerne un
  déploiement, la question ne se pose que si un environnement a déjà des profils créés
  manuellement avant ce lot).
- Toute modification du contenu des profils une fois semés — ce lot ne fait que les créer une
  fois, l'édition normale via `/import-profiles/{id}/edit` et `/export-profiles/{id}/edit` reste
  le seul mécanisme de modification ensuite.

---

## Décisions actées (validées avec le client)

1. **Nommage** : `"Profil OXO standard"` pour les deux profils (import et export).
2. **Idempotence stricte** : le seeder ne réécrase jamais un profil existant, quel que soit son
   contenu — recherche par identifiant stable, création uniquement si absent.
3. **Export minimal** : seulement les colonnes déjà extraites aujourd'hui ; pas de colonnes
   `Source = null` en anticipation.
4. **Seeding `BlazorAdmin` uniquement**, pour ce lot.
5. **Source des coordonnées** : `spec-extraction-fichier-source-oxo.md`, confirmée suffisante et
   transcrite dans la section M2 ci-dessus.

## Dernier point de vérification avant implémentation — FAIT, aucune divergence

Vérification effectuée par lecture directe des 5 services d'extraction (`ProcedureExtractionService`,
`IsolementExtractionService`, `UnconditionalIsolementSheetExtractionService`,
`AutresJointsTouchesExtractionService`, `DiversExtractionService`) et de leurs tests, y compris
`OrificesCapacitesExtractionServiceIntegrationTests` (Infrastructure.Tests) pour la feuille
ORIFICES CAPACITES, non couverte par un test Application dédié puisqu'elle réutilise le service
PLATINES tel quel. **Tous les littéraux transcrits en M2 (noms de champ, plages de cellules,
`ComparisonValue` des 7 règles DIVERS) correspondent mot pour mot au code réel — zéro divergence.**

Deux points structurants confirmés au passage et déjà reportés dans les sections correspondantes
ci-dessus (pas de nouvelle information au-delà de ce qui est déjà écrit en M2 §1 et §6, ce
paragraphe ne fait que résumer où chercher) :
- PROCEDURE : cellules d'en-tête (`M2:O2`/`P2:Q2`/`R2:T2`) et Points `"TRAVAUX COMPLET"`/
  `"TRAVAUX DETAIL"` sont hardcodés dans le service, hors profil.
- DIVERS : `loc1` (`B6:E6`) est un `ReadCellValue` isolé, hors profil, pas un `BlockFieldDefinition`.

Le seeder peut être implémenté directement à partir du contenu de M2/M3 tel qu'écrit dans ce
document, sans relecture de code supplémentaire.
