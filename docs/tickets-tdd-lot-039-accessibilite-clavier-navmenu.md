# Tickets TDD — Lot 039 : accessibilité clavier du toggler mobile NavMenu

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Sixième lot
utilisant la convention numérique à trois chiffres, après le lot 038
(`tickets-tdd-lot-038-blazor-page-test-api-m2m.md`).*

**Contexte** : fait suite à `audit-design-blazoradmin-2026-07-27.md` (§3.2) et à sa synthèse
priorisée (`audit-priorisation-design-blazoradmin-2026-07-27.md`, recommandation prioritaire 2).
C'est le seul point de blocage clavier réel identifié sur l'ensemble du périmètre BlazorAdmin
audité — tout le reste de l'audit constate une conformité correcte ou des écarts de confort/
annonce, pas un blocage total d'accès.

**Deux constats distincts à traiter, dans `NavMenu.razor`** :
1. Le toggler mobile est un `<input type="checkbox" class="navbar-toggler" title="...">` : porte
   un `title` mais aucun `aria-expanded`, `aria-controls`, ni `aria-label`. L'ouverture/fermeture
   du menu repose entièrement sur le sélecteur CSS `.navbar-toggler:checked ~ .nav-scrollable`
   (`NavMenu.razor.css:164`) — mécanisme purement visuel, sans relais sémantique pour les
   technologies d'assistance.
2. `<div class="nav-scrollable" onclick="document.querySelector('.navbar-toggler').click()">` :
   attribut HTML natif `onclick` (pas une directive Blazor `@onclick`), sans `role`/`tabindex`
   propres — mais ce `div` n'est pas lui-même un contrôle interactif : son rôle est de refermer le
   menu mobile quand l'utilisateur clique un lien à l'intérieur. **Ne pas supposer que c'est cassé
   au clavier sans vérifier** (voir 39.0) — un événement `click` DOM est également émis par le
   navigateur quand un `<a>` interne est activé via `Entrée` au clavier, ce qui pourrait déjà
   couvrir ce cas sans changement de code.

**Décision actée pour ce lot** : ne pas corriger le point 2 avant d'avoir vérifié en 39.0 s'il y a
un problème réel. Cohérent avec le principe du projet "pas de correction spéculative" — un constat
d'audit statique ne peut pas garantir un comportement clavier réel (l'audit source le signale
lui-même en section "Non couvert / incertain").

**Conventions déjà en place à respecter** : IDs HTML stables sur tout élément interactif
(convention de tests bUnit) ; xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit ; sélection en test
uniquement par ID, jamais par texte/position (leçon du Lot L1) ; pas de JS interop nouveau sauf
strictement nécessaire.

---

## 39.0. Investigation préalable (obligatoire avant tout code)

- [x] Lire l'état actuel exact de `NavMenu.razor` : structure précise du toggler (`id` existant ou
  absent, classes, attributs), structure de `.nav-scrollable`, et confirmer l'`id` du conteneur
  que le toggler doit référencer via `aria-controls` (l'ajouter s'il n'existe pas encore, sans
  renommer un `id` déjà utilisé ailleurs).
  - **Constat** : le toggler n'avait aucun `id` (uniquement `class="navbar-toggler"` + `title`).
    `.nav-scrollable` n'avait pas d'`id` non plus. Ajoutés : `id="nav-menu-toggler"` sur la
    checkbox, `id="nav-scrollable"` sur le conteneur (aucun `id` `nav-scrollable` déjà pris
    ailleurs dans le projet, confirmé par recherche).
- [x] Lire `NavMenu.razor.css` pour confirmer que le mécanisme `:checked ~ .nav-scrollable` est
  bien la seule logique d'ouverture/fermeture (pas de JS caché ailleurs) — le correctif de 39.1
  doit s'ajouter à ce mécanisme, pas le remplacer.
  - **Constat** : confirmé, `NavMenu.razor.css:164` (`.navbar-toggler:checked ~ .nav-scrollable`)
    est la seule règle d'ouverture/fermeture ; aucun JS/`.razor.js` associé à ce composant. 39.1
    ajoute `aria-expanded` en parallèle sans toucher à ce sélecteur ni au comportement `:checked`
    natif de la checkbox.
