# État des lieux technique global — vérification indépendante (2026-07-24)

*Basé exclusivement sur une lecture directe du code sur `main` au commit `d018a90`
(2026-07-24), `git log`, exécution réelle de la suite de tests, requêtage direct de la base
SQL Server LocalDB, et recherche exhaustive dans `src/`/`tests/`/`legacy/`/`.claude/`. Aucune
affirmation de `CLAUDE.md` n'a été acceptée sans confrontation directe au code. Référence de
départ : `docs/etat-avancement-global-2026-07-22.md` (dernier commit couvert : `7a7c414`,
624/624 tests slnx + 15/15 legacy hors-solution annoncés).*

---

## 1. Suite de tests complète

**Exécution réelle, `dotnet test ExcelETL.slnx` à la racine, aujourd'hui :**

```
ExcelETL.Domain.Tests.dll         : 264/264 réussis
ExcelETL.Application.Tests.dll    : 121/121 réussis
ExcelETL.Infrastructure.Tests.dll : 121/121 réussis
ExcelETL.WebAPI.Tests.dll         :  13/13  réussis
ExcelETL.BlazorAdmin.Tests.dll    : 278/278 réussis
ExcelETL.Hosting.Tests.dll        :   6/6   réussis
Legacy.NewApiPingService.Tests    :   9/9   réussis (net48)
```

**Total via `ExcelETL.slnx` : 812/812 verts.** Aucun incident de build cette fois-ci (le
verrouillage MSB3027/MSB3021 rencontré le 22/07 ne s'est pas reproduit).

### Comparaison avec le 624/624 du 22/07

| Projet | 22/07 | 24/07 | Écart | Explication |
|---|---|---|---|---|
| Domain.Tests | 215 | 264 | **+49** | Lot U1/U2 (`ImportProfile.DefaultTableaux`/`DefaultApplicationNames`, `EquipementPivot`/`IsolementPivot.Tableaux`/`Applications`/`RepereParent`), Lot U4 (`ApplicationColumnDefinition`, invariants `SheetGenerationRule`), Lot T1/T2 (`PivotSource.TacheMultiple`, 5 membres `PivotFieldRef`). Chiffre final identique à celui déjà annoncé dans `CLAUDE.md` ("Domain 264"). |
| Application.Tests | 94 | 121 | +27 | Lot T3 (`ExcelSheetNameSanitizer`, regroupement dynamique par `TypeTacheMultipleCode`), Lot U3 (retrait des consts PROCEDURE, diffusion Tableaux/Applications), Lot U5 (colonnes Application dans `SheetGenerationEngine`). Cohérent avec "Application 121" dans `CLAUDE.md`. |
| Infrastructure.Tests | 113 | 121 | +8 | Lot T5/T8 (seeding + migration additive de la règle "Tâches multiples"), Lot U6 (contenu enrichi des profils seedés). Cohérent avec "Infrastructure 121". |
| WebAPI.Tests | 13 | 13 | 0 | Inchangé — aucun lot depuis le 22/07 n'a touché la couche WebAPI. |
| BlazorAdmin.Tests | 174 | 278 | **+104** | Somme de : correctif/labels/édition ImportProfileEditor (post-22/07), Lot Q (parité visuelle+fonctionnelle ExportProfileEditor), bouton retour sur les pages de test, correctifs upload/accordéons `ImportProfileTest`/`ExportProfileTest`, Lot T6/T7 (UI TacheMultiple), Lot R (densification), Lot S (réorganisation NavMenu), correctif accordéon R3, Lot V1→V13 (passe mobile-first complète), Lot U1 (UI Tableaux/Applications), Lot W (édition/suppression `UnconditionalColonneNames`/`ConditionalPointRule`). Chiffre final identique à "BlazorAdmin 278" — dernière valeur annoncée dans `CLAUDE.md`. |
| Hosting.Tests | 6 | 6 | 0 | Inchangé depuis Lot G3. |
| Legacy (NewApiPingService) | 9 | 9 | 0 | Inchangé. |
| **Total (slnx)** | **624** | **812** | +188 | Somme des écarts ci-dessus. |

**Tous les sous-totaux par projet cités dans `CLAUDE.md`** (Domain 264, Application 121,
Infrastructure 121, BlazorAdmin 278) **correspondent exactement à l'exécution réelle
d'aujourd'hui.** Aucun écart trouvé.

### Écart signalé le 22/07, toujours non corrigé : `ExcelProcessingClientService.Tests` absent de `ExcelETL.slnx`

Revérifié aujourd'hui : le projet `legacy/ExcelProcessingClientService.Tests/` existe toujours
sur disque, `ExcelETL.slnx` ne référence toujours que `ExcelETL.Hosting`/`ExcelETL.Hosting.Tests`
et pas ce second projet legacy (`grep` direct du fichier `.slnx`, confirmé). Exécuté séparément :

```
dotnet test legacy/ExcelProcessingClientService.Tests/Legacy.ExcelProcessingClientService.Tests.csproj
→ Legacy.ExcelProcessingClientService.Tests.dll : 15/15 réussis
```

**Total réel du dépôt, toutes suites confondues : 812 (slnx) + 15 (hors solution) = 827 tests
verts**, dont 15 toujours exécutés par aucune commande `dotnet test ExcelETL.slnx`. Aucun commit
entre le 22/07 et aujourd'hui ne touche `ExcelETL.slnx` (vérifié via `git log -- ExcelETL.slnx`,
dernier commit sur ce fichier : `3982f3f`, 2026-07-20, ajout de `Hosting`). **Ce point reste donc
un vrai trou non traité, identique à celui déjà signalé il y a deux jours.**

