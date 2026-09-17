# Tickets TDD — Lot 080 : noms de feuilles refusés par Excel dans un profil d'export

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suite directe du
lot 079 (`tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md`, §2 et décision D3), qui a
**signalé** sans le corriger un défaut réel : le domaine accepte des noms de feuilles que ClosedXML
refuse. La génération échoue alors seulement à l'écriture du fichier, en erreur technique 500.*

**Objet** : refuser ces configurations à l'enregistrement du profil avec un message métier localisé,
et remplacer l'erreur 500 par un rejet explicite pour les conflits de noms qui ne dépendent que des
données importées.

**Hors périmètre explicite de ce lot** :
- Les espaces en début ou fin de nom de feuille (`" Parents "`) : ClosedXML les accepte et les
  conserve (lot 079, §2). Aucun nettoyage, aucune validation.
- Toute migration ou correction automatique des profils déjà en base (constat 2). Ils sont couverts à
  la génération par 80.5.
- Le profil d'import, le moteur d'extraction, le nom du profil lui-même (lot 027).
- La comparaison des noms de Colonne des points (lot 081).
- Les autres causes possibles d'une erreur 500 sur `POST /api/oxo/process`.

---

## Constats (relus dans le code au commit `1ea79d3`, non supposés)

### 1. Le domaine ne vérifie que le nom vide

- `SheetGenerationRule.ValidateSheetNameNotEmpty` (`SheetGenerationRule.cs` L170) ne fait qu'un
  `IsNullOrWhiteSpace` : pas de longueur, pas de caractère, pas d'apostrophe. Le nom est stocké tel
  quel.
- `ExportProfile` (L35-55) ne valide **rien entre ses règles** : ni doublon de nom, ni nombre de règles
  `TacheMultiple`.
- Codes existants : `ExportProfile_EmptyName`/`_NoSheetRules`/`_NameTooLong`,
  `SheetGenerationRule_EmptySheetName`/`_DuplicateHeader`/`_DuplicateColonneNom`/
  `_ColumnSourceIncompatibleWithPivotSource`/`_PointColumnDefinitionsNotAllowedForTacheMultiple`/
  `_ApplicationColumnDefinitionsNotAllowedForTacheMultiple`/`_DuplicateApplicationNom`
  (`DomainErrorCode.cs` L63-72).

### 2. Un profil déjà en base contourne toute validation

`ExportProfile()` (L68) et `SheetGenerationRule()` (L165) ont un constructeur privé sans paramètre
réservé à EF Core. `ExportProfileConfiguration.cs` : `SheetName` en `HasMaxLength(200)` (L34-36), aucun
index. Un profil enregistré avant ce lot se charge donc toujours, même invalide. Il n'existe aucun
moyen propre de **construire** un tel profil dans un test.

### 3. Les règles vivent aujourd'hui dans la couche présentation

- `src/ExcelETL.BlazorAdmin/Formatting/ExcelSheetNameRules.cs` : `MaxLength = 31`, `IsTooLong`,
  `ForbiddenCharactersIn` (distincts, dans l'ordre d'apparition), `HasApostropheAtEdge`, `NameComparer`
  (`OrdinalIgnoreCase`). Seul consommateur : `ExportProfileDescriptionBuilder.cs`.
- `ExportProfileDescriptionBuilder.DescribeWorkbookBlocking` (L51-76) : doublons à la casse près,
  plusieurs règles `TacheMultiple`, feuille nommée `TM_PROC_MAD`/`TM_PROC_REL` (liste codée en dur L18)
  avec une règle `TacheMultiple`. `DescribeSheetNameBlocking` (L79-105) : longueur, caractères,
  apostrophe ; ignoré pour `TacheMultiple`. Clés `ExportProfileDetails_Blocking*`
  (`BlazorAdminMessages.resx` L1954-1984).
- `tests/ExcelETL.BlazorAdmin.Tests/Formatting/ExcelSheetNameRulesTests.cs` : 20 cas (L27-49),
  **construits comme des profils** (`ExportProfile(rules)`), générés puis écrits avec le vrai
  `ClosedXmlWorkbookWriter`. `ExportProfileDescriptionBuilderBlockingTests.cs` construit lui aussi des
  profils invalides pour tester les phrases.
- **Conséquence** : dès que le constructeur refuse ces noms, ces deux fichiers ne peuvent plus
  construire leurs cas.

### 4. Le moteur ne gère aucun conflit de nom

