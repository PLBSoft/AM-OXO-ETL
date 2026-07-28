# État des lieux design & accessibilité BlazorAdmin

**Date**: 2026-07-27
**Commit**: `9b2587b52e2f3ffef58903cb9fd728b63191944d`
**Branche**: `main`
**Portée**: constat factuel, lecture statique uniquement (markup Razor + CSS), sans exécution
navigateur ni analyse runtime. Couvre `src/ExcelETL.BlazorAdmin/Components/`,
`src/ExcelETL.BlazorAdmin/Shared/`, `src/ExcelETL.BlazorAdmin/wwwroot/app.css`,
`src/ExcelETL.BlazorAdmin/wwwroot/css/theme-m3.css`. Aucune modification de code n'a été
effectuée pour produire ce document.

---

## 1. Inventaire structurel

### 1.1 Fichiers `.razor`

| Fichier | Rôle |
| :--- | :--- |
| `Components/App.razor` | Shell HTML racine — charge Bootstrap, `theme-m3.css`, `app.css`, monte `<Routes>` en `InteractiveServer`, `<ReconnectModal>`. |
| `Components/Routes.razor` | `<Router>` + `<AuthorizeRouteView>`, redirige vers `RedirectToLogin` si non autorisé. |
| `Components/_Imports.razor` | Usings globaux Razor (pas de rendu). |
| `Components/Layout/MainLayout.razor` (+`.razor.css`) | Layout racine : `<div class="page">` avec `.sidebar` (NavMenu) + `<main><article>`. Bannière d'erreur Blazor globale. |
| `Components/Layout/NavMenu.razor` (+`.razor.css`) | Barre de navigation latérale/mobile : liens admin, logs, profil/déconnexion, login/register selon l'état d'auth. Toggler mobile en `<input type="checkbox">`. |
| `Components/Layout/ReconnectModal.razor` (+`.razor.css`, `.razor.js`) | Modale native `<dialog>` de reconnexion SignalR (composant framework Blazor standard). |
| `Components/Layout/PageBackNavLink.razor` | Bouton "retour" projeté dans le bandeau NavMenu via `SectionContent`/`SectionOutlet`. |
| `Components/Account/Pages/Login.razor` | `/Account/Login` — formulaire de connexion (`EditForm`+`InputText`). |
| `Components/Account/Pages/Register.razor` | `/Account/Register` — formulaire d'inscription. |
| `Components/Account/Shared/StatusMessage.razor` | Bandeau de message (succès/erreur) réutilisé par Login/Register/Profile. |
| `Components/Account/Shared/RedirectToLogin.razor` | Redirection pure (aucun rendu visible). |
| `Components/Pages/Error.razor` | `/Error` — page d'erreur générique ASP.NET. |
| `Components/Pages/NotFound.razor` | `/not-found` — page 404. |
| `Components/Pages/Admin/Users.razor` | `/users` — liste des utilisateurs (table desktop + cartes mobile), lecture seule. |
| `Components/Pages/Admin/Logs.razor` (+`.razor.css`) | `/logs` — journal système filtrable (niveau/période/texte), messages tronqués avec disclosure. |
| `Components/Pages/Admin/Profile.razor` | `/profile` — auto-édition profil (infos + changement de mot de passe). |
| `Components/Pages/Admin/ImportProfiles.razor` | `/import-profiles`, `/` — liste des profils d'import (CRUD list : modifier/dupliquer/supprimer). |
| `Components/Pages/Admin/ExportProfiles.razor` | `/export-profiles` — liste des profils d'export, symétrique à `ImportProfiles.razor`. |
| `Components/Pages/Admin/ImportProfileEditor.razor` | `/import-profiles/new`, `/import-profiles/{Id}/edit` — formulaire complet de construction/édition de profil d'import. |
| `Components/Pages/Admin/ExportProfileEditor.razor` | `/export-profiles/new`, `/export-profiles/{Id}/edit` — équivalent côté export. |
| `Components/Pages/Admin/SheetRuleForm.razor` | Composant imbriqué (pas de route) : sous-formulaire d'ajout/édition d'une `SheetExtractionRule`, utilisé par `ImportProfileEditor.razor`. |
| `Components/Pages/Admin/BlockFieldForm.razor` | Composant imbriqué : sous-formulaire d'un champ de bloc (plage Excel absolue), utilisé par `SheetRuleForm.razor`. |
| `Components/Pages/Admin/SheetGenerationRuleForm.razor` | Composant imbriqué : sous-formulaire `SheetGenerationRule`, utilisé par `ExportProfileEditor.razor`. |
| `Components/Pages/Admin/ColumnDefinitionForm.razor` | Composant imbriqué : sous-formulaire d'une colonne descriptive, utilisé par `SheetGenerationRuleForm.razor`. |
| `Components/Pages/Admin/PointColumnDefinitionForm.razor` | Composant imbriqué : sous-formulaire d'une colonne Point. |
| `Components/Pages/Admin/ApplicationColumnDefinitionForm.razor` | Composant imbriqué : sous-formulaire d'une colonne Application. |
| `Components/Pages/Admin/ImportProfileTest.razor` (+`.razor.css`) | `/import-profiles/test` — upload multi-fichiers + exécution en process du pipeline d'import, résultats en accordéons. |
| `Components/Pages/Admin/ExportProfileTest.razor` (+`.razor.css`) | `/export-profiles/test` — même principe + génération de workbook cible téléchargeable. |
| `Components/Pages/Admin/GeneratedFiles.razor` | `/generated-files` — consultation de l'archive des fichiers source/cible générés via l'API. |
| `Components/Pages/Admin/ApiTest.razor` | `/api-test` — appel HTTP réel vers `POST /api/oxo/process` (seule page BlazorAdmin qui parle HTTP au WebAPI). |

### 1.2 Fichiers CSS

- **`wwwroot/app.css`** — feuille globale partagée, contient :
  - Overrides de scaffolding Identity par défaut (`.valid.modified`, `.invalid`, `.validation-message`, `.blazor-error-boundary`, `.darker-border-checkbox`).
  - Layout de liste de champs éditables : `.block-field-list`, `.block-field-item`, `.block-field-item-editing`, `.block-field-info`, `.block-field-name`, `.block-field-range`, `.block-field-grid` (densification en grille), `.block-field-actions`, `.block-field-icon-btn` (boutons carrés 34×34px).
  - Alignement des actions : `.right-aligned-actions` (`display:flex; justify-content:flex-end`) — implémentation de `convention-ui-blazor-alignement-boutons.md`.
  - Cartes de règle de feuille : `.sheet-rule-list`, `.sheet-rule-grid` (grille responsive `minmax(480px,1fr)`, collapse à 1 colonne sous 767.98px), `.sheet-rule-card`, `.sheet-rule-editing-item`, `.sheet-rule-card-header`, `.sheet-rule-card-title`, `.sheet-rule-card-meta`.
  - Disclosure de sous-listes : `.sheet-rule-sublist-details` (styles de `<summary>`, masquage du marqueur natif WebKit).
  - Utilitaire responsive custom : `.w-md-auto` (Bootstrap n'a pas d'équivalent `w-100`/`w-md-auto`).