---

## 2. Items ouverts du rapport du 22/07 — statut aujourd'hui

| Item (§ du rapport 22/07) | Statut au 22/07 | Statut réel au 24/07 |
|---|---|---|
| §2 — 3 commentaires de code obsolètes (`OxoController.cs`, `ProcessOxoFileService.cs`, `ClosedXmlWorkbookWriter.cs`) référant des classes supprimées par Lot K4 | Signalé, cosmétique | ✅ **Corrigé** — commit `596ea46` ("Remove stale comment references to the deleted POC pipeline", 2026-07-23), vérifié par lecture directe des 3 fichiers : les références à `ExcelController`/`ProcessExcelFileService`/`ClosedXmlGeneratorService` ont disparu, le reste des commentaires (toujours exact) est conservé. |
| §7.2 — `ExcelProcessingClientService.Tests` absent de `ExcelETL.slnx` | Signalé, jamais traité avant le 22/07 | ⚠️ **Toujours ouvert** — voir §1 ci-dessus. Aucune action depuis. |
| §7.3 — Application réelle des migrations EF Core sur une vraie base SQL Server, non vérifiable le 22/07 (serveur injoignable) | Non vérifiable | ⚠️ **Vérifiable aujourd'hui, et révèle un écart réel — voir §3 ci-dessous.** |
| §7.4 — Reliquat de worktree Git détaché (`.claude/worktrees/elated-wilson-b9c165/`, contenant encore les fichiers POC retirés sur `main`) | Signalé, à examiner | ✅ **Effectivement nettoyé** — `git worktree list` ne montre plus que le worktree principal (`C:/AM-OXO-ETL d018a90 [main]`). Le dossier `.claude/worktrees/elated-wilson-b9c165/` existe toujours sur disque mais est **vide** (aucun fichier, `.git` absent) — un reliquat de coquille inoffensif, plus le worktree fonctionnel signalé le 22/07. Suppression possible mais sans urgence. |

---

## 3. Nouveau constat : la base SQL Server locale n'a pas la dernière migration (Lot U4)

Le service SQL Server LocalDB est joignable aujourd'hui (contrairement au 22/07). Requêtage
direct de la base `ExcelEtl` :

```sql
SELECT MigrationId FROM __EFMigrationsHistory_ExcelEtl ORDER BY MigrationId;
```

```
20260710140017_InitialCreate
20260710174749_AddCompletedAtUtcToExtractionHistories
20260717113850_AddImportProfile
20260718092214_AddExportProfile
20260721095640_RemoveExtractionConfigPoc
```

**La dernière migration présente sur `main`, `20260724005133_AddTableauxApplicationsToProfiles`
(Lot U4, commit `9df1711`, 2026-07-24), n'a pas encore été appliquée** — confirmé à la fois par
l'historique des migrations ci-dessus et par l'absence de la table
`ExportProfileSheetRuleApplicationColumnDefinitions` dans `sys.tables` de la base `ExcelEtl`.

