# Tickets TDD — Lot 067 : `Repère TM` / `TYPE ELEMENT CODE` / `Colonne Travaux` sur les feuilles TM_PROC_*

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`).*

**Contexte** : l'app legacy a besoin, pour chaque ligne de tâche multiple exportée (feuilles
`TM_PROC_MAD`/`TM_PROC_REL`, générées dynamiquement par `SheetGenerationEngine` à partir de la règle
`PivotSource.TacheMultiple`, Lot T), de savoir à quel `BaseElement` (repère) et à quelle "colonne de
travaux" (terminologie client — un `Point`/`Colonne` cible côté legacy, sans lien avec les noms de
Tableaux) rattacher la tâche. Discuté et tranché avec Simon avant ce ticket (voir conversation) :
3 nouvelles colonnes sur la règle export `"Tâches multiples"` — `"Repère TM"`, `"TYPE ELEMENT CODE"`,
`"Colonne Travaux"`.

---

## Décisions actées (résumé, ne pas rouvrir)

1. **`Repère TM`** : diffuse `EquipementPivot.Repere` sur chaque `TacheMultiplePivot` du run (un seul
   Équipement par run, contrainte déjà actée ailleurs — aucune ambiguïté possible).
2. **`TYPE ELEMENT CODE`** : diffuse `EquipementPivot.TypeElementNom` sur chaque `TacheMultiplePivot`
   du run.
3. **`Colonne Travaux`** : **pas** de mapping en dur dans le moteur. Nouveau champ de configuration
   `ImportProfile.TacheMultipleTypeLabels` — une liste de paires `(Code, Label)` (ex.
   `("TM_PROC_MAD", "Procédure MAD")`, `("TM_PROC_REL", "Procédure REL")`), éditable dans
   `ImportProfileEditor.razor`. Diffusée sur chaque `TacheMultiplePivot` par recherche du
   `TypeTacheMultipleCode` de la tâche dans cette liste (**trim + insensible à la casse**, cohérent
   avec le reste du moteur, spec §7) — **cellule vide si aucune correspondance** (pas d'erreur, pas de
   valeur de repli sur le code brut).
4. **Rattachement à `ImportProfile`, pas `ExportProfile`** : cohérent avec le fait que
   `TypeTacheMultipleCode` (`"TM_PROC_MAD"`/`"TM_PROC_REL"`) est déjà produit par une logique fixe
   côté extraction (`ProcedureExtractionService.MapTypeTacheMultipleAlias`) — la "traduction" de ce
   code vit au même endroit conceptuel, au même niveau que `DefaultTableaux`/`DefaultApplicationNames`.
5. **Paramètre optionnel, pas requis** : contrairement à `DefaultTableaux`/`DefaultApplicationNames`
   (rendus obligatoires au constructeur pour éliminer un risque de hardcode silencieux sur un
   comportement bloquant), `TacheMultipleTypeLabels` a un défaut sans risque et explicitement voulu
   (liste vide → cellule vide, comportement décision 3 ci-dessus) : ajouté comme **dernier paramètre
   optionnel** (`= null`, normalisé en `[]`) sur les 3 constructeurs `ImportProfile`, pour éviter de
   toucher les ~72 sites d'appel existants (`new ImportProfile(...)`). Documenté ici, pas une
   dérogation silencieuse à la convention "pas de défaut magique" des deux autres listes — leur
   caractère obligatoire répondait à un risque différent (une extraction bloquante, pas une colonne
   d'export facultative).
6. **`TacheMultipleColonneTravaux`** (le `PivotFieldRef` d'export) lit `TacheMultiplePivot.ColonneTravaux`
   telle quelle — aucune logique de mapping côté `PivotFieldResolver`/`SheetGenerationEngine`, tout se
   joue à la diffusion (`ImportPipelineOrchestrator`).

---

## Hors périmètre explicite de ce lot (ne pas rouvrir)

- Toute modification du calcul de `TypeTacheMultipleCode` lui-même (`MapTypeTacheMultipleAlias`,
  hardcode déjà identifié et volontairement laissé de côté, voir Lot 059/etat-des-lieux) — ce lot
  consomme ce code, ne le modifie pas.
- Toute exposition de `TacheMultipleTypeLabels` côté `ExportProfileEditor.razor` (n'a pas de sens,
  c'est un champ `ImportProfile`).
- Migration idempotente pour un profil déjà seedé (même raisonnement que le Lot 066 : base de données
  jetable en pré-production, un reseed suffit — pas de migration de données `T8`-style ici).
- Toute colonne supplémentaire au-delà des 3 listées (pas de demande au-delà).

---

## 67.0. Investigation préalable (obligatoire avant tout code)

- [x] Confirmer que `TacheMultiplePivot` n'a aujourd'hui aucun champ de repère/type — confirmé,
  `Ordre`/`Action`/`Acteur`/`Risques`/`TypeTacheMultipleCode`/`DateValidation`/`EstFactice` uniquement.
- [x] Confirmer le mécanisme de broadcast existant (`IsolementPivot.Localisation`/`Tableaux`/
  `Applications`/`RepereParent`, `init` properties remplies après coup via `with { ... }` dans
  `ImportPipelineOrchestrator.BroadcastEquipementContext`) — à reproduire à l'identique.
- [x] Confirmer que `SheetGenerationEngine.GenerateTacheMultipleSheets` résout déjà chaque
  `ColumnDefinition` via `PivotFieldResolver.Resolve(tache, column.Source.Value)` — donc aucune
  extension moteur nécessaire, seulement de nouveaux membres `PivotFieldRef` + une nouvelle branche de
  résolution.
- [x] Confirmer la forme des value objects existants dans `Extraction/Profile` (`HeaderCompositeRule` :
  `sealed partial record`, deux champs scalaires, validation dans le constructeur) — modèle à suivre
  pour `TacheMultipleTypeLabel`.
- [x] Confirmer le mapping EF Core de `ImportProfile.SheetRules`/`DefaultTableaux` (`ImportProfileConfiguration.cs`)
  — `DefaultTableaux`/`DefaultApplicationNames` sont des primitive collections (`IsRequired()`,
  colonne JSON), mais `TacheMultipleTypeLabel` (paire de scalaires) n'est **pas** un type primitif :
  nécessite un `OwnsMany` directement sur `ImportProfile` (comme `SheetRules`), pas une primitive
  collection.
- [x] Compter les sites d'appel `new ImportProfile(...)` (~72, `grep -rn` sur `src`/`tests`) — confirme
  la décision 5 (paramètre optionnel plutôt que requis).

---

## 67.1. Domain — `TacheMultipleTypeLabel` + `ImportProfile.TacheMultipleTypeLabels`

**Comportement attendu** :
- Nouveau `src/ExcelETL.Domain/Extraction/Profile/TacheMultipleTypeLabel.cs` : `sealed record
  TacheMultipleTypeLabel(string Code, string Label)` — constructeur validant `Code`/`Label` non-vides
  (`DomainValidationException`, nouveaux `DomainErrorCode.TacheMultipleTypeLabel_EmptyCode`/
  `_EmptyLabel`) et non-dépassement de `ImportProfile.MaxListItemNameLength` (nouveaux
  `_CodeTooLong`/`_LabelTooLong`, réutilisant la constante existante — pas de nouvelle constante de
  longueur).
- `ImportProfile` gagne `TacheMultipleTypeLabels` (`IReadOnlyList<TacheMultipleTypeLabel>`, backing
  field privé mutable + ctor EF sans validation, même pattern que `SheetRules`) — paramètre optionnel
  `= null` normalisé en `[]`, en dernière position sur les 3 constructeurs publics. Validation dans le
  constructeur principal : boucle sur la liste, détection de doublon de `Code` (trim + insensible à la
  casse) via un nouveau `DomainErrorCode.ImportProfile_DuplicateTacheMultipleTypeLabelCode` — même
  esprit que `ValidateListItemName`, mais sur `.Code` plutôt que sur la chaîne entière (les `Label` en
  double sont, eux, autorisés : rien n'empêche `"MAD"` et `"MAD2"` de partager le même libellé cible).
- EF Core (`ImportProfileConfiguration.cs`) : `builder.OwnsMany(p => p.TacheMultipleTypeLabels, ...)`
  — table `ImportProfileTacheMultipleTypeLabels`, FK `ImportProfileId`, clé fantôme `int Id`, `Code`/
  `Label` `IsRequired().HasMaxLength(ImportProfile.MaxListItemNameLength)`. Nouvelle migration EF (générée
  via `dotnet ef migrations add`, jamais écrite à la main).
- `.resx` : `DomainErrorMessages.resx`/`.fr.resx` gagnent les 5 nouvelles clés (`TacheMultipleTypeLabel_EmptyCode`/
  `_EmptyLabel`/`_CodeTooLong`/`_LabelTooLong`, `ImportProfile_DuplicateTacheMultipleTypeLabelCode`) — sans
  quoi `BusinessExceptionLocalizer` afficherait la clé brute à l'admin.

**Tests** (Domain + Infrastructure) :
- `TacheMultipleTypeLabel` : Code/Label vide rejeté, longueur excessive rejetée, cas nominal.
- `ImportProfile` : paramètre omis → `TacheMultipleTypeLabels` vide ; liste fournie → conservée ;
  doublon de `Code` (y compris variante `Trim`/casse) → rejeté ; `Code` différents avec même `Label` →
  accepté.
- `EfImportProfileStoreTests` : round-trip complet de `TacheMultipleTypeLabels` (plusieurs entrées,
  round-trip d'une liste vide).
- `DomainErrorMessagesImportProfileListItemLocalizationTests` (ou fichier équivalent) : les 5 nouvelles
  clés sont réellement localisées en EN et FR (pas de repli sur la clé brute).

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/`, `src/ExcelETL.Infrastructure/Persistence/Configurations/ImportProfileConfiguration.cs`.

