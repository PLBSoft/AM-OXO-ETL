# Tickets TDD — Lot 085 : rejeter un fichier dont deux éléments ont le même repère

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 28/09 à partir
de la demande de l'équipe AlphaMaintenance « Rejeter les repères d'isolement en double dans
`POST /api/oxo/process` » (fichier reçu hors dépôt, résumé en §0). Investigation faite sans rien
implémenter. **Décisions du §3 validées par Simon le 28/09 (D0 = A, D1-D7 comme proposé).***

> **Lot 086 (28/09)** : à la demande du même demandeur, une seule entrée par repère en double (feuille et
> ligne de la première ligne concernée, message inchangé). Les décisions D3 (une entrée par ligne) et D7
> (ordre des entrées) ci-dessous sont remplacées ; voir `tickets-tdd-lot-086-une-entree-par-repere-en-double.md`.

---

## 0. La demande (résumé)

Un import du dossier `Dossier.de.MaD.IDL.-.LRSJ2M.xlsx` plante dans AlphaMaintenance
(`/OXO/ImportProgress`, « La séquence contient plusieurs éléments »). La feuille DIVERS contient deux
lignes d'identification `LRSJ2M` (types INSTRUMENTATION et ZERO ENERGIE, même désignation). AM-OXO-ETL
répond 200 et le fichier généré contient deux lignes `LRSJ2M-LRSJ2M` dans `Enfants`. AlphaMaintenance
identifie un isolement par son repère (`Trim().ToLower()`) : il crée deux isolements, la recherche suivante
échoue, la base reste dans un état partiel à corriger à la main.

Demandé :

1. Contrôle sur **toutes les feuilles d'éléments confondues** (c'est le repère final qui doit être unique
   dans `Enfants`).
2. Comparaison **insensible à la casse et aux espaces de début/fin**.
3. Réponse **422** au format `errors[]` existant, **une entrée par ligne en double** (pas seulement la
   deuxième) : `sheet`, `blockIdentifier`, `code` (nouveau membre, par ex. `DuplicateRepere`), `message`,
   `extractedValue` (le repère en double).
4. Aucun fichier cible généré.
5. Question : inclure les lignes « en suppression » ? (proposition : oui).

Aucune modification côté AlphaMaintenance : `OXOImportService` affiche déjà chaque entrée d'un 422 et
s'arrête avant toute écriture.

---

## 1. Constat (vérifié dans le code et sur les instantanés des 14 fixtures)

### 1.1 Où naît le repère, et l'absence de contrôle

- `ElementSheetExtractionService.Extract` construit `repere = {repereEcho}-{Identification}` pour chaque
  bloc lu (concaténation brute, sans nettoyage).
- `ImportPipelineOrchestrator.Run` concatène les éléments des 5 feuilles (ISOLEMENT, PLATINES,
  ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS, dans cet ordre) sans aucun contrôle d'unicité.
