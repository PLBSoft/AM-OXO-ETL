# Tickets TDD — Lot 054 : page d'accueil épurée avec indicateurs

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 053
(`tickets-tdd-lot-053-largeur-densite-formulaires-editeurs-profil.md`).*

**Origine (demande Simon, 28/07)** — l'application n'a pas de page d'accueil. La racine `/` est la
seconde route de `ImportProfiles.razor` : tout compte qui se connecte atterrit directement dans une
liste d'édition, sans jamais voir l'état général de l'application. Ce lot crée une véritable page
d'accueil, volontairement **épurée**, qui répond à une seule question : *qu'y a-t-il dans cette
application en ce moment ?*

Deux effets attendus, dans cet ordre d'importance.

**Effet principal — donner un point d'entrée neutre.** Un utilisateur qui se connecte voit d'abord
un état, pas un formulaire. C'est aussi ce qui rend le parcours de première connexion (lots
044/045/049/052) lisible : le compte fraîchement créé arrive sur une page qui lui appartient, au
lieu d'un écran d'administration de profils dont il ne sait pas encore s'il a le droit d'y toucher.

**Effet secondaire — découpler la racine.** Aujourd'hui `/` est mappée par `ImportProfiles.razor`,
et `convention-autorisation-pages-blazoradmin.md` §5 documente cette dépendance comme structurelle :
la cible de redirection après connexion et le lien de retour de `/Account/AccessDenied` pointent
tous deux vers `/`. Ce lot rend cette dépendance saine — la racine devient une page qui n'a aucune
raison de changer de niveau d'autorisation, au lieu d'une page d'édition qui pourrait un jour en
changer.

---

## Décisions actées pour ce lot

Elles sont posées ici et n'ont pas à être rediscutées pendant l'implémentation.

- **Quatre indicateurs, pas un de plus** : nombre de profils d'import, nombre de profils d'export,
  nombre total de fichiers générés archivés, date de la dernière génération. « Épuré » est une
  contrainte de conception, pas une préférence esthétique : tout cinquième indicateur exige un
  ticket.
- **Aucune donnée nouvelle n'est persistée.** Les quatre indicateurs se lisent exclusivement depuis
  les stores existants (`IImportProfileStore`, `IExportProfileStore`,
  `IGeneratedFileArchiveStore`). Aucune table, aucune colonne, aucune migration — la règle
  « pas de mécanisme de persistance parallèle » du projet s'applique intégralement.
- **Aucun graphique, aucune bibliothèque de visualisation.** Quatre valeurs numériques ou textuelles
  dans des tuiles. Une courbe supposerait un historique, qui n'existe pas.
- **La page est accessible à tout compte authentifié, sans rôle** (`[Authorize]` sans rôle), comme
  toutes les pages métier depuis le lot 052. Elle affiche **exactement le même contenu** pour un
  Admin et pour un compte sans rôle — aucun indicateur conditionné au rôle.
- **`/` appartient désormais à la page d'accueil, et à elle seule.** `ImportProfiles.razor` ne
  conserve que `@page "/import-profiles"`.
- **Un indicateur indisponible n'abat jamais la page.** Une lecture qui échoue affiche un état
  dégradé pour cette tuile-là ; les trois autres restent affichées. Une page d'accueil qui plante
  rend l'application entière inaccessible dès la connexion : c'est le seul risque réel introduit
  par ce lot, et il se traite par conception, pas par espoir.

---

## Conventions déjà en place à respecter

- **`convention-autorisation-pages-blazoradmin.md`** — §4 (chaque route porte son attribut
  explicitement, même quand il coïncide avec la `FallbackPolicy`) et §5 (dépendance de la
  redirection post-connexion à la racine, que ce lot modifie et doit donc mettre à jour).
- **Leçon des lots 049, 051 et 052** : un test bUnit ne prouve jamais qu'une route est atteignable.
  Toute assertion de routage ou d'autorisation exige un test `WebApplicationFactory<Program>` avec
  une vraie requête HTTP. bUnit ne sert qu'au contenu d'un composant déjà atteint.
