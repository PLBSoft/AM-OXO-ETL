# État d'avancement — Lot L, masquage NavMenu selon l'authentification (2026-07-21)

*Preuve de livraison de L1, voir `tickets-tdd-blazor-navmenu-visibilite-authentification.md`.
Dépendait de la fin du Lot K (en particulier K4) — confirmée avant de démarrer : `NavMenu.razor`
sur `main` ne contenait déjà plus aucun lien `Mappings`/`Dashboard`/`History` et exposait bien
`/import-profiles`, `/import-profiles/test`, `/export-profiles`, `/export-profiles/test`, `/users`.*

---

## 1. Fichier modifié

[`NavMenu.razor`](../src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor) — le bloc admin
était déjà enveloppé dans un unique `<AuthorizeView Roles="@IdentitySeeder.AdminRoleName">`
(lignes 24-59) avant ce lot, donc les liens admin étaient **déjà** absents du DOM tant que
l'utilisateur n'est pas authentifié en tant qu'Admin (pas de régression de sécurité, conforme au
constat initial du ticket). Ce qui manquait réellement :

- **Aucun `id` stable** sur les 5 `NavLink` admin (`users`, `import-profiles`,
  `import-profiles/test`, `export-profiles`, `export-profiles/test`) — impossible de vérifier
  leur absence du DOM par sélecteur bUnit sans dépendre du texte.
- **Aucun bloc `<NotAuthorized>`** sur cet `AuthorizeView` — donc rien n'y indiquait explicitement
  à l'utilisateur non authentifié qu'un lien de connexion existe à cet endroit précis du menu.

Ajouté (lignes 27, 32, 37, 42, 47 pour les 5 IDs ; lignes 52-58 pour le nouveau bloc) :

```razor
<NotAuthorized>
    <div class="nav-item px-3">
        <NavLink id="nav-login-link" class="nav-link" href="Account/Login">
            <span class="bi bi-person-badge-nav-menu" aria-hidden="true"></span> @Loc["NavMenu_Login"]
        </NavLink>
    </div>
</NotAuthorized>
```

Réutilise la clé de ressource `NavMenu_Login` (déjà existante, EN "Login" / FR "Connexion") et
l'icône `bi-person-badge-nav-menu` (déjà présente dans `NavMenu.razor.css`) — **aucune nouvelle
entrée resx ni règle CSS**, donc pas de nouvelle icône orpheline à surveiller pour ce lot.

Le second `<AuthorizeView>` générique (tout utilisateur authentifié, lignes 61-95 — Profil/
Logout/Register/Login) n'a pas été touché, conformément au périmètre du ticket ("aucun changement
de comportement pour un lien qui ne serait pas protégé par rôle").

---

## 2. Recherche exhaustive des pages protégées

`grep "Authorize(Roles = IdentitySeeder.AdminRoleName)"` sur
`Components/Pages/Admin/*.razor` → 7 fichiers :

| Page | Route | Lien NavMenu correspondant |
|---|---|---|
| `Users.razor` | `/users` | `#nav-users-link` |
| `ImportProfiles.razor` | `/import-profiles` (+ `/`) | `#nav-import-profiles-link` |
| `ImportProfileTest.razor` | `/import-profiles/test` | `#nav-import-profiles-test-link` |
| `ExportProfiles.razor` | `/export-profiles` | `#nav-export-profiles-link` |
| `ExportProfileTest.razor` | `/export-profiles/test` | `#nav-export-profiles-test-link` |
| `ImportProfileEditor.razor` | `/import-profiles/new`, `/import-profiles/{id}/edit` | *aucun — pré-existant* |
| `ExportProfileEditor.razor` | `/export-profiles/new`, `/export-profiles/{id}/edit` | *aucun — pré-existant* |

Les deux pages `*ProfileEditor.razor` n'ont jamais eu d'entrée dans `NavMenu.razor` (atteintes
uniquement via les boutons "Créer"/"Éditer" des pages de liste, Lots F1.1/J1/J2) — ce n'est pas un
oubli de ce lot, comportement inchangé. Les 5 autres pages ont chacune leur lien, tous dans le
bloc `<Authorized>` du même `AuthorizeView Roles="Admin"` : aucun lien protégé oublié.

---

## 3. Tests ajoutés

[`NavMenuTests.cs`](../tests/ExcelETL.BlazorAdmin.Tests/Layout/NavMenuTests.cs), convention bUnit
déjà en place (`this.AddAuthorization().SetNotAuthorized()` / `.SetAuthorized(...)`), `SetRoles`
utilisé pour la première fois dans ce fichier (aucun précédent dans le dépôt, méthode confirmée
présente dans `bunit.xml` 2.7.2 avant utilisation) :

- `NavMenu_WhenNotAuthorized_HidesAdminLinks_AndShowsLoginLink` — non authentifié : les 5 IDs
  admin absents du DOM (`FindAll` vide, pas une vérification CSS `hidden`/`disabled`) ;
  `#nav-login-link` présent, `href="Account/Login"`.
- `NavMenu_WhenAuthorizedAsAdmin_ShowsAdminLinks_AndHidesLoginLink` — authentifié +
  `SetRoles(IdentitySeeder.AdminRoleName)` : les 5 IDs admin présents (1 occurrence chacun) ;
  `#nav-login-link` absent.
- Pas de test "authentifié sans rôle Admin" — décision actée du ticket, aucun compte de ce type
  n'existe dans l'app aujourd'hui.
- Les 5 tests pré-existants (culture EN/FR, profil, logout) passent sans modification de leur
  intention.

Cycle TDD respecté : les 2 nouveaux tests ont d'abord été exécutés seuls et ont échoué (rouge —
`ElementNotFoundException` sur `#nav-login-link`, `FindAll` vide sur les IDs admin faute
d'attribut `id`), puis l'implémentation ci-dessus les fait passer (vert).

---

## 4. Résultat des tests

```
dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter "FullyQualifiedName~NavMenuTests"
→ Réussi : 7/7, 0 échec, 0 ignoré

dotnet test tests/ExcelETL.BlazorAdmin.Tests
→ Réussi : 79/79, 0 échec, 0 ignoré (aucune régression sur le reste de la suite BlazorAdmin)
```

---

## 5. Hors périmètre confirmé non touché

- `FallbackPolicy` et les attributs `[Authorize(Roles = ...)]` des 7 pages — inchangés.
- Écran de login/logout (`Account/Login`, `Account/Logout`) — inchangés, seul le lien vers la
  route existante a été ajouté.
- `NavMenu.razor.css` — aucune modification (icône réutilisée, pas de nouvelle règle).

**Lot L complet (L1, seul ticket du lot).**
