# Lot 074 — Pilote « brouillon unique possédé par la racine » (P3) sur l'éditeur de profil d'export

**Prérequis** : lot 073 livré. Ses tests aller-retour bUnit sont le filet boîte noire de cette
réécriture interne.

## Contexte

Les éditeurs de profil imbriquent plusieurs niveaux d'état non persisté, chacun avec son propre bouton
de validation. Deux défauts en découlent (tableau complet dans
`tickets-tdd-lot-073-test-aller-retour-sous-formulaires-editeurs-profil.md`) :

- **A — brouillon non validé** : une saisie laissée dans un niveau intermédiaire est perdue si l'on
  sauvegarde un niveau au-dessus (lot 056, commit `0cbac22`) ;
- **B — champ perdu à la reconstruction** : un formulaire reconstruit l'objet métier à partir des seuls
  champs qu'il connaît (lot 048.1, commits `1736886` et `0cbac22`).

Chaque occurrence a été corrigée sur place. La cause est le patron lui-même : l'état de chaque niveau
vit dans son composant, et le parent doit penser à le récupérer.

## Décision d'architecture (sessions du 16/09 avec Simon)

### Retenu : P3, en pilote sur l'export, sans changement visible

- **Un seul brouillon, possédé par la page racine.** Une arborescence de classes brouillon (objets de
  données mutables, sans comportement) reproduit la forme du profil. Les sous-formulaires se lient
  directement aux propriétés du brouillon qu'on leur passe : **ils n'ont plus d'état de données propre**.
- **Deux conversions pures centralisées** : `FromDomain(profil) → brouillon` au chargement,
  `ToDomain(brouillon) → profil ou erreurs` à la validation. Le domaine reste la seule source de vérité
  des règles métier : la conversion appelle ses constructeurs, elle ne duplique aucune règle.
- **Défaut A supprimé par construction** : une saisie vit dans le brouillon dès la frappe ; il n'existe
  plus d'endroit où elle pourrait rester sans être lue par la sauvegarde.
- **Défaut B supprimé par construction** : la correspondance brouillon ↔ domaine tient en un seul
  endroit, gardé par un test unitaire aller-retour sur le profil standard.
- **Pilote sur l'export d'abord** : le plus petit éditeur (voir constats), déjà victime du défaut B.
  L'import (`SheetRuleForm`, 1 145 lignes) ne sera rédigé qu'après le bilan de ce pilote.
- **Aucun changement visible** : mêmes identifiants HTML, mêmes gestes, mêmes messages. Les lignes
  « Ajouter » restent, sous forme de brouillons « en attente ». Mêler un changement d'interface à un
  changement d'architecture empêcherait d'attribuer une casse à l'un ou à l'autre.

Critères qui ont tranché, à la demande de Simon : **simplicité, facilité de relecture, réduction de la
dette**. Sur ces trois axes, P3 est la seule piste qui retire du code et des concepts au lieu d'en
ajouter.

### Écarté, avec la raison — à relire avant de rouvrir

| Option | Raison du rejet |
| :--- | :--- |
| **P1 — registre de validation automatique** (classe de base, `EditScope` en cascade, test d'architecture) | Livraison plus rapide, mais ajoute un mécanisme implicite (inscription par valeur en cascade, cycle de vie scellé) **par-dessus** la structure actuelle, qui reste entière ; chaque ajout de champ garde 6 points à toucher et rend `IsBlank` critique (un oubli y recrée le défaut A — l'`IsBlank` actuel de `SheetRuleForm` oublie déjà les lignes de règles de Point en cours) ; ne traite pas B. **Repli prévu si le pilote n'est pas concluant.** Première version de ce ticket (commit `2608120`), remplacée. |
| **P2 — propagation continue** | Change l'interface et casse autant de tests que P3 sans traiter B. |
| **Statu quo + correction ciblée des fuites** | Laisse le risque entier pour chaque futur sous-formulaire. |
| Autosave, versionnement, maître-détail par route | Écartés au lot 056, raisons inchangées (`tickets-tdd-lot-056-modele-enregistrement-editeurs-profil.md`). |

### Réouverture assumée de la décision « ViewModel écarté » du lot 056