Ce n'est **pas une régression ni un oubli de code** : `CLAUDE.md` documente déjà ce fait
explicitement dans sa propre note Lot U4 ("not yet applied to any real SQL Server database as of
this lot"). Le Lot G4 (auto-application des migrations au démarrage de chaque hôte) reste en
place et non modifié — la migration s'appliquera automatiquement au prochain démarrage réel
d'`ExcelETL.WebAPI`/`ExcelETL.BlazorAdmin` sur cette machine, exactement comme le mécanisme est
conçu pour le faire. **Point de vigilance à connaître avant toute manipulation directe de la base
`ExcelEtl`/`ExportProfiles` en dehors de l'application** (ex. requête SQL manuelle, script de
diagnostic) : la colonne `ApplicationColumnDefinitions` et les colonnes `DefaultTableaux`/
`DefaultApplicationNames` d'`ImportProfiles` n'y existent pas encore tant qu'aucun des deux hôtes
n'a été relancé depuis le commit `9df1711`. C'est exactement le même schéma d'incident que celui
déjà rencontré et documenté pour Lot T8 (profil déjà seedé avant l'ajout d'une règle) — ici la
migration elle-même, pas seulement le contenu seedé, est en attente.

Tables réellement présentes dans `ExcelEtl` aujourd'hui (18) : `AspNetRoleClaims`/`AspNetRoles`/
`AspNetUserClaims`/`AspNetUserLogins`/`AspNetUserRoles`/`AspNetUsers`/`AspNetUserTokens`,
`ExportProfiles`/`ExportProfileSheetRuleColumnDefinitions`/
`ExportProfileSheetRulePointColumnDefinitions`/`ExportProfileSheetRules`, `ImportProfiles`/
`ImportProfileSheetRuleBlockFields`/`ImportProfileSheetRulePointRules`/`ImportProfileSheetRules`,
`SystemLogs`, et les deux tables d'historique de migration (`__EFMigrationsHistory_ExcelEtl`/
`__EFMigrationsHistory_Identity` — noms personnalisés, pas le nom par défaut EF Core, cohérent
avec deux `DbContext` partageant une même base physique). **Confirmation positive** : aucune des
4 tables POC retirées par Lot K4 (`CellMappings`/`ExtractionHistories`/`SheetConfigs`/
`ExtractionConfigs`) n'est présente — le retrait du 21/07 est bien effectif en base réelle, pas
seulement dans le code EF Core.

---

## 4. Travaux réalisés entre le 22/07 (`7a7c414`) et aujourd'hui (`d018a90`), pour recalage du suivi

Confirmé par `git log` (35 commits) et lecture de `CLAUDE.md` :

