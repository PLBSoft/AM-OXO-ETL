# Tickets TDD — Lot 28 : suppression de profils d'import et d'export avec confirmation

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite à la
demande de Simon du 24/07, initialement illustrée par une capture d'écran de `ExportProfiles.razor`
(`localhost:7013/export-profiles`) puis **explicitement étendue par Simon à `ImportProfiles.razor`
également** — ce document couvre donc les deux pages en parallèle, par symétrie confirmée (pas
supposée). Anciennement désigné Lot Z / "lot aa" avant révision de la convention de nommage : les
lots lettrés s'arrêtent à Z (dernier lot lettré existant, `tickets-tdd-blazor-polish-ux-lot-y.md`
étant le Lot Y), et tout lot au-delà utilise désormais un identifiant numérique à trois chiffres
(`convention-nommage-documents.md` §3). Numéroté **Lot 28** — le Lot 27 étant un lot distinct en
cours de création en parallèle.*

**Conventions déjà en place à respecter (tout le lot)** : `convention-ui-blazor-alignement-boutons.md`
(boutons d'action alignés à droite), `convention-ui-blazor-icones-boutons.md` (icône `bi-trash`
pour une action CRUD standard, `aria-label`/`title` obligatoires en bouton icône seule) ; IDs HTML
stables, jamais de sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions 7.x +
Moq + bUnit ; Bootstrap déjà en usage — **aucun JS interop nouveau** (cohérent avec le principe
directeur déjà acté en Partie B du Lot V : toute interaction reste gérée par l'état C# Blazor, pas
par `IJSRuntime`/`window.confirm`).

**Point de départ favorable** : `IImportProfileStore.DeleteAsync(Guid id, CancellationToken ct)` et
`IExportProfileStore.DeleteAsync(Guid id, CancellationToken ct)` existent déjà dans les deux
interfaces (voir `modele-domaine-import-profile.md` §4 pour l'import ; symétrique côté export,
Lot I) et sont très probablement déjà implémentés côté `Ef*ProfileStore` (même génération que le
reste du CRUD, Lots E/I) — **ce lot ne devrait ajouter aucune méthode de store**, seulement
l'exposer depuis les deux pages `Import`/`Export`. 28.0 confirme ce point avant tout code.

**Traitement en parallèle, pas en série** : les deux pages sont structurellement indépendantes
(composants distincts, stores distincts) — ce document décrit une seule fois le comportement
attendu et les tests, avec les deux couples `ImportProfile`/`ExportProfile` en regard, pour éviter
de dupliquer deux fois le même texte. Toute divergence réelle entre les deux pages (ex. mécanisme
de confirmation légèrement différent) doit être un choix explicite documenté, pas un oubli de
parité.

---

## 28.0. Investigation préalable (obligatoire avant tout code)

- [ ] Confirmer que `EfImportProfileStore.DeleteAsync` **et** `EfExportProfileStore.DeleteAsync`
  sont bien implémentés et déjà couverts par un test EF Core InMemory existant côté Infrastructure
  (probable, à vérifier plutôt que supposé) — si un test manque pour l'un ou l'autre, l'ajouter en
  28.1 plutôt que d'ouvrir un nouveau sous-ticket.
- [ ] Lire `ImportProfiles.razor` **et** `ExportProfiles.razor` : structure exacte de la ligne de
  tableau (où vivent `#edit-profile-button-{id}`/`#duplicate-profile-button-{id}` côté import,
  `#edit-export-profile-button-{id}`/`#duplicate-export-profile-button-{id}` côté export),
  conteneur des boutons d'action, pattern déjà en place pour l'alignement/l'icône
  (`bi-pencil`/`bi-copy`, voir Lot X7/V3) à reprendre à l'identique pour le nouveau bouton sur les
  deux pages.