Le lot 056 avait écarté ce modèle pour « 2 à 3 lots et une forte réécriture des tests, pour le même
nombre de clics ». Ce qui a changé :

- le critère n'est plus le nombre de clics mais la maintenance : au moins 6 évolutions ont ajouté un
  champ à ces formulaires en deux mois (048, 063, 067, trois fois la couleur d'étiquette,
  `FieldPresencePointRule` le 16/09) ;
- deux lots de correction supplémentaires et une convention ont été nécessaires depuis, et des fuites
  subsistent (constat 4) ; le défaut B, que le flush ne couvre pas, s'est produit trois fois ;
- **l'estimation de réécriture des tests était surestimée** : les tests bUnit pilotent la page par ses
  identifiants et ses gestes ; une réécriture interne qui les conserve ne les casse pas. La casse
  n'aurait été massive qu'en changeant l'interface — ce que ce lot s'interdit. Ce pilote sert
  précisément à le mesurer.

## Constats (code au commit `2608120`)

1. **Taille de l'éditeur d'export** : `ExportProfileEditor.razor` (434 lignes, 11 champs d'état),
   `SheetGenerationRuleForm.razor` (510 lignes, 10 champs d'état, 3 `@ref`),
   `ColumnDefinitionForm`/`PointColumnDefinitionForm`/`ApplicationColumnDefinitionForm` (151/137/142
   lignes, 3 champs d'état chacun). À comparer avec `ImportProfileEditor` (33) et `SheetRuleForm` (34).
2. **Mécanisme actuel** : `SheetGenerationRuleForm.TryCommitAsync` (`:440`) valide les 3 sous-formulaires
   ouverts via `@ref`, puis construit `SheetGenerationRule`. `ExportProfileEditor.SaveProfileAsync`
   (`:376`) ne valide que le formulaire de règle ouvert, via `TryCloseOpenFormAsync` (`:276`, lot 057).
3. **Points à préserver à l'identique** :
   - `ConstantColumnDefinitions` n'est pas éditable dans l'interface mais **doit** être reporté
     (`SheetGenerationRuleForm.razor:300/330`, bogue du commit `0cbac22`) ;
   - passer la source pivot à `TacheMultiple` vide les colonnes Point et Application déjà ajoutées
     (`OnPivotSourceChanged`, `:345`, lots T6/U4) ; revenir en arrière ne les restaure pas ;
   - les listes de sources d'une colonne sont filtrées par la source pivot
     (`ColumnDefinitionForm.AvailableFieldRefs`, `:109`) ; une source vide signifie « non mappée »
     (`null`) ;
   - valeurs pré-remplies des lignes d'ajout : `MarkValue = "X"` (Point), `"O"` (Application) —
     assertées par des tests (`ExportProfileEditorTests.cs:1137`, `:1224`) ;
   - lot 043 (confirmation de navigation), lot 056 (indicateur de modification, `Ctrl+Entrée`, barre
     collante), lot 057 (un seul formulaire de règle ouvert ; basculer valide d'abord le formulaire
     ouvert et refuse si invalide ; « Annuler » abandonne), lot 059 (bouton de sauvegarde désactivé sans
     modification).
4. **Fuite résiduelle du défaut A côté export (L4)**, relevée à la lecture, non reproduite : une ligne
   d'ajout de colonne, colonne Point ou colonne Application **remplie mais pas ajoutée** est perdue à la
   sauvegarde (ni validation à la sortie du champ, ni flush). Même chose via `Ctrl+Entrée`.
5. **Aucun test ne rend un sous-formulaire seul**, et l'export n'a aucun éditeur de ligne écrit
   directement dans la page : tout passe par les 3 composants de colonnes.
6. **Tests exposés** : `ExportProfileEditorTests.cs` (99), `ExportProfileEditorLot056Tests.cs` (17),
   `ExportProfileEditorLot057Tests.cs` (16), `ExportProfileEditorLot059Tests.cs` (9),
   `ExportProfileEditorNestedEditFlushTests.cs` (4), plus les tests export du lot 073 et, partagés avec
   l'import, `ProfileEditorParityTests.cs` et `FormFloatingStructureAuditTests.cs`.
7. **Le profil standard d'export** couvre les colonnes à source `null`, les colonnes Point, Application
   et constantes, et les trois sources pivot — référence suffisante pour l'aller-retour unitaire. Il se
   charge par `DefaultProfileSeeder.SeedAsync()` sur EF Core InMemory, comme dans
   `tests/ExcelETL.BlazorAdmin.Tests/Formatting/BlockFieldRangeFormatterTests.cs`.

## Décisions de conception

| Sujet | Décision |
| :--- | :--- |
| Emplacement | `src/ExcelETL.BlazorAdmin/Editing/` (outils génériques) et `Editing/Export/` (brouillons et conversions de l'export). Présentation pure, aucune dépendance nouvelle vers Application/Domain au-delà des types déjà utilisés par les formulaires. Tests en miroir sous `tests/ExcelETL.BlazorAdmin.Tests/Editing/`. |
| Classes brouillon | `ExportProfileDraft` (`Id`, `Name`, `SheetRules`, `PendingSheetRule`), `SheetGenerationRuleDraft` (`SheetName`, `PivotSource`, `Columns`, `PointColumns`, `ApplicationColumns`, `ConstantColumns`, et une ligne en attente par liste), `ColumnDefinitionDraft` (`Header`, `SourceValue`), `PointColumnDefinitionDraft` (`ColonneNom`, `Header`, `MarkValue = "X"`), `ApplicationColumnDefinitionDraft` (`ApplicationNom`, `Header`, `MarkValue = "O"`). Propriétés publiques simples, aucune méthode métier. |
| `ConstantColumns` | Gardées telles quelles, en objets du domaine (immuables, jamais éditées) : pas de brouillon pour ce qui ne s'édite pas. |
| Conversions | `ExportProfileDraftMapper`, statique et pur : `FromDomain`, `ToDomain(profil)`, et les conversions unitaires d'une règle et d'une colonne (réutilisées par les boutons de ligne et la bascule du lot 057 — **un seul chemin de validation**). Construit les enfants avant le parent, pour attacher chaque erreur au bon élément. |
| Erreurs | La conversion renvoie la valeur **ou** la liste des couples (brouillon fautif, exception). Elle ne localise rien (pas de dépendance au localiseur). La page applique : chaque brouillon porte une propriété d'affichage `Error`, exclue de la sérialisation (`[JsonIgnore]`) ; le composant l'affiche là où il affiche aujourd'hui son message local. |
| Ligne « en attente » vide | Générique, sans code par champ : un brouillon est vide s'il est identique à une instance neuve de son type, comparaison par sérialisation JSON (`DraftJson.IsPristine`). Les valeurs pré-remplies (`"X"`, `"O"`) sont donc ignorées d'office. |
| Ligne en attente à la sauvegarde | Vide ⇒ ignorée. Non vide et valide ⇒ ajoutée **en fin de liste**, comme un clic sur « Ajouter ». Non vide et invalide ⇒ sauvegarde bloquée, erreur affichée sur la ligne, saisie conservée, `SaveAsync` non appelé. |
| Message global | Une ligne fautive peut être loin de la barre collante (lot 056.6) : message en tête de page, `role="alert"`. Nouvelle clé `ExportProfileEditor_PendingRowInvalidError` (EN/FR). **Seul ajout visible du lot.** |
| « Annuler » d'un élément | À l'ouverture en modification, la page garde une copie du brouillon (`DraftJson.Clone`, sérialisation JSON, sans code par champ). « Annuler » remet la copie en place. |
| Bouton d'enregistrement d'une ligne | En modification : convertit l'élément seul ; valide ⇒ referme, invalide ⇒ reste ouvert avec son erreur. En ajout : convertit la ligne en attente ; valide ⇒ l'ajoute à la liste et la remplace par une instance neuve. |
| Invariant | **Tout élément refermé est valide** (on ne referme qu'après conversion réussie, ou par « Annuler » qui restaure une copie valide). Les cartes de résumé peuvent donc afficher le brouillon sans cas d'erreur. |
| Lot 057 | Basculer vers un autre formulaire convertit la règle ouverte (lignes en attente comprises) ; échec ⇒ refus, comme aujourd'hui. « Annuler » et la fermeture par le bouton bascule n'essaient rien. |
| Indicateur de modification (lots 043, 056, 059) | **Conservé tel quel pour le pilote** (`OnDirty` et `_hasUnsavedChanges`), pour ne changer qu'une variable à la fois. Voir « Refactor à considérer ». |
| `TacheMultiple` | La règle « vider les colonnes Point/Application » reste dans `SheetGenerationRuleForm`, appliquée au brouillon. C'est une règle d'interface, pas une conversion. |
| Identifiants HTML et paramètres des composants | Strictement conservés (`IdPrefix`, `SubmitButtonId`, `ShowCancel`, etc.). Seul le paramètre de pré-remplissage (`InitialRule`, `InitialColumn`, …) est remplacé par le brouillon lié. |
| Ce qui disparaît | Dans les 5 composants : les champs d'état de données, les pré-remplissages `OnInitialized`, les 4 `TryCommitAsync`, les 4 `ResetForm`, `IsBlank`, les 3 `@ref` de sous-formulaires et la chaîne de validation imbriquée, les `@ref` de `TryCloseOpenFormAsync`. |

## Étapes TDD

### 074.1 — Outils génériques `DraftJson` (effort standard)

`Clone<T>` et `IsPristine<T>` (xUnit) : copie profonde indépendante (modifier la copie ne touche pas
l'original, listes comprises) ; instance neuve ⇒ vierge ; valeur par défaut non vide (`MarkValue = "X"`)
⇒ toujours vierge ; un seul champ renseigné ⇒ non vierge ; `Error` ignorée par les deux fonctions ;
objets du domaine immuables embarqués (`ConstantColumnDefinition`) copiés correctement — **à vérifier
tel quel**, pas à supposer (constructeur de record à paramètres).

### 074.2 — `ExportProfileDraftMapper` (effort élevé)

Tests xUnit, sans bUnit :

- **aller-retour** : profil standard d'export ⇒ `FromDomain` ⇒ `ToDomain` ⇒ équivalent, ordre strict,
  `Id` conservé (garde-fou définitif du défaut B côté export) ;
- brouillon sans `Id` ⇒ nouveau profil ;
- colonne à en-tête vide ⇒ erreur rattachée à **ce** brouillon de colonne, pas à la règle ;
- colonne Point sous `TacheMultiple`, en-tête dupliqué, source incompatible avec la source pivot ⇒
  erreur rattachée à la règle ;
- plusieurs éléments invalides ⇒ toutes les erreurs remontées (pour les afficher toutes, pas seulement
  la première) ;
- ligne en attente vide ignorée ; non vide valide ajoutée en fin de liste ; non vide invalide ⇒ erreur
  sur la ligne ;
- `ConstantColumns` reportées à l'identique.

### 074.3 — Migration de l'éditeur d'export (effort élevé)

**Rouge d'abord** (nouveau fichier `ExportProfileEditorPendingRowTests.cs`), sur l'interface actuelle :

- L4 : pour chacune des 3 lignes d'ajout, ligne complète, `#save-export-profile-button` ⇒ la colonne est
  dans le profil sauvegardé ;
- idem via `Ctrl+Entrée` sur `.profile-editor-container` ;
- ligne partielle ⇒ `SaveAsync` jamais appelé, erreur de ligne et message global affichés, saisie
  conservée ;
- ligne d'ajout vide ⇒ sauvegarde normale (le cas le plus fréquent) ;
- élément existant vidé en modification puis sauvegarde du profil ⇒ erreur, pas de sauvegarde,
  ancienne valeur non restaurée en silence ;
- élément existant modifié puis « Annuler » puis sauvegarde ⇒ ancienne valeur.

**Vert** : migrer `ExportProfileEditor`, `SheetGenerationRuleForm` et les 3 formulaires de colonnes sur
les brouillons, en une seule étape (un état intermédiaire mi-brouillon mi-domaine ne serait pas
cohérent). **Tous les tests existants listés au constat 6 et ceux du lot 073 passent sans modifier
leurs assertions.** Un test qui ne passe pas : s'arrêter et inspecter avant de le modifier. S'il
teste un détail interne plutôt qu'un comportement, le signaler dans « Résultat » avec la raison.

### 074.4 — Bilan du pilote et décision (effort élevé)

Remplir la section « Résultat » avec des mesures, pas des impressions :

- lignes ajoutées/supprimées par fichier (`git diff --stat` sur la migration) ;
- champs d'état et méthodes restants dans les 5 composants, contre les chiffres du constat 1 ;
- nombre de tests existants dont une assertion a dû changer, avec la raison de chacun ;
- points à toucher pour ajouter un champ à `ColumnDefinition`, avant (formulaire, pré-remplissage,
  construction, remise à zéro, balisage) et après ;
- difficultés rencontrées (rafraîchissement, sérialisation, lot 057) et comment elles ont été résolues.

**Critères proposés, à arbitrer avec Simon sur ces mesures** :

- poursuite vers l'import si : assertions de tests existants inchangées (hors cas signalés et
  justifiés), solde de lignes négatif ou nul sur les composants migrés, aucun code par champ apparu
  dans les outils génériques, relecture jugée plus simple par Simon ;
- arrêt et repli sur P1 si : un nombre significatif de tests doit changer d'assertion, ou si
  `DraftJson`/les conversions imposent du code spécifique par type.

### 074.5 — Documentation

- Mettre à jour `CLAUDE.md` (« CURRENT SOLUTION STATE ») : nouveau dossier `Editing/`, éditeur d'export
  sur brouillons, bilan du pilote.
- **Ne pas encore réécrire** la section 9 de `docs/conventions/recommandations-tickets-tdd.md` : l'import
  fonctionne toujours sur l'ancien modèle et la section y reste valable. Elle sera réécrite à la fin de
  la migration de l'import.
- En cas de poursuite : ne **pas** rédiger ici les tickets de l'import. Ils feront l'objet d'une session
  à part, sur la base des mesures de 074.4.

## Refactor à considérer

- **Indicateur de modification par comparaison** : `_hasUnsavedChanges` pourrait devenir « le brouillon
  sérialisé diffère de celui du chargement » (générique, exact même après « Annuler », et supprime tout
  le câblage `OnDirty`). À n'adopter **que si** tous les tests des lots 043, 056 et 059 restent verts sans
  modification ; sinon, laisser pour le lot de l'import et le noter dans le bilan.
- Si la page applique les erreurs aux brouillons par un parcours écrit à la main, envisager que la
  conversion renvoie directement ce parcours plutôt que de dupliquer la connaissance de l'arborescence.
- Rendu des cartes de résumé depuis le brouillon : réutiliser les conversions unitaires plutôt que
  recoder l'affichage (par exemple le libellé de source) s'il y a doublon.

## Hors périmètre explicite

- **L'éditeur d'import** (`ImportProfileEditor`, `SheetRuleForm` et ses sous-formulaires) : aucune
  modification ; son mécanisme de flush actuel reste en place jusqu'à sa propre migration.
- **Tout changement d'interface** autre que le message global : pas d'ajout immédiat d'élément vide,
  pas de changement de libellé, de classe CSS ou d'identifiant.
- **P1, P2, autosave, versionnement, maître-détail** : écartés (voir ci-dessus).
- **La persistance** (`IExportProfileStore`, EF Core) et **le domaine** : aucune modification.
- Les pages de test d'export, la liste des profils d'export.

## Notes d'exécution

- Tests filtrés pendant l'itération :
  `dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter "FullyQualifiedName~Editing|FullyQualifiedName~ExportProfileEditor|FullyQualifiedName~ProfileEditorParity|FullyQualifiedName~FormFloatingStructureAudit" --verbosity quiet`.
  Clôture de 074.3 : `ExcelETL.BlazorAdmin.Tests` complet (seul projet touché).
- Un commit par sous-ticket ; 074.3 est volontairement un seul commit (voir son étape verte).
- Avant de modifier une assertion existante : lire le diff et comprendre la cause, ne pas relancer en
  boucle (`docs/conventions/recommandations-tickets-tdd.md`, section 5).

## Résultat

Mesures prises au commit `3c88420` (074.3), sur `git diff --stat 07425bc 3c88420` (07425bc =
état juste après 074.2, avant toute migration).

### Lignes de code

| Fichier | Avant (constat 1) | Après | Delta |
| :--- | ---: | ---: | ---: |
| `ExportProfileEditor.razor` | 434 | 517 | +83 |
| `SheetGenerationRuleForm.razor` | 510 | 552 | +42 |
| `ColumnDefinitionForm.razor` | 151 | 100 | −51 |
| `PointColumnDefinitionForm.razor` | 137 | 85 | −52 |
| `ApplicationColumnDefinitionForm.razor` | 142 | 85 | −57 |
| **Total, 5 composants migrés** | **1 374** | **1 339** | **−35** |

Solde négatif sur l'ensemble des composants migrés, confirmé par `git diff --stat` sur le commit
074.3 lui-même (`404 insertions(+), 439 deletions(-)`, soit −35). Les deux composants racines
(éditeur, formulaire de règle) grossissent — la logique de conversion/promotion/annulation qui vivait
implicitement dans le remontage de composant (React-like "unmount = reset") doit maintenant être
écrite explicitement quelque part, et c'est le niveau qui possède la liste qui en hérite. Les 3
formulaires de colonnes, devenus de purs lieurs de champs, perdent entre un tiers et 40 % de leurs
lignes chacun.

En plus de ces 5 fichiers : 413 lignes de nouvelle infrastructure générique (`Editing/`,
`Editing/Export/` — DraftJson, ConversionResult, DraftConversionError, IDraftWithError, les 4 classes
brouillon, ExportProfileDraftMapper), et 249 lignes de test rouge-d'abord
(`ExportProfileEditorPendingRowTests.cs`) qui n'existaient pas avant.

### Champs d'état et méthodes restants, contre le constat 1

- **`ExportProfileEditor.razor`** : 11 champs (`_draft`, `_editingId`, `_notFound`,
  `_profileErrorMessage`, `_pendingDeleteIndex`, `_expandedSheetRuleDetails`,
  `_sheetRuleEditSnapshot`, `_openSheetRuleForm`, `_hasUnsavedChanges`,
  `_showNavigationConfirmation`, `_pendingNavigationTarget`) contre 13 avant (`_name`+`_sheetRules`
  fusionnés en `_draft` ; `_addSheetRuleFormRef`/`_editSheetRuleFormRef` disparus ;
  `_sheetRuleEditSnapshot` apparu pour « Annuler »). **0 `@ref`** (2 avant).
- **`SheetGenerationRuleForm.razor`** : 6 champs d'état propres (`_editingColumnIndex`,
  `_editingPointColumnIndex`, `_editingApplicationColumnIndex`, `_columnEditSnapshot`,
  `_pointColumnEditSnapshot`, `_applicationColumnEditSnapshot`) contre 13 avant (les 6 listes/valeurs
  brouillon, les 3 index d'édition, les 3 `@ref`, `_errorMessage`). **0 `@ref`** (3 avant). Plus de
  `InitialRule`/`OnInitialized`/`ResetForm`/`IsBlank`/`TryCommitAsync` publics.
- **`ColumnDefinitionForm.razor`/`PointColumnDefinitionForm.razor`/`ApplicationColumnDefinitionForm.razor`** :
  **0 champ d'état propre** chacun (contre 3 avant : les valeurs du formulaire + `_errorMessage`). Plus
  de `InitialXxx`/`OnInitialized`/`ResetForm`/`TryCommitAsync` du tout — chacun se réduit à des
  liaisons de champs + deux `@onclick` qui relaient `OnSubmit`/`OnCancel` sans validation locale.

### Tests existants dont une assertion a dû changer

**Aucun.** Les 252 tests exposés au constat 6 (`ExportProfileEditorTests.cs`,
`ExportProfileEditorLot056Tests.cs`, `ExportProfileEditorLot057Tests.cs`,
`ExportProfileEditorLot059Tests.cs`, `ExportProfileEditorNestedEditFlushTests.cs`, les tests export du
lot 073, `ProfileEditorParityTests.cs`, `FormFloatingStructureAuditTests.cs`) sont passés sans qu'une
seule ligne d'assertion soit modifiée. Un seul test a exigé une intervention —
`ReopeningAfterPartialInput_FieldsAreEmpty_ProvingRemount` (`ExportProfileEditorLot057Tests.cs`) — et
son **assertion elle-même n'a pas changé** : c'est le code de production
(`ToggleAddSheetRuleFormAsync`) qui a été complété pour continuer à produire le même comportement
observable. Sous l'ancienne architecture, fermer le formulaire d'ajout via le bouton bascule
détruisait implicitement le composant enfant (et son état local) ; sous P3, l'état vit dans
`_draft.PendingSheetRule`, qui ne disparaît plus tout seul — fermer sans avoir cliqué « Ajouter » doit
donc désormais remettre explicitement `_draft.PendingSheetRule` à une instance neuve (le même geste
que « Annuler » ailleurs dans ce composant). Un vrai comportement à préserver, pas un détail interne :
sans ce correctif, rouvrir le formulaire d'ajout après une saisie abandonnée aurait fait réapparaître
cette saisie.

### Points à toucher pour ajouter un champ à `ColumnDefinition`

**Avant** (5 catégories, ~7 points concrets) : le record Domain ; un champ local dans
`ColumnDefinitionForm` ; son pré-remplissage dans `OnInitialized` ; sa construction dans
`TryCommitAsync` ; sa remise à zéro dans `ResetForm` ; le balisage (`<input>`/label) ; et, si affiché
dans le résumé, la lecture correspondante dans `ExportProfileEditor.razor`.

**Après** (4 points concrets) : le record Domain (inchangé) ; une propriété sur
`ColumnDefinitionDraft` ; deux lignes dans `ExportProfileDraftMapper`
(`FromDomainColumn`/`ConvertColumn`) ; le balisage, lié directement à `Draft.LeChamp` (aucun
pré-remplissage ni remise à zéro à écrire). Le résumé, si affiché, reste un point identique aux deux
architectures. Un champ oublié dans le mapper se voit immédiatement au test d'aller-retour du lot
073/074.2 (`RoundTrip_SeededDefaultProfile_...`), qui compare membre à membre — c'est exactement le
filet qui aurait détecté le bogue du commit `0cbac22` avant qu'il ne parte en production.

### Difficultés rencontrées

- **Rafraîchissement** : aucune difficulté réelle. Remplacer `_draft.SheetRules[index]` par une copie
  restaurée (« Annuler ») se reflète automatiquement au rendu suivant, puisque la boucle `@for` relit
  `_draft.SheetRules[index]` à chaque rendu plutôt que de capturer une référence figée.
- **Sérialisation** : aucun problème rencontré avec `System.Text.Json` par défaut, y compris pour
  `ConstantColumnDefinition` (record Domain immuable à un seul constructeur paramétré, sans
  `[JsonConstructor]`) — vérifié par un test dédié (074.1) plutôt que supposé.
- **Lot 057** : la seule vraie surprise du pilote (voir ci-dessus, « Tests existants ») — fermer le
  formulaire d'ajout sans soumettre doit désormais réinitialiser explicitement le brouillon en attente,
  faute de quoi le remontage implicite qui faisait ce travail avant n'existe plus.
- **Message global à la sauvegarde** : décider quel message afficher dans la bannière de tête de page
  (`_profileErrorMessage`) a demandé une distinction explicite entre une erreur portée par le
  brouillon racine (nom vide/trop long, aucune règle de feuille — message réel, inchangé depuis avant
  ce lot) et une erreur portée par un brouillon imbriqué (ligne en attente invalide — nouveau message
  générique `ExportProfileEditor_PendingRowInvalidError`), par comparaison de référence entre le
  brouillon fautif et `_draft` lui-même.

### Recommandation

Les trois critères mesurables du 074.4 sont remplis : assertions de tests existants inchangées (0/252,
hors le seul cas signalé et justifié ci-dessus) ; solde de lignes négatif sur les composants migrés
(−35, avant la nouvelle infrastructure générique) ; aucun code spécifique à `ExportProfile`/
`SheetGenerationRule`/etc. n'est apparu dans `DraftJson`/`ConversionResult`/`DraftConversionError`
(vérifié par lecture directe de ces 3 fichiers, toujours génériques sur `T`/`TDraft`/`TDomain`). Le
quatrième critère — relecture jugée plus simple — reste à trancher par Simon, non mesurable depuis
cette session.

Sur la base des trois critères mesurables, ce pilote justifie une poursuite vers l'éditeur d'import
(`ImportProfileEditor`/`SheetRuleForm` et ses sous-formulaires), à confirmer avec Simon avant d'ouvrir
les tickets correspondants (hors périmètre de ce document, par instruction explicite du 074.5).
