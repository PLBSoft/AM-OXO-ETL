# Tickets TDD — Lot 033 : upload multi-fichiers sur les pages de test (import/export)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Quatrième lot
utilisant la convention numérique à trois chiffres, après le lot 032
(`tickets-tdd-lot-032-detection-incoherence-type-procedure.md`).*

**Demande client/Simon** : le client va fournir une dizaine de nouveaux fichiers sources
supplémentaires (en plus des 3 fixtures réelles existantes) pour valider l'extraction. Tester ces
fichiers un par un sur `/import-profiles/test` et `/export-profiles/test` est fastidieux. Ce lot
étend les deux pages pour accepter l'upload de plusieurs fichiers en une seule fois, avec
traitement séquentiel de chacun via la pipeline existante, inchangée.

**Décisions actées avec Simon** :
- `<InputFile multiple>` sur les deux pages — accepte nativement 1 à N fichiers, aucun mode UI
  séparé "un fichier / plusieurs fichiers" à construire.
- **Limite : 20 fichiers max par batch, 10 Mo max par fichier individuel.** Limite de poids total
  du batch = valeur **dérivée**, pas une constante indépendante à maintenir séparément
  (`20 × 10 Mo = 200 Mo`) — si l'une des deux constantes de base change un jour, le total se
  recalcule automatiquement, pas de risque de désynchronisation entre deux valeurs codées en dur.
- **Dépassement (nombre > 20 OU un fichier > 10 Mo OU total > 200 Mo) → rejet total du batch avant
  tout traitement.** Aucun fichier n'est traité, message d'erreur global explicite (ex. nombre de
  fichiers sélectionnés vs limite, ou poids total vs limite, selon la contrainte violée),
  cohérent avec le principe "rien de traité en cas de doute" déjà appliqué au rejet métier d'un
  fichier individuel invalide.
- **Traitement séquentiel** (boucle `foreach`, pas de parallélisation) — la pipeline reste
  intégralement en mémoire et rapide à l'échelle de 20 fichiers de quelques centaines de Ko ;
  paralléliser serait une optimisation prématurée (YAGNI) et introduirait un risque de
  thread-safety sur l'état du composant Blazor sans bénéfice mesurable.
- **Isolement des erreurs techniques** : si un fichier du batch lève une exception non gérée
  pendant sa lecture/son traitement (fichier corrompu, format totalement invalide — distinct du
  rejet métier `Equipement is null`/validation PROCEDURE), les autres fichiers du batch continuent
  d'être traités normalement. Le fichier en échec technique affiche un statut **"Erreur
  technique"**, visuellement et sémantiquement distinct du statut **"Rejected"** (rejet métier) et
  du statut **"Avertissement non bloquant"** (déjà existant, ex. cas VANNE de D8570).
