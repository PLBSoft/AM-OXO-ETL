# Tickets TDD — Lot 045 : application effective du verrou `RequirePasswordChangeOnFirstLogin`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Premier lot
numérique après le lot 044 (`tickets-tdd-lot-044-gestion-utilisateurs.md`).*

**Origine (état des lieux post-Lot 044, `etat-des-lieux-technique-2026-07-27.md` §3.1)** : le
Lot 044 pose bien `RequirePasswordChangeOnFirstLogin = true` à la création et à la
réinitialisation d'un compte (`UserManagementService.CreateUserAsync`/`ResetPasswordAsync`), mais
**aucun point du flux de connexion ne lit ce flag**. `Login.razor` appelle
`SignInManager.PasswordSignInAsync(...)` puis redirige directement vers `ReturnUrl`, sans jamais
vérifier le flag ; `Profile.razor` permet de changer son mot de passe mais rien ne l'impose.
Conséquence : un compte créé par un admin conserve **indéfiniment** son mot de passe temporaire
tant que l'utilisateur ne va pas spontanément sur `/profile`. C'est précisément le scénario que
la demande client à l'origine du Lot 044 (« éviter que le client partage ses propres
identifiants », session du 27/07) voulait empêcher.

**Ce lot ferme cet écart** : il rend le verrou effectif. Le texte du Lot 044 le promettait
explicitement (« ... positionné à `true` pour **forcer** l'utilisateur concerné à changer ce mot
de passe temporaire à sa prochaine connexion ») — ce lot n'introduit donc aucune décision
nouvelle, il implémente une décision déjà actée mais non livrée.

**Priorité** : c'est le **seul point réellement bloquant avant le déploiement** identifié par
l'état des lieux du 27/07 (§6, point 1). À traiter avant que le client crée de vrais comptes pour
d'autres utilisateurs en production. Aucune nouvelle migration EF Core n'est nécessaire : le champ
`RequirePasswordChangeOnFirstLogin` existe déjà sur `ApplicationUser` (migration Identity du
Lot 044).

**Conventions déjà en place à respecter** :
- Accès Identity via `UserManager<ApplicationUser>`/`SignInManager<ApplicationUser>` déjà en place,
  jamais réinventé ; pas de `DbContext` scopé injecté directement.
- xUnit 2.9.3 + FluentAssertions 7.x (jamais v8+) + Moq + bUnit ; `WebApplicationFactory` pour les
  tests d'intégration Web/SSR. `UserManager`/`SignInManager` mockables via leurs constructeurs
  standards (pattern déjà utilisé pour `IdentitySeeder`/`UserManagementService`, à réutiliser).
