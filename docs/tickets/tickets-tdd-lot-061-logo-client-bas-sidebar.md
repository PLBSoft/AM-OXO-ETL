# Tickets TDD — Lot 061 : logo client en bas de la sidebar

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 060.*

**Origine** : demande client relayée par Simon (30/07, en session Claude/chat) — afficher le logo du
client en bas de la sidebar de `ExcelETL.BlazorAdmin`. Simon a fourni un unique fichier logo, pas de
déclinaison ni de mécanisme multi-client à prévoir (décision actée en session : asset statique commité
au dépôt, aucune configurabilité).

---

## 61.0 — Constats vérifiés (déjà faits en session chat, pas à refaire par Claude Code)

1. **Asset déjà déposé au dépôt par Simon**, prêt à l'emploi — pas à générer ni à demander par Claude
   Code : `src/ExcelETL.BlazorAdmin/wwwroot/images/client-logo.png` (PNG RVBA 256×256, 1065 octets,
   fond rouge plein avec texte « ETL » blanc). C'est le seul fichier fourni, aucune variante
   sombre/claire ni format vectoriel — pas de logique de bascule de logo par thème à inventer.
2. **Dossier `wwwroot/images/` créé pour l'occasion** — n'existait pas avant ce lot dans
   `ExcelETL.BlazorAdmin` (vérifié par recherche exhaustive du dépôt).
3. **Sidebar = `NavMenu.razor`, rendue dans `MainLayout.razor`** (`Components/Layout/MainLayout.razor:8-10`) :
   `<div class="sidebar"><NavMenu /></div>`. Toute la structure de navigation vit dans
   `Components/Layout/NavMenu.razor` (lignes 20-112) : un conteneur `#nav-scrollable` contenant un seul
   `<nav class="nav flex-column">`, lui-même composé d'une suite de blocs `<AuthorizeView>` (un par lien).
   Le logo prend place comme **dernier enfant** de `<nav class="nav flex-column">`, après le dernier
   `<AuthorizeView>` (profil/déconnexion ou connexion selon l'état d'authentification, lignes 84-110) —
   décision de portée ci-dessous.
4. **Comportement responsive existant à ne pas casser** (`NavMenu.razor.css:156-177`) :
   `#nav-scrollable` est masqué (`display:none`) par défaut et affiché uniquement quand la case à cocher
   `#nav-menu-toggler` est cochée (mobile, largeur < 641px) ; au-delà de 641px il reste toujours affiché
   et devient défilable (`overflow-y:auto`) si la liste de liens dépasse la hauteur de l'écran. Le logo,
   en tant que dernier enfant de `#nav-scrollable`, hérite naturellement de ce comportement : visible
   en bas de sidebar en desktop, visible en bas du menu déroulé en mobile, jamais affiché seul hors du
   menu ouvert. Aucune règle CSS de positionnement fixe/sticky n'est nécessaire ni demandée — décision
   de ne pas ajouter de complexité non demandée par le client (« en bas de la sidebar », pas
   « toujours visible même en scrollant »).
5. **Convention de clé de ressource déjà en place** : chaque libellé de `NavMenu.razor` a une clé
   `NavMenu_*` dans `Resources/BlazorAdminMessages.resx` (anglais, culture par défaut) et son pendant
   dans `BlazorAdminMessages.fr.resx` (français) — ex. `NavMenu_ImportProfiles` (`resx:391-393` dans les
   deux fichiers). Le texte alternatif du logo suit la même convention plutôt que d'inventer un mécanisme
   parallèle.
6. **Convention de test déjà en place** : `tests/ExcelETL.BlazorAdmin.Tests/Layout/NavMenuTests.cs` rend
   `<NavMenu />` via bUnit (`BunitContext`, `this.AddAuthorization()...`), avec un helper `WithCulture`
   existant (lignes 17-30) pour tester les deux cultures (`en-US`/`fr-FR`) sans le dupliquer. Le nouveau
   test réutilise cet helper tel quel plutôt que d'en écrire un nouveau.
7. **bUnit ne vérifie jamais qu'un fichier statique existe réellement sur disque au runtime servi** — le
   test ne peut porter que sur le balisage produit (attribut `src` pointant vers le bon chemin relatif,
   attribut `alt` localisé, position dans le DOM). L'existence physique du fichier
   (`wwwroot/images/client-logo.png`, point 61.0.1) est déjà vérifiée par constat direct, pas par un
   test automatisé — cohérent avec la règle « recommandations-tickets-tdd.md » : ceci n'est pas un
   comportement de routage/navigation, donc la section 6 (test `WebApplicationFactory`) ne s'applique
   pas ici.

