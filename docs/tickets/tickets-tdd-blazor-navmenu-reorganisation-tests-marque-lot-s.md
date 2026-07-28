# Tickets TDD — Lot S : réorganisation de la NavMenu, déplacement des liens de test de profil, renommage de la marque

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite à
une demande client du 23/07 après revue visuelle de l'admin Blazor (capture d'écran de
`/export-profiles`). Dépend du Lot L (masquage NavMenu par authentification) et du Lot M (fusion
lien profil) déjà livrés — ce lot **réorganise** la structure de `NavMenu.razor` qu'ils ont
laissée, il ne la réouvre pas.*

**Rappel de l'état de départ** (confirmé par lecture de code, voir
`etat-avancement-global-2026-07-22.md` §3 et `etat-avancement-lot-l-navmenu-visibilite-authentification-2026-07-21.md`) :

- `NavMenu.razor` contient aujourd'hui, dans cet ordre :
  1. `#nav-logs-link` (Journaux) — dans un `<AuthorizeView>` générique (tout utilisateur
     authentifié, sans rôle).
  2. Bloc `<AuthorizeView Roles="@IdentitySeeder.AdminRoleName">` contenant, dans cet ordre :
     `#nav-users-link`, `#nav-import-profiles-link`, `#nav-import-profiles-test-link`,
     `#nav-export-profiles-link`, `#nav-export-profiles-test-link`.
  3. Second `<AuthorizeView>` générique contenant `#nav-profile-link` (fusionné nom
     d'utilisateur/Profil, Lot M) et le lien de déconnexion, avec branche `<NotAuthorized>`
     portant `#nav-login-link`.
- Le lien de marque en haut de la sidebar affiche en dur le texte `ExcelETL.BlazorAdmin`.

---

## S1. Retrait des liens de test de la NavMenu, ajout dans les pages de liste de profils

**Constat client** : les liens `#nav-import-profiles-test-link` ("Tester un profil") et
`#nav-export-profiles-test-link` ("Tester un profil d'export") n'ont pas leur place dans la
sidebar globale — ce sont des actions qui se rattachent conceptuellement à l'écran de liste des
profils concernés (`/import-profiles`, `/export-profiles`), pas des sections de navigation de
premier niveau.

**Comportement attendu** :
- **Retirer entièrement** `#nav-import-profiles-test-link` et `#nav-export-profiles-test-link` de
  `NavMenu.razor` (les routes `/import-profiles/test` et `/export-profiles/test` restent
  inchangées et fonctionnelles — seul le point d'entrée depuis la sidebar disparaît).
- **`ImportProfiles.razor`** : ajouter un bouton `#test-import-profile-button`, positionné à côté
  de `#create-profile-button` (même ligne, alignement à droite — voir
  `convention-ui-blazor-alignement-boutons.md`), qui navigue vers `/import-profiles/test` via
  `NavigationManager.NavigateTo`. Réutiliser la clé de ressource existante déjà utilisée pour ce
  lien dans `NavMenu.razor` (probablement `NavMenu_ImportProfilesTest` ou équivalent — vérifier le
  `.resx` avant d'en créer une nouvelle, pour ne pas dupliquer une traduction EN/FR déjà validée).
- **`ExportProfiles.razor`** : même traitement, bouton `#test-export-profile-button` à côté de
  `#create-export-profile-button`, navigation vers `/export-profiles/test`, réutilisation de la
  clé de ressource existante du lien `NavMenu` correspondant.
- Ces boutons ne sont **pas** liés à une ligne de profil spécifique (`ImportProfileTest.razor`/
  `ExportProfileTest.razor` ne prennent pas d'identifiant de profil en paramètre de route
  aujourd'hui — confirmé par lecture de `tickets-tdd-blazor-profil-import.md`/
  `tickets-tdd-blazor-profil-export.md`) : un seul bouton par page, pas un bouton par ligne de
  tableau.

**Tests** (bUnit) :
- `NavMenuTests` : les deux `id` retirés (`#nav-import-profiles-test-link`,
  `#nav-export-profiles-test-link`) sont absents du DOM, y compris pour un utilisateur authentifié
  avec le rôle Admin (pas seulement masqués — retrait de markup, pas de `hidden`/`disabled`). Les
  5 tests existants de `NavMenuTests.cs` qui touchaient ces deux liens sont mis à jour en
  conséquence (pas de nouveau test qui les recherche encore).
- `ImportProfilesTests` : clic sur `#test-import-profile-button` navigue vers
  `/import-profiles/test` (même style d'assertion que les tests de navigation déjà en place pour
  `#create-profile-button`/`#edit-profile-button-{id}`).
- `ExportProfilesTests` : même test, symétrique, pour `#test-export-profile-button` →
  `/export-profiles/test`.

**Dossier** : `NavMenu.razor`, `ImportProfiles.razor`, `ExportProfiles.razor` (+ miroir tests dans
`tests/ExcelETL.BlazorAdmin.Tests/Layout/` et `Pages/Admin/`).

---

## S2. Réordonnancement de la sidebar admin

**Ordre cible demandé par le client** :
1. Profils d'import
2. Profils d'export
3. Utilisateurs
4. Journaux
5. Mon Profil
6. Déconnexion

**Point d'attention architectural** : `#nav-logs-link` (Journaux) vit dans un `<AuthorizeView>`
générique séparé (visible à tout utilisateur authentifié, pas seulement Admin), distinct du bloc
`<AuthorizeView Roles="Admin">` qui contient Utilisateurs/Profils d'import/Profils d'export. **Ne
pas fusionner ces deux `AuthorizeView` en un seul** — ce serait un changement de sémantique
d'autorisation (un utilisateur authentifié non-Admin doit continuer à voir Journaux mais pas les
3 autres), hors périmètre de ce ticket et non demandé par le client. La bonne implémentation
consiste à **déplacer le bloc `<AuthorizeView>` générique de Journaux après** le bloc
`<AuthorizeView Roles="Admin">` (au lieu d'avant, comme aujourd'hui), et à réordonner les 3 liens
restants à l'intérieur du bloc Admin (après retrait des 2 liens de test par S1) dans l'ordre
Import → Export → Utilisateurs.

**Comportement attendu** :
- Dans `NavMenu.razor`, ordre final des blocs/liens :
  1. `<AuthorizeView Roles="Admin">` : `#nav-import-profiles-link`,
     `#nav-export-profiles-link`, `#nav-users-link` (dans cet ordre).
  2. `<AuthorizeView>` générique : `#nav-logs-link`.
  3. `<AuthorizeView>` générique (déjà existant, inchangé) : `#nav-profile-link` puis lien de
     déconnexion, branche `<NotAuthorized>` avec `#nav-login-link`.
- Aucun changement de rôle/condition d'autorisation sur aucun des liens — seul l'ordre du markup
  change.

**Tests** (bUnit) :
- Nouveau test `NavMenu_RendersAdminAndLogsLinks_InExpectedOrder` : recherche les liens par leurs
  `id` stables existants et vérifie leur ordre relatif dans le DOM (ex. comparaison des index
  retournés par `FindAll` sur un sélecteur commun, ou vérification que l'index de
  `#nav-import-profiles-link` < `#nav-export-profiles-link` < `#nav-users-link` <
  `#nav-logs-link` < `#nav-profile-link` < lien de déconnexion) — pas de sélection par texte,
  conformément à la convention de test déjà en place.
- Les tests existants de visibilité par rôle (`NavMenu_WhenNotAuthorized_HidesAdminLinks...`,
  `NavMenu_WhenAuthorizedAsAdmin_ShowsAdminLinks...`) continuent de passer sans modification de
  leur intention (ils vérifient la présence/absence, pas l'ordre).

**Dossier** : `NavMenu.razor` (+ `NavMenuTests.cs`).

---

## S3. Renommage de la marque dans la sidebar

**Comportement attendu** : remplacer le texte actuellement codé en dur `ExcelETL.BlazorAdmin`
(lien `navbar-brand` en haut de `NavMenu.razor`) par `Alpha - MAD / REL OXO`. Simple changement de
littéral de texte, aucune nouvelle clé de ressource requise sauf si le texte est déjà porté par
une clé `.resx` existante — dans ce cas, mettre à jour la valeur EN/FR de cette clé plutôt que de
casser en dur le lien vers la ressource.

**Tests** (bUnit) :
- Test de non-régression : le markup rendu de `NavMenu.razor` contient `Alpha - MAD / REL OXO` et
  ne contient plus `ExcelETL.BlazorAdmin`.

**Dossier** : `NavMenu.razor` (+ `.resx` si applicable).

---

## Hors périmètre explicite de ce lot

- Tout changement de comportement des pages `ImportProfileTest.razor`/`ExportProfileTest.razor`
  elles-mêmes (contenu, logique) — seul leur point d'entrée dans la navigation change.
- Tout changement des règles d'autorisation par rôle (`AuthorizeView Roles="Admin"` vs générique)
  — uniquement l'ordre du markup est concerné, pas la logique de visibilité déjà livrée par le
  Lot L.
- Renommage du projet `ExcelETL.BlazorAdmin` lui-même (namespace, nom de solution, titre
  d'onglet navigateur `<title>`) — la demande porte uniquement sur le texte affiché dans le lien
  `navbar-brand` de la sidebar.
- `NavMenu.razor.css` : aucune règle CSS nouvelle attendue (réutilisation des classes/icônes
  existantes pour S1/S2 ; S3 est un changement de texte pur).

---

## Note d'efficacité d'implémentation

- Traiter **S3 en premier** (changement isolé d'une ligne, aucune dépendance, valide rapidement le
  cycle de build/test).
- Traiter **S1 avant S2** : S2 réordonne notamment les 3 liens qui restent dans le bloc Admin
  *après* le retrait des 2 liens de test par S1 — faire S2 avant S1 obligerait à réordonner 5
  liens puis à en supprimer 2, pour un résultat identique en plus de travail.
- Un seul passage de lecture complète de `NavMenu.razor` suffit avant de commencer (fichier
  compact, ~111 lignes au dernier état documenté) — pas besoin de relire entre S1/S2/S3, les trois
  se font dans un seul passage d'édition du même fichier avant de lancer les tests.
- Vérifier les clés `.resx` existantes (`BlazorAdminMessages.resx`) pour les libellés des liens de
  test **avant** d'en créer de nouvelles pour S1 — éviter une duplication EN/FR déjà traduite.

---

## Ordre recommandé

1. **S3** (renommage marque)
2. **S1** (retrait des liens de test + ajout dans les pages de liste)
3. **S2** (réordonnancement, dépend de S1 pour le nombre final de liens dans le bloc Admin)
