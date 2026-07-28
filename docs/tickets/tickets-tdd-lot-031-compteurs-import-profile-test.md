# Tickets TDD — Lot 031 : compteurs d'éléments extraits sur `ImportProfileTest.razor`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Troisième lot
utilisant la convention numérique à trois chiffres, après le lot 030
(`tickets-tdd-lot-030-parite-import-export-form-floating-boutons.md`).*

**Demande client/Simon (captures d'écran du 24/07)** : sur `/export-profiles/test`, chaque section
repliable de résultat affiche son titre suivi du nombre d'éléments qu'elle contient — ex.
`Parents (1)`, `Enfants (23)`, `TM_PROC_MAD (51)` (fonctionnalité livrée le 23/07, voir
`etat-avancement-global-2026-07-24.md`, commit `aa8fdb6`, "Polish UX `ExportProfileTest.razor`
(sections repliables, retour à la ligne lisible, **compteurs d'éléments par feuille**)"). Sur
`/import-profiles/test`, les sections équivalentes (`Équipement`, `Isolements`, `Points`,
`Tâches multiples`, `Avertissements non bloquants`) n'affichent **pas** ce compteur dans leur
titre — écart de parité UI/UX entre les deux pages de test, alors qu'elles partagent le même
pattern de sections repliables (accordéons) issu du même lot (voir Lot V8/commits du 23/07).

**Portée** : ce lot ne fait qu'étendre un pattern d'affichage déjà validé et livré côté export au
composant `ImportProfileTest.razor` — ce n'est pas une nouvelle fonctionnalité, aucune nouvelle
donnée n'est nécessaire (les listes existent déjà en mémoire dans l'`ImportResult` rendu par
`ImportPipelineOrchestrator.Run(...)`, seul le titre de section affiché change).

**Hors périmètre explicite** :
- Aucune modification de `ImportPipelineOrchestrator`, d'`ImportResult`, ou de tout code
  Domain/Application — le nombre d'éléments est déjà connu au moment du rendu (`.Count` sur les
  collections déjà présentes), c'est une modification strictement Blazor/présentation.
- Aucune modification du comportement/contenu de `ExportProfileTest.razor` — page de référence,
  non touchée, seulement lue pour investigation.
- La section "Avertissements non bloquants" (n'existe que côté import, sans équivalent côté
  export) : à traiter par cohérence si le pattern s'y applique naturellement, mais ne pas
  bloquer le lot dessus si sa structure diverge (voir 31.0).
- Le composant d'upload de fichier personnalisé masquant le texte natif du navigateur (demande
  X11, déjà mise de côté explicitement par Simon le 24/07) — non concerné, non réouvert ici.

---

## 31.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `ExportProfileTest.razor` et repérer précisément où et comment le compteur est
  construit pour chaque section repliable (`Parents (1)`, `Enfants (23)`, `TM_PROC_MAD (51)`,
  etc.) : nom de la variable/expression Razor utilisée (`.Count`, `.Count()`, propriété dédiée),
  format exact du texte (espace avant la parenthèse, présence de parenthèses simples), et si ce
  texte fait partie de l'en-tête cliquable de l'accordéon (donc doit rester dans un élément
  cliquable/`aria`-cohérent) ou d'un élément séparé à côté.
- [ ] Vérifier si ce compteur est codé en dur par section (une expression par titre) ou factorisé
  via un composant/une méthode partagée (ex. un composant d'en-tête d'accordéon générique) —
  déterminant pour savoir si le lot doit dupliquer une expression ou simplement invoquer un
  composant déjà réutilisable.
- [ ] Lire `ImportProfileTest.razor` dans son état actuel réel : lister les sections repliables
  existantes (Équipement, Isolements, Points, Tâches multiples, Avertissements non bloquants),
  confirmer les collections `ImportResult` correspondant à chacune (probablement
  `ImportResult.Equipement` — singulier ou liste selon le modèle, à vérifier —,
  `ImportResult.Isolements`, `ImportResult.Points`, `ImportResult.TachesMultiples`,
  `ImportResult.Errors`), et le texte de titre actuel de chaque section (clé resx utilisée, s'il y
  en a une).
