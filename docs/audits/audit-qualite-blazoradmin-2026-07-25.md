# Audit qualité — ExcelETL.BlazorAdmin

- **Date d'exécution** : 2026-07-25
- **Commit réellement audité** : `8119f78` (2026-07-25, "Ajout des audits qualite par couche et de l'etat d'avancement global") — un commit après la référence `d018a90` indiquée dans le brief ; aucun changement fonctionnel de `ExcelETL.BlazorAdmin` entre les deux (le commit `8119f78` n'ajoute que des documents d'audit).
- **Méthode** : lecture directe du code source (`.razor`, `.razor.css`, `.cs` de tests, `app.css`, `NavMenu.razor`) via Grep/Read/Glob. Aucune supposition tirée de l'historique CLAUDE.md — cet historique a été utilisé uniquement comme carte pour savoir où regarder, chaque affirmation ci-dessous est vérifiée sur le code tel qu'il existe aujourd'hui.
- **Portée** : uniquement `src/ExcelETL.BlazorAdmin` et `tests/ExcelETL.BlazorAdmin.Tests`. Aucune modification apportée — audit en lecture seule.

---

## 1. Respect de Clean Architecture / Onion

**Constat factuel** : recherche exhaustive de `HttpClient`, `AddHttpClient`, `api/oxo`, `http://`, `https://` dans `src/ExcelETL.BlazorAdmin/**/*.{razor,cs}` (hors `wwwroot/lib/bootstrap` tiers, hors `.resx`, hors `launchSettings.json`).

- **Aucune occurrence de `HttpClient` dans tout le projet** — ni déclaration de champ/paramètre, ni `builder.Services.AddHttpClient(...)` dans `Program.cs`.
- Les seules occurrences de `WebAPI`/`api/oxo/process` dans le code sont des **commentaires** ou du texte de ressource (`Program.cs:32,75,170`, `Excel/BrowserFileStreamBuffering.cs:5`, `Resources/BlazorAdminMessages.cs:6`, et la chaîne descriptive `BlazorAdminMessages.resx:953` affichée sur `/generated-files`).
- **Le brief du 2026-07-25 mentionne "l'exception documentée du typed HttpClient de la page `/upload-test`" — cette exception n'existe plus et le brief est obsolète sur ce point.** `UploadTest.razor`, `ExcelProcessingClient` et `IExcelDownloadInterop` ont été supprimés en totalité (Lot K4, 2026-07-21, confirmé par une recherche `Glob`/`Grep` ne retournant aucun résultat pour ces trois noms dans `src/`). Il n'existe aujourd'hui **aucune page BlazorAdmin qui appelle WebAPI en HTTP** : `ImportProfileTest.razor`/`ExportProfileTest.razor` exécutent le pipeline OXO entièrement en process (`ClosedXmlWorkbookReader` → `IImportPipelineOrchestrator` → `ISheetGenerationEngine`/`IWorkbookWriter`, tous injectés depuis `Application`/`Infrastructure`, jamais un `HttpClient`).

**Impact estimé** : aucun — c'est une confirmation positive (RAS), pas un défaut. Le seul enjeu est documentaire : le brief d'audit lui-même (et potentiellement d'autres documents qui font référence à cette "exception documentée") doit être mis à jour pour refléter la suppression du Lot K4, faute de quoi un futur audit ou une future revue continuera de chercher une exception qui n'existe plus.

**Refacto envisageable** : aucune sur le code. Mettre à jour le texte du brief/processus d'audit qui référence encore le typed HttpClient de `/upload-test`.

---

## 2. Parité Import/Export

### 2.1 Divergence confirmée — boutons Modifier/Supprimer d'une carte de règle de feuille (niveau racine de l'éditeur)

**Localisation** :
- `ImportProfileEditor.razor:192-202` (bloc `else` du `@if (_pendingDeleteIndex == index)`)
- `ExportProfileEditor.razor:127-149` (même position structurelle)

**Constat factuel** : côté Export, les boutons Modifier/Supprimer d'une carte `sheet-rule-card` sont des **boutons icône seule** (`class="btn btn-sm btn-outline-secondary block-field-icon-btn"` / `btn-outline-danger`, SVG inline, `aria-label`/`title`), conformes à `convention-ui-blazor-icones-boutons.md` (action CRUD standard → icône). Côté Import, au même niveau (carte de règle de feuille entière, pas les champs de bloc à l'intérieur), les boutons sont du **texte brut sans icône** :

```
<button id="@($"modify-sheet-rule-button-{index}")" class="btn btn-sm btn-secondary" @onclick="() => _editingIndex = index">
    @Loc["ImportProfileEditor_ModifyButton"]
</button>
<button id="@($"delete-sheet-rule-button-{index}")" class="btn btn-sm btn-danger" @onclick="() => _pendingDeleteIndex = index">
    @Loc["ImportProfileEditor_DeleteButton"]
</button>
```

