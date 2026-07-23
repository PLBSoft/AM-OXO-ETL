# Ticket TDD — Lot M : fusion du nom d'utilisateur et du lien "Mon Profil" dans le NavMenu

✅ Implémenté — voir commit `3388b63`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suite du Lot L
(`tickets-tdd-blazor-navmenu-visibilite-authentification.md`, `NavMenu.razor` déjà en place avec
un second `<AuthorizeView>` générique — tout utilisateur authentifié — couvrant nom d'utilisateur/
Profil/Logout/Register/Login, non modifié par le Lot L).*

---

## Constat (capture d'écran fournie par le porteur du projet, 2026-07-21)

Dans la sidebar de `ExcelETL.BlazorAdmin`, deux items distincts et adjacents pour la même notion :

1. Un item affichant seulement le nom de l'utilisateur connecté (`SLB` dans la capture) — texte
   statique, pas de lien, ne mène nulle part.
2. Juste en dessous, un lien `Mon Profil` (`#nav-profile-link` probable, route `/profile`).

**Décision produit** : fusionner les deux en un unique lien cliquable, libellé
`"{NomUtilisateur} - Mon Profil"`, vers la route `/profile`. L'item de texte statique disparaît.

---

## M1. Fusion du nom d'utilisateur et du lien Profil

### Fichier concerné

[`NavMenu.razor`](../src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor) — second
`<AuthorizeView>` générique (tout utilisateur authentifié, bloc situé après le bloc admin du Lot
L). **Avant de coder** : relire ce bloc tel qu'il existe réellement sur `main` (ne pas supposer sa
structure exacte à partir de ce ticket) et confirmer :
- l'`id` HTML actuel du lien Profil existant (probablement `nav-profile-link`, à vérifier, pas à
  deviner) ;
- la clé de ressource actuelle du libellé "Mon Profil" (probablement `NavMenu_Profile`, à
  vérifier dans `BlazorAdminMessages.resx`/`.fr.resx`) ;
- comment le nom d'utilisateur est actuellement récupéré (`context.User.Identity?.Name` le plus
  probable vu le `AuthorizeView`, mais à confirmer — pas d'autre source à inventer).

### Comportement attendu après modification

- Un seul élément de navigation (une seule `<div class="nav-item px-3">`, un seul `NavLink`) pour
  cette ligne, avec :
  - le même `id` HTML stable que le lien Profil existant aujourd'hui (**ne pas en créer un
    nouveau** si un id conforme existe déjà — réutiliser, pas dupliquer) ;
  - `href="profile"` inchangé ;
  - contenu textuel = nom d'utilisateur courant, suivi du libellé existant "Mon Profil"
    (`@context.User.Identity?.Name - @Loc["NavMenu_Profile"]` ou équivalent selon ce qui est
    réellement en place) ;
  - même icône Bootstrap Icons que le lien Profil actuel (ne pas changer l'icône).
- L'élément de texte statique affichant seul le nom d'utilisateur est **supprimé** (plus aucune
  occurrence de ce `<span>`/`<div>` autonome dans le DOM).
- Aucun changement aux autres liens du même bloc (`Logout`, `Register`, `Login` du bloc
  `NotAuthorized` du Lot L) — strictement hors périmètre.
- Aucune nouvelle clé `.resx` — réutilisation de `NavMenu_Profile` (EN/FR déjà traduites).
- Aucune règle CSS à ajouter dans `NavMenu.razor.css` (pas de nouvel élément, pas de nouvelle
  icône) — si un style ciblait spécifiquement l'ancien `<span>` du nom d'utilisateur (peu
  probable), le retirer pour éviter une règle orpheline (cf. convention "pas d'icône/règle CSS
  orpheline" documentée dans `CLAUDE.md`).

### Cycle TDD (Red → Green → Refactor)

1. **Rouge** — adapter/écrire dans
   [`NavMenuTests.cs`](../tests/ExcelETL.BlazorAdmin.Tests/Layout/NavMenuTests.cs) (convention
   bUnit déjà en place : `this.AddAuthorization().SetAuthorized("nom-utilisateur-de-test")`) :
   - `NavMenu_WhenAuthorized_ShowsSingleLinkWithUsernameAndProfileLabel` : utilisateur authentifié
     avec un nom connu (ex. `"jdupont"`) → le lien Profil (même `id` que l'actuel) contient à la
     fois `"jdupont"` et le libellé localisé "Mon Profil" (`Loc["NavMenu_Profile"]` résolu dans le
     test, pas de chaîne en dur, cohérent avec les tests existants du fichier) ; `href="profile"`.
   - `NavMenu_WhenAuthorized_DoesNotRenderStandaloneUsernameElement` : le même rendu ne contient
     **aucun** élément de texte autonome portant uniquement le nom d'utilisateur (recherche par
     sélecteur, pas par texte — si l'ancien élément avait un `id`/une classe dédiée, l'assertion se
     fait dessus ; sinon, s'assurer par lecture du DOM avant/après qu'un seul nœud correspond
     désormais au couple nom+lien).
   - Adapter le(s) test(s) existant(s) qui vérifiai(en)t séparément le nom d'utilisateur affiché et
     le lien Profil (probablement dans les tests de culture EN/FR mentionnés dans le Lot L —
     "les 5 tests pré-existants (culture EN/FR, profil, logout) passent sans modification de leur
     intention" : ici l'intention change légèrement, donc ces tests sont à mettre à jour, pas à
     laisser rouges).
   - Exécuter ces tests seuls d'abord et confirmer qu'ils échouent pour la bonne raison (élément
     fusionné pas encore en place), avant toute modification du composant.
2. **Vert** — modifier `NavMenu.razor` selon la section précédente, le minimum nécessaire pour
   faire passer les tests, sans toucher au bloc admin du Lot L ni au bloc `NotAuthorized`.
3. **Refactor** — si le nom d'utilisateur ou le libellé Profil sont dupliqués ailleurs dans le
   fichier, factoriser proprement ; sinon, ne rien changer de plus.

### Critères d'acceptation

- [ ] Un seul élément de navigation visible pour "nom d'utilisateur + Mon Profil", cliquable,
      menant à `/profile`.
- [ ] Aucun élément de texte résiduel affichant le nom d'utilisateur seul, non cliquable.
- [ ] Même `id` HTML qu'avant (pas de rupture pour d'éventuels tests/sélecteurs externes qui s'y
      référeraient déjà).
- [ ] Aucune nouvelle entrée `.resx`, aucune nouvelle règle `NavMenu.razor.css`.
- [ ] Suite complète `dotnet test tests/ExcelETL.BlazorAdmin.Tests` au vert, sans régression sur
      le reste (Journaux, Utilisateurs, Profils d'import/export, Login/Logout du Lot L).

### Hors périmètre explicite

- Bloc admin (`AuthorizeView Roles="Admin"`, Lot L) — inchangé.
- Bloc `NotAuthorized`/lien `Account/Login` (Lot L) — inchangé.
- Liens `Logout`/`Register` du même bloc générique — inchangés, ni dans leur libellé ni dans leur
  position relative au nouveau lien fusionné.
- Toute évolution du mécanisme d'authentification lui-même (Identity, rôles) — hors périmètre,
  ce ticket est purement présentation/NavMenu.
