# État des lieux technique global — vérification indépendante (2026-07-22)

*Basé exclusivement sur une lecture directe du code sur `main` au commit `7a7c414`
(2026-07-22 18:11:03 +0200), `git log`, exécution réelle de la suite de tests, et recherche
exhaustive dans `src/`/`tests/`/`legacy/`. Aucune affirmation d'un ticket, d'une doc vivante ou de
`CLAUDE.md` n'a été acceptée sans confrontation directe au code. Référence de départ :
`docs/audit-coherence-globale-2026-07-19.md` (dernier commit couvert : `e378c7e`, 613/613 tests
verts annoncés).*

---

## 1. Suite de tests complète

**Exécution réelle, `dotnet test ExcelETL.slnx` à la racine, aujourd'hui :**

```
ExcelETL.Domain.Tests.dll         : 215/215 réussis
ExcelETL.Application.Tests.dll    :  94/94  réussis
ExcelETL.Infrastructure.Tests.dll : 113/113 réussis
ExcelETL.WebAPI.Tests.dll         :  13/13  réussis
ExcelETL.BlazorAdmin.Tests.dll    : 174/174 réussis
ExcelETL.Hosting.Tests.dll        :   6/6   réussis
Legacy.NewApiPingService.Tests    :   9/9   réussis (net48)
```

**Total via `ExcelETL.slnx` : 624/624 verts.**