- [ ] Confirmer les identifiants stables (`Guid` constants) des profils d'import **et** d'export
  seedés par défaut (`DefaultProfileSeeder`, `tickets-tdd-seed-profils-defaut.md` M1) —
  nécessaire pour documenter correctement le comportement décrit en 28.5 (reseeding), pas pour
  bloquer la suppression.
- [ ] Vérifier si les deux composants partagent déjà un pattern commun (ex. même structure de
  ligne de tableau) qui permettrait de factoriser le futur bloc de confirmation (composant Razor
  partagé) plutôt que de dupliquer le markup deux fois — sans pour autant forcer une
  factorisation si les deux pages divergent déjà sur d'autres points (cohérent avec le principe
  "architecture par pertinence, pas par mimétisme" déjà acté au projet).

---

## 28.1. `EfImportProfileStore.DeleteAsync` / `EfExportProfileStore.DeleteAsync` — combler un test manquant si nécessaire

**Comportement attendu** : si 28.0 révèle que les deux méthodes existent déjà et sont testées, ce
ticket devient un test de non-régression documentant l'état déjà conforme (pas une nouvelle
implémentation) — ne pas dupliquer une méthode ou un test déjà présents, pour l'un ou l'autre
store.

**Si un test manque (pour l'un des deux stores, ou les deux)** :
- Suppression d'un profil existant (contre EF Core InMemory, jamais mocké) → `GetByIdAsync`
  renvoie `null` ensuite, `GetAllAsync` ne le contient plus.
- Suppression d'un identifiant inexistant → pas d'exception non gérée (comportement idempotent,
  cohérent avec le reste du CRUD du projet).

**Dossier** :
- `tests/ExcelETL.Infrastructure.Tests/Persistence/EfImportProfileStoreTests.cs`
- `tests/ExcelETL.Infrastructure.Tests/Persistence/EfExportProfileStoreTests.cs`

---

## 28.2. `ImportProfiles.razor` / `ExportProfiles.razor` — bouton de suppression par ligne

**Comportement attendu (les deux pages, symétrique)** :
- Nouveau bouton par ligne — `#delete-profile-button-{id}` côté `ImportProfiles.razor`,
  `#delete-export-profile-button-{id}` côté `ExportProfiles.razor` (conventions de nommage
  existantes de chaque page respectées, pas de renommage des IDs déjà en place pour Modifier/
  Dupliquer) — placé **à côté** des boutons d'édition/duplication existants (même conteneur
  d'actions, alignement à droite déjà en place — `convention-ui-blazor-alignement-boutons.md`).
- Icône seule Bootstrap Icons `bi-trash`, conformément à `convention-ui-blazor-icones-boutons.md`
  (ligne de grille/tableau + action CRUD standard → icône). `aria-label`/`title` explicites
  obligatoires (ex. `aria-label="Supprimer le profil {Name}"`), même pattern que
  `bi-pencil`/`bi-copy` déjà en place sur les deux pages.
- Le clic **n'appelle pas directement** `DeleteAsync` : il ouvre la confirmation décrite en 28.3, en
  mémorisant l'identifiant (et le nom, pour l'affichage) du profil ciblé.

**Tests** (bUnit, dupliqués symétriquement dans `ImportProfilesTests`/`ExportProfilesTests`) :
- Le bouton de suppression est présent pour chaque ligne rendue, porte
  `<span class="bi bi-trash" aria-hidden="true">`, un `aria-label` non vide, sans texte visible.
- Clic sur ce bouton n'appelle **pas** `DeleteAsync` immédiatement (vérifiable via
  `Mock<IImportProfileStore>`/`Mock<IExportProfileStore>.Verify(..., Times.Never)`) — seule
  l'étape de confirmation (28.3) peut déclencher l'appel réel.
- Non-régression : les boutons `Modifier`/`Dupliquer` existants et leurs tests restent inchangés,
  sur les deux pages.

---

## 28.3. Confirmation avant suppression effective