- `SheetGenerationEngine.GenerateTacheMultipleSheets` (L68-89) nomme chaque feuille
  `ExcelSheetNameSanitizer.Sanitize(group.Key)` (L87). Les autres règles gardent `rule.SheetName` (L106).
- Aucun contrôle : deux codes qui donnent le même nom une fois nettoyés, un code égal au nom d'une
  autre feuille, deux règles `TacheMultiple`.
- `ExcelSheetNameSanitizer.Sanitize` (`ExcelSheetNameSanitizer.cs` L13-24) remplace `\ / ? * [ ] :` par
  `_` et coupe à 31 caractères. Il **ne traite ni les apostrophes de bord, ni un résultat vide**.
- Les codes de type de tâche viennent du fichier importé : seuls `TM_PROC_MAD`/`TM_PROC_REL` sont connus
  à l'avance (conversion `MAD`/`REL`, lot 078). Un conflit avec un autre code **ne peut pas se prévoir
  à partir du profil**.
- `GeneratedWorkbook`/`GeneratedSheet` sont des records simples : un classeur généré se construit dans
  un test avec n'importe quel nom de feuille, sans passer par un profil.

### 5. Chemin de l'erreur jusqu'à l'utilisateur

- `ClosedXmlWorkbookWriter.Write` relance l'`ArgumentException` (L57-60).
  `ProcessOxoFileService.ProcessAsync` (L44-83) la journalise puis la relance. `OxoController` n'attrape
  que `FileFormatException`.
- `GlobalExceptionHandler` : exception sans code d'erreur → **500** avec `exceptionType`/
  `exceptionMessage` (L49-60, lot 065). Une exception à code d'erreur passe par `StatusCodeFor` (L41).
- `ExportProfileTest.razor` (L502-520) : `GenerationErrorMessage = BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message`,
  soit aujourd'hui le message technique brut de ClosedXML.

### 6. Affichage des erreurs dans l'éditeur

- `ExportProfileDraftMapper.ConvertSheetRule` attrape `DomainValidationException` et
  `DomainRuleViolationException` (L150) et pose l'erreur sur le brouillon de la règle.
- `ExportProfileDraftMapper.ToDomain` construit le profil dans un `try` qui n'attrape **que
  `DomainValidationException`** (L113) et pose l'erreur sur le brouillon racine.
  `ExportProfileEditor.SaveProfileAsync` (L428-448) l'affiche en message racine (L444-447).

### 7. Tests existants

- Domain.Tests (`ExportProfileTests`, `SheetGenerationRuleTests`) : aucun cas de longueur, caractère,
  apostrophe ou doublon de nom de feuille.
- Domain.Tests ne référence que Domain. Infrastructure.Tests référence Infrastructure, Application et
  Domain.
- `SheetGenerationEngineTests` (L237-264) et `ExcelSheetNameSanitizerTests` : nettoyage des codes,
  aucun test de conflit.

---

## Décisions à valider avant 80.1 (effort élevé)

