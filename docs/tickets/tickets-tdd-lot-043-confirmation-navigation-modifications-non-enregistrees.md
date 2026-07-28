# Tickets TDD — Lot 043 : confirmation de navigation avant perte de modifications non enregistrées

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Dixième lot
utilisant la convention numérique à trois chiffres, après le lot 042
(`tickets-tdd-lot-042-validation-titres-parite-editeurs.md`).*

**Contexte** : `ImportProfileEditor.razor`/`ExportProfileEditor.razor` n'enregistrent en base
qu'au clic explicite sur `#save-profile-button`/`#save-export-profile-button` — comportement
volontaire, **non remis en cause par ce lot**. Le risque identifié est purement UX : si
l'utilisateur navigue vers une autre page via un lien de la sidebar, ou revient en arrière, ses
modifications en cours sont perdues **sans aucun avertissement**, ce qui peut créer un faux
sentiment de sécurité (l'utilisateur croit avoir enregistré alors que non).

**Principe directeur de ce lot (KISS/YAGNI)** : on ajoute un avertissement avant perte de données,
pas un mécanisme de sauvegarde alternatif. Pas de nouvelle persistance, pas de comparaison d'état
profonde (diff JSON avant/après), pas de nouvelle abstraction partagée entre les deux éditeurs —
un simple indicateur booléen "modifications en attente" suffit, porté par le composant natif
Blazor `<NavigationLock>` (disponible nativement depuis .NET 7, aucune dépendance ajoutée).

**Ce que ce lot n'est pas** :
- Ni une sauvegarde automatique (auto-save) — le clic explicite sur Enregistrer reste le seul
  chemin de persistance, décision non réouverte.
- Ni un système de suivi précis "quel champ exact a changé" — un seul indicateur global par
  éditeur (`_hasUnsavedChanges`), pas un état par champ.
- Ni une nouvelle abstraction de composant partagé entre `ImportProfileEditor.razor` et
  `ExportProfileEditor.razor` — la logique est dupliquée entre les deux, cohérent avec le niveau
  de duplication déjà toléré entre ces deux fichiers ailleurs dans le projet (voir
  `audit-qualite-blazoradmin-2026-07-25.md` §4).
- Ni une extension à d'autres pages (`Login.razor`/`Register.razor`/`Profile.razor`,
  `ImportProfileTest.razor`/`ExportProfileTest.razor`) — ces pages ne portent pas de construction
  de profil multi-étapes comparable, hors périmètre sauf nouvelle demande explicite.

---

## 43.0. Investigation préalable (obligatoire avant tout code)

- [ ] Confirmer que `ImportProfileEditor.razor`/`ExportProfileEditor.razor` utilisent bien un
  `EditForm`/`EditContext` Blazor standard (probable, au vu de l'usage de `<ValidationMessage>`
  ailleurs dans le projet) — si oui, `EditContext.OnFieldChanged` est le point d'accroche unique
  et le plus simple pour détecter une modification sur les champs racine (`_name`,
  `_reperePrefix`/nom du profil, `_equipementTypeElementNom`), sans instrumenter chaque champ un
  par un.
- [ ] Recenser les méthodes déjà existantes de mutation des listes en mémoire (ajout/modification/
  suppression de `SheetExtractionRule`/`SheetGenerationRule` et de leurs sous-listes imbriquées
  via `SheetRuleForm.razor`/`BlockFieldForm.razor`/`SheetGenerationRuleForm.razor`/
  `ColumnDefinitionForm.razor`/`PointColumnDefinitionForm.razor`/
  `ApplicationColumnDefinitionForm.razor`) — ce sont les points où `_hasUnsavedChanges = true`
  doit être positionné côté éditeur racine (probablement déjà des callbacks remontant vers le
  composant parent, à vérifier avant d'ajouter un mécanisme d'événement supplémentaire).
- [ ] **Vérifier la faisabilité réelle d'un test bUnit couvrant `<NavigationLock>`** :
  `NavigationLock` s'enregistre auprès du `NavigationManager` via un mécanisme interne au routeur
  Blazor ; confirmer si bUnit (avec son `TestNavigationManager`) permet de déclencher
  `OnBeforeInternalNavigation` de bout en bout via un rendu réel du composant, ou si seule
  l'invocation directe de la méthode gestionnaire (avec un `LocationChangingContext` construit à
  la main) est réalisable. Documenter le résultat avant d'écrire les tests de 43.1 — ne pas
  supposer la couverture possible sans l'avoir vérifié.
