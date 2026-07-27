# Tickets TDD — Lot 042 : liaison des messages de validation, hiérarchie des titres, parité structurelle Import/Export

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Neuvième lot
utilisant la convention numérique à trois chiffres, après le lot 041
(`tickets-tdd-lot-041-convention-icones-coherence.md`).*

**Contexte** : dernier lot de correctifs issu de `audit-design-blazoradmin-2026-07-27.md` et de sa
synthèse priorisée (`audit-priorisation-design-blazoradmin-2026-07-27.md`). Couvre les trois
constats restants **retenus** par Simon (28/07) :
1. `aria-describedby` absent entre les champs et leurs messages de validation
   (Login/Register/Profile).
2. Sauts de niveaux de titre (`h1`→`h3`/`h4`/`h5`) sur plusieurs pages.
3. Divergence structurelle `container-fluid px-3` entre `ExportProfileEditor.razor` (présent) et
   `ImportProfileEditor.razor` (absent).

**Explicitement exclu de ce lot, à la demande de Simon (28/07)** : le contraste de couleur. Ce
point reste non traité — pas par oubli, mais parce qu'il n'est pas calculable statiquement
(variables `--m3-*` recomposées via `color-mix()` au runtime, thème clair/sombre) et nécessite une
vérification en navigateur réel, jugée hors périmètre pour l'instant. Ne pas le rouvrir dans ce lot.

**Ce que ce lot n'est pas** : ni une refonte des formulaires Identity (Login/Register/Profile
restent scaffoldés tels quels, seul l'attribut de liaison est ajouté), ni un changement de
contenu/texte des titres (seul leur niveau sémantique `hN` est corrigé), ni une harmonisation
visuelle plus large des deux éditeurs de profil (seule la divergence `container-fluid`
spécifiquement relevée est corrigée).

---

## 42.0. Investigation préalable (obligatoire avant tout code)

- [ ] Relire `Login.razor`, `Register.razor`, `Profile.razor` : confirmer la structure exacte de
  chaque `<ValidationMessage>`/message d'erreur de champ et de son `<InputText>`/`<input>`
  associé, et l'`id` existant (ou son absence) sur chacun — un `aria-describedby` a besoin d'un
  `id` cible stable des deux côtés.
- [ ] Relister exhaustivement, page par page, tous les sauts de niveaux de titre relevés par
  l'audit (`NotFound.razor` sans `h1`, éditeurs `h1`→`h3`, pages de test `h1`→`h4`/`h5`) et
  confirmer qu'aucun autre saut non signalé n'existe ailleurs dans BlazorAdmin.
- [ ] Confirmer sur `ImportProfileEditor.razor`/`ExportProfileEditor.razor` la classe CSS exacte
  en cause (`container-fluid px-3`) et vérifier s'il existe une raison fonctionnelle à cette
  différence (ex. une contrainte de largeur spécifique à l'export) avant de supposer qu'il s'agit
  d'un oubli pur — si une raison existe, la signaler explicitement plutôt que d'harmoniser
  aveuglément.
- [ ] Confirmer que les tests bUnit existants sur les fichiers concernés passent avant toute
  modification (baseline verte).

---

## 42.1. `aria-describedby` — liaison champ/message de validation

**Comportement attendu** :
- Chaque champ de formulaire de `Login.razor`, `Register.razor`, `Profile.razor` porteur d'un
  message de validation reçoit un `aria-describedby` pointant vers l'`id` du message associé (l'
  `id` du message est ajouté s'il n'existe pas déjà).
- Si un champ n'a pas encore de message affiché (pas d'erreur), `aria-describedby` peut rester
  présent en pointant vers un conteneur vide plutôt que d'être ajouté/retiré dynamiquement — plus
  simple et suffisamment robuste, éviter un mécanisme de va-et-vient inutile.
- Aucun changement de la logique de validation Identity sous-jacente (scaffolding standard) — seul
  l'attribut de liaison est ajouté.

**Tests bUnit** :
- Pour chaque champ concerné : un test vérifie que la valeur d'`aria-describedby` du champ
  correspond exactement à l'`id` du message de validation rendu dans le même composant.