- `convention-ui-blazor-icones-boutons.md` et `convention-ui-blazor-alignement-boutons.md` pour
  toute icône ou bouton introduit.
- Annonces d'état asynchrone (lot 040) : la page charge ses données après le premier rendu, elle
  doit donc annoncer chargement et fin de chargement comme les autres pages du projet.
- Rendu mobile-first (lot V) : les tuiles s'empilent proprement sur petit écran, sans gabarit
  dupliqué inutilement.
- IDs HTML stables sur tout élément interactif et sur chaque tuile ; jamais de sélection par texte
  ni par position en bUnit.
- Localisation EN/FR via `.resx`, aucune chaîne en dur dans le composant.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit.
- Accès aux données uniquement par les interfaces de repository de `Application` ; aucun
  `DbContext` injecté dans un composant Razor.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite de ce lot

- **Tout indicateur supplémentaire** : nombre d'utilisateurs, volume d'erreurs récentes, taille
  cumulée des fichiers, taux de succès des extractions. Écartés — chacun rouvrirait la question de
  ce que « épuré » veut dire, et deux d'entre eux dépendraient du rôle.
- **Tout graphique, courbe, jauge, sparkline ou bibliothèque de visualisation.**
- **Toute nouvelle table, colonne ou migration EF Core.** Si un indicateur ne peut pas être obtenu
  depuis les stores existants, il est abandonné, pas rendu possible par un ajout de schéma.
- **Rafraîchissement automatique, temps réel, SignalR, minuterie.** La page se met à jour quand on
  la recharge.
- **Toute modification du contenu, du comportement ou des tests de `ImportProfiles.razor`** au-delà
  du retrait de la directive `@page "/"`.
- **Toute modification du parcours de connexion, du mot de passe temporaire ou du modèle
  d'autorisation** (lots 044/045/049/051/052) : la cible de redirection reste `/`, inchangée — c'est
  ce qui se trouve derrière `/` qui change.
- **Refonte de `NavMenu.razor`** au-delà de ce que 54.5 autorise explicitement. L'ordre des liens
  acté au Lot S2 n'est pas rediscuté.
- **Pages `/Error` et `/not-found`** — inchangées.

---

## 54.0. Investigation préalable (obligatoire avant tout code)

- [ ] **Relever la signature réelle des trois stores** (`IImportProfileStore`,
  `IExportProfileStore`, `IGeneratedFileArchiveStore`) : existe-t-il une méthode de comptage, ou
  seulement un `GetAllAsync` ? Et l'enregistrement de fichier généré porte-t-il un horodatage
  exploitable pour « dernière génération » ? **C'est le livrable central de 54.0** : il détermine
  si 54.2 se contente de composer l'existant ou doit ajouter une méthode.
- [ ] **Décider du comptage à partir de ce relevé, en le justifiant dans le rapport.** Si seul
  `GetAllAsync` existe, un comptage en mémoire est acceptable pour les profils (quelques dizaines
  d'éléments) mais discutable pour les fichiers générés, dont le volume croît sans borne. Dans ce
  cas, ajouter une méthode de comptage à l'interface du store et l'implémenter dans
  `Infrastructure` — jamais écrire une requête EF Core dans `Application`.
- [ ] **Relever le mode de rendu réel de `ImportProfiles.razor`** (interactif serveur ou SSR
  statique) et la façon dont il charge ses données. La page d'accueil suit le même mode, sauf
  raison contraire explicitée dans le rapport.
- [ ] **Recenser tous les tests existants qui portent sur la route `/`** — au minimum
  `BusinessPageAuthorizationHttpTests` (`NonAdminAccount_CanReachEveryBusinessRoute`, paramétré sur
  `"/"`), `AccessDeniedHttpTests`
  (`ReturnLink_PointsToARouteAccessibleToAnAuthenticatedAccountWithoutARole`) et le test de parcours
  bout en bout du lot 052 qui assère `GET /` → **200**. Distinguer ceux qui assèrent seulement
  l'**accessibilité** de `/` (ils doivent rester verts sans modification) de ceux qui assèrent son
  **contenu** comme étant la liste des profils d'import (ils se **corrigent**, ils ne se doublent
  pas).
