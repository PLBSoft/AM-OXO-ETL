# Tickets TDD — Lot L : masquage des liens NavMenu selon l'état d'authentification

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite à
un échange assistant du 21/07 : constat que `NavMenu.razor` affiche les liens vers les pages
admin (protégées par `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` + `FallbackPolicy`)
**avant** authentification — l'utilisateur non connecté voit les liens dans la sidebar, est
redirigé vers le login uniquement au clic. Correction purement UI, aucune régression de sécurité
réelle constatée (la protection serveur existe déjà et fonctionne).*

**Dépend strictement de la fin du Lot K** (voir `tickets-tdd-migration-webapi-oxo.md`), et en
particulier de **K4** : K4 modifie déjà `NavMenu.razor` (retrait des liens `Mappings.razor`,
`Dashboard.razor`, `History.razor`) et ajoute une route `@page "/"` sur `ImportProfiles.razor`.
Démarrer L1 avant que K4 soit terminé et mergé créerait un conflit inutile sur le même fichier.
**Ne pas démarrer ce lot tant que K4 n'est pas confirmé terminé** (vérifier
`etat-des-lieux-technique.md` ou un futur `etat-avancement-lot-k-*.md` avant de commencer).

**Conventions déjà en place à respecter** (voir `etat-des-lieux-technique.md` et les lots F/J) :
xUnit 2.9.3 + FluentAssertions 7.0.0 (v8+ interdit, commercial) + Moq + bUnit pour les composants
Razor ; IDs HTML stables sur chaque élément interactif, jamais de sélection par texte ou position
dans les tests bUnit ; paire `.razor`/`.razor.css` toujours tenue à jour ensemble (pas d'icône ou
de règle CSS orpheline, cf. `CLAUDE.md`) ; `AddCascadingAuthenticationState()` +
`IdentityRevalidatingAuthenticationStateProvider` déjà câblés dans
`ExcelETL.BlazorAdmin/Program.cs` — `AuthorizeView` fonctionne donc directement, sans câblage DI
supplémentaire.

**Décisions actées (21/07, avant démarrage du lot) — ne pas rouvrir sans nouvelle demande
explicite** :
- Bloc `<NotAuthorized>` : affiche un lien **"Se connecter"** vers la page login existante (pas
  rien, contrairement à l'hypothèse par défaut envisagée initialement).
- **Aucun compte authentifié non-admin n'existe** dans l'app aujourd'hui (`IdentitySeeder` ne crée
  que des comptes Admin) — donc pas de test bUnit "authentifié mais sans rôle Admin" à fabriquer
  artificiellement pour ce lot ; seuls les deux états réels (non authentifié / authentifié Admin)
  sont testés. Si un rôle non-admin est introduit plus tard, ce cas devra être rouvert séparément.

**Hors périmètre explicite** :
- Toute modification du `FallbackPolicy` ou des attributs `[Authorize(Roles = ...)]` déjà posés
  sur chaque page admin — ils restent la seule protection réelle, inchangée par ce lot.
- Tout écran de login/logout — déjà en place et fonctionnel, seul le **lien** vers la page login
  est ajouté dans le menu, pas de modification de l'écran lui-même.
- Toute page qui n'est pas dans `NavMenu.razor` (ce lot ne touche que l'affichage du menu).
- Introduction d'un rôle non-admin ou de tout mécanisme multi-rôles — hors périmètre tant qu'aucun
  compte de ce type n'existe réellement.

---

## L1. `NavMenu.razor` — masquer les liens admin tant que l'utilisateur n'est pas authentifié

**Comportement attendu** :
- Les liens vers les pages admin listées dans le menu (`/import-profiles`, `/export-profiles`,
  `/export-profiles/test`, `/users`, et tout autre lien protégé par
  `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` existant après la fin du Lot K) sont
  enveloppés dans un seul `<AuthorizeView Roles="@IdentitySeeder.AdminRoleName">` englobant, pas
  un `AuthorizeView` par lien — un seul point de vérité pour le rôle requis, cohérent avec le fait
  que toutes ces pages partagent le même rôle aujourd'hui.