- Non-régression : le comportement de validation existant (apparition/disparition du message)
  reste inchangé.

**Dossier** : `Login.razor`, `Register.razor`, `Profile.razor` (ou leurs partials Identity
associés selon la structure confirmée en 42.0).

---

## 42.2. Correction des niveaux de titre

**Comportement attendu** :
- Chaque page corrigée respecte une hiérarchie continue sans saut (`h1` unique par page, puis
  `h2`, `h3`... sans sauter de niveau), conformément à la liste confirmée en 42.0.
- `NotFound.razor` reçoit un `h1` (actuellement absent, la page commence à `h3`).
- Les éditeurs de profil et pages de test sont corrigés pour ne plus sauter de niveau entre le
  titre de page et les titres de section/carte.
- Aucun changement de la taille visuelle affichée des titres si elle est pilotée par une classe
  CSS indépendante du niveau sémantique (ex. `<h3 class="h5">` reste visuellement identique, seul
  le niveau réel change) — pas de redesign visuel non demandé.

**Tests bUnit** :
- Pour chaque page corrigée : un test vérifie la présence et l'unicité du `h1`, et l'absence de
  saut de niveau dans les titres suivants rendus par le composant.

**Dossier** : fichiers listés exhaustivement en 42.0 (a minima `NotFound.razor`,
`ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `ImportProfileTest.razor`,
`ExportProfileTest.razor`).

---

## 42.3. Parité structurelle `container-fluid` Import/Export

**Comportement attendu** (conditionné au résultat de l'investigation 42.0) :
- Si aucune raison fonctionnelle n'est trouvée : `ImportProfileEditor.razor` reçoit le même
  conteneur `container-fluid px-3` qu'`ExportProfileEditor.razor`, pour supprimer la divergence de
  largeur de page entre les deux écrans jumeaux.
- Si une raison fonctionnelle existe : ne rien changer, documenter la raison trouvée dans ce
  ticket, et signaler explicitement à Simon que la divergence est intentionnelle plutôt que de la
  faire disparaître silencieusement.

**Tests bUnit** :
- Si le correctif est appliqué : test confirmant la présence de la classe `container-fluid px-3`
  sur le conteneur racine des deux éditeurs.
- Non-régression : le contenu et le comportement des deux éditeurs restent inchangés, seul le
  conteneur englobant est concerné.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`.

---

## Hors périmètre explicite de ce lot

- **Contraste de couleur** — explicitement écarté par Simon (28/07), voir en tête de document. Ne
  pas le traiter, même partiellement, dans ce lot.
- Toute harmonisation visuelle plus large entre les deux éditeurs de profil au-delà du seul
  `container-fluid` relevé par l'audit.
- Redesign visuel des titres (taille, graisse, couleur) — seul le niveau sémantique `hN` est
  corrigé.
- Les constats déjà traités dans les Lots 039, 040, 041.

---

## Note d'efficacité d'implémentation (Claude Code)

- **42.0 doit trancher 42.3 avant que le code ne soit écrit** : si une raison fonctionnelle
  existe pour la divergence `container-fluid`, 42.3 devient un sous-ticket de documentation/
  signalement, pas un correctif de code — ne pas harmoniser par réflexe sans avoir vérifié.
- **42.1 et 42.2 sont indépendants** — aucune dépendance technique entre eux, livrables dans
  n'importe quel ordre ou en parallèle.
- **42.2 est le plus long à vérifier exhaustivement mais mécaniquement simple** — le temps
  principal est dans le recensement (42.0), le correctif lui-même est un changement de balise par
  page.
- Ne pas réintroduire de discussion sur le contraste de couleur dans ce lot, même en passant —
  décision explicitement actée à exclure.

## Ordre recommandé

1. **42.0** (investigation — recensement exhaustif + vérification de la raison éventuelle du
   `container-fluid`)
2. **42.3** (parité structurelle ou documentation de la raison — dépend directement de 42.0)
3. **42.1** (liaison validation — indépendant)
4. **42.2** (niveaux de titre — indépendant, le plus volumineux)