- **`wwwroot/css/theme-m3.css`** — palette Material Design 3 mappée sur les variables custom Bootstrap 5.3 (`--bs-primary`, `--bs-danger`, etc. redirigées vers `--m3-*`), définie pour `:root`/`[data-bs-theme="light"]` et `[data-bs-theme="dark"]`, plus des overrides de composants (`.btn-primary`, `.btn-outline-*`, `.text-bg-*`, focus rings). **Est bien référencée** : `Components/App.razor` ligne 10 charge `<link rel="stylesheet" href="@Assets["css/theme-m3.css"]" />`, avant `app.css` (donc `app.css` peut surcharger si besoin — il ne le fait pas).
- `Components/Layout/MainLayout.razor.css` — layout `.page`/`.sidebar`/`.top-row`, breakpoint responsive à 641px, `main { min-width: 0; }` (fix documenté dans `convention-ui-blazor-tableaux-generes-lisibilite.md`).
- `Components/Layout/NavMenu.razor.css` — toggler mobile, icônes SVG inline en `background-image` (data URI), `.nav-item`, `.top-row-back-link`.
- `Components/Layout/ReconnectModal.razor.css` — styles de la modale de reconnexion SignalR (couleurs et animations propres, non alignées sur le thème M3, voir §3).
- `Components/Pages/Admin/Logs.razor.css` — lignes de tableau colorées par niveau (`.log-row-error`, `.log-row-warning`, `.log-row-info`), bouton copie.
- `Components/Pages/Admin/ImportProfileTest.razor.css` — accordéons `.test-table-details`, conteneur de défilement `.test-table-scroll`, en-têtes de tableau collants.
- `Components/Pages/Admin/ExportProfileTest.razor.css` — même patron + `.generated-sheet-table`/`.generated-sheet-scroll` avec les règles anti-débordement de `convention-ui-blazor-tableaux-generes-lisibilite.md` (`white-space:normal`, `max-width:320px`, `overflow-wrap:break-word`, `vertical-align:top`).

### 1.3 Jetons de design / variables CSS custom trouvés

Définis dans `wwwroot/css/theme-m3.css` (`:root`/`[data-bs-theme="light"]` et `[data-bs-theme="dark"]`) :

```css
--m3-primary: #D81F11;   --m3-on-primary: #FFFFFF;
--m3-secondary: #775652; --m3-on-secondary: #FFFFFF;
--m3-success: #2E7D32;   --m3-danger: #BA1A1A;   --m3-warning: #ff7518;   --m3-info: #008c94;
--m3-background: #FFFBFF; --m3-on-background: #201A19; --m3-surface: #F5DDDA; --m3-border: #857371;
```

(valeurs `dark` distinctes, non reproduites ici par souci de concision). Ces `--m3-*` sont ensuite
réassignées aux variables standard Bootstrap (`--bs-primary`, `--bs-body-bg`, `--bs-card-bg`,
`--bs-dropdown-bg`, `--bs-link-color`, etc.), si bien que la majorité des composants Bootstrap
consomment le thème sans code additionnel. Aucune variable `--m3-*`/`--bs-*` custom n'est
redéfinie ailleurs dans `app.css` ou dans un `.razor.css` — la seule exception constatée est
`ReconnectModal.razor.css`, qui utilise des couleurs hexadécimales littérales indépendantes du
thème (voir §2 et §3).

---

## 2. Cohérence — écarts par rapport aux conventions existantes

### 2.1 Boutons / alignement (`convention-ui-blazor-alignement-boutons.md`)

