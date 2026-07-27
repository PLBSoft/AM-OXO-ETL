# Tickets TDD — Lot 040 : annonce accessible des états asynchrones

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Septième lot
utilisant la convention numérique à trois chiffres, après le lot 039
(`tickets-tdd-lot-039-accessibilite-clavier-navmenu.md`).*

**Contexte** : fait suite à `audit-design-blazoradmin-2026-07-27.md` (§3.2, §3.3) et à sa synthèse
priorisée (`audit-priorisation-design-blazoradmin-2026-07-27.md`, recommandation prioritaire 1).
Trois constats distincts, mais un seul et même angle mort : un changement de contenu significatif
survient dans le DOM après une action de l'utilisateur (soumission de formulaire, traitement de
lot, demande de suppression), sans qu'aucun mécanisme programmatique (`role="alert"`,
`aria-live`) n'en informe les technologies d'assistance. Un utilisateur clavier/lecteur d'écran
peut ne jamais savoir que son action a échoué, a abouti, ou nécessite une confirmation.

**Ce que ce lot n'est pas** : ni un refactoring de duplication (les 25 occurrences du bloc
d'erreur restent des blocs indépendants dans leurs fichiers respectifs — une éventuelle
factorisation en composant partagé est une décision distincte, hors périmètre ici, cf. section
"Hors périmètre"), ni une réécriture du mécanisme de validation (`try/catch` + `_errorMessage`
reste tel quel — seul l'attribut d'accessibilité est ajouté autour du rendu existant).

**Trois constats traités** :
1. 25 occurrences de `<div class="alert alert-danger">@_errorMessage</div>` (éditeurs de profil et
   les 6 sous-formulaires imbriqués), sans `role="alert"` ni `aria-live`.
