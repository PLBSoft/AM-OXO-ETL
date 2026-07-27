# Tickets TDD — Lot 032 : détection d'incohérence de TYPE dans les tâches multiples PROCEDURE

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Premier lot
consacré à une anomalie de saisie observée dans les données client, pas à une évolution
fonctionnelle demandée.*

**Origine** : constat de Simon sur le fichier réel `Dossier_de_MaD_IDL__C7401.xlsx`, feuille
PROCEDURE, tâche multiple factice **"10-MISE EN SERVICE DU COMPRESSEUR"** (tâches 49 à 88).
La colonne TYPE (alias `TypeTacheMultiple.Code`, `TM_PROC_MAD`/`TM_PROC_REL`) y présente 3 runs
consécutifs : `REL` (tâches 49–72), `MAD` (tâches 73–78), `REL` (tâches 79–88). Le libellé de la
tâche 73 ("Ouvrir V2 (vanne refoulement) et dépose prolock") suit le même gabarit rédactionnel que
la tâche 72 ("Ouvrir V1 (vanne aspiration) et dépose prolock"), ce qui rend hautement probable une
erreur de saisie côté client (copier-coller de ligne, TYPE non mis à jour) plutôt qu'une réalité
métier. Une fois importé dans l'app legacy AvancementRecette, corriger cette erreur après coup
serait laborieux, potentiellement impossible sans réimport complet (perte de synchronisation
MAUI, perte de données) — d'où le besoin d'un avertissement précoce, à l'import.

**Vérification de calibration** (faite en amont de ce ticket, sur les 3 fixtures réelles) : sur
l'ensemble des tâches multiples factices des 3 fichiers (C7401, D8570, G6306B), **cette section
est la seule** à présenter plus d'un run de TYPE — toutes les autres sont parfaitement homogènes.
Aucun contre-exemple disponible pour calibrer un faux positif de transition légitime.

**Conventions déjà en place à respecter** : xUnit 2.9.3 + FluentAssertions 7.x + Moq ; politique
d'erreur non bloquante déjà en place (`modele-domaine-import-profile.md` §3/§3.1/§3.2, déjà
exercée par les cas `"VANNE"` et `"POINT FEU"`/`"POINT DE FEU"`) ; règle ad hoc PROCEDURE câblée en
dur dans `ProcedureExtractionService`, pas généralisée dans le catalogue (`ImportProfile`) — voir
`modele-domaine-import-profile.md`, section "Hors catalogue".

---

## Décisions actées avec Simon

1. **Portée** : la détection couvre à la fois le cas *sandwich* (run minoritaire encadré par le
   type majoritaire des deux côtés) et le cas *bord de section* (run minoritaire en tout début ou
   toute fin de section, un seul voisin) — ce dernier avec un niveau de confiance moindre
   explicite dans le libellé, car c'est justement le cas qu'un humain relit sans le "voir".
2. **Pas de seuil de longueur minimal** : toute rupture de run est signalée, sans exception —
   un seuil produirait une détection perçue comme incohérente par l'utilisateur ("pourquoi ce
   cas-là oui, celui-là non").
3. **Câblé en dur** dans `ProcedureExtractionService` — ce n'est pas une règle métier
   paramétrable par profil (`ImportProfile`), c'est un garde-fou qualité des données, cohérent
   avec les autres cas ad hoc déjà câblés en dur sur cette même feuille (règle `Ordre`/ligne
   factice).
4. **Nouveau membre d'`ExtractionErrorCode`** : `TypeIncoherenceDansTacheMultiple` — aucun des 3
   membres existants (`RequiredFieldMissing`, `UnparsableValue`, `UnrecognizedTypeElement`) ne
   correspond sémantiquement à ce cas.
5. **Détermination du type majoritaire** : le type totalisant le plus de tâches sur l'ensemble de
   la section (somme des runs de ce type, pas seulement le run le plus long individuellement).
6. **Cas d'égalité stricte** (deux types — ou plus — se partagent exactement le plus grand nombre
   de tâches dans la section, donc aucune majorité claire) : **un avertissement est émis quand
   même**, mais **sans désigner lequel des types serait le bon** — la section entière est
   remontée comme ambiguë (voir libellé dédié, décision 7). Pas de décomposition en runs
   minoritaire/majoritaire dans ce cas précis : la notion de "minoritaire" n'a pas de sens tant
   qu'aucun type ne domine réellement.
