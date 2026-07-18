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

## En cas de doute
Si un document sert à répondre "où en est-on maintenant" → catégorie 1, pas de date.
Si un document sert à répondre "qu'est-ce qui était vrai le jour où on a vérifié" → catégorie 2, daté.