**Décision retenue (par défaut, cohérente avec l'absence de JS interop dans le projet, appliquée
symétriquement aux deux pages)** : mécanisme géré entièrement en état C# Blazor, pas de
`window.confirm()` ni de nouvelle dépendance JS — un état `_profileIdPendingDeletion: Guid?` (+
nom mémorisé pour l'affichage) piloté par `@onclick`, avec un bloc de confirmation inline
(Bootstrap `alert`/`card` avec deux boutons `Confirmer`/`Annuler`) rendu conditionnellement dans
la page, sur le modèle de ce qui existe déjà pour d'autres messages conditionnels du projet
(`#import-profile-not-found`, etc.) plutôt qu'un composant `<Modal>` Bootstrap JS (qui
nécessiterait du JS interop pour l'ouverture/fermeture).

**Comportement attendu (les deux pages)** :
- Clic sur le bouton de suppression d'une ligne → affichage d'un bloc de confirmation
  identifiable par un `id` stable et unique par ligne (ex.
  `#delete-profile-confirm-{id}`/`#delete-export-profile-confirm-{id}`), contenant le nom du
  profil ciblé, un bouton de confirmation dédié (`btn-danger`, ex.
  `#confirm-delete-profile-button-{id}`/`#confirm-delete-export-profile-button-{id}`) et un
  bouton d'annulation (`btn-secondary`, sans icône — action secondaire, cf. matrice de
  `convention-ui-blazor-icones-boutons.md`, ex.
  `#cancel-delete-profile-button`/`#cancel-delete-export-profile-button`).
- Clic sur le bouton d'annulation → referme le bloc, aucun appel à `DeleteAsync`, aucune autre
  action de la page perturbée (édition/duplication toujours fonctionnelles ensuite).
- Clic sur le bouton de confirmation → appelle `DeleteAsync(id)`, recharge la liste
  (`GetAllAsync`), referme le bloc de confirmation.
- Un seul bloc de confirmation actif à la fois **par page** (ouvrir la confirmation d'une autre
  ligne referme la précédente sans suppression) — évite toute ambiguïté sur la ligne réellement
  ciblée. Les deux pages restent indépendantes l'une de l'autre (état de confirmation propre à
  chaque composant, pas partagé).

**Tests** (bUnit, dupliqués symétriquement dans `ImportProfilesTests`/`ExportProfilesTests`) :
- Clic sur le bouton de suppression d'une ligne affiche le bloc de confirmation correspondant,
  avec le nom du profil ciblé visible dans le texte rendu.
- Clic sur `Annuler` masque le bloc, `DeleteAsync` jamais appelé (`Mock.Verify(Times.Never)`).
- Clic sur `Confirmer` appelle `DeleteAsync` avec l'identifiant exact du profil ciblé
  (`Mock.Verify(..., Times.Once)` avec l'`id` attendu), puis la liste rechargée ne contient plus ce
  profil (assertions sur le tableau, IDs stables).
- Ouvrir la confirmation d'une ligne B pendant que celle d'une ligne A est déjà ouverte referme A
  sans appeler `DeleteAsync` pour A.

**Dossier** :
- `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor` (extension) + miroir
  `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfilesTests.cs`
- `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfiles.razor` (extension) + miroir
  `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfilesTests.cs`

---

## 28.4. Ressources de localisation (EN/FR)

**Comportement attendu** : nouvelles clés `.resx` pour le texte de confirmation (ex.
`ImportProfiles_ConfirmDeleteMessage`/`ExportProfiles_ConfirmDeleteMessage`, avec placeholder pour
le nom), le libellé `aria-label`/`title` du bouton suppression sur chaque page, et les libellés
`Confirmer`/`Annuler` s'ils ne réutilisent pas déjà des clés génériques existantes (vérifier avant
d'en créer de nouvelles, pour ne pas dupliquer une traduction déjà validée — même réflexe que
documenté en Lot S1). Si les deux pages peuvent partager les mêmes clés `Confirmer`/`Annuler`
génériques, ne pas créer deux clés redondantes par page pour ce seul texte.