- `SheetGenerationEngine` rattache les points par `ParentRepere == Repere` : deux éléments de même repère
  reçoivent chacun **l'union** des points des deux. Le lot I5 l'avait déjà remarqué (« un repère généré
  n'est pas une clé sûre ») sans en tirer de règle.

### 1.2 Des doublons existent déjà dans 4 des 14 fixtures réelles

Relevé sur `tests/ExcelETL.Infrastructure.Tests/Snapshots/*.txt` (sortie du profil standard, comparaison
rognée et insensible à la casse) :

| Fixture | Repère | Lignes (feuille · type · désignation) | Nature |
| :--- | :--- | :--- | :--- |
| LRS4504 | `LRS4504-LRS4504` | DIVERS · INSTRUMENTATION · TRANSMETTEUR DE NIVEAU F4501 / DIVERS · ZERO ENERGIE · idem | **Le cas de la demande** (même contenu que LRSJ2M) : un seul appareil, deux types |
| D8570 | `D8570-V4` | ISOLEMENT · VANNE · (vide) / DIVERS · ZERO ENERGIE · Purge condensats vers égout chimique | Un appareil décrit sur deux feuilles |
| E6431A | `E6431A-P1`, `E6431A-P2` | PLATINES · PLATINE · ENTREE E6431 / DIVERS · ZERO ENERGIE · Bride aéro | Idem, deux fois |
| D8570 | `D8570-V7` | DIVERS · ZERO ENERGIE · Purge condensat vers égoût (×2, identiques) | Ligne recopiée |
| RANGEE N°1 | `RANGEE N°1-PT5` | PLATINES · PLATINE · LIGNE N2 GENERALE (×2, identiques) | Ligne recopiée |
| D8570 | `D8570-PT1` | PLATINES · AMONT HV8573 / PLATINES · LIGNE DE TETE D8570 | Deux platines différentes, même identification |

**Conséquence** : la règle telle que demandée rejette 4 dossiers réels sur 14, dont **D8570, le fichier de
référence** de la plupart des tests d'intégration (24 fichiers de test le citent). Voir D0.

### 1.3 Il n'existe pas de lignes « en suppression » à l'extraction

L'extraction ne lit aucune notion de suppression : la colonne `SUPPRESSION` du fichier généré est une
colonne sans source (Parents/Enfants) ou une constante `N` (tâches multiples). Toutes les lignes lues sont
des éléments à créer. La question 5 de la demande est sans objet côté AM-OXO-ETL (voir D6).

### 1.4 Le mécanisme de rejet existe déjà

`ImportResult.Equipement is null` signifie « fichier rejeté » (modèle §3.1) et tout le reste en découle
sans code nouveau :

- `ProcessOxoFileService` : pas de génération, archivage `Rejected` avec ses `Warnings` (lot 072) ;
- `OxoController` : 422, `Extensions["errors"]` avec `Sheet`/`BlockIdentifier`/`Code`/`Message`/
  `ExtractedValue`, en-tête `X-Warning-Count` ;
- `BatchImportProcessing` (pages de test BlazorAdmin) : statut « Rejeté » et liste des erreurs.

`ExtractionErrorLogging` journalise en `Error` tout code non listé comme non bloquant : un nouveau code
bloquant n'a rien à y ajouter.

### 1.5 L'élément extrait ne connaît pas sa ligne source

`IsolementPivot` ne porte pas la ligne de son bloc (contrairement à `TacheMultiplePivot.LigneSource`,
lot 069). Le `RepeatingBlock.StartRow` est connu dans `ElementSheetExtractionService` mais perdu ensuite.
Or le contrôle est transverse aux feuilles : il se fait après l'agrégation, il faut donc transporter la
ligne. `BlockIdentifier` vaut aujourd'hui la ligne de début de bloc pour les autres erreurs de ces feuilles
(`RequiredFieldMissing`).

Remarque : la colonne « N° » citée dans la demande (1, 2…) n'est lue par aucun profil ; la ligne Excel du
bloc est la seule référence disponible, et c'est celle que l'utilisateur retrouve dans son fichier.

---

## 2. Point d'architecture à trancher d'abord (D0)

La demande est cohérente avec le modèle d'AlphaMaintenance (le repère est la clé d'un isolement) et un
rejet explicite vaut mieux qu'un 200 qui casse l'import en aval. Mais le §1.2 montre que les doublons ne
sont pas tous des erreurs de saisie : dans 3 cas sur 6, **un même appareil est décrit sur deux feuilles
ou avec deux types** (une vanne à isoler qui est aussi une purge zéro énergie, une platine qui est aussi
une bride zéro énergie). Rejeter ces dossiers oblige l'utilisateur à renommer une identification dans son
fichier source alors que les deux lignes désignent bien le même objet.

