# Tickets TDD — Lot 083 : six points de l'élément parent sur PROCEDURE, dont deux conditionnés aux tâches

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suite directe du lot 082
(`tickets-tdd-lot-082-points-office-element-parent-procedure.md`), ouvert le même jour (19/09) : le client
a précisé sa demande juste après la livraison du lot 082. Décisions prises avec Simon ; réalisé en
autonomie jusqu'au commit.*

**Demande du client (verbatim)** :

> Au niveau de la feuille procédure il faut créer les points parents suivants :
> VISITE PRÉALABLE CHANTIER
> PROCÉDURE MAD que si il y a des TM
> AUTORISATION DÉPLATINAGES
> PROCÉDURE REL que si il y a des TM
> AUTORISATION DE REMISE EN SERVICE
> RÉCEPTION FINALE CHANTIER

**Ce qui change par rapport au lot 082** : la décision D5 (« `PointRules` reste ignoré sur PROCEDURE ») est
annulée ; la feuille Parents de l'export passe de 1 à 6 colonnes de points propres à l'équipement. Les
décisions D1-D4, D6 (placement avant les points des enfants) et D7 (réinitialisation, pas de migration)
restent valables.

---

## 1. Décisions (Simon, 19/09)

| # | Sujet | Décision |
| :- | :--- | :--- |
| E1 | Sens de « que si il y a des TM » | PROCÉDURE MAD si au moins une tâche MAD ; PROCÉDURE REL si au moins une tâche REL. |
| E2 | Lignes « titre de section » (sans ordre) | Ne comptent pas. Seules les vraies tâches (numéro d'ordre renseigné) sont évaluées. |
| E3 | Paramétrage | Les règles conditionnelles existantes de la règle PROCEDURE (`PointRules`), évaluées sur chaque vraie tâche : le point est créé si **au moins une** tâche satisfait une règle de sa colonne. Même évaluateur que les autres feuilles (valeur rognée, casse ignorée). Aucun changement de modèle ni de base. |
| E4 | Export, feuille Parents | Les 6 colonnes dans l'ordre du client, avant les 16 colonnes remontées des enfants. |

Choix techniques pris en autonomie (non tranchés avec Simon, faute d'enjeu métier) :
- Le champ comparé est un **nom de champ du bloc** PROCEDURE, comme sur les autres feuilles ; le profil
  standard utilise `TypeTacheMultipleAlias` (valeur brute lue en colonne R, « MAD »/« REL »).
- Aucun avertissement quand une règle ne correspond à aucune tâche : un dossier sans tâche REL est normal
  (différent des feuilles d'éléments, où chaque élément devrait correspondre à une règle).
- Un nom de champ inconnu lève `UnknownFieldReferenceException`, comme sur les autres feuilles (erreur de
  paramétrage).

## 2. Sous-tickets

### 83.1 — Application : `ProcedureExtractionService` évalue les règles conditionnelles sur les tâches

- **Rouge** (Moq sur `IWorkbookReader`) : une règle `TypeTacheMultipleAlias = MAD` crée le point quand une
  tâche MAD existe ; ne le crée pas quand seules des tâches REL existent ; ne le crée pas quand la seule
  ligne MAD est un titre de section ; un seul point même si plusieurs tâches correspondent ; casse et
  espaces ignorés ; fichier rejeté ⇒ aucun point ; aucune erreur ajoutée quand rien ne correspond.
- **Vert** : `ReadTachesMultiples` garde, pour chaque vraie tâche, ses valeurs brutes par nom de champ ;
  `IConditionalPointRuleEvaluator` est injecté dans le service ; règles groupées par `ColonneName`.
- Intégration C7401 (tâches MAD et REL) et D8570 (MAD seulement) sur fixture réelle.

### 83.2 — Profils standard

- Import, règle PROCEDURE : `UnconditionalColonneNames` = VISITE PRÉALABLE CHANTIER, AUTORISATION
  DÉPLATINAGES, AUTORISATION DE REMISE EN SERVICE, RÉCEPTION FINALE CHANTIER ; `PointRules` =
  `TypeTacheMultipleAlias Equals MAD` → PROCÉDURE MAD, `TypeTacheMultipleAlias Equals REL` → PROCÉDURE REL.
- Export, Parents : 6 `PointColumnDefinition` dans l'ordre E4, puis les 16 existantes.
- Intégration : C7401 → les 6 colonnes cochées ; D8570 → PROCÉDURE REL vide, les 5 autres cochées.

### 83.3 — BlazorAdmin : vue Détails

- `ImportSheetUsage` : PROCEDURE lit `ConditionalPointRules`.
- Phrases dédiées : « L'équipement est coché dans la colonne « PROCÉDURE MAD » si au moins une tâche a le
  champ « TypeTacheMultipleAlias » égal à « MAD ». » (variante « différent de » et plusieurs colonnes).
  Pas de phrase « si aucune condition n'est remplie » pour PROCEDURE.
- Catalogues figés (tickets 078/079 §5) mis à jour.

### 83.4 — Documentation

- Spec §1.3, ticket 082 (D5 annulée), tickets 078/079, `CLAUDE.md`.

## 3. Hors périmètre

- Toute condition autre que « au moins une vraie tâche satisfait la règle » (comptage, toutes les tâches…).
- Migration d'un profil existant : boutons « Réinitialiser » (D7 du lot 082).

## 4. Résultat

*(rempli à la clôture du lot)*