Aucune des 3 suites de tests de parité croisée (`ProfileEditorParityTests.cs`) ne compare ce point précis — R1/R2/R3/30.5 comparent la grille `.sheet-rule-grid`, la grille de champs `.block-field-grid`, le disclosure `.sheet-rule-sublist-details`, les conteneurs `form-floating`, les cartes `bg-light`, un bouton d'ajout intermédiaire et le bouton de sauvegarde final — mais jamais le bouton Modifier/Supprimer de carte lui-même.

**Impact estimé** : moyen. C'est exactement le type de dérive silencieuse que Lot 030 avait pour but de corriger (le lot a explicitement traité la parité des labels/boutons intermédiaires/boutons Ajouter, mais pas ce niveau précis) — et exactement le type de gap qu'un futur screenshot client répétera si non corrigé, avec un coût de correction faible mais un risque de réapparaître ailleurs si la cause (absence de test de parité sur ce point) n'est pas comblée.

**Refacto envisageable** : aligner `ImportProfileEditor.razor` sur le motif icône d'`ExportProfileEditor.razor` (réutiliser les mêmes SVG que `SheetRuleForm.razor` utilise déjà pour ses propres boutons Modifier/Supprimer de champ de bloc), puis ajouter un test dans `ProfileEditorParityTests.cs` comparant les classes des boutons Modifier/Supprimer de carte entre les deux éditeurs, pour empêcher toute régression future.

### 2.2 Divergence structurelle — conteneur englobant

**Localisation** : `ExportProfileEditor.razor:19` (`<div class="container-fluid px-3">`) fermé en `:170`. Aucun équivalent dans `ImportProfileEditor.razor`.

**Constat factuel** : `MainLayout.razor:11` enveloppe déjà tout `@Body` dans `<article class="content px-4">`. `ExportProfileEditor.razor` ajoute un second niveau de conteneur avec son propre padding horizontal (`px-3` = 1rem), qu'`ImportProfileEditor.razor` n'a pas. Aucun commentaire dans le fichier n'explique cette différence ; elle n'apparaît dans aucun ticket répertorié dans le CLAUDE.md du projet.

**Impact estimé** : faible visuellement (léger différentiel de marge horizontale entre les deux pages), mais c'est un exemple concret de structure DOM non-identique entre deux pages qui sont censées être des gabarits jumeaux — susceptible de fausser une future comparaison de parité automatisée si elle utilise des sélecteurs de position/profondeur plutôt que des sélecteurs d'ID.

**Refacto envisageable** : supprimer le `<div class="container-fluid px-3">` d'`ExportProfileEditor.razor` (ou l'ajouter symétriquement à `ImportProfileEditor.razor` si son intention était délibérée — à confirmer, rien dans le code ne l'indique).

### 2.3 Parité confirmée (RAS) sur les points explicitement listés par le brief