- [ ] **Vérifier si un lien de marque / titre d'application pointant vers `/` existe déjà** dans
  `MainLayout.razor` ou `NavMenu.razor`. Conditionne 54.5 : s'il existe, aucun lien de navigation
  n'est ajouté.
- [ ] Relever les clés de ressources existantes réutilisables (« Profils d'import », « Profils
  d'export », « Fichiers générés ») plutôt que d'en créer des doublons.

**Effort** : élevé sur le relevé des stores et l'inventaire des tests de `/`. Le reste est de la
lecture.

---

## 54.1. La racine appartient à la page d'accueil

**Comportement attendu** : une nouvelle page `Home.razor` (`Components/Pages/`) déclare
`@page "/"` et `@attribute [Authorize]` — explicitement, même si la `FallbackPolicy` produirait le
même effet (convention §4). `ImportProfiles.razor` perd sa directive `@page "/"` et ne conserve que
`@page "/import-profiles"`.

**Tests** (`WebApplicationFactory<Program>`, requêtes HTTP réelles — seule couche probante) :
- Compte **sans rôle** authentifié → `GET /` → **200**, et le corps contient le marqueur de la page
  d'accueil (un ID stable, jamais un texte localisé).
- Compte **Admin** → `GET /` → **200**, même marqueur. Non-régression : la page ne se comporte pas
  différemment selon le rôle.
- **Non authentifié** → `GET /` → `302` vers `/Account/Login` (non-régression de la
  `FallbackPolicy`).
- `GET /import-profiles` → **200**, liste des profils d'import : la route survit au retrait de la
  racine.
- Ce test est écrit **avant** la création de `Home.razor` et vérifié **rouge** — sans quoi il ne
  prouve rien, la racine répondant déjà 200 aujourd'hui.

---

## 54.2. Lecture des indicateurs — couche Application

**Comportement attendu** : un service applicatif dédié (`IHomeIndicatorsService` /
`HomeIndicatorsService`, `Application`) expose une méthode unique retournant un objet immuable
portant les quatre valeurs : nombre de profils d'import, nombre de profils d'export, nombre de
fichiers générés, date de la dernière génération (nullable — l'absence est un cas normal, pas une
erreur).

- Le service **compose les stores existants**, il ne connaît ni EF Core ni SQL.
- **Chaque indicateur se lit indépendamment** : l'échec de l'un ne propage pas d'exception, il
  produit une valeur « indisponible » pour ce seul indicateur (voir 54.4). L'objet retourné
  distingue donc « valeur connue », « valeur absente » (aucune donnée) et « indisponible » (lecture
  en échec) — trois états distincts, jamais confondus dans un zéro.
- Une lecture en échec est journalisée en `Warning` via `ILogger` (mécanisme Serilog existant,
  aucun autre).

**Tests** (unitaires, `Application.Tests`, stores mockés avec Moq) :
- Les quatre valeurs sont reprises telles quelles quand les trois stores répondent normalement.
- Aucun profil / aucun fichier généré → compteurs à zéro et date de dernière génération **absente**,
  jamais une date par défaut ni une exception.
- Un store qui lève → l'indicateur correspondant est **indisponible**, les autres conservent leur
  valeur, et la méthode ne lève pas.
- La date de dernière génération correspond bien au fichier le plus récent, pas au dernier inséré,
  quand les deux diffèrent.

---

## 54.3. La page — contenu et états

**Comportement attendu** : `Home.razor` affiche un titre, une phrase de contexte, et quatre tuiles.
Chaque tuile porte un libellé localisé, une valeur, une icône conforme à
`convention-ui-blazor-icones-boutons.md`, et un ID stable :
`#home-kpi-import-profiles`, `#home-kpi-export-profiles`, `#home-kpi-generated-files`,
`#home-kpi-last-generation`.

Les trois premières tuiles sont des liens vers la page correspondante (`/import-profiles`,
`/export-profiles`, `/generated-files`) — toutes accessibles à un compte authentifié sans rôle
depuis le lot 052, donc **aucune tuile ne peut mener à un refus d'accès**. La quatrième n'est pas un
lien. Chaque lien porte son propre ID stable.

Trois états à couvrir : chargement en cours, valeurs affichées, valeur indisponible.

**Tests** (bUnit — contenu uniquement, jamais le routage ni l'autorisation) :
- Service mocké renvoyant des valeurs connues → les quatre tuiles sont présentes et affichent ces
  valeurs.
- Compteurs à zéro et dernière génération absente → les tuiles affichent zéro et un état « aucune
  génération » explicite, **pas** une tuile masquée ni une date vide.
- Pendant le chargement → indicateur de chargement présent, aucune valeur affichée.
- Les trois liens pointent vers les routes attendues (assertion sur l'attribut `href`, par ID).
- Rendu mobile : les tuiles restent présentes, sans duplication de contenu entre gabarits (invariant
  d'identité de contenu du lot V, s'il s'applique au gabarit retenu).

---

## 54.4. Résilience — un indicateur en échec n'abat pas la page

**Comportement attendu** : quand le service signale un indicateur indisponible, la tuile concernée
affiche un état dégradé lisible et localisé (jamais une trace technique, jamais un zéro trompeur) ;
les trois autres tuiles s'affichent normalement ; la page renvoie **200**.

**Tests** :
- (bUnit) Service renvoyant un indicateur indisponible sur trois valides → la tuile en échec porte
  son état dégradé, les trois autres leurs valeurs.
- (bUnit) Service dont l'appel lui-même lève → la page se rend malgré tout, avec un message d'erreur
  global localisé et sans exception remontée au rendu.
- (`WebApplicationFactory`) La racine répond **200** même lorsque la source de données est
  indisponible. **C'est le test qui protège l'application entière** : `/` est la cible de
  redirection après connexion, une racine en erreur 500 rendrait l'application inaccessible dès la
  connexion à tout le monde, y compris aux Admin.

---

## 54.5. Navigation

**Comportement attendu**, conditionné au relevé de 54.0 :

- **Si un lien de marque / titre pointant vers `/` existe déjà** dans la mise en page : **rien
  n'est ajouté**. C'est le cas préféré — le plus épuré, et il ne touche pas à l'ordre acté au
  Lot S2.
- **Sinon** : ajout d'un unique lien `#nav-home-link` en **première position** de `NavMenu.razor`,
  dans un bloc `<AuthorizeView>` **sans rôle**. C'est la seule modification d'ordre autorisée par ce
  lot ; l'ordre relatif de tous les liens existants reste inchangé.

**Tests** (bUnit, uniquement dans le second cas) :
- Utilisateur sans rôle → `#nav-home-link` présent, exactement une fois.
- Utilisateur Admin → présent également.
- Utilisateur non authentifié → **absent du DOM** (absence réelle, pas `hidden` ni `disabled`),
  et le test d'unicité de `#nav-login-link` du Lot L2 reste vert **sans modification**.

---

## 54.6. Accessibilité et localisation

- Titre de page (`<PageTitle>`) localisé, hiérarchie de titres correcte (un seul `<h1>`).
- Chaque tuile est lisible par un lecteur d'écran : la valeur seule ne suffit pas, le libellé doit
  lui être associé et non simplement voisin visuellement.
- Annonce d'état asynchrone conforme au lot 040 pour le chargement et sa fin.
- Nouvelles clés `.resx` EN/FR pour le titre, la phrase de contexte, les quatre libellés, l'état
  « aucune génération » et l'état dégradé. Réutiliser les clés existantes relevées en 54.0 plutôt
  que de les dupliquer ; aucune clé orpheline laissée derrière.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet). L'association
libellé/valeur, elle, est vérifiée en bUnit.

---

## 54.7. Non-régression bout en bout — le parcours de connexion

**Comportement attendu** : les deux parcours qui aboutissent à `/` continuent de fonctionner, et
aboutissent désormais à la page d'accueil.

**Tests** (`WebApplicationFactory`) :
- Connexion normale d'un compte sans rôle → redirection vers `/` → **200**, page d'accueil.
- Parcours complet du lot 052 (création par un Admin, mot de passe temporaire, changement forcé,
  redirection) → aboutit à la page d'accueil. **Étendre le test de parcours existant plutôt que le
  dupliquer** ; seule son assertion finale de contenu change.
- Le lien de retour de `/Account/AccessDenied` pointe toujours vers une route accessible à un compte
  authentifié sans rôle — le test existant doit rester vert **sans modification**. S'il casse, c'est
  un défaut d'attribut sur `Home.razor`, pas un test à ajuster.

---

## 54.8. Mise à jour de la convention

`convention-autorisation-pages-blazoradmin.md` : le tableau §2 gagne une ligne pour `/`
(page d'accueil, Authentifié sans rôle) et la ligne de `ImportProfiles.razor` perd la racine. Le §5
est réécrit pour refléter la nouvelle situation — la cible de redirection post-connexion reste `/`,
mais pointe désormais vers une page dont le niveau d'autorisation n'a aucune raison de changer, ce
qui referme le risque que ce paragraphe signalait. Document vivant : mise à jour **en place**, aucun
historique de version ajouté à l'intérieur.

---

## Ordre recommandé

1. **54.0** (relevé des stores et inventaire des tests de `/` — alimente tout le reste)
2. **54.2** (service applicatif : testable seul, sans aucune UI)
3. **54.1** (test HTTP rouge sur la racine, puis création de `Home.razor` et retrait de `@page "/"`)
4. **54.3** puis **54.4** (contenu, puis états dégradés)
5. **54.5** (navigation, si nécessaire)
6. **54.6** (accessibilité et ressources)
7. **54.7** (parcours bout en bout — clôture)
8. **54.8** (convention)

## Note d'efficacité d'implémentation (Claude Code)

- **54.2 avant toute UI.** Les quatre indicateurs et leurs trois états se spécifient entièrement
  avec des mocks, sans rendu. Commencer par la page conduirait à découvrir l'état « indisponible »
  trop tard, une fois le balisage figé.
- **Le test HTTP de 54.1 doit être vérifié rouge.** La racine répond déjà **200** aujourd'hui : un
  test qui n'assère que le code HTTP passerait avant même que la page existe et ne prouverait rien.
  L'assertion doit porter sur un marqueur de contenu propre à la page d'accueil.
- **Ne jamais tester le routage avec bUnit.** bUnit rend un composant en isolation : il ne dira
  jamais qui possède la route `/`. Deux couches, deux types de test.
- **Les tests existants sur `/` se corrigent, ils ne se doublent pas.** Ceux qui n'assèrent que
  l'accessibilité doivent rester verts **sans modification** — c'est le signal que ce lot n'a
  déplacé que du contenu, pas des droits.
- **La racine ne doit jamais pouvoir échouer.** C'est la cible de redirection après connexion :
  une exception non traitée y verrouille l'application pour tous les comptes. 54.4 n'est pas du
  confort, c'est la contrepartie du fait de mettre une page qui lit la base à la racine.
- **Aucune migration ne doit apparaître dans ce lot.** Si `dotnet ef migrations` devient nécessaire,
  c'est qu'un indicateur a dérivé hors du périmètre : le supprimer plutôt que l'alimenter.
- Exécuter les tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~Home|FullyQualifiedName~Indicators|FullyQualifiedName~Authorization" --verbosity quiet`.
- Effort élevé sur **54.0** et **54.2** uniquement. 54.3 à 54.7 sont de l'application mécanique de
  décisions déjà prises.