| Composant | Motif observé | Conforme à `.right-aligned-actions` ? | Remarque |
| :--- | :--- | :--- | :--- |
| `ImportProfiles.razor` / `ExportProfiles.razor` — boutons d'en-tête (Tester/Créer) | `<div class="right-aligned-actions d-flex gap-2 mb-3">` | Oui | Identique caractère pour caractère entre les deux pages. |
| `ImportProfiles.razor` / `ExportProfiles.razor` — ligne d'actions de suppression en attente de confirmation | `<div class="right-aligned-actions">` | Oui | |
| `ImportProfileEditor.razor` — `save-profile-button` | `<div class="right-aligned-actions"><button class="btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4">` | Oui | |
| `ExportProfileEditor.razor` — `save-export-profile-button` | Identique | Oui | Classes du bouton strictement identiques à l'import. |
| `ImportProfileEditor.razor` / `ExportProfileEditor.razor` — actions Modifier/Supprimer de carte de règle de feuille | `<div class="right-aligned-actions">` | Oui | |
| `SheetRuleForm.razor` / `SheetGenerationRuleForm.razor` / `BlockFieldForm.razor` / `ColumnDefinitionForm.razor` / `PointColumnDefinitionForm.razor` / `ApplicationColumnDefinitionForm.razor` — bouton Submit/Cancel de bas de formulaire | `<div class="right-aligned-actions"><button class="btn btn-outline-secondary w-100 mt-3">` | Oui pour l'alignement | Voir §2.2 : le bouton principal (`Ajouter`/`Enregistrer`) et le bouton `Annuler` partagent la **même** classe `btn-outline-secondary`, sans distinction visuelle primaire/secondaire malgré l'alignement correct. |
| `ImportProfileEditor.razor` — ajout Tableaux par défaut / Applications par défaut | `<div class="right-aligned-actions"><button class="btn btn-outline-secondary w-100 mt-3">` | Oui | |
| `Logs.razor` — bouton Rafraîchir / Charger plus | `<button class="btn btn-outline-secondary">` en flux normal (pas de wrapper `.right-aligned-actions`), mais déjà positionné via `d-flex justify-content-between` pour le premier, en flux normal pour le second | Rafraîchir : oui via wrapper `justify-content-between` (pas la classe elle-même mais un motif équivalent) ; Charger plus (ligne 125) : **non**, aucun wrapper d'alignement, bouton en flux gauche naturel | `Logs.razor` n'utilise jamais `.right-aligned-actions`. |
| `GeneratedFiles.razor` — Rechercher/Effacer | `<div class="input-group">` (boutons accolés au champ, pas une action de bas de section) | N/A | Motif "bouton intégré à une ligne de saisie", cas non couvert explicitement par la convention. |
| `ApiTest.razor` — `process-button` | `<div class="right-aligned-actions"><button class="btn btn-primary btn-lg">` | Oui | Pas de `w-100`/`w-md-auto` contrairement à `save-profile-button`/`generate-workbook-button` — largeur non responsive. |
| `ExportProfileTest.razor` — `generate-workbook-button` | `<button class="btn btn-primary btn-lg w-100 w-md-auto mb-3">`, **hors** de tout wrapper `.right-aligned-actions` | Non emballé dans la classe convention, mais en flux normal (pleine largeur ou largeur naturelle selon le viewport) | Décrit comme "bouton d'action principale" par le commentaire Lot V12 du fichier — pas explicitement couvert par la convention (qui parle d'actions de bas de carte/section), mais illustre une divergence d'implémentation par rapport à `save-profile-button`. |
| `ImportProfileTest.razor` — aucun bouton d'action principale équivalent (traitement auto au changement de fichier) | N/A | N/A | |
| `Profile.razor` — boutons de soumission (`btn-primary w-100 btn-lg`) | Pas de wrapper `.right-aligned-actions`, flux normal | Non | Page non couverte par la convention (portée = "pages d'administration" au sens large ; `Profile.razor` est bien sous `/profile` avec `[Authorize]` mais n'est pas listée explicitement dans les lots UI). |
| `Login.razor` / `Register.razor` | Boutons `w-100 btn btn-lg btn-primary` en flux normal | Non | Hors périmètre déclaré de la convention (pages non-admin). |

### 2.2 Boutons / icônes (`convention-ui-blazor-icones-boutons.md`)

Note préalable : la convention documente l'usage de **Bootstrap Icons (`bi bi-*`)**, mais son
propre texte introductif cite déjà `NavMenu.razor`/`NavMenu.razor.css` comme référence — or ces
fichiers n'utilisent jamais la police Bootstrap Icons : toutes les icônes du projet (NavMenu
compris) sont des **SVG inline** en `background-image` (NavMenu) ou en balise `<svg>` directe
(pages Admin), commentaires du code le confirmant explicitement (ex. `AdminIconMarkup.cs` ligne
6-7 : *"No bootstrap-icons font/CSS is loaded anywhere in this project"*). La convention décrit
donc un mécanisme (classes `bi bi-*`) différent de celui réellement implémenté (SVG inline) —
écart entre le texte de la convention et le code, constaté factuellement, sans jugement sur son
importance.

| Composant | Bouton | Icône seule / texte / icône+texte | Respecte la matrice de décision ? | Remarque |
| :--- | :--- | :--- | :--- | :--- |
| `ImportProfiles.razor`/`ExportProfiles.razor` | Modifier/Dupliquer/Supprimer (ligne de tableau + carte) | Icône seule, `aria-label`+`title` présents | Oui (action de ligne de tableau) | Utilise `AdminIconMarkup.Pencil/Copy/Trash`. |
| `ImportProfiles.razor`/`ExportProfiles.razor` | Créer / Tester un profil | Texte seul, aucune icône | Non conforme à la matrice ("Action principale (CTA) → Oui") | `create-profile-button`/`create-export-profile-button` sont des CTA principaux sans icône. |
| `ImportProfileEditor.razor`/`ExportProfileEditor.razor` | Modifier/Supprimer de carte de règle de feuille | Icône seule, `aria-label`+`title` présents | Oui | Confirme la parité Lot 037 revendiquée dans CLAUDE.md (voir §5.3). |
| `ImportProfileEditor.razor` | Modifier/Supprimer/Enregistrer/Annuler des listes Tableaux par défaut / Applications par défaut | Icône seule (SVG check/croix inline, pas dans `AdminIconMarkup`), `aria-label`+`title` présents | Oui pour la présence d'icône | Icônes dupliquées inline (voir §2.4) plutôt que centralisées. |
| `SheetRuleForm.razor` | Modifier/Supprimer de champ de bloc / colonne inconditionnelle / règle de point | Icône seule | Oui pour la présence | `aria-label` présent, **`title` absent** sur les boutons Modifier/Supprimer de champ de bloc (lignes 76-93), alors que les boutons Enregistrer/Annuler de la même liste et les boutons équivalents de `ImportProfileEditor.razor` (Tableaux/Applications) portent systématiquement `title`. Incohérence de couverture `title` au sein du même patron. |
| `SheetRuleForm.razor` / `BlockFieldForm.razor` / `SheetGenerationRuleForm.razor` / `ColumnDefinitionForm.razor` / `PointColumnDefinitionForm.razor` / `ApplicationColumnDefinitionForm.razor` | Bouton principal de soumission (`Ajouter le champ`, `Ajouter la colonne`, `Enregistrer les modifications`, etc.) | **Texte seul**, aucune icône | Non conforme à la matrice ("Action CRUD standard → Oui" pour "Enregistrer") | Concerne aussi bien le mode Ajout que le mode Modification (`SubmitLabel` = libellé "Enregistrer les modifications" dans certains contextes). |
| `ImportProfileEditor.razor`/`ExportProfileEditor.razor` | `save-profile-button` / `save-export-profile-button` | Texte seul (`btn-primary`), aucune icône | Non conforme à la matrice pour un CTA principal | |
| `ApiTest.razor` | `process-button` | Texte seul (`btn-primary`) | Non conforme | |
| `ExportProfileTest.razor` | `generate-workbook-button` | Texte seul (`btn-primary`) | Non conforme | |
| `GeneratedFiles.razor` | Rechercher / Effacer | Texte seul | Non couvert explicitement par la matrice | |
| `Logs.razor` | Rafraîchir / Charger plus | Texte seul | Non couvert explicitement (pas un CRUD classique) | |
| `Logs.razor` | Copier le message (`log-copy-btn`) | Icône seule (SVG presse-papiers), pas d'`aria-label`, `title` seul présent | **Non conforme** — la convention exige `aria-label` obligatoire pour tout bouton icône seule ; ici seul `title` est présent (ligne 101-103 : `title="@Loc[...]"`, pas de `aria-label=`). Le texte de remplacement `"Copié"` qui s'affiche après clic n'a pas non plus d'attribut d'accessibilité dédié. | |
| `ImportProfiles.razor`/`ExportProfiles.razor` | Confirmer/Annuler la suppression | Texte seul (`btn-sm btn-danger`/`btn-sm btn-secondary`) | Cohérent avec "Action secondaire → Non" pour Annuler ; Confirmer (CRUD standard) n'a pas d'icône | Non conforme partiellement. |
| `PageBackNavLink.razor` | Bouton retour | Icône + texte (masqué `d-none d-md-inline` sous 768px, donc icône seule sur mobile) | Cas explicitement documenté comme exception par CLAUDE.md ("Action secondaire → Non" mais bouton conserve une icône flèche) | Non couvert littéralement par la matrice (ni CTA ni CRUD ni ligne de grille), mais cohérent avec la pratique de navigation de la plupart des SPA ; la convention ne prévoit pas explicitement ce cas. |

### 2.3 Divergences visuelles/structurelles entre composants similaires

- **Conteneur de page** : `ExportProfileEditor.razor` enveloppe tout son contenu dans
  `<div class="container-fluid px-3">...</div>` (lignes 20 et 165), alors que
  `ImportProfileEditor.razor` n'a **aucun** conteneur équivalent — le contenu est rendu directement
  dans `<article class="content px-4">` du layout. Ceci est une divergence structurelle réelle entre
  les deux pages malgré les lots de parité (Q, 030) revendiqués dans CLAUDE.md, qui portent sur les
  boutons/cartes/labels mais pas sur le conteneur racine de la page.
- **`.sheet-rule-card` / `.block-field-*`** : le CSS (`app.css`) est partagé à 100 % entre les deux
  éditeurs (aucune règle scoped dans un `.razor.css` propre à l'un ou l'autre), donc le rendu visuel
  des cartes est structurellement identique. Vérification littérale du markup confirme que les deux
  fichiers produisent la même arborescence de classes pour les boutons Modifier/Supprimer de carte
  (`btn btn-sm btn-outline-secondary block-field-icon-btn` / `btn btn-sm btn-outline-danger
  block-field-icon-btn`), confirmant la revendication de parité du Lot 037.
- **Bouton Submit vs Cancel dans les sous-formulaires** : dans `SheetRuleForm.razor`,
  `BlockFieldForm.razor`, `SheetGenerationRuleForm.razor`, `ColumnDefinitionForm.razor`,
  `PointColumnDefinitionForm.razor`, `ApplicationColumnDefinitionForm.razor`, le bouton principal
  (Ajouter/Enregistrer) et le bouton Annuler utilisent tous deux `class="btn btn-outline-secondary
  w-100 mt-3"` — aucune distinction de couleur/poids visuel entre une action de sauvegarde et une
  action d'annulation, ce qui diverge du couple `btn-danger`/`btn-secondary` utilisé pour les
  confirmations de suppression (`ImportProfiles.razor`, `ImportProfileEditor.razor`).
- **`ReconnectModal.razor.css`** utilise des couleurs hexadécimales fixes
  (`background-color: #6b9ed2`, `#3b6ea2`, `border: 3px solid #0087ff`,
  `background-color: white`) sans passer par aucune variable `--m3-*`/`--bs-*` — ce composant ne
  suit pas le thème M3 appliqué partout ailleurs (bouton "Réessayer" restera bleu même si
  `--m3-primary` est rouge).

### 2.4 Duplication de patrons au lieu de réutilisation

- **`AdminIconMarkup`** (Pencil/Copy/Trash) est bien réutilisé dans `ImportProfiles.razor`,
  `ExportProfiles.razor`, `ImportProfileEditor.razor` et `ExportProfileEditor.razor` (boutons de
  carte de règle de feuille). En revanche, les **mêmes icônes Pencil/Trash sont redéclarées
  intégralement en `<svg>` inline** (chaînes de `path` identiques caractère pour caractère) dans :
  `SheetRuleForm.razor` (boutons Modifier/Supprimer de champ de bloc, de colonne inconditionnelle,
  de règle de point), `SheetGenerationRuleForm.razor` (boutons Modifier/Supprimer de colonne, de
  colonne Point, de colonne Application). Ces fichiers n'importent pas `AdminIconMarkup` et ne le
  référencent jamais — la centralisation Lot 035.5 documentée dans CLAUDE.md n'a donc pas été
  étendue à ces composants imbriqués.
- **Icônes "Enregistrer" (coche) et "Annuler" (croix)** utilisées dans les listes éditables en
  ligne (`ImportProfileEditor.razor` — Tableaux/Applications par défaut ; `SheetRuleForm.razor` —
  colonnes inconditionnelles, règles de point) sont des `<svg>` inline dupliqués verbatim à
  **6 emplacements distincts** (2 dans `ImportProfileEditor.razor`, 2 dans `SheetRuleForm.razor`,
  répétés une fois par liste), sans constante centralisée équivalente à `AdminIconMarkup`.
- **`.test-table-details` / `<summary>` styles** : les règles CSS de l'accordéon (`cursor:pointer`,
  `::before` triangle, `[open]` rotation) sont dupliquées à l'identique entre
  `ImportProfileTest.razor.css` et `ExportProfileTest.razor.css` (17 lignes identiques), au lieu
  d'être factorisées dans `app.css` comme le sont `.sheet-rule-card`/`.block-field-*`.