**Tests** : aucun test dédié aux ressources (cohérent avec le reste du projet, pas de test resx
séparé ailleurs).

---

# Partie D — Décision actée avec Simon

## 28.5. Profils seedés par défaut ("Profil OXO standard") supprimés puis re-seedés au redémarrage — comportement accepté tel quel

**Constat** : `DefaultProfileSeeder` (Lot M) vérifie l'existence des profils d'import et d'export
par défaut via des **`Guid` constants stables**, et ne les recrée **que s'ils sont absents**. Si
un admin supprime l'un de ces deux profils précis via ces nouveaux boutons, **il sera
automatiquement re-créé au prochain redémarrage** de `BlazorAdmin` (comportement du seeder, pas un
bug de ce lot) — puisque l'identifiant constant ne sera alors plus trouvé en base. Ce constat
s'applique symétriquement aux deux profils (import et export).

**Décision actée avec Simon (24/07)** : ce comportement est **accepté tel quel**, simplement
documenté ici — aucune protection contre la suppression des profils seedés n'est ajoutée par ce
lot. Aucun code supplémentaire (pas de vérification de l'`Id` contre les `Guid` constants avant
suppression, pas de bouton désactivé). Ce point ne doit **pas être rouvert** dans un futur ticket
sans nouvelle demande explicite de Simon.

---

# Hors périmètre explicite

- Suppression en masse (sélection multiple, bouton "tout supprimer"), sur l'une ou l'autre page.
- Protection spécifique des profils seedés par défaut (voir 28.5 — comportement accepté tel quel,
  décision actée, à ne pas rouvrir sans nouvelle demande explicite).
- Toute modification du contenu ou de la structure d'un profil existant (couvert par les éditeurs,
  Lots F/J, non concerné par ce lot).
- Undo/corbeille — la suppression reste définitive une fois confirmée (pas de soft-delete, cohérent
  avec l'absence de mécanisme de ce type ailleurs dans le projet).

---

# Note d'efficacité d'implémentation

1. **28.0 en premier**, intégralement — conditionne si 28.1 est un vrai développement ou un simple
   test de non-régression, pour l'un ou l'autre store.
2. **28.1 et 28.2 peuvent être menés en parallèle**, y compris entre import et export : 28.1 (stores) ne
   dépend pas du markup Blazor, 28.2 (boutons) peut être développé avec des mocks sans attendre 28.1.
3. **28.3 dépend de 28.2** sur chaque page respectivement — ne pas commencer 28.3 pour une page avant
   que son 28.2 compile et passe ses propres tests. Les deux pages (import/export) restent
   indépendantes : 28.2/28.3 côté import peuvent être livrés avant, après, ou en parallèle de
   28.2/28.3 côté export, sans dépendance de fichier entre les deux.
4. **28.5 est désormais actée** (comportement accepté tel quel, aucun code dédié) — aucune incidence
   sur l'ordre d'implémentation de 28.0-28.4.

---

# Ordre recommandé

1. 28.0 (investigation, les deux pages)
2. 28.1 (stores, si nécessaire, les deux)
3. 28.2 (boutons de suppression — import et export, en parallèle ou dans l'ordre choisi)
4. 28.3 (confirmation — import et export)
5. 28.4 (ressources)

**Dossiers concernés** :
- `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor`
- `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfiles.razor`
- `src/ExcelETL.Infrastructure/Persistence/Repositories/EfImportProfileStore.cs` (si 28.1)
- `src/ExcelETL.Infrastructure/Persistence/Repositories/EfExportProfileStore.cs` (si 28.1)
- ressources `.resx` associées (+ miroirs tests correspondants, `ImportProfilesTests`/
  `ExportProfilesTests`)
