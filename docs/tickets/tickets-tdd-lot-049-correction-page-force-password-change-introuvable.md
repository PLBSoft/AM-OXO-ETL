# Tickets TDD — Lot 049 : correction de la page `/Account/ForcePasswordChange` « Introuvable »

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le
lot 048 (`tickets-tdd-lot-048-edition-blazor-regles-entete-profil.md`).*

**Origine (constat terrain Simon, 27/07, après livraison du lot 045)** : le verrou de mot de passe
temporaire fonctionne côté redirection mais sa page cible est inatteignable. Parcours reproduit :

1. `/Account/Login` s'affiche normalement, saisie username + mot de passe temporaire, « Se connecter ».
2. Redirection effective vers `https://localhost:7013/Account/ForcePasswordChange` (45.3 : OK).
3. **La page affiche « Introuvable / Désolé, le contenu que vous recherchez n'existe pas. »** — le
   formulaire de changement de mot de passe n'apparaît jamais.
4. Toute navigation vers une autre page est ramenée là (45.4 : garde OK), seule la déconnexion
   fonctionne. L'utilisateur est enfermé dans une boucle connexion / déconnexion.

**Gravité** : bloquant absolu pour le go-live. Le lot 045 était identifié comme *le seul point
réellement bloquant avant déploiement* ; en l'état il est pire qu'avant sa livraison, puisqu'un
compte créé par un admin (donc porteur de `RequirePasswordChangeOnFirstLogin = true`) ne peut plus
utiliser l'application du tout. **Aucun compte utilisateur ne doit être créé en production avant la
clôture de ce lot.**

**Déblocage local en attendant** (dev uniquement, ne pas industrialiser) :

```sql
UPDATE AspNetUsers SET RequirePasswordChangeOnFirstLogin = 0 WHERE UserName = 'SLB';
```

---

## Constats déjà établis — ne pas les réinvestiguer

Ce travail a été fait ; le repartir de zéro est du gaspillage de contexte.

- `src/ExcelETL.BlazorAdmin/Components/Account/Pages/ForcePasswordChange.razor` **existe**, déclare
  bien `@page "/Account/ForcePasswordChange"` (ligne 1) et `@attribute [Authorize]` (ligne 2), avec
  tous les IDs stables exigés par 45.2 (`#force-password-change-form`, `#current-password-input`,
  `#new-password-input`, `#confirm-password-input`, `#force-password-change-submit`).
- Le binaire réellement exécuté (`bin/Debug/net10.0/ExcelETL.BlazorAdmin.dll`, compilé 87 s avant la
  reproduction) **contient bien le template de route** `/Account/ForcePasswordChange`, au même titre
  que `/Account/Login` et `/Account/Register`. Ce n'est **ni** un fichier manquant, **ni** un
  `@page` absent, **ni** un build obsolète.
- Le rendu « Introuvable » provient de `Components/Pages/NotFound.razor` (`@page "/not-found"`),
  atteint soit par le `NotFoundPage` du `<Router>` (`Components/Routes.razor` ligne 2), soit par
  `app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)`
  (`Program.cs` ligne 185) — ce second mécanisme **conserve l'URL d'origine dans la barre
  d'adresse**, ce qui explique l'URL `/Account/ForcePasswordChange` affichée avec un contenu
  NotFound.
- Indice à expliquer par le diagnostic : le **titre d'onglet** de la capture est
  « Changer votre mot de passe temporaire » (donc `ForcePasswordChange_Title`, posé par le
  `<PageTitle>` de la vraie page) alors que le **corps** est celui de NotFound, qui ne définit
  aucun `<PageTitle>`. Le `<head>` de la vraie page a donc été produit à un moment ou à un autre.
- `App.razor` (ligne 19) applique `<Routes @rendermode="InteractiveServer" />`
  **inconditionnellement**. La chaîne `AcquireRenderModeForPage` est **absente du binaire compilé** :
  la moitié « côté App.razor » du mécanisme `[ExcludeFromInteractiveRouting]` (attribut posé dans
  `Components/Account/_Imports.razor`) n'est pas implémentée.