- **`FileUploadIconMarkup`** (icône SVG d'upload) est redéclarée verbatim en constante privée dans
  `ImportProfileTest.razor`, `ExportProfileTest.razor` et `ApiTest.razor` (3 occurrences
  identiques), sans équivalent centralisé.
- **`BuildBatchSummaryText`, `GetStatusBadgeClass`, `GetStatusLabel`, `ToggleFileSection`,
  `ToggleSection`** sont des méthodes quasi identiques dupliquées entre `ImportProfileTest.razor`
  et `ExportProfileTest.razor` (le code source documente lui-même ce choix comme délibéré — Lot
  033.4 — au nom d'un couplage jugé trop fort pour une factorisation complète ; seul le traitement
  d'import lui-même a été extrait dans `BatchImportProcessing`).

---

## 3. Accessibilité structurelle (WCAG 2.1 AA, markup/CSS statique uniquement)

### 3.1 Perceivable

| Composant / fichier | Ligne / extrait | Constat | Critère WCAG concerné |
| :--- | :--- | :--- | :--- |
| Toutes les icônes SVG décoratives (`AdminIconMarkup`, icônes de `NavMenu.razor.css`, `PageBackNavLink.razor`, icônes upload) | ex. `AdminIconMarkup.cs:11` | `aria-hidden="true"` systématiquement présent sur les `<svg>` décoratifs accompagnant un texte ou un `aria-label` porté par le bouton parent. | 1.1.1 (Non-text Content) — conforme sur ce point précis |
| `Logs.razor` | ligne 110-113 | Icône SVG de copie **sans** `aria-hidden="true"` explicite sur la balise `<svg>` elle-même (contrairement au patron `AdminIconMarkup`) ; le bouton parent porte `title` mais pas `aria-label`. | 1.1.1 / 4.1.2 |
| `ReconnectModal.razor` | lignes 7-10 | `<div class="components-rejoining-animation" aria-hidden="true">` — animation décorative correctement masquée. | 1.1.1 — conforme |
| Aucune balise `<img>` avec `alt` manquant trouvée | — | L'application n'utilise aucune balise `<img>` dans les fichiers audités (uniquement SVG inline et un favicon référencé dans `App.razor`, hors périmètre du contenu de page). | 1.1.1 |
| Landmarks sémantiques | `MainLayout.razor:10` | `<main>` unique englobant `<article class="content px-4">@Body</article>` — présent sur toutes les pages via le layout. | 1.3.1 / repères de région |
| Landmarks sémantiques | `NavMenu.razor:18` | `<nav class="nav flex-column">` présent, mais **aucun** attribut `aria-label` distinctif (il n'y a qu'une seule `<nav>` dans l'app donc l'absence de label n'est pas ambiguë, mais aucun landmark `<header>` n'encadre le bandeau `.top-row` contenant la marque et le lien retour). | 1.3.1 |
| Hiérarchie de titres | Voir §1.1 pour l'inventaire — plusieurs pages sautent des niveaux : `NotFound.razor` (h3 sans h1) ; `ImportProfileEditor.razor`/`ExportProfileEditor.razor` (h1→h3, aucun h2) ; `ImportProfileTest.razor`/`ExportProfileTest.razor` (h1→h4 direct pour les titres de rejet/erreur technique) ; `Users.razor`/`ImportProfiles.razor`/`ExportProfiles.razor`/`GeneratedFiles.razor` (h1→h5 direct pour les titres de carte mobile). | Sauts de niveau confirmés par grep exhaustif sur `<h[1-6]` (voir §1.1). | 1.3.1 (Info and Relationships), bonne pratique de hiérarchie de titres |
| Couleurs codées en dur (indépendantes du thème) | `ReconnectModal.razor.css:24,93,100,104,115` | `background-color: #6b9ed2`/`#3b6ea2`/`white`, `border: 3px solid #0087ff` — valeurs hex littérales, non dérivées de `--m3-*`. | 1.4.1 / cohérence visuelle, pas un critère de contraste calculable statiquement (voir §6) |
| Utilitaires Bootstrap de couleur codant du sens | `ImportProfileTest.razor`/`ExportProfileTest.razor` — `bg-success`/`bg-warning text-dark`/`bg-danger`/`bg-dark` sur les badges de statut de fichier (`GetStatusBadgeClass`) ; `GeneratedFiles.razor` — même patron | Le statut est **également** porté par le texte du badge (`GetStatusLabel`), donc l'information n'est pas véhiculée par la couleur seule. | 1.4.1 — conforme sur ce point (texte + couleur) |
| `Logs.razor.css` | lignes 1-9 | `.log-row-error`/`.log-row-warning` utilisent une bordure gauche colorée + fond teinté (`rgba(220,53,69,0.08)`) en plus du texte de la colonne "Niveau" — information non portée uniquement par la couleur. | 1.4.1 — conforme |

### 3.2 Operable

| Composant / fichier | Ligne / extrait | Constat | Critère WCAG concerné |
| :--- | :--- | :--- | :--- |
| `NavMenu.razor` | ligne 17 : `<div class="nav-scrollable" onclick="document.querySelector('.navbar-toggler').click()">` | Attribut HTML natif `onclick` sur un `<div>`, sans `role="button"`, sans `tabindex`, sans gestionnaire clavier (`onkeydown`). Seul élément de tout le périmètre audité utilisant un `onclick` HTML natif (tous les autres gestionnaires sont des directives Blazor `@onclick` sur de vrais `<button>`). | 2.1.1 (Keyboard) |
| `NavMenu.razor` | ligne 15 : `<input type="checkbox" title="..." class="navbar-toggler ...">` | Le menu hamburger mobile est un `<input type="checkbox">` stylé (pas un `<button aria-expanded>`). Porte un `title` mais aucun `aria-label`, `aria-expanded`, ni `aria-controls` vers `.nav-scrollable`. Le mécanisme d'ouverture/fermeture repose entièrement sur le sélecteur CSS `.navbar-toggler:checked ~ .nav-scrollable` (`NavMenu.razor.css:164`). | 4.1.2 (Name, Role, Value) — un lecteur d'écran annoncera une case à cocher générique, pas un bouton de menu |
| `ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `Logs.razor`, `ImportProfileTest.razor`, `ExportProfileTest.razor` | motif répété : `<summary @onclick="..." @onclick:preventDefault="true">` | Le `<summary>` natif est utilisable au clavier et par la souris nativement ; ici `@onclick:preventDefault="true"` bloque le comportement natif d'ouverture/fermeture du `<details>` pour le remplacer par un état Blazor (`_expandedSheetRuleDetails`/`_collapsedSections`, etc.) — comme documenté explicitement par CLAUDE.md (note Lot R3). L'attribut `open` de l'élément `<details>` est correctement re-synchronisé (`open="@_expandedSheetRuleDetails.Contains(index)"` dans `ImportProfileEditor.razor:286`, et de même dans `Logs.razor:81`, `ImportProfileTest.razor` multiples occurrences), donc l'état visuel/sémantique reste cohérent malgré le contournement du comportement natif. Le clavier (Entrée/Espace sur `<summary>` focusé) déclenche toujours l'événement `click`, donc l'opérabilité clavier n'est **pas** rompue par ce motif — seul le double-déclenchement natif est empêché. | 4.1.2 — pas de rupture confirmée, mais mécanisme non standard à signaler |
| Boutons icône seule dans toutes les listes éditables (`block-field-icon-btn`) | `app.css:107-114` | `width:34px; height:34px; padding:0` — taille de cible mesurée en CSS. Aucune propriété `min-height`/`min-width` supplémentaire ailleurs qui l'agrandirait. | 2.5.5 (Target Size, Enhanced, AAA) / 2.5.8 (Target Size, Minimum, AA — seuil 24×24px) — **valeur mesurée seulement, voir note ci-dessous** |
| Boutons standard `.btn` (Bootstrap par défaut) | non surchargé par `app.css`/`theme-m3.css` pour le `padding`/`line-height` de base | Taille dépend du `padding`/`line-height` Bootstrap par défaut, non mesurable ici sans rendu (voir §6). | 2.5.8 |
| `Logs.razor` | `log-copy-btn` (`Logs.razor.css:15-19`) | `padding: 0.1rem 0.35rem; line-height: 1` — bouton plus petit que `.block-field-icon-btn`'s 34px déclaré, taille de cible réduite par rapport aux autres boutons icône du reste de l'application. | 2.5.8 |
| `tabindex` | recherche exhaustive sur tout `Components/` | **Aucune** occurrence de `tabindex` dans tout le périmètre audité. Aucun ordre de tabulation personnalisé n'est défini nulle part (positif ou négatif) — l'ordre de focus suit donc entièrement l'ordre du DOM. | 2.4.3 (Focus Order) — absence de `tabindex` négatif signifie qu'aucun élément interactif n'est explicitement retiré du flux de tabulation par ce mécanisme |
| `ImportProfiles.razor`/`ExportProfiles.razor` — boutons Modifier/Dupliquer/Supprimer d'une même ligne (table + carte mobile) | ids `edit-profile-button-{id}` / `edit-profile-button-card-{id}` | Deux jeux d'ids distincts (`-card-` suffixé) coexistent simultanément dans le DOM (l'un affiché en `d-none d-md-table`, l'autre en `d-md-none`) — pas de collision d'id constatée à la lecture du markup. | 4.1.1 (Parsing / unicité des id) — conforme |

### 3.3 Understandable

| Composant / fichier | Ligne / extrait | Constat | Critère WCAG concerné |
| :--- | :--- | :--- | :--- |
| `Login.razor`, `Register.razor`, `Profile.razor` | ex. `Login.razor:28-30` | Tous les champs `<InputText>` ont un `<label for="...">` correspondant à l'`id` du champ. `ValidationMessage For="..."` associé mais **sans** `aria-describedby` liant explicitement le champ à son message d'erreur — l'association reste uniquement visuelle/DOM-adjacente, pas programmatique. | 3.3.1 (Error Identification) / 1.3.1 |
| `Login.razor`/`Register.razor`/`Profile.razor` | `<ValidationSummary class="text-danger" role="alert" />` | `role="alert"` présent sur les récapitulatifs de validation Blazor natifs (`EditForm`/`DataAnnotationsValidator`). | 4.1.3 (Status Messages) — conforme pour ces pages |
| `ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `SheetRuleForm.razor`, `BlockFieldForm.razor`, `SheetGenerationRuleForm.razor`, `ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`, `ApplicationColumnDefinitionForm.razor` | motif répété : `<div class="alert alert-danger">@_errorMessage</div>` (25 occurrences recensées, aucune ne porte `role="alert"` ni `aria-live`) | Ces pages n'utilisent **pas** `EditForm`/`DataAnnotationsValidator`/`ValidationSummary` — la validation se fait via try/catch sur les exceptions du domaine (cf. CLAUDE.md), et le message d'erreur résultant est un `<div>` texte brut sans annonce programmatique. Un lecteur d'écran ne sera pas averti de l'apparition du message sauf s'il se trouve déjà dans le flux de focus. | 4.1.3 (Status Messages) |
| `ImportProfileTest.razor`/`ExportProfileTest.razor` | `<p id="batch-summary">@BuildBatchSummaryText()</p>` | Résumé de traitement de lot inséré dans le DOM après la fin du traitement asynchrone, sans `aria-live`/`role="status"`. Le spinner de traitement (`role="status"`, ligne 72) disparaît et est remplacé par ce résumé sans région annoncée. | 4.1.3 |
| `Logs.razor` | filtres `log-level-filter`/`log-time-filter`/`log-search` (lignes 32, 41, 49) | **Aucun** `<label>` associé à ces trois champs de filtre (ni `for=`, ni `aria-label`). Vérifié : aucune clé resx `Logs_LevelFilterLabel`/`Logs_TimeFilterLabel` n'existe dans `BlazorAdminMessages.resx`. Seul le champ recherche a un `placeholder`. | 3.3.2 (Labels or Instructions) / 1.3.1 |
| `Register.razor` | ligne 30 : `<label for="Input.Email">` | Contrairement à `Login.razor` (`<label ... class="form-label">`), ce `<label>` n'a **pas** la classe `form-label` — incohérence mineure de style entre les deux pages issues du même template Identity scaffoldé, sans impact sémantique. | — (cohérence, pas un critère WCAG direct) |
| `ImportProfileEditor.razor`/`ExportProfileEditor.razor`/sous-formulaires | tous les `<input>`/`<select>` en `form-floating` | Chaque champ vérifié possède un `<label for="...">` correspondant (confirmé par lecture exhaustive du markup pour les ~35 champs des formulaires imbriqués). CLAUDE.md revendique une couverture complète (Lots N/O/Q/Y) — confirmé par cette lecture. | 3.3.2 — conforme |
| `ApiTest.razor`, `ImportProfileTest.razor`, `ExportProfileTest.razor`, `GeneratedFiles.razor` | selects/inputs de sélection de profil/recherche | Tous portent un `<label class="form-label" for="...">` explicite. | 3.3.2 — conforme |

### 3.4 Robust

| Composant / fichier | Ligne / extrait | Constat | Critère WCAG concerné |
| :--- | :--- | :--- | :--- |
| Accordéons `<details>`/`<summary>` (toutes pages concernées) | ex. `ImportProfileEditor.razor:286-320`, `Logs.razor:81-90`, `ImportProfileTest.razor` (multiples) | Élément HTML natif porteur de sémantique intrinsèque (pas de `role` ARIA custom nécessaire). `open` correctement synchronisé avec l'état Blazor (voir §3.2). Aucun `aria-expanded` redondant ajouté (non nécessaire, `<details>` expose déjà cet état nativement aux technologies d'assistance modernes). | 4.1.2 — conforme structurellement |
| `<select>` de filtre/choix de profil (tous fichiers) | ex. `Logs.razor:32-38` | `<select>` HTML natif, aucun widget ARIA custom — sémantique native complète. | 4.1.2 — conforme |
| Confirmation de suppression inline (`ImportProfiles.razor`, `ExportProfiles.razor`, `ImportProfileEditor.razor`, `ExportProfileEditor.razor`) | ex. `ImportProfiles.razor:63-75` | Le remplacement des boutons Modifier/Dupliquer/Supprimer par un bandeau de confirmation (`<span class="text-danger me-auto">`+2 boutons) se fait par re-rendu conditionnel Blazor (`@if`), **sans** `aria-live` sur le conteneur englobant ni `role="alert"` sur le message de confirmation — un changement de contenu significatif non annoncé aux technologies d'assistance. | 4.1.3 (Status Messages) |
| `ReconnectModal.razor` | ligne 5 : `<dialog id="components-reconnect-modal" data-nosnippet>` | Élément `<dialog>` HTML natif (composant framework Blazor standard, non modifié par ce projet) — **aucun** `role="dialog"`/`aria-modal="true"` explicite ajouté, mais `<dialog>` porte cette sémantique nativement dans les navigateurs modernes. Pas de `aria-labelledby` pointant vers un titre. | 4.1.2 — sémantique native présente, labellisation absente |
| Badges de statut (`<span class="badge bg-...">`) | `ImportProfileTest.razor`, `ExportProfileTest.razor`, `GeneratedFiles.razor` | `<span>` simple, pas de `role="status"` — le texte du badge est cependant lu normalement comme contenu textuel statique (pas une mise à jour dynamique isolée puisqu'il apparaît dans un bloc déjà non annoncé, voir 3.3). | 4.1.2 |
| Boutons de type natif | Vérification : tout élément `@onclick`-porteur dans le périmètre audité est soit un `<button type="button">`/`<button type="submit">`, soit un `<a>` (liens de téléchargement), à l'exception du `<div onclick>` de `NavMenu.razor` (§3.2). | La quasi-totalité des interactions personnalisées repose sur de vrais éléments interactifs natifs (rôle/nom/valeur corrects par construction). | 4.1.2 — globalement conforme, une exception notée |

---

## 4. IDs HTML stables

Vérification par lecture exhaustive de tous les fichiers listés : la quasi-totalité des éléments
interactifs (boutons, liens, inputs, selects) porte un `id` stable, conformément à la convention
de tests bUnit documentée dans CLAUDE.md. Constats :

- **Conformité générale confirmée** : tous les boutons/inputs des pages `ImportProfiles.razor`,
  `ExportProfiles.razor`, `ImportProfileEditor.razor`, `ExportProfileEditor.razor`,
  `SheetRuleForm.razor`, `BlockFieldForm.razor`, `SheetGenerationRuleForm.razor`,
  `ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`,
  `ApplicationColumnDefinitionForm.razor`, `ImportProfileTest.razor`, `ExportProfileTest.razor`,
  `GeneratedFiles.razor`, `ApiTest.razor`, `Logs.razor`, `NavMenu.razor` portent un `id`
  explicite, généralement interpolé (`id="@($"...-{index}")"` ou `id="...-@profile.Id"`).
- **Patron table+carte mobile** : `ImportProfiles.razor`, `ExportProfiles.razor`,
  `GeneratedFiles.razor` dupliquent chaque bouton d'action avec un id `-card-` suffixé pour la
  vue carte mobile (ex. `edit-profile-button-@profile.Id` / `edit-profile-button-card-@profile.Id`).
  Les deux versions sont simultanément présentes dans le DOM (l'affichage étant contrôlé par CSS
  `d-none d-md-table`/`d-md-none`), donc aucune collision d'id n'est possible puisque les deux
  chaînes générées diffèrent toujours par le suffixe `-card-`.
- **Patron lot de fichiers** : `ImportProfileTest.razor`/`ExportProfileTest.razor` utilisent un
  id sans suffixe pour un lot d'un seul fichier (`equipement-table`) et un id `-{index}` suffixé
  dès que le lot contient plus d'un fichier (`equipement-table-0`, `equipement-table-1`, ...) —
  documenté dans le code (`var suffix = singleFile ? "" : $"-{index}";`) comme un choix délibéré
  de rétrocompatibilité avec les tests bUnit préexistants. Ce mécanisme est intrinsèquement sûr
  contre les collisions puisque `singleFile` est vrai uniquement quand il n'y a qu'un seul
  résultat à rendre.
- **Aucune exception identifiée** : aucun bouton/lien/input/select sans `id` n'a été trouvé dans
  le périmètre audité, à l'exception d'éléments non interactifs (`<span>`, `<div>` de mise en
  forme) qui n'ont pas vocation à en porter un.
- **`ReconnectModal.razor`** : ids fixes non templatés (`components-reconnect-modal`,
  `components-reconnect-button`, `components-resume-button`, `components-seconds-to-next-attempt`)
  — cohérent avec son statut de composant framework standard, non généré dynamiquement.

---

## 5. Zones à mouvement récent

### 5.1 `NavMenu.razor` (+ `.razor.css`)

- **Landmarks** : un seul `<nav class="nav flex-column">` (ligne 18) encapsule tous les liens ;
  le bandeau supérieur (`.top-row`, marque + lien retour) est un `<div>`, pas un `<header>`. Pas de
  `aria-label` sur `<nav>`.
- **Toggler mobile** : `<input type="checkbox" title="@Loc["NavMenu_ToggleTitle"]"
  class="navbar-toggler flex-shrink-0" />` (ligne 15) — voir §3.2 pour l'analyse détaillée
  (pas de `aria-expanded`/`aria-controls`, rôle "checkbox" annoncé plutôt que "bouton de menu").
  Positionné en `position:absolute` (`NavMenu.razor.css:7-9`), hors du flux flex normal, avec un
  commentaire de code expliquant que ce choix est requis pour que le sélecteur CSS
  `.navbar-toggler:checked ~ .nav-scrollable` continue de fonctionner (contrainte technique
  documentée, Lot Y1).
- **Ouverture/fermeture du menu au clic sur un lien** : `<div class="nav-scrollable"
  onclick="document.querySelector('.navbar-toggler').click()">` (ligne 17) — `onclick` HTML natif
  sur un conteneur, ferme le menu mobile après clic sur n'importe quel lien enfant par
  event-bubbling ; ce `<div>` n'est lui-même pas un élément interactif (il délègue au clic sur ses
  enfants réels, qui sont tous des `<a>`/`<button>`), donc l'opérabilité clavier des liens
  eux-mêmes n'est pas affectée — seul le mécanisme de fermeture auto au clic est un `onclick`
  générique sur un `<div>`.
- **Tous les liens de nav portent un `id` stable** : `nav-import-profiles-link`,
  `nav-export-profiles-link`, `nav-api-test-link`, `nav-users-link`, `nav-generated-files-link`,
  `nav-logs-link`, `nav-profile-link`, `nav-register-link`, `nav-login-link`. Le lien Logout n'a
  pas d'`id` propre (c'est un `<button type="submit">` dans un `<form>`, sans `id`).