7. **Libellés des avertissements** (`ExtractionError.Message`) — les cas sandwich et bord de
   section portent **le même poids/la même importance**, aucun n'est présenté comme moins fiable
   que l'autre ; seule la description factuelle de la position change :
   - **Sandwich** (run minoritaire encadré des deux côtés) :
     > Incohérence de TYPE détectée dans la tâche multiple "{titre section}" : tâches
     > {N1}–{N2} en {TypeMinoritaire}, encadrées par des tâches en {TypeMajoritaire} — vérifier
     > une possible erreur de saisie.
   - **Bord de section** (run minoritaire avec un seul voisin, en tête ou en fin de section) :
     > Incohérence de TYPE détectée dans la tâche multiple "{titre section}" : tâches
     > {N1}–{N2} en {TypeMinoritaire}, en {début|fin} de section, adjacentes à des tâches en
     > {TypeMajoritaire} — vérifier une possible erreur de saisie.
   - **Égalité stricte** (aucun type majoritaire identifiable) — libellé proposé par Claude, non
     explicitement confirmé par Simon à ce stade — à valider ou amender en 32.0 :
     > Répartition de TYPE ambiguë dans la tâche multiple "{titre section}" : {Type1}
     > ({plages de tâches}) et {Type2} ({plages de tâches}) se partagent la section à parts
     > égales — impossible de déterminer le type correct, vérifier manuellement.
8. **Non bloquant** : aucun impact sur l'extraction elle-même — les `TacheMultiplePivot`
   concernées sont extraites normalement, qu'une incohérence soit détectée ou non. L'avertissement
   est ajouté à `ImportResult.Errors`, au même titre que les cas `"VANNE"`/`"POINT FEU"`.

---

## Hors périmètre explicite

- **Aucune détection basée sur une similarité textuelle** des libellés d'action (`Action`,
  ex. gabarit "Ouvrir Vx ... dépose prolock"). C'est un excellent indice de confirmation humaine
  a posteriori, mais fragile et non déterministe pour une règle testée en TDD — la détection reste
  strictement structurelle (colonne TYPE uniquement).
- **Aucune option de configuration côté `ImportProfile`** pour activer/désactiver ou paramétrer
  cette détection (voir décision 3).
- **Aucun blocage d'import ni d'export** — uniquement un avertissement non bloquant.
- **Aucune correction automatique** du TYPE détecté comme incohérent — la correction reste à la
  charge du client sur son fichier source, à réimporter.
- **Aucune autre feuille que PROCEDURE** n'est concernée par ce lot — le TYPE tel que décrit ici
  (`TM_PROC_MAD`/`TM_PROC_REL`) n'existe que sur cette feuille.
- **Pas de nouvelle table/mécanisme de persistance** — réutilisation stricte d'`ImportResult.Errors`
  existant, pas de canal parallèle (cohérent avec la remarque déjà actée sur Serilog/`SystemLogs`).

---

## 32.0. Investigation préalable (obligatoire avant tout code)

- [ ] Confirmer avec Simon le libellé du cas d'égalité stricte (décision 7, dernier point non
  explicitement validé) avant d'écrire le test correspondant — le reste des libellés et la
  portée (sandwich + bord de section, décisions 1 et 7) sont actés.
- [ ] Localiser le mécanisme actuel de regroupement des tâches par tâche multiple factice dans
  `ProcedureExtractionService` (probablement déjà présent pour construire `TacheMultiplePivot` et
  gérer la règle `Ordre`/ligne factice) — réutiliser cette structure de groupement plutôt que d'en
  recréer une nouvelle en parallèle.
- [ ] Vérifier si `ExtractionErrorCode` est consommé par un `switch` exhaustif ailleurs dans le
  code (Blazor, mapping de message localisé, etc.) qui devrait gérer explicitement le nouveau
  membre `TypeIncoherenceDansTacheMultiple` plutôt que de tomber silencieusement dans un cas
  `default`.
