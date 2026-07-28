# Tickets TDD — Lot 044 : gestion des utilisateurs (création / modification / suppression)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Sixième lot
utilisant la convention numérique à trois chiffres, après le lot 043
(`tickets-tdd-lot-043-confirmation-navigation-modifications-non-enregistrees.md`).*

**Demande client (remontée par Simon, session du 27/07)** : à l'approche du déploiement, le
client va vouloir tester l'application et potentiellement donner des accès à d'autres
utilisateurs. Pour éviter qu'il partage ses propres identifiants, il doit pouvoir créer et
supprimer des comptes lui-même depuis l'admin. Aujourd'hui, `Users.razor` (`/users`) n'affiche la
liste des utilisateurs qu'en lecture seule (voir `audit-design-blazoradmin-2026-07-27.md` §1.1) ;
un seul rôle `Admin` existe, porté par les 3 comptes du seed (`IdentitySeeder`).

**Conventions déjà en place à respecter** :
- Patron CRUD de liste déjà validé sur `ImportProfiles.razor`/`ExportProfiles.razor` (Lot 028) :
  bouton d'action par ligne, confirmation de suppression **inline** (pas de `window.confirm()`,
  pas de modale Bootstrap JS), un seul bloc de confirmation actif à la fois par page, boutons
  `Confirmer`/`Annuler` (`btn-danger`/`btn-secondary`), IDs stables par ligne
  (`#action-button-{id}`).
- `convention-ui-blazor-alignement-boutons.md` (actions alignées à droite) et
  `convention-ui-blazor-icones-boutons.md` (icône seule pour une action de ligne de tableau,
  `aria-label`/`title` obligatoires).
- `IDbContextFactory<T>` / pas de `DbContext` scoped injecté directement — non concerné ici,
  l'accès se fait via `UserManager<ApplicationUser>`/`RoleManager` (ASP.NET Core Identity), déjà
  en place, jamais réinventé.
- xUnit 2.9.3 + FluentAssertions 7.x (jamais v8+) + Moq + bUnit. Pas de `DbContext` mocké au
  niveau repository — ici, `UserManager`/`SignInManager` sont mockables via leurs constructeurs
  standards (pattern déjà utilisé pour tester `IdentitySeeder`, à réutiliser).
- IDs HTML stables, jamais de sélection par texte/position en bUnit.

**Décisions actées avec Simon (27/07)** :
- **Pas de nouveau rôle.** "Non-admin" = utilisateur sans aucun rôle. Le rôle `Admin` reste
  l'unique rôle du système, réservé aux comptes déjà seedés — YAGNI, aucun rôle supplémentaire
  n'apporte de permission distincte à ce jour.
- **Le formulaire de création ne propose aucun choix de rôle.** Tout compte créé via l'UI est
  non-admin par construction ; il n'existe aucun moyen de créer un Admin depuis l'interface.
