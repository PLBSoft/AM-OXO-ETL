# Recommandations pour la génération de tickets TDD (Blazor / bUnit)

À appliquer par défaut lors de la création de tickets TDD Markdown destinés à Claude Code.

## 1. Granularité des tickets

- Un ticket = un cycle red/green/refactor par comportement testé, pas un lot entier regroupant plusieurs fonctionnalités.
- Si une demande couvre plusieurs comportements indépendants (ex: labels + édition + suppression), générer **plusieurs tickets distincts** plutôt qu'un ticket unique "lot".
- Chaque ticket doit pouvoir être traité, testé et clôturé (avec `/clear` ou `/compact`) sans dépendre du contexte d'un autre ticket.

## 2. Niveau d'effort par étape

- **Red** (écrire le test qui échoue) : effort standard suffit, étape mécanique.
- **Green** (faire passer le test au plus simple) : effort standard suffit.
- **Refactor** : réserver l'effort élevé/réflexion approfondie à cette étape, où le raisonnement architectural compte réellement.
- Ne pas activer un mode de réflexion élevé pour l'ensemble du ticket par défaut — le préciser explicitement seulement pour le refactor si nécessaire.

## 3. Réduction du bruit des tests

- Recommander l'exécution des tests avec une sortie minimale :
  - `dotnet test --verbosity quiet` ou `--logger "console;verbosity=minimal"`
- Filtrer les tests exécutés à chaque itération avec `--filter` pour ne cibler que la classe/le test concerné, plutôt que de relancer toute la suite à chaque cycle.

## 4. Gestion des diffs et relectures

- Préférer des diffs ciblés (portion modifiée) plutôt que la relecture systématique de fichiers entiers après chaque modification, sauf si le fichier est court ou si une vue d'ensemble est nécessaire.

## 5. Gestion des échecs inattendus

- En cas d'échec de test inattendu (ex: rendu Blazor/bUnit, timing, markup généré différemment), éviter les tentatives de correction en boucle non pilotées.
- Privilégier : interruption, inspection du diff produit, puis relance ciblée d'un tour précis plutôt qu'un enchaînement de retries qui traînent tout l'historique du contexte.

## 6. Ce qu'un test bUnit ne prouve jamais (leçon des lots 045 / 049)

bUnit rend un composant **directement** : il court-circuite le routage, le mode de rendu et tout le
pipeline HTTP. Un composant peut donc être intégralement vert en bUnit et **totalement inatteignable**
dans l'application — c'est exactement ce qui est arrivé au lot 045 (page `/Account/ForcePasswordChange`
livrée verte, affichant « Introuvable » en production, cf. lot 049).

Règles à appliquer dès qu'un ticket fait du **routage, une navigation ou une redirection** une exigence :

- Il lui faut au moins un test `WebApplicationFactory<Program>` qui effectue une **vraie requête HTTP**
  sur l'URL concernée (code de statut + présence des IDs stables attendus dans le corps).
- Ce test est **nécessaire mais pas suffisant**. Un `HttpClient` n'ouvre jamais le circuit SignalR :
  un défaut qui ne se manifeste qu'après le démarrage du circuit interactif reste invisible. Quand la
  page dépend du **mode de rendu** (page `[ExcludeFromInteractiveRouting]`, écriture de cookie
  d'authentification pendant un POST, lecture du `HttpContext` en cascade), assortir la requête d'une
  assertion sur le mode effectivement servi — typiquement l'absence/présence du marqueur de composant
  interactif (`"type":"server"`) dans la réponse.
- Vérifier qu'un test de non-régression n'est pas **vide de sens** : le remettre au rouge en défaisant
  volontairement le correctif. Au lot 049, le test de parcours bout en bout passait aussi bien avec le
  défaut en place — il a fallu lui ajouter l'assertion de mode de rendu pour qu'il serve à quelque chose.

## 7. Structure attendue d'un ticket TDD

Chaque ticket doit inclure :
- Le comportement précis à implémenter (un seul par ticket)
- Les cas de test bUnit à écrire (rouge)
- L'implémentation minimale attendue (vert)
- Les pistes de refactor à considérer, si pertinent
- Toute contrainte d'effort/mode à appliquer selon l'étape (cf. section 2)
- Pour tout comportement de routage/navigation/redirection : le test HTTP exigé par la section 6