⚠️ **Incident de build rencontré et résolu pendant cet audit** : le premier run a échoué à la
compilation d'`ExcelETL.BlazorAdmin.csproj` (`MSB3027`/`MSB3021`, verrouillage de
`ExcelETL.Domain.dll`/`ExcelETL.Hosting.dll`/`ExcelETL.Infrastructure.dll`/`ExcelETL.Application.dll`
par un processus "ExcelETL.BlazorAdmin (27992)" et par Visual Studio (PID 33128)) — `ExcelETL.BlazorAdmin.Tests`
n'a donc pas tourné au premier essai. Le verrou a disparu de lui-même au second essai
(le process 27992 n'existait plus). Cause probable : Visual Studio avait la solution ouverte et
détenait un verrou de build transitoire. **Sans lien avec le code** — à garder en tête si un futur
`dotnet test` échoue de la même façon : fermer/attendre Visual Studio plutôt que soupçonner une
régression.

### Comparaison avec le 613/613 du 19/07

| Projet | 19/07 | 22/07 | Écart | Explication |
|---|---|---|---|---|
| Domain.Tests | 255 | 215 | **-40** | Lot K4 (commit `6de9519`, 2026-07-21) a supprimé les entités POC `ExtractionConfig`/`SheetConfig`/`CellMapping`/`ExtractionHistory` + enums `CellDataType`/`ExtractionStatus` et tous leurs tests. Chiffre cohérent avec la note déjà présente dans `CLAUDE.md` sous Lot K4 ("Domain 215... après retrait"). |
| Application.Tests | 91 | 94 | +3 | Lot K1-K3 (`ProcessOxoFileService`, `ImportProfileNotFoundException`, `ExportProfileNotFoundException`) — cohérent avec le chiffre déjà noté dans `CLAUDE.md` (94, inchangé depuis K4). |
| Infrastructure.Tests | 135 | 113 | -22 | Composé de deux mouvements : Lot K4 a retiré `ClosedXmlExtractionServiceTests`/`ClosedXmlGeneratorServiceTests`/`ExtractionConfigRepositoryTests`/etc. (POC), puis le seeding par défaut (`DefaultProfileSeederTests`, `DefaultProfileSeederPipelineIntegrationTests`, commit `4bbba60`) en a rajouté. Solde net -22 vs le 19/07 ; **`CLAUDE.md` cite 101 juste après K4** (avant l'ajout du seeder), 113 aujourd'hui (+12) est cohérent avec l'ajout ultérieur du seeder. |
| WebAPI.Tests | 14 | 13 | -1 | K4 a retiré `ExcelController`/`ProcessExcelFileService` et leurs tests d'intégration WebApplicationFactory ; K1/K2 en ont ajouté pour `OxoController`. Solde -1, cohérent avec le chiffre déjà noté dans `CLAUDE.md` (13, inchangé depuis K4). |
| BlazorAdmin.Tests | 112 | 174 | **+62** | K4 a retiré les tests de `Mappings.razor`/`UploadTest.razor`/`Dashboard.razor`/`History.razor` (POC), puis une longue série de lots UI (L1/L2, Lot M ×2, labels+édition ImportProfileEditor, Lot N, Lot O ×2, Lot P, confirmation de suppression, labels manquants, Lot Q ×2, bouton "Back to list") en a rajouté beaucoup plus qu'il n'en a retiré. Chiffre final (174) confirmé exactement identique à la dernière valeur annoncée dans `CLAUDE.md` ("Full ExcelETL.BlazorAdmin.Tests suite: 174/174 green"). |
| Hosting.Tests | 6 | 6 | 0 | Inchangé (Lot G3, aucune modification depuis). |
| Legacy (NewApiPingService) | 9 | 9 | 0 | Inchangé. |
| **Total (slnx)** | **607** | **624** | +17 | Somme des écarts ci-dessus. |
| Hosting.Tests (hors slnx au 19/07) | +6 | *(déjà inclus ci-dessus)* | — | Résolu : voir §6. |
| **Total annoncé (19/07)** | **613** | — | — | 607 (slnx) + 6 (Hosting, alors hors solution). |

**Chiffre publié dans `CLAUDE.md` vs vérification indépendante** : tous les sous-totaux par
projet cités dans `CLAUDE.md` (Domain 215, Application 94, WebAPI 13, BlazorAdmin 174, Hosting 6)
correspondent exactement à l'exécution réelle d'aujourd'hui. Aucun écart trouvé sur ces chiffres.

### ⚠️ Écart nouveau, non détecté par les audits précédents (07-17 et 07-19 inclus) : un second projet de test legacy totalement absent de `ExcelETL.slnx`

`legacy/ExcelProcessingClientService.Tests/Legacy.ExcelProcessingClientService.Tests.csproj`
existe sur disque (le service `ExcelProcessingClientService` lui-même date de Milestone 7,
bien avant le premier audit) mais **n'a jamais été référencé dans `ExcelETL.slnx`** — vérifié en
listant l'historique complet du fichier solution (`git log --oneline -- ExcelETL.slnx` : seulement
2 commits, le scaffold initial `a4cc935` et l'ajout de `Hosting`/`Hosting.Tests`, `3982f3f`,
2026-07-20 — jamais un commit touchant `ExcelProcessingClientService`).

Exécuté séparément dans cet audit :
```
dotnet test legacy/ExcelProcessingClientService.Tests/Legacy.ExcelProcessingClientService.Tests.csproj
→ Legacy.ExcelProcessingClientService.Tests.dll : 15/15 réussis
```

**Ce chiffre "15" correspond exactement** à celui que `CLAUDE.md` cite dans sa propre note Lot K4
("Full suite green after removal: ... legacy 15") — confirmant que ce nombre désignait déjà
`ExcelProcessingClientService.Tests` seul, et non `NewApiPingService.Tests` (9) ni les deux
combinés (24). Ni `CLAUDE.md` ni l'audit du 19/07 n'a jamais signalé que ce projet est absent de
la solution — **c'est un vrai trou, structurellement identique à celui déjà documenté pour
`ExcelETL.Hosting`/`ExcelETL.Hosting.Tests` au 19/07 (§5.1 de cet audit-là), mais jamais corrigé
pour ce second projet legacy.**

**Total réel du dépôt, toutes suites confondues (slnx + les deux projets legacy hors-solution
n'existant qu'un seul l'est réellement, l'autre non)** : 624 (slnx, incluant déjà
`NewApiPingService.Tests`) + 15 (`ExcelProcessingClientService.Tests`, hors solution) =
**639 tests verts au total dans le dépôt**, dont 15 ne sont exécutés par aucune commande
`dotnet test ExcelETL.slnx`.

**Conséquence concrète** : identique à celle déjà documentée le 19/07 pour `Hosting.Tests` —
aucun impact aujourd'hui (pas de CI dans ce dépôt, `.github/workflows/` n'existe même pas,
confirmé par recherche), mais silencieusement exclu le jour où une CI s'appuierait sur
`dotnet test ExcelETL.slnx`. Contrairement à `Hosting`/`Hosting.Tests` (corrigés le 20/07), ce
deuxième trou n'a jamais été traité.

---

## 2. Lot K — migration Web API OXO