---

## 67.2. Domain — `TacheMultiplePivot.Repere`/`TypeElementNom`/`ColonneTravaux` + `PivotFieldRef`

**Comportement attendu** :
- `TacheMultiplePivot` gagne 3 `init` properties : `Repere`, `TypeElementNom`, `ColonneTravaux` (toutes
  `string`, défaut `""`) — pas de nouveau paramètre de constructeur (même mécanisme que
  `IsolementPivot.Localisation`/`Tableaux`/`Applications`/`RepereParent` : connu seulement après coup,
  diffusé par l'orchestrateur via `with { ... }`). Pas de validation (une chaîne vide est un état valide
  — absence de correspondance pour `ColonneTravaux`, ou run théorique sans Équipement pour les deux
  autres, cas qui ne se produit jamais en pratique puisque PROCEDURE rejette tout le fichier avant que
  `ReadTachesMultiples` soit appelé si `Equipement` est `null`).
- `PivotFieldRef` gagne `TacheMultipleRepere`, `TacheMultipleTypeElementNom`, `TacheMultipleColonneTravaux`.
- `PivotFieldResolver.GetPivotSource` et `Resolve(TacheMultiplePivot, PivotFieldRef)` gagnent les 3
  nouvelles branches (lecture directe des 3 nouvelles propriétés, aucune logique).