| # | Question | Recommandation | Raison |
| :--- | :--- | :--- | :--- |
| D1 | Où placer les règles de nom de feuille ? | **Dans le domaine**, type statique pur `ExcelSheetName` (`Domain/Generation/Profile/`), source unique. `ExcelSheetNameRules` (BlazorAdmin) est supprimé. | Une règle métier reste dans le domaine (`recommandations-tickets-tdd.md` §9). Logique de chaîne sans dépendance. Deux copies divergeraient. |
| D2 | Quels contrôles au domaine ? | Règle Equipement/Isolement : longueur ≤ 31, caractères interdits, apostrophe de bord. Profil : doublon de nom à la casse près entre règles Equipement/Isolement, au plus une règle `TacheMultiple`. Tous en `DomainValidationException`. | Tout ce qui se décide à partir du profil seul. `DomainValidationException` parce que `ToDomain` n'attrape que ce type au niveau du profil (constat 6). |
| D3 | Feuille nommée `TM_PROC_MAD`/`TM_PROC_REL` avec une règle `TacheMultiple` | **Pas au domaine** : couvert par le contrôle générique du moteur (D4), qui couvre aussi les codes inconnus. | Ces deux libellés viennent du pipeline d'import. Les écrire dans le domaine d'export crée un couplage, sans couvrir les autres codes. |
| D4 | Contrôle au moment de la génération | Validateur pur `GeneratedSheetNameValidator` (Application) appliqué à **tous** les noms produits par `SheetGenerationEngine` : nom valide selon `ExcelSheetName`, noms distincts à la casse près. Échec → `GeneratedSheetNameException` (`IHasApplicationErrorCode`), deux codes : `GeneratedSheetNameInvalid`, `GeneratedSheetNameConflict`. | Un seul contrôle couvre les conflits dus aux données (constat 4) **et** les profils enregistrés avant ce lot (constat 2). Testable avec de simples chaînes, sans profil invalide. |
| D5 | Code HTTP de cette exception | **422**, comme un rejet métier du fichier. | Le fichier et le profil sont incompatibles : ni requête mal formée (400), ni panne (500). |
| D6 | `ExcelSheetNameSanitizer` | Retirer les apostrophes de bord ; un résultat vide devient `_`. S'appuie sur `ExcelSheetName` pour la liste des caractères. | Même famille de défaut que les caractères interdits, déjà traités là. |
| D7 | Section « Problèmes qui empêchent la génération » de la page Détails | **Retirer** les contrôles désormais portés par le domaine (longueur, caractères, apostrophe, doublon, plusieurs règles `TacheMultiple`), leurs clés et leurs tests. **Garder** l'alerte « feuille nommée comme un code de tâche connu ». | Ces cas deviennent impossibles à enregistrer et impossibles à construire dans un test (constat 2). Un profil ancien est rejeté clairement à la génération (D4) et à son prochain enregistrement. L'alerte des codes connus reste vraie et utile avant génération. |
| D8 | Test qui confronte au vrai writer | **Déplacé dans Infrastructure.Tests** (`Excel/ExcelSheetNameWriterAgreementTests.cs`), sur des `GeneratedWorkbook` construits directement (constat 4) et jugés par `ExcelSheetName`. | Il vérifie le domaine contre l'infrastructure. Construire le classeur directement évite de dépendre d'un profil invalide. |

---

## 80.0. Investigation restante (pas de code de production)

1. Ouvrir dans Excel (et LibreOffice) un classeur ClosedXML contenant une feuille nommée `History`.
   Si Excel refuse ou répare le fichier, soumettre à Simon l'ajout de ce contrôle à `ExcelSheetName`.
   Consigner le résultat ici dans tous les cas.
2. Lire `GlobalExceptionHandler.StatusCodeFor` : forme exacte de l'ajout d'un cas 422.
3. `grep` de toute construction de `SheetGenerationRule`/`ExportProfile` dans `src/` et `tests/` avec
   un nom invalide selon D2, hors les deux fichiers du constat 3. Attendu : aucune.

---

## 80.1. Règles de nom de feuille dans le domaine

**Comportement** : `ExcelSheetName` (`Domain/Generation/Profile/`) expose `MaxLength = 31`,
`IsTooLong`, `ForbiddenCharactersIn`, `HasApostropheAtEdge`, `IsValid`, `NameComparer` — même
comportement que `ExcelSheetNameRules` (constat 3).

**Rouge** : `ExcelSheetNameTests` (Domain.Tests) : 31 et 32 caractères, chaque caractère interdit, ordre
et unicité de `ForbiddenCharactersIn`, apostrophe au début, à la fin, au milieu, espaces de bord
acceptés, comparaison à la casse près.

**Vert** : créer le type. `ExportProfileDescriptionBuilder` pointe dessus ; `ExcelSheetNameRules` est
supprimé.

**Garde-fou** (D8) : `ExcelSheetNameRulesTests` est remplacé par `ExcelSheetNameWriterAgreementTests`
(Infrastructure.Tests). Mêmes cas de noms (y compris doublons et espaces de bord), écrits dans un
`GeneratedWorkbook` construit directement. Attendu : l'écriture échoue exactement quand `IsValid`
est faux ou quand deux noms sont égaux selon `NameComparer`. Vérifier qu'il n'est pas vide de sens :
passer `MaxLength` à 32 le remet au rouge.

## 80.2. Validation d'une règle Equipement/Isolement

**Rouge** (`SheetGenerationRuleTests`) :
- 32 caractères → `DomainValidationException`, `SheetGenerationRule_SheetNameTooLong` ; 31 accepté ;
- chacun de `\ / ? * [ ] :` → `SheetGenerationRule_SheetNameForbiddenCharacter`, `Args` = caractères
  trouvés ;
- apostrophe en début ou fin → `SheetGenerationRule_SheetNameApostropheAtEdge` ; au milieu accepté ;
- **non-généralisation** : une règle `TacheMultiple` avec un nom de 40 caractères contenant `/` reste
  acceptée (libellé interne, lot 079 §1).

**Vert** : `ValidateSheetNameIsValidExcelName`, appelée juste après `ValidateSheetNameNotEmpty`, ignorée
pour `PivotSource.TacheMultiple`.