| Option | Effet sur les 4 fixtures | Pour | Contre |
| :--- | :--- | :--- | :--- |
| **A. Rejet de tout doublon** (la demande) | 4 rejetées | Simple, sans règle de fusion à inventer ; AlphaMaintenance ne sait représenter qu'un type par isolement | L'utilisateur doit corriger des fichiers qui décrivent une réalité ; forte reprise des tests (D8570) |
| B. Fusion des doublons identiques, rejet des autres | V7 et PT5 fusionnés ; LRS4504, V4, P1/P2, PT1 rejetés | Tolère la ligne recopiée | Règle « identique » à définir (quels champs ?) ; gagne peu |
| C. Fusion par repère (un élément, union des points) | Aucune rejetée | Aucune correction de fichier | Quel type/désignation garder ? Choix arbitraire, masque PT1 (vraie erreur) ; à négocier avec AlphaMaintenance |

**Recommandation : A**, comme demandé. C'est le seul comportement qui ne choisit pas à la place de
l'utilisateur entre deux données contradictoires, et il est réversible (on pourra assouplir plus tard vers
B ou C sans casser le contrat). **À confirmer avec le demandeur, preuves du §1.2 à l'appui** : les
dossiers D8570, E6431A, LRS4504 et RANGEE N°1, tels quels, seront refusés.

---

## 3. Décisions (validées par Simon le 28/09)

