# Lot 074 — Registre de validation automatique des sous-formulaires (éditeurs de profil)

**Prérequis** : lot 073 livré (ses tests aller-retour servent de filet de non-régression pendant la
migration de ce lot).

## Contexte

Les éditeurs de profil imbriquent plusieurs niveaux d'état non persisté (`ImportProfileEditor` →
`SheetRuleForm` → `BlockFieldForm`, et leurs équivalents export), chacun avec son propre bouton de
validation. Une saisie laissée dans un niveau intermédiaire est perdue en silence si l'utilisateur
sauvegarde un niveau au-dessus : c'est le **défaut A** (voir le tableau des deux défauts dans
`tickets-tdd-lot-073-test-aller-retour-sous-formulaires-editeurs-profil.md`).

Corrigé deux fois au cas par cas (lot 056 ; commit `0cbac22`), par une chaîne de `@ref` et de `if` que
chaque parent doit écrire à la main pour chaque enfant. La section 9 de
`docs/conventions/recommandations-tickets-tdd.md` le dit elle-même : ce recensement « limite les
dégâts du patron actuel, il ne le corrige pas ». Ce lot remplace le branchement manuel par un
mécanisme où **un sous-formulaire est validé par son parent du seul fait d'exister**.

## Décision d'architecture (session du 16/09 avec Simon)

### Retenu : P1, registre automatique

- Un `EditScope` par niveau propriétaire, transmis aux descendants par `CascadingValue`.
- Une classe de base `NestedFormBase<TValue>` dont hérite tout sous-formulaire : elle s'inscrit dans le
  scope à l'initialisation et s'en retire à la destruction.
- Un parent valide tout son scope (`CommitAllAsync()`) avant de construire son propre objet.
- Un test d'architecture fait échouer tout sous-formulaire qui n'hérite pas de la base.

Critère qui a guidé le choix : **quand on oublie de brancher quelque chose, la panne doit être visible
immédiatement, pas silencieuse sur un ordre de clics inhabituel.**

### Écarté, avec la raison — à relire avant de rouvrir

| Option | Raison du rejet |
| :--- | :--- |
| **P2 — propagation continue** (chaque saisie remonte Valide/Invalide/Vide, plus de bouton par ligne) | Supprime A mais pas B ; change l'interface (bouton « Enregistrer » d'une ligne devenu « Fermer », messages d'erreur à la frappe) ; réécriture d'une large part des 734 références de tests aux boutons `add-/save-/modify-`. Même coût que P3 sans son bénéfice sur B. |
| **P3 — brouillon unique possédé par la racine** (le « ViewModel » du lot 056) | **Seule option qui élimine A et B par construction**, mais 2 à 3 lots et réécriture lourde des tests (estimation du lot 056 toujours valable). Arguments nouveaux par rapport au lot 056, consignés pour ne pas refaire l'analyse : le lot 056 comparait le **nombre de clics** face à **un** incident ; depuis, deux lots de correction supplémentaires, une convention, des fuites résiduelles (constat 2 ci-dessous) et trois occurrences du défaut B. **À reconsidérer si les éditeurs continuent de grossir** (chaque nouveau type de sous-liste renforce P3). |
| Autosave, versionnement, maître-détail par route | Écartés au lot 056, raisons inchangées (`tickets-tdd-lot-056-modele-enregistrement-editeurs-profil.md`). |
| Statu quo + correction ciblée des fuites | Laisse le risque entier pour chaque futur sous-formulaire ; c'est exactement ce qui a produit les trois incidents. |

## Constats (code au commit `8c05bc4`)

1. **Mécanisme actuel.** `SheetRuleForm.TryCommitAsync` (`SheetRuleForm.razor:989`) enchaîne 4 appels
   sur des `@ref` (`_editingBlockFieldFormRef`, `_editingHeaderFieldFormRef`,
   `_editingHeaderCompositeFormRef`, `_editingFieldPresencePointRuleFormRef`) puis 2 appels directs aux
   éditeurs en place (colonne inconditionnelle, règle de Point). `SheetGenerationRuleForm.TryCommitAsync`
   (`SheetGenerationRuleForm.razor:440`) fait de même sur 3 `@ref`. À la racine,
   `SaveProfileAsync` (`ImportProfileEditor.razor:1071`) n'appelle que `TryCloseOpenFormAsync`
   (`:962`), qui ne couvre que les formulaires de règle de feuille (lot 057).