**Localisation** : 3 entrées `DomainErrorMessages.resx`/`.fr.resx`, prouvées EN et FR par un test sur le
vrai `.resx` (modèle `DomainErrorMessagesHeaderRuleLocalizationTests`).

## 80.3. Validation entre les règles du profil

**Rouge** (`ExportProfileTests`) :
- deux règles Equipement/Isolement `Parents`/`parents` → `DomainValidationException`,
  `ExportProfile_DuplicateSheetName`, `Args` = nom ;
- deux règles `TacheMultiple` → `ExportProfile_SeveralTacheMultipleRules` ;
- **non-généralisation** : une règle `TacheMultiple` de libellé `Parents` à côté d'une règle `Parents`
  est acceptée (le libellé n'est jamais un nom de feuille) ;
- le profil semé (`DefaultProfileSeeder`) reste constructible.

**Vert** : deux méthodes `Validate*` dans le constructeur principal d'`ExportProfile`, après
`ExportProfile_NoSheetRules`. Localisation comme en 80.2.

## 80.4. Affichage dans l'éditeur (bUnit)

**Rouge** (`ExportProfileEditorSheetNameValidationTests.cs`) :
- nom de feuille de 32 caractères puis « Ajouter la règle » → message localisé sur la règle, rien
  ajouté ;
- deux règles `Parents`/`parents` puis « Enregistrer le profil » → message racine localisé,
  `SaveAsync` jamais appelé (`Mock<IExportProfileStore>`, `Times.Never`).

**Vert** : aucun code attendu (constat 6). S'il en faut, s'arrêter et le signaler avant d'écrire.

## 80.5. Contrôle au moment de la génération

**Rouge** :
- `ExcelSheetNameSanitizerTests` : `'TM'` → `TM` ; `''` → `_` (D6) ;
- `GeneratedSheetNameValidatorTests` : liste valide acceptée ; nom trop long, caractère interdit,
  apostrophe de bord → `GeneratedSheetNameInvalid` portant le nom ; `Parents`/`parents` →
  `GeneratedSheetNameConflict` portant le nom ;
- `SheetGenerationEngineTests` : règle Equipement `TM_PROC_MAD` + règle `TacheMultiple` + tâche de code
  `TM_PROC_MAD` → `GeneratedSheetNameException` ; même chose avec deux codes `A/B` et `A:B` ; **aucune
  exception** si aucune tâche ne porte le code en conflit ;
- `OxoProcessEndpointTests` (`WebApplicationFactory`, orchestrateur substitué) : 422, message localisé,
  aucun `exceptionType` dans le corps ;
- `ExportProfileTestTests` : message localisé affiché, pas le texte ClosedXML.

**Vert** : `ApplicationErrorCode.GeneratedSheetNameInvalid`/`GeneratedSheetNameConflict` +
`ApplicationMessages.resx`/`.fr.resx` ; validateur appelé en fin de `SheetGenerationEngine.Generate` ;
cas 422 dans `GlobalExceptionHandler.StatusCodeFor`.

## 80.6. Page Détails et documents vivants

- D7 : suppression dans `ExportProfileDescriptionBuilder` des contrôles longueur/caractère/apostrophe/
  doublon/plusieurs règles `TacheMultiple`, de leurs clés `ExportProfileDetails_Blocking*` (EN et FR,
  aucune orpheline) et de leurs tests dans `ExportProfileDescriptionBuilderBlockingTests`. Les tests de
  l'alerte « code de tâche connu » restent inchangés.
- `ExportProfileDescriptionBuilderSeededProfileTests` reste vert sans changement (le profil semé n'a
  aucun problème bloquant).
- Ticket 079 : §2, décision D3 et hors périmètre mis à jour en place, avec renvoi à ce lot.
- `CLAUDE.md` : puce du lot 079 mise à jour, nouvelle puce lot 080.

---

## Portée des tests

- Itérations : `--filter` sur la classe travaillée.
- Clôture : Domain.Tests, Application.Tests, Infrastructure.Tests, WebAPI.Tests, puis
  `ExcelETL.BlazorAdmin.Tests` complet.
- Doivent rester verts **sans changement d'assertion** : tests aller-retour des mappers et des éditeurs
  (lots 073-076), `DefaultProfileSeederPipelineIntegrationTests`, `GenerationPipelineIntegrationTests`.
- Les seules assertions supprimées sont celles de D7, listées dans le commit.
- Pas de navigateur : Simon vérifie.