- **Mot de passe (création et réinitialisation) : mécanisme unique.** Un mot de passe temporaire
  est **généré automatiquement** par le serveur (jamais saisi par l'admin), affiché **une seule
  fois** à l'écran juste après l'action (création ou réinitialisation), puis **jamais stocké en
  clair ni ré-affichable** ensuite. Le flag Identity `RequirePasswordChangeOnFirstLogin` (custom,
  voir 44.1) est positionné à `true` pour forcer l'utilisateur concerné à changer ce mot de passe
  temporaire à sa prochaine connexion. Décision explicite : pas d'envoi d'e-mail (aucune
  infrastructure SMTP dans le stack actuel) — communication du mot de passe temporaire à
  l'utilisateur hors application, à la charge de l'admin.
- **Modification** : Prénom / Nom / Email + réinitialisation de mot de passe (même mécanisme que
  ci-dessus). Pas de changement de rôle (cohérent avec la décision précédente).
- **Garde-fous de suppression** : un admin ne peut pas se supprimer lui-même ; le dernier compte
  `Admin` restant ne peut pas être supprimé. Les deux vérifications sont indépendantes et
  cumulatives (un même clic peut être bloqué par l'une, l'autre, ou les deux à la fois).
- **Page Journaux restreinte aux Admin.** Effet de bord acté explicitement avec Simon : les
  utilisateurs non-admin créés par ce lot ne doivent pas voir `#nav-logs-link`, aujourd'hui
  visible par tout utilisateur authentifié (décision du Lot S revue à cette occasion). Le bloc
  `<AuthorizeView>` générique de Journaux est déplacé à l'intérieur du bloc
  `<AuthorizeView Roles="Admin">` existant.

---

## 44.0. Investigation préalable (obligatoire avant tout code)

- [ ] Confirmer la forme exacte de `ApplicationUser` (`FirstName`/`LastName`/`Email`/`UserName`,
  voir `audit-verification-base-de-donnees-2026-07-27.md` §2.2 — colonnes déjà présentes en
  base) et vérifier qu'aucune migration EF Core supplémentaire n'est nécessaire pour ce lot (le
  flag `RequirePasswordChangeOnFirstLogin` est un champ nouveau — une migration Identity sera
  needed, voir 44.1).
- [ ] Relire `IdentitySeeder` pour identifier le pattern déjà en place de génération de mot de
  passe/attribution de rôle, et **réutiliser** son style plutôt qu'en inventer un nouveau (même
  emplacement conceptuel `Infrastructure/Identity/`).
- [ ] Vérifier la politique de complexité de mot de passe déjà configurée
  (`IdentityOptions.Password`) pour que le générateur de mot de passe temporaire produise des
  valeurs qui la respectent systématiquement (sinon `CreateAsync`/`ResetPasswordAsync`
  échoueraient silencieusement côté Identity).
- [ ] Lister les autres comptes disposant du rôle `Admin` = mécanisme exact pour la vérification
  "dernier Admin" (`UserManager.GetUsersInRoleAsync("Admin").Count`).

---

## 44.1. Backend — `UserManagementService` (Infrastructure)