- [ ] Confirmer le format exact attendu pour `ExtractionError.BlockIdentifier` sur ce cas (proposé :
  `"{titre section} (tâches {N1}-{N2})"`, ex. `"10-MISE EN SERVICE DU COMPRESSEUR (tâches 73-78)"`)
  — cohérent avec les formats déjà utilisés pour les autres `ExtractionError` (repère d'Isolement,
  numéro de ligne).
- [ ] Vérifier comment le titre de section ("10-MISE EN SERVICE DU COMPRESSEUR") est actuellement
  extrait/conservé en mémoire pendant le parcours de la feuille (probablement déjà disponible via
  le même mécanisme que `TacheMultiplePivot`) — ne pas re-parser la feuille une deuxième fois pour
  le récupérer.

---

## 32.1. Calcul des runs de TYPE et détermination du type majoritaire

**Comportement attendu** :
- Fonction/méthode pure (testable isolément, sans dépendance à ClosedXML) qui, à partir de la
  séquence ordonnée des couples `(NuméroTâche, Type)` d'une section, retourne la liste des runs
  (`Type`, `NuméroTâcheDébut`, `NuméroTâcheFin`) — comparaison normalisée (`Trim()` + insensible à
  la casse, cohérent avec la recommandation transverse §7 de `spec-extraction-fichier-source-oxo.md`).
- Si un seul run → aucune anomalie, liste vide en sortie.
- Sinon, détermine le type totalisant le plus de tâches sur l'ensemble de la section.
- **Si ce total maximal est partagé par au moins deux types** (égalité stricte, décision 6) :
  ne pas décomposer en runs minoritaire/majoritaire — retourner un résultat de nature différente
  (anomalie "section ambiguë", portant la liste des types concernés et leurs plages respectives),
  distinct du cas normal à runs minoritaires.
- Sinon (une majorité claire existe) : chaque run dont le type diffère du type majoritaire est un
  run minoritaire à décomposer (voir 32.2).

**Tests** (xUnit, purement unitaires, pas de fixture Excel) :
- Section à un seul run → aucune anomalie retournée.
- Section reproduisant exactement le pattern C7401 (24 REL / 6 MAD / 10 REL) → type majoritaire
  = REL, un seul run minoritaire identifié (MAD, tâches correspondantes).
- Section avec run minoritaire en tête (ex. 3 MAD / 20 REL) → run minoritaire = premier run,
  aucun voisin avant lui.
- Section avec run minoritaire en fin (ex. 20 REL / 3 MAD) → run minoritaire = dernier run, aucun
  voisin après lui.
- Section avec deux runs minoritaires distincts (ex. 3 MAD / 20 REL / 2 MAD) → deux anomalies
  distinctes retournées, chacune avec sa propre plage.
- Section à répartition strictement égale (ex. 10 REL / 10 MAD) → une anomalie de type "section
  ambiguë" est retournée (et non une liste de runs minoritaires), portant les deux types et leurs
  plages respectives, sans qu'aucun des deux ne soit désigné comme correct.

---

## 32.2. Classification sandwich vs bord de section