**Tests** (Domain) : `TacheMultiplePivotTests` (nouvelles propriétés, valeur par défaut, `with`
fonctionne) ; `PivotFieldResolverTests` (les 3 nouveaux membres résolvent la bonne propriété,
`GetPivotSource` les rattache à `PivotSource.TacheMultiple`).

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/TacheMultiplePivot.cs`,
`src/ExcelETL.Domain/Generation/Fields/`.

---

## 67.3. Application — diffusion dans `ImportPipelineOrchestrator`

**Comportement attendu** : après construction de `equipement` (ligne où `Localisation`/`Tableaux`/
`Applications` sont déjà appliqués), diffuser sur `procedureResult.TachesMultiples` — nouvelle méthode
privée `BroadcastTachesMultiplesContext(tachesMultiples, equipement, profile.TacheMultipleTypeLabels)`,
symétrique à `BroadcastEquipementContext` :
- `Repere = equipement.Repere`
- `TypeElementNom = equipement.TypeElementNom`
- `ColonneTravaux` = résultat d'une recherche (trim + `OrdinalIgnoreCase`) du `TypeTacheMultipleCode` de
  la tâche dans `profile.TacheMultipleTypeLabels` — `""` si aucune entrée ne correspond.

Le résultat diffusé remplace `procedureResult.TachesMultiples` dans l'appel à `new ImportResult(...)`
(actuellement ligne directe — devient la liste diffusée).

**Tests** (Application, unitaires sur l'orchestrateur avec des mocks des 5 services) :
- `TacheMultipleTypeLabels` contient une entrée correspondant au code de la tâche (y compris variante
  trim/casse) → `ColonneTravaux` = le `Label` configuré.
- Aucune entrée correspondante → `ColonneTravaux` = `""`.
- `TacheMultipleTypeLabels` vide (profil par défaut avant migration/config) → `ColonneTravaux` = `""`
  pour toutes les tâches, aucune exception.
- `Repere`/`TypeElementNom` diffusés correctement sur chaque tâche du run (plusieurs tâches).
- Non-régression : le reste du comportement de l'orchestrateur (rejet fichier, isolements, points,
  erreurs) inchangé.

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs`.

---

## 67.4. Infrastructure — seed du profil d'import par défaut + colonnes sur la règle d'export