- **Icônes** : toutes en `background-image` SVG data-URI (`NavMenu.razor.css`), avec
  `aria-hidden="true"` sur chaque `<span class="bi bi-*-nav-menu">` correspondant dans le markup.
- **Visibilité conditionnelle admin/auth** : structure à 3 blocs `<AuthorizeView>` imbriqués/
  séquentiels — un premier `Roles="@IdentitySeeder.AdminRoleName"` (liens Import/Export/ApiTest/
  Users/GeneratedFiles), un second sans rôle (`Logs`, tout utilisateur authentifié), un troisième
  sans rôle avec `<Authorized>`/`<NotAuthorized>` (Profil+Logout vs Register+Login). Cohérent avec
  la description CLAUDE.md du Lot S2 (ordre Import→Export→Users→Logs→Profil→Logout).

### 5.2 Pages TEST (`ImportProfileTest.razor`, `ExportProfileTest.razor`)

- **Upload multi-fichiers** : `<InputFile ... multiple ...>` dans un `.input-group` avec icône SVG
  décorative (`aria-hidden="true"`), `<label class="form-label" for="...">` correctement associé.
  Le bouton `<input type="file">` natif hérite du comportement clavier/lecteur d'écran standard du
  navigateur (non personnalisé).
- **Accordéons imbriqués sur 2 niveaux** : niveau fichier (`batch-file-details`, un par fichier du
  lot) puis niveau section (`equipement`/`isolements`/`points`/`taches-multiples`/`warnings` côté
  import ; `warnings`/feuille générée côté export) — tous suivent le même patron
  `<details><summary @onclick @onclick:preventDefault="true">` documenté en §3.2, avec `open`
  correctement lié à l'état de collapse (`_collapsedFileSections`/`_collapsedSections`).