- **Affichage** : un résumé global en tête de page (ex. "12 fichiers traités : 10 OK, 1
  avertissement non bloquant, 1 rejeté") + une section accordéon par fichier, titrée par le nom du
  fichier source, réutilisant le pattern accordéon déjà en place (Lot V, correctif R3).
- **Côté `ExportProfileTest.razor`** : un bouton de téléchargement par fichier généré (pas de zip
  agrégé — inutile à ce stade, YAGNI), nommé
  `{nom-fichier-source-sans-extension}_export.xlsx`. Si l'`ImportResult` d'un fichier du batch a
  une erreur bloquante, la génération est bloquée **pour ce fichier uniquement** (même règle que
  J3), les autres fichiers du batch ne sont pas affectés.
- **Factorisation de composant partagé** : à **vérifier**, pas à imposer a priori. Le comportement
  (validation batch, boucle de traitement, résumé + accordéon par fichier) est identique entre les
  deux pages à l'exception du contenu affiché par accordéon (import seul vs import+export) et du
  bouton de téléchargement export. Si la structure réelle du code des deux composants s'y prête
  (logique extractible sans complexifier ni l'une ni l'autre page), factoriser ; sinon, dupliquer
  et documenter pourquoi la factorisation n'a pas été retenue — cohérent avec le principe déjà
  acté "architecture by fitness, not mimicry" (ne pas copier un pattern sans vérifier qu'il
  convient réellement aux deux cas).

**Hors périmètre explicite** :
- Pas de parallélisation du traitement des fichiers du batch (voir décision ci-dessus).
- Pas de zip de téléchargement agrégé côté export.
- Pas de persistance/historique des résultats de batch (aucune nouvelle table, aucun nouveau sink
  — les pages de test restent en mémoire, sans round-trip HTTP, comme aujourd'hui).
- Pas de barre de progression fichier par fichier pendant le traitement — un indicateur de
  chargement global (spinner déjà en place pour le cas mono-fichier) suffit, le traitement de 20
  fichiers de cette taille restant rapide.
- Pas de traitement de la demande X11 (bouton d'upload personnalisé masquant le texte natif du
  navigateur, déjà mise de côté explicitement par Simon le 24/07) — ce lot ajoute l'attribut
  `multiple` à l'`<InputFile>` existant tel qu'habillé par V10 (`form-control form-control-lg` +
  `input-group`), sans reconstruire le composant.
- Pas de déduplication ni de détection de noms de fichiers identiques au sein d'un même batch —
  chaque fichier est traité et affiché indépendamment, un nom dupliqué dans le batch n'est pas une
  erreur (l'orchestrateur ne connaît pas le nom du fichier source, seulement son contenu).
- Pas de modification du comportement mono-fichier existant au-delà de son extension au cas
  multi-fichiers (`multiple` accepte transparemment 1 fichier — aucune branche de code séparée
  "ancien comportement legacy" à maintenir).

---

## 33.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire intégralement `ImportProfileTest.razor` et `ExportProfileTest.razor` (code-behind +
  markup) pour confirmer la structure exacte du gestionnaire `OnInputFileChangeAsync` actuel,
  l'endroit précis où `e.File.OpenReadStream(...)` est appelé, et la structure de l'état du
  composant utilisé pour l'affichage du résultat mono-fichier actuel.
- [ ] Vérifier la signature exacte de `InputFileChangeEventArgs.GetMultipleFiles(int
  maxAllowedFiles)` disponible côté .NET 10/Blazor utilisé par le projet, et si un
  `maxAllowedSize` doit être passé explicitement à `OpenReadStream(maxAllowedSize: ...)` (la
  valeur par défaut de Blazor est très basse, ~500 Ko — **piège connu**, à vérifier et fixer
  explicitement à la limite de 10 Mo retenue pour ce lot, sans quoi tout fichier > 500 Ko lèverait
  une exception avant même la validation métier du lot).
- [ ] Confirmer l'emplacement du pattern accordéon réutilisable (composant partagé existant, ou
  markup dupliqué par section) introduit en Lot V8/R3, pour réutiliser exactement la même
  structure/CSS pour l'accordéon "par fichier" de ce lot plutôt que d'en inventer un nouveau.
- [ ] Lire `ImportProfileTestTests.cs`/`ExportProfileTestTests.cs` existants pour identifier les
  tests d'upload mono-fichier à ne pas casser (`InputFileContent.CreateFromText(...)` +
  `.UploadFiles(file)`), et confirmer qu'ils restent verts sans modification une fois `multiple`
  ajouté (un seul fichier reste un batch valide de taille 1).