- [ ] Repérer un pattern de confirmation déjà existant dans le projet à réutiliser pour la
  modale (ex. le bandeau de confirmation de suppression de `ImportProfiles.razor`/
  `ExportProfiles.razor`, qui remplace des boutons par un message + 2 actions via `@if`) plutôt
  que d'inventer un nouveau design de modale.

---

## 43.1. État "modifications non enregistrées" et confirmation de navigation — `ImportProfileEditor.razor`

**Comportement attendu** :
- Un champ privé `_hasUnsavedChanges` (bool, initialisé à `false`), mis à `true` :
  - via `EditContext.OnFieldChanged` sur les champs racine du formulaire ;
  - dans chaque méthode existante de mutation des listes de règles/sous-règles (ajout,
    modification, suppression) — pas de nouvel événement, réutilisation des points d'entrée déjà
    en place (confirmés en 43.0).
- Remis à `false` immédiatement après un `SaveAsync` réussi (pas en cas d'échec — si la
  sauvegarde échoue, les modifications restent non enregistrées, l'avertissement doit continuer
  à s'appliquer).
- Un `<NavigationLock>` ajouté au composant :
  - `ConfirmExternalNavigation="_hasUnsavedChanges"` → délègue au navigateur la boîte de dialogue
    native pour la fermeture d'onglet/rafraîchissement/navigation hors SPA.
  - `OnBeforeInternalNavigation="OnBeforeInternalNavigationAsync"` → si `_hasUnsavedChanges` est
    vrai, appelle `context.PreventNavigation()`, mémorise l'URI cible visée
    (`context.TargetLocation`) dans un champ privé, et affiche une confirmation inline (réutilisant
    le pattern repéré en 43.0) avec deux actions :
    - `#discard-changes-and-leave-button` : navigue explicitement vers l'URI mémorisée
      (`NavigationManager.NavigateTo(...)`), sans repasser par le verrou.
    - `#stay-on-page-button` : referme simplement la confirmation, aucune navigation.
  - Si `_hasUnsavedChanges` est faux, la navigation interne n'est pas interceptée (comportement
    actuel inchangé).

**Tests** (bUnit, `ImportProfileEditorTests.cs`) :
- [ ] `_hasUnsavedChanges` passe à `true` après modification d'un champ racine (via
  `EditContext`/simulation de saisie), initialement `false` au chargement d'un profil existant
  comme d'un nouveau profil.
- [ ] `_hasUnsavedChanges` passe à `true` après ajout/modification/suppression d'une règle de
  feuille ou d'une sous-règle imbriquée.
- [ ] `_hasUnsavedChanges` repasse à `false` après un `SaveAsync` réussi (mock
  `IImportProfileStore.SaveAsync` retournant succès) ; **reste `true`** si le mock lève une
  exception (chemin d'échec existant déjà testé ailleurs, à réutiliser).
- [ ] Le paramètre `ConfirmExternalNavigation` du `<NavigationLock>` rendu reflète exactement la
  valeur courante de `_hasUnsavedChanges` (vérification d'attribut de composant, pas de
  simulation de fermeture de navigateur — non testable en bUnit).
- [ ] Selon la conclusion de 43.0 : soit un test de bout en bout déclenchant une navigation
  interne bloquée puis confirmée/annulée via les deux boutons, soit (si bUnit ne permet pas la
  simulation complète du verrou) un test unitaire direct de la méthode gestionnaire avec un
  `LocationChangingContext` construit manuellement, vérifiant l'appel à `PreventNavigation()`
  quand `_hasUnsavedChanges` est vrai et son absence quand il est faux.
- [ ] Clic sur `#discard-changes-and-leave-button` déclenche la navigation vers l'URI cible
  mémorisée ; clic sur `#stay-on-page-button` ne navigue pas et referme la confirmation.
- [ ] Non-régression : tous les tests existants de `ImportProfileEditorTests.cs` non liés à ce
  comportement restent verts sans modification.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor`
(+ miroir `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileEditorTests.cs`).

---

## 43.2. Parité — `ExportProfileEditor.razor`

**Comportement attendu** : miroir exact de 43.1, appliqué à `ExportProfileEditor.razor` et à ses
sous-formulaires (`SheetGenerationRuleForm.razor`, `ColumnDefinitionForm.razor`,
`PointColumnDefinitionForm.razor`, `ApplicationColumnDefinitionForm.razor`). Mêmes IDs
(`#discard-changes-and-leave-button`, `#stay-on-page-button`) pour rester cohérent avec la
convention de parité déjà appliquée entre les deux éditeurs ailleurs dans le projet (Lot 037).

**Tests** (bUnit, `ExportProfileEditorTests.cs`) : mêmes cas que 43.1, transposés à
`IExportProfileStore.SaveAsync` et aux listes `SheetGenerationRule`/`ColumnDefinition`/
`PointColumnDefinition`/`ApplicationColumnDefinition`.

**Garde-fou de parité** : si `ProfileEditorParityTests.cs` existe déjà (voir Lot 037.2), y ajouter
un test comparant la présence et la structure du `<NavigationLock>`/de la confirmation entre les
deux éditeurs, sur le même modèle que les tests de parité déjà en place.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileEditor.razor`
(+ miroir `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfileEditorTests.cs`).

---

## 43.3. Complément léger — indicateur visuel "modifications non enregistrées" (optionnel mais recommandé)

**Comportement attendu** : quand `_hasUnsavedChanges` est vrai, afficher un texte/badge discret à
proximité immédiate de `#save-profile-button`/`#save-export-profile-button` (ex. "Modifications
non enregistrées"), pour que l'état soit visible même sans tentative de navigation. Élément
purement textuel, pas d'icône nouvelle à ajouter au catalogue (réutiliser un style d'alerte déjà
existant si possible — ne pas créer de nouvelle classe CSS dédiée si une classe Bootstrap
générique suffit).