**Comportement attendu** : pour chaque run minoritaire identifié en 32.1 (cas à majorité claire
uniquement — le cas "section ambiguë" de 32.1 n'entre jamais dans cette étape), déterminer s'il
possède un run voisin avant **et** après dans la section (sandwich) ou un seul voisin (bord de
section) — utilise uniquement la position du run dans la séquence, aucune nouvelle lecture de la
feuille. Les deux classifications ont le même poids : aucune n'est traitée comme moins fiable
que l'autre, seule la description de la position diffère (voir décision 7).

**Tests** (xUnit) :
- Run minoritaire encadré des deux côtés → classifié "sandwich".
- Run minoritaire en tête de section (aucun run avant) → classifié "bord".
- Run minoritaire en fin de section (aucun run après) → classifié "bord".

---

## 32.3. Construction du message et câblage dans `ExtractionError`

**Comportement attendu** :
- Ajout du membre `TypeIncoherenceDansTacheMultiple` à `ExtractionErrorCode`.
- Pour chaque run minoritaire détecté (32.1) et classifié (32.2), construction d'un
  `ExtractionError` :
  - `Sheet` = `"PROCEDURE"`
  - `BlockIdentifier` = format confirmé en 32.0
  - `Code` = `ExtractionErrorCode.TypeIncoherenceDansTacheMultiple`
  - `Message` = libellé sandwich ou bord (décision 7), avec les valeurs interpolées (titre
    section, plage de tâches, type minoritaire, type majoritaire, "début"/"fin" pour le cas bord).
- Pour une section "ambiguë" (égalité stricte, 32.1), construction d'un unique `ExtractionError`
  pour toute la section (pas un par type) :
  - `Sheet` = `"PROCEDURE"`
  - `BlockIdentifier` = couvre la section entière (ex. `"10-MISE EN SERVICE DU COMPRESSEUR"`,
    sans plage de tâches puisqu'aucune n'est désignée comme anormale)
  - `Code` = `ExtractionErrorCode.TypeIncoherenceDansTacheMultiple` (même code — c'est la même
    famille d'anomalie, seule la présentation diffère)
  - `Message` = libellé "égalité stricte" (décision 7, à confirmer en 32.0), énumérant chaque
    type concerné et sa/ses plage(s) de tâches, sans désigner de type correct.
- Câblage dans `ProcedureExtractionService` : chaque `ExtractionError` produit est ajouté à la
  collection `Errors` du résultat, au même titre que les autres avertissements non bloquants
  déjà en place (aucun impact sur l'extraction des `TacheMultiplePivot` eux-mêmes).

**Tests** (xUnit, contre la fixture réelle **C7401** — ground truth) :
- `ProcedureExtractionService` appliqué à `Dossier_de_MaD_IDL__C7401.xlsx` produit exactement un
  `ExtractionError` de code `TypeIncoherenceDansTacheMultiple`, avec `BlockIdentifier` couvrant
  les tâches 73–78, et un message correspondant au libellé "sandwich" (type minoritaire MAD,
  type majoritaire REL).
- Toutes les `TacheMultiplePivot` 73 à 78 sont malgré tout présentes et correctement extraites
  dans le résultat (non-régression : l'anomalie ne bloque rien).
- Test de non-régression sur les fixtures **D8570** et **G6306B** : aucun `ExtractionError` de
  code `TypeIncoherenceDansTacheMultiple` n'est produit (toutes leurs sections sont homogènes) —
  garde-fou explicite contre un faux positif introduit par erreur d'implémentation.
- Test synthétique dédié (pas de fixture réelle disponible) pour le cas "bord de section" : à
  construire à partir d'un classeur minimal en mémoire (ou d'un objet intermédiaire, selon la
  structure interne de `ProcedureExtractionService` déterminée en 32.0) reproduisant un run
  minoritaire en tête ou en fin de section — vérifie le libellé "bord" exact.
- Test synthétique dédié pour le cas "égalité stricte" (pas de fixture réelle disponible) :
  section synthétique à répartition égale entre deux types — vérifie qu'un seul
  `ExtractionError` est produit pour toute la section (pas un par run), que son message énumère
  bien les deux types et leurs plages sans désigner de type correct, et que toutes les
  `TacheMultiplePivot` de la section sont malgré tout extraites normalement.

---

## Note d'efficacité d'implémentation

1. Traiter 32.1 et 32.2 comme des fonctions pures indépendantes de ClosedXML, testables sans
   fixture Excel — l'essentiel de la couverture de tests (cas limites, égalité stricte, runs
   multiples) doit être validé à ce niveau, pas via des tests d'intégration lourds contre les
   fixtures réelles.
2. Ne traiter 32.3 (câblage réel + fixtures C7401/D8570/G6306B) qu'une fois 32.1/32.2 verts —
   évite de déboguer simultanément la logique de détection et la lecture Excel.
3. Ne pas toucher à `TacheMultiplePivot`, à `EquipementPivot`, ni à aucune autre règle déjà
   câblée dans `ProcedureExtractionService` (règle `Ordre`/ligne factice, alias `TypeTacheMultiple.Code`)
   — ce lot ajoute une collecte d'avertissements en parallèle, il ne modifie aucune règle
   d'extraction existante.
4. Si 32.0 fait apparaître que le regroupement par section n'existe pas encore sous une forme
   directement réutilisable, le construire comme une étape discrète et testée séparément avant
   32.1, plutôt que de l'improviser inline dans la même méthode que le calcul des runs.
