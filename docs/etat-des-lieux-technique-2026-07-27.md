# État des lieux technique — vérification indépendante post-Lot 044 (2026-07-27)

*Instantané daté, jamais mis à jour en place (voir `docs/conventions/convention-nommage-documents.md`
catégorie 2) — à ne pas confondre avec un document vivant. Basé exclusivement sur une lecture
directe du code sur `main` au commit `6d9d6f6` (2026-07-27), exécution réelle de
`dotnet test ExcelETL.slnx`, `git log`/`git status`/`git fetch`, et lecture croisée de
`CLAUDE.md` contre les fichiers source réels. Aucune décision déjà actée dans un ticket ou une
spec n'a été rouverte — les points listés ci-dessous sont soit non ticketés, soit des
divergences factuelles entre la doc et le code.*

---

## 1. Suite de tests — confirmation exacte

**Exécution réelle, `dotnet test ExcelETL.slnx --verbosity quiet`, aujourd'hui :**

```
ExcelETL.Hosting.Tests.dll                     :    6/6   réussis
ExcelETL.Domain.Tests.dll                      :  278/278 réussis
Legacy.NewApiPingService.Tests.dll             :    9/9   réussis (net48)
Legacy.ExcelProcessingClientService.Tests.dll  :   15/15  réussis (net48)
ExcelETL.Application.Tests.dll                 :  152/152 réussis
ExcelETL.Infrastructure.Tests.dll              :  170/170 réussis
ExcelETL.WebAPI.Tests.dll                      :   25/25  réussis
ExcelETL.BlazorAdmin.Tests.dll                 :  548/548 réussis
```

**Total : 1203/1203 verts, zéro échec, zéro build cassé.** Chiffre exact et identique à celui
revendiqué par `CLAUDE.md` en clôture du Lot 044 ("1203 tests"). Aucun projet de test n'échappe à
`dotnet test ExcelETL.slnx` (le rattachement du projet legacy, résolu le 24/07, tient toujours).

**Deux avertissements de build (`NU1902`/`NU1903`) sur `dotnet test`** — connus depuis l'audit du
25/07 (`etat-avancement-global-2026-07-25.md` §4), toujours présents, non aggravés :
`System.Security.Cryptography.Xml` 10.0.9 (haute gravité, transitif sur `ExcelETL.WebAPI.Tests`),
`AngleSharp` 1.4.0 (moyenne, dépendance bUnit sur `ExcelETL.BlazorAdmin.Tests`). N'affectent aucun
test — non retraités ici, `docs/conventions/procedure-mise-a-jour-packages.md` reste la procédure
de référence.

**Nouveaux avertissements de compilation, non présents dans le rapport du 25/07** : 4 occurrences
de `CS8604` ("argument de référence null possible") sur `ImportProfiles.razor`/`ExportProfiles.razor`,
lignes du message de confirmation de suppression (`@Loc["ImportProfiles_ConfirmDeleteMessage",
_profileNamePendingDeletion]` — `_profileNamePendingDeletion` est `string?`). Vérifié par lecture :
le champ est toujours affecté avec `_profileIdPendingDeletion` dans le même appel (`RequestDeleteProfile`),
donc jamais réellement `null` au moment où ce bloc est rendu — faux positif de l'analyse de
nullabilité (le compilateur ne peut pas corréler les deux champs), pas un bug fonctionnel. Voir §3.

