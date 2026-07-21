# État d'avancement — Lot L2, correctif duplication login + "Journaux" (2026-07-21)

*Preuve de livraison de L2, voir `tickets-tdd-blazor-navmenu-visibilite-authentification.md`.
Ouvert suite à vérification manuelle dans le navigateur après L1 : lien de connexion dupliqué et
lien "Journaux" visible sans authentification, tous deux dus à une recherche exhaustive L1 trop
étroite (limitée à `Authorize(Roles = IdentitySeeder.AdminRoleName)`).*

---

## 1. Audit élargi (étape 1 du ticket)

`grep "@page \|@attribute"` sur tous les `.razor` de `ExcelETL.BlazorAdmin` — chaque page et sa
protection réelle :

| Page | Route(s) | Attribut déclaré | Protection réelle |
|---|---|---|---|
| `ImportProfiles.razor` | `/import-profiles`, `/` | `[Authorize(Roles = Admin)]` | Rôle Admin |
| `ImportProfileTest.razor` | `/import-profiles/test` | `[Authorize(Roles = Admin)]` | Rôle Admin |
| `ImportProfileEditor.razor` | `/import-profiles/new`, `/import-profiles/{id}/edit` | `[Authorize(Roles = Admin)]` | Rôle Admin (pas de lien menu, inchangé depuis F1.2) |
| `ExportProfiles.razor` | `/export-profiles` | `[Authorize(Roles = Admin)]` | Rôle Admin |
| `ExportProfileTest.razor` | `/export-profiles/test` | `[Authorize(Roles = Admin)]` | Rôle Admin |
| `ExportProfileEditor.razor` | `/export-profiles/new`, `/export-profiles/{id}/edit` | `[Authorize(Roles = Admin)]` | Rôle Admin (pas de lien menu, inchangé depuis J1/J2) |
| `Users.razor` | `/users` | `[Authorize(Roles = Admin)]` | Rôle Admin |
| `Profile.razor` | `/profile` | `[Authorize]` | Authentifié, tout rôle |
| **`Logs.razor`** | `/logs` | **aucun** | **Authentifié, tout rôle — via `FallbackPolicy` uniquement** ([`Program.cs:100-105`](../src/ExcelETL.BlazorAdmin/Program.cs#L100-L105), `RequireAuthenticatedUser()`) |
| `Login.razor` | `/Account/Login` | `[AllowAnonymous]` | Publique |
| `Register.razor` | `/Account/Register` | `[AllowAnonymous]` | Publique |

**Constat clé** : `Logs.razor` était la seule page du menu sans aucun attribut `@attribute`
explicite — sa protection réelle (authentifié, sans exigence de rôle) reposait entièrement sur le
`FallbackPolicy` implicite, contrairement à `Profile.razor` qui déclare `[Authorize]`
explicitement pour le même niveau de protection. Techniquement déjà protégée côté serveur (pas de
faille), mais incohérente avec la convention du reste du dépôt (toute page nécessitant une
protection la déclare sur elle-même). **Corrigé** : ajout de `@attribute [Authorize]` sur
[`Logs.razor:2`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/Logs.razor#L2) — comportement
serveur inchangé (le `FallbackPolicy` couvrait déjà ce cas), mais la protection est maintenant
déclarée de façon cohérente avec `Profile.razor`.

Son lien menu (`href="logs"`) n'était enveloppé dans **aucun** `AuthorizeView` — toujours visible,
authentifié ou non. C'est la régression 2 constatée.

---

## 2. Correctifs apportés

[`NavMenu.razor`](../src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor) :

**Régression 1 (lien de connexion dupliqué)** — le `<NotAuthorized>` ajouté par L1 au bloc
`AuthorizeView Roles="Admin"` (lignes 52-58 dans la version L1) est **retiré**. Le bloc redevient
`<Authorized>` seul, comme avant L1, en conservant les 5 `id` ajoutés par L1. L'emplacement
canonique retenu (recommandation du ticket suivie) est le lien **préexistant** `Account/Login`
dans le second `<AuthorizeView>` générique (`<NotAuthorized>`, ligne ~87) — il porte maintenant
l'`id="nav-login-link"` (déplacé depuis le bloc Admin, pas dupliqué : un seul `id="nav-login-link"`
existe dans tout le fichier).

**Régression 2 ("Journaux" visible sans authentification)** — le lien `logs`, auparavant hors de
tout `AuthorizeView` (en tête de menu), est maintenant enveloppé dans son propre
`<AuthorizeView><Authorized>` (lignes 18-26), sans `Roles=` puisque sa protection réelle est
"authentifié, tout rôle" (voir audit ci-dessus) — pas le bloc Admin, qui aurait été trop
restrictif et n'aurait pas reflété sa vraie protection. `id="nav-login-link"` réutilisé dans ce
même bloc n'a **pas** été ajouté ici : ce bloc n'a pas de `<NotAuthorized>` du tout, le lien de
connexion canonique restant uniquement dans le bloc générique existant, conformément à l'exigence
"un seul lien de connexion dans tout le menu".

Aucune icône ni règle CSS nouvelle : `bi-card-list-nav-menu` (Journaux) et
`bi-person-badge-nav-menu` (connexion) existaient déjà dans `NavMenu.razor.css` avant ce lot —
`NavMenu.razor.css` reste inchangé.

---

## 3. Tests

[`NavMenuTests.cs`](../tests/ExcelETL.BlazorAdmin.Tests/Layout/NavMenuTests.cs) :

- **Modifiés** (régression corrigée, comportement attendu changé) : les 2 tests culture EN/FR
  pré-existants (`...ShowsEnglishLinks`/`...ShowsFrenchLinks`) affirmaient à tort que "Logs"/
  "Journaux" était visible **sans authentification** — assertion retirée (elle contredisait le
  correctif de la régression 2), les assertions Register/Login conservées.
- **Ajoutés** :
  - `NavMenu_WhenNotAuthorized_ShowsLoginLink_ExactlyOnce` — `FindAll("#nav-login-link")` a
    exactement 1 élément (preuve directe de la non-duplication).
  - `NavMenu_WhenNotAuthorized_HidesLogsLink` — `#nav-logs-link` absent de l'arbre de rendu (pas
    un masquage CSS).
  - `NavMenu_WhenAuthorized_WithoutAdminRole_ShowsLogsLink` — `SetAuthorized(...)` sans
    `SetRoles` (même pattern que le test générique Logout pré-existant, pas un compte non-admin
    fabriqué artificiellement — juste une vérification que le bloc générique, sans exigence de
    rôle, fonctionne) → `#nav-logs-link` présent.
- Les 2 tests L1 (`HidesAdminLinks_AndShowsLoginLink`, `ShowsAdminLinks_AndHidesLoginLink`)
  passent sans modification de leur intention (le sélecteur `#nav-login-link` reste valide, juste
  déplacé dans le fichier source).

Cycle TDD respecté : les 3 nouveaux tests ont d'abord tous été exécutés — 1 a échoué en rouge
(`NavMenu_WhenAuthorized_WithoutAdminRole_ShowsLogsLink`, `#nav-logs-link` inexistant faute d'`id`
sur le lien Logs à l'époque), les 2 autres étaient déjà vrais par coïncidence sur l'ancien code
(un seul `#nav-login-link` existait déjà, juste au mauvais endroit ; `#nav-logs-link` n'existait
nulle part donc son absence pour un visiteur non connecté était déjà vraie) — ce qui ne remet pas
en cause le correctif, qui reste nécessaire pour la régression réelle (visibilité pour
l'utilisateur authentifié, testée par le 3ᵉ cas). Implémentation appliquée, tout passe au vert.

---

## 4. Résultat des tests

```
dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter "FullyQualifiedName~NavMenuTests"
→ Réussi : 10/10, 0 échec, 0 ignoré

dotnet test tests/ExcelETL.BlazorAdmin.Tests
→ Réussi : 82/82, 0 échec, 0 ignoré (aucune régression sur le reste de la suite BlazorAdmin)
```

---

## 5. Vérification manuelle navigateur

Serveur `blazor-admin` (`.claude/launch.json`) démarré, navigation vers `/` non authentifié →
redirection vers `/Account/Login` (comportement inchangé, `ImportProfiles.razor` reste protégée).
`read_page` sur la sidebar confirme : uniquement **"Register"** et **"Login"** visibles (un seul
lien "Login") — ni "Journaux", ni aucun lien admin. Aucune erreur console. Vérification en état
authentifié Admin non refaite au navigateur (nécessiterait un mot de passe de compte seed non
présent dans le dépôt, lu depuis `AdminSeedPasswords` via User Secrets) — couverte de façon
déterministe par les tests bUnit `SetRoles(IdentitySeeder.AdminRoleName)` à la place.

---

## 6. Hors périmètre confirmé

Aucune page découverte par l'audit ne s'est révélée publique sans être déjà marquée
`[AllowAnonymous]` (`Login`/`Register`) — pas de cas "hors périmètre" à documenter.

**Lot L complet (L1 + L2).**