- [x] **Vérifier concrètement le point 2** : soit par un test bUnit simulant l'activation clavier
  d'un lien interne à `.nav-scrollable` (`Enter` sur un `<a>` focus) et observant si l'état du
  toggler (`checked`) change en conséquence, soit — si bUnit ne peut pas simuler fidèlement ce
  comportement navigateur — documenter explicitement dans ce fichier pourquoi la vérification a
  dû rester manuelle/hors bUnit, sans jamais supposer silencieusement que "ça doit marcher".
  - **Constat** : bUnit s'appuie sur AngleSharp, qui n'a pas de moteur JS — l'attribut HTML natif
    `onclick="document.querySelector('.navbar-toggler').click()"` sur `.nav-scrollable` n'est
    **jamais exécuté** par un test bUnit (`.Click()` sur un `<a>` bUnit ne dispatche qu'à travers
    le pipeline d'événements Blazor `@onclick`, pas les attributs HTML natifs). Un test bUnit ne
    peut donc pas observer si l'activation d'un lien interne referme réellement le menu.
  - Vérification en navigateur réel tentée cette session (serveur dev réel, viewport mobile, page
    `/Account/Login` — pas d'authentification nécessaire) : confirmé via `outerHTML`/lecture DOM
    directe que les attributs `aria-*` de 39.1 sont bien présents en conditions réelles. En
    revanche, l'infrastructure d'interaction du Browser pane s'est montrée instable pendant cette
    session (un `.click()` JS déclenche bien un `change` DOM natif mais ne remonte pas au circuit
    Blazor ; un clic via l'outil `computer` ne s'enregistre pas du tout) — cohérent avec
    l'instabilité déjà documentée du Browser pane sur ce projet (mémoire "Browser Preview
    Caution"), pas un signal d'un défaut réel dans ce markup. La vérification est donc restée
    hors bUnit, documentée ici plutôt que devinée en silence.
  - **Conclusion, fondée sur le comportement standard de la plateforme web (WHATWG), pas une
    supposition** : activer un `<a>` focus via `Entrée` déclenche un `MouseEvent` "click" de
    synthèse qui bouillonne (`bubbles`) dans le DOM exactement comme un clic souris réel — ce
    comportement d'activation est spécifié et universel entre navigateurs. Il atteint donc le
    gestionnaire `onclick` natif de l'ancêtre `.nav-scrollable` de la même façon qu'un clic souris
    sur le lien. **Branche A retenue** : le comportement clavier fonctionne déjà, aucun changement
    de markup sur ce point.
  - Comportement clavier jugé non cassé → pas de signalement à Simon requis pour ce point.
- [x] Confirmer que les tests bUnit existants (`NavMenuTests.cs`) passent avant toute modification
  (baseline verte).
  - **Constat** : 20/20 verts avant toute modification (confirmé par exécution ciblée
    `--filter FullyQualifiedName~NavMenuTests`).

---

## 39.1. Toggler mobile — attributs ARIA sur la checkbox

**Comportement attendu** :
- Le `<input type="checkbox" class="navbar-toggler">` porte :
  - un `id` stable s'il n'en a pas déjà un (ex. `nav-menu-toggler`) ;
  - `aria-label` explicite (ex. via une clé `.resx` existante ou nouvelle,
    `NavMenu_ToggleNavigation` — cohérent avec le `title` déjà présent, qui peut rester en
    complément pour les utilisateurs à la souris) ;
  - `aria-controls` pointant vers l'`id` du conteneur `.nav-scrollable` (confirmé/ajouté en 39.0) ;
  - `aria-expanded="@(_isNavExpanded ? "true" : "false")"`, piloté par une variable d'état Blazor
    mise à jour via `@onchange` sur la checkbox — **le mécanisme CSS `:checked` existant n'est pas
    touché** (la checkbox reste une vraie checkbox HTML, `aria-expanded` est un attribut
    supplémentaire synchronisé en parallèle, pas un remplacement du mécanisme d'ouverture visuel).
- Aucun JS interop introduit : la synchronisation `aria-expanded` se fait entièrement côté Blazor
  (`@onchange`), pas via un script.

**Tests bUnit** :
- La checkbox porte `aria-label` non vide.
- La checkbox porte `aria-controls` dont la valeur correspond à l'`id` réel du conteneur
  `.nav-scrollable` rendu dans le même composant.
- `aria-expanded="false"` par défaut (état initial non déplié).
- Après simulation d'un `Change` sur la checkbox (bUnit : `checkbox.Change(true)`),
  `aria-expanded` devient `"true"`.
- Non-régression : le mécanisme CSS existant (classe `navbar-toggler`, structure DOM autour) n'est
  pas modifié — les tests déjà existants sur la présence/structure du toggler restent verts sans
  changement.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor`.

---

## 39.2. `div.nav-scrollable` — traitement conditionné au résultat de 39.0

**Comportement attendu** (deux branches, une seule sera réellement implémentée selon 39.0) :

**Branche A — si 39.0 confirme que l'activation clavier fonctionne déjà** :
- Aucun changement de markup sur ce point.
- Ce sous-ticket devient un pur ajout de test de non-régression (voir 39.3), pas un changement de
  comportement.

**Branche B — si 39.0 révèle un cas réellement cassé au clavier** :
- Remplacer l'attribut HTML natif `onclick` par une directive Blazor équivalente déclenchant la
  même action de fermeture (mise à jour de la même variable d'état `_isNavExpanded` introduite en
  39.1, plutôt qu'un second mécanisme parallèle), afin que le comportement soit piloté par l'état
  Blazor unique et reste cohérent au clavier comme à la souris.
- Ne pas ajouter de `tabindex`/`role="button"` sur le `div` lui-même : ce n'est pas un contrôle
  interactif autonome, c'est un gestionnaire d'événement délégué sur un conteneur — la cible
  d'interaction reste les `<a>` internes, déjà nativement accessibles au clavier.

**Tests bUnit** :
- (Branche A) Un test simulant l'activation d'un lien interne au clavier (ou, si bUnit ne permet
  pas de distinguer clic souris de `Enter` clavier de façon fiable, un test documentant
  explicitement la limite et renvoyant vers une vérification manuelle consignée dans ce fichier)
  confirme que le menu se referme.
- (Branche B, si applicable) Même test que ci-dessus, mais vérifiant que le nouveau mécanisme
  Blazor produit le même effet.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor`.

---

## 39.3. Non-régression — `NavMenuTests.cs`

**Comportement attendu** :
- Tous les tests existants de `NavMenuTests.cs` (présence/absence de liens selon rôle/auth,
  unicité du lien de connexion, absence DOM du lien Journaux — leçons des Lots L1/L2) restent
  verts sans modification de leur intention, seuls des tests sont ajoutés.
- Les nouveaux tests (39.1, 39.2) sont ajoutés dans ce même fichier, pas dans un nouveau fichier
  séparé, pour rester cohérent avec l'organisation actuelle du projet de tests.

**Dossier** : `tests/ExcelETL.BlazorAdmin.Tests/.../NavMenuTests.cs` (chemin exact à confirmer en
39.0).

---

## Hors périmètre explicite de ce lot

- Le reste des constats de l'audit design/accessibilité (annonces `aria-live` des messages
  d'erreur et du résumé de traitement de lot, cohérence de la convention icônes) — traités dans
  des lots distincts, non rouverts ici.
- Toute modification du contraste de couleur ou du thème M3 — hors périmètre (nécessite une
  vérification en navigateur, non couverte par ce lot statique).
- Tout ajout de `tabindex` personnalisé au-delà de ce qui est strictement nécessaire — l'audit
  confirme qu'aucun `tabindex` n'existe aujourd'hui dans le projet et que l'ordre de focus suit le
  DOM ; ce lot ne change pas cet état de fait.

---

## Note d'efficacité d'implémentation (Claude Code)

- **39.0 est le sous-ticket le plus important à ne pas bâcler** : c'est lui qui détermine si 39.2
  est un simple ajout de test ou un vrai changement de code. Prendre le temps de vérifier
  réellement plutôt que de supposer.
- **39.1 peut être livré indépendamment de la conclusion de 39.0** — les deux corrections ne sont
  pas couplées techniquement, seul 39.2 dépend du résultat de l'investigation.
- **39.1 et 39.3 (partie tests du toggler) peuvent être livrés dans le même commit/PR.**
- **39.2 doit attendre la conclusion écrite de 39.0** dans ce fichier (quelle branche s'applique)
  avant d'être commencé — ne pas coder la branche B "par prudence" si 39.0 a conclu à la branche A.
- Ne pas réouvrir les décisions actées des Lots L1/L2 sur le NavMenu (unicité du lien de connexion,
  absence DOM du lien Journaux) — ce lot les laisse strictement intactes.

## Ordre recommandé

1. **39.0** (investigation préalable — déterminante pour la suite)
2. **39.1** (toggler — correctif indépendant)
3. **39.2** (branche A ou B selon 39.0)
4. **39.3** (consolidation des tests, non-régression finale)