- [ ] Vérifier s'il existe déjà une constante/config centralisée pour des limites similaires
  ailleurs dans le projet (aucune trouvée à ce jour dans les tickets/specs existants) — si absent,
  ce lot introduit les deux constantes (`MaxFilesPerBatch = 20`, `MaxFileSizeBytes = 10 Mo`) à un
  emplacement partagé clair (ex. constantes statiques sur le composant, ou classe dédiée si
  réutilisée par les deux pages — dépend du résultat de l'investigation de factorisation, 33.4).

---

## 33.1. Validation du batch avant tout traitement (nombre, poids individuel, poids total)

**Comportement attendu**, identique sur les deux pages :
- Au changement de sélection de fichiers (`OnInputFileChangeAsync`), avant tout appel à
  `ImportPipelineOrchestrator.Run(...)` :
  1. Si `files.Count > 20` → rejet total, message localisé explicite (nombre sélectionné vs
     limite), aucun fichier traité, état de résultat précédent effacé.
  2. Si un fichier individuel dépasse 10 Mo → rejet total, message localisé nommant le(s)
     fichier(s) en cause et leur taille, aucun fichier traité.
  3. Si la somme des tailles dépasse 200 Mo (garde-fou en plus des deux contraintes ci-dessus,
     redondant dans la plupart des cas mais couvre le cas 20 fichiers de 10 Mo pile à la limite
     individuelle mais dont l'addition dépasserait une éventuelle marge future si les constantes
     de base changent) → rejet total, message localisé donnant le poids total vs limite.
  4. Si aucune contrainte violée → traitement séquentiel démarre (33.2/33.3).
- Les trois messages de rejet sont des clés `.resx` distinctes (pas de message générique unique),
  pour que l'utilisateur sache immédiatement laquelle des trois contraintes a été violée.

**Tests** (bUnit) :
- Sélection de 21 fichiers valides (taille/contenu peu importent ici) → message de rejet "nombre"
  affiché, `ImportPipelineOrchestrator.Run` jamais appelé (vérifiable via spy/mock si
  l'orchestrateur est injecté en interface, sinon assertion sur l'absence de toute section
  résultat dans le DOM).
- Sélection de 20 fichiers exactement → accepté, traitement démarre (limite inclusive, pas de
  "off by one").
- Sélection d'1 fichier de 11 Mo → message de rejet "poids individuel" affiché, nommant le
  fichier concerné.
- Sélection d'1 fichier de 10 Mo exactement → accepté (limite inclusive).
- Sélection de fichiers dont le total dépasse 200 Mo sans qu'aucun fichier individuel ne dépasse
  10 Mo (ex. via une construction de test qui contourne la contrainte individuelle) → message de
  rejet "poids total" affiché.
- Non-régression : sélection d'1 seul fichier valide (cas mono-fichier historique) → comportement
  strictement inchangé par rapport à avant ce lot (réutiliser les tests existants sans
  modification de leurs assertions).

---

## 33.2. `ImportProfileTest.razor` — traitement séquentiel du batch + résumé + accordéon par fichier

**Comportement attendu** :
- Boucle séquentielle sur les fichiers validés par 33.1 : pour chaque fichier, `new
  ClosedXmlWorkbookReader(fileStream)` → `ImportPipelineOrchestrator.Run(...)`, exactement comme
  le cas mono-fichier actuel, encapsulé dans un `try/catch` par fichier pour l'isolement des
  erreurs techniques (voir décision actée).
- Statut par fichier, un parmi quatre : **OK** (aucune erreur), **Avertissement non bloquant**
  (comportement déjà existant, ex. cas VANNE), **Rejected** (`Equipement is null`/validation
  PROCEDURE — comportement déjà existant), **Erreur technique** (exception non gérée levée
  pendant le traitement — nouveau statut de ce lot).
- Résumé global en tête de page : nombre total de fichiers, et répartition par statut (ex. "12
  fichiers traités : 10 OK, 1 avertissement non bloquant, 1 rejeté"). Le résumé inclut le compte
  "Erreur technique" séparément s'il y en a au moins un.
- Une section accordéon par fichier, titrée par le nom du fichier source (pas un numéro
  générique), avec le badge de statut visible dans l'en-tête de l'accordéon (repliée par défaut si
  plus d'un fichier dans le batch, cohérent avec le pattern déjà en place pour les sections
  internes à un résultat — dépliée par défaut si un seul fichier, pour ne pas dégrader
  l'expérience mono-fichier actuelle d'un clic supplémentaire).
- Contenu de chaque accordéon "fichier" : identique au contenu actuellement affiché en mono-fichier
  (tables Équipement/Isolements/Points/Tâches multiples/Avertissements non bloquants, compteurs du
  Lot 031), sans changement de ce sous-contenu.

**Tests** (bUnit) :
- Batch de 3 fichiers (les 3 fixtures réelles C7401/D8570/G6306B) → résumé affichant "3 fichiers
  traités : ..." avec la répartition exacte attendue (D8570 = avertissement non bloquant VANNE,
  les deux autres = OK), 3 sections accordéon titrées par nom de fichier.
- Batch avec un fichier synthétique invalide (`Equipement is null`) mêlé à des fixtures valides →
  le fichier invalide affiche "Rejected" avec ses erreurs, les autres fichiers du batch affichent
  leur statut normal, aucun fichier valide n'est affecté par le rejet du fichier invalide.
- Batch avec un fichier synthétique corrompu (contenu non-Excel valide, provoquant une exception
  non gérée dans `ClosedXmlWorkbookReader`) mêlé à des fixtures valides → le fichier corrompu
  affiche "Erreur technique", les autres fichiers du batch sont traités normalement, aucune
  exception ne remonte jusqu'à faire échouer le rendu de la page entière.
- Non-régression explicite : les tests bUnit existants
  (`Run_D8570Fixture_ShowsVanneAsNonBlockingWarning_NotAsFileRejection`,
  `SelectingFile_ThatFailsProcedureValidation_ShowsRejectedFileSection_NotAsAWarning`) restent
  verts sans modification de leurs assertions (batch d'1 fichier = cas particulier du batch de N).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileTest.razor`
(+ miroir tests).

---

## 33.3. `ExportProfileTest.razor` — miroir de 33.2, avec génération/téléchargement par fichier

**Comportement attendu**, symétrique de 33.2, avec les particularités déjà actées pour cette page
(import **et** export par fichier du batch) :
- Pour chaque fichier du batch : import (comme 33.2) puis, si l'`ImportResult` n'a pas d'erreur
  bloquante, génération en mémoire via `SheetGenerationEngine` + `ClosedXmlWorkbookWriter`, sans
  appel HTTP — inchangé par rapport au comportement mono-fichier actuel.
- Si l'`ImportResult` d'un fichier a une erreur bloquante (`Equipement is null`), l'étape de
  génération est bloquée **pour ce fichier uniquement** (comportement déjà acté en J3), la section
  accordéon de ce fichier affiche "Rejected" sans proposer de génération/téléchargement ; les
  autres fichiers du batch ne sont pas affectés.
- Bouton de téléchargement individuel par fichier généré avec succès, nommé
  `{nom-fichier-source-sans-extension}_export.xlsx`.
- Même résumé global + accordéon par fichier que 33.2, avec en plus le bouton de téléchargement
  dans chaque accordéon "OK"/"Avertissement non bloquant" (les deux statuts où une génération a eu
  lieu).

**Tests** (bUnit) :
- Batch de 3 fixtures réelles + un `ExportProfile` de test → 3 sections accordéon, chacune avec un
  bouton de téléchargement nommé correctement (`C7401_export.xlsx`, etc. — nom exact dépendant du
  nom réel des fixtures, à vérifier lors de l'implémentation).
- Batch avec un fichier synthétique `Equipement is null` mêlé à des fixtures valides → le fichier
  concerné affiche "Rejected" sans bouton de téléchargement, aucun appel au moteur de génération
  pour ce fichier (vérifiable via `Mock`/spy si le moteur est injecté en interface) ; les autres
  fichiers du batch sont générés et téléchargeables normalement.
- Batch avec un fichier corrompu → "Erreur technique", pas de tentative de génération pour ce
  fichier, reste du batch non affecté.
- Non-régression : tests existants du cas mono-fichier (round-trip C7401, cas `Equipement is
  null`, cas VANNE non bloquant) restent verts sans modification.
- Aucune référence à `HttpClient`/`ExcelProcessingClient` dans le composant (même test que déjà
  documenté pour F2/J3).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileTest.razor`
(+ miroir tests).

---

## 33.4. Vérification de factorisation de composant partagé

**Comportement attendu** :
- Une fois 33.1/33.2/33.3 implémentés (dans cet ordre, en dupliquant si nécessaire pour avancer
  sans blocage), comparer la logique réellement écrite sur les deux pages : validation du batch
  (33.1, déjà quasi identique par construction), boucle de traitement + gestion des statuts par
  fichier, structure du résumé + accordéon.
- Si la logique commune est extractible sans complexifier la lecture d'aucune des deux pages
  (ex. un service/composant partagé prenant en paramètre une fonction de traitement par fichier
  et retournant les statuts, réutilisé par les deux), factoriser dans ce même lot.
- Si l'extraction introduirait une abstraction plus complexe que la duplication qu'elle évite
  (ex. les deux boucles divergent trop sur le contenu par fichier pour qu'un composant générique
  reste simple), ne pas factoriser — documenter explicitement dans l'état des lieux de fin de lot
  pourquoi, pour que la décision soit tracée plutôt que silencieuse.

**Tests** : aucun test dédié à cette étape en elle-même — si une factorisation a lieu, les tests
existants de 33.2/33.3 doivent rester verts sans modification de leurs assertions (seule
l'implémentation change, pas le comportement observable).

**Dossier** : dépend du résultat (nouveau fichier partagé sous
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/` ou `Shared/` selon convention déjà en place
dans le projet pour d'éventuels composants partagés existants — à vérifier en 33.0).

---

## Ordre recommandé

1. **33.0** (investigation — en particulier le piège `maxAllowedSize`/`GetMultipleFiles`, à
   confirmer avant d'écrire le moindre test)
2. **33.1** (validation du batch — brique commune aux deux pages, la moins risquée)
3. **33.2** (`ImportProfileTest.razor` — plus simple des deux pages, valide le pattern
   résumé/accordéon/statuts avant de l'étendre au cas export)
4. **33.3** (`ExportProfileTest.razor` — réutilise le pattern validé en 33.2, ajoute la couche
   génération/téléchargement)
5. **33.4** (vérification de factorisation, en dernier — une fois le comportement réel des deux
   pages connu, pas avant)

## Note d'efficacité d'implémentation

Traiter le piège `maxAllowedSize` de 33.0 en tout premier, avant d'écrire le moindre test bUnit
d'upload multi-fichiers : un batch de fichiers de test dépassant silencieusement la limite par
défaut de Blazor (~500 Ko) ferait échouer des tests avec une exception `IOException`/"Byte limit"
sans rapport avec la logique métier réellement testée (validation 20/10 Mo/200 Mo), ce qui
gaspillerait du temps de debug sur un faux problème. Fixer `maxAllowedSize` à la limite retenue de
ce lot (10 Mo) dès le premier appel à `OpenReadStream(...)` du code de production, avant d'écrire
33.1.

Ne pas commencer 33.4 avant d'avoir terminé 33.2 **et** 33.3 : toute tentative de factorisation
anticipée avant de connaître le code réel des deux pages traitées séparément risque de produire une
abstraction prématurée qu'il faudrait ensuite défaire — cohérent avec le principe déjà appliqué au
Lot 031 pour l'ordre `31.0`/facteur commun.