**Git** : `main` local au commit `6d9d6f6`, arbre de travail propre (`git status` : rien à
commit). `git fetch origin` confirme **7 commits locaux non poussés** — tous de purs
déplacements de fichiers doc (réorganisation `docs/tickets/`, `docs/audits/`, `docs/reference/`,
`docs/conventions/`, 16 insertions / 446 suppressions au total, aucune ligne de code source
concernée). Le code du Lot 044 lui-même (commits `70c8a70` → `3ec55c2`) est déjà sur
`origin/main`. Point mineur mais réel au regard de la convention de session ("pousser après
chaque commit") : à pousser avant la prochaine session.

---

## 2. Vérification ciblée des revendications CLAUDE.md pour les Lots 037→044

Confirmé par lecture directe du code, pas seulement par confiance dans la doc vivante :

| Revendication | Vérifié | Constat |
| :--- | :--- | :--- |
| Lot 037 — parité icônes Modifier/Supprimer carte de règle de feuille | ✅ | `AdminIconMarkup.Pencil`/`.Trash` utilisés à l'identique dans `ImportProfileEditor.razor` et `ExportProfileEditor.razor` pour les boutons de carte. |
| Lot 038 — `ApiTest.razor` | ✅ | Route `/api-test`, `OxoApiTestClient` réel (`AddHttpClient`), configuration `OxoApiTestClient:BaseUrl`/`ApiKey` présente dans les 2 `appsettings.Development.json`. |
| Lot 039 — accessibilité clavier toggler | ✅ | `aria-label`/`aria-controls="nav-scrollable"`/`aria-expanded="@(_isNavExpanded ...)"` présents sur `<input id="nav-menu-toggler">` (`NavMenu.razor:15-18`). |
| Lot 040 — `role="alert"`/`aria-live` | ✅ | Les 20 `alert alert-danger` recensés par l'audit du 27/07 portent tous `role="alert"` ; `#test-status-region`/`#export-test-status-region` (`aria-live="polite"`) présents et déjà montés au premier rendu ; bandeaux de confirmation de suppression (`ImportProfiles.razor`/`ExportProfiles.razor`) portent `role="alert"`. |
| Lot 041 — convention icônes | ✅ | `AdminIconMarkup.Plus`/`.Check`/`.Send`/`.FileEarmarkSpreadsheet` existent et sont utilisés sur `create-profile-button`, `save-profile-button`, `process-button`, `generate-workbook-button` ; `log-copy-btn` porte désormais `aria-label`. |
| Lot 042 — sauts de titres + `aria-describedby` + parité `container-fluid` | ✅ | `NotFound.razor` a un `h1` ; `ImportProfileEditor.razor` a `<div class="container-fluid px-3">` (identique à Export) ; `Login.razor` lie chaque `ValidationMessage` via un `<div id="Input.X-validation">` + `aria-describedby`. |
| Lot 043 — confirmation de navigation | ✅ | `<NavigationLock>` présent dans les deux éditeurs, `_hasUnsavedChanges` posé sur chaque mutation. Non ré-audité en détail ici (hors périmètre de l'audit design du 27/07, qui précède ce lot). |
| Lot 044 — gestion des utilisateurs | ⚠️ Partiel | Migration, service, page `Users.razor`, restriction `#nav-logs-link` : tous présents et testés. **Le flag `RequirePasswordChangeOnFirstLogin` est posé (`true`) à la création/réinitialisation mais n'est lu nulle part dans le flux de connexion** — voir §3, écart concret par rapport au texte même du ticket. |

**Aucune autre divergence structurelle trouvée** entre ce que `CLAUDE.md` documente pour ces 8
lots et le code réellement présent sur le disque.

---

## 3. Dette / incohérence détectée à la lecture, absente de tout audit existant

### 3.1 `RequirePasswordChangeOnFirstLogin` posé mais jamais appliqué (fonctionnel, pas cosmétique)

Le ticket Lot 044 (`docs/tickets/tickets-tdd-lot-044-gestion-utilisateurs.md`, lignes 40-42) est
explicite : *"Le flag Identity `RequirePasswordChangeOnFirstLogin` ... est positionné à `true`
pour **forcer** l'utilisateur concerné à changer ce mot de passe temporaire à sa prochaine
connexion."* Vérification du code :

- `UserManagementService.CreateUserAsync`/`ResetPasswordAsync` positionnent bien le flag à `true`
  (`src/ExcelETL.Infrastructure/Identity/UserManagementService.cs:23,46`).
- **Aucun point du flux de connexion ne lit ce flag.** `Login.razor` (`OnValidSubmitAsync`)
  appelle `SignInManager.PasswordSignInAsync(...)` puis redirige directement vers `ReturnUrl`
  (`Login.razor:71-76`) sans aucune vérification de `ApplicationUser.RequirePasswordChangeOnFirstLogin`,
  aucune page de changement de mot de passe forcé, aucun filtre/middleware de redirection.
  `Profile.razor` permet bien de changer son mot de passe, mais rien n'y contraint l'utilisateur.
- **Conséquence concrète** : un compte créé par un admin conserve indéfiniment le mot de passe
  temporaire tant que l'utilisateur ne va pas spontanément sur `/profile`. C'est exactement le
  scénario que la demande client du 27/07 visait à éviter ("client va vouloir tester
  l'application et potentiellement donner des accès à d'autres utilisateurs") — le mécanisme de
  protection promis par le ticket lui-même n'est pas en place.
- Aucun test (`UserManagementServiceTests`, `UsersTests`, `LoginTests`) ne couvre cette
  application du flag — cohérent avec son absence de code, mais confirme qu'il ne s'agit pas d'un
  oubli de test isolé : la fonctionnalité d'application n'a simplement pas été écrite.

### 3.2 `CS8604` nullable sur `_profileNamePendingDeletion` (mineur, faux positif fonctionnel)

Voir §1 — 4 avertissements de compilation apparus depuis le dernier rapport du 25/07, dans
`ImportProfiles.razor`/`ExportProfiles.razor`. Sans impact d'exécution vérifié (le champ est
toujours affecté avant le rendu concerné), mais un avertissement `CS8604` qui ne se serait pas
introduit tout seul mérite d'être noté — signal de dette de compilation à surveiller, pas un bug.

---

## 4. Backlog résiduel — constats non ticketés de `audit-design-blazoradmin-2026-07-27.md`

L'audit design du 27/07 a été produit au commit `9b2587b` (avant les Lots 039-042). Vérification
point par point de ce qui reste réellement ouvert aujourd'hui, après ces 4 lots :

### 4.1 Fermés par les Lots 039-042 (confirmés par lecture directe du code, pas seulement CLAUDE.md)

- `aria-expanded`/`aria-controls` du toggler mobile — **fermé** (Lot 039).
- `role="alert"`/`aria-live` sur les messages d'erreur des éditeurs — **fermé** (Lot 040, y
  compris le résumé de lot `#batch-summary` et les bandeaux de confirmation de suppression).
- Sauts de niveaux de titres (`NotFound.razor`, éditeurs, cartes mobile) — **fermé** (Lot 042).
- `aria-label` du bouton copie (`log-copy-btn`) — **fermé** (Lot 041.4).
- Icônes manquantes sur les CTA principaux (`Créer`, `Enregistrer`, `process-button`,
  `generate-workbook-button`) — **fermé** (Lot 041.2).
- Parité structurelle `container-fluid` Import/Export — **fermé** (Lot 042.3).
- `aria-describedby` champ/message de validation (Login/Register/Profile) — **fermé** (Lot 042.1).

### 4.2 Toujours ouverts — non couverts par un lot livré

- **Gestion clavier du `<div onclick>` de `NavMenu.razor`** (`.nav-scrollable`, ligne 20) —
  Lot 039.0 a investigué ce point et conclu, par argumentation documentée (comportement standard
  WHATWG : Entrée sur un `<a>` focusé déclenche un `click` synthétique qui remonte par bubbling
  exactement comme un clic souris), qu'**aucune correction n'était nécessaire** — décision déjà
  actée, non rouverte ici.
- **Labels des filtres de `Logs.razor`** (`log-level-filter`, `log-time-filter`, `log-search`) —
  confirmé toujours absents (aucun `<label for=...>` ni `aria-label`, aucune clé resx
  `Logs_LevelFilterLabel`/`Logs_TimeFilterLabel`). **Non traité par aucun lot 039-044.**
- **Couleurs codées en dur de `ReconnectModal.razor.css`** (`#6b9ed2`, `#3b6ea2`, `#0087ff`,
  `white`) — confirmé toujours présentes, aucune variable `--m3-*`/`--bs-*`. Le bouton "Réessayer"
  reste bleu quel que soit le thème M3 actif. **Non traité par aucun lot 039-044.** `<dialog>`
  reste sans `role="dialog"`/`aria-modal`/`aria-labelledby` explicite (sémantique native
  `<dialog>` seule).
- **Divergence de convention icônes/boutons** (§2.2/§2.4 de l'audit) :
  - `SheetRuleForm.razor` (champs de bloc, colonnes inconditionnelles, règles de point) et
    `SheetGenerationRuleForm.razor` (colonnes, colonnes Point, colonnes Application) contiennent
    toujours des `<svg>` Pencil/Trash **dupliqués inline**, non centralisés via `AdminIconMarkup`
    — confirmé par lecture : ces deux fichiers `@using ExcelETL.BlazorAdmin.Shared` mais
    n'utilisent `AdminIconMarkup` que pour l'icône `Check` du bouton de soumission de bas de
    formulaire (Lot 041), jamais pour les boutons Modifier/Supprimer imbriqués.
  - Icônes "Enregistrer"/"Annuler" (coche/croix) des listes éditables en ligne (Tableaux/
    Applications par défaut dans `ImportProfileEditor.razor`, colonnes inconditionnelles/règles de
    point dans `SheetRuleForm.razor`) restent dupliquées en `<svg>` inline à 6 emplacements
    distincts, sans constante centralisée.
  - Le bouton principal (`Ajouter`/`Enregistrer les modifications`) et le bouton `Annuler` des 6
    sous-formulaires partagent toujours la même classe `btn-outline-secondary` — aucune
    distinction visuelle primaire/secondaire, contrairement au couple `btn-danger`/`btn-secondary`
    des confirmations de suppression.
  - `process-button` (`ApiTest.razor`) n'a pas `w-100 w-md-auto` contrairement à
    `save-profile-button`/`generate-workbook-button` — largeur non responsive (divergence
    mineure).
  - `.test-table-details`/`<summary>` (accordéons) : 17 lignes de CSS dupliquées à l'identique
    entre `ImportProfileTest.razor.css` et `ExportProfileTest.razor.css`, non factorisées dans
    `app.css` contrairement à `.sheet-rule-card`/`.block-field-*`.
  - `FileUploadIconMarkup` dupliqué verbatim à 3 emplacements (`ImportProfileTest.razor`,
    `ExportProfileTest.razor`, `ApiTest.razor`).
  - `BuildBatchSummaryText`/`GetStatusBadgeClass`/`GetStatusLabel`/`ToggleFileSection`/
    `ToggleSection` dupliqués entre `ImportProfileTest.razor`/`ExportProfileTest.razor` — **choix
    délibéré documenté** (Lot 033.4), pas une omission, à ne pas rouvrir sans raison nouvelle.

### 4.3 Non couvert par l'audit lui-même (rappel, hors périmètre statique)

Contraste de couleur réel, comportement effectif des lecteurs d'écran, ordre de focus réel, taille
de cible tactile réellement rendue, rendu du thème sombre — l'audit du 27/07 le documente déjà
explicitement comme non vérifiable sans navigateur réel. Non réexaminé ici.

---

## 5. Backlog résiduel — rappel des items déjà signalés par Lot 035 comme nécessitant arbitrage

Confirmés toujours ouverts par relecture directe du code aujourd'hui (aucun lot depuis le 035 ne
les a traités, cohérent avec leur statut "hors périmètre, nécessite arbitrage de Simon") :

- **`ProcessOxoFileService` archive toujours en double** — le mécanisme `IFileStorageService`
  (Lot K, cible uniquement, à plat, sans métadonnées) et le mécanisme `IGeneratedFileWriter`/
  `IGeneratedFileArchiveStore` (Lot 034, source+cible, horodaté, best-effort) coexistent, tous deux
  actifs à chaque appel de `POST /api/oxo/process` — confirmé par lecture du fichier, commentaire
  de code explicite documentant la coexistence comme délibérée mais non arbitrée définitivement.
- **`DirectCell`** (`src/ExcelETL.Domain/Extraction/Primitives/DirectCell.cs`) — confirmé toujours
  jamais construit nulle part dans `src/` (seule occurrence hors sa propre définition : l'énumération
  `DomainErrorCode`). Candidat mort ou primitive préparée pour un besoin futur non encore arrivé —
  nécessite un arbitrage explicite avant suppression ou avant de servir de base à une évolution.
- **`IWorkbookReader.SheetExists`** — confirmé toujours jamais appelé par aucun des 5 services
  d'extraction ni par aucun contrôleur/page. Implémenté (`ClosedXmlWorkbookReader.SheetExists`)
  mais mort en pratique.

---

## 6. Priorisation recommandée pour la suite, en vue du déploiement

*Recommandation d'enchaînement uniquement — aucun ticket rédigé ici, conformément à la consigne.*

### Bloquant avant déploiement (ou au tout début du go-live)

1. **Appliquer réellement `RequirePasswordChangeOnFirstLogin` au flux de connexion** (§3.1). C'est
   le seul écart de ce rapport qui touche directement la demande client à l'origine du Lot 044
   ("éviter que le client partage ses propres identifiants") — sans ce verrou, un compte créé avec
   un mot de passe temporaire reste valable indéfiniment avec ce mot de passe. À traiter avant que
   le client crée de vrais comptes pour d'autres utilisateurs en production.
2. **Pousser les 7 commits en attente** (§1) et **vérifier la migration Identity
   `AddRequirePasswordChangeOnFirstLoginToApplicationUser`** sur le serveur cible au moment du
   déploiement — `audit-verification-base-de-donnees-2026-07-27.md` (10/10 migrations) a été
   produit **avant** cette migration ; elle n'a donc jamais été vérifiée contre une base réelle.
   Cohérent avec le design d'auto-migration (Lot G4) et déjà noté comme étape du
   `guide-deploiement-am-oxo-etl-windows-server.md` ("essai à blanc sur le SQL Server cible") —
   confirmer explicitement que cette 9ᵉ migration Identity est incluse dans cet essai.
3. **Labels des filtres `Logs.razor`** (§4.2) — rapide, accessibilité de base (WCAG 3.3.2), aucune
   dépendance, bon candidat à traiter avec le point suivant avant le go-live plutôt qu'après.

### Polish / dette technique, sans urgence de déploiement

4. Couleurs codées en dur de `ReconnectModal.razor.css` — cosmétique (bouton "Réessayer" hors
   thème M3), aucun impact fonctionnel ou d'accessibilité bloquant.
5. Centralisation des icônes Pencil/Trash/Check/Cross encore dupliquées dans `SheetRuleForm.razor`/
   `SheetGenerationRuleForm.razor`, et factorisation du CSS `.test-table-details` dupliqué — pure
   réduction de dette, aucun comportement observable ne change.
6. Distinction visuelle primaire/secondaire des boutons Submit/Cancel des 6 sous-formulaires
   imbriqués, largeur responsive de `process-button` — polish visuel mineur.
7. Arbitrage `DirectCell`/`IWorkbookReader.SheetExists` (suppression ou justification d'un usage
   futur) et arbitrage du double mécanisme d'archivage `ProcessOxoFileService` — dette technique
   documentée, sans échéance de déploiement associée à ce jour ; à programmer quand Simon aura
   tranché plutôt qu'à l'improviste.
8. `CS8604` sur `_profileNamePendingDeletion` (§3.2) — cosmétique, à corriger en passant lors d'un
   prochain lot touchant ces deux fichiers plutôt qu'en isolation.

### Explicitement non retenu ici (déjà tranché, à ne pas rouvrir)

`<div onclick>` de `NavMenu.razor` (Lot 039.0, décision actée : pas de correction nécessaire) ;
duplication `BuildBatchSummaryText`/`GetStatusBadgeClass`/etc. entre `ImportProfileTest.razor`/
`ExportProfileTest.razor` (Lot 033.4, choix délibéré documenté) ; tout point listé "hors périmètre
explicite" dans un ticket déjà livré (Lots 037-044).

---

## Synthèse

- **1203/1203 tests verts**, exactement conforme à la revendication de clôture du Lot 044 —
  aucune divergence de compte trouvée.
- **8 lots (037→044) vérifiés conformes** à ce que `CLAUDE.md` documente, à une exception près :
  le Lot 044 pose `RequirePasswordChangeOnFirstLogin` mais ne l'applique jamais au flux de
  connexion — écart concret entre le texte du ticket et le code livré (§3.1), le seul de ce
  rapport à recommander un traitement avant déploiement pour des raisons fonctionnelles (pas
  seulement esthétiques).
- **Backlog design/accessibilité du 27/07** : 7 des 9 constats explicitement cités par la demande
  de ce rapport sont déjà fermés par les Lots 039-042 ; seuls les labels de filtres `Logs.razor`
  et les couleurs codées en dur de `ReconnectModal` restent réellement ouverts et non ticketés,
  aux côtés de plusieurs divergences de convention icônes/boutons (duplication SVG, distinction
  primaire/secondaire des sous-formulaires) déjà cataloguées par l'audit design mais non encore
  traitées.
- **7 commits locaux non poussés** (déplacements de documentation uniquement, aucun code) — à
  pousser avant la prochaine session, sans quoi le dépôt distant ne reflète pas la réorganisation
  documentaire la plus récente.
- **Rien de bloquant côté tests, build ou architecture** pour le déploiement lui-même — le seul
  point réellement bloquant identifié ici est fonctionnel/sécurité (§6, point 1), pas technique.