2. **Fuites résiduelles du défaut A**, relevées à la lecture du code, **non reproduites** — chacune doit
   être confirmée par un test rouge avant correction :

   | # | Où | Nature |
   | :--- | :--- | :--- |
   | L1 | `SheetRuleForm`, ligne d'ajout d'une règle de Point (4 champs) | Pas de validation à la sortie du champ (exclue en 56.4) ni au flush : une ligne complète est perdue à la sauvegarde. `IsBlank()` (`:980`) ignore aussi `_newPointRule*` et `_newUnconditionalColonneName`. |
   | L2 | `HeaderFieldRuleForm`, ligne d'ajout | Idem (case à cocher, pas de validation à la sortie du champ). |
   | L3 | Lignes d'ajout de `BlockFieldForm`, `HeaderCompositeRuleForm`, `FieldPresencePointRuleForm`, colonne inconditionnelle | Validées à la sortie du champ **seulement si complètes** : une ligne partiellement remplie est abandonnée sans message. Et `Ctrl+Entrée` (`ImportProfileEditor.razor:1063`) sauvegarde sans sortie de champ : même une ligne complète est perdue. |
   | L4 | Lignes d'ajout de `ColumnDefinitionForm`, `PointColumnDefinitionForm`, `ApplicationColumnDefinitionForm` (export) | Pas de validation à la sortie du champ ni au flush. |
   | L5 | Racine import : lignes d'ajout **et** lignes en modification des Tableaux, Applications, libellés de types de tâches multiples (`_editingDefaultTableauIndex`, `_editingDefaultApplicationNameIndex`, `_editingTacheMultipleTypeLabelIndex`, `ImportProfileEditor.razor:669/674/686`) | `SaveProfileAsync` ne les valide jamais. |

   Dans un vrai navigateur, cliquer sur un bouton provoque d'abord la sortie du champ ; bUnit non. Les
   tests reproduisent donc L3 via `Ctrl+Entrée` et via une ligne partielle, les deux cas réels.
3. **Aucun test ne rend un sous-formulaire seul** (recherche de `Render<…Form>` dans
   `tests/ExcelETL.BlazorAdmin.Tests` : aucun résultat). La base peut donc exiger un scope et échouer
   bruyamment sans casser de test existant.
4. **Valeurs par défaut pré-remplies** : `PointColumnDefinitionForm._markValue` vaut `"X"`,
   `ApplicationColumnDefinitionForm._markValue` vaut `"O"`, la case « retirer le préfixe » de
   `HeaderFieldRuleForm` vaut `false`. Une ligne « vide » doit s'évaluer **sans** ces valeurs.
5. **Redonner le focus après validation** : `BlockFieldForm.razor:146`, `HeaderCompositeRuleForm.razor:113`,
   `FieldPresencePointRuleForm.razor:157`, `SheetRuleForm.razor:753/761`. Utile sur un geste
   utilisateur (saisie rapide, lot 056.4), sans objet — voire risqué sur un élément déjà démonté —
   pendant un flush.
6. **L'export n'a aucun éditeur de ligne en place** (aucun champ `_new…` dans `ExportProfileEditor`
   ni `SheetGenerationRuleForm`) : tout passe déjà par des composants.
7. **Les 8 tests `*NestedEditFlushTests.cs`** (commit `8c05bc4`) et les 734 références de tests aux
   boutons `add-/save-/modify-` constituent le filet de non-régression : ils doivent rester verts **sans
   modification**.

## Décisions de conception

