# Tickets TDD — Lot 050 : identité de connexion (nom d'utilisateur explicite), unicité de l'e-mail, liste utilisateurs lisible

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 049
(`tickets-tdd-lot-049-correction-page-force-password-change-introuvable.md`).*

**Origine (constat terrain Simon, 28/07, sur `/users` en environnement local)** — quatre défauts
distincts observés sur la page de gestion des utilisateurs livrée au lot 044 :

1. **Le formulaire de création/modification ne permet pas de saisir un nom d'utilisateur.** Le
   lot 044 (§44.1) actait `UserName = email`. Or `Login.razor` authentifie **par nom
   d'utilisateur**, et les 3 comptes seedés par `IdentitySeeder` portent des trigrammes (`SLB`,
   `J2M`, `JPN`), pas des e-mails. Deux conventions d'identifiant de connexion incompatibles
   coexistent donc dans la même base.
2. **Aucune distinction visuelle entre un compte Admin et un compte standard** dans la liste, alors
   que la différence de droits est réelle (pages admin, page Journaux depuis 44.4).
3. **Deux comptes partagent la même adresse e-mail.** `IdentityOptions.User.RequireUniqueEmail` vaut
   `false` par défaut dans ASP.NET Core Identity — rien ne l'a jamais interdit.
4. **Prénom et Nom sont saisis à la création mais jamais affichés** dans la liste ; le tableau
   desktop déborde horizontalement et chaque ligne fait quatre fois sa hauteur utile (GUID replié
   sur quatre lignes, boutons d'action empilés verticalement).

**Ce lot rouvre explicitement une décision du lot 044** (`UserName = email`, §44.1), et c'est
assumé : cette décision reposait sur une prémisse implicite — « l'e-mail est un identifiant de
connexion acceptable » — que le terrain invalide, puisque la connexion se fait par nom
d'utilisateur et que les comptes seedés n'ont jamais suivi cette convention. Aucune autre décision
du lot 044 n'est rouverte.

---

## Décisions actées avec Simon (28/07)

- **D1 — Format du nom d'utilisateur** : longueur **3 à 30 caractères**, jeu de caractères limité
  aux **lettres, chiffres, underscore (`_`) et point (`.`)**. Aucun motif imposé au-delà (pas de
  trigramme obligatoire). Le tiret (`-`), l'arobase (`@`) et le plus (`+`), présents dans le jeu de
  caractères Identity par défaut, sont **retirés**.
- **D2 — Longueur de Prénom et Nom** : **2 à 50 caractères**, les deux obligatoires.
  **Contrainte de longueur uniquement — aucune restriction de jeu de caractères.** Les espaces,
  tirets et apostrophes sont légitimes dans un patronyme (« Le Becq », « Jean-Marie », « O'Brien »,
  « N'Diaye ») et **doivent** être acceptés. Ne pas étendre par symétrie la règle de jeu de
  caractères de D1, qui ne concerne que le nom d'utilisateur : deux des trois comptes seedés
  seraient rejetés, dont celui de l'administrateur principal.
- **D3 — Nom d'utilisateur modifiable après création** : oui, via le formulaire de modification.
- **D4 — Effet d'un renommage sur une session ouverte** : après un changement effectif de nom
  d'utilisateur, `UserManager.UpdateSecurityStampAsync` est appelé, ce qui invalide la session en
  cours du compte concerné à la prochaine revalidation. Sans cela, une session active continuerait
  de porter un nom d'identité périmé. Un renommage « à l'identique » ne déclenche **aucune**
  invalidation.
- **D5 — Unicité de l'e-mail : complète, aux deux niveaux.**
  `IdentityOptions.User.RequireUniqueEmail = true` (validation applicative) **et** index unique
  **filtré** sur `NormalizedEmail` en base (contrainte de schéma). Voir 50.5 : le filtre n'est pas
  optionnel.
- **D6 — Affichage** : colonnes « Prénom », « Nom » et « Rôle » ajoutées à la liste, cette dernière
  avec un **badge** pour les comptes Admin.
- **D7 — Compacité du tableau desktop** : la colonne `Id` est **retirée** de l'affichage, le tableau
  passe en `table-sm`, et les trois boutons d'action de ligne sont **alignés horizontalement** au
  lieu d'être empilés. Voir 50.9.
- **D8 — Base de données repartant de zéro** : la base de développement est supprimée avant
  livraison, et l'application n'est pas encore déployée. Toutes les migrations s'appliquent donc sur
  une base **vierge**. Conséquence directe : aucun doublon d'e-mail préexistant, aucun compte au
  `UserName` contenant `@`. Les deux pièges de reprise de données correspondants **disparaissent** —
  et aucune reprise de données n'est à écrire.

---

## Risque principal du lot — conformité des comptes seedés

**D8 supprime deux risques mais en aggrave un troisième, qui devient le point le plus dangereux de
ce lot.**

Sur une base vierge, `IdentitySeeder` recrée les **trois** comptes au démarrage, et les soumet donc
tous au validateur introduit en 50.1. Or, conformément au comportement déjà en place, un échec de
création de compte au seed produit un **avertissement Serilog et un démarrage poursuivi**, pas une
erreur. Si un compte seedé viole D1 ou D2 — un `FirstName` d'une seule lettre, un tiret dans un
identifiant, une valeur absente — il est **silencieusement ignoré**.

Et comme **aucun rôle Admin ne peut être attribué depuis l'UI** (décision du lot 044, non rouverte),
un seed d'administrateur ignoré signifie : aucun compte Admin en base, aucun moyen d'en créer un,
**application inaccessible à son propre administrateur**.

Traitement : vérification explicite en 50.0 **avant** toute modification d'option, et test permanent
en 50.1 vérifiant que les valeurs de seed satisfont le validateur.

### Note résiduelle sur les migrations

Le contrôle pré-vol de doublons d'e-mail n'a plus lieu d'être sous D8 (base vierge). Il redeviendrait
nécessaire **uniquement** si une base pré-remplie était un jour restaurée sur l'environnement cible.
Le cas échéant, avant migration :

```sql
SELECT NormalizedEmail, COUNT(*) FROM AspNetUsers
WHERE NormalizedEmail IS NOT NULL
GROUP BY NormalizedEmail HAVING COUNT(*) > 1;
```

La migration Identity du lot 044, jamais exercée contre une vraie base
(`audit-verification-base-de-donnees-2026-07-27.md` lui est antérieur), et celle de 50.5 seront
toutes deux couvertes par le même essai à blanc sur le SQL Server cible.

---

## Conventions déjà en place à respecter

- Accès Identity via `UserManager<ApplicationUser>`/`RoleManager<IdentityRole>` déjà en place,
  jamais réinventé ; pas de `DbContext` scopé injecté directement.
- **Aucune duplication de règle de validation côté client** (convention actée au lot F) : les règles
  D1/D2 vivent **uniquement** côté serveur, dans le point d'extension prévu par Identity. Le
  formulaire ne valide que la **présence** des champs et **affiche** les erreurs serveur.
- `LocalizedIdentityErrorDescriber` existe déjà (`Infrastructure/Identity/`) : les erreurs Identity
  natives (`DuplicateUserName`, `DuplicateEmail`, `InvalidUserName`) transitent **par ce
  mécanisme**, jamais par des messages écrits en dur dans une page.
- **Lot V2 — le système tableau desktop + cartes mobiles existe déjà sur `/users`** : un `<table>`
  (`d-none d-md-table`) et une liste de cartes (`d-md-none`) alimentés par **un seul jeu de
  données**, avec un test verrouillant l'**identité de contenu entre les deux**. Il n'y a rien à
  créer de ce côté ; tout champ ajouté à l'un doit l'être à l'autre, et le test d'invariant doit
  être **étendu**, jamais doublé.
- **Lot V3 — icônes seules pour les actions de ligne**, avec `aria-label` + `title` obligatoires
  (`convention-ui-blazor-icones-boutons.md`).
- `convention-ui-blazor-alignement-boutons.md`.
- Bandeaux d'erreur : `role="alert"` (Lot 040).
- Jamais la couleur seule comme porteuse d'information (WCAG 1.4.1) — un statut de rôle porte
  toujours un texte.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite, licence commerciale) + Moq + bUnit ;
  `WebApplicationFactory` pour tout ce qui touche au routage/HTTP.
- IDs HTML stables sur tout élément interactif, jamais de sélection par texte ou position en bUnit.
- Localisation EN/FR via `.resx`.
- Strict Red-Green-Refactor : le test qui échoue d'abord, toujours.

---

## Hors périmètre explicite de ce lot

- **Toute possibilité de créer, promouvoir ou rétrograder un Admin depuis l'UI** — décision du
  lot 044, **non rouverte**. Ce lot **affiche** le rôle, il ne le modifie jamais.
- **Tout nouveau rôle** — « non-admin » = sans rôle, décision du lot 044 inchangée.
- **Connexion par e-mail** en alternative au nom d'utilisateur — non demandé, et contraire à
  l'objectif du lot (un identifiant de connexion, un seul).
- **Toute reprise de données** sur les comptes existants (renommage, déduplication d'e-mail) — sans
  objet sous D8.
- **Menu déroulant « Actions »** pour les boutons de ligne : le lot V3 fixait son seuil d'éligibilité
  à plus de deux actions, et `/users` en a exactement trois — l'option est donc techniquement
  ouverte. **Écartée explicitement ici** : elle ajoute un clic sur « Modifier », l'action la plus
  fréquente, alors que l'alignement horizontal (D7) résout déjà le problème de hauteur. Choix
  documenté, pas oubli.
- **Politique de mot de passe, envoi d'e-mail, lockout, 2FA** — hors périmètre depuis le lot 045,
  inchangé.
- **Toute modification du mécanisme de mot de passe temporaire** (génération, affichage unique,
  verrou `RequirePasswordChangeOnFirstLogin`) — lots 044/045/049, inchangés.
- **Généralisation de la compacité (D7) aux autres pages de liste** (`ImportProfiles.razor`,
  `ExportProfiles.razor`, `Logs.razor`) — non demandé, et la page Journaux a une décision propre
  (V5). Ce lot ne touche que `/users`.

---

## 50.0. Investigation préalable (obligatoire avant tout code)

- [x] **Conformité des 3 comptes seedés à D1 et D2 : vérifiée par Simon le 28/07, conforme.**
  Configuration `AdminSeedUsers` : `SLB`/`J2M`/`JPN` (3 caractères chacun, lettres et chiffres, dans
  le jeu autorisé) ; prénoms et noms de 4 à 13 caractères ; trois e-mails distincts (nécessaire sous
  D5 pour que le seed aboutisse sur base vierge). **Ne pas réinvestiguer ce point.**
  Deux valeurs sont à la limite et méritent attention lors de l'écriture du validateur :
  `Le Becq` (espace) et `Jean-Marie` (tiret) — cf. D2, aucune restriction de jeu de caractères sur
  les noms.
- [ ] Lire `Infrastructure/Identity/UserManagementService.cs` : signatures actuelles de
  `CreateUserAsync`/`UpdateUserAsync`, et point exact où `UserName` est dérivé de l'e-mail.
- [ ] Relever la configuration Identity réelle (`Program.cs` de `BlazorAdmin` **et de tout autre
  hôte** enregistrant Identity) : `User.RequireUniqueEmail`, `User.AllowedUserNameCharacters`.
  **Si Identity est configuré à plus d'un endroit, D1 et D5 doivent être appliqués partout** — une
  option divergente entre deux hôtes est un piège silencieux.
- [ ] **Vérifier si une page `/Account/Register` est atteignable** (le lot 049 en mentionne la route
  dans le binaire compilé). Le gabarit Blazor Identity standard y pose `UserName = Email`, ce qui
  devient invalide sous D1. **Signaler le constat sans le corriger** : une inscription publique sur
  une application d'administration est une question de sécurité à arbitrer par Simon, pas un défaut
  de format à rustiner.
- [ ] Lire `IdentitySeeder` : confirmer la recherche par nom (`FindByNameAsync`) et le comportement
  exact si `CreateAsync` échoue (avertissement Serilog attendu, démarrage poursuivi). Confirmer que
  ce comportement est bien celui décrit dans « Risque principal ».
- [ ] Lire `LocalizedIdentityErrorDescriber` : confirmer que `DuplicateUserName`, `DuplicateEmail` et
  `InvalidUserName` y sont surchargés et localisés ; compléter en 50.11 si l'un manque.
- [ ] Lire `Users.razor` : relever (a) comment l'ensemble des comptes Admin est récupéré pour la
  règle « dernier Admin non supprimable » (44.3) — **à réutiliser en 50.8**, jamais un
  `IsInRoleAsync` par ligne ; (b) la structure exacte des deux gabarits V2 et le test qui verrouille
  leur identité de contenu ; (c) la disposition actuelle des trois boutons d'action de ligne
  (empilement vertical constaté) et sa cause — classe CSS explicite ou repli faute de largeur.
- [ ] Relever la nullabilité et le type actuel de `FirstName`/`LastName` sur `ApplicationUser`
  (probablement `nvarchar(max)`, aucune longueur configurée) — conditionne 50.5.
- [ ] Confirmer dans `Login.razor` que le champ de connexion est le nom d'utilisateur
  (`PasswordSignInAsync` prend un `userName`), pas l'e-mail.
- [ ] Vérifier, dans le SDK .NET 10 réellement installé, si `UserManager.SetUserNameAsync` persiste
  immédiatement (appel interne à `UpdateUserAsync`). **Si oui, ne pas l'utiliser en 50.3** — lui
  préférer l'affectation directe des propriétés suivie d'un **unique** `UpdateAsync`, pour qu'un
  champ invalide ne puisse pas laisser un nom d'utilisateur déjà écrit en base. Documenter la
  conclusion ici avant d'écrire les tests de 50.3.

**Effort** : réflexion approfondie justifiée sur la conformité du seed, la page `Register` et
l'atomicité de la mise à jour. Le reste est de la lecture.

---

## 50.1. Validation serveur — format du nom d'utilisateur et longueurs (D1, D2)

**Comportement attendu** : les règles D1 et D2 sont portées par le **point d'extension prévu par
Identity**, jamais par du code de page, et s'appliquent donc automatiquement à toute création ou
modification, y compris celles du seeder.

- **Jeu de caractères** : `IdentityOptions.User.AllowedUserNameCharacters` fixé aux lettres
  (minuscules et majuscules), chiffres, `_` et `.`. Mécanisme natif, aucun code custom.
- **Longueurs** : nouveau `ApplicationUserValidator : IUserValidator<ApplicationUser>`
  (`Infrastructure/Identity/`), enregistré via `AddUserValidator<ApplicationUserValidator>()`,
  vérifiant `UserName` (3–30), `FirstName` (2–50) et `LastName` (2–50). Retourne des `IdentityError`
  à codes explicites et messages localisés, agrégés par Identity avec les erreurs natives.
- **Un seul validateur** pour les trois champs : un seul point d'extension, appelé une fois par
  opération.

**Tests** (xUnit, validateur testé directement — inutile de traverser l'UI) :
- **Les valeurs de seed satisfont le validateur.** Test alimenté par les valeurs réelles utilisées
  par `IdentitySeeder`, pas par des valeurs recopiées à la main dans le test — sans quoi il
  cesserait de protéger dès que le seed change. C'est le garde-fou permanent contre le verrouillage
  administrateur décrit en tête de document.
- Nom d'utilisateur de 2 caractères → erreur ; 3 → accepté ; 30 → accepté ; 31 → erreur. **Les
  bornes exactes sont testées**, pas seulement « trop court / trop long ».
- Prénom de 1 caractère → erreur ; 2 → accepté ; 50 → accepté ; 51 → erreur. Idem Nom.
- **Prénoms et noms contenant espace, tiret ou apostrophe acceptés** : `Le Becq`, `Jean-Marie`,
  `O'Brien`, `N'Diaye`. Test non facultatif — il verrouille D2 contre une extension par symétrie de
  la règle de jeu de caractères de D1, qui rejetterait deux des trois comptes seedés.
- Plusieurs champs invalides simultanément → **toutes** les erreurs retournées, pas seulement la
  première (un admin doit voir tout ce qui bloque en une fois).
- Cas nominal → succès, aucune erreur.
- **Test du jeu de caractères via Identity, pas via le validateur** : un nom d'utilisateur contenant
  `@` ou `-` est refusé par `CreateAsync` (test d'intégration Identity + EF Core InMemory), ce qui
  prouve que l'option est réellement câblée et pas seulement déclarée.

---

## 50.2. Backend — nom d'utilisateur explicite à la création

**Comportement attendu** : `CreateUserAsync` reçoit le nom d'utilisateur en **paramètre explicite**
et ne le dérive plus jamais de l'e-mail. Signature cible :
`CreateUserAsync(string userName, string email, string firstName, string lastName)`.

- Compte créé avec `UserName` et `Email` **indépendants**.
- Aucun rôle assigné ; `RequirePasswordChangeOnFirstLogin = true` ; mot de passe temporaire généré et
  retourné une seule fois (lot 044, inchangé).
- En cas d'échec Identity (nom pris, format invalide, longueur invalide, e-mail pris), la méthode
  retourne un **résultat d'échec explicite portant les erreurs Identity localisées**, jamais une
  exception, et **aucun compte n'est créé**.

**Tests** (Moq sur `UserManager<ApplicationUser>`) :
- Création nominale → l'utilisateur passé à `CreateAsync` porte `UserName` = valeur fournie et
  `Email` = valeur fournie, **les deux différentes**. Le test **doit** utiliser deux valeurs
  distinctes : avec des valeurs identiques il passerait aussi sur l'ancien code, donc ne serait
  jamais rouge et ne prouverait rien.
- Échec Identity (`CreateAsync` → `IdentityResult.Failed`) → résultat d'échec, erreurs remontées,
  `AddToRoleAsync` jamais appelé, aucun mot de passe retourné.
- Nom d'utilisateur vide ou blanc → échec avant tout appel à `CreateAsync`.
- Non-régression lot 044 : flag `RequirePasswordChangeOnFirstLogin` à `true`, aucun rôle assigné.

---

## 50.3. Backend — modification, avec nom d'utilisateur (D3, D4)

**Comportement attendu** :
`UpdateUserAsync(Guid userId, string userName, string email, string firstName, string lastName)`.

- Les quatre champs sont mis à jour en **une seule opération de persistance** (cf. conclusion de
  50.0) : si l'un est refusé par les validateurs, **aucun** n'est écrit.
- Si le nom d'utilisateur a **effectivement changé** (comparaison sur la valeur **normalisée**, pas
  sur la casse brute), `UpdateSecurityStampAsync` est appelé après le succès de la mise à jour (D4).
- Si le nom d'utilisateur est inchangé, `UpdateSecurityStampAsync` n'est **pas** appelé.
- Rôle et mot de passe restent hors du périmètre de cette méthode (lot 044, inchangé).

**Tests** (Moq) :
- Renommage nominal → quatre champs mis à jour, `UpdateSecurityStampAsync` appelé exactement une
  fois.
- Modification du seul prénom → mise à jour effectuée, `UpdateSecurityStampAsync` **jamais** appelé.
- Changement de casse seul (`slb` → `SLB`) → traité comme inchangé au sens de la normalisation
  Identity ; comportement **verrouillé par un test**, pas laissé implicite.
- Nom d'utilisateur déjà porté par un autre compte → échec, erreurs remontées,
  `UpdateSecurityStampAsync` jamais appelé.
- Échec de validation → **aucun** champ persisté (vérifié par le nombre d'appels de persistance sur
  le mock, pas par une inspection d'état).

---

## 50.4. Unicité applicative de l'e-mail (D5, volet Identity)

**Comportement attendu** : `IdentityOptions.User.RequireUniqueEmail = true`, appliqué à **tous** les
points d'enregistrement d'Identity relevés en 50.0. Création et modification avec un e-mail déjà
utilisé échouent avec l'erreur `DuplicateEmail`, localisée par `LocalizedIdentityErrorDescriber`.

**Tests** (intégration Identity + EF Core InMemory — c'est une option du framework qu'on veut voir
agir réellement, pas un mock) :
- Création d'un second compte avec un e-mail déjà utilisé → échec, un seul compte en base.
- Modification d'un compte vers l'e-mail d'un autre compte → échec.
- **Modification d'un compte en conservant son propre e-mail → succès.** Test non facultatif : c'est
  le faux positif classique de `RequireUniqueEmail` (un compte détecté comme doublon de lui-même),
  et son absence rendrait toute modification d'utilisateur impossible en production.
- Deux e-mails ne différant que par la casse → traités comme doublons (normalisation Identity).
- **Les e-mails des 3 comptes seedés sont distincts entre eux** — sans quoi le seed lui-même
  échouerait partiellement au démarrage sur base vierge (même classe de risque que 50.1).

---

## 50.5. Migration EF Core — index unique filtré et longueurs de colonnes (D5, volet schéma)

**Comportement attendu** : une **seule** migration Identity, nommée selon la convention en place
(`yyyyMMddHHmmss_AddUniqueEmailIndexAndNameLengthsToApplicationUser`), portant deux changements
regroupés :

1. **Index unique filtré** sur `NormalizedEmail`, remplaçant l'index non unique `EmailIndex` créé par
   défaut par Identity. Le filtre `WHERE [NormalizedEmail] IS NOT NULL` **n'est pas optionnel** :
   sur SQL Server, un index unique considère plusieurs `NULL` comme des valeurs égales, donc deux
   comptes sans e-mail violeraient un index non filtré.
2. **`HasMaxLength(50)`** sur `FirstName` et `LastName` (aujourd'hui sans longueur configurée, donc
   `nvarchar(max)`). Filet de sécurité de schéma, **pas** duplication de la règle D2 : la règle
   utilisateur (2–50, message localisé) vit en 50.1 et elle seule est visible de l'administrateur.

Configuration en Fluent API dans le `OnModelCreating` du contexte Identity, **après** l'appel à
`base.OnModelCreating` (sans quoi la définition par défaut d'Identity écraserait la nôtre).

**Tests** :
- Test de modèle EF Core vérifiant que l'index sur `NormalizedEmail` est déclaré unique et que les
  deux propriétés portent une longueur maximale de 50.
- **Limite explicite à documenter, pas à contourner** : le provider InMemory n'applique ni index ni
  filtre SQL. L'efficacité réelle de l'index unique et de sa clause de filtrage **ne peut être
  vérifiée que contre un vrai SQL Server**, lors de l'essai à blanc. Ne pas écrire un test InMemory
  qui prétendrait le prouver.

---

## 50.6. UI — champ « Nom d'utilisateur » dans les formulaires

**Comportement attendu**, sur `Users.razor`, dans les deux formulaires (création et modification),
**en première position** puisqu'il s'agit de l'identifiant de connexion :

- Champ `#user-username-input`, obligatoire, libellé localisé.
- Formulaire de modification **pré-rempli** avec le nom d'utilisateur courant.
- Validation client : **présence uniquement**. Longueur, jeu de caractères et unicité **ne sont pas
  dupliqués côté client** (convention lot F) — ils remontent du serveur via les erreurs Identity.
- Un texte d'aide court sous le champ énonce les règles de forme (3 à 30 caractères ; lettres,
  chiffres, `_` et `.`) et précise qu'il s'agit de l'identifiant de connexion. Texte **informatif**,
  pas une validation : l'information n'est déductible d'aucun autre élément de la page, et un admin
  ne doit pas découvrir la règle en essuyant un refus.
- Les erreurs serveur s'affichent dans le bandeau du formulaire, `role="alert"`, **sans fermer le
  formulaire ni perdre la saisie**.

**Tests** (bUnit) :
- Création : la valeur de `#user-username-input` est celle passée au service mocké (assertion sur
  l'argument exact, avec un nom d'utilisateur **différent** de l'e-mail saisi).
- Champ vide à la soumission → message de validation, service **jamais** appelé.
- Modification : `#user-username-input` pré-rempli avec la valeur du compte édité.
- Service retournant un échec (`DuplicateUserName`) → message affiché, formulaire toujours ouvert,
  saisie conservée, liste non rechargée.
- Non-régression lot 044 : après création réussie, `#temporary-password-display` reste affiché et la
  liste est rechargée.

---

## 50.7. UI — colonnes « Prénom » et « Nom » dans la liste (D6)

**Comportement attendu** : les deux champs, déjà saisis à la création et déjà persistés, deviennent
visibles.

- **Tableau desktop** : deux colonnes, en-têtes localisés.
- **Cartes mobiles** : les deux mêmes champs (invariant V2).
- IDs stables par ligne : `#user-firstname-{id}`, `#user-lastname-{id}`, présents dans les deux
  gabarits.

**Tests** (bUnit) :
- Prénom et Nom d'un compte donné rendus dans le tableau.
- Les mêmes valeurs rendues dans la carte correspondante.
- **Non-régression V2** : le test existant d'identité de contenu entre gabarits est **étendu** aux
  nouveaux champs, pas doublé par un nouveau test parallèle.

---

## 50.8. UI — colonne « Rôle » avec badge Admin (D6)

**Comportement attendu** : nouvelle colonne « Rôle », dans le tableau **et** dans les cartes.

- Compte Admin : badge `#user-role-badge-{id}` portant le **texte** localisé « Admin ».
- Compte standard : même emplacement, texte localisé « Utilisateur », sans badge. Pas de cellule vide
  ni de tiret : un lecteur d'écran doit énoncer le statut de chaque ligne, et la couleur du badge ne
  peut pas être le seul porteur de l'information (WCAG 1.4.1).
- **Purement lecture.** Aucun contrôle, aucun lien, aucune action — le changement de rôle depuis
  l'UI reste hors périmètre (lot 044).
- **Source de données** : l'ensemble des comptes Admin déjà récupéré pour la règle « dernier Admin »
  (44.3), réutilisé tel quel.

**Tests** (bUnit) :
- Ligne d'un compte Admin → `#user-role-badge-{id}` présent avec le libellé Admin.
- Ligne d'un compte sans rôle → libellé standard rendu au même emplacement.
- Cartes mobiles → même statut que la ligne de tableau correspondante (invariant V2 étendu).
- **Aucune requête de rôle par ligne** : le mock de récupération des Admin est appelé **exactement
  une fois** pour un rendu de N lignes (N ≥ 3). Ce test protège contre une régression N+1 qui ne se
  verrait jamais autrement.
- Non-régression 44.3 : les boutons de suppression désactivés (auto-suppression, dernier Admin)
  conservent exactement leur comportement.

---

## 50.9. UI — compacité du tableau desktop (D7)

**Constat** : avant même l'ajout des trois colonnes de 50.7/50.8, le tableau desktop déborde
horizontalement (barre de défilement visible) et chaque ligne fait environ quatre fois sa hauteur
utile. Deux causes distinctes, traitées séparément :

1. **Colonne `Id` retirée** de l'affichage, dans le tableau **et** dans les cartes. Un GUID complet
   est la colonne la plus large, se replie sur quatre lignes, et n'est actionnable par personne. Les
   identifiants restent portés par les IDs HTML des boutons de ligne (`#edit-user-button-{id}`,
   etc.), donc **aucun test ne dépend de son affichage** — vérifier néanmoins qu'aucun test existant
   n'assertait sa présence textuelle, et corriger le cas échéant plutôt que de conserver la colonne
   pour faire passer un test.
2. **Trois boutons d'action alignés horizontalement** au lieu d'empilés, avec un conteneur empêchant
   le repli (`text-nowrap` ou équivalent). C'est la cause principale de la hauteur de ligne. Le
   lot V3 avait déjà tranché ce point (« icônes côte à côte ») mais pour les pages Profils :
   `/users`, créée au lot 044, n'en a jamais bénéficié. Les icônes, `aria-label` et `title` existants
   sont **conservés à l'identique** — seule la disposition change.
3. **Tableau en `table-sm`** (padding réduit), sans autre modification de style.

Sur les cartes mobiles, la disposition des actions reste celle décidée en V3 — ce point ne concerne
que le gabarit desktop.

**Tests** (bUnit) :
- La colonne `Id` n'est plus rendue : aucun élément d'en-tête ni de cellule ne la porte, dans aucun
  des deux gabarits.
- Le conteneur des trois boutons d'action porte la classe d'alignement horizontal attendue
  (assertion sur la classe, pas sur un rendu visuel — bUnit ne calcule pas de layout).
- Le `<table>` porte `table-sm`.
- **Non-régression fonctionnelle** : les trois boutons conservent leurs IDs, leurs `aria-label`, et
  déclenchent exactement les mêmes actions qu'avant. Réutiliser les tests fonctionnels existants du
  lot 044, ne pas les dupliquer.

---

## 50.10. Non-régression bout en bout — la connexion fonctionne avec le nom d'utilisateur défini

**Leçon du lot 049, appliquée ici** : aucun test bUnit ne prouve qu'une identité de connexion
fonctionne réellement. Ce lot modifie ce avec quoi les utilisateurs se connectent ; il lui faut donc
au moins un test HTTP réel.

**Test** (`WebApplicationFactory<Program>`, projet `ExcelETL.BlazorAdmin.Tests`, un seul test
enchaînant les étapes) :
- Compte créé via `UserManagementService` avec un nom d'utilisateur **distinct de son e-mail**
  (ex. `TST_01` / `test@exemple.fr`).
- `POST /Account/Login` avec `TST_01` + mot de passe temporaire → connexion réussie, redirection vers
  `/Account/ForcePasswordChange` (non-régression du verrou des lots 045/049).
- `POST /Account/Login` avec `test@exemple.fr` + le même mot de passe → **échec** de connexion
  (l'e-mail n'est pas un identifiant de connexion, cf. hors périmètre).

**Contraintes** : réutiliser le câblage `WebApplicationFactory` déjà en place
(`IdentitySeeding:Enabled` et `Database:AutoMigrate` désactivés en test) — ne pas le réinventer.

---

## 50.11. Ressources de localisation (EN/FR)

**Comportement attendu** : nouvelles clés `.resx` pour le libellé et le texte d'aide du champ nom
d'utilisateur, les messages d'erreur du validateur de 50.1 (longueurs de nom d'utilisateur, prénom,
nom), les en-têtes de colonnes « Prénom », « Nom », « Rôle », et les libellés Admin/Utilisateur.
Compléter `LocalizedIdentityErrorDescriber` si 50.0 a révélé une erreur Identity non localisée.
Retirer les clés devenues inutilisées par le retrait de la colonne `Id`. Réutiliser les clés
génériques existantes plutôt que de dupliquer.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet).

---

## Ordre recommandé

1. **50.0** (investigation — la conformité du seed conditionne tout le lot)
2. **50.1** (validateur serveur — socle de tout le reste)
3. **50.2** + **50.3** (backend création et modification, livrés ensemble)
4. **50.4** (unicité applicative de l'e-mail)
5. **50.5** (migration)
6. **50.6** (formulaires UI)
7. **50.7** + **50.8** + **50.9** (colonnes et compacité — mêmes fichiers, mêmes tests d'invariant V2
   à étendre : les traiter d'un bloc évite trois passes successives sur le même markup)
8. **50.10** (non-régression HTTP — clôture du lot)
9. **50.11** (ressources, une fois les textes définitifs connus)

## Note d'efficacité d'implémentation (Claude Code)

- **50.0 contient deux points d'arrêt.** Si un compte seedé ne respecte pas D1/D2, ou si
  `/Account/Register` est atteignable, **s'arrêter et remonter le constat** — ne pas ajuster la
  règle, le seed ou la page de sa propre initiative. Le premier cas rendrait l'application
  inaccessible à son administrateur sur base vierge.
- **50.7, 50.8 et 50.9 touchent le même markup et le même test d'invariant V2.** Les traiter en une
  seule passe plutôt qu'en trois : sinon le test d'identité de contenu entre gabarits est réécrit
  trois fois de suite.
- **50.2 et 50.3 changent une signature publique** consommée par `Users.razor` : les livrer ensemble
  puis enchaîner sur 50.6, plutôt que de laisser la compilation cassée entre deux étapes.
- **Le test « e-mail inchangé sur son propre compte » (50.4)** est celui qui casse en production s'il
  est oublié. Ce n'est pas un cas limite optionnel.
- **Le test de 50.2 doit utiliser un nom d'utilisateur différent de l'e-mail.** Avec deux valeurs
  identiques il passerait aussi bien sur l'ancien code que sur le nouveau : jamais rouge, donc sans
  valeur.
- **Le test « valeurs de seed conformes » (50.1) doit lire les valeurs réelles du seed**, pas les
  recopier. Un test qui duplique les valeurs cesse de protéger dès que le seed change — c'est-à-dire
  exactement au moment où il servirait.
- **Ne pas prétendre tester l'index unique avec InMemory** (50.5) — le provider ignore les index. Un
  test vert à cet endroit serait un faux témoignage.
- Exécuter les tests en sortie minimale et filtrée :
  `dotnet test --filter FullyQualifiedName~User --verbosity quiet`.
- Effort élevé réservé à **50.0** (conformité du seed, atomicité de la mise à jour) et au refactor
  éventuel de 50.3. Les étapes red/green restent en effort standard, conformément à
  `recommandations-tickets-tdd.md` §2.