| Point à vérifier | Constat | Preuve |
|---|---|---|
| `POST /api/oxo/process` câblé, `ImportProfileId`/`ExportProfileId` requis | ✅ Conforme | [`OxoController.cs`](../src/ExcelETL.WebAPI/Controllers/OxoController.cs) — `[HttpPost("process")]`, `[FromForm] ProcessOxoFileRequest`. [`ProcessOxoFileRequest.cs`](../src/ExcelETL.WebAPI/Contracts/ProcessOxoFileRequest.cs) — `Guid ImportProfileId`/`Guid ExportProfileId` (non nullable, valeurs requises par construction du type, pas de valeur par défaut côté contrat) |
| 404 si profil inconnu, pas d'exception non gérée | ✅ Conforme | [`ProcessOxoFileService.cs:25-28`](../src/ExcelETL.Application/Extraction/Oxo/ProcessOxoFileService.cs#L25-L28) lève `ImportProfileNotFoundException`/`ExportProfileNotFoundException` ; [`GlobalExceptionHandler.cs:47`](../src/ExcelETL.WebAPI/ExceptionHandling/GlobalExceptionHandler.cs#L47) les mappe explicitement vers `404 Not Found` |
| `POST /api/excel/process` (`ExcelController`) disparu | ✅ Conforme | `src/ExcelETL.WebAPI/Controllers/` ne contient que `HealthController.cs` et `OxoController.cs` (recherche directe du dossier) |
| Zéro référence à `ExtractionConfig*`/`ExtractionHistory`/`SheetConfig`/`CellMapping`/`CellDataType`/`ExtractionStatus` dans `src/`/`tests/` (hors migrations) | ✅ Conforme | Recherche exhaustive : seules occurrences restantes sont les 2 fichiers de la migration de suppression elle-même (`20260721095640_RemoveExtractionConfigPoc.cs`/`.Designer.cs`) et les 3 migrations antérieures qui la précèdent historiquement (`InitialCreate`, `AddCompletedAtUtcToExtractionHistories`, `AddImportProfile`/`AddExportProfile` — ces dernières référencent le type via leur `.Designer.cs`/snapshot EF, normal pour un historique de migrations). Zéro occurrence dans `tests/`. |
| Zéro référence à `Mappings.razor`/`Dashboard.razor`/`History.razor`/`UploadTest.razor`/`ExcelProcessingClient`/`IExcelDownloadInterop`/`WebApiClientOptions`/`fileDownload.js` dans `src/` (branche `main`) | ✅ Conforme | Recherche exhaustive dans `src/` : aucune occurrence. **Ces 4 fichiers `.razor` existent bien encore sur disque**, mais uniquement dans `.claude/worktrees/elated-wilson-b9c165/src/...` — un **worktree Git détaché** (`git worktree list` confirme : `HEAD detached at d125905`, commit antérieur à K4 du 2026-07-19-ish, avant retrait du POC), pas sur `main`. Ce n'est pas un écart de code, mais un reliquat d'environnement à nettoyer — voir §6. |
| Migration EF Core de suppression des tables POC | ✅ Fichier présent | [`20260721095640_RemoveExtractionConfigPoc.cs`](../src/ExcelETL.Infrastructure/Persistence/Migrations/20260721095640_RemoveExtractionConfigPoc.cs) — `DropTable` sur `CellMappings`/`ExtractionHistories`/`SheetConfigs`/`ExtractionConfigs` |
| Migration appliquée à une vraie base SQL Server | ⚠️ **Non vérifiable dans cette session** | SQL Server injoignable au moment de cet audit : `sqlcmd -S localhost -d ExcelEtl` **et** `sqlcmd -S "(localdb)\MSSQLLocalDB"` échouent tous deux ("serveur introuvable ou inaccessible" / timeout) — service probablement arrêté sur cette machine actuellement. `CLAUDE.md` affirme l'avoir vérifié le 2026-07-21 lors du Lot K4 ("Full suite green after removal... Verified against the real LocalDB"), mais **cette affirmation n'a pas pu être recontrôlée indépendamment aujourd'hui** faute d'accès. Ne pas la considérer comme reconfirmée par cet audit. |
| `/` atterrit sur `ImportProfiles.razor` | ✅ Conforme | [`ImportProfiles.razor:1-3`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor#L1-L3) : `@page "/import-profiles"` **et** `@page "/"`, protégé par `@attribute [Authorize(Roles = IdentitySeeder.AdminRoleName)]` — un visiteur non authentifié atterrissant sur `/` est bien redirigé vers le login (comportement déjà décrit dans `CLAUDE.md`, reconfirmé ici par lecture directe du fichier, pas simplement recopié) |

### Écart de documentation trouvé (mineur, cosmétique) : commentaires de code obsolètes dans le pipeline OXO

Trois commentaires de code, toujours au présent/futur, décrivent une coexistence ou une
comparaison avec des classes **qui n'existent plus depuis Lot K4** :
- [`OxoController.cs:11-13`](../src/ExcelETL.WebAPI/Controllers/OxoController.cs#L11-L13) : *"Both routes coexist until Lot K4 retires this one and the old one."* — Lot K4 a déjà eu lieu (commit `6de9519`, 2026-07-21) ; la phrase est maintenant fausse au présent (il n'y a plus qu'une seule route).
- [`OxoController.cs:57`](../src/ExcelETL.WebAPI/Controllers/OxoController.cs#L57) et [`ProcessOxoFileService.cs:7`](../src/ExcelETL.Application/Extraction/Oxo/ProcessOxoFileService.cs#L7) : comparent le comportement à `ExcelController`/`ProcessExcelFileService`, des classes qui n'existent plus dans le dépôt — un futur lecteur cherchant ces noms pour comparer ne les trouvera pas.
- [`ClosedXmlWorkbookWriter.cs:11`](../src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookWriter.cs#L11) : même chose avec `ClosedXmlGeneratorService`.

Aucun impact fonctionnel (uniquement des commentaires), mais contredit directement la propre
convention documentée dans `CLAUDE.md` ("Don't reference the current task, fix, or callers...
since those belong in the PR description and rot as the codebase evolves") — ces 3 commentaires
ont justement pourri de cette façon. Cosmétique, sans urgence.

---

## 3. Lot L — NavMenu

**Confirmé conforme, code lu intégralement** ([`NavMenu.razor`](../src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor), 111 lignes) :
- `#nav-logs-link` dans un `<AuthorizeView>` générique (sans rôle) — visible à tout utilisateur authentifié, masqué sinon. Conforme à L2.
- `#nav-users-link`/`#nav-import-profiles-link`/`#nav-import-profiles-test-link`/`#nav-export-profiles-link`/`#nav-export-profiles-test-link` dans `<AuthorizeView Roles="@IdentitySeeder.AdminRoleName">` — conforme à L1.
- Un seul lien de connexion dans tout le fichier : `#nav-login-link`, uniquement dans la branche `<NotAuthorized>` du bloc `<AuthorizeView>` générique — pas de doublon (confirmé par lecture directe, une seule occurrence de `nav-login-link` dans le fichier). Conforme à L2 (correction du doublon introduit puis retiré par L1→L2).
- Aucun `<span class="nav-link">` autonome pour le nom d'utilisateur : le lien `#nav-profile-link` combine directement `@context.User.Identity?.Name - @Loc["NavMenu_Profile"]` — conforme au Lot M (fusion nom d'utilisateur/lien Profil).

**Aucun écart trouvé entre le code réel et
`docs/etat-avancement-lot-l-navmenu-visibilite-authentification-2026-07-21.md`/`CLAUDE.md`.**
Rien n'indique de modification postérieure non documentée sur ce fichier au-delà de ce que
`CLAUDE.md` décrit déjà (Lot L1 → L2 → Lot M, dans cet ordre, tous confirmés présents dans l'état
final du fichier).

---

## 4. Lot M et seeding — implémentation confirmée

### `tickets-tdd-blazor-navmenu-fusion-lien-profil.md` : **implémenté intégralement**
Voir §3 ci-dessus — le lien fusionné existe exactement comme décrit. Commit `3388b63` ("Add Lot M:
merge NavMenu username display and Profile link into one", 2026-07-21) confirmé dans `git log`.

### `tickets-tdd-seed-profils-defaut.md` (`DefaultProfileSeeder`) : **implémenté intégralement**
- [`src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs`](../src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs) existe.
- Câblage confirmé dans [`ExcelETL.BlazorAdmin/Program.cs:104,187-193`](../src/ExcelETL.BlazorAdmin/Program.cs#L104) :
  `builder.Services.AddScoped<DefaultProfileSeeder>()`, puis gate `ProfileSeeding:Enabled`
  (défaut `true`) exécutant `profileSeeder.SeedAsync()` juste après le seeding Identity — exactement
  comme documenté dans `CLAUDE.md`.
- Tests dédiés présents et verts (inclus dans les 113/113 d'`ExcelETL.Infrastructure.Tests` du §1) :
  `tests/ExcelETL.Infrastructure.Tests/Seeding/DefaultProfileSeederTests.cs` et
  `DefaultProfileSeederPipelineIntegrationTests.cs`.
- Commits identifiés : `897d5fa` (ticket doc), `4bbba60` ("Add DefaultProfileSeeder to seed
  standard OXO import/export profiles", 2026-07-21).

**Ces deux lots correspondent donc à des échanges directs non tracés ailleurs par l'utilisateur —
confirmé livrés, avec preuve de code + tests verts, aucune trace de travail partiel ou abandonné.**

---

## 5. Travaux non couverts par les points 1-4, identifiés dans `git log`/`CLAUDE.md` (pour recalage du suivi utilisateur)

Tout ce qui suit est postérieur au commit `e378c7e` (dernier commit couvert par l'audit du 19/07)
et antérieur ou égal à `7a7c414` (HEAD actuel) :

| Date / commit | Contenu |
|---|---|
| 2026-07-20, `3982f3f` | Ajout de `ExcelETL.Hosting`/`ExcelETL.Hosting.Tests` à `ExcelETL.slnx` — corrige l'écart §5.1 de l'audit du 19/07 (confirmé, voir §6 ci-dessous). |
| 2026-07-20/21, `d972434`→`6de9519` | Lot K complet (K0/K0bis logging, K1/K2 route Web API, K3 migration client legacy, K4 retrait POC) — voir §2. |
| 2026-07-21, `28e2db5`→`4fdaab6` | Lot L1 puis L2 (NavMenu) — voir §3. |
| 2026-07-21, `3388b63`, `4bbba60` | Lot M (fusion NavMenu) et seeding par défaut — voir §4. |
| 2026-07-21, `1008cad`→`3e70d95` | Labels + édition/suppression inline des règles de feuille et des champs de bloc dans `ImportProfileEditor.razor` (ticket `docs/ticket-profil-import-edition.md`), puis Lot N (plages Excel absolues au lieu d'offsets bruts, `BlockFieldRangeFormatter`). |
| 2026-07-22, `5e75a7b`→`c476056` | Lot O (boutons icônes + libellés de section), correctif de style le même jour, Lot P (cartes par règle de feuille + alignement des boutons, nouvelle convention `docs/convention-ui-blazor-alignement-boutons.md`), confirmation de suppression pour les règles de feuille (avec un **incident réel documenté dans `CLAUDE.md`** : suppression accidentelle de 2 règles du profil d'import seedé en base LocalDB pendant une vérification manuelle instable du Browser pane, corrigée par suppression/reseed du profil via `sqlcmd`). |
| 2026-07-22, `e71d9ef` | Libellés visibles ajoutés aux derniers champs sans label du formulaire "Add a sheet rule". |
| 2026-07-22, `fa15791`→`8ba1ec5` | Parité visuelle (Q1/Q2/Q3/Q6) puis fonctionnelle (Q4/Q5) de `ExportProfileEditor.razor` avec `ImportProfileEditor.razor` — confirmé présent : `docs/tickets-tdd-blazor-profil-export-parite-visuelle.md` et `-fonctionnelle.md` existent sur disque, `ExportProfileEditor.razor` contient bien `back-to-export-profiles-button` (voir ligne suivante) et la structure `.sheet-rule-card` (vérifié par recherche de classe). |
| 2026-07-22, `7a7c414` (HEAD) | Bouton "Back to list" ajouté aux deux éditeurs de profil — confirmé : `back-to-import-profiles-button`/`back-to-export-profiles-button` trouvés dans les deux fichiers `.razor` respectifs par recherche directe. |

Aucun de ces éléments ne contredit ce que `CLAUDE.md` documente déjà à leur sujet ; ils sont listés
ici uniquement parce que la demande porte sur "tout ce qui a été fait depuis le 19/07", et que le
19/07 ne pouvait pas les connaître.

---

## 6. Items non bloquants déjà connus — statut

| Item | Statut au 19/07 | Statut réel au 22/07 |
|---|---|---|
| `ExcelETL.Hosting`/`ExcelETL.Hosting.Tests` absents de `ExcelETL.slnx` | Signalé, non corrigé | ✅ **Corrigé** — commit `3982f3f` (2026-07-20). Vérifié par lecture directe du `.slnx` (les deux projets y figurent) et par l'exécution réelle de `dotnet test ExcelETL.slnx` qui exécute bien `ExcelETL.Hosting.Tests.dll` (6/6, voir §1). |
| Renommage `PlatinesExtractionService`→`UnconditionalIsolementSheetExtractionService` non répercuté dans `tickets-tdd-extraction.md` | Signalé, cosmétique | Non revérifié dans cet audit (hors périmètre de la demande explicite) — présumé toujours vrai, à confirmer si besoin. |
| **Nouveau** : `ExcelProcessingClientService.Tests` (15 tests) absent de `ExcelETL.slnx` | Non détecté | ⚠️ **Toujours non corrigé, jamais signalé avant cet audit** — voir §1. Structurellement identique au trou déjà corrigé pour `Hosting`, mais concernant l'autre projet legacy. |

---

## 7. Écarts documentation ↔ code — récapitulatif

1. **Commentaires de code obsolètes** (`OxoController.cs`, `ProcessOxoFileService.cs`,
   `ClosedXmlWorkbookWriter.cs`) référant des classes supprimées par Lot K4 comme si elles
   existaient encore ou comme si le retrait était encore futur — voir §2. Cosmétique.
2. **`ExcelProcessingClientService.Tests` absent de `ExcelETL.slnx`** — voir §1 et §6. Le plus
   significatif des écarts trouvés dans cet audit : aucune doc, aucun ticket, aucun audit
   précédent ne le mentionne, alors que le projet de test correspondant existe et passe (15/15)
   depuis au moins Lot K3.
3. **Application effective des migrations EF Core sur une vraie base SQL Server** — affirmée
   vérifiée par `CLAUDE.md` (Lot G4, Lot K4) mais non re-confirmable dans cette session (serveur
   SQL injoignable). Ni une confirmation ni une infirmation — juste non vérifiable aujourd'hui.
4. **Reliquat de worktree Git** (`.claude/worktrees/elated-wilson-b9c165/`, détaché au commit
   `d125905`, antérieur à Lot K4) contenant encore les fichiers POC retirés sur `main`
   (`Mappings.razor`, `Dashboard.razor`, `History.razor`, `UploadTest.razor`). Pas un écart de
   code sur `main`, mais un espace de travail non nettoyé — à examiner (contient-il un travail en
   cours à préserver, ou est-il jetable ?) avant suppression.

Aucun autre écart structurel trouvé entre ce que documente `CLAUDE.md` et l'état réel du code
pour les points explicitement demandés (Lot K, Lot L, Lot M, seeding).

---

## 8. Non vérifiable depuis le dépôt seul

- **Application réelle des migrations `AddImportProfile`/`AddExportProfile`/`RemoveExtractionConfigPoc`
  sur une base SQL Server** — service SQL Server injoignable dans cette session (`localhost` et
  `(localdb)\MSSQLLocalDB` testés, tous deux en échec de connexion). Nécessiterait de relancer le
  service SQL Server local puis de requêter `__EFMigrationsHistory`.
- **Format définitif de `OXO_TRAME_IMPORT_MAD.xlsx`** — dépend d'une confirmation client externe
  au dépôt, non réexaminé dans cet audit (hors périmètre de la demande).

---

## Synthèse

- **Suite de tests** : 624/624 verts via `ExcelETL.slnx` (vs 613/613 le 19/07, expliqué en
  détail §1) **+ 15/15 verts dans un projet legacy toujours hors solution**, jamais détecté avant
  cet audit.
- **Lot K** : entièrement conforme à ce qu'annonce `CLAUDE.md` — route unique, ancienne route
  disparue, POC intégralement retiré de `main` (un reliquat existe seulement dans un worktree
  détaché), landing page `/` correcte. Seuls bémols : 3 commentaires de code obsolètes et
  l'application réelle des migrations non re-vérifiable aujourd'hui.
- **Lot L** : conforme à 100 % à sa documentation, aucun écart.
- **Lot M / seeding** : les deux confirmés implémentés intégralement, avec preuve de code et
  tests verts — correspondent bien à des échanges non tracés côté utilisateur.
- **Nouveau point à corriger** : ajouter `legacy/ExcelProcessingClientService/` et
  `legacy/ExcelProcessingClientService.Tests/` à `ExcelETL.slnx`, sur le même principe que ce qui
  a déjà été fait pour `ExcelETL.Hosting` le 2026-07-20.