- **Badges de statut** : `<span class="badge @GetStatusBadgeClass(...)">@GetStatusLabel(...)</span>`
  — texte + couleur, jamais couleur seule (conforme, voir §3.1).
  Classes observées : `bg-success` (Ok), `bg-warning text-dark` (Warning), `bg-danger` (Rejected),
  `bg-dark` (TechnicalError).
  `ExportProfileTest.razor` utilise en plus `bg-success`/etc. sur la carte de résultat de
  génération (`card shadow-sm bg-success-subtle`).
- **Liens de téléchargement** : `<a class="btn btn-success w-100 btn-lg mb-3" download="..."
  href="@fileResult.DownloadDataUrl">` — vrai `<a>` avec attribut `download`, comportement natif,
  pas de JS interop.
- **Résumé de lot** (`#batch-summary`) : aucune région `aria-live`/`role="status"` (voir §3.3),
  malgré le fait qu'il apparaît après un traitement asynchrone potentiellement long (jusqu'à 20
  fichiers).

### 5.3 Cartes de règle de feuille (`SheetRuleForm.razor`, `SheetGenerationRuleForm.razor`, résumé lecture-seule des éditeurs)

- **Vérification littérale de la revendication de parité Lot 037** : les boutons Modifier/Supprimer
  du résumé lecture-seule de carte de règle de feuille (`modify-sheet-rule-button-{index}` /
  `delete-sheet-rule-button-{index}` dans `ImportProfileEditor.razor` lignes 336-347, et
  `modify-sheet-generation-rule-button-{index}` / `delete-sheet-generation-rule-button-{index}`
  dans `ExportProfileEditor.razor` lignes 131-142) utilisent **la même** classe CSS
  (`btn btn-sm btn-outline-secondary block-field-icon-btn` / `btn btn-sm btn-outline-danger
  block-field-icon-btn`) et **la même** icône (`AdminIconMarkup.Pencil`/`AdminIconMarkup.Trash`)
  dans les deux fichiers, confirmant la revendication de CLAUDE.md — vérifié caractère pour
  caractère.
