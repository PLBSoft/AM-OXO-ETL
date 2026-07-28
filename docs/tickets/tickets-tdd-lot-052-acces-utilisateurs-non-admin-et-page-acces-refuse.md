# Tickets TDD — Lot 052 : accès des comptes non-Admin et page « Accès refusé »

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 051
(`tickets-tdd-lot-051-retrait-inscription-publique.md`).*

**Origine (constat terrain Simon, 28/07)** — premier compte non-Admin jamais créé dans
l'application (`JAD`, via `/users`). Parcours réel observé :

1. Connexion avec le mot de passe temporaire → changement forcé (lots 045/049) → **succès**.
2. Redirection vers `/`, qui est la seconde route de `ImportProfiles.razor`, réservée aux Admin.
3. Autorisation refusée → redirection vers `/Account/AccessDenied?ReturnUrl=%2F`.
4. Cette page **n'existe pas** → le mécanisme d'erreur générique affiche **« Introuvable — Désolé,
   le contenu que vous recherchez n'existe pas. »**

**Deux défauts distincts, de gravité inégale.**

**Défaut A — la page `AccessDenied` n'existe pas.** Un refus d'autorisation affiche un message de
ressource inexistante. C'est faux : la ressource existe, c'est l'accès qui est refusé. Même classe
de problème qu'au lot 049.

**Défaut B, le principal — un compte non-Admin n'a accès à rien.** Toutes les pages fonctionnelles
sont aujourd'hui réservées aux Admin. Il ne reste à un tel compte que sa fiche de profil et la
déconnexion. Et comme aucune promotion de rôle n'est possible depuis l'interface (lot 044,
confirmé), cet état est définitif.

**Conséquence métier** : la fonctionnalité du lot 044 ne remplit pas le besoin qui l'a fait naître.
Le client voulait que l'administrateur crée des comptes pour ses collègues **afin de ne plus
partager ses propres identifiants**. Aujourd'hui, l'administrateur crée des comptes qui n'ouvrent
aucune porte, donc le partage d'identifiants reste la seule voie praticable. Ce lot est ce qui rend
le lot 044 réellement utile.

**Effet secondaire positif déjà acquis** : ce parcours a validé empiriquement la posture de sécurité
supposée au lot 051 — un compte authentifié sans rôle est bien refoulé des pages Admin, et la barre
latérale ne lui propose que ce à quoi il a droit. Les mécanismes fonctionnent ; c'est leur réglage
qui est faux.

---

## Décisions actées avec Simon (28/07)

Elles sont consignées dans **`convention-autorisation-pages-blazoradmin.md`**, document vivant créé
pour ce lot. **Ce ticket ne les reformule pas : il les applique.** Lire la convention avant de
commencer, elle fait autorité en cas de divergence avec ce ticket.

Rappel des trois points structurants :

- **Deux niveaux, binaire, définitifs** : Admin (comptes du seed uniquement) et Authentifié. Pas de
  troisième rôle, pas d'attribution de rôle depuis l'interface, pas de permission intermédiaire de
  type lecture seule.
- **Admin gouverne l'administration de l'application, pas son usage** : `/users` et `/logs` restent
  Admin ; **toutes les autres pages fonctionnelles passent à Authentifié.**
- **Accès complet, éditeurs compris** : un compte authentifié qui accède à une page métier y accède
  pleinement, création, modification et suppression incluses. Distinguer lecture et écriture
  reviendrait à créer un second rôle sous forme de règle d'affichage.

---

## Conventions déjà en place à respecter

- **`convention-autorisation-pages-blazoradmin.md`** — référence de ce lot, en particulier §3 (deux
  couches obligatoires) et §5 (piège de la redirection post-connexion).
- **Leçon des lots 049 et 051** : un test bUnit ne prouve jamais qu'une route est atteignable ou
  refusée. Toute assertion d'autorisation exige un test `WebApplicationFactory` faisant une vraie
  requête HTTP. bUnit ne sert qu'à la visibilité des liens.