| Sujet | Décision |
| :--- | :--- |
| Emplacement | `src/ExcelETL.BlazorAdmin/Editing/` (`EditScope.cs`, `NestedFormBase.cs`) — même niveau que `Formatting/`, `Excel/`. Présentation pure, aucune dépendance Application/Domain nouvelle. |
| `EditScope` | Classe simple : `Register`, `Unregister`, `CommitAllAsync()`. Parcourt **une copie** de la liste (un enfant validé en mode modification se démonte, donc se désinscrit, pendant le parcours). Ordre d'inscription ; **arrêt au premier échec** (comportement actuel). |
| Inscription | Dans `NestedFormBase`, `protected sealed override void OnInitialized()` inscrit puis appelle un `protected virtual void OnFormInitialized()`. Le `sealed` rend impossible, **à la compilation**, d'oublier `base.OnInitialized()` dans une classe dérivée — sans lui, l'oubli recréerait le défaut en silence. Les pré-remplissages actuels passent dans `OnFormInitialized`. |
| Scope absent | `InvalidOperationException` (message anglais, invariant développeur, hors i18n) à l'initialisation. Tout parent qui oublie de fournir un scope fait échouer tous ses tests existants. |
| Scopes imbriqués | Propriétaires : `ImportProfileEditor`, `ExportProfileEditor`, `SheetRuleForm`, `SheetGenerationRuleForm`. Chacun entoure ses enfants d'un `<CascadingValue Value="_childScope" IsFixed="true">`. `SheetRuleForm` est à la fois membre du scope racine et propriétaire du sien : son `TryCommitAsync` commence par `await _childScope.CommitAllAsync()`. |
| Contrat d'un membre | `abstract bool IsEditMode`, `abstract bool IsBlank` (sans les valeurs par défaut, constat 4), `abstract Task<bool> TryCommitAsync()`. La base expose `FlushAsync()` : **en mode ajout**, ligne vide ⇒ rien à faire ; sinon ⇒ `TryCommitAsync()`. **En mode modification, jamais de raccourci** : vider tous les champs d'un élément existant produit une erreur, pas une conservation silencieuse de l'ancienne valeur. |
| Ligne partielle à la sauvegarde | **Bloque** la sauvegarde. L'erreur propre à la ligne s'affiche (messages de validation existants, inchangés), la saisie reste en place, `SaveAsync` n'est pas appelé. |
| Message global | Une ligne fautive peut être loin de la barre d'enregistrement collante (lot 056.6) : un message en tête de page, `role="alert"`, en plus de l'erreur de ligne. Nouvelles clés par page : `ImportProfileEditor_PendingRowInvalidError` / `ExportProfileEditor_PendingRowInvalidError` (EN/FR). |
| Sortie du champ (56.4) | Inchangée : une ligne partielle à la sortie du champ ne produit toujours ni ajout ni message. |
| Clic explicite sur « Ajouter » d'une ligne vide | Inchangé (erreur actuelle). Le raccourci « vide ⇒ rien » ne vaut que pour le flush. |
| Focus (constat 5) | Uniquement sur geste utilisateur, jamais pendant un flush. |
| `OnSubmit` | Reste un paramètre de chaque composant dérivé (nom établi sur les 9 formulaires, sur lequel s'appuie le test d'architecture). |
| Bascule entre formulaires (lot 057) | `TryCloseOpenFormAsync` reste tel quel pour **ouvrir** un autre formulaire : ouvrir une règle de feuille ne doit pas valider une ligne de Tableaux en cours. Seul `SaveProfileAsync` passe à `CommitAllAsync()` sur le scope racine. |
| Identifiants HTML | **Strictement conservés**, y compris pour les éditeurs extraits en composants (074.3/074.4). |

## Étapes TDD

### 074.1 — `EditScope` et `NestedFormBase` (effort élevé)

Tests xUnit/bUnit dans `tests/ExcelETL.BlazorAdmin.Tests/Editing/`, avec un composant dérivé minimal
déclaré **dans le projet de tests** (pas de dépendance aux vrais formulaires) :

- les membres sont validés dans l'ordre d'inscription ;
- arrêt au premier échec, les suivants ne sont pas appelés ;
- un membre qui se désinscrit pendant `CommitAllAsync` ne fait pas lever d'exception de collection
  modifiée ;
- la destruction du composant le retire du scope ;
- mode ajout + vide ⇒ `TryCommitAsync` non appelé ; mode ajout + non vide ⇒ appelé ; mode modification
  ⇒ toujours appelé, même vide ;
- rendu sans scope ⇒ `InvalidOperationException`.

### 074.2 — Migration des 7 sous-formulaires composants et des 4 propriétaires

`BlockFieldForm`, `HeaderFieldRuleForm`, `HeaderCompositeRuleForm`, `FieldPresencePointRuleForm`,
`ColumnDefinitionForm`, `PointColumnDefinitionForm`, `ApplicationColumnDefinitionForm` héritent de la
base (`@inherits`). `SheetRuleForm` et `SheetGenerationRuleForm` héritent aussi de la base et
fournissent leur scope ; les deux racines fournissent le leur.

Supprimer les `@ref` d'édition et la chaîne de `if` de `SheetRuleForm.TryCommitAsync` (hors éditeurs
en place, traités en 074.3) et de `SheetGenerationRuleForm.TryCommitAsync`. Conserver les `@ref` de
`TryCloseOpenFormAsync` (lot 057).

**Rouge d'abord** (nouveau fichier par éditeur, `…PendingRowFlushTests.cs`) :

- L2 : ligne d'ajout d'en-tête complète, `#save-profile-button` ⇒ l'en-tête est dans le profil sauvegardé ;
- L4 : même scénario pour chacune des 3 lignes d'ajout export ;
- L3 via `Ctrl+Entrée` : ligne d'ajout de champ de bloc complète, `keydown` `Ctrl+Enter` sur
  `.profile-editor-container` sans sortie de champ ⇒ le champ est sauvegardé ;
- L3 ligne partielle : nom seul renseigné, `#save-profile-button` ⇒ `SaveAsync` jamais appelé, erreur de
  ligne affichée, message global affiché, saisie toujours présente ;
- ligne d'ajout vide ⇒ la sauvegarde aboutit normalement (non-régression du cas le plus fréquent) ;
- modification d'un élément existant vidé ⇒ erreur, pas de sauvegarde, ancienne valeur non restaurée
  en silence.

**Vert** : les 8 tests `*NestedEditFlushTests.cs` et les tests aller-retour du lot 073 passent sans
modification.