**Tests** :
- [ ] Le badge est présent dans le DOM quand `_hasUnsavedChanges` est vrai, absent sinon (par ID
  dédié, ex. `#unsaved-changes-indicator`), sur les deux éditeurs.

**Dossier** : mêmes fichiers que 43.1/43.2.

---

## Hors périmètre explicite de ce lot

- Sauvegarde automatique (auto-save) de tout ou partie du profil en cours d'édition.
- Suivi précis champ par champ de ce qui a changé (un seul indicateur global suffit).
- Comparaison d'état structurée (diff avant/après) pour déterminer la présence de changements
  réels — un simple flag positionné sur mutation est suffisant et volontairement plus simple.
- Nouveau composant Blazor partagé factorisant `ImportProfileEditor.razor`/
  `ExportProfileEditor.razor` au-delà de ce qui existe déjà — duplication acceptée, cohérente
  avec l'état actuel du projet.
- Extension du mécanisme à `Login.razor`/`Register.razor`/`Profile.razor`,
  `ImportProfileTest.razor`/`ExportProfileTest.razor`, ou toute autre page — non demandé,
  périmètre strictement limité aux deux éditeurs de profil.
- Personnalisation du texte/de l'apparence de la boîte de dialogue native du navigateur
  (`ConfirmExternalNavigation`) — non personnalisable par Blazor, comportement du navigateur
  accepté tel quel.

---

## Note d'efficacité d'implémentation (Claude Code)

- **43.0 doit impérativement trancher la faisabilité de test bUnit de `NavigationLock` avant
  d'écrire les tests de 43.1** — évite de découvrir en cours de route qu'une partie du
  comportement n'est testable qu'indirectement, ce qui changerait la forme des tests à écrire.
- **43.1 sert de patron pour 43.2** — implémenter et faire valider 43.1 en premier, puis dupliquer
  strictement la même logique (mêmes noms de méthodes/IDs) plutôt que de développer les deux en
  parallèle, pour éviter toute micro-divergence entre les deux éditeurs.
- **43.3 est trivial et indépendant** — peut être livré dans le même commit que 43.1/43.2 plutôt
  qu'un cycle de revue séparé.
- Ne pas céder à la tentation d'un mécanisme plus général (ex. un service `IDirtyStateTracker`
  réutilisable) tant qu'un seul couple de pages en a besoin — cohérent avec le principe YAGNI
  rappelé en tête de ce document.

## Ordre recommandé

1. **43.0** (investigation — confirme `EditContext`, les points de mutation existants, et la
   faisabilité de test de `NavigationLock`)
2. **43.1** (implémentation complète côté Import — patron de référence)
3. **43.2** (miroir côté Export, dépend directement de 43.1)
4. **43.3** (indicateur visuel, trivial, peut être groupé avec 43.1/43.2)