**Comportement attendu** :
- `DefaultProfileSeeder.BuildDefaultImportProfile` : `TacheMultipleTypeLabels = [new("TM_PROC_MAD",
  "Procédure MAD"), new("TM_PROC_REL", "Procédure REL")]` — les valeurs discutées avec Simon, désormais
  de la donnée de configuration, plus une valeur en dur.
- `DefaultProfileSeeder.BuildTacheMultipleSheetRule` gagne 3 `ColumnDefinition` : `"Repère TM"` →
  `TacheMultipleRepere`, `"TYPE ELEMENT CODE"` → `TacheMultipleTypeElementNom`, `"Colonne Travaux"` →
  `TacheMultipleColonneTravaux`. **Décision d'implémentation** : `"Repère TM"`/`"TYPE ELEMENT CODE"`
  en tête (même position que Repère/Type Elément sur `Parents`/`Enfants`), `"Colonne Travaux"` en
  dernier (colonne de liaison legacy, position logiquement finale). Ordre final : Repère TM, TYPE
  ELEMENT CODE, Ordre, Action, Acteur, Risques, Date de validation, Colonne Travaux.

**Tests** (Infrastructure) :
- Profil d'import seedé : `TacheMultipleTypeLabels` contient bien les 2 entrées attendues.
- Profil d'export seedé : la règle `"Tâches multiples"` contient les 3 nouvelles colonnes, mêmes
  `Header`/`Source` que ci-dessus.
- Intégration bout-en-bout (fixture C7401, déjà connue pour produire `TM_PROC_MAD`/`TM_PROC_REL`,
  Lot T) : chaque ligne de chaque feuille dynamique porte le bon `Repère TM` (= repère de l'Équipement),
  le bon `TYPE ELEMENT CODE` (= `"MAD TRAVAUX"`), et le bon `Colonne Travaux` (`"Procédure MAD"` sur la
  feuille `TM_PROC_MAD`, `"Procédure REL"` sur `TM_PROC_REL"`).

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs`.

---

## 67.5. BlazorAdmin — édition de `TacheMultipleTypeLabels` dans `ImportProfileEditor.razor`

**Comportement attendu** :
- Nouvelle section root-level (au même niveau que Tableaux/Applications, Lot 059), listant les paires
  `Code`/`Label` déjà ajoutées, avec Modifier/Supprimer par ligne (même gabarit icône que le reste de
  la page, `AdminIconMarkup`).
- **Décision d'implémentation, déviation du plan initial ci-dessus** : implémenté **inline** dans
  `ImportProfileEditor.razor` (comme Tableaux/Applications), pas via un nouveau composant
  `TacheMultipleTypeLabelForm.razor` — vérification faite sur le code réel : les listes root-level de
  cette page (Tableaux/Applications) sont déjà entièrement inline, le patron `XxxForm.razor` dédié
  n'est utilisé que pour les listes imbriquées dans `SheetRuleForm`, un contexte différent. 2 champs
  `form-floating` (Code, Label) par ligne ; chaque candidat est construit via le constructeur Domain
  réel de `TacheMultipleTypeLabel` (catch `DomainValidationException` → `BusinessExceptionLocalizer`,
  même convention que le reste de la page) puis validé contre les doublons via
  `ImportProfile.ValidateTacheMultipleTypeLabelCode` (67.1).
- Suit le modèle d'enregistrement déjà en place (Lot 056/057) : commit explicite dans la liste en
  mémoire du composant racine, un seul `SaveAsync` au clic "Enregistrer le profil".
- Nouvelles clés `.resx` (`ImportProfileEditor_TacheMultipleTypeLabelsHeading`/`CodeLabel`/
  `CodePlaceholder`/`LabelLabel`/`LabelPlaceholder`/etc., EN/FR) — pas de duplication des clés
  Tableaux/Applications existantes, qui ne correspondent pas au même contenu.

**Tests** (BlazorAdmin, bUnit) : ajout/édition/suppression d'une entrée, doublon de `Code` rejeté avec
message localisé visible, persistance round-trip via `SaveProfileAsync` → `IImportProfileStore` →
relecture. Suivre le fichier dédié par lot déjà établi (`ImportProfileEditorLot067Tests.cs`) plutôt que
d'alourdir `ImportProfileEditorTests.cs`.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/`.

---

## Ordre d'implémentation recommandé

67.0 → 67.1 → 67.2 → 67.3 → 67.4 → 67.5. 67.4 dépend de 67.1/67.2/67.3 (sans eux, les nouvelles
colonnes seedées produiraient un résultat silencieusement vide/faux). 67.5 est indépendant du reste une
fois 67.1 posé (peut être fait en parallèle si besoin, mais suit ici l'ordre séquentiel par défaut).
