# Tickets TDD — Lot 082 : points créés d'office sur l'élément parent, déclarés sur la feuille PROCEDURE

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 19/09 à la
suite d'un retour du client en test sur BlazorAdmin, décisions prises avec Simon le même jour. Réalisé en
autonomie (Simon absent), de bout en bout jusqu'au commit.*

**Objet** : la liste « Tableaux » du profil d'import ne sert plus qu'à indiquer les tableaux ; les points
créés d'office sur l'élément parent se déclarent dans les colonnes cochées d'office de la règle PROCEDURE.

---

## 1. Constat

- Le client a ajouté « VISITE PRÉALABLE CHANTIER » dans les colonnes cochées d'office de la règle
  PROCEDURE. Aucun point n'est créé et la vue Détails (lot 078) le signale « configuré mais ignoré ».
- `ProcedureExtractionService` (L69) crée un point sur l'Équipement pour **chaque nom de
  `ImportProfile.DefaultTableaux`** (lot U3) et ne lit jamais `SheetExtractionRule.UnconditionalColonneNames`.
- La même liste `DefaultTableaux` remplit la colonne « Tableaux » de Parents **et** d'Enfants
  (`ImportPipelineOrchestrator`, diffusion lot U). Un nom de point y apparaît donc comme un tableau, sur
  le parent et sur chaque enfant.
- « VISITE PRÉALABLE CHANTIER » a été ajoutée au profil standard le 16/09 (commit `b8e9d69`) par ce même
  chemin, faute d'un autre moyen de créer un point sur le parent.
- Origine : la spec (`spec-extraction-fichier-source-oxo.md` §1.3, dernière puce) dit que des points
  sont créés pour chaque Colonne associée aux Tableaux TRAVAUX COMPLET/DETAIL. Le lot U3 l'a traduit en
  « un point nommé d'après chaque tableau », ce qui mélange les deux notions.

## 2. Décisions (Simon, 19/09)

