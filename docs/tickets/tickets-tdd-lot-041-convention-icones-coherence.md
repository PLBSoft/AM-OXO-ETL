# Tickets TDD — Lot 041 : mise en cohérence de la convention icônes

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Huitième lot
utilisant la convention numérique à trois chiffres, après le lot 040
(`tickets-tdd-lot-040-annonces-etat-asynchrone.md`).*

**Contexte** : fait suite à `audit-design-blazoradmin-2026-07-27.md` (§2, tableau Cohérence) et à
sa synthèse priorisée (`audit-priorisation-design-blazoradmin-2026-07-27.md`, recommandation
prioritaire 3). Trois écarts distincts entre le texte de `convention-ui-blazor-icones-boutons.md`
et/ou son application réelle, plus un correctif ciblé identifié dans le même tableau.

**Décisions actées avec Simon (27/07)** :
- Les CTA principaux et les boutons "Enregistrer" des sous-formulaires imbriqués **doivent
  recevoir une icône**, conformément à la matrice de décision déjà écrite dans la convention — ce
  n'est pas la convention qui est fausse sur ce point, c'est l'implémentation qui est en retard.
- Le texte de la convention décrivant des classes `bi bi-*` (police Bootstrap Icons) est, lui,
  **obsolète et doit être corrigé pour documenter le SVG inline réel** (`AdminIconMarkup`) —
  aucune migration de code vers `bi bi-*` n'aura lieu. Rationale : le SVG inline évite une
  dépendance à une police externe (pas de flash de contenu non stylé), permet un contrôle exact de
  la couleur via `currentColor`/`fill` pour s'adapter aux thèmes clair/sombre, et l'implémentation
  réelle est déjà à 100 % cohérente sur ce mécanisme (aucune exception trouvée par l'audit) —
  migrer vers `bi bi-*` serait une réécriture purement cosmétique, à haut risque de régression,
  sans bénéfice utilisateur. **Ce point ne doit pas être rouvert dans ce lot ni dans un lot
  ultérieur sans nouvelle décision explicite.**

**Ce que ce lot n'est pas** : ni une redesign visuelle des boutons existants qui ont déjà une
icône, ni une extension du catalogue d'icônes `AdminIconMarkup` au-delà de ce qui existe déjà si
les icônes nécessaires y sont déjà présentes (réutiliser, pas dupliquer).

---

## 41.0. Investigation préalable (obligatoire avant tout code)

- [ ] Relire le texte actuel de `convention-ui-blazor-icones-boutons.md` en intégralité pour
  identifier précisément tous les passages mentionnant `bi bi-*` à corriger (pas seulement
  l'exemple principal).
- [ ] Confirmer la liste exacte des CTA principaux actuellement sans icône : bouton "Créer un
  profil" (listes de profils), `save-profile-button`, `process-button`,
  `generate-workbook-button`, et les boutons "Enregistrer" des 6 sous-formulaires imbriqués
  (`SheetRuleForm`, `BlockFieldForm`, `SheetGenerationRuleForm`, `ColumnDefinitionForm`,
  `PointColumnDefinitionForm`, `ApplicationColumnDefinitionForm`) — confirmer que ce compte n'a
  pas changé depuis l'audit du 27/07.
- [ ] Vérifier dans `AdminIconMarkup` si des icônes appropriées existent déjà pour "créer/ajouter"
  (probablement déjà utilisée ailleurs, ex. bouton "Ajouter une règle") et pour "enregistrer/
  valider/traiter" (à distinguer : "Enregistrer" un profil/sous-formulaire vs "Traiter"/"Générer"
  un fichier sont deux actions sémantiquement différentes, une même icône pour les deux serait
  trompeuse). Réutiliser l'existant si pertinent ; ne créer une nouvelle icône dans le catalogue
  que si aucune icône existante ne convient sémantiquement.
- [ ] Confirmer l'absence exacte de `title` sur `SheetRuleForm.razor` (champ de bloc) et
  `SheetGenerationRuleForm.razor` (colonne Application) — ces deux composants ont déjà
  `aria-label`, seul `title` manque.
- [ ] Confirmer l'absence exacte d'`aria-label` sur `log-copy-btn` (`Logs.razor`) — seul `title`
  est présent aujourd'hui, c'est l'inverse des deux composants ci-dessus.

---

## 41.1. Correction du texte de `convention-ui-blazor-icones-boutons.md`

**Comportement attendu** :
- Remplacer toute mention de classes `bi bi-*` par une description fidèle du mécanisme réel : SVG
  inline via le composant/dictionnaire `AdminIconMarkup`, avec un exemple d'utilisation réel tiré
  du code (pas un exemple inventé).
- Mise à jour **en place** du document existant — ne pas ajouter un second bloc "v2" à côté du
  texte `bi bi-*`, le remplacer entièrement (cohérent avec la convention "pas d'accumulation
  d'historique de version dans un document vivant").
- La matrice de décision (quels boutons doivent porter une icône) n'est **pas modifiée** par ce
  sous-ticket — seul le mécanisme technique décrit change, pas la règle de décision elle-même
  (celle-ci est confirmée telle quelle par la décision actée en tête de ce document).

**Dossier** : `convention-ui-blazor-icones-boutons.md`.

---

## 41.2. Icônes sur les CTA principaux et boutons "Enregistrer"

**Comportement attendu** :
- Chacun des boutons listés en 41.0 reçoit une icône `AdminIconMarkup` sémantiquement appropriée
  (réutilisée si trouvée, cf. 41.0), positionnée conformément au patron déjà utilisé par les
  boutons qui en ont déjà une (ordre icône/texte, espacement).
- `save-profile-button` et les 6 boutons "Enregistrer" des sous-formulaires reçoivent la même
  icône (action identique — enregistrer), pour rester cohérent entre eux.
- `process-button` et `generate-workbook-button` reçoivent chacun une icône distincte de celle
  d'"Enregistrer" si l'investigation 41.0 confirme qu'aucune icône existante ne convient aux deux
  à la fois — sinon, réutiliser celle déjà en place pour une action équivalente ailleurs.
- Aucun changement de la logique métier des boutons (handlers `@onclick` inchangés) — uniquement
  l'ajout du markup icône, à l'identique du patron déjà utilisé sur les boutons conformes.

**Tests bUnit** :
- Pour chaque bouton listé : un test vérifie la présence d'un élément `<svg>` (ou équivalent
  `AdminIconMarkup`) à l'intérieur du bouton, sélectionné par son `id` stable existant — jamais
  par texte ou position.
- Non-régression : le texte du bouton et son comportement au clic restent inchangés (réutiliser
  les tests de clic déjà existants sans les modifier).

**Dossier** : les fichiers listés en 41.0, sous
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/` (éditeurs de profil et listes de profils).

---

## 41.3. `title` manquant — `SheetRuleForm.razor` et `SheetGenerationRuleForm.razor`

**Comportement attendu** :
- Le bouton icône du champ de bloc dans `SheetRuleForm.razor` et celui de la colonne Application
  dans `SheetGenerationRuleForm.razor` reçoivent un attribut `title` (texte identique à
  l'`aria-label` déjà présent), conformément à la convention qui exige les deux attributs sur tout
  bouton icône seule.

**Tests bUnit** :
- Les deux boutons concernés portent un `title` non vide, identique en contenu à leur
  `aria-label` existant.

**Dossier** : `SheetRuleForm.razor`, `SheetGenerationRuleForm.razor`.

---

## 41.4. `aria-label` manquant et taille de cible — `log-copy-btn` (`Logs.razor`)

**Comportement attendu** :
- `log-copy-btn` reçoit un `aria-label` (contenu identique au `title` déjà présent) — c'est le
  seul cas relevé par l'audit strictement non conforme à la règle déjà actée (`aria-label`
  obligatoire, `title` complémentaire).
- Alignement de la taille de la cible tactile sur celle des autres boutons icône du projet
  (`.block-field-icon-btn`, 34px), au lieu du `padding: 0.1rem 0.35rem` actuel.

**Tests bUnit** :
- `log-copy-btn` porte un `aria-label` non vide.
- Non-régression : le comportement de copie et le `title` existant restent inchangés.

**Dossier** : `Logs.razor` (+ CSS associé si isolation CSS par composant).

---

## Hors périmètre explicite de ce lot

- Toute nouvelle icône ajoutée au catalogue `AdminIconMarkup` si une icône existante peut être
  réutilisée sémantiquement (cf. 41.0) — pas de duplication d'icônes équivalentes.
- Migration vers `bi bi-*` — décision explicitement écartée en tête de ce document, à ne pas
  rouvrir.
- Les autres constats déjà traités (Lots 039, 040) ou non retenus dans cette synthèse
  (`aria-describedby`, sauts de niveaux de titre, divergence `container-fluid` Import/Export) —
  non traités ici.

---

## Note d'efficacité d'implémentation (Claude Code)

- **41.0 doit trancher le choix d'icônes avant que 41.2 ne commence** — éviter de coder 41.2 puis
  découvrir en cours de route qu'une icône plus appropriée existait déjà ailleurs.
- **41.1 est indépendant du reste** (pure documentation) — peut être livré dans n'importe quel
  ordre, y compris en parallèle de 41.2/41.3/41.4.
- **41.3 et 41.4 sont triviaux et indépendants l'un de l'autre** — peuvent être livrés dans le
  même commit que 41.2 plutôt qu'un cycle de revue séparé.
- Ne pas rouvrir la matrice de décision de la convention (quels boutons doivent avoir une icône) —
  seule sa description technique change en 41.1, jamais la règle elle-même.

## Ordre recommandé

1. **41.0** (investigation — choix d'icônes à trancher avant tout code)
2. **41.1** (correction du texte de convention — indépendant, peut être fait à tout moment)
3. **41.2** (icônes sur CTA principaux et boutons Enregistrer)
4. **41.3 + 41.4** (correctifs ciblés `title`/`aria-label`, triviaux — même commit que 41.2)
