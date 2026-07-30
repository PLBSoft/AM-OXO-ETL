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
- Ce même principe s'applique à la **phase de rédaction** des tickets, pas seulement à leur implémentation : voir section 8.

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
- Aucune information recopiée d'un autre document du contexte : la citer par son nom de fichier
  (cf. section 8, « un fait, un endroit »)

## 8. Coût d'une session de rédaction de tickets (leçon des lots 056 / 057 / 058)

Rédiger des tickets consomme des ressources dans le même ordre de grandeur que les implémenter. La
séance qui a produit les lots 056 à 058 a été mesurée : **le texte produit pesait 44 % du coût total**
(rédaction des lots + discussion des options), la lecture des documents du contexte 20 %, et
l'inspection du code seulement 11 %. Ce n'est donc pas la lecture du dépôt qui coûte cher, c'est
l'écriture — et la réécriture.

Règles, par ordre de gain décroissant :

- **Rassembler les faits avant de rédiger, jamais après.** Un ticket écrit sur hypothèses puis réécrit
  sur constats coûte deux fois le ticket. Si la demande porte sur du code existant, vérifier d'abord
  que le dépôt est accessible et le demander **dans la première réponse** s'il ne l'est pas — pas après
  avoir produit une première version. C'était le principal gâchis de la séance 056-058 (~12 % du total,
  entièrement évitable).
- **Déléguer la reconnaissance de code à un sous-agent**, avec une consigne fermée et un retour limité
  aux faits demandés (recensement d'`id`, de chaînes de classes, de clés `.resx`, de compteurs de
  champs). Charger les fichiers dans le contexte principal les fait relire à chaque tour suivant.
- **Un fait, un endroit.** Ce qui vaut pour plusieurs lots (conventions en place, garde-fous
  transverses) va dans une convention citée par son nom de fichier, pas recopié dans chaque lot.
  Trois lots frères qui répètent la même section « Conventions déjà en place » paient trois fois la
  même information — et divergeront.
- **Effort élevé pour les arbitrages, standard pour le reste.** Choisir un modèle d'enregistrement,
  peser des options qui s'excluent, détecter qu'une demande contredit une décision antérieure : effort
  élevé, c'est là que se joue la valeur. Recenser des identifiants, appliquer une décision déjà prise
  aux deux éditeurs, publier les documents : effort standard.
- **Moins de tours, pas des tours plus courts.** Le préambule (instructions système + liste d'outils)
  est rechargé à chaque aller-retour et représentait 22 % de la séance mesurée. Grouper les appels
  d'outils indépendants dans un même tour vaut mieux que multiplier les petits tours.

Ce qui **ne se coupe pas**, même pour raccourcir un ticket :

- les constats sourcés (fichier, ligne, valeur réelle) qui remplacent une hypothèse ;
- les décisions écartées **et leur raison** — sans elles, la décision est rouverte au lot suivant ;
- la section « hors périmètre » (exigée par les instructions projet) ;
- les tests de garde-fou de non-généralisation.

Ces quatre postes sont précisément ceux qui évitent une séance d'implémentation jetée. Les supprimer
pour économiser du texte déplace la dépense au lieu de la réduire, et la déplace vers l'endroit où
elle coûte le plus cher.