- `ExcelETL.BlazorAdmin.csproj` porte `<BlazorDisableThrowNavigationException>true</…>` : en rendu
  SSR statique, `NavigationManager.NavigateTo` **ne lève pas** et l'exécution continue après
  l'appel. C'est probablement ce qui empêche une boucle de redirection infinie de se substituer au
  symptôme observé.

---

## Hors périmètre explicite de ce lot

- Toute modification des règles métier du verrou (quand le flag est posé, quand il est levé) —
  décisions du lot 044/045, **non rouvertes**.
- Toute refonte de l'ergonomie de la page de changement forcé (textes, disposition, masquage du
  NavMenu pendant le verrou) — le lot corrige l'accessibilité technique de la page, pas son design.
- Suppression ou modification de `BlazorDisableThrowNavigationException` : à ne toucher **que si**
  le diagnostic 49.0 démontre qu'il est causal. Sinon, laisser tel quel et l'inscrire comme dette
  connue dans le prochain état des lieux.
- Politique de mot de passe, e-mail, lockout, 2FA — déjà hors périmètre au lot 045, inchangé.
- Toute réécriture de `UseStatusCodePagesWithReExecute` en dehors de ce que 49.4 exige.

---

## 49.0. Reproduction et diagnostic (obligatoire avant tout code)

**Le lot 045 a échoué précisément parce que la couche routage/HTTP n'a jamais été exercée.** Ne pas
écrire une ligne de correctif avant d'avoir tranché entre les deux branches ci-dessous, et
documenter la conclusion dans ce document avant de passer à 49.2.

- [ ] Reproduire : lancer BlazorAdmin, se connecter avec un compte portant
      `RequirePasswordChangeOnFirstLogin = true`, atterrir sur la page « Introuvable ».
- [ ] **Test discriminant n°1 — source brut de la page** (Ctrl+U / `view-source:`) : le HTML renvoyé
      par le serveur contient-il `force-password-change-form` ?
  - **Branche A — oui, le formulaire est dans le HTML brut** : le serveur renvoie la bonne page,
    c'est le **routeur interactif côté client** qui la remplace ensuite. Cause : `App.razor`
    n'honore pas `[ExcludeFromInteractiveRouting]`, la page est donc exclue de la table de routes
    interactive tout en recevant un mode de rendu interactif. → correctif 49.2.
  - **Branche B — non, le HTML brut contient déjà « Introuvable »** : le serveur répond un code
    d'erreur nu que `UseStatusCodePagesWithReExecute` transforme en page NotFound. → relever le
    **code de statut HTTP exact** de la requête document (F12 → Réseau) et la sortie de la fenêtre
    Sortie de Visual Studio / console Serilog pendant la requête, puis écrire le correctif
    correspondant en 49.3.
- [ ] Vérifier au passage si `/Account/Login` et `/Account/Register` présentent le même défaut de
      façon latente (elles portent le même attribut `[ExcludeFromInteractiveRouting]`) : si oui, le
      correctif 49.2 est requis quelle que soit la branche retenue.
- [ ] Documenter la branche retenue, le code de statut observé et le mécanisme exact **dans ce
      fichier**, section « Conclusion du diagnostic 49.0 » à créer sous ce ticket.

**Effort** : réflexion approfondie justifiée ici (c'est la seule étape du lot où le raisonnement
compte réellement, cf. `recommandations-tickets-tdd.md` §2).

---

## Conclusion du diagnostic 49.0 (28/07)

**Branche retenue : A.** Le serveur renvoie la bonne page ; c'est le routeur interactif côté client
qui la remplace. Reproduit non pas au navigateur mais par une vraie requête HTTP
(`WebApplicationFactory<Program>`, environnement `Development`, contextes EF basculés en InMemory,
utilisateur créé via `UserManager` avec `RequirePasswordChangeOnFirstLogin = true`) :

| Étape | Observé |
| --- | --- |
| `GET /Account/Login` | `200` |
| `POST /Account/Login` (mot de passe temporaire) | `302` → `http://localhost/Account/ForcePasswordChange` (45.3 : OK) |
| `GET /Account/ForcePasswordChange` | **`200`**, corps contenant `force-password-change-form` |