| # | Question | Proposition |
| :--- | :--- | :--- |
| D0 | Rejet, fusion partielle ou fusion (§2) ? | **A, rejet de tout doublon** (validé) |
| D1 | Nom du code | `ExtractionErrorCode.DuplicateRepere` |
| D2 | Comparaison | `Trim` + `OrdinalIgnoreCase` (même effet que `Trim().ToLower()` côté AlphaMaintenance, sans dépendre de la culture) |
| D3 | Contenu d'une entrée | `Sheet` = feuille de la ligne ; `BlockIdentifier` = ligne Excel de début de bloc (comme `RequiredFieldMissing`) ; `ExtractedValue` = le repère tel que généré pour cette ligne ; `Message` = « Repère « LRS4504-LRS4504 » en double (DIVERS ligne 18, DIVERS ligne 21) : chaque élément doit avoir une identification unique. », la liste citant toutes les lignes du groupe dans l'ordre de traitement |
| D4 | Autres entrées d'un fichier rejeté | Uniquement les doublons (les avertissements non bloquants sont écartés, comme pour un rejet PROCEDURE). `ImportResult(null, [], [], [], doublons)` |
| D5 | Où contrôler | En fin de `ImportPipelineOrchestrator.Run`, après agrégation : l'API et les deux pages de test donnent le même verdict sans second point d'appel. Contre-option écartée : un validateur appelé par `ProcessOxoFileService` et `BatchImportProcessing`, qui garderait les tests d'orchestrateur intacts mais crée deux appels à tenir synchronisés |
| D6 | Lignes « en suppression » | Sans objet (§1.3) ; toutes les lignes lues sont contrôlées |
| D7 | Ordre des entrées | Groupes dans l'ordre de première apparition, lignes dans l'ordre de traitement (feuilles dans l'ordre du pipeline, puis ligne) |

---

## 4. Tickets

Un commit par ticket, dans l'ordre. Tests filtrés sur la classe en cours ; suite complète seulement en
085.7.

### 085.0 — Validation des décisions

- Faire valider D0 à D7 par Simon (et D0 par le demandeur).
- Si D0 ≠ A : réécrire 085.3 à 085.6 avant de coder.
- Récupérer, si possible, le fichier `LRSJ2M` d'origine ; à défaut, la fixture `LRS4504` reproduit le cas
  (mêmes lignes DIVERS) et sert de fixture de référence du lot.

### 085.1 — Domaine : code d'erreur et ligne source de l'élément

**Rouge** (`ExcelETL.Domain.Tests`) :
- `IsolementPivot` expose `LigneSource` (int), valeur passée au constructeur, 0 par défaut ; prise en
  compte dans `Equals`/`GetHashCode`.
- `ExtractionErrorCode.DuplicateRepere` existe.

**Vert** : paramètre optionnel en dernière position du constructeur (comme `sourceSheetName`, lot 070 :
les appels positionnels existants ne bougent pas).

### 085.2 — La ligne source est renseignée à l'extraction

**Rouge** (`ElementSheetExtractionServiceTests`, `Mock<IWorkbookReader>`) : deux blocs lus, chaque
élément porte le `StartRow` de son bloc ; un bloc écarté (`RequiredFieldMissing`) ne décale pas la ligne
du suivant.

**Vert** : `ElementSheetExtractionService` passe `block.StartRow`.

**Non-régression** : les instantanés `FixtureOutputSnapshotTests` n'affichent pas `LigneSource` ; s'ils
l'affichaient, régénérer et relire le diff (seule cette colonne doit changer).

### 085.3 — Détecteur de doublons (pur)

Nouveau `DuplicateRepereDetector` (`Application/Extraction/Oxo/Elements/`, statique, sans ClosedXML) :
`IReadOnlyList<ExtractionError> Detect(IReadOnlyList<IsolementPivot> elements)`.

**Rouge** (`ExcelETL.Application.Tests`) :
- aucun doublon → liste vide ;
- deux éléments de même repère sur une même feuille → 2 entrées, une par ligne ;
- même repère sur deux feuilles différentes → 2 entrées, chacune avec sa feuille ;
- `LRS-V1` et ` lrs-v1 ` → doublons ; `LRS-V1` et `LRS-V10` → pas de doublon ;
- groupe de 3 → 3 entrées, chaque message cite les 3 lignes ;
- deux groupes distincts → entrées dans l'ordre D7 ;
- `ExtractedValue` = repère brut de la ligne ; `Code` = `DuplicateRepere` ; message exact (D3).

### 085.4 — L'orchestrateur rejette le fichier

**Rouge** (`ImportPipelineOrchestratorTests`, services simulés) :
- deux feuilles renvoient un élément de même repère → `Equipement` nul, `Isolements`/`Points`/
  `TachesMultiples` vides, `Errors` = uniquement les entrées `DuplicateRepere` (D4) ;
- sans doublon, résultat inchangé (non-régression explicite) ;
- le rejet PROCEDURE reste prioritaire (les feuilles d'éléments ne sont toujours pas lues).

**Vert** : appel de `DuplicateRepereDetector.Detect` après agrégation ; si non vide, `LogWarning` (même
forme que le rejet PROCEDURE) et retour du résultat rejeté. Mettre à jour le commentaire de tête de la
classe.

### 085.5 — Bout en bout sur la fixture réelle

**Rouge** :
- `ImportPipelineOrchestratorIntegrationTests` (profil lu depuis `DefaultProfileSeeder`) : `LRS4504` →
  rejeté, 2 entrées `DuplicateRepere` feuille DIVERS, `ExtractedValue` = `LRS4504-LRS4504`, lignes des
  deux blocs différentes.
- `OxoProcessEndpointTests` (`WebApplicationFactory`) : même fichier → 422, `errors[]` avec les 5 champs,
  pas de fichier cible écrit, enregistrement d'archive `Rejected` portant les 2 entrées.

**Vert** : aucun code attendu (§1.4). Si un test échoue, c'est un vrai trou : le traiter ici.

### 085.6 — Pages de test BlazorAdmin

**Rouge** (`ImportProfileTestTests`, `ExportProfileTestTests`) : `LRS4504` apparaît « Rejeté » avec les
deux entrées ; sur `ExportProfileTest`, le bouton de génération n'est pas proposé pour ce fichier.

**Vert** : aucun code attendu.

### 085.7 — Reprise des tests et instantanés existants

Selon D0 = A, D8570, E6431A, LRS4504 et RANGEE N°1 deviennent des fichiers rejetés.

- Recenser les tests qui les passent par l'orchestrateur ou la génération (`grep -rl` sur les 4 noms dans
  `tests/`). Les tests d'une feuille isolée (`ElementSheetExtractionService` seul) ne changent pas.
- Pour chaque test d'orchestration/génération sur D8570 : le basculer sur un fichier sans doublon qui
  couvre le même cas, ou le réécrire en test de rejet. **Ne pas affaiblir une assertion** : si un cas
  n'est couvert que par D8570 (par ex. l'avertissement `VANNE`, lot 055), le garder au niveau de la
  feuille ISOLEMENT seule.
