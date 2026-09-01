# Tickets TDD — Lot 065 : détail d'erreur enrichi sur `/api-test`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 064
(`tickets-tdd-lot-064-heure-locale-logs.md`).*

**Origine** : signalé par Simon (01/09) — un appel réel échoué depuis `/api-test` n'affiche que
« Une erreur inattendue s'est produite lors de l'appel au Web API. Consulter les journaux serveur
pour plus de détails. », obligeant à aller chercher les logs serveur séparément pour comprendre la
cause (dans le cas réel qui a déclenché ce ticket : `UnknownFieldReferenceException: Field
'HasZeroEnergie' was not found among the already-extracted fields`, non liée à ce lot — traitée
séparément).

**Cadrage** : ce lot ne concerne que la **restitution du détail d'une erreur déjà survenue** — il
ne modifie ni le code HTTP retourné pour une exception non gérée (reste 500), ni la forme des
réponses déjà mappées explicitement (Lot 036 : 400/422 sur payload invalide), ni le comportement du
pipeline d'extraction lui-même.

---

## Décisions actées avec Simon (01/09, non négociables)

- **Pas de mécanisme de corrélation** (pas d'ID de corrélation, pas de requête a posteriori vers
  `SystemLogs` depuis BlazorAdmin) — jugé trop complexe au regard du besoin. Principe KISS retenu.
- **Le détail vient directement de la réponse HTTP du WebAPI**, pas d'une source secondaire.
- **Contenu du détail** : type d'exception (nom court, ex. `UnknownFieldReferenceException`, pas la
  stack trace) **+** message de l'exception. **Jamais la stack trace.**
- **Contrat unique pour tout appelant** : le WebAPI renvoie ce détail enrichi systématiquement,
  qu'il soit appelé depuis `/api-test` ou par un client M2M externe — pas de comportement
  conditionnel selon l'origine de l'appel. `/api-test` se contente d'afficher ce que le WebAPI a
  toujours renvoyé.

---

## 65.0. Investigation préalable (obligatoire avant tout code)

- [ ] Identifier le mécanisme actuel de gestion des exceptions non gérées dans
  `ExcelETL.WebAPI` (middleware `UseExceptionHandler`, filtre d'action, `IExceptionHandler` .NET 10,
  ou absence de mécanisme dédié avec retour au comportement par défaut ASP.NET Core) — déterminer
  précisément ce que contient aujourd'hui le corps de la réponse HTTP 500 pour une exception non
  mappée (vide, `ProblemDetails` par défaut sans détail, page développeur désactivée en Production).
  Le log fourni par Simon montre le code 500 et la trace serveur, mais rien sur le corps HTTP
  réellement renvoyé au client.
- [ ] Identifier si des exceptions métier (Lot 036 : validations 400/422 sur le payload) sont déjà
  mappées vers un `ProblemDetails` structuré existant — si oui, ce lot doit réutiliser exactement ce
  mécanisme pour les exceptions non mappées plutôt que d'en introduire un second en parallèle.
- [ ] Confirmer le fichier exact et la méthode qui, dans `ExcelETL.BlazorAdmin`, appelle le WebAPI
  depuis `/api-test` (`OxoApiTestClient`, `ApiTest.razor` ou nom réel du composant) et comment la
  réponse d'erreur actuelle est traitée aujourd'hui (le body est-il déjà lu et simplement ignoré au
  profit du message générique, ou le code s'arrête-t-il dès que `IsSuccessStatusCode` est faux sans
  jamais lire le body ?).
- [ ] Vérifier si `ProblemDetails` (type ASP.NET Core standard) est déjà utilisé ailleurs dans le
  projet pour les réponses d'erreur (Lot 036 notamment) — si oui, l'étendre plutôt que d'introduire
  un type de réponse différent pour ce cas.

---

## 65.1. WebAPI — enrichissement de la réponse pour toute exception non gérée

**Comportement attendu** : pour toute exception atteignant le point de gestion global (c'est-à-dire
non déjà interceptée et transformée en 400/422 par un mapping métier existant, Lot 036), la réponse
HTTP conserve son code 500 actuel mais son corps devient un `ProblemDetails` (ou l'extension déjà en
place si 65.0 en révèle une) portant :
- le nom court du type d'exception (`ex.GetType().Name`) ;
- le message de l'exception (`ex.Message`) ;
- **jamais** `ex.StackTrace` ni aucune propriété en dérivant.

Ce comportement s'applique **sans distinction d'appelant** — même réponse pour `/api-test` et pour
tout client M2M externe utilisant la clé API.