---

## 61.1 — Afficher le logo client en bas de la sidebar

**Effort** : standard (mise en place mécanique, aucun arbitrage architectural restant).

- **Rouge** : nouveau test dans `NavMenuTests.cs` (même classe, même `WithCulture` helper) :
  - `NavMenu_AlwaysRendersClientLogo_AsLastNavItem` : rend `<NavMenu />` (état authentifié, peu importe
    la culture) ; `cut.Find("#sidebar-client-logo")` existe ; son attribut `src` vaut
    `images/client-logo.png` (chemin relatif à `wwwroot`, cohérent avec la résolution Blazor des assets
    statiques — pas de `/` initial, pour rester valide quel que soit le chemin de base de déploiement) ;
    l'élément est le **dernier enfant** de `nav.nav.flex-column` (assertion sur
    `cut.Find("nav.nav.flex-column").Children.Last()` ou équivalent — pas une simple présence, pour
    prouver la position « en bas »).
  - `NavMenu_ClientLogo_AndEnglishCulture_HasEnglishAltText` (`WithCulture("en-US", ...)`) : l'attribut
    `alt` de `#sidebar-client-logo` contient le texte de `NavMenu_ClientLogoAlt` en anglais.
  - `NavMenu_ClientLogo_AndFrenchCulture_HasFrenchAltText` (`WithCulture("fr-FR", ...)`) : idem en
    français.
  - `NavMenu_ClientLogo_VisibleRegardlessOfAuthorizationState` : rendre une fois avec
    `SetNotAuthorized()` et une fois authentifié — `#sidebar-client-logo` présent dans les deux cas (le
    logo n'est enveloppé dans aucun `<AuthorizeView>`, contrairement aux liens de navigation).
- **Vert** :
  - Ajouter les deux clés de ressource dans `Resources/BlazorAdminMessages.resx`
    (`NavMenu_ClientLogoAlt` → `Client logo`) et `Resources/BlazorAdminMessages.fr.resx`
    (`NavMenu_ClientLogoAlt` → `Logo client`), même structure `<data name="..." xml:space="preserve">`
    que les entrées `NavMenu_*` voisines.
  - Dans `NavMenu.razor`, ajouter après le dernier `<AuthorizeView>` (ligne 110) et avant `</nav>`
    (ligne 111), **hors de tout `<AuthorizeView>`** :
    ```razor
    <div class="sidebar-logo-container">
        <img id="sidebar-client-logo" src="images/client-logo.png" alt="@Loc["NavMenu_ClientLogoAlt"]" />
    </div>
    ```
  - Dans `NavMenu.razor.css`, ajouter une règle `.sidebar-logo-container` (padding vertical cohérent
    avec `.nav-item` existant, `text-align:center`) et `.sidebar-logo-container img` (`max-width: 70%`
    ou équivalent contenu dans la largeur de la sidebar, `height: auto` pour conserver le ratio 1:1 du
    PNG source).
- **Refactor** : vérifier qu'aucune règle CSS existante (`.nav-item:last-of-type { padding-bottom: 1rem; }`,
  `NavMenu.razor.css:130-132`) ne cible plus le mauvais élément maintenant que le logo est le vrai
  dernier enfant du conteneur — cette règle cible `.nav-item`, une classe que le nouveau `<div>` du logo
  ne porte pas, donc pas de conflit attendu, mais à vérifier visuellement (espacement bas de sidebar) une
  fois le test vert.

---

## Hors périmètre

- **Mécanisme multi-client / logo configurable par tenant** — décision actée avec Simon (30/07) : un
  seul client, un seul logo statique commité, pas de configuration à prévoir. Ne pas réouvrir sans
  nouvelle instruction explicite.
- **Variante de logo par thème clair/sombre** — un seul fichier fourni par Simon, aucune déclinaison à
  produire ni à demander de produire par Claude Code.
- **Positionnement collant (`position: sticky`/`fixed`) au bas de la sidebar pendant le défilement** —
  la demande client est « en bas de la sidebar », pas « toujours visible même en scrollant » ; le
  placement en dernier enfant du conteneur défilant satisfait la demande sans complexité additionnelle
  (61.0.4). Revisiter uniquement si retour client explicite demandant ce comportement.
- **Lien cliquable sur le logo** (ex. retour à l'accueil) — non demandé, l'image reste statique et
  non interactive.
- **Toute modification hors de `ExcelETL.BlazorAdmin`** — ce lot est strictement Blazor Admin (asset +
  markup + CSS + ressources de localisation), aucun impact Domain/Application/Infrastructure/WebAPI.