- [ ] Confirmer si les titres de section sont des clés de ressource localisées (`.resx`, EN/FR,
  cohérent avec le reste du projet) plutôt que du texte en dur — si oui, le format du compteur
  doit être injecté par interpolation dans la valeur résolue, pas concaténé en dur dans le
  Razor, pour éviter de casser la localisation (voir le bug corrigé en V1 sur un problème similaire
  d'interpolation manquante).
- [ ] Vérifier le cas particulier `Equipement` : si le modèle expose un seul `EquipementPivot`
  (pas une liste, un import ne traitant qu'un seul Équipement racine par fichier — voir
  `modele-domaine-import-profile.md` §2.2), le "compteur" pertinent est soit `1`/`0` (présent ou
  non), soit ce champ n'a simplement pas d'équivalent de compteur multiple et peut être laissé de
  côté ou affiché `(1)`/`(0)` selon présence — trancher explicitement, ne pas suivre aveuglément
  le pattern "Count" d'une collection s'il n'y a pas de collection.

---

## 31.1. `ImportProfileTest.razor` — ajout du compteur dans le titre de chaque section

**Comportement attendu** :
- Chaque titre de section repliable existante affiche désormais le nombre d'éléments qu'elle
  contient, au même format que `ExportProfileTest.razor` (ex. `Isolements (67)`,
  `Points (12)`, `Tâches multiples (3)`).
- `Équipement` : affiche `(1)` si un Équipement a été extrait, `(0)` si absent (cas de rejet de
  fichier, voir F2 — dans ce cas la section entière est de toute façon remplacée par l'alerte
  rouge "File rejected", donc ce cas ne devrait pas se présenter en pratique pour cette section
  précise ; documenter ce raisonnement dans le code/commit plutôt que de le deviner silencieusement
  si l'investigation 31.0 montre autre chose).
- `Avertissements non bloquants` : affiche le nombre d'éléments de `ImportResult.Errors`
  (cohérent avec la condition d'affichage existante `Errors.Count > 0`), au même format que les
  autres sections, uniquement si l'investigation 31.0 confirme que sa structure d'en-tête est
  suffisamment proche du pattern des autres sections pour l'appliquer sans réécriture ad hoc ; à
  défaut, documenter explicitement pourquoi elle est laissée de côté plutôt que de forcer le
  pattern.
- Aucune section n'est ajoutée ni retirée par ce lot — uniquement le titre de chacune des
  sections déjà existantes est enrichi.
- Si le compteur est factorisé côté export via un composant/méthode partagée (voir 31.0) :
  réutiliser exactement ce composant/cette méthode plutôt que dupliquer une expression
  équivalente en Razor — cohérent avec le principe DRY déjà en place sur ce type de pattern
  partagé entre les deux pages de test.
- Si les titres sont des clés resx : mettre à jour les clés EN/FR concernées pour inclure un
  paramètre `{0}` de compteur (interpolé via `string.Format`/interpolation, jamais concaténé
  séparément du texte localisé), à l'identique du mécanisme déjà en place côté export.

**Tests** (bUnit, contre les fixtures réelles déjà utilisées par les tests existants de
`ImportProfileTest.razor` — pas de nouvelle fixture) :
- Test contre la fixture `D8570_chgt_plateaux` (67 isolements déjà utilisés par le test existant
  `Run_D8570Fixture_ShowsVanneAsNonBlockingWarning_NotAsFileRejection`) : le titre de la section
  Isolements rendu contient bien `(67)` — assertion sur le texte du titre, pas sur un
  recomptage manuel dans le test.
- Un test par section restante (Équipement, Points, Tâches multiples) contre une fixture réelle
  au choix (C7401 ou G6306B), avec assertion sur le nombre exact affiché dans le titre
  correspondant à `.Count` de la collection réellement retournée par
  `ImportPipelineOrchestrator.Run(...)` pour cette fixture (pas une valeur codée en dur dans le
  test qui pourrait diverger silencieusement du vrai résultat — comparer au `.Count` obtenu en
  invoquant le même orchestrateur dans le test, pas à un nombre magique).
- Non-régression explicite : les tests bUnit existants de `ImportProfileTest.razor`
  (`Run_D8570Fixture_ShowsVanneAsNonBlockingWarning_NotAsFileRejection`,
  `SelectingFile_ThatFailsProcedureValidation_ShowsRejectedFileSection_NotAsAWarning`) restent
  verts sans modification de leurs assertions existantes (uniquement des assertions
  supplémentaires ajoutées, pas de suppression/altération des assertions déjà en place).
- Si un composant/une méthode partagée est réutilisée depuis le code export (voir 31.0) :
  test de non-régression sur `ExportProfileTest.razor` confirmant qu'aucun comportement existant
  n'y a été modifié par cette réutilisation (le composant est appelé depuis un nouvel
  emplacement, pas changé dans son comportement).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileTest.razor`
(+ éventuel composant partagé si factorisation confirmée en 31.0, + miroir tests existants,
étendus — pas de nouveau fichier de test si la classe de test existante peut simplement recevoir
des `[Fact]` supplémentaires).

---

## Note d'efficacité d'implémentation

Traiter 31.0 intégralement avant d'écrire le moindre test ou code : la totalité de ce lot dépend
de la structure exacte trouvée côté export (format du texte, factorisation ou duplication,
mécanisme resx). Ne pas commencer à écrire des tests sur `ImportProfileTest.razor` en supposant un
format de compteur avant d'avoir lu le code réel de `ExportProfileTest.razor` — le risque
principal de ce lot est de reproduire un format légèrement différent (espace, parenthèses,
pluriel) qui casserait la parité visuelle réellement demandée par Simon.