| Date / commit(s) | Contenu |
|---|---|
| 2026-07-23, `596ea46` | Nettoyage des 3 commentaires obsolètes signalés au §2 du rapport du 22/07. |
| 2026-07-23, `d6ca947`→`191b7df` | Publication du rapport du 22/07 lui-même comme doc versionnée, marquage des tickets Lot M comme implémentés. |
| 2026-07-23, `4bb4bb9`, `f866505` | Lot R — densification des cartes de règle de feuille (grille responsive, sous-listes repliables) sur `ImportProfileEditor.razor`/`ExportProfileEditor.razor`. |
| 2026-07-23, `35ecef1`, `8270b38` | Lot S — réorganisation du `NavMenu` (retrait des liens de test du menu, déplacés sur les pages de liste), renommage de la marque en "Alpha - MAD / REL OXO". |
| 2026-07-23, `07cb8c1`, `3b0390c`, `d8689c1` | Correctif R3 — l'accordéon des sous-listes ne s'ouvrait pas dans un vrai navigateur pour une règle sans sous-éléments (`<details>` toujours rendu désormais, `open` lié à l'état C#). |
| 2026-07-23, `7596564` | Boutons Modifier/Supprimer d'une carte de règle épinglés en bas à droite. |
| 2026-07-23, `c6bb814` | **Correctif fonctionnel réel** — `IBrowserFile.OpenReadStream()` ne supporte que la lecture asynchrone ; `ClosedXmlWorkbookReader` lisait en synchrone, provoquant une exception "Synchronous reads are not supported." sur tout upload réel via `/import-profiles/test`/`/export-profiles/test`. Jamais détecté par bUnit (le double de test supporte la lecture synchrone). Corrigé par un tampon `MemoryStream` intermédiaire. |
| 2026-07-23, `1e6c611`, `456acc0`, `80c1d38`, `5c29b54` | Bouton "Retour à la liste", accordéons repliables et en-têtes collants sur `ImportProfileTest.razor`/`ExportProfileTest.razor`. |
| 2026-07-23, `78e535e`→`01d1bc9` | **Lot T complet** — `PivotSource.TacheMultiple`, génération dynamique d'une feuille par `TypeTacheMultipleCode`, UI export, seeding, et Lot T8 (migration additive pour les profils déjà seedés avant l'existence de la règle). |
| 2026-07-23, `aa8fdb6` | Polish UX `ExportProfileTest.razor` (sections repliables, retour à la ligne lisible, compteurs d'éléments par feuille). |
| 2026-07-24, `baac339`→`a82dedd` | **Lot U complet** — `ImportProfile.DefaultTableaux`/`DefaultApplicationNames`, `Tableaux`/`Applications`/`RepereParent` sur les pivots, `ApplicationColumnDefinition`, rendu des colonnes Application dans `SheetGenerationEngine`, enrichissement des profils par défaut seedés (voir écart migration, §3 ci-dessus). |
| 2026-07-24, `1c4c8a8`→`519925d` | **Lot V complet (V1→V13)** — passe mobile-first : correctif d'en-tête de colonne, bascule tableau→cartes sur mobile, actions icône seule, accordéon des messages de log, boutons pleine largeur, bandeau de retour compact, dépriorisation des textes d'intro, champs de formulaire agrandis, résultats en carte. |
| 2026-07-24, `aa50bc1` | Consolidation de tout ce qui précède dans `CLAUDE.md`. |
| 2026-07-24, `21c2d61`→`d018a90` | Docs annexes (brief system-instructions, recommandations tickets TDD) puis **Lot W** — édition/suppression en place de `UnconditionalColonneNames`/`ConditionalPointRule` dans `ImportProfileEditor.razor`/`SheetRuleForm.razor` (dernier gap ajout-seul restant sur cette page). |

Aucun de ces éléments ne contredit ce que `CLAUDE.md` documente déjà à leur sujet — vérification
par lecture directe du code pour Lot T (regroupement dynamique par code, migration additive),
Lot U (nouvelles propriétés sur les pivots/`ImportProfile`, colonnes Application) et Lot W
(présence des ids `edit-unconditional-colonne-button-*`/`save-conditional-point-rule-button-*`
dans `SheetRuleForm.razor`), tous confirmés présents dans le code réel, pas seulement dans les
notes.

---

## 5. Écarts documentation ↔ code — récapitulatif au 24/07

1. **`ExcelProcessingClientService.Tests` (15 tests) toujours absent de `ExcelETL.slnx`** — voir
   §1. Non traité depuis le 22/07, aucune régression mais toujours un vrai trou de couverture CI
   potentielle.
2. **Migration `20260724005133_AddTableauxApplicationsToProfiles` non appliquée à la base
   LocalDB locale** — voir §3. Comportement attendu (auto-application au prochain démarrage d'un
   hôte, déjà documenté dans `CLAUDE.md`), mais point de vigilance réel si quelqu'un interroge la
   base directement avant ce redémarrage.
3. Les 3 commentaires obsolètes signalés le 22/07 sont **résolus**.
4. Le reliquat de worktree Git détaché est **résolu** (worktree lui-même supprimé du registre
   Git ; ne reste qu'un dossier vide sans contenu).
5. Aucun autre écart structurel trouvé entre `CLAUDE.md` et l'état réel du code pour les lots
   livrés depuis le 22/07 (R, S, correctif R3, T, U, V, W) — tous vérifiés par lecture directe du
   code, pas seulement par confiance dans la documentation vivante.

---

## 6. Non vérifiable depuis le dépôt seul

- **Format définitif de `OXO_TRAME_IMPORT_MAD.xlsx`** — dépend d'une confirmation client externe
  au dépôt, non réexaminé (hors périmètre de cet audit, comme le 22/07).
- **Application de la migration Lot U4 sur un environnement autre que cette machine locale**
  (ex. environnement de test partagé) — non vérifiable depuis ce dépôt seul.

---

## Synthèse

- **Suite de tests** : 812/812 verts via `ExcelETL.slnx` (vs 624/624 le 22/07, entièrement
  expliqué par les Lots T/U/V/W et les correctifs intermédiaires) **+ 15/15 verts dans le projet
  legacy toujours hors solution**, écart non traité depuis deux jours.
- **3 des 4 écarts signalés le 22/07 sont résolus** (commentaires obsolètes, worktree détaché,
  et la question des migrations réelles est désormais vérifiable — voir point suivant) ; **le
  4ᵉ (legacy hors slnx) reste ouvert**.
- **Nouveau point de vigilance** : la base SQL Server locale accuse un retard d'une migration
  (Lot U4, `AddTableauxApplicationsToProfiles`) — comportement conforme au design (auto-migration
  Lot G4 au prochain démarrage d'hôte), mais à garder en tête avant toute inspection directe de
  la base hors application.
- **Travaux Lots R, S, correctif R3, T, U (partiel côté base), V, W** : tous confirmés livrés et
  conformes à ce que `CLAUDE.md` documente, par lecture directe du code — aucune trace de travail
  partiel ou divergent.