**Tests** (`WebApplicationFactory`, xUnit, `ExcelETL.WebAPI.Tests`) :
- [ ] Un appel déclenchant une exception non mappée (via un service substitué levant une exception
  arbitraire dans le pipeline, ou reproduction du cas réel `UnknownFieldReferenceException` si
  faisable simplement avec les fixtures existantes) → corps de réponse JSON contenant le nom du
  type d'exception et son message.
- [ ] **Garde-fou explicite** : le corps de réponse ne contient jamais la chaîne `"   at "` (motif
  caractéristique d'une `StackTrace` .NET) ni de propriété nommée `StackTrace`/`stackTrace` — test
  dédié empêchant qu'une régression future ne réintroduise la stack trace silencieusement.
- [ ] Non-régression : les réponses déjà mappées explicitement (Lot 036, 400/422 sur payload
  invalide) conservent exactement leur forme actuelle — test de non-régression sur au moins un cas
  existant de ce lot.
- [ ] Le code HTTP reste 500 pour ce type d'exception (pas de changement de code — décision déjà
  fermée ailleurs, non rouverte ici).

**Dossier** : mécanisme de gestion d'exception identifié en 65.0 (`src/ExcelETL.WebAPI/`).

---

## 65.2. BlazorAdmin — affichage direct du détail sur `/api-test`

**Comportement attendu** : le composant identifié en 65.0 désérialise le `ProblemDetails` reçu en
cas d'échec HTTP et affiche le type d'exception et le message directement dans le bandeau d'erreur
existant, à la place du message générique actuel. Aucun nouvel appel réseau, aucune consultation de
`SystemLogs` (décision actée).

**Repli explicite** : si le corps de réponse n'est pas un `ProblemDetails` exploitable (erreur
réseau avant d'atteindre le WebAPI, timeout, réponse vide) → conserver le message générique actuel
tel quel, sans lever d'exception côté client.

**Tests** (bUnit, `ExcelETL.BlazorAdmin.Tests`) :
- [ ] Réponse HTTP 500 avec un `ProblemDetails` enrichi valide → le bandeau affiche le type
  d'exception et le message attendus ; ID HTML stable sur la zone de détail (sélection par ID, pas
  par texte).
- [ ] Réponse HTTP 500 avec un corps vide ou non parsable → message générique actuel affiché
  inchangé, pas d'exception non gérée côté composant.
- [ ] Non-régression : le cas de succès (200, fichier généré) n'est pas affecté par ce changement.

**Dossier** : composant `/api-test` identifié en 65.0 (`src/ExcelETL.BlazorAdmin/`).

---

## Hors périmètre explicite de ce lot

- Tout mécanisme de corrélation entre un appel `/api-test` et les entrées `SystemLogs`
  correspondantes (ID de corrélation, requête a posteriori) — explicitement écarté par Simon.
- Inclusion de la stack trace dans la réponse, sous quelque forme que ce soit — explicitement
  écarté par Simon.
- Changement du code HTTP retourné pour une exception non gérée (reste 500) — non rouvert.
- Correction du défaut réel ayant déclenché l'erreur `UnknownFieldReferenceException` /
  `HasZeroEnergie` observée par Simon — sujet distinct, traité séparément (probable écart de
  déploiement entre `WebAPI` et `BlazorAdmin`, à vérifier directement, pas via ce lot).
- Tout changement à la forme des réponses déjà mappées explicitement (Lot 036) — seule l'absence de
  mapping actuel change de comportement.

---

## Ordre recommandé

1. **65.0** (investigation — conditionne le choix technique exact de 65.1, notamment la
   réutilisation ou non d'un mécanisme `ProblemDetails` déjà en place)
2. **65.1** (WebAPI — le contrat doit exister et être testé avant que BlazorAdmin ne le consomme)
3. **65.2** (BlazorAdmin — dépend strictement de la forme de réponse livrée par 65.1)

## Note d'efficacité d'implémentation

- Ne pas grouper 65.1 et 65.2 dans le même commit : ce sont deux projets distincts (WebAPI /
  BlazorAdmin), l'isolement facilite la relecture et la localisation d'une éventuelle régression,
  cohérent avec la pratique déjà suivie au Lot 036.
- Le garde-fou anti-stack-trace de 65.1 est un test à ne pas sauter pour gagner du temps : c'est
  precisément le type de régression silencieuse qu'un refacto ultérieur pourrait réintroduire sans
  qu'aucun autre test ne le détecte.
