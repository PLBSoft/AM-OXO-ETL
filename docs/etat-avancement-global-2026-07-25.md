# État des lieux technique global — vérification indépendante (2026-07-25)

*Basé exclusivement sur une lecture directe du code sur `main` au commit `5246b9d`
(2026-07-25), `git log`/`git status`/`git diff`, exécution réelle de la suite de tests,
tentative de requêtage direct de la base SQL Server LocalDB, et recherche exhaustive dans
`src/`/`tests/`/`legacy/`/`.claude/`/`docs/`. Aucune affirmation de `CLAUDE.md` n'a été acceptée
sans confrontation directe au code. Référence de départ : `docs/etat-avancement-global-2026-07-24.md`
(dernier commit couvert : `d018a90`, 812/812 tests slnx + 15/15 legacy hors-solution annoncés).*

---

## 1. Suite de tests complète

**Exécution réelle, `dotnet test ExcelETL.slnx` à la racine, aujourd'hui :**

```
ExcelETL.Domain.Tests.dll                    : 278/278 réussis
ExcelETL.Application.Tests.dll               : 152/152 réussis
ExcelETL.Infrastructure.Tests.dll            : 152/152 réussis
ExcelETL.WebAPI.Tests.dll                    :  19/19  réussis
ExcelETL.BlazorAdmin.Tests.dll               : 413/413 réussis
ExcelETL.Hosting.Tests.dll                   :   6/6   réussis
Legacy.NewApiPingService.Tests.dll           :   9/9   réussis (net48)
Legacy.ExcelProcessingClientService.Tests.dll:  15/15  réussis (net48)
```

**Total via `ExcelETL.slnx` : 1046/1046 verts, zéro échec, zéro build cassé.**

### Écart majeur résolu depuis le 24/07 : le projet legacy manquant est maintenant dans `ExcelETL.slnx`

Le trou signalé les 22/07 et 24/07 (`ExcelProcessingClientService.Tests` absent de la solution,
15 tests jamais exécutés par une seule commande `dotnet test ExcelETL.slnx`) **est corrigé** —
commit `c05b493` ("chore: add ExcelProcessingClientService (+.Tests) to ExcelETL.slnx",
2026-07-24, avant le début du Lot 034). Confirmé par lecture directe de `ExcelETL.slnx` :

```xml
<Project Path="legacy/ExcelProcessingClientService/ExcelProcessingClientService.csproj" />
...
<Folder Name="/legacy/ExcelProcessingClientService.Tests/">
  <Project Path="legacy/ExcelProcessingClientService.Tests/Legacy.ExcelProcessingClientService.Tests.csproj" />
```

et par l'exécution ci-dessus, qui montre bien les 4 projets legacy (`NewApiPingService`,
`NewApiPingService.Tests`, `ExcelProcessingClientService`, `ExcelProcessingClientService.Tests`)
dans le même passage de `dotnet test ExcelETL.slnx`. **Il n'existe donc plus, à ce jour, aucun
test du dépôt qui échappe à `dotnet test ExcelETL.slnx` — 1046/1046 est le total réel et complet.**

### Comparaison avec le 812/812 du 24/07