- **En revanche, les boutons Modifier/Supprimer imbriqués plus profondément** (champ de bloc dans
  `SheetRuleForm.razor` lignes 76-93 ; colonne/colonne Point/colonne Application dans
  `SheetGenerationRuleForm.razor` lignes 69-86, 134-151, 196-213) **n'utilisent pas**
  `AdminIconMarkup` — ce sont des `<svg>` inline dupliqués séparément (voir §2.4), même si les
  classes CSS (`btn btn-sm btn-outline-secondary block-field-icon-btn`) et le shape SVG résultant
  restent visuellement identiques à `AdminIconMarkup.Pencil`/`.Trash`. La parité visuelle est donc
  réelle au niveau du rendu, mais pas au niveau de la source (duplication, pas réutilisation).
- **Bouton principal de soumission** (`Ajouter le champ`, `Ajouter la colonne`, etc.) : texte seul,
  `btn btn-outline-secondary w-100 mt-3`, identique entre côté import et export — parité confirmée,
  mais absence d'icône malgré la matrice de décision (voir §2.2).

---

## Hors périmètre / non fait

Conformément à la mission, **aucun fichier de code source n'a été modifié**. Seul le présent
document (`docs/audit-design-blazoradmin-2026-07-27.md`) a été créé. Aucune correction, aucune
recommandation d'implémentation n'est formulée dans ce document — uniquement des constats
factuels destinés à alimenter une revue priorisée par une session Claude distincte.