- Régénérer les instantanés (`UPDATE_FIXTURE_SNAPSHOTS=1`) ; relire le diff : seuls les 4 dossiers
  changent, en « rejeté ».
- Suite complète de la solution.

### 085.8 — Documentation et réponse

- `CLAUDE.md` (section Web API, `POST /api/oxo/process`) : nouveau cas de 422.
- `docs/reference/spec-extraction-fichier-source-oxo.md` : règle d'unicité du repère.
- Réponse au demandeur (§5).

---

## 5. Réponse proposée au demandeur

1. **Faisabilité** : oui, sous la forme demandée, lot 085. Attention : 4 de nos 14 dossiers réels de test
   seraient refusés (tableau §1.2), dont des cas où un même appareil apparaît sur deux feuilles
   (ex. V4 en ISOLEMENT et en DIVERS). Confirmez-vous que ces dossiers doivent être corrigés à la source ?
2. **Code** : `DuplicateRepere`.
3. **Lignes en suppression** : AM-OXO-ETL ne lit aucune notion de suppression dans le fichier source ;
   toutes les lignes sont contrôlées.
4. `blockIdentifier` contiendra la ligne Excel du bloc (la colonne « N° » n'est pas lue).

---

## 6. Résultat (28/09)

Livré en 5 commits : `09ae108` (085.1), `5b9e5d4` (085.2), `f06f96a` (085.3), `15f724e` (085.4 à 085.7,
regroupés pour ne jamais pousser une suite rouge : le contrôle seul casse les tests sur D8570), puis la
documentation (085.8).

- **Lignes réelles** (profil standard) : LRS4504 → DIVERS lignes 9 et 12 (les deux premiers blocs, les
  « lignes 1 et 2 » de la demande) ; D8570 → V4 ISOLEMENT 117 / DIVERS 9, PT1 PLATINES 17 / 177,
  V7 DIVERS 15 / 18.
- **085.5/085.6 sans code de production** : le chemin de rejet existant a suffi (422, archive `Rejected`
  avec les entrées, statut « Rejeté » et pas de bouton de génération sur les pages de test).
- **085.7, reprise des tests** (aucune assertion affaiblie) : tests d'orchestration et de génération sur
  D8570 basculés sur G4010A (tâches MAD seules, un avertissement ISOLEMENT « PROLOCK », un ZERO ENERGIE en
  DIVERS) ; `ImportSheetUsageTests` sur G6306B (éléments sur les 5 feuilles, aucun doublon) ; les tests
  propres à D8570 réécrits en tests de rejet. Le cas « deux avertissements PROLOCK + VANNE » reste couvert au
  niveau de la feuille ISOLEMENT (`IsolementExtractionServiceIntegrationTests`). Instantanés : seuls les
  4 dossiers du §1.2 changent, en « rejeté ».
- **Suites complètes** : Domain 434, Application 283, Infrastructure 292, WebAPI 78, Hosting 15,
  BlazorAdmin 1603, toutes vertes.
- **Écart à la demande** : l'affichage de la page de test BlazorAdmin reste `{feuille} / {ligne}: {message}`
  (sans le code), comme pour tout rejet ; l'API renvoie bien les 5 champs.

---

## 7. Hors périmètre

- Fusion de doublons (options B/C), sauf changement de D0.
- Unicité d'autre chose que le repère des éléments (tâches multiples, repère de l'équipement parent).
- Nettoyage du repère généré (espaces conservés dans `Enfants`) : seule la comparaison les ignore.
- Lecture de la colonne « N° » des feuilles sources.
- Toute modification d'AlphaMaintenance.