| Projet | 24/07 | 25/07 | Écart | Explication |
|---|---|---|---|---|
| Domain.Tests | 264 | 278 | +14 | Lot 027 (27.0 — `MaxNameLength=60` sur `ImportProfile`/`ExportProfile`). Chiffre final identique à "Domain 270" puis "Domain 278" (inchangé depuis Lot 032) annoncés dans `CLAUDE.md`. |
| Application.Tests | 121 | 152 | +31 | Lot 027 (27.1 — `ProfileNameAlreadyExistsException`), Lot 032 (détecteur d'incohérence TYPE PROCEDURE, +24 tests purs), Lot 034 (34.1/34.4 — `GeneratedFileRecord`, `ProcessOxoFileService`). Cohérent avec "Application 152" dans `CLAUDE.md`. |
| Infrastructure.Tests | 121 | 152 | +31 | Lot 027 (27.2 — index unique EF + migration), Lot 029 (renommage base, +2 tests factory), Lot 032 (+3 intégration), Lot 034 (34.2/34.3 — `EfGeneratedFileArchiveStore`/`FileSystemGeneratedFileWriter`/`GeneratedFileNameSanitizer`, +16). Cohérent avec "Infrastructure 152". |
| WebAPI.Tests | 13 | 19 | +6 | Lot 029 (+2 `ConnectionStringConfigurationTests`), Lot 034 (34.4/34.6, +4 `OxoProcessEndpointTests` archivage best-effort). Cohérent avec "WebAPI 19". |
| BlazorAdmin.Tests | 278 | 413 | **+135** | Lots 027/028/029/030/031/032/033/034 côté UI (unicité nom, suppression avec confirmation, parité form-floating, compteurs, détection TYPE non-blocking, upload multi-fichiers, page `/generated-files`) **+ 2 tests non commités** (voir §2 ci-dessous — `CLAUDE.md` annonce "411", le disque en a réellement 413). |
| Hosting.Tests | 6 | 6 | 0 | Inchangé depuis Lot G3. |
| Legacy (NewApiPingService) | 9 | 9 | 0 | Inchangé. |
| Legacy (ExcelProcessingClientService) | 15 (hors slnx) | 15 (**dans slnx désormais**) | 0 net, mais bascule de statut | Voir ci-dessus. |
| **Total (slnx)** | **812** | **1046** | +234 | Somme des écarts ci-dessus, dont +15 dus au seul rattachement du projet legacy à `ExcelETL.slnx`. |

**Presque tous les sous-totaux cités dans `CLAUDE.md` correspondent à l'exécution réelle
d'aujourd'hui — sauf un, décrit au §2.**

---

## 2. Écart réel trouvé : travail en cours non commité, non documenté dans `CLAUDE.md`

`git status` révèle 5 fichiers modifiés dans l'arbre de travail, **aucun commit, aucun stash** :

```
 M src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileTest.razor
 M src/ExcelETL.BlazorAdmin/Components/Pages/Admin/GeneratedFiles.razor
 M src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.fr.resx
 M src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx
 M tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfileTestTests.cs
```

`main` local est par ailleurs strictement synchronisé avec `origin/main` (`5246b9d` des deux
côtés) — ce n'est donc pas un retard de push, c'est du travail non terminé/non validé dans la
copie de travail actuelle.

**Contenu du diff (lu intégralement), en résumé :**

1. **`ExportProfileTest.razor`** gagne un bloc `<details>`/`<summary>` affichant
   `ImportResult.Errors` (les avertissements non bloquants, ex. `UnrecognizedTypeElement` sur le
   "VANNE" de D8570) **par fichier du lot**, avec la même mécanique repliable
   (`ToggleSection(index, "warnings")`) que le reste de la page — reprend exactement la forme déjà
   présente sur `ImportProfileTest.razor`. Un commentaire dans le diff documente le motif : *"a
   file badged 'Warning' ... showed no way to see what the warning actually was on this page,
   unlike ImportProfileTest.razor's own warnings table"* — un signalement client repris tel quel.
   4 nouvelles clés `.resx` (`ExportProfileTest_WarningsHeading`/`Sheet`/`BlockIdentifier`/
   `ErrorCode`/`Message`, EN uniquement pour l'instant — **aucune entrée FR ajoutée pour ces 5
   clés**, vérifié par lecture du diff sur `BlazorAdminMessages.fr.resx` qui ne contient que la
   6ᵉ clé ci-dessous).
2. **`GeneratedFiles.razor`** (page du Lot 034.5) gagne un bandeau `alert-info` explicite
   (`GeneratedFiles_ScopeNote`, EN+FR cette fois) précisant que la page ne liste que les fichiers
   archivés via `POST /api/oxo/process`, pas ceux générés depuis les pages de test Blazor — comble
   une ambiguïté que le Lot 034 lui-même signalait déjà comme hors périmètre volontaire
   ("`ImportProfileTest.razor`/`ExportProfileTest.razor` archiving ... explicit ticket
   exclusion").
3. **`ExportProfileTestTests.cs`** gagne 2 tests (`Run_D8570Fixture_ShowsWarningsDetails_...`,
   `WarningsDetails_TogglesOpenAndClosed`) couvrant le point 1 — ce sont exactement les 2 tests
   qui expliquent l'écart 411 (annoncé dans `CLAUDE.md`, dernière phrase du Lot 034) vs 413
   (compté réellement ci-dessus au §1).

**Aucune trace de ce travail dans `CLAUDE.md`** — normal, `CLAUDE.md` documente des lots
terminés/commités, pas une modification en cours. Ce n'est pas une régression ni une
incohérence de la doc, mais un point de vigilance réel pour la suite de session : **ce diff
existe uniquement sur cette machine, n'est protégé par aucun commit**, et si l'entrée FR
manquante pour les 5 nouvelles clés `ExportProfileTest_*` n'est pas ajoutée avant de considérer
ce travail terminé, la page affichera la clé brute en anglais faute de traduction (comportement
`IStringLocalizer` par défaut) pour un utilisateur en culture `fr-FR`.

---

## 3. Nouveau constat : la base SQL Server LocalDB est arrêtée et n'a pas pu être interrogée

Contrairement au 24/07 (base joignable), l'instance `MSSQLLocalDB` est **arrêtée** aujourd'hui
(`SqlLocalDB.exe info MSSQLLocalDB` → `État : Arrêté`) et une tentative de redémarrage manuel
échoue (`Error occurred during LocalDB instance startup: SQL Server process failed to start`,
sans plus de détail exploité — ce diagnostic est hors périmètre de cet audit, qui ne modifie pas
l'environnement). **Impossible de vérifier aujourd'hui** :
- si la migration `20260724005133_AddTableauxApplicationsToProfiles` (signalée en retard le
  24/07) a depuis été appliquée,
- si les 2 migrations livrées depuis (`20260724115715_AddProfileNameUniqueIndexAndMaxLength`
  Lot 027, `20260725010636_AddGeneratedFileRecord` Lot 034) ont été appliquées,
- l'état réel des tables de la base `AM-OXO-ETL-MAD-REL` (renommée par Lot 029 — nom confirmé par
  lecture des `appsettings.Development.json` des deux hôtes, identique dans les deux).

Ceci reste cohérent avec le design (Lot G4, auto-migration au prochain démarrage réel d'un
hôte) — aucune régression suspectée, simplement un point non vérifiable cette fois-ci, pour la
raison inverse du 22/07 (serveur injoignable) et du 24/07 (serveur vérifié, migration en retard
confirmée) : ici le service lui-même ne démarre pas.

---

## 4. Nouveau constat, hors périmètre fonctionnel : 2 avertissements de sécurité NuGet dans le build

Visibles dans la sortie de `dotnet test` aujourd'hui (jamais mentionnés dans les rapports du
22/07 ou du 24/07) :

| Paquet | Version | Gravité | Projets concernés |
|---|---|---|---|
| `System.Security.Cryptography.Xml` | 10.0.9 | **Haute** (5 CVE GHSA distincts) | `ExcelETL.WebAPI.Tests` (dépendance transitive) |
| `AngleSharp` | 1.4.0 | Moyenne | `ExcelETL.BlazorAdmin.Tests` (dépendance de bUnit) |

Ce sont des avertissements `NU1902`/`NU1903` de restauration NuGet, pas des échecs de test — la
suite reste 1046/1046 verte. Non investigué plus loin ici (hors périmètre de cet audit de
statut), mais à signaler : aucun des rapports précédents ne les avait relevés, donc soit ils sont
apparus avec une mise à jour de la base d'avisories NuGet depuis le 24/07 (le plus probable, ces
alertes dépendent d'un flux externe re-téléchargé à chaque restore), soit ils étaient présents
et non lus jusqu'ici. `docs/procedure-mise-a-jour-packages.md` existe déjà dans le dépôt comme
procédure de référence pour traiter ce genre d'alerte le moment venu.

---

## 5. Travaux réalisés entre le 24/07 (`d018a90`) et aujourd'hui (`5246b9d`), pour recalage du suivi

Confirmé par `git log` (23 commits) et lecture de `CLAUDE.md` :

| Date / commit(s) | Contenu |
|---|---|
| 2026-07-24, `c05b493` | **Rattache enfin `ExcelProcessingClientService`(+.Tests) à `ExcelETL.slnx`** — clôt l'écart signalé aux rapports du 22/07 et du 24/07 (voir §1). |
| 2026-07-24, `7f89b50`, `b0c1c75` | Lot X (partiel, X1-X9 + X10/X11) — polish mobile-first `ExportProfileEditor`, largeur des boutons de liste, faisabilité `SectionOutlet`, bandeau de retour partagé. |
| 2026-07-24, `bcb5525`, `a9d29e9` | Lot Y — corrections UX mobile (collision NavMenu/toggler, garde `form-floating`, largeur CTA, débordement en édition), suivi du jour même étendant Y3 à `ImportProfileEditor`. |
| 2026-07-24, `737360b`→`3012fe9` | **Lot 027 complet (27.0→27.4)** — unicité du nom des profils import/export (invariant Domain `MaxNameLength=60`, `ProfileNameAlreadyExistsException`, index unique EF + migration, affichage d'erreur dans les éditeurs, auto-incrément du bouton Dupliquer). |
| 2026-07-24, `5af701e` | Convention de numérotation "Lot NNN" + docs tickets 028/029. |
| 2026-07-24, `c2b9c86` | **Lot 028** — suppression de profils import/export avec confirmation en ligne (icône, pas de modal, symétrique sur les 2 pages). |
| 2026-07-24, `6a0f49f` | **Lot 029** — renommage de la base SQL Server par défaut en `AM-OXO-ETL-MAD-REL` (config uniquement, 2 design-time factories corrigées). |
| 2026-07-24, `06e5b0d` | **Lot 030** — parité visuelle Import/Export (`form-floating` généralisé), correctif du bug réel de `app.css` (placeholder visible au lieu de transparent), boutons de liste sur une seule ligne. |
| 2026-07-24, `d338f0d`, `0311d28` | **Lot 031** — compteurs d'éléments sur les titres de section de `ImportProfileTest.razor` (parité avec `ExportProfileTest.razor` livré le 23/07). |
| 2026-07-24, `c7dbe60` | **Lot 032** — détection d'incohérence de TYPE dans les tâches multiples PROCEDURE (nouvel `ExtractionErrorCode`, avertissement non bloquant, déclenché par une anomalie réelle repérée par le client sur C7401). |
| 2026-07-24/25, `66f333a` | Réordonnancement d'affichage sur `ImportProfileTest.razor` (avertissements avant Equipement). |
| 2026-07-25, `a2e0e22` | **Lot 033** — upload multi-fichiers sur `ImportProfileTest.razor`/`ExportProfileTest.razor` (validation nombre/taille/taille totale, statuts par fichier incl. nouveau `TechnicalError`, factorisation partielle `BatchImportProcessing`). |
| 2026-07-25, `3d539d6`→`5246b9d` | **Lot 034 complet (34.0→34.6)** — archivage best-effort source/cible dans `POST /api/oxo/process` (`GeneratedFileRecord`, `EfGeneratedFileArchiveStore`, `FileSystemGeneratedFileWriter`, wiring `ProcessOxoFileService`/`OxoController`), page Blazor admin `/generated-files` de consultation. Coexiste, documenté comme délibéré, avec l'archivage best-effort préexistant de `ProcessOxoFileService` (Lot K). |

Vérification par lecture directe du code (pas seulement confiance dans `CLAUDE.md`) pour les
points structurants : présence de `MaxNameLength` dans `ImportProfile.cs`/`ExportProfile.cs`
(Lot 027), présence de `TacheMultipleTypeCoherenceAnalyzer`/`TacheMultipleSectionGrouper` dans
`Extraction/Oxo/Procedure/` (Lot 032), présence de `GeneratedFileRecord`/
`IGeneratedFileArchiveStore`/`FileSystemGeneratedFileWriter`/`GeneratedFiles.razor` (Lot 034),
présence de `BatchFileValidator`/`BatchImportProcessing` (Lot 033) — tous confirmés dans le code
réel. Aucune divergence trouvée entre ce que `CLAUDE.md` annonce pour ces lots et le code livré,
**à l'exception du diff non commité décrit au §2, antérieur au dernier commit et donc non encore
répercuté dans la doc vivante.**

---

## 6. Écarts documentation ↔ code — récapitulatif au 25/07

1. **Résolu depuis le 24/07** : `ExcelProcessingClientService.Tests` fait maintenant partie de
   `ExcelETL.slnx` — plus aucun test du dépôt n'échappe à `dotnet test ExcelETL.slnx`.
2. **Nouvel écart réel, mineur, à traiter avant la prochaine consolidation `CLAUDE.md`** :
   5 fichiers modifiés non commités (§2) — fonctionnalité cohérente et déjà testée (2 tests
   passent), mais **incomplète du point de vue i18n** (5 clés `ExportProfileTest_*` sans entrée
   FR) et non protégée par un commit.
3. **Non vérifiable aujourd'hui** (service LocalDB arrêté, redémarrage manuel échoué) : état réel
   des migrations sur la base `AM-OXO-ETL-MAD-REL` — sujet resté ouvert au 24/07 pour la
   migration Lot U4, aujourd'hui aggravé par l'indisponibilité totale du serveur plutôt que
   résolu.
4. **Nouveau, hors périmètre fonctionnel** : 2 avertissements de sécurité NuGet (`NU1903` haute
   gravité sur `System.Security.Cryptography.Xml`, `NU1902` moyenne sur `AngleSharp`) visibles
   dans la sortie de build, non traités, non mentionnés dans les rapports précédents.
5. Aucun autre écart structurel trouvé entre `CLAUDE.md` et l'état réel du code pour les lots
   livrés depuis le 24/07 (X reliquat, Y, 027, 028, 029, 030, 031, 032, 033, 034) — tous vérifiés
   par lecture directe du code, pas seulement par confiance dans la documentation vivante.

---

## 7. Non vérifiable depuis le dépôt seul

- **Format définitif de `OXO_TRAME_IMPORT_MAD.xlsx`** — dépend d'une confirmation client externe
  au dépôt, non réexaminé (hors périmètre de cet audit, comme le 22/07 et le 24/07).
- **État réel des migrations EF Core sur la base SQL Server LocalDB** — voir §3, service arrêté
  aujourd'hui, non redémarrable sans investigation dédiée hors périmètre de ce rapport.
- **Application des migrations sur un environnement autre que cette machine locale** (ex.
  environnement de test partagé) — non vérifiable depuis ce dépôt seul, point déjà soulevé le
  24/07 et toujours sans réponse possible depuis le dépôt.

---

## Synthèse

- **Suite de tests** : 1046/1046 verts via `ExcelETL.slnx` (vs 812/812 le 24/07) — **et c'est
  désormais un total réellement complet** : le rattachement du projet legacy manquant
  (`ExcelProcessingClientService.Tests`, +15) clôt un trou signalé lors des deux rapports
  précédents. Croissance nette hors ce rattachement : +219 tests, cohérente avec les 9 lots
  livrés depuis le 24/07 (X reliquat, Y suite, 027 à 034).
- **Écart réel nouveau** : 5 fichiers modifiés non commités sur `ExportProfileTest.razor`/
  `GeneratedFiles.razor` (§2) — fonctionnellement testés (413 vs 411 documentés dans `CLAUDE.md`)
  mais incomplets côté traduction FR et non protégés par un commit. À finaliser (ajouter les
  clés `.resx` FR manquantes) avant de considérer ce point clos.
- **Nouveau point de vigilance environnemental** : le service SQL Server LocalDB est arrêté et ne
  redémarre pas sur cette machine aujourd'hui — impossible de confirmer l'état des 3 migrations
  livrées depuis le 22/07 (`AddTableauxApplicationsToProfiles`,
  `AddProfileNameUniqueIndexAndMaxLength`, `AddGeneratedFileRecord`) sur la base réelle.
- **Nouveau signal à surveiller, hors périmètre de ce rapport** : 2 avertissements de sécurité
  NuGet apparus dans le build (`System.Security.Cryptography.Xml` haute gravité,
  `AngleSharp` moyenne) — jamais relevés dans les rapports précédents.
- **Travaux Lots X (reliquat), Y (suite), 027, 028, 029, 030, 031, 032, 033, 034** : tous
  confirmés livrés et conformes à ce que `CLAUDE.md` documente, par lecture directe du code —
  aucune trace de travail partiel ou divergent en dehors du diff non commité relevé au §2.