- Pages de compte en rendu SSR statique (`[ExcludeFromInteractiveRouting]` via
  `Components/Account/_Imports.razor`, lot 049) — toute nouvelle page `Account/` suit ce mode.
- `NavMenu.razor` : ordre des liens acté au **Lot S2**, non modifié par ce lot — seuls les blocs
  `<AuthorizeView>` qui les encadrent changent.
- IDs stables sur tout élément interactif ; jamais de sélection par texte ou position en bUnit.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit.
- Localisation EN/FR via `.resx`.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite de ce lot

- **Tout nouveau rôle**, toute permission intermédiaire, toute attribution de rôle depuis
  l'interface — écartés explicitement le 28/07, cf. convention §1.
- **Masquage ou désactivation sélective de boutons d'action** (« Créer », « Modifier »,
  « Dupliquer », « Supprimer ») selon le rôle : contraire à la décision d'accès complet. Les pages
  métier se comportent **strictement à l'identique** pour un Admin et pour un compte sans rôle.
- **Modification du seed** ou de la liste des comptes Admin.
- **Modification du parcours de mot de passe temporaire** (lots 044/045/049).
- **Refonte de `NavMenu.razor`** au-delà du déplacement de liens entre blocs `<AuthorizeView>`.
- **Page « Introuvable »** et mécanisme `UseStatusCodePagesWithReExecute` — inchangés ; ce lot ajoute
  une sortie distincte, il ne touche pas à l'existante.
- **Journalisation des refus d'accès** — non demandé.

---

## 52.0. Investigation préalable (obligatoire avant tout code)

- [ ] **Dresser l'inventaire exhaustif des composants routables** de `BlazorAdmin` : pour chaque
  route, relever l'attribut d'autorisation réellement présent (`[Authorize]`, `[Authorize(Roles =
  ...)]`, `[AllowAnonymous]`, ou **aucun**). Cet inventaire est le livrable central de 52.0 : il
  alimente 52.1 et sert à compléter le tableau §2 de la convention, dont les routes des pages de
  test sont aujourd'hui approximatives.
- [ ] **Lever une contradiction documentaire sur `/logs`.** `audit-design-blazoradmin-2026-07-27.md`
  §5.1 décrit le lien Journaux dans un bloc `<AuthorizeView>` **sans rôle**, alors que
  `etat-des-lieux-technique-2026-07-27.md` indique la restriction `#nav-logs-link` livrée et testée
  au lot 44.4 — et que l'observation du 28/07 montre le lien **absent** pour le compte `JAD`. Le
  code fait foi : établir la structure réelle et **le dire explicitement dans le rapport**, sans
  supposer laquelle des deux sources est à jour.
- [ ] **Relever la configuration de `AccessDeniedPath`** : valeur par défaut d'Identity ou réglage
  explicite dans `Program.cs`. Conditionne la route exacte de la page créée en 52.3.
- [ ] Relever la **cible de redirection après changement de mot de passe forcé** et après connexion
  (`ReturnUrl` par défaut). Confirme le mécanisme du défaut observé et sécurise le test de 52.4.
- [ ] Relever la structure exacte des blocs `<AuthorizeView>` de `NavMenu.razor` et **les tests
  existants qui la verrouillent** (lots L2, S2, 051) : ils devront être **corrigés**, leur intention
  s'inversant pour quatre liens.
- [ ] Confirmer que la `FallbackPolicy` globale (découverte au lot 051) exige l'**authentification**
  et non un rôle — c'est elle qui produit la branche « non authentifié → Login ».

**Effort** : réflexion approfondie sur l'inventaire et la contradiction `/logs`. Le reste est de la
lecture.

---

## 52.1. Autorisation des pages — couche réelle

**Comportement attendu**, conformément au tableau §2 de la convention :

