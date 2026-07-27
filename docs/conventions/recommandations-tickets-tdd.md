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

## 6. Structure attendue d'un ticket TDD

Chaque ticket doit inclure :
- Le comportement précis à implémenter (un seul par ticket)
- Les cas de test bUnit à écrire (rouge)
- L'implémentation minimale attendue (vert)
- Les pistes de refactor à considérer, si pertinent
- Toute contrainte d'effort/mode à appliquer selon l'étape (cf. section 2)