**Comportement attendu**, nouveau composant `Infrastructure/Identity/UserManagementService.cs` (ou
extension d'un service existant si un équivalent existe déjà — à confirmer en 44.0) :

1. `CreateUserAsync(email, firstName, lastName)` :
   - Génère un mot de passe temporaire respectant la politique de complexité en vigueur.
   - Crée l'utilisateur via `UserManager.CreateAsync` (`UserName` = email, pas de rôle assigné).
   - Positionne `RequirePasswordChangeOnFirstLogin = true`.
   - Retourne le mot de passe temporaire en clair **au seul appelant de cette méthode**, pour
     affichage unique côté UI — ne le persiste nulle part (ni logs Serilog, ni base).
2. `UpdateUserAsync(userId, firstName, lastName, email)` : met à jour les champs, ne touche ni au
   rôle ni au mot de passe.
3. `ResetPasswordAsync(userId)` : même mécanique de génération que `CreateUserAsync` (mot de passe
   temporaire régénéré, `RequirePasswordChangeOnFirstLogin` remis à `true`), réutilise
   `UserManager.RemovePasswordAsync` + `AddPasswordAsync` (ou `ResetPasswordAsync` avec token,
   selon ce qui est déjà en usage dans le projet — vérifié en 44.0).
4. `DeleteUserAsync(userId, currentUserId)` :
   - Refuse (retourne un résultat d'échec explicite, pas d'exception) si `userId == currentUserId`
     (auto-suppression).
   - Refuse si l'utilisateur ciblé a le rôle `Admin` **et** qu'il est le dernier compte `Admin`
     restant.
   - Sinon, supprime via `UserManager.DeleteAsync`.

**Tests** (Moq sur `UserManager<ApplicationUser>`/`RoleManager<IdentityRole>`, pas de vrai SQL
Server — pattern déjà utilisé pour les tests `IdentitySeeder`) :
- Création : mot de passe généré respecte la politique de complexité (test de forme, pas de valeur
  fixe), `RequirePasswordChangeOnFirstLogin` vaut `true` après création, aucun rôle assigné.
- Modification : les 3 champs sont mis à jour, rôle et mot de passe inchangés.
- Réinitialisation : nouveau mot de passe différent de l'ancien (mock), flag remis à `true`.
- Suppression refusée si `userId == currentUserId` (`DeleteAsync` jamais appelé sur le
  `UserManager` mocké).
- Suppression refusée si l'utilisateur ciblé est le seul `Admin` restant (mock
  `GetUsersInRoleAsync("Admin")` retournant une liste à un seul élément = l'utilisateur ciblé).
- Suppression autorisée dans tous les autres cas (utilisateur non-admin, ou Admin non-dernier).

---

## 44.2. Migration EF Core — `RequirePasswordChangeOnFirstLogin`

**Comportement attendu** : ajout du champ booléen (défaut `false`) sur `ApplicationUser`,
migration Identity nommée selon la convention déjà en place
(`yyyyMMddHHmmss_AddRequirePasswordChangeOnFirstLoginToApplicationUser`). Les 3 comptes seedés
existants ne sont pas affectés (valeur par défaut `false`, cohérent avec leur statut de comptes
déjà opérationnels).

**Tests** : test de migration EF Core InMemory (colonne présente, valeur par défaut correcte sur
un utilisateur existant non touché par ce lot).

---

## 44.3. `Users.razor` — boutons créer / modifier / supprimer / réinitialiser mot de passe

**Comportement attendu**, sur le modèle exact de `ImportProfiles.razor`/`ExportProfiles.razor`
(Lot 028) :

- `#create-user-button` : ouvre un formulaire (page ou section inline, à trancher en 44.0 selon ce
  qui existe déjà comme patron dans le projet pour un formulaire de création simple — pas de
  nouvelle convention à inventer si un équivalent existe). Champs : Prénom, Nom, Email. Après
  soumission réussie, affichage **unique** du mot de passe temporaire généré (bloc
  `#temporary-password-display`, avec un avertissement explicite indiquant qu'il ne sera plus
  jamais affiché), et rechargement de la liste.
- `#edit-user-button-{id}` : formulaire de modification (Prénom/Nom/Email), symétrique au
  formulaire de création, sans mot de passe.
- `#reset-password-button-{id}` : action directe (pas de formulaire), avec sa propre confirmation
  inline dédiée (`#reset-password-confirm-{id}`, `Confirmer`/`Annuler`), puisqu'elle génère un
  nouveau mot de passe temporaire immédiatement affiché après confirmation — même bloc
  `#temporary-password-display` que la création.
- `#delete-user-button-{id}` : confirmation inline (`#delete-user-confirm-{id}`,
  `Confirmer`/`Annuler`, `btn-danger`/`btn-secondary`), un seul bloc de confirmation actif à la
  fois par page (identique au comportement déjà validé sur les pages Profils).
  - Si le refus vient d'une auto-suppression ou d'un dernier-Admin, le bouton de suppression est
    **désactivé** pour la ligne concernée (`disabled`, `aria-label` explicite indiquant la
    raison), plutôt que de laisser l'utilisateur cliquer puis échouer après coup — évite un
    aller-retour serveur inutile pour un cas prévisible côté client.

**Tests** (bUnit, sur le modèle de `ImportProfilesTests`/`ExportProfilesTests`) :
- Création réussie → `#temporary-password-display` visible avec le mot de passe retourné par le
  service mocké, liste rechargée avec le nouvel utilisateur.
- Modification réussie → champs mis à jour visibles dans la liste.
- Réinitialisation → confirmation inline avant appel, `ResetPasswordAsync` jamais appelé avant
  clic sur `Confirmer`, mot de passe affiché après confirmation.
- Suppression → mêmes assertions que Lot 028 (`Annuler` n'appelle jamais `DeleteUserAsync`,
  `Confirmer` l'appelle avec l'ID exact, ouvrir une confirmation B ferme A sans suppression).
- Ligne correspondant à l'utilisateur courant connecté → `#delete-user-button-{id}` rendu
  `disabled`.
- Ligne correspondant à l'unique Admin restant → `#delete-user-button-{id}` rendu `disabled`
  (scénario simulé via mock retournant une liste d'Admin à un seul élément).

---

## 44.4. `NavMenu.razor` — restriction de la page Journaux aux Admin

**Comportement attendu** : déplacement de `#nav-logs-link`, actuellement dans un bloc
`<AuthorizeView>` générique (visible à tout utilisateur authentifié), à l'intérieur du bloc
`<AuthorizeView Roles="Admin">` existant (celui qui contient déjà
Utilisateurs/Profils d'import/Profils d'export, voir
`tickets-tdd-blazor-navmenu-reorganisation-tests-marque-lot-s.md` §S2). Aucune autre
réorganisation de l'ordre des liens n'est demandée par ce lot.

**Tests** (bUnit) :
- `NavMenu_WhenAuthorizedAsNonAdmin_HidesLogsLink` (nouveau) : un utilisateur authentifié sans le
  rôle `Admin` ne voit plus `#nav-logs-link` dans le DOM (absence réelle, pas `hidden`/`disabled`).
- Mise à jour du test existant qui vérifiait la visibilité de Journaux pour un utilisateur
  authentifié non-Admin (son intention change : il doit désormais vérifier l'**absence**, pas la
  présence) — ne pas dupliquer, corriger le test existant.
- Test existant `NavMenu_WhenAuthorizedAsAdmin_...` : Journaux reste visible pour un Admin, sans
  changement d'intention.

---

## 44.5. Ressources de localisation (EN/FR)

**Comportement attendu** : nouvelles clés `.resx` pour le formulaire de création/modification, le
texte d'avertissement du mot de passe temporaire (`Users_TemporaryPasswordWarning`), les messages
de confirmation de suppression/réinitialisation, et les `aria-label` des boutons désactivés
(auto-suppression / dernier Admin). Vérifier avant création si `Confirmer`/`Annuler` génériques
existent déjà (Lot 028) pour ne pas dupliquer.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet).

---

## Hors périmètre explicite

- Tout nouveau rôle (`User`, `NonAdmin`, etc.) — décision actée : "non-admin" = sans rôle.
- Toute possibilité de créer/promouvoir un Admin depuis l'UI.
- Envoi d'e-mail (invitation, notification de mot de passe réinitialisé) — aucune infrastructure
  SMTP dans le stack actuel, non demandé.
- Changement de rôle sur un utilisateur existant.
- Suppression en masse (sélection multiple).
- Toute réorganisation de `NavMenu.razor` au-delà du déplacement de `#nav-logs-link` (l'ordre des
  autres liens, acté au Lot S, n'est pas rouvert ici).
- Historique/audit des créations/suppressions d'utilisateurs (SystemLogs capture déjà les
  événements applicatifs génériques via Serilog — pas de table dédiée supplémentaire sans ticket
  explicite, cohérent avec la remarque déjà actée sur `ExtractionHistory`).

---

## Note d'efficacité d'implémentation (Claude Code)

- **44.0 doit être terminé avant tout le reste** — la forme exacte de `ApplicationUser` et le
  mécanisme de réinitialisation de mot de passe déjà en usage (token vs remove/add) conditionnent
  44.1 directement.
- **44.1 et 44.2 sont fortement couplés** (le service référence le nouveau champ) — les livrer
  ensemble, migration en premier.
- **44.4 est totalement indépendant** du reste (aucune dépendance sur le service utilisateur) —
  peut être livré à tout moment, y compris en parallèle de 44.1/44.2/44.3.
- **44.3 dépend de 44.1** (le composant Blazor consomme `UserManagementService` via son
  interface) — ne pas commencer le markup avant que l'interface du service soit stabilisée par les
  tests de 44.1.
- **44.5 en dernier**, une fois les textes définitifs de 44.3 connus.

## Ordre recommandé

1. **44.0** (investigation)
2. **44.2** (migration — précède 44.1 puisque le service référence le nouveau champ)
3. **44.1** (service backend)
4. **44.3** (UI)
5. **44.4** (NavMenu — peut être fait en parallèle des points précédents)
6. **44.5** (ressources)