- Passent à **Authentifié** (attribut `[Authorize]` sans rôle) : `/`, `/import-profiles` et ses
  routes d'édition, `/export-profiles` et ses routes d'édition, les pages de test, la page des
  fichiers générés, `/profile`.
- Restent **Admin** (`[Authorize(Roles = IdentitySeeder.AdminRoleName)]`) : `/users`, `/logs`.
- **Chaque route porte son attribut explicitement**, y compris quand il coïncide avec la
  `FallbackPolicy` (convention §4) : ce qui n'est pas écrit n'est pas une décision.

**Tests** (`WebApplicationFactory<Program>`, requêtes HTTP réelles — seule couche probante) :
- Compte **sans rôle** authentifié → `GET` sur chacune des routes métier → **200**. Une assertion
  par route, pas un test global : un échec doit désigner la route fautive.
- Compte **sans rôle** → `GET /users` et `GET /logs` → redirection vers `/Account/AccessDenied`.
- Compte **Admin** → `GET` sur **toutes** les routes, métier et administration → **200**. Non-
  régression : ce lot élargit un accès, il n'en retire aucun.
- **Non authentifié** → `GET` sur une route métier → redirection vers `/Account/Login`
  (non-régression de la `FallbackPolicy`, comportement établi au lot 051).

---

## 52.2. Visibilité des liens de navigation

**Comportement attendu** : `NavMenu.razor` reflète exactement les niveaux de 52.1. Les liens
Profils d'import, Profils d'export, Test API et Fichiers générés quittent le bloc réservé aux Admin
pour un bloc `<AuthorizeView>` sans rôle. Les liens Utilisateurs et Journaux restent dans le bloc
`Roles="@IdentitySeeder.AdminRoleName"`. **Aucun lien n'est déplacé dans l'ordre d'affichage**
(Lot S2).