**Aucun code d'erreur HTTP n'est en jeu** : pas de 404, pas de 500,
`UseStatusCodePagesWithReExecute` n'est jamais déclenché — la branche B est écartée formellement.

Le corps de la réponse contient en tête de `<body>` le marqueur
`<!--Blazor:{"type":"server","prerenderId":"…"}-->` : **la page `Account/` est servie en composant
interactif préredu**, pas en SSR statique. `[ExcludeFromInteractiveRouting]`
(`Components/Account/_Imports.razor`) n'a donc aujourd'hui **aucun effet**, faute de la moitié
« côté `App.razor` » du mécanisme (constat déjà relevé plus haut : la chaîne de résolution du mode
de rendu par requête est absente du binaire).

**Mécanisme exact, mesuré et non supposé.** Un test bUnit rendant directement le composant
`Router` du framework (même `AppAssembly`, même `NotFoundPage` que `Components/Routes.razor`) sur
trois URL donne :

| URL demandée au routeur interactif | Composant résolu |
| --- | --- |
| `/import-profiles` | `ImportProfiles` |
| `/Account/Login` | **`NotFound`** |
| `/Account/ForcePasswordChange` | **`NotFound`** |

La table de routes du `Router` **exclut** les pages portant `[ExcludeFromInteractiveRouting]` — c'est
précisément le rôle de cet attribut. D'où l'enchaînement complet :

1. le serveur rend la page correctement (SSR de prérendu) → `200`, formulaire présent, et
   `<title>` = `ForcePasswordChange_Title` écrit dans le `<head>` ;
2. le circuit SignalR se connecte, `<Routes>` est **re-rendu en interactif** ;
3. la route n'existe pas dans la table interactive → le `Router` rend `NotFoundPage`
   (`Components/Pages/NotFound.razor`) et **remplace le corps de la page** ;
4. `<HeadOutlet />` d'`App.razor` n'a, lui, aucun mode de rendu : il reste statique, donc le titre
   d'onglet de la vraie page **survit** au remplacement du corps. C'est exactement l'indice
   « titre correct / corps NotFound » relevé dans les constats.

**Réponse à la troisième case de 49.0 : oui.** `/Account/Login` et `/Account/Register` portent le
défaut à l'identique (`NotFound` dans le tableau ci-dessus) ; il est simplement resté inaperçu en
usage réel. Le correctif **49.2 est donc requis quelle que soit la lecture du symptôme**, et il
corrige au passage un second défaut certain de `ForcePasswordChange.razor` : en rendu interactif, le
`[CascadingParameter] HttpContext` est `null`, donc `UserManager.GetUserAsync(HttpContext.User)`
dans `OnInitializedAsync` ne peut pas fonctionner — la page n'a jamais été exécutable en interactif.

**`BlazorDisableThrowNavigationException` n'est pas causal** (aucune redirection n'intervient dans le
parcours reproduit) : laissé tel quel, conformément au hors-périmètre, à inscrire comme dette connue.

**Conséquence pour 49.1** : le test « `GET` → `200` + formulaire présent » est **déjà vert
aujourd'hui** — il n'exerce pas la couche fautive. L'assertion réellement discriminante au niveau
HTTP est l'**absence du marqueur de composant interactif** dans la réponse des pages `Account/`
(et sa présence sur une page admin), cf. la note d'efficacité du ticket : « si le test passe du
premier coup, le réécrire ».

**API à utiliser en 49.2** : `AcquireRenderModeForPage` **n'existe pas** dans le SDK installé
(vérifié sur `Microsoft.AspNetCore.App` 10.0.10 : chaîne absente de toutes les assemblies du
framework partagé). L'API réellement fournie est
`RazorComponentsEndpointHttpContextExtensions.AcceptsInteractiveRouting(this HttpContext)`
(`Microsoft.AspNetCore.Components.Endpoints`), qui lit `ExcludeFromInteractiveRoutingAttribute` sur
les métadonnées de l'endpoint. C'est elle qui est employée.