### 074.3 — Éditeurs en place de `SheetRuleForm` extraits en composants

Colonnes inconditionnelles (ajout + modification) et règles de Point conditionnelles (ajout +
modification) deviennent des composants dérivés de la base. Identifiants inchangés ;
`FormFloatingStructureAuditTests` doit rester vert.

Rouge d'abord : L1 (ligne d'ajout de règle de Point complète, sauvegarde du profil ⇒ règle présente) et
colonne inconditionnelle via `Ctrl+Entrée`. Supprimer ensuite les 2 appels directs restants de
`SheetRuleForm.TryCommitAsync` et élargir `IsBlank()` de la règle de feuille à ses lignes en cours.

### 074.4 — Listes de premier niveau de `ImportProfileEditor` extraites en composants

Tableaux, Applications et libellés de types de tâches multiples (ajout + modification) deviennent des
composants dérivés de la base, dans le scope racine. Identifiants inchangés.

Rouge d'abord : L5, pour chacune des 3 listes, ligne d'ajout complète puis sauvegarde, et élément en
modification changé puis sauvegarde sans cliquer sur sa coche.

### 074.5 — Test d'architecture

`tests/ExcelETL.BlazorAdmin.Tests/Architecture/NestedFormArchitectureTests.cs`, par réflexion sur
l'assemblage `ExcelETL.BlazorAdmin` : tout type dérivé de `ComponentBase` exposant une propriété
publique `OnSubmit` de type `EventCallback<T>` hérite de `NestedFormBase<T>`. Message d'échec qui
nomme le composant fautif et cite ce ticket.

Preuve de détection : retirer temporairement `@inherits` d'un sous-formulaire ⇒ ce test échoue (et, par
le constat 3, rien d'autre ne l'aurait signalé avant la production).

### 074.6 — Convention et documentation

- Réécrire la section 9 de `docs/conventions/recommandations-tickets-tdd.md` : le recensement manuel
  est remplacé par la règle « tout sous-formulaire ou éditeur de ligne est un composant dérivé de
  `NestedFormBase` », garantie par le test de 074.5. Conserver l'historique des incidents, citer ce
  ticket plutôt que recopier la conception.
- **Limite à y écrire honnêtement** : le test d'architecture détecte un composant qui n'hérite pas de
  la base, **pas** un nouvel éditeur de ligne écrit directement dans une page (champs `_new…` dans le
  `@code`). Ce cas reste couvert par la convention seule — c'est précisément ce que 074.3/074.4
  suppriment de l'existant.
- Mettre à jour `CLAUDE.md` (section « CURRENT SOLUTION STATE ») en fin de lot.

## Refactor à considérer

- Tableaux, Applications et colonnes inconditionnelles sont trois éditeurs d'une seule chaîne : un
  composant générique paramétré par un validateur est envisageable **seulement** s'il reproduit
  exactement les identifiants existants de chaque liste. Sinon, trois composants — la duplication est
  acceptée dans ce projet quand l'abstraction coûterait plus cher qu'elle ne rapporte.
- Si `OnDirty` (lot 056.3) se révèle répété à l'identique dans tous les composants dérivés, le remonter
  dans la base est un bon candidat — mais pas une obligation de ce lot.
- Évaluer, sans l'imposer, une heuristique de test complémentaire contre la limite de 074.6 (champs
  privés nommés `_new…` dans les pages propriétaires). À ne retenir que si elle reste lisible et peu
  fragile.

## Hors périmètre explicite

- **Le défaut B** : lot 073.
- **P2, P3, autosave, versionnement, maître-détail** : écartés (voir la décision ci-dessus).
- **La bascule entre formulaires** du lot 057 et la validation à la sortie du champ du lot 056.4 :
  inchangées.
- **Tout changement visuel** autre que le message global ; aucun nouveau bouton, aucune classe CSS
  modifiée.
- **Les pages de test** et les listes de profils ; `Users.razor`, `Profile.razor`.
- **Domain, Application, Infrastructure, WebAPI** : aucune modification.

## Notes d'exécution

- Effort élevé pour 074.1 (conception du contrat) et pour le refactor de 074.2 ; standard pour
  074.3–074.6.
- Tests filtrés pendant l'itération :
  `dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter "FullyQualifiedName~Editing|FullyQualifiedName~PendingRowFlush|FullyQualifiedName~NestedEditFlush|FullyQualifiedName~RoundTrip" --verbosity quiet`.
  Clôture de chaque sous-ticket : `ExcelETL.BlazorAdmin.Tests` complet (projet unique touché). Pas de
  run de la solution entière.
- Un commit par sous-ticket.
- En cas d'échec inattendu d'un test existant après migration (notamment l'ordre de rendu des
  `CascadingValue`), s'arrêter et inspecter le diff plutôt que d'ajuster le test.

## Résultat

_À remplir en fin de lot._