**Tests** (bUnit — visibilité uniquement, jamais l'autorisation) :
- Utilisateur **sans rôle** → `#nav-import-profiles-link`, `#nav-export-profiles-link`,
  `#nav-api-test-link`, `#nav-generated-files-link`, `#nav-profile-link` et le bouton de déconnexion
  **présents**.
- Utilisateur **sans rôle** → `#nav-users-link` et `#nav-logs-link` **absents du DOM** (absence
  réelle, pas `hidden` ni `disabled` — exigence posée au Lot L2).
- Utilisateur **Admin** → tous les liens présents.
- Utilisateur **non authentifié** → seul `#nav-login-link`, exactement une fois (non-régression du
  test d'unicité du Lot L2, à conserver tel quel).
- **Corriger les tests existants** dont l'intention s'inverse pour les quatre liens déplacés — ne
  pas en ajouter de nouveaux à côté des anciens.

---

## 52.3. Page « Accès refusé »

**Comportement attendu** : création de la page à la route relevée en 52.0
(`/Account/AccessDenied` sauf découverte contraire), dans `Components/Account/Pages/`, en rendu SSR
statique comme ses voisines.

- Titre et message localisés annonçant un **refus d'accès**, jamais une ressource inexistante. Le
  message indique que le compte ne dispose pas des droits nécessaires et invite à contacter un
  administrateur — même logique que le texte de recours du lot 51.3, et cohérente avec le fait
  qu'aucune promotion n'est possible depuis l'interface.
- Un lien de retour vers `/`, désormais accessible à tout compte authentifié (convention §5).
  **Vérifier explicitement l'absence de boucle** : le lien ne doit pas ramener vers une page qui
  refuserait à nouveau l'accès.
- ID stable sur le conteneur (`#access-denied-message`) et sur le lien de retour.
- Accessible **sans être authentifié** : la page peut être atteinte dans des cas limites, elle ne
  doit jamais elle-même déclencher un refus.

**Tests** :
- (`WebApplicationFactory`) `GET /Account/AccessDenied` → **200**, contenu du message de refus,
  authentifié comme non authentifié.
- (`WebApplicationFactory`) Compte sans rôle → `GET /users` → redirection suivie → page de refus,
  et **non** la page « Introuvable ». C'est le test qui distingue les deux sorties ; il doit être
  écrit **avant** la création de la page et vérifié **rouge**.
- Le lien de retour pointe vers une route accessible à un compte authentifié sans rôle.

---

## 52.4. Non-régression bout en bout — le parcours réel du 28/07

**Comportement attendu** : le parcours exact qui a produit le défaut fonctionne de bout en bout.

**Test** (`WebApplicationFactory`, un seul test enchaînant les étapes) :
- Un Admin crée un compte sans rôle (`RequirePasswordChangeOnFirstLogin = true`).
- Connexion avec le nom d'utilisateur et le mot de passe temporaire → redirection vers
  `/Account/ForcePasswordChange` (non-régression lots 045/049).
- Soumission d'un nouveau mot de passe valide → flag levé → redirection vers `/`.
- `GET /` → **200**, page des profils d'import. **C'est l'assertion qui capture le défaut d'origine**
  et interdit sa réapparition.
- Dans la foulée, `GET /users` avec la même session → page de refus.

**Contraintes** : réutiliser le câblage `WebApplicationFactory` existant. Si un test de parcours
équivalent existe déjà (clôture du lot 049), **l'étendre plutôt que le dupliquer**.

---

## 52.5. Ressources de localisation (EN/FR)

Nouvelles clés pour le titre, le message et le lien de la page de refus. Réutiliser les clés
génériques existantes plutôt que de dupliquer. Aucune clé orpheline laissée derrière.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet).

---

## 52.6. Mise à jour de la convention

`convention-autorisation-pages-blazoradmin.md` §2 est complété avec les **routes exactes** relevées
en 52.0 (pages de test et fichiers générés, aujourd'hui approximatives dans le tableau). Document
vivant : mise à jour **en place**, aucun historique de version ajouté à l'intérieur.

---

## Ordre recommandé

1. **52.0** (inventaire — alimente tout le reste)
2. **52.3** (page de refus : test rouge d'abord, puis création — indépendante de 52.1)
3. **52.1** (autorisation des pages)
4. **52.2** (visibilité des liens)
5. **52.4** (parcours bout en bout — clôture)
6. **52.5** puis **52.6** (ressources, puis convention complétée des routes réelles)

## Note d'efficacité d'implémentation (Claude Code)

- **52.3 avant 52.1.** Tant que la page de refus n'existe pas, les tests de 52.1 sur `/users` et
  `/logs` observeraient une redirection vers une page « Introuvable » et devraient être réécrits
  ensuite. La créer d'abord évite une double passe.
- **Ne jamais tester une autorisation avec bUnit.** bUnit rend un composant en isolation, il
  n'exerce ni la chaîne d'autorisation ni le middleware. Un test bUnit vert sur `/users` ne prouve
  rien du tout. Deux couches, deux types de test (convention §3).
- **L'inventaire de 52.0 doit être exhaustif avant de modifier quoi que ce soit.** Une route métier
  oubliée reste inaccessible aux non-Admin et reproduit exactement le défaut d'origine, en plus
  discret.
- **Les tests de NavMenu existants se corrigent, ils ne se doublent pas.** Quatre liens changent de
  bloc : leur intention s'inverse. Ajouter de nouveaux tests à côté des anciens laisserait des tests
  contradictoires dans la suite.
- **Ce lot n'élargit pas les droits des Admin.** Toute assertion existante sur un accès Admin doit
  rester verte sans modification ; si l'une casse, c'est le signe d'une erreur d'attribut, pas d'un
  test à ajuster.
- Exécuter les tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~Authorization|FullyQualifiedName~NavMenu|FullyQualifiedName~AccessDenied" --verbosity quiet`.
- Effort élevé sur **52.0** uniquement. Le reste est de l'application mécanique d'une décision
  déjà prise.