2. `<p id="batch-summary">@BuildBatchSummaryText()</p>` (`ImportProfileTest.razor`/
   `ExportProfileTest.razor`), inséré après un traitement asynchrone (jusqu'à 20 fichiers), sans
   `aria-live`.
3. Bandeau de confirmation de suppression (`ImportProfiles.razor`/`ExportProfiles.razor`) qui
   remplace Modifier/Dupliquer/Supprimer par un message + 2 boutons via `@if`, sans `aria-live`.

**Conventions déjà en place à respecter** : IDs HTML stables sur tout élément interactif ;
xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit ; sélection en test uniquement par ID/attribut,
jamais par texte/position ; `ValidationSummary` de `Login.razor`/`Register.razor`/`Profile.razor`
porte déjà `role="alert"` — ce lot aligne les autres pages sur ce patron déjà existant, il ne
l'invente pas.

---

## 40.0. Investigation préalable (obligatoire avant tout code)

- [ ] Recenser exhaustivement, par un grep sur `class="alert alert-danger"`, les 25 occurrences
  annoncées par l'audit dans `ImportProfileEditor.razor`, `ExportProfileEditor.razor`,
  `SheetRuleForm.razor`, `BlockFieldForm.razor`, `SheetGenerationRuleForm.razor`,
  `ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`,
  `ApplicationColumnDefinitionForm.razor` — confirmer le nombre exact (peut avoir varié depuis
  l'audit du 27/07) et lister les fichiers concernés.
- [ ] Lire la structure exacte de `<p id="batch-summary">` dans `ImportProfileTest.razor`/
  `ExportProfileTest.razor` : confirmer si le conteneur parent est un candidat naturel pour porter
  `aria-live` (éviter de le poser directement sur un élément qui pourrait être démonté/remonté en
  entier à chaque rendu, ce qui annulerait l'effet d'`aria-live` — le conteneur englobant stable
  est préférable à l'élément qui change de contenu lui-même).
- [ ] Lire la structure exacte du bandeau de confirmation de suppression dans `ImportProfiles.razor`/
  `ExportProfiles.razor` pour identifier le conteneur `@if` à cibler.
- [ ] Confirmer que les tests bUnit existants sur ces fichiers passent avant toute modification
  (baseline verte).

---

## 40.1. Messages d'erreur des éditeurs et sous-formulaires — `role="alert"`

**Comportement attendu** :
- Chacune des 25 occurrences de `<div class="alert alert-danger">@_errorMessage</div>` reçoit
  l'attribut `role="alert"`, à l'identique du patron déjà utilisé par `ValidationSummary` sur
  Login/Register/Profile.
- Aucun changement de la logique `try/catch`/`_errorMessage` sous-jacente — seul le markup de
  présentation change.
- Ne pas ajouter `aria-live` en plus de `role="alert"` sur ces blocs : `role="alert"` implique déjà
  `aria-live="assertive"` de façon native, un doublon serait redondant.

**Tests bUnit** :
- Pour chacun des fichiers concernés : un test simulant l'apparition de l'erreur (via le chemin
  existant qui déclenche déjà `_errorMessage`, réutiliser le test d'erreur existant si un tel test
  existe déjà plutôt que d'en écrire un nouveau) vérifie que l'élément rendu porte
  `role="alert"`.
- Non-régression : le texte du message d'erreur affiché reste inchangé, seul l'attribut est
  ajouté.

**Dossier** : les 8 fichiers listés en 40.0, sous
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/`.

---

## 40.2. `#batch-summary` — annonce du résultat de traitement de lot

**Comportement attendu** :
- Le conteneur parent stable de `#batch-summary` (confirmé en 40.0) reçoit `aria-live="polite"`
  (pas `assertive` : ce n'est pas une erreur bloquante, un résumé de fin de traitement peut
  attendre que l'utilisateur ait fini son action en cours au clavier/lecteur d'écran).
- Le spinner de traitement (`role="status"`, déjà présent) n'est pas modifié — seul le relais
  après sa disparition est ajouté.
- Comportement identique entre `ImportProfileTest.razor` et `ExportProfileTest.razor` (même
  patron, cohérent avec la duplication déjà assumée et documentée de ces deux pages — Lot 033.4).

**Tests bUnit** :
- Le conteneur ciblé porte `aria-live="polite"` dès le rendu initial (avant tout traitement), pas
  seulement après.
- Non-régression : le contenu textuel du résumé (`BuildBatchSummaryText()`) reste inchangé.

**Dossier** : `ImportProfileTest.razor`, `ExportProfileTest.razor`.

---

## 40.3. Bandeau de confirmation de suppression — annonce du changement de contexte

**Comportement attendu** :
- Le conteneur du bandeau de confirmation (remplaçant Modifier/Dupliquer/Supprimer) reçoit
  `role="alert"` — c'est une action destructive en attente de confirmation, elle justifie une
  annonce assertive plutôt que polie (cohérent avec le traitement retenu en 40.1, pas avec le
  `aria-live="polite"` retenu en 40.2 qui concerne un résumé non critique).
- Aucun changement du comportement `@if`/logique de confirmation existante.

**Tests bUnit** :
- Test simulant un clic sur Supprimer (chemin déjà couvert par les tests existants) et vérifiant
  que le bandeau affiché porte `role="alert"`.
- Non-régression : les tests déjà existants sur ce bandeau (texte, présence des 2 boutons)
  restent verts.

**Dossier** : `ImportProfiles.razor`, `ExportProfiles.razor`.

---

## Hors périmètre explicite de ce lot

- Factorisation des 25 blocs d'erreur dupliqués en un composant partagé (ex. un composant
  `ErrorAlert` réutilisable) — amélioration de duplication distincte, qui mériterait son propre
  lot si retenue, pas mêlée ici à un correctif d'accessibilité ciblé.
- Réutilisation de `Account/Shared/StatusMessage.razor` pour ces 25 blocs — même remarque : un
  changement d'architecture de composant est une décision à part entière, pas un sous-produit
  d'un correctif ARIA.
- Les autres constats de la synthèse d'audit (cohérence de la convention icônes, `aria-describedby`
  entre champ et message de validation sur Login/Register/Profile, sauts de niveaux de titre,
  `aria-label` manquant sur `log-copy-btn`) — traités dans des lots distincts.
- Vérification du rendu réel par un lecteur d'écran (NVDA/JAWS/VoiceOver) — hors portée d'un test
  bUnit, resterait une vérification manuelle si souhaitée séparément.

---

## Note d'efficacité d'implémentation (Claude Code)

- **40.0 doit confirmer le compte exact des 25 occurrences avant de commencer** — si le nombre a
  changé depuis l'audit du 27/07 (nouveau code entre-temps), traiter toutes les occurrences
  trouvées, pas seulement celles listées ici.
- **40.1, 40.2 et 40.3 sont indépendants entre eux** — peuvent être livrés dans un seul commit/PR
  ou trois commits séparés selon la préférence de revue, aucune dépendance technique entre eux.
- **40.1 est le plus volumineux mais le plus mécanique** : même changement (`role="alert"`) répété
  8 fois — pas de raison de le fragmenter en sous-tickets supplémentaires, un seul passage
  systématique suffit.
- Ne pas céder à la tentation de factoriser les 25 blocs en composant partagé "pendant qu'on y
  est" — décision explicitement hors périmètre (voir ci-dessus), à proposer comme lot séparé si
  jugé utile après coup.

## Ordre recommandé

1. **40.0** (investigation — confirmer le périmètre exact)
2. **40.1** (messages d'erreur — le plus volumineux, mécanique)
3. **40.2** (résumé de lot)
4. **40.3** (confirmation de suppression)
