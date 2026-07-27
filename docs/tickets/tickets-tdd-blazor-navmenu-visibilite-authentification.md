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

## L2. Correctif — lien "Connexion" dupliqué + lien "Journaux" visible sans authentification

*Ouvert le 21/07 suite à vérification manuelle dans le navigateur après livraison de L1 (voir
capture) : deux régressions constatées, toutes deux dues au fait que la recherche exhaustive de
L1 ne portait que sur `Authorize(Roles = IdentitySeeder.AdminRoleName)` — trop étroite pour
couvrir tous les cas réels de `NavMenu.razor`.*

**Constat 1 — lien de connexion dupliqué** :
`NavMenu.razor` contient deux `AuthorizeView` distincts qui produisent chacun un lien de connexion
en `<NotAuthorized>` quand l'utilisateur n'est pas connecté :
- Le bloc rôle Admin (lignes ~24-59), auquel L1 a ajouté `#nav-login-link`.
- Le bloc générique préexistant "utilisateur authentifié" (lignes ~61-95, Profil/Logout/
  Register/Login), qui affichait déjà un lien de connexion dans son propre `<NotAuthorized>`,
  **non modifié par L1** (confirmé par `etat-avancement-lot-l-navmenu-visibilite-authentification-2026-07-21.md`
  §1 : "n'a pas été touché").

L1 a donc ajouté un lien redondant sans vérifier qu'un lien de connexion existait déjà ailleurs
dans le menu.

**Constat 2 — lien "Journaux" visible sans authentification** :
Ce lien (probablement l'écran de lecture de `SystemLogs`, voir `etat-des-lieux-technique.md` §3)
n'apparaît dans aucune des 7 pages listées par la recherche `grep` de L1 (section 2 de l'état
d'avancement) — sa protection réelle (attribut `[Authorize]` sans rôle, absence totale
d'attribut appuyée uniquement sur le `FallbackPolicy`, ou autre) n'a jamais été vérifiée, et son
lien dans le menu n'est manifestement enveloppé dans aucun `AuthorizeView` adéquat.

**Tâches** :
1. **Audit élargi, avant toute correction** : lister **tous** les liens de `NavMenu.razor` un par
   un (pas seulement les 5 déjà couverts par L1) et, pour chacun, identifier la page cible et sa
   protection réelle dans le code (`[Authorize]` avec ou sans `Roles=`, ou absence totale
   d'attribut). Inclure explicitement le lien "Journaux" dans cet audit — déterminer précisément
   quelle page il cible et quelle est sa protection réelle avant de décider où l'envelopper.
2. **Un seul lien de connexion visible dans tout le menu** quand l'utilisateur n'est pas
   authentifié — pas un par `AuthorizeView`. Choisir un unique emplacement canonique (recommandé :
   conserver le lien générique préexistant dans le bloc "utilisateur authentifié", puisqu'il
   existait avant ce lot ; retirer le `#nav-login-link`/`<NotAuthorized>` ajouté par L1 dans le
   bloc rôle Admin, qui est la source de la duplication) plutôt que d'en supprimer un autre.
3. **"Journaux" masqué tant que sa condition d'accès réelle n'est pas remplie** : envelopper son
   lien dans l'`AuthorizeView` correspondant à sa protection réelle constatée à l'étape 1 — dans
   le bloc rôle Admin si la page exige effectivement ce rôle (même si l'attribut ne le reflète pas
   explicitement, à corriger alors côté page aussi), ou dans un `AuthorizeView` générique
   (authentifié, sans rôle) si c'est son niveau de protection réel. Ne pas supposer, vérifier.
4. Ne pas dupliquer ce travail avec L1 : cette recherche remplace/complète celle de L1, qui reste
   valide pour les 5 liens déjà traités mais n'était pas exhaustive pour le reste du menu.

**Tests** (bUnit) :
- Non authentifié : recherche du sélecteur du lien de connexion (`#nav-login-link` ou équivalent
  retenu à l'étape 2) → **exactement une occurrence** dans tout le rendu de `NavMenu.razor`, pas
  deux.
- Non authentifié : lien "Journaux" **absent** de l'arbre de rendu (même exigence que pour les 5
  liens admin — absence réelle du DOM, pas un masquage CSS).
- Authentifié avec la protection réelle requise (rôle Admin si c'est le cas, ou simple
  authentification sinon, selon le constat de l'étape 1) : lien "Journaux" présent ; lien de
  connexion absent.
- Non-régression : les tests L1 existants (5 liens admin + `#nav-login-link` unique) continuent de
  passer, adaptés si le nouvel emplacement canonique du lien de connexion change leur ID de
  référence.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor` (même fichier que L1) —
et, si l'audit de l'étape 1 révèle qu'une page comme "Journaux" devrait porter
`[Authorize(Roles = IdentitySeeder.AdminRoleName)]` mais ne l'a pas, corriger aussi l'attribut sur
la page elle-même (cohérence entre protection serveur réelle et affichage du menu — ne pas se
contenter de masquer un lien vers une page mal protégée sans corriger la page).

**Hors périmètre** : toute page découverte par l'audit qui ne serait *ni* protégée par rôle *ni*
par authentification simple (page publique légitime) — dans ce cas, documenter explicitement
pourquoi son lien reste visible sans connexion, ne pas la masquer par réflexe.

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
- **Pour L2** : avant toute correction, lire `NavMenu.razor` en entier une seule fois pour
  cartographier tous les liens et tous les `AuthorizeView` existants — évite de répéter l'écart de
  L1 (correction locale sans vue d'ensemble du fichier).

## Ordre recommandé

1. Confirmer que le Lot K (en particulier K4) est terminé et mergé.
2. **L1** (déjà livré, voir `etat-avancement-lot-l-navmenu-visibilite-authentification-2026-07-21.md`).
3. **L2** (correctif — duplication du lien de connexion + visibilité de "Journaux").