| # | Sujet | Décision |
| :- | :--- | :--- |
| D1 | Rôle de « Tableaux » | Uniquement la colonne « Tableaux » du fichier généré. Ne crée aucun point. |
| D2 | TRAVAUX COMPLET / TRAVAUX DETAIL | Des tableaux seulement. Ils restent dans « Tableaux » et ne créent plus de point. L'app legacy ne s'en sert pas. |
| D3 | Diffusion de « Tableaux » aux enfants | Inchangée (colonne « Tableaux » d'Enfants toujours remplie). |
| D4 | Points d'office du parent | Déclarés dans `UnconditionalColonneNames` de la règle PROCEDURE, créés sur l'Équipement uniquement. |
| D5 | Points conditionnels sur PROCEDURE | Non. `PointRules` reste ignoré sur PROCEDURE (pas de besoin exprimé). |
| D6 | Export, feuille Parents | Une colonne cochée (« X ») par point d'office, placée avant les 16 colonnes de points remontées des enfants. |
| D7 | Profil standard déjà en base | Aucune migration : le client utilise les boutons « Réinitialiser » (`reset-profile-button-*`, `reset-export-profile-button-*`) sur les deux profils standard. |

Liste des points d'office : pour l'instant « VISITE PRÉALABLE CHANTIER » seule. Le client pense qu'il y en
a au moins un autre ; il sera ajouté au profil standard quand il sera connu (changement de données seul).

## 3. Sous-tickets

### 82.1 — Application : `ProcedureExtractionService` crée les points depuis la règle PROCEDURE

- **Rouge** : `Extract` avec `UnconditionalColonneNames = ["VISITE PRÉALABLE CHANTIER"]` crée exactement
  ce point sur le repère de l'Équipement ; une liste vide ne crée aucun point ; rejet du fichier ⇒ aucun
  point. Test garde-fou : deux règles avec des listes différentes donnent chacune leurs propres points.
- **Vert** : `points = sheetRule.UnconditionalColonneNames.Select(...)`. Le paramètre `defaultTableaux`
  disparaît de `IProcedureExtractionService.Extract` (plus aucun usage dans le service) ; l'orchestrateur
  ne le passe plus. La diffusion de `DefaultTableaux` par l'orchestrateur est inchangée (D3).
- Tests existants qui attendaient les points TRAVAUX COMPLET/DETAIL : corrigés sur place (comportement
  volontairement changé, D2).

### 82.2 — Profils standard (`DefaultProfileSeeder`)

- Import : `DefaultTableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"]` ; règle PROCEDURE
  `UnconditionalColonneNames = ["VISITE PRÉALABLE CHANTIER"]`.
- Export, Parents : `PointColumnDefinition("VISITE PRÉALABLE CHANTIER")` en tête des colonnes de points.
  Enfants inchangé.
- Tests : contenu seedé ; test de cohérence croisée « chaque colonne de point de Parents correspond à un
  point produit par le profil d'import » ; intégration sur fixture réelle (profil seedé relu en base) :
  la cellule « VISITE PRÉALABLE CHANTIER » de Parents vaut « X », la colonne « Tableaux » vaut
  « TRAVAUX COMPLET, TRAVAUX DETAIL ».

### 82.3 — BlazorAdmin : vue Détails et table d'usage

- `ImportSheetUsage` : PROCEDURE lit `UnconditionalColonnes`. La vue Détails décrit ces colonnes comme des
  points cochés sur l'élément parent et ne les signale plus « ignorées ». `PointRules` sur PROCEDURE reste
  signalé ignoré (D5).
- La phrase générale sur « Tableaux » ne dit plus que ces noms créent des points.
- `ImportSheetUsageTests` (garde contre le vrai pipeline) : PROCEDURE ne figure plus parmi les feuilles
  où remplir `UnconditionalColonneNames` est sans effet.
- Catalogue de la vue Détails sur le profil seedé (ticket 078 §5) mis à jour avec le test qui le fige.

### 82.4 — Documentation

- `spec-extraction-fichier-source-oxo.md` §1.3, `glossaire-ef6-legacy-AMAR-ModelCF.md`, ticket 078 §5,
  `CLAUDE.md` (bullet de lot).

## 4. Hors périmètre

- Points conditionnels sur PROCEDURE (D5).
- Toute migration automatique d'un profil existant (D7).
- Le deuxième point d'office annoncé par le client (données, à ajouter quand il sera connu).
- Changement du moteur de génération : la voie directe (`PointPivot.ParentRepere == Equipement.Repere`)
  coche déjà un point du parent.

## 5. Résultat (19/09)

- **82.1** (`3041a1b`) : `ProcedureExtractionService` crée les points de l'équipement depuis
  `sheetRule.UnconditionalColonneNames` ; le paramètre `defaultTableaux` est retiré de
  `IProcedureExtractionService.Extract`. **82.3 livré dans le même commit**, parce que le garde-fou
  `ImportSheetUsageTests` (vrai pipeline sur D8570) passait au rouge dès 82.1 : `ImportSheetUsage`
  marque PROCEDURE comme lisant `UnconditionalColonnes` (nouvelle propriété
  `UnconditionalColonnesTickTheEquipement`), et la vue Détails dit « L'équipement est coché dans la
  colonne … » (clés `ImportProfileDetails_PointEquipementUnconditionalOne`/`Several`, même texte français
  dans les deux `.resx`, décision D5 du lot 078). Une règle conditionnelle sur PROCEDURE reste signalée
  ignorée (test ajouté).
- **82.2** (`f83be62`) : profil standard mis à jour (§3). Test de cohérence croisée ajouté pour Parents.
  Intégration C7401 : colonne « VISITE PRÉALABLE CHANTIER » de Parents = « X », colonne « Tableaux » =
  « TRAVAUX COMPLET, TRAVAUX DETAIL » sur Parents et sur chaque ligne d'Enfants. Catalogues des vues
  Détails import et export (tickets 078 et 079 §5) mis à jour, lettres des colonnes de points de Parents
  décalées d'un rang (O à AE).
- **82.4** : spec §1.3, tickets 078/079, commentaire d'`ImportProfile`, `CLAUDE.md`.
- Tests existants modifiés : ceux qui attendaient les points TRAVAUX COMPLET/DETAIL ou « VISITE » dans
  « Tableaux », et le test du lot 078 qui figeait « VISITE PRÉALABLE CHANTIER » comme ignorée sur
  PROCEDURE (comportement volontairement changé, D2/D4). Supprimé :
  `Run_PassesProfileDefaultTableauxToProcedureService_NotAHardcodedConstant` (l'orchestrateur ne passe
  plus cette liste au service ; sa diffusion reste couverte par
  `Run_BroadcastsDefaultTableauxApplicationsAndRepereParentFromProfileOntoEquipementAndEveryIsolement`).
- Suites complètes : Domain 468, Application 316, Infrastructure 278, WebAPI 75, Hosting 15,
  BlazorAdmin 1592, legacy 9+15 — toutes vertes.
- **Mise en service** : sur un environnement existant, cliquer « Réinitialiser » sur le profil d'import
  standard **et** sur le profil d'export standard (D7). Sans ça, l'ancien profil garde « VISITE »
  dans « Tableaux » : la valeur reste affichée dans la colonne Tableaux et aucun point n'est plus créé
  sur l'équipement.
- Non vérifié dans un navigateur.