- **Grille responsive Lot R (`.sheet-rule-grid`/`.block-field-grid`)** : classes identiques appliquées aux deux éditeurs (`ImportProfileEditor.razor:111,134`, `ExportProfileEditor.razor:54,87`), et testées littéralement identiques par `ProfileEditorParityTests.SheetRuleGrid_CssClass_IsIdenticalBetweenImportAndExportEditors`/`BlockFieldGrid_CssClass_...`.
- **`form-floating` Lot 030** : conteneurs `mb-3`/`form-floating` identiques sur les deux champs racine testés (`RootFieldContainer_CssClass_IsIdenticalBetweenImportAndExportEditors`), cartes `bg-light` identiques (`SubformCardContainer_CssClass_...`).
- **Boutons Lot X/Y (bouton d'ajout intermédiaire, bouton de sauvegarde final)** : classes `btn btn-outline-secondary w-100 mt-3` et `btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4` identiques et testées (`IntermediateAddButton_CssClass_...`, `FinalSaveButton_CssClass_...`).
- **Boutons d'en-tête des pages de liste (Lot 030.7)** : `ImportProfiles.razor:25-32` et `ExportProfiles.razor:20-27` partagent exactement `right-aligned-actions d-flex gap-2 mb-3` + `flex-fill`, testé par `ProfileListPageParityTests`.
- **`<InputFile>` (Lot V, partie B)** : `ImportProfileTest.razor:65` et `ExportProfileTest.razor:65` restent tous deux un unique élément `<InputFile>` avec `multiple`, sans wrapper qui casserait `FindComponent<InputFile>()`.
- **Page Journaux (`Logs.razor`)** : conserve bien son tableau natif sans variante carte mobile (`Logs.razor:62`, aucune occurrence de `d-md-none`) — exclusion documentée et respectée.

### 2.4 Angle mort de couverture (voir aussi §6)

`ProfileListPageParityTests.cs` (2 tests) ne compare que le conteneur et les 2 boutons d'en-tête (`test-*-profile-button`/`create*-button`) entre `ImportProfiles.razor`/`ExportProfiles.razor`. **Aucun test de parité croisée ne compare les boutons d'action de ligne** (Modifier/Dupliquer/Supprimer, icône SVG) entre les deux pages de liste, alors que leur code est dupliqué presque à l'identique (voir §4) — un divergence future y serait indétectable par les tests de parité existants tant qu'elle ne casse pas un test unitaire propre à l'une des deux pages.

---

## 3. NavMenu / visibilité selon rôle

**Constat factuel** — `NavMenu.razor` (112 lignes) / `NavMenuTests.cs` (316 lignes, 19 `[Fact]`) :

- Les 4 liens admin (`nav-import-profiles-link`, `nav-export-profiles-link`, `nav-users-link`, `nav-generated-files-link`, tableau `AdminLinkIds` à `NavMenuTests.cs:112-118`) sont vérifiés par **absence DOM réelle** : `NavMenu_WhenNotAuthorized_HidesAdminLinks_AndShowsLoginLink` (`cut.FindAll($"#{id}").Should().BeEmpty()`, ligne 129) et présence exacte (`HaveCount(1)`, ligne 145).
- Le lien Journaux (régression corrigée au Lot L2) est couvert par une absence DOM réelle dédiée : `NavMenu_WhenNotAuthorized_HidesLogsLink` (`cut.FindAll("#nav-logs-link").Should().BeEmpty()`, ligne 168) et sa présence pour un utilisateur authentifié sans rôle Admin (`NavMenu_WhenAuthorized_WithoutAdminRole_ShowsLogsLink`, ligne 178).
- Le lien de connexion (régression de duplication corrigée au Lot L2) a un test d'unicité stricte par ID : `NavMenu_WhenNotAuthorized_ShowsLoginLink_ExactlyOnce` (`FindAll("#nav-login-link").Should().HaveCount(1)`, ligne 158), et `NavMenu_WhenAuthorized_DoesNotRenderStandaloneUsernameElement` vérifie l'absence de l'ancien `span.nav-link` orphelin (ligne 99).
- Les liens de test de profil (`nav-import-profiles-test-link`/`nav-export-profiles-test-link`, retirés au Lot S) sont vérifiés absents par ID (`NavMenu_WhenAuthorizedAsAdmin_DoesNotRenderProfileTestLinks`, lignes 188-189) — pas seulement "absents d'une liste attendue".

**Point faible identifié** : deux tests utilisent encore une assertion sur le **texte du markup entier**, pas sur un ID/élément précis :
- `NavMenu_WhenNotAuthorized_DoesNotShowProfileLink` (`NavMenuTests.cs:109`) : `cut.Markup.Should().NotContain("My Profile")` — alors qu'un test ID-based existe déjà pour ce même lien ailleurs dans le fichier (`nav-profile-link`, ligne 85-89) et aurait pu être réutilisé ici.
- `NavMenu_WhenNotAuthorized_AndEnglishCulture_ShowsEnglishLinks`/`...AndFrenchCulture_ShowsFrenchLinks` (lignes 39-40, 50-51) : `cut.Markup.Should().Contain("Register")`/`"Login"` — vérifie la présence de texte, pas l'élément lui-même par ID.

**Impact estimé** : faible en pratique (ces 3 tests portent sur des libellés suffisamment spécifiques pour ne pas produire de faux positif aujourd'hui), mais c'est une incohérence de convention par rapport à la règle "toujours vérifier par absence/présence DOM réelle, jamais par texte" que le reste du fichier respecte scrupuleusement — et c'est précisément la classe de test qui a laissé passer la régression du Lot L1 (le texte "Journaux" restait présent dans le markup, la vérification par texte ne l'aurait pas détecté si elle avait cherché la bonne chose).

**Refacto envisageable** : remplacer les 3 assertions `cut.Markup.Should().Contain/NotContain(...)` ci-dessus par des sélections par ID (`#nav-profile-link`, `#nav-login-link`/liens Register déjà présents sans ID dédié — en ajouter un si nécessaire pour ce test).

---

## 4. Duplication

### 4.1 `ImportProfiles.razor` / `ExportProfiles.razor` — duplication quasi intégrale

**Localisation** : `ImportProfiles.razor` (262 lignes) / `ExportProfiles.razor` (234 lignes).

**Constat factuel** :
- Les 3 constantes `PencilIconMarkup`/`CopyIconMarkup`/`TrashIconMarkup` (SVG inline complets) sont **répétées mot pour mot** dans les deux fichiers (`ImportProfiles.razor:167-179`, `ExportProfiles.razor:152-162`).
- La méthode `BuildAvailableDuplicateName(string profileName)` (résolution de collision de nom par suffixe auto-incrémenté, Lot 027.4) est **dupliquée à l'identique**, à l'exception du type de la liste `_profiles` qu'elle referme (`ImportProfiles.razor:236-261`, `ExportProfiles.razor:208-233`) — même regex, même boucle, même logique de normalisation `Trim()`/`OrdinalIgnoreCase`.
- Le rendu tableau desktop + carte mobile (structure `<table class="table d-none d-md-table">` / `<div class="d-md-none">`) est dupliqué avec seulement les colonnes/labels qui diffèrent (Import a une colonne `EquipementTypeElementNom` que Export n'a pas — différence fonctionnelle légitime).
- Le bloc de confirmation de suppression inline (deux boutons Confirmer/Annuler avec IDs `confirm-delete-*-button-{id}`/`cancel-delete-*-button-{id}`) est dupliqué à l'identique 4 fois au total (table+carte × Import+Export).

**Impact estimé** : moyen. Ces deux fichiers ont déjà divergé une fois sur un point non fonctionnel (l'icône Modifier/Dupliquer/Supprimer, Lots V3/028) et pourraient très facilement diverger de nouveau lors d'un futur correctif appliqué à un seul des deux fichiers par inadvertance — le risque n'est pas hypothétique, c'est le scénario exact qui a déclenché Lot 030. Le brief demande explicitement de ne pas casser "un seul jeu de données, deux gabarits d'affichage" (Lot V2) : la duplication actuelle n'est pas celle-là (elle ne duplique pas le chargement des données, seulement le markup et deux méthodes utilitaires), donc un refacto est possible sans toucher à ce principe.

**Refacto envisageable** : extraire les 3 constantes SVG dans un composant/fichier statique partagé (ex. `Shared/AdminIconMarkup.cs`), et factoriser `BuildAvailableDuplicateName` en une méthode générique paramétrée par `IReadOnlyList<string> existingNames` plutôt que par le type concret de profil — les deux extractions sont mécaniques et à faible risque, sans toucher à la duplication légitime du templating tableau/carte (qui diffère fonctionnellement).

### 4.2 `Users.razor` — pas de duplication significative avec les deux pages ci-dessus

`Users.razor` (66 lignes) partage le même motif tableau/carte responsive (`d-none d-md-table`/`d-md-none`) mais n'a ni actions de ligne, ni logique de duplication de nom, ni icônes SVG — la surface de duplication réelle avec `Users.razor` est minime (juste la structure de templating responsive elle-même, qui est un idiome délibérément répété page par page selon Lot V2, pas une duplication accidentelle).

### 4.3 Asymétrie de factorisation entre `SheetRuleForm.razor` et `SheetGenerationRuleForm.razor`

**Constat factuel** : côté Export, les 3 sous-listes (`ColumnDefinition`, `PointColumnDefinition`, `ApplicationColumnDefinition`) sont **chacune** extraites dans leur propre composant dédié (`ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`, `ApplicationColumnDefinitionForm.razor`). Côté Import, seule la sous-liste `BlockFieldDefinition` a été extraite (`BlockFieldForm.razor`) — les deux autres sous-listes (`UnconditionalColonneNames`, `ConditionalPointRule`, ajoutées au Lot W) restent **entièrement inline** dans `SheetRuleForm.razor`, avec leur propre état d'édition (`_editingUnconditionalColonneIndex`, `_editingPointRuleIndex`, etc.) dupliqué dans le même fichier plutôt que dans des composants dédiés suivant le même patron que `BlockFieldForm.razor`.

Cela explique directement pourquoi `SheetRuleForm.razor` (585 lignes) est le fichier `.razor` le plus long du projet alors que son équivalent structurel `SheetGenerationRuleForm.razor` ne fait que 405 lignes pour une responsabilité comparable (3 sous-listes éditables) — voir §7.

**Impact estimé** : moyen. Ce n'est pas un défaut fonctionnel (les tests passent, Lot W a été livré correctement), mais une incohérence d'architecture entre les deux familles de formulaires qui rend `SheetRuleForm.razor` nettement plus difficile à maintenir que son pendant Export, et alourdit toute évolution future des deux listes concernées.

**Refacto envisageable** : extraire `UnconditionalColonneNameForm.razor`/`ConditionalPointRuleForm.razor` sur le modèle de `BlockFieldForm.razor`/`ColumnDefinitionForm.razor`, ramenant `SheetRuleForm.razor` à une taille comparable à `SheetGenerationRuleForm.razor`.

---

## 5. Cohérence des conventions déjà actées

### 5.1 IDs stables en bUnit

**Constat factuel** : recherche de `.Find("button")`, `.Find("a")`, `.Find("input")`, `.Find("select")`, `.FindAll("button")` etc. (sélection par tag seul, sans ID ni classe) dans tout `tests/ExcelETL.BlazorAdmin.Tests` : **aucune occurrence**. Toutes les sélections d'éléments interactifs passent par un ID (`cut.Find("#xxx-input")`) ou, pour des assertions de contenu (non d'interaction), par une classe structurelle (`.block-field-name`, `.sheet-rule-card-title`) combinée à une comparaison de `TextContent` — ce qui est une vérification de contenu, pas une sélection d'élément interactif par position/texte. **RAS sur ce point précis.**

Seule nuance mineure hors périmètre strict de la règle : `ProfileTests.cs:181` sélectionne un `<h2>` par texte (`cut.FindAll("h2").Single(h => h.TextContent.Contains("Security"))`) — un titre de section, pas un élément interactif, donc hors du champ strict de la règle mais à signaler par souci d'exhaustivité.

### 5.2 `convention-ui-blazor-alignement-boutons.md` (boutons à droite)

**Constat factuel** : tous les groupes de boutons d'action de contenu (Ajouter/Enregistrer/Annuler/Modifier/Supprimer de carte, bouton de sauvegarde de profil) sont enveloppés dans `.right-aligned-actions` (`display:flex; justify-content:flex-end`, `app.css:120-124`) de façon cohérente entre `ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `SheetRuleForm.razor`, `SheetGenerationRuleForm.razor`, `ImportProfiles.razor`, `ExportProfiles.razor`. **RAS.**

Un seul point à signaler pour information : la convention elle-même (`convention-ui-blazor-alignement-boutons.md:35-37`) cite explicitement `modify-sheet-rule-button-{i}`/`delete-sheet-rule-button-{i}` comme exemple de boutons "de bas de carte" régis par l'alignement — mais reste **muette sur la présence/absence d'icône** à cet endroit, ce qui laisse la porte ouverte à la divergence documentée en §2.1 : les deux documents de convention (alignement vs icônes) ne se contredisent pas frontalement, mais ne couvrent pas explicitement ensemble ce point précis, ce qui a probablement contribué à la dérive.

### 5.3 `convention-ui-blazor-icones-boutons.md` (icônes + aria-label/title)

**Constat factuel** : tous les boutons icône-seule identifiés (`block-field-icon-btn` dans `SheetRuleForm.razor`, `SheetGenerationRuleForm.razor`, `ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`, `ApplicationColumnDefinitionForm.razor`, `ImportProfiles.razor`, `ExportProfiles.razor`) portent un `aria-label`. **Un sous-ensemble n'a pas de `title`** en plus de l'`aria-label`, alors que la convention l'exige explicitement ("il **doit** porter un `aria-label` ... et un tooltip visuel (`title` ...)") :
- `ExportProfileEditor.razor:132,141` (Modifier/Supprimer de carte export) : `aria-label` + `title` présents — conforme.
- `SheetRuleForm.razor:78,87` (Modifier/Supprimer de champ de bloc import) : **`aria-label` présent, mais pas de `title`**.
- `SheetGenerationRuleForm.razor` (Modifier/Supprimer d'`ApplicationColumnDefinition`, lignes 198,207) : **`aria-label` présent, pas de `title`** non plus.
- Les boutons Modifier/Supprimer de champ de bloc *unconditional colonne*/*point rule* ajoutés au Lot W (`SheetRuleForm.razor:132-133,141-142,161-162,171-172,240-241,249-250,270-271,280-281`) ont bien les deux (`aria-label` **et** `title`).

**Impact estimé** : faible (l'`aria-label` seul satisfait déjà l'accessibilité lecteur d'écran ; c'est le confort souris/tooltip visuel qui manque par endroits) mais c'est une application incohérente d'une règle pourtant explicite et déjà correctement suivie ailleurs dans le même fichier (Lot W l'a bien fait, Lot O1/U4 ne l'ont pas fait).

**Refacto envisageable** : ajouter `title="@Loc[...]"` aux boutons icône identifiés ci-dessus, en réutilisant la même clé de ressource que leur `aria-label`.

### 5.4 `<InputFile>` — RAS (voir §2.3)

### 5.5 Asymétrie fonctionnelle non couverte par les conventions écrites : listes racine "add-only" sans édition/suppression

**Constat factuel** : `ImportProfileEditor.razor:60-101` (`DefaultTableaux`/`DefaultApplicationNames`, ajoutés au Lot U) n'offrent **aucun bouton Modifier/Supprimer par élément** — uniquement un formulaire d'ajout et un paragraphe récapitulatif en texte joint par virgules (`<p id="default-tableaux-summary">@string.Join(", ", _defaultTableaux)</p>`). C'est le même patron "ajout seul" que `SheetRuleForm.razor` avait pour `UnconditionalColonneNames`/`ConditionalPointRule` **avant** le Lot W — mais le Lot W n'a traité que les sous-listes de `SheetRuleForm.razor`, pas les listes racine de `ImportProfileEditor.razor` elles-mêmes, introduites par un lot ultérieur (Lot U) au Lot W (Lot W lui est antérieur au 2026-07-24/Lot U du même jour — vérifié par la date des tickets dans le CLAUDE.md). Il n'existe donc aujourd'hui, dans tout le projet, **aucun moyen de corriger une faute de frappe dans un nom de Tableau/Application par défaut sans recréer tout le profil**.

**Impact estimé** : faible à moyen sur le plan produit (source d'irritation utilisateur documentée ailleurs dans ce même projet — c'est exactement le problème que Lot W a résolu pour les listes de `SheetRuleForm.razor`), mais à signaler car ce n'est la responsabilité d'aucune convention écrite existante et pourrait être oublié.

**Refacto envisageable** : appliquer le même patron Modifier/Supprimer-en-place que Lot W a introduit pour `UnconditionalColonneNames` aux deux listes `DefaultTableaux`/`DefaultApplicationNames`.

---

## 6. Dette de test

### 6.1 Comptage des tests par fichier (`[Fact]`/`[Theory]`)

| Fichier de test | Tests | Lignes | Fichier(s) source couvert(s) principal(aux) | Lignes source |
|---|---|---|---|---|
| `ImportProfileEditorTests.cs` | 94 | 1834 | `ImportProfileEditor.razor` + `SheetRuleForm.razor` + `BlockFieldForm.razor` | 348+585+136=1069 |
| `ExportProfileEditorTests.cs` | 74 | 1418 | `ExportProfileEditor.razor` + `SheetGenerationRuleForm.razor` + 3 sous-formulaires | 260+405+128+123+118=1034 |
| `ImportProfileTestTests.cs` | 31 | 867 | `ImportProfileTest.razor` | 468 |
| `ExportProfileTestTests.cs` | 30 | 1060 | `ExportProfileTest.razor` | 510 |
| `ImportProfilesTests.cs` | 22 | 469 | `ImportProfiles.razor` | 262 |
| `ExportProfilesTests.cs` | 21 | 432 | `ExportProfiles.razor` | 234 |
| `NavMenuTests.cs` | 19 | 316 | `NavMenu.razor` | 112 |
| `GeneratedFilesTests.cs` | 9 | 236 | `GeneratedFiles.razor` | 221 |
| `LogsTests.cs` | 9 | 188 | `Logs.razor` | 243 |
| `BatchFileValidatorTests.cs` | 9 | 113 | `Excel/BatchFileValidator.cs` (support) | — |
| `FormFloatingStructureAuditTests.cs` | 7 | 322 | transverse (5 pages) | — |
| `ProfileEditorParityTests.cs` | 7 | 240 | parité croisée Import/Export éditeurs | — |
| `UsersTests.cs` | 3 | 79 | `Users.razor` | 66 |
| `ProfileListPageParityTests.cs` | 2 | 76 | parité croisée Import/Export listes | — |
| `BrowserFileStreamBufferingTests.cs` | 1 | 62 | `Excel/BrowserFileStreamBuffering.cs` (support) | — |

### 6.2 Zone de couverture la plus faible identifiée — `ApplicationColumnDefinition` (Modifier/Supprimer)

**Localisation** : `SheetGenerationRuleForm.razor:175-216` (Modifier/Supprimer d'une `ApplicationColumnDefinition` déjà ajoutée, introduit au Lot U4).

**Constat factuel** : recherche de `modify-application-column`/`delete-application-column`/`save-application-column`/`cancel-application-column` dans tout `tests/ExcelETL.BlazorAdmin.Tests` → **aucune occurrence**. Seul le chemin d'**ajout** d'une `ApplicationColumnDefinition` est testé (`ExportProfileEditorTests.cs:382-431` : sauvegarde, labels visibles, masquage pour `PivotSource.TacheMultiple`). Le patron Modifier/Sauvegarder/Annuler/Supprimer-en-place existe bel et bien dans le markup (boutons icône avec IDs `modify-application-column-definition-button-{i}`/`delete-application-column-definition-button-{i}`) mais **aucun test ne clique dessus** — contrairement à `ColumnDefinition`/`PointColumnDefinition`/`SheetGenerationRule` elles-mêmes, dont Lot Q documente explicitement "5 tests chacun" pour ce même cycle modifier/pré-remplir/sauvegarder/annuler/supprimer.

**Impact estimé** : moyen — c'est un chemin de code utilisateur entièrement fonctionnel (les boutons sont câblés, `HandleSaveApplicationColumn`/`DeleteApplicationColumn` existent) mais à ce jour non couvert par un seul test, sur la fonctionnalité la plus récemment ajoutée au formulaire d'export (Lot U, 2026-07-24) — exactement la zone que le brief demande de vérifier en priorité.

**Refacto envisageable** : ajouter les ~5 tests manquants (modifier/pré-remplir/sauvegarder-en-place/annuler/supprimer) pour `ApplicationColumnDefinition`, sur le modèle exact des tests déjà écrits pour `PointColumnDefinition`.

### 6.3 Autres zones à couverture relativement faible

- **`GeneratedFiles.razor`** (221 lignes, page la plus récente du projet — Lot 034, livrée le jour même de cet audit) : 9 tests, contre 22/21 pour `ImportProfiles.razor`/`ExportProfiles.razor` (262/234 lignes, complexité comparable : recherche, tableau/carte responsive, téléchargement paresseux). La couverture existante est fonctionnellement solide (vide, badges de statut, absence de bouton cible sur rejet, recherche, effacement de recherche, téléchargement source/cible, gabarit carte) mais reste plus mince en volume relatif que les pages de liste de profils, et ne teste aucune des deux langues (`fr-FR`) alors que d'autres pages de liste le font (`ImportProfilesTests.cs`/`ExportProfilesTests.cs`/`LogsTests.cs` ont chacune au moins un test `fr-FR`).
- **Aucun test en culture `fr-FR`** dans `ImportProfileEditorTests.cs`, `ExportProfileEditorTests.cs`, `ImportProfileTestTests.cs`, `ExportProfileTestTests.cs`, `GeneratedFilesTests.cs`, `UsersTests.cs`, `FormFloatingStructureAuditTests.cs`, `ProfileEditorParityTests.cs`, `ProfileListPageParityTests.cs` — seuls `ImportProfilesTests.cs`/`ExportProfilesTests.cs`/`LogsTests.cs` en contiennent un chacun. Pour les deux plus gros fichiers de tests du projet (`ImportProfileEditorTests.cs`/`ExportProfileEditorTests.cs`), l'absence totale de vérification `fr-FR` est notable vu le volume de clés `.resx` FR ajoutées lot après lot sur ces mêmes pages.

### 6.4 Tests vérifiant une classe CSS sans comportement associé (ou l'inverse)

**Constat factuel** : `ProfileEditorParityTests.cs` et `ProfileListPageParityTests.cs` sont, par construction, des tests de **classe CSS pure** (`.GetAttribute("class").Should().Be(...)`), sans clic ni vérification de comportement — ce qui est cohérent avec leur objectif déclaré (garde-fou de non-régression visuelle) et documenté comme tel en commentaire. Ce n'est donc pas un défaut en soi. En revanche, `FormFloatingStructureAuditTests.cs` mélange les deux dans le même test générique (`AssertAllFormFloatingFieldsAreStructurallyValid`) — structure DOM (input avant label) et présence d'attribut (`placeholder` non vide) — sans vérifier le comportement réel de flottaison CSS (impossible à observer en bUnit, qui n'exécute pas de moteur de rendu CSS) : c'est une limite structurelle de bUnit plutôt qu'un défaut de test, mais elle mérite d'être connue — un test vert ici ne garantit pas que le label flotte visuellement correctement, seulement que les préconditions HTML de Bootstrap sont réunies.

---

## 7. Lisibilité / complexité

### 7.1 Nombre de lignes par composant (`Components/Pages/Admin/` + `Components/Layout/`)

| Fichier | Lignes | Remarque |
|---|---|---|
| `SheetRuleForm.razor` | **585** | Le plus long du projet — voir §4.3 : asymétrie de factorisation avec `SheetGenerationRuleForm.razor` |
| `ExportProfileTest.razor` | **510** | Page de test batch (upload multiple, accordéons imbriqués par fichier/section, génération) |
| `ImportProfileTest.razor` | **468** | Idem côté import |
| `SheetGenerationRuleForm.razor` | **405** | Équivalent export de `SheetRuleForm.razor`, mais mieux factorisé (3 composants enfants dédiés) |
| `ImportProfileEditor.razor` | 348 | |
| `ImportProfiles.razor` | 262 | |
| `ExportProfileEditor.razor` | 260 | |
| `Logs.razor` | 243 | |
| `ExportProfiles.razor` | 234 | |
| `Profile.razor` | 230 | |
| `GeneratedFiles.razor` | 221 | |
| `BlockFieldForm.razor` | 136 | |
| `ColumnDefinitionForm.razor` | 128 | |
| `ApplicationColumnDefinitionForm.razor` | 123 | |
| `PointColumnDefinitionForm.razor` | 118 | |
| `NavMenu.razor` | 112 | |
| `Users.razor` | 66 | |
| `ReconnectModal.razor` | 33 | |
| `PageBackNavLink.razor` | 31 | |
| `MainLayout.razor` | 21 | |

**Moyenne** (20 fichiers) : ≈ 227 lignes. **5 fichiers dépassent nettement cette moyenne** (>1.5×, soit >340 lignes) : `SheetRuleForm.razor` (585), `ExportProfileTest.razor` (510), `ImportProfileTest.razor` (468), `SheetGenerationRuleForm.razor` (405), `ImportProfileEditor.razor` (348).

**Constat factuel par fichier** :

- **`SheetRuleForm.razor` (585 lignes)** : combine 4 responsabilités dans un seul composant — champs du localisateur, sous-liste `BlockFieldDefinition` (déléguée à `BlockFieldForm.razor`), sous-liste `UnconditionalColonneNames` (état d'édition entièrement inline, non délégué), sous-liste `ConditionalPointRule` (état d'édition entièrement inline, non délégué). C'est la cause directe de sa taille — voir refacto proposé en §4.3.
- **`ImportProfileTest.razor` (468) / `ExportProfileTest.razor` (510)** : la taille est en grande partie justifiée fonctionnellement (upload multi-fichiers Lot 033, accordéon par fichier, 5 sections imbriquées par résultat côté import — Equipement/Isolements/Points/Taches multiples/Warnings — ou N feuilles générées dynamiquement côté export). Il n'y a pas de découpage en sous-composants pour l'affichage d'un fichier de batch individuel alors que la structure s'y prête (un composant `BatchFileResultCard` par fichier réduirait la profondeur d'imbrication du `@foreach` principal), mais l'absence de découpage n'est pas documentée comme un choix délibéré dans le code (contrairement à d'autres décisions de ce projet qui sont systématiquement commentées).
- **`ImportProfileEditor.razor` (348 lignes)** : taille cohérente avec sa responsabilité (page racine + listes `DefaultTableaux`/`DefaultApplicationNames` + liste de cartes de règles de feuille) ; pas de découpage supplémentaire manifestement nécessaire.

**Impact estimé** : moyen pour `SheetRuleForm.razor` spécifiquement (maintenabilité réduite, risque accru de bug lors d'une prochaine modification touchant l'une de ses 3 sous-listes, cf. §4.3), faible pour `ImportProfileTest.razor`/`ExportProfileTest.razor` (taille en grande partie justifiée par la fonctionnalité, bien testée par ailleurs).

**Refacto envisageable** : voir §4.3 pour `SheetRuleForm.razor`. Pour les pages de test, extraire un composant `BatchFileResultCard`/`BatchFileAccordion` par fichier de batch réduirait la longueur des deux fichiers sans changer le comportement, mais reste d'un intérêt plus marginal vu leur bonne couverture de tests actuelle.

---

## Non couvert / incertain

- **Vérification runtime réelle (navigateur)** : aucune vérification en navigateur n'a été effectuée dans le cadre de cet audit — analyse strictement statique du code source et des tests. Les divergences visuelles signalées (§2.1, §2.2) sont des faits de code (classes/markup différents), pas des captures d'écran comparées.
- **`docs/convention-ui-blazor-tableaux-generes-lisibilite.md`** (convention plus récente, tableaux générés) n'a pas été confrontée systématiquement à `ImportProfileTest.razor`/`ExportProfileTest.razor` au-delà des points explicitement demandés par la grille de critères — un passage dédié à cette convention spécifique n'a pas été fait.
- **Couverture exhaustive des 152 occurrences de `cut.Markup.Should().Contain(...)`** dans `tests/ExcelETL.BlazorAdmin.Tests` : seul un échantillon ciblé (NavMenu, et une recherche de sélection positionnelle `.Find("button")`/`.Find("a")` sans ID) a été vérifié en détail. Il est possible que d'autres tests utilisent `Markup.Should().Contain(...)` comme unique assertion sur la présence d'un bouton là où un sélecteur par ID serait plus robuste — un passage fichier par fichier plus systématique n'a pas été fait faute de temps dans le cadre de cet audit.
- **`Profile.razor`, `ReconnectModal.razor`, `PageBackNavLink.razor`, `MainLayout.razor`** : lus pour le comptage de lignes et les vérifications transverses (form-floating, structure NavMenu) mais pas audités ligne à ligne pour des problèmes propres à ces fichiers en dehors des critères de la grille.
- **Volume exact de tests par assertion** (nombre d'`Assert`/`Should()` par test, taille moyenne d'un test) n'a pas été mesuré — seul le nombre de méthodes `[Fact]`/`[Theory]` a été compté, ce qui peut sous-estimer ou surestimer l'effort de test réel selon la densité d'assertions par méthode.
- **Cohérence des clés `.resx`** (doublons, clés orphelines) : non auditée — hors grille de critères demandée, et déjà partiellement traitée par l'historique du projet (élagage documenté à plusieurs lots).
- **Hors périmètre — observé en passant** : le brief de cet audit lui-même contient une référence obsolète (l'exception `/upload-test`, voir §1) qui devrait être corrigée dans le processus d'audit pour les prochaines exécutions, mais cela ne concerne pas le code de `ExcelETL.BlazorAdmin` lui-même.