---

## Non couvert / incertain

- **Contraste de couleur réel rendu** : les valeurs `--m3-*` sont converties en couleurs Bootstrap
  effectives au moment du rendu (cascade CSS, `color-mix()`, thème clair/sombre sélectionné par
  l'utilisateur) — impossible de calculer un ratio de contraste WCAG fiable sans exécuter le
  navigateur et lire les styles calculés.
- **Comportement d'annonce réel des lecteurs d'écran** (NVDA/JAWS/VoiceOver) pour les accordéons
  `<details>`, les badges de statut, les messages d'erreur sans `aria-live` — le markup statique
  permet de repérer l'absence de mécanismes programmatiques, mais pas de garantir ce qu'un lecteur
  d'écran annoncera réellement (certains navigateurs/lecteurs ont un support natif partiel de
  `<details>` qui peut compenser certains manques).
- **Ordre de focus clavier réel et anneau de focus visible** : dépend du CSS `:focus`/
  `:focus-visible` par défaut de Bootstrap (non surchargé de façon visible dans les fichiers audités
  hormis `theme-m3.css` qui redéfinit `--bs-focus-ring-color` et le focus des `.form-control`/
  `.form-select`/`.form-check-input`) combiné à l'ordre du DOM — non vérifiable sans rendu
  interactif réel.
- **Taille de cible tactile réellement rendue** : les valeurs CSS (`34px` pour
  `.block-field-icon-btn`, tailles Bootstrap par défaut pour les autres `.btn`) sont des
  dimensions déclarées ; la boîte réellement rendue dépend du contenu (texte, ligne, police
  chargée), du `box-sizing` cascadé et du zoom navigateur — non mesurable statiquement avec
  certitude à 100 %.
- **Rendu effectif du thème sombre** (`[data-bs-theme="dark"]`) sur chaque composant — les valeurs
  sont définies dans `theme-m3.css`, mais leur application réelle sur des composants tiers
  (spinners Bootstrap, badges, etc.) n'a pas été vérifiée par capture d'écran.
- **Ordre de tabulation à travers les accordéons multi-niveaux** (fichier → section →
  tableau/lien de téléchargement) sur `ImportProfileTest.razor`/`ExportProfileTest.razor` — la
  structure DOM suggère un ordre linéaire cohérent, mais cela n'a pas été vérifié par navigation
  clavier réelle.
- **Fichiers `.razor.js`** (ex. `ReconnectModal.razor.js`) : non lus dans le cadre de cette mission
  (hors périmètre défini — uniquement `.razor`/`.razor.css`), pourraient contenir des comportements
  d'accessibilité complémentaires (gestion de focus au moment de l'ouverture de la modale, etc.)
  non capturés ici.
- **`BlazorAdminMessages.resx` couverture exhaustive** : seul un sondage ciblé a été effectué (clés
  `Logs_*`, `ApiTest_*`, `GeneratedFiles_*`) — la couverture complète des ~36+ clés `ApiTest_`/
  `GeneratedFiles_` et de l'ensemble du fichier (plusieurs centaines de clés vraisemblables au vu
  du nombre de pages) n'a pas été vérifiée exhaustivement clé par clé.