---

## 49.1. Test d'intégration rouge — la page cible répond réellement en HTTP

**À écrire AVANT tout correctif**, et à vérifier rouge sur le code actuel. C'est le test qui
manquait au lot 045 : les tests bUnit de 45.2 rendent le composant directement et court-circuitent
donc entièrement le routage, le mode de rendu et le pipeline HTTP — exactement les couches où le
bug se trouve.

**Comportement attendu** : un `GET /Account/ForcePasswordChange` authentifié avec un utilisateur
portant `RequirePasswordChangeOnFirstLogin = true` répond **200** et le corps de la réponse contient
`force-password-change-form`.

**Tests** (`WebApplicationFactory<Program>`, projet `ExcelETL.BlazorAdmin.Tests`) :
- Utilisateur flag `true` → `GET /Account/ForcePasswordChange` → `200 OK`, corps contenant
  `id="force-password-change-form"` et les quatre autres IDs stables de 45.2. **Rouge attendu
  aujourd'hui.**
- Utilisateur flag `false` → `GET /Account/ForcePasswordChange` → redirection hors de la page
  (couvre la règle 45.2 « ne pas exposer le formulaire à qui n'en a pas besoin », jamais vérifiée
  au niveau HTTP jusqu'ici).
- Utilisateur non authentifié → redirection vers `/Account/Login` (non-régression du challenge
  cookie).

**Contraintes** : suivre le pattern `WebApplicationFactory` déjà en place (`IdentitySeeding:Enabled`
et `Database:AutoMigrate` désactivés dans les tests, cf. `Program.cs` lignes 208-236 et les
commentaires associés — ne pas réinventer ce câblage). xUnit 2.9.3 + FluentAssertions **7.x**
(v8+ interdite, licence commerciale).

---

## 49.2. Correctif — `App.razor` honore `[ExcludeFromInteractiveRouting]`

*(À appliquer si branche A, et de toute façon si 49.0 confirme que le mécanisme est incomplet.)*

**Comportement attendu** : les pages de `Components/Account/` sont rendues en **SSR statique**, les
pages admin restent en `InteractiveServer`. Concrètement, `<Routes>` (et `<HeadOutlet>` si le SDK
installé le prévoit) reçoit un mode de rendu **calculé par requête** au lieu d'une constante,
en s'appuyant sur l'API du framework qui lit `ExcludeFromInteractiveRoutingAttribute`
(`HttpContext.AcquireRenderModeForPage(...)` dans les versions récentes).

**Impératif** : ne pas recopier de mémoire la signature de cette API — **vérifier la forme exacte
attendue par le SDK .NET 10 réellement installé** (IntelliSense / métadonnées de
`Microsoft.AspNetCore.Components.Endpoints`) avant d'écrire le code. Si l'API diffère de ce qui est
supposé ici, implémenter l'équivalent documenté par le SDK installé plutôt que de forcer ce nom.

**Tests** : le test rouge de 49.1 passe au vert. Ajouter en complément :
- `GET /Account/Login` → 200 contenant le formulaire de connexion (non-régression explicite : ces
  pages ne doivent pas basculer en interactif au passage).
- Une page admin (ex. `/import-profiles`) → 200 et présence du marqueur de démarrage du circuit
  Blazor interactif dans la réponse (le mode interactif ne doit pas être perdu pour l'admin).

**Attention non-régression** : la connexion écrit le cookie d'authentification pendant le POST SSR.
Si ce correctif faisait basculer par erreur une page `Account/` en interactif, `PasswordSignInAsync`
échouerait (« response has already started »). Les deux tests ci-dessus doivent verrouiller ça.

---

## 49.3. Correctif — code d'erreur serveur

*(À appliquer uniquement si branche B. Le contenu exact de ce ticket dépend du code de statut relevé
en 49.0 ; il est volontairement laissé ouvert plutôt que rempli par anticipation.)*

**Comportement attendu** : la cause du code d'erreur relevé en 49.0 est supprimée à sa racine, pas
masquée par un contournement de `UseStatusCodePagesWithReExecute`. Le test 49.1 passe au vert sans
qu'aucune assertion n'ait été affaiblie.

**Interdit** : « corriger » en retirant ou en restreignant `UseStatusCodePagesWithReExecute`, ou en
ajoutant une exception de chemin pour `/Account/ForcePasswordChange` — cela masquerait le défaut au
lieu de le résoudre.

---

## 49.4. Garde — la page d'erreur ne doit pas être une cible de redirection

**Comportement attendu** : `PasswordChangeGuard` (`Components/Layout/PasswordChangeGuard.razor`)
n'essaie pas de rediriger depuis la page d'erreur. Son allow-list actuelle
(`AllowedPathPrefixes`, lignes 31-35) ne contient que `account/forcepasswordchange` et
`account/logout` ; toute page d'erreur (`/not-found`, `/Error`) est donc une cible de redirection
dès que le flag est actif. C'est aujourd'hui masqué par `BlazorDisableThrowNavigationException`,
mais c'est une boucle de redirection en puissance — exactement le risque signalé dans la note
d'implémentation du lot 045.

**Tests** (bUnit, `TestNavigationManager`, IDs stables, jamais de sélection par texte) :
- Utilisateur flag `true` sur `/not-found` → **aucune** redirection déclenchée.
- Utilisateur flag `true` sur `/Error` → **aucune** redirection déclenchée.
- Non-régression : utilisateur flag `true` sur `/users` → toujours redirigé vers la page de
  changement forcé (le lot ne doit pas affaiblir le verrou).

---

## 49.5. Non-régression — le parcours complet de bout en bout

**Comportement attendu** : le scénario client réel fonctionne sans intervention SQL manuelle.

**Test** (`WebApplicationFactory`, un seul test enchaînant les étapes) :
- Compte créé par un admin (flag `true`) → `POST /Account/Login` avec le mot de passe temporaire →
  302 vers `/Account/ForcePasswordChange` → `GET` de cette page → 200 avec le formulaire →
  `POST` du formulaire avec un nouveau mot de passe valide → flag levé en base, redirection accueil
  → `GET /import-profiles` → **200, plus aucune redirection vers la page de changement forcé**.
- Puis : déconnexion, reconnexion avec le **nouveau** mot de passe → accès direct à l'accueil.

C'est le test qui aurait dû exister au lot 045 ; il est la condition de clôture de ce lot.

---

## Ordre recommandé

1. **49.0** (diagnostic — bloquant, conditionne 49.2 vs 49.3)
2. **49.1** (test rouge HTTP — écrit et vérifié rouge *avant* tout correctif)
3. **49.2** ou **49.3** selon la branche retenue (vert)
4. **49.4** (garde — indépendant, peut être traité isolément)
5. **49.5** (non-régression bout en bout — clôture du lot)

## Note d'efficacité d'implémentation (Claude Code)

- **49.0 est réellement bloquant.** Le test discriminant (source brut de la page) coûte 30 secondes
  et évite d'écrire le mauvais correctif. Ne pas le sauter au motif que la branche A « semble
  probable ».
- **49.1 doit être rouge avant le correctif.** Si le test écrit passe du premier coup sur le code
  actuel, c'est qu'il n'exerce pas la bonne couche (typiquement : rendu bUnit du composant au lieu
  d'une vraie requête HTTP) — le réécrire, ne pas le déclarer vert.
- **Leçon à retenir du lot 045, applicable à tout futur lot Blazor** : un test bUnit ne prouve
  jamais qu'une page est atteignable. Dès qu'un ticket fait de la navigation, du routage ou une
  redirection une exigence, il lui faut au moins un test `WebApplicationFactory` qui fait une vraie
  requête HTTP sur l'URL concernée. À propager dans `recommandations-tickets-tdd.md` une fois ce lot
  clos.
- Exécuter les tests en sortie minimale et filtrée :
  `dotnet test --filter FullyQualifiedName~ForcePasswordChange --verbosity quiet`.
- Effort élevé réservé à 49.0 (diagnostic) et au refactor éventuel de 49.2 ; les étapes red/green
  restent en effort standard.
