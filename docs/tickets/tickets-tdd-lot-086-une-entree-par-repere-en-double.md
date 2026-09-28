# Tickets TDD — Lot 086 : une seule entrée `DuplicateRepere` par repère en double

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 28/09 à partir
de la demande complémentaire de l'équipe AlphaMaintenance « `DuplicateRepere` : une seule entrée par repère
en double » (fichier reçu hors dépôt, résumé en §0). Suite directe du lot 085
(`tickets-tdd-lot-085-rejet-reperes-en-double.md`), dont il modifie les décisions D3 et D7. Investigation
faite sans rien implémenter. **Décisions du §2 validées par Simon le 28/09.***

---

## 0. La demande (résumé)

Le lot 085 est publié et vérifié côté AlphaMaintenance : LRSJ2M est rejeté dès l'appel, rien n'est écrit
en base. Mais pour un seul doublon, l'utilisateur voit deux lignes d'erreur identiques, puisque le message
cite déjà toutes les lignes concernées. Le demandeur avait lui-même demandé une entrée par ligne ; il
revient dessus.

Demandé : **une entrée `errors[]` par repère en double**, quel que soit le nombre de lignes.

| Champ | Contenu demandé |
| :--- | :--- |
| `sheet` | Feuille de la **première** ligne concernée (ordre de traitement) |
| `blockIdentifier` | Ligne Excel de cette première ligne |
| `code` | `DuplicateRepere` (inchangé) |
| `message` | Inchangé : le repère et **toutes** les lignes, avec leur feuille |
| `extractedValue` | Le repère généré (inchangé) |

Reste du contrat inchangé : 422, fichier entier rejeté, pas de fichier cible, une entrée par repère si le
fichier en contient plusieurs. `X-Warning-Count` et l'historique (`warnings`) suivent le nouveau nombre.
Aucune modification côté AlphaMaintenance. Questions : faisabilité et délai ; autre convention préférée
pour `sheet`/`blockIdentifier` ?

---

## 1. Constat

- Tout se joue dans `DuplicateRepereDetector.ToErrors` (`Application/Extraction/Oxo/Elements/`) :
  aujourd'hui un `ExtractionError` par élément du groupe. Il suffit d'en produire un seul, à partir du
  premier élément du groupe. Le message ne change pas.
- L'ordre de traitement est déjà celui voulu : feuilles dans l'ordre du pipeline, puis ligne ; `GroupBy`
  garde l'ordre de première apparition des groupes et des éléments. « Première ligne » = premier élément du
  groupe.
- `extractedValue` : aujourd'hui le repère de **chaque** ligne, tel que généré (casse et espaces
  compris). Avec une seule entrée, c'est celui de la première ligne. Pour `LRS-V1` / ` lrs-v1 `, l'entrée
  porte `LRS-V1`, et le message cite `LRS-V1` (clé du groupe, rognée) : cohérent.
- Le journal d'orchestration (`ImportPipelineOrchestrator.Run`) écrit « {N} element(s) with a duplicate
  repère » avec `N` = nombre d'entrées. Avec une entrée par repère, `N` devient le nombre de repères en
  double : le libellé doit suivre.
- Aucun changement ailleurs : API, archive et pages de test affichent les entrées reçues.

Effet sur les fixtures (profil standard) :

| Dossier | Entrées aujourd'hui | Après |
| :--- | :--- | :--- |
| LRS4504 | 2 | 1 (DIVERS / 9) |
| D8570 | 6 | 3 (ISOLEMENT / 117 pour V4, PLATINES / 17 pour PT1, DIVERS / 15 pour V7) |
| E6431A | 4 | 2 |
| RANGEE N°1 | 2 | 1 |

---

## 2. Décisions (validées par Simon le 28/09)

| # | Question | Proposition |
| :--- | :--- | :--- |
| D1 | Convention `sheet`/`blockIdentifier` (question 2 du demandeur) | **Celle du demandeur** : feuille et ligne de la première ligne concernée. Elle ne perd rien (le message reste complet) et c'est l'ordre dans lequel l'utilisateur parcourt son fichier. Seule alternative sérieuse, `blockIdentifier` = toutes les lignes (« 9, 12 ») : écartée, elle mélangerait des lignes de feuilles différentes sous un seul `sheet`. |
| D2 | `extractedValue` | Repère de la première ligne, tel que généré |
| D3 | Libellé du journal | « {N} duplicated repère(s) » |

Remplace les décisions D3 (« une entrée par ligne ») et D7 (ordre des entrées) du lot 085 ; les autres
restent valables.

---

## 3. Tickets

Un commit par ticket. Tests filtrés sur la classe en cours ; suite complète en 086.2.

### 086.1 — Une entrée par repère

**Rouge** (`DuplicateRepereDetectorTests`, réécrits sur place — c'est un changement de comportement voulu,
pas une régression masquée) :
- deux lignes d'une même feuille → **une** entrée : feuille et ligne de la première, message citant les
  deux lignes, `extractedValue` = repère de la première ;
- même repère sur deux feuilles → une entrée portant la feuille de la première (ISOLEMENT), message citant
  les deux feuilles ;
- casse/espaces : une entrée, `extractedValue` = `LRS-V1` (première ligne) ;
- groupe de 3 → une entrée, message citant les 3 lignes ;
- deux groupes → deux entrées, dans l'ordre de première apparition.

`ImportPipelineOrchestratorTests` : le cas « deux feuilles, même repère » attend une seule entrée
(ISOLEMENT / 116).

**Vert** : `ToErrors` renvoie un seul `ExtractionError` construit sur `group.First()` ; mise à jour du
commentaire de tête du détecteur. Libellé du journal (D3) dans l'orchestrateur.

### 086.2 — Tests d'intégration, instantanés, suite complète

Mêmes fichiers qu'au lot 085.7, assertions ajustées au nouveau nombre d'entrées (§1) :
- `DefaultProfileSeederPipelineIntegrationTests` : D8570 → 3 entrées (lignes du §1), LRS4504 → 1 ;
- `ImportPipelineOrchestratorIntegrationTests` (D8570, 3 entrées) ;
- `ImportPipelineOrchestratorLoggingIntegrationTests` : « 3 duplicated repère(s) » ;
- `OxoProcessEndpointTests` : 1 entrée, `X-Warning-Count` = `1`, archive avec 1 avertissement ;
- `ImportProfileTestTests` / `ExportProfileTestTests` : 1 ligne dans le bloc « Rejeté » ;
- instantanés (`UPDATE_FIXTURE_SNAPSHOTS=1`) : seuls les 4 dossiers rejetés changent, une ligne par
  repère ;
- suite complète de la solution.

### 086.3 — Documentation et réponse

- `CLAUDE.md` (section Web API et puce du lot 085) et `spec-extraction-fichier-source-oxo.md` : « une entrée
  par repère en double ».
- Note en tête du ticket 085 : D3/D7 remplacées par ce lot.
- Réponse au demandeur (§4).

---

## 4. Réponse proposée au demandeur

1. **Faisabilité** : oui, lot 086 ; changement limité à la construction des entrées, disponible à la
   prochaine publication de l'API.
2. **Convention** : nous retenons la vôtre (feuille et ligne de la première ligne concernée, dans l'ordre
   de traitement ; message complet inchangé ; `extractedValue` = repère de cette première ligne).

---

## 5. Hors périmètre

- Tout le reste du contrat du lot 085 (périmètre, comparaison, rejet total, 422, pas de fichier cible).
- Fusion de doublons.
- Affichage côté BlazorAdmin (inchangé : une ligne par entrée reçue).
