# Convention de nommage des documents — AM-OXO-ETL

## Deux catégories de documents

### 1. Documents vivants (référence courante, jamais de suffixe de date)
Reflètent l'état actuel des décisions/du modèle/de la spec. Mis à jour en place, sans jamais
accumuler d'historique de versions dans le corps du texte. Quand une décision change, l'ancien
contenu est remplacé, pas empilé à côté d'un nouveau paragraphe daté.

- `glossaire-ef6-legacy-AMAR-ModelCF.md`
- `modele-domaine-import-profile.md`
- `spec-extraction-fichier-source-oxo.md`
- `tickets-tdd-extraction.md`
- `tickets-tdd-blazor-profil-import.md` *(à renommer sans date au prochain document généré)*
- `etat-des-lieux-technique.md` *(déjà sans date, référence générale)*

Règle : pas de "Mise à jour (vN, date)" dans le corps. L'historique des décisions vit dans les
échanges avec l'assistant et dans git (commits, PR), pas dans le document lui-même.

### 2. Instantanés d'état des lieux (datés, jamais mis à jour en place)
Documents de constat ponctuel, produits par Claude Code à un instant donné, qui devraient être
volontairement figés dans le temps (ce sont des "photos" d'un état du code, pas une référence à
maintenir). Suffixe `AAAA-MM-JJ` (ou `AAAA-MM-JJ-HHhMM` si plusieurs exécutions le même jour).

- `etat-avancement-pipeline-extraction-2026-07-17.md`
- `etat-avancement-lot-f-blazor-profil-import-2026-07-17-18h06.md`
- tout futur `etat-avancement-*` ou `audit-*`

Règle : un nouvel état des lieux est un **nouveau fichier**, jamais une modification d'un
ancien. Les anciens peuvent être retirés du contexte du projet une fois qu'un nouveau les rend
obsolètes, pour éviter d'avoir plusieurs instantanés similaires qui se contredisent en contexte.

### 3. Préfixe de lot obligatoire pour les tickets TDD, identifiant numérique à largeur fixe

Tout fichier `tickets-tdd-*.md` correspondant à un lot porte son identifiant de lot juste après
`tickets-tdd-`, pour que le tri alphabétique d'un explorateur de fichiers respecte l'ordre
chronologique des lots.

**Lots F à Z (lettres simples, historiques)** : identifiants déjà attribués, conservés tels quels
— non renommés rétroactivement.

**Lots à partir du 27ᵉ (celui qui suit Z)** : un tri alphabétique pur ne respecte pas l'ordre
chronologique au-delà de Z (ex. "aa" < "z" en comparaison caractère par caractère, comme les
colonnes Excel après Z) — l'identifiant lettré est donc abandonné au profit d'un **numéro à trois
chiffres zéro-paddé dès le premier lot numérique** : `lot-027`, `lot-028`, `lot-029`, ... jusqu'à
`lot-999`.

Trois chiffres dès le départ (plutôt que deux, avec un passage à trois "quand on dépassera 99")
anticipe qu'un projet actif sur plusieurs années finira par dépasser 99 lots — repartir sur deux
chiffres ne ferait que reproduire exactement le même problème de tri au lot 100. Trois chiffres
n'introduisent aucun coût de lisibilité (`lot-027` n'est pas moins lisible que `lot-27`) et évitent
tout second renommage futur.

Exemple : `tickets-tdd-lot-027-....md`, `tickets-tdd-lot-028-....md`.

Objectif : retrouver le lot concerné depuis le nom de fichier seul, avec un tri qui respecte
l'ordre chronologique réel des lots, sans jamais nécessiter de renommage futur.

### 4. Identifiant des sous-tickets à l'intérieur d'un lot

Pour les lots lettrés (F à Z), l'identifiant d'un sous-ticket est la concaténation directe
`{lettre}{numéro}` (ex. `X6`, `Y1`, `V13`) — déjà unique dans toute la documentation du projet
sans ambiguïté possible, puisqu'aucune autre lettre de lot ne porte ce numéro.

Pour les lots numériques (à partir de 027), la concaténation directe ne fonctionne plus sans
séparateur : `286` serait ambigu (lot 28 sous-ticket 6 ? lot 2 sous-ticket 86 ? lot 286 seul ?).
L'identifiant devient donc **`{numéro-de-lot}.{numéro-de-sous-ticket}`**, séparés par un point,
le numéro de lot étant écrit **sans les zéros de tête** dans cet identifiant (contrairement au nom
de fichier, qui reste zéro-paddé à trois chiffres pour le tri) :

- Nom de fichier : `tickets-tdd-lot-028-....md` (zéro-paddé, pour le tri alphabétique)
- Identifiant de sous-ticket dans le corps du document : `28.0`, `28.1`, ... `28.13` (sans les
  zéros de tête, pour la lisibilité)

Cette double notation (fichier zéro-paddé / sous-ticket non paddé) n'est pas une incohérence :
chaque forme sert un objectif différent — le tri pour le nom de fichier, l'identification unique
et lisible pour le sous-ticket. Le point est obligatoire dans l'identifiant de sous-ticket (jamais
`28-0` ni `280`) pour éviter toute confusion avec un simple numéro de lot ou de ligne.

## En cas de doute
Si un document sert à répondre "où en est-on maintenant" → catégorie 1, pas de date.
Si un document sert à répondre "qu'est-ce qui était vrai le jour où on a vérifié" → catégorie 2, daté.