- IDs HTML stables sur tout élément interactif, jamais de sélection par texte/position en bUnit.
- Pages de compte (`Components/Account/Pages/`) : rendu SSR statique côté Identity (le cookie
  d'authentification est écrit pendant le POST SSR, `HttpContext` disponible) — à distinguer des
  pages admin interactives (`InteractiveServer`). Ce clivage conditionne le point d'accroche exact
  du verrou (voir 45.0).
- Localisation EN/FR via `.resx`, comme le reste du projet.
- Strict Red-Green-Refactor : test qui échoue d'abord, toujours.

**Décision de conception actée avec Simon (27/07)** :
- Le verrou est **global tant que le flag est `true`**, pas seulement un message à la connexion :
  un utilisateur porteur du flag ne doit pas pouvoir atteindre les pages normales de
  l'application en contournant l'étape (ex. en éditant l'URL) — il est ramené à l'étape de
  changement de mot de passe jusqu'à ce que le flag soit levé. Un simple message post-connexion
  non contraignant est insuffisant et ne fermerait pas le risque décrit ci-dessus.
- Le flag est **remis à `false` uniquement après un changement de mot de passe réussi** par
  l'utilisateur lui-même. Une réinitialisation par un admin le repositionne à `true` (comportement
  Lot 044 inchangé, non rouvert).

---

## Hors périmètre explicite de ce lot

- Toute nouvelle politique de mot de passe (complexité, expiration périodique, historique
  anti-réutilisation) — seul le verrou « premier changement obligatoire » est concerné.
- Envoi d'e-mail (notification, lien de changement) — aucune infrastructure SMTP dans le stack
  actuel, cohérent avec le hors-périmètre déjà acté au Lot 044.
- Verrouillage de compte après échecs répétés (`Lockout`), double authentification (2FA) — non
  demandés, hors sujet.
- Modification du mécanisme de génération / d'affichage unique du mot de passe temporaire
  (Lot 044) — inchangé.
- Application du verrou à un canal autre que la connexion interactive BlazorAdmin (l'API
  `/api/oxo/process` est protégée par `ApiKeyAuthenticationHandler`, pas par un compte utilisateur
  Identity — non concernée).

---

## 45.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire le flux de connexion réel : `Components/Account/Pages/Login.razor`
  (`OnValidSubmitAsync`, appel `SignInManager.PasswordSignInAsync`, redirection `ReturnUrl`) et
  confirmer que la page est en rendu SSR statique (le cookie est bien écrit pendant le POST, donc
  une redirection serveur conditionnelle est possible à cet endroit).
- [ ] Lire `Profile.razor` : mécanisme exact de changement de mot de passe déjà en place
  (`UserManager.ChangePasswordAsync` avec ancien+nouveau, ou autre), pour **réutiliser** ce
  mécanisme plutôt que d'en inventer un — c'est le point naturel où lever le flag après succès.
- [ ] Trancher le point d'accroche du verrou global en fonction de l'architecture réelle :
  - Option A (**recommandée**) : exposer le flag comme **claim** ajouté à la connexion (via un
    `IUserClaimsPrincipalFactory<ApplicationUser>` personnalisé, ou ajout explicite du claim au
    sign-in), lu par un composant de garde placé haut dans l'arbre (`Routes.razor`/`MainLayout`)
    qui force la navigation vers la page de changement forcé tant que le claim est présent. Le
    claim disparaît naturellement au ré-sign-in effectué après le changement — pas de lecture base
    à chaque rendu. Vérifier qu'aucun `IUserClaimsPrincipalFactory` custom n'existe déjà avant d'en
    ajouter un.
  - Option B : lecture directe du flag en base par requête de garde — écartée par défaut (coût par
    navigation), à ne retenir que si l'option A se révèle infaisable dans l'architecture réelle.
  - Documenter la conclusion **avant** d'écrire les tests de 45.3/45.4 (elle en change la forme).
- [ ] Confirmer comment forcer un rafraîchissement de l'état d'authentification après le
  changement (re-sign-in / `RefreshSignInAsync`) pour que le claim/flag levé soit pris en compte
  immédiatement, sans exiger une déconnexion manuelle.
- [ ] Recenser les pages qui doivent rester accessibles malgré le flag (au minimum : la page de
  changement forcé elle-même et la déconnexion) pour ne pas créer de boucle de redirection.

---

## 45.1. Backend — levée du flag au changement de mot de passe réussi

**Comportement attendu** : lorsqu'un utilisateur change effectivement son mot de passe
(mécanisme existant de `Profile.razor` / page de changement forcé), `RequirePasswordChangeOnFirstLogin`
est repositionné à `false` **après** le succès de `ChangePasswordAsync` (jamais avant ; si le
changement échoue, le flag reste `true`). Réutiliser le service Identity existant ; encapsuler la
levée du flag dans une méthode dédiée testable (ex. sur `UserManagementService` ou un petit
service de flux de mot de passe) plutôt que de la disperser dans le code-behind d'une page.

**Tests** (xUnit, Moq sur `UserManager<ApplicationUser>`) :
- Changement réussi → `ChangePasswordAsync` renvoie succès, puis le flag est mis à `false` (mise à
  jour de l'utilisateur vérifiée), une seule fois.
- Changement échoué (mot de passe actuel invalide, nouvelle valeur non conforme à la politique) →
  le flag **reste** `true`, aucune mise à jour de levée n'est effectuée.
- Un utilisateur dont le flag est déjà `false` qui change son mot de passe → pas de régression, le
  flag reste `false`.

---

## 45.2. Page de changement de mot de passe forcé (SSR compte)

**Comportement attendu** : page dédiée côté Identity (ex. `/Account/ForcePasswordChange`, nom exact
à aligner sur la convention de routes `Account/` existante) :
- N'est accessible **que** lorsque le flag est `true` ; si le flag est `false`, redirection
  immédiate hors de la page (vers l'accueil), pour ne pas exposer un formulaire de changement forcé
  à un utilisateur qui n'en a pas besoin.
- Demande le mot de passe temporaire courant + le nouveau mot de passe (mécanisme identique à
  `Profile.razor`, réutilisé). Après succès : levée du flag (45.1), ré-authentification/rafraîchissement
  de session (45.0), redirection vers l'accueil.
- Messages d'erreur localisés, `role="alert"` cohérent avec le reste du projet (Lot 040).
- IDs stables : `#force-password-change-form`, `#current-password-input`, `#new-password-input`,
  `#confirm-password-input`, `#force-password-change-submit`.

**Tests** (`WebApplicationFactory` pour le flux SSR, et/ou bUnit selon la forme retenue en 45.0) :
- Accès à la page avec un utilisateur dont le flag est `false` → redirigé hors de la page (pas de
  formulaire rendu).
- Soumission avec un nouveau mot de passe valide → flag levé, redirection accueil, connexion
  ensuite possible avec le nouveau mot de passe (non-régression du sign-in).
- Soumission invalide → reste sur la page, message d'erreur affiché, flag toujours `true`.

---

## 45.3. Redirection depuis la connexion quand le flag est `true`

**Comportement attendu** : après un `PasswordSignInAsync` réussi dans `Login.razor`, si
l'utilisateur porte le flag `true`, la redirection cible la page de changement forcé (45.2) au lieu
de `ReturnUrl`. Si le flag est `false`, comportement actuel strictement inchangé (redirection
`ReturnUrl`).

**Tests** (`WebApplicationFactory`) :
- Connexion d'un compte flag `true` → 302 vers `/Account/ForcePasswordChange` (pas vers `ReturnUrl`).
- Connexion d'un compte flag `false` → redirection `ReturnUrl` inchangée (non-régression du flux
  Lot L / existant).
- Mauvais mot de passe → échec de connexion inchangé, aucune redirection vers la page forcée.

---

## 45.4. Garde globale — impossibilité de contourner l'étape

**Comportement attendu** : tant que l'utilisateur authentifié porte le flag `true`, toute tentative
de navigation vers une page normale de l'application (hors page de changement forcé et
déconnexion) est ramenée à la page de changement forcé. Implémentation selon l'option retenue en
45.0 (recommandée : composant de garde lisant le claim, placé haut dans l'arbre de rendu). Une fois
le flag levé et la session rafraîchie, la navigation redevient libre.

**Tests** (bUnit pour la garde interactive ; forme exacte selon 45.0) :
- Utilisateur authentifié avec flag `true` tentant d'atteindre une page admin (ex. `/users`,
  `/import-profiles`) → redirigé vers la page de changement forcé (vérifié via
  `TestNavigationManager`).
- Même utilisateur atteignant la page de changement forcé ou la déconnexion → **pas** de
  redirection (pas de boucle).
- Utilisateur sans le flag (ou flag levé) → navigation normale, aucune redirection parasite
  (non-régression : les tests de navigation existants restent verts).

---

## 45.5. Ressources de localisation (EN/FR)

**Comportement attendu** : nouvelles clés `.resx` pour le titre et les libellés de la page de
changement forcé, le texte explicatif (« vous devez changer votre mot de passe temporaire avant de
continuer »), et les messages d'erreur spécifiques. Réutiliser les clés génériques existantes
(Enregistrer, mot de passe, etc.) plutôt que de dupliquer.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet).

---

## Ordre recommandé

1. **45.0** (investigation — tranche l'option A/B, condition de forme de 45.3/45.4)
2. **45.1** (levée du flag — pur backend, testable en isolation, base de 45.2)
3. **45.2** (page de changement forcé)
4. **45.3** (redirection depuis la connexion)
5. **45.4** (garde globale — dépend du mécanisme claim/flag choisi en 45.0)
6. **45.5** (ressources, une fois les textes définitifs connus)

## Note d'efficacité d'implémentation (Claude Code)

- **45.0 est réellement bloquant** : le choix claim vs lecture base change la forme des tests de
  45.3/45.4. Ne pas écrire ces tests avant d'avoir tranché.
- **45.1 est autonome** : à livrer et valider en premier pour isoler la logique de levée du flag du
  reste (pages, redirections).
- Attention à la **boucle de redirection** : la page de changement forcé et la déconnexion doivent
  être explicitement exclues de la garde 45.4 — cas à couvrir par un test dédié, pas seulement en
  passant.
- Non-régression prioritaire : les comptes seedés existants (flag `false`) doivent conserver
  exactement le flux de connexion actuel — un test explicite « flag false → ReturnUrl inchangé »
  protège contre une redirection parasite introduite par ce lot.
