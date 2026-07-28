# Tickets TDD — Lot 051 : retrait de l'inscription publique et fiabilisation des pages de compte

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 050
(`tickets-tdd-lot-050-identite-connexion-username-unicite-email-role-visible.md`).*

**Origine (constat remonté par Claude Code en clôture du lot 050, point d'arrêt 50.0)** :
`/Account/Register` est atteignable et construit toujours `UserName` à partir de l'e-mail. Sous D1
du lot 050 (jeu de caractères restreint aux lettres, chiffres, `_` et `.`), l'arobase est désormais
interdite : **la page rejette donc systématiquement ses propres inscriptions**, avec une erreur
Identity incompréhensible pour qui la rencontre.

Conformément à l'instruction explicite du ticket 050, Claude Code a signalé sans corriger : il
s'agit d'une décision de produit et de sécurité, pas d'un défaut de format.

**L'état actuel est le pire des trois possibles** : la page est publique, paraît fonctionnelle, et
échoue toujours. Elle n'est stable ni comme fonctionnalité, ni comme protection.

**Fait aggravant relevé dans `audit-design-blazoradmin-2026-07-27.md` (§5.1)** : le lien
`#nav-register-link` figure dans `NavMenu.razor`, dans le bloc `<NotAuthorized>` du troisième
`<AuthorizeView>`, aux côtés de `#nav-login-link`. L'inscription publique n'est donc pas une route
résiduelle qu'il faudrait connaître pour l'atteindre — **c'est un lien proposé à tout visiteur non
authentifié** arrivant sur l'application.

---

## Décision actée avec Simon (28/07) : suppression, pas restriction

`Register.razor` et son lien de navigation sont **supprimés**. Les deux alternatives ont été
examinées et écartées :

- **Restreindre la page au rôle Admin** en ferait un doublon strict de `/users`, qui gère déjà la
  création de comptes avec génération de mot de passe temporaire, verrou de premier changement et
  garde-fous de suppression. Deux chemins de création divergents pour un même besoin, c'est de la
  dette immédiate.
- **La réparer** (poser un `UserName` valide au lieu de l'e-mail) réintroduirait l'auto-inscription
  publique, ce qui **annule l'objectif même du lot 044** : la demande client à l'origine de la
  gestion des utilisateurs était de permettre à l'administrateur de créer les comptes *pour éviter
  qu'il partage ses propres identifiants*. Une page d'inscription libre rend cette précaution sans
  objet.

**Enjeu de sécurité, mesuré sans dramatiser** : sous la décision du lot 044 « non-admin =
utilisateur sans rôle », un compte auto-inscrit obtient une session authentifiée valide, qui
franchit tout `[Authorize]` générique. Le périmètre réellement atteignable est étroit aujourd'hui
(la page Journaux a été restreinte aux Admin en 44.4), mais il s'élargit mécaniquement à chaque page
ajoutée sans `Roles="Admin"` explicite. La suppression ferme la classe de risque, pas seulement son
instance actuelle.

**Ce lot ne rouvre aucune décision antérieure.** Le lot 044 n'a jamais statué sur `Register.razor` —
la page est un résidu du gabarit Blazor Identity d'origine, jamais interrogé depuis.

---

## Périmètre réel — établi par l'inventaire, pas supposé

`audit-design-blazoradmin-2026-07-27.md` §1.1 donne l'inventaire complet de
`Components/Account/` à cette date :

| Fichier | Statut sous ce lot |
| :--- | :--- |
| `Pages/Login.razor` | **Conservé.** Vérifier son bloc de liens (51.3). |
| `Pages/Register.razor` | **Supprimé** (51.1). |
| `Pages/ForcePasswordChange.razor` (ajouté au lot 045) | **Conservé, non modifié.** |
| `Shared/StatusMessage.razor` | **Conservé** — partagé par Login **et** `Profile.razor`, sa suppression casserait les deux. |
| `Shared/RedirectToLogin.razor` | **Conservé, non modifié.** |

**Aucune page dépendante de SMTP n'existe dans ce projet** — ni `ForgotPassword`, ni `ConfirmEmail`,
ni `ResendEmailConfirmation`, ni `ExternalLogin`. Le gabarit Identity a été élagué dès l'origine.
Il n'y a donc **pas** de famille de pages mortes à auditer : le périmètre se limite à `Register` et
à ses points d'entrée. Ne pas partir en exploration au-delà de cet inventaire.

---

## Conventions déjà en place à respecter

- `NavMenu.razor` : structure à trois blocs `<AuthorizeView>` (Admin / authentifié / `<Authorized>`
  vs `<NotAuthorized>`), ordre des liens acté au **Lot S2** — ce lot **retire** un lien, il n'en
  réordonne aucun autre.
- IDs stables sur tout élément interactif, jamais de sélection par texte ou position en bUnit.
- **Lesson du lot 049** : un test bUnit ne prouve jamais qu'une route est atteignable ou non. Toute
  assertion sur la disparition d'une route exige un test `WebApplicationFactory` faisant une vraie
  requête HTTP.
- Pages de compte en rendu SSR statique (`[ExcludeFromInteractiveRouting]` via
  `Components/Account/_Imports.razor`, corrigé au lot 049) — ne pas y toucher.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit.
- Localisation EN/FR via `.resx`.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite de ce lot

- **Toute forme de récupération de mot de passe en self-service** (lien e-mail, questions secrètes) —
  aucune infrastructure SMTP dans le stack, hors périmètre depuis le lot 044. Le recours d'un
  utilisateur ayant perdu son mot de passe est la **réinitialisation par un administrateur**
  (`#reset-password-button-{id}`, lot 044), déjà livrée.
- **Toute modification de `Login.razor` au-delà du retrait de liens morts** (51.3) : ni refonte de
  formulaire, ni changement de mécanisme d'authentification.
- **Toute modification de `ForcePasswordChange.razor`** — lots 045/049, non rouverts.
- **Tout nouveau rôle, toute promotion d'Admin depuis l'UI** — lot 044, non rouvert.
- **Réordonnancement des liens de `NavMenu.razor`** au-delà du retrait de `#nav-register-link`
  (ordre acté au Lot S2).
- **Suppression de `StatusMessage.razor`** — encore consommé par `Login.razor` et `Profile.razor`.

---

## 51.0. Investigation préalable (obligatoire avant tout code)

- [ ] **Recenser toutes les références à la route `Account/Register`** dans `src/` : markup
  (`href`, `NavigateTo`), code C#, configuration, tests. Le retrait doit être exhaustif — un lien
  résiduel produirait exactement le symptôme du lot 049 (page « Introuvable » atteinte depuis un
  lien qui paraît légitime).
- [ ] **Lire le bloc de liens de bas de formulaire de `Login.razor`.** Le gabarit Blazor Identity
  standard y place trois liens : « Register as a new user », « Forgot your password? », « Resend
  email confirmation ». **Hypothèse à vérifier, pas constat** : si ce bloc a survécu à l'élagage du
  gabarit, le premier lien devient mort par ce lot et les deux autres l'étaient déjà, puisque les
  pages correspondantes n'existent pas. Relever ce qui s'y trouve réellement et conclure en 51.3.
- [ ] **Vérifier si `IEmailSender<ApplicationUser>` (ou `IdentityNoOpEmailSender`) est enregistré**
  dans `Program.cs`, et **si `Register.razor` en est le seul consommateur**. Le cas échéant,
  l'enregistrement devient du code mort à retirer — même démarche que le lot 046 (`IFileStorageService`,
  `IWorkbookReader.SheetExists`). **Ne rien supprimer sans avoir confirmé l'absence d'autre
  consommateur**, `ForcePasswordChange.razor` inclus.
- [ ] **Recenser les tests existants portant sur `Register.razor`** (les lots 040 et 042 ont touché
  cette page : `role="alert"` sur `ValidationSummary`, `aria-describedby` sur les champs). Ces tests
  sont **supprimés avec la page**, pas adaptés.
- [ ] Relever le test d'unicité `NavMenu_WhenNotAuthorized_ShowsLoginLink_ExactlyOnce`
  (`audit-qualite-blazoradmin-2026-07-25.md` §, régression corrigée au Lot L2) : il porte sur le
  bloc `<NotAuthorized>` que ce lot modifie, il doit rester vert **sans être réécrit**.

**Effort** : standard. C'est du recensement, pas de la conception — la décision est déjà prise.

---

## 51.1. Suppression de `Register.razor`

**Comportement attendu** : le fichier `Components/Account/Pages/Register.razor` (et son éventuel
`.razor.css` / code-behind) est supprimé. La route `/Account/Register` n'existe plus ; une requête
sur cette URL est traitée par le mécanisme d'erreur standard
(`UseStatusCodePagesWithReExecute("/not-found")`, `Program.cs`), qui **conserve l'URL d'origine dans
la barre d'adresse tout en renvoyant le corps de la page NotFound** — comportement documenté au
lot 049, attendu et non à contourner.

**Tests** (`WebApplicationFactory<Program>`, projet `ExcelETL.BlazorAdmin.Tests`) :
- `GET /Account/Register` **non authentifié** → **404**. C'est le test central du lot ; il doit être
  écrit **avant** la suppression et vérifié **rouge** (la page répond 200 aujourd'hui).
- `GET /Account/Login` → **200** avec le formulaire de connexion — non-régression explicite : la
  suppression d'un fichier voisin dans le même dossier `Account/Pages/` ne doit pas affecter le
  rendu SSR statique de Login (mécanisme `[ExcludeFromInteractiveRouting]`, lot 049).
- `GET /Account/ForcePasswordChange` authentifié avec flag `true` → **200** avec
  `force-password-change-form` — non-régression du lot 049, même dossier, même mode de rendu.

**Suppression des tests de la page** : les tests bUnit portant sur `Register.razor` sont supprimés.
**Le nombre total de tests de la solution va donc diminuer** (1349 aujourd'hui). C'est le résultat
attendu d'une suppression de fonctionnalité, **pas une régression** : ne pas chercher à compenser en
ajoutant des tests ailleurs, ne pas restaurer la page au motif que le compteur baisse.

---

## 51.2. Retrait de `#nav-register-link` dans `NavMenu.razor`

**Comportement attendu** : le lien d'inscription est retiré du bloc `<NotAuthorized>` du troisième
`<AuthorizeView>`. Ce bloc ne contient plus que le lien de connexion. Aucun autre lien n'est déplacé,
renommé ni réordonné (ordre acté au Lot S2).

**Tests** (bUnit) :
- Utilisateur **non authentifié** → `#nav-register-link` **absent du DOM** (absence réelle, pas
  `hidden` ni `disabled` — même exigence qu'au Lot L2 pour `#nav-logs-link`).
- Utilisateur non authentifié → `#nav-login-link` toujours présent, **exactement une fois**
  (non-régression du test d'unicité existant, à conserver tel quel).
- Utilisateur **authentifié** → `#nav-profile-link` et le bouton de déconnexion inchangés ;
  `#nav-register-link` absent également.
- Mise à jour du test existant qui vérifiait la **présence** de `#nav-register-link` pour un
  visiteur non authentifié : son intention s'inverse, il doit désormais vérifier l'**absence** —
  **corriger le test existant, ne pas en ajouter un second à côté**.

---

## 51.3. Liens morts de `Login.razor`

*(Contenu conditionné par la conclusion de 51.0 — à ne remplir qu'après le relevé réel.)*

**Comportement attendu** : `Login.razor` ne propose plus aucun lien vers une page inexistante.

- Lien vers `Account/Register` → **retiré** (la page n'existe plus).
- Liens vers `Account/ForgotPassword` / `Account/ResendEmailConfirmation`, **s'ils existent** → ils
  pointent vers des pages absentes du projet et sont **retirés**.
- **Remplacement, pas simple suppression** : si au moins un lien de récupération de mot de passe est
  retiré, un texte statique localisé prend sa place, indiquant à l'utilisateur de contacter son
  administrateur pour une réinitialisation. Un utilisateur qui a perdu son mot de passe **a** un
  recours réel (réinitialisation par l'admin, lot 044) ; le laisser devant un formulaire muet, ou le
  renvoyer vers une page « Introuvable », transforme un problème résolu en impasse. Pas de lien, pas
  d'adresse e-mail codée en dur : un texte, rien de plus.
- Si le relevé de 51.0 montre que ce bloc de liens n'existe pas dans `Login.razor`, **ce
  sous-ticket est clos sans code** — le consigner explicitement ici plutôt que d'inventer une
  modification.

**Tests** (bUnit) :
- Le rendu de `Login.razor` ne contient **aucun** `href` pointant vers `Account/Register`,
  `Account/ForgotPassword` ou `Account/ResendEmailConfirmation`.
- Si le texte de recours administrateur est ajouté : il est présent, porte un ID stable
  (`#login-password-recovery-hint`), et provient des ressources localisées.
- Non-régression : le formulaire de connexion, ses champs et son bouton de soumission sont inchangés
  (réutiliser les tests existants, ne pas les dupliquer).

---

## 51.4. Nettoyage du code et des ressources devenus morts

**Comportement attendu** :
- Enregistrement DI de `IEmailSender<ApplicationUser>` / `IdentityNoOpEmailSender` retiré **si et
  seulement si** 51.0 a confirmé l'absence de tout autre consommateur.
- Clés `.resx` (EN et FR) propres à `Register.razor` et à `NavMenu_Register` supprimées.
- Aucune clé orpheline laissée derrière, dans aucune des deux langues — même exigence qu'au 50.11.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet). La suppression du
code mort est couverte par la compilation et par la suite existante.

---

## 51.5. Non-régression du parcours d'authentification complet

**Comportement attendu** : le parcours réel reste intact de bout en bout après suppression.

**Test** (`WebApplicationFactory`, un seul test enchaînant les étapes) :
- Compte créé par un admin (flag `RequirePasswordChangeOnFirstLogin = true`) →
  `POST /Account/Login` avec son nom d'utilisateur → 302 vers `/Account/ForcePasswordChange` →
  `GET` de cette page → 200 avec le formulaire → `POST` d'un nouveau mot de passe valide → flag
  levé, redirection accueil → `GET /import-profiles` → 200.
- Reprend exactement le test de clôture du lot 049 (§49.5). **S'il existe déjà sous cette forme, ne
  pas le dupliquer** : vérifier qu'il reste vert suffit, et le consigner ici.

---

## Ordre recommandé

1. **51.0** (recensement — conditionne le contenu réel de 51.3 et 51.4)
2. **51.1** (test HTTP rouge d'abord, puis suppression de la page)
3. **51.2** (NavMenu)
4. **51.3** (liens de Login, selon le relevé)
5. **51.4** (code mort et ressources)
6. **51.5** (non-régression bout en bout — clôture)

## Note d'efficacité d'implémentation (Claude Code)

- **Le test HTTP de 51.1 doit être rouge avant la suppression.** S'il passe du premier coup sur le
  code actuel, c'est qu'il n'exerce pas la bonne couche (rendu bUnit au lieu d'une vraie requête) —
  le réécrire, ne pas le déclarer vert. C'est la leçon du lot 049, appliquée à l'envers : on prouve
  ici qu'une route **cesse** d'exister.
- **Le compteur de tests va baisser, et c'est correct.** Supprimer une page supprime ses tests. Ne
  pas compenser artificiellement, ne pas s'en alarmer, ne pas revenir en arrière.
- **51.3 est conditionnel.** Si le bloc de liens n'existe pas dans `Login.razor`, clore le
  sous-ticket en le consignant plutôt qu'en inventant une modification. Un ticket qui ne s'applique
  pas se ferme, il ne se remplit pas de force.
- **Ne pas supprimer `StatusMessage.razor`** en même temps que `Register.razor` : il est partagé
  avec `Login.razor` et `Profile.razor`. C'est le faux positif évident d'un nettoyage « par
  dossier ».
- **Ne rien explorer au-delà de l'inventaire du § Périmètre réel.** Les pages Identity dépendantes
  de SMTP n'existent pas dans ce projet ; les chercher est du temps perdu.
- Exécuter les tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~Register|FullyQualifiedName~NavMenu|FullyQualifiedName~Login" --verbosity quiet`.
- Effort standard sur tout le lot. Aucune étape ne demande de conception : la décision est prise, il
  s'agit de l'appliquer exhaustivement.