- Bloc `<Authorized>` : les liens actuels, inchangés (mêmes IDs, mêmes icônes `bi-*-nav-menu`,
  mêmes classes CSS).
- Bloc `<NotAuthorized>` : un lien **"Se connecter"** (ID stable, ex. `#nav-login-link`, à
  confirmer/adapter à la convention réelle du fichier) pointant vers la page login existante
  (identifier la route exacte dans le code — `Account/Login` ou équivalent Identity déjà en place,
  ne pas en créer une nouvelle). Aucun autre lien protégé visible dans ce bloc.
- Aucun changement de comportement pour un lien qui ne serait pas protégé par rôle (s'il en existe
  — à vérifier à l'implémentation, pas supposé).

**Tests** (bUnit, miroir des conventions déjà en place pour `Users.razor`/`ImportProfiles.razor`) :
- Rendu de `NavMenu.razor` avec un contexte bUnit non authentifié (`TestAuthorizationContext`
  sans `SetAuthorized`) → les liens admin (IDs stables du menu, ex. `#nav-import-profiles-link`
  ou équivalent réel une fois vérifié dans le fichier) **absents de l'arbre de rendu**, pas
  seulement masqués en CSS (`disabled`/`hidden` insuffisant — vérifier absence réelle du DOM) ;
  lien `#nav-login-link` **présent** avec la bonne route.
- Rendu avec un contexte bUnit authentifié + rôle `IdentitySeeder.AdminRoleName`
  (`SetAuthorized(...)` + `SetRoles(...)`, pattern déjà utilisé si présent ailleurs dans
  `BlazorAdmin.Tests` — vérifier convention existante avant d'écrire, ne pas en introduire une
  nouvelle) → tous les liens admin présents, inchangés par rapport au comportement actuel ; lien
  `#nav-login-link` **absent**.
- Pas de test "authentifié mais sans rôle Admin" — décision actée : aucun compte de ce type
  n'existe aujourd'hui (voir décisions actées ci-dessus), ne pas fabriquer ce cas artificiellement.
- Non-régression : les tests bUnit existants sur `NavMenu.razor` (s'il y en a déjà) continuent de
  passer sans modification de leur intention, seulement adaptés au nouveau wrapping si nécessaire.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor` (extension, pas nouveau
fichier) — pas de changement attendu côté `NavMenu.razor.css` (le CSS ne dépend pas de l'état
d'authentification), donc pas de nouvelle règle orpheline à surveiller ici, sauf si
l'implémentation en décide autrement (à documenter si c'est le cas).

**Preuve attendue dans le futur `etat-avancement-lot-l-*.md`** : lien exact du fichier + lignes du
wrapping `AuthorizeView`, liste des tests ajoutés avec leurs IDs, et confirmation explicite qu'
aucun lien protégé n'a été oublié dans le bloc `<Authorized>` (recherche exhaustive des
`[Authorize(Roles = IdentitySeeder.AdminRoleName)]` restants dans
`Components/Pages/Admin/*.razor` après la fin du Lot K, comparée un par un aux liens du menu).

---

## Note d'efficacité d'implémentation (Claude Code)

- Lot volontairement réduit à un seul ticket (L1) — pas de découpage supplémentaire, la tâche est
  suffisamment petite pour rester atomique.
- Vérifier la convention `TestAuthorizationContext`/bUnit déjà en usage dans le dépôt (si un autre
  composant authentifié a déjà ce type de test) plutôt que d'en inventer une nouvelle.
- Ne pas toucher au `FallbackPolicy` ni aux attributs `[Authorize]` des pages — la protection
  serveur réelle est déjà correcte et hors périmètre de ce lot.
- Ne pas ajouter de lien "Se connecter" dans `<NotAuthorized>` sans le demander explicitement —
  rester strictement dans le périmètre décrit ci-dessus.

## Ordre recommandé

1. Confirmer que le Lot K (en particulier K4) est terminé et mergé.
2. **L1** (seul ticket de ce lot).
