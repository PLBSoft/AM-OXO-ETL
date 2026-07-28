# Tickets TDD — Lot 035 : dette légère / polish (issu des audits qualité du 25/07)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Quatrième lot
utilisant la convention numérique à trois chiffres, après le Lot 034
(`tickets-tdd-lot-034-archivage-fichiers-generes-api.md`, dernier lot livré). Fait suite à la
triple/quintuple passe d'audit qualité par couche du 25/07 (`audit-qualite-domain-2026-07-25.md`,
`audit-qualite-application-2026-07-25.md`, `audit-qualite-infrastructure-2026-07-25.md`,
`audit-qualite-webapi-2026-07-25.md`, `audit-qualite-blazoradmin-2026-07-25.md`), triés et
priorisés en session Claude AI puis validés par Simon le 25/07.*

**Ce lot regroupe exclusivement les points classés "dette légère / cosmétique"** — aucun des
points classés à impact réel/fonctionnel n'est traité ici (voir "Hors périmètre explicite"
ci-dessous, ils feront l'objet de tickets dédiés séparés). Chaque sous-ticket ci-dessous est
indépendant des autres — aucune dépendance croisée entre 35.1 et 35.8, peuvent être livrés dans
n'importe quel ordre ou en parallèle par blocs de couche.

**Conventions déjà en place à respecter (tout le lot)** : `convention-ui-blazor-alignement-boutons.md`,
`convention-ui-blazor-icones-boutons.md` ; IDs HTML stables, jamais de sélection par texte/position
en bUnit ; xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit ; EF Core InMemory réel pour les
repositories, jamais mocké au niveau `DbContext` ; Serilog seul mécanisme de log. **Aucun de ces
correctifs ne change de comportement observable côté utilisateur final ou côté contrat API/Domain**
— chaque sous-ticket est un refacto à comportement figé, les tests existants (avant le sous-ticket)
doivent tous rester verts après.

---

## 35.0. Investigation préalable (obligatoire avant tout code)

- [ ] Confirmer le commit de référence réellement en tête de `main` au démarrage de ce lot (les 5
  audits ont eux-mêmes été exécutés contre `8119f78`, un commit après la référence `d018a90`
  initialement transmise — vérifier qu'aucun lot n'a été mergé entretemps qui invaliderait un des
  constats ci-dessous, notamment sur `NavMenuTests.cs`/`ImportProfiles.razor`/`ExportProfiles.razor`).
- [ ] Relire les extraits cités de chaque audit (localisation exacte fichier + ligne) avant
  d'écrire le moindre test — les numéros de ligne indiqués datent du 25/07 et peuvent avoir
  légèrement dérivé si un autre lot a touché les mêmes fichiers entretemps.

---

## Partie A — `ExcelETL.Domain`

## 35.1. Lisibilité — extraction des blocs de validation de `SheetGenerationRule`

**Constat (audit Domain §7.1)** : le constructeur de `SheetGenerationRule`
(`Generation/Profile/SheetGenerationRule.cs:34-121`, ~90 lignes) concentre 6 blocs de validation
distincts sans découpage nommé — nettement plus long que tout autre constructeur du Domain.

**Comportement attendu** : extraire chacun des 6 blocs en méthode privée statique nommée
(`ValidateSheetNameNotEmpty`, `ValidateColumnPivotSourceCompatibility`,
`ValidateNoPointColumnsForTacheMultiple`, `ValidateNoApplicationColumnsForTacheMultiple`,
`ValidateNoDuplicateHeaders`, `ValidateNoDuplicateColonneOuApplicationNom` — noms indicatifs,
Claude Code choisit la formulation la plus fidèle au code réel), appelées séquentiellement depuis
le constructeur, **dans le même ordre qu'aujourd'hui** (l'ordre de levée d'exception est un
comportement observable testé par `SheetGenerationRuleTests.cs` — ne pas le changer).

**Tests** :
- [ ] Aucun nouveau test requis — les 21 méthodes existantes de `SheetGenerationRuleTests.cs`
  doivent rester vertes sans modification. Ce sous-ticket est validé si la suite existante passe
  intégralement après le refacto, preuve que le comportement observable (message d'exception,
  ordre de levée) est inchangé.

**Hors périmètre de ce sous-ticket** : changer une règle de validation, changer un message
d'erreur, changer `DomainErrorCode`.

---

## 35.2. Déduplication — constantes `MaxNameLength`/`DefaultMarkValue`

**Constat (audit Domain §3.3)** : `MaxNameLength = 60` est déclarée indépendamment dans
`ImportProfile.cs:13` et `ExportProfile.cs:13` ; `DefaultMarkValue = "X"` est déclarée
indépendamment dans `PointColumnDefinition.cs:12` et `ApplicationColumnDefinition.cs:12`. Même
nom, même valeur, aucune relation d'héritage.

**Comportement attendu** :
- Créer `Common/ProfileNaming.cs` portant `public const int MaxNameLength = 60;`, référencée par
  `ImportProfile`/`ExportProfile` à la place de leur constante locale.
- Créer (ou compléter si un fichier `Common/` pertinent existe déjà) une constante partagée pour
  `DefaultMarkValue = "X"`, référencée par `PointColumnDefinition`/`ApplicationColumnDefinition`.
- Ne pas fusionner ces deux constantes entre elles (elles n'ont aucun rapport conceptuel l'une
  avec l'autre) — deux constantes partagées distinctes, pas une classe fourre-tout.

**Tests** :
- [ ] Tests existants (`ImportProfileTests.cs`, `ExportProfileTests.cs`,
  `PointColumnDefinitionTests.cs`/`ApplicationColumnDefinitionTests.cs` ou équivalents) doivent
  rester verts sans modification — même valeur numérique/textuelle, seule la source change.
- [ ] Si un test référence actuellement `ImportProfile.MaxNameLength` directement (plutôt qu'un
  littéral `60`), vérifier qu'il continue de compiler après le déplacement (accès via
  `ProfileNaming.MaxNameLength` si le type d'origine n'expose plus sa propre constante, ou
  conserver une redirection `public const int MaxNameLength = ProfileNaming.MaxNameLength;` sur
  chaque type si supprimer l'accès existant romprait une convention d'accès déjà utilisée
  ailleurs — à trancher en investigation, pas par défaut).

---

## 35.3. Cohérence de nommage — `TypeIncoherenceDansTacheMultiple` en anglais

**Constat (audit Domain §4.2)** : c'est le seul membre d'enum de tout le Domain qui n'est pas en
anglais (`ExtractionErrorCode.TypeIncoherenceDansTacheMultiple`, ajouté au Lot 032). Touche
Domain (déclaration de l'enum) **et** Application (points de construction —
`TacheMultipleTypeCoherenceAnalyzer` et tout `switch`/mapping de message localisé qui le
référence).

**Comportement attendu** : renommer le membre en `TacheMultipleTypeIncoherence` (ou
`TacheMultipleTypeMismatch` — Claude Code choisit celui qui est le plus cohérent avec les noms de
membres voisins de la même enum, ex. `UnrecognizedTypeElement`) et répercuter le renommage sur
tous les points d'usage (`TacheMultipleTypeCoherenceAnalyzer`, tests, ressources `.resx` si une
clé y référence littéralement le nom du membre C#).

**Tests** :
- [ ] Tous les tests existants qui référencent `ExtractionErrorCode.TypeIncoherenceDansTacheMultiple`
  (Domain et Application) sont mis à jour pour utiliser le nouveau nom — recherche exhaustive
  obligatoire avant de considérer ce sous-ticket terminé, un renommage partiel casserait la build.
- [ ] Aucun changement de comportement observable (le message d'erreur textuel affiché à
  l'utilisateur, piloté par les ressources `.resx`, ne change pas — seul l'identifiant C# change).

**Hors périmètre de ce sous-ticket** : changer le texte du message affiché à l'utilisateur, changer
la logique de détection d'incohérence elle-même (Lot 032, non rouvert).

---

## Partie B — `ExcelETL.Application`

## 35.4. Lisibilité — extraction de la boucle de diffusion dans `ImportPipelineOrchestrator`

**Constat (audit Application §7)** : `ImportPipelineOrchestrator.Run` (~90 lignes) contient une
boucle de diffusion répétitive (`AddRange` + propagation `Localisation`/`Tableaux`/`Applications`/
`RepereParent`, lignes ~91-100) qui gagnerait en lisibilité en méthode nommée.

**Comportement attendu** : extraire cette boucle en méthode privée nommée
(`BroadcastEquipementContext` ou nom équivalent choisi par Claude Code), appelée au même point du
flux, sans changer l'ordre ni le contenu de la propagation.

**Tests** :
- [ ] Aucun nouveau test requis — les tests existants d'`ImportPipelineOrchestratorTests` doivent
  rester verts sans modification, preuve que le comportement observable (contenu final
  d'`ImportResult`) est inchangé.

---

## Partie C — `ExcelETL.BlazorAdmin`

## 35.5. Déduplication — icônes SVG et `BuildAvailableDuplicateName` entre pages de liste

**Constat (audit BlazorAdmin §4.1)** : les 3 constantes `PencilIconMarkup`/`CopyIconMarkup`/
`TrashIconMarkup` (SVG inline) et la méthode `BuildAvailableDuplicateName(string profileName)`
sont dupliquées à l'identique entre `ImportProfiles.razor` et `ExportProfiles.razor` — c'est
exactement le type de duplication qui a déjà causé une divergence silencieuse (icônes,
Lots V3/028) ayant nécessité le Lot 030 pour être corrigée.

**Comportement attendu** :
- Extraire les 3 constantes SVG dans un fichier statique partagé (ex.
  `Shared/AdminIconMarkup.cs`, classe statique avec les 3 constantes `public const string`),
  référencé par les deux pages à la place de leur déclaration locale.
- Factoriser `BuildAvailableDuplicateName` en une méthode générique paramétrée par
  `IReadOnlyList<string> existingNames` (plutôt que par le type concret de profil), placée dans
  un helper partagé (ex. `Shared/ProfileDuplicateNaming.cs`), appelée par les deux pages avec leur
  propre liste de noms existants.
- **Ne pas toucher** à la structure de templating tableau/carte (duplication fonctionnelle
  légitime selon Lot V2, hors périmètre de ce sous-ticket).

**Tests** :
- [ ] Tests existants (`ImportProfilesTests.cs`, `ExportProfilesTests.cs`) doivent rester verts
  sans modification — même rendu SVG, même comportement de résolution de collision de nom, seule
  la source du code change.
- [ ] Ajouter un test unitaire dédié au helper extrait (`BuildAvailableDuplicateName`/équivalent)
  si celui-ci n'était testé jusqu'ici qu'indirectement via le rendu de page — au minimum un cas
  "nom déjà pris → suffixe incrémenté" et un cas "nom disponible → inchangé", si absents des
  tests actuels.

---

## 35.6. Cohérence de convention — assertions par ID plutôt que par texte dans `NavMenuTests.cs`

**Constat (audit BlazorAdmin §3)** : 3 tests utilisent encore `cut.Markup.Should().Contain(...)`/
`NotContain(...)` sur du texte plutôt qu'une sélection par ID — `NavMenu_WhenNotAuthorized_DoesNotShowProfileLink`
(ligne ~109), et les deux tests de culture EN/FR (lignes ~39-40, ~50-51). C'est précisément la
classe de vérification qui avait laissé passer la régression du Lot L1 (texte "Journaux" toujours
présent, non détecté par une recherche de texte qui ne cherchait pas la bonne chose).

**Comportement attendu** :
- `NavMenu_WhenNotAuthorized_DoesNotShowProfileLink` : remplacer par une assertion d'absence DOM
  réelle sur `#nav-profile-link` (`cut.FindAll("#nav-profile-link").Should().BeEmpty()`), sur le
  modèle des autres tests d'absence déjà présents dans le même fichier.
- Tests de culture EN/FR (Register/Login) : si les liens correspondants n'ont pas d'ID stable
  aujourd'hui, en ajouter un (`#nav-register-link` ou équivalent cohérent avec `#nav-login-link`
  déjà existant) puis basculer l'assertion sur une sélection par ID plutôt que sur le texte.

**Tests** :
- [ ] Les 3 tests modifiés continuent de couvrir exactement le même scénario fonctionnel qu'avant
  (mêmes conditions d'authentification/culture simulées), seule la méthode d'assertion change.
- [ ] Si un ID est ajouté à un élément qui n'en avait pas, vérifier qu'aucun autre test du fichier
  ne sélectionnait déjà cet élément par un autre moyen (position, texte) qui deviendrait
  redondant — à nettoyer si trouvé.

---

## 35.7. Dette de test — couverture Modifier/Supprimer d'`ApplicationColumnDefinition`

**Constat (audit BlazorAdmin §6.2)** : `SheetGenerationRuleForm.razor:175-216` (Modifier/Supprimer
d'une `ApplicationColumnDefinition` déjà ajoutée, Lot U4) n'a aucun test — seul le chemin d'ajout
est couvert (`ExportProfileEditorTests.cs:382-431`).

**Comportement attendu** : aucun changement de code de production dans ce sous-ticket
(le comportement Modifier/Supprimer existe déjà et fonctionne selon l'audit — c'est un pur
comblement de trou de test, pas un correctif).

**Tests** (à écrire, sur le modèle des tests Modifier/Supprimer déjà existants pour
`PointColumnDefinition`/`UnconditionalColonneNames` dans le même fichier ou fichier voisin) :
- [ ] Modifier une `ApplicationColumnDefinition` déjà ajoutée → les champs se pré-remplissent
  correctement, la sauvegarde met à jour l'élément en place sans dupliquer.
- [ ] Supprimer une `ApplicationColumnDefinition` déjà ajoutée → l'élément disparaît de la liste
  rendue, aucun autre élément n'est affecté.
- [ ] Annuler une modification en cours → l'élément reste inchangé, le formulaire d'édition se
  referme.

**Si un bug réel est découvert en écrivant ces tests** (le comportement ne seraiat pas conforme à
ce que l'audit a supposé en lecture statique) : le signaler à Simon avant de corriger — ce
sous-ticket est cadré comme un comblement de couverture, pas comme un correctif, un écart de
comportement réel changerait sa priorité.

---

## 35.8. Cohérence — pattern modifier/supprimer-en-place pour `DefaultTableaux`/`DefaultApplicationNames`

**Constat (audit BlazorAdmin §5)** : le Lot W a introduit un pattern modifier/supprimer-en-place
pour les listes `UnconditionalColonneNames`/`ConditionalPointRule`, mais les listes
`DefaultTableaux`/`DefaultApplicationNames` (introduites au Lot U1) n'ont reçu que la capacité
d'ajout — pas de modification/suppression d'un élément déjà ajouté à la liste.

**Comportement attendu** : appliquer le même patron que le Lot W (boutons icône Modifier/Supprimer
par élément de liste, mêmes conventions `aria-label`/`title`, mêmes IDs stables suivant le schéma
déjà en place pour `UnconditionalColonneNames`) aux deux listes `DefaultTableaux`/
`DefaultApplicationNames`, sur le composant Blazor concerné (probablement
`ImportProfileEditor.razor`/`SheetRuleForm.razor` selon où ces listes sont rendues — à confirmer
en investigation).

**Tests** (sur le modèle exact des tests Modifier/Supprimer de `UnconditionalColonneNames` du
Lot W) :
- [ ] Modifier un élément déjà présent dans `DefaultTableaux`/`DefaultApplicationNames` → mise à
  jour en place, pas de doublon.
- [ ] Supprimer un élément déjà présent → disparaît de la liste rendue.
- [ ] Non-régression : le chemin d'ajout existant (Lot U1) reste fonctionnel et testé tel quel.

**Hors périmètre de ce sous-ticket** : toute nouvelle validation métier sur le contenu de ces
listes (Domain, non rouvert) ; extension de ce pattern à d'autres listes non citées par l'audit.

---

## Hors périmètre explicite de ce lot (traité séparément)

- **Validation `POST /api/oxo/process`** (paramètres de profil absents du multipart, fichier
  malformé, fichier vide non testé côté HTTP) — impact réel, ticket dédié séparé.
- **Parité boutons Modifier/Supprimer de carte de règle de feuille** (Import en texte brut,
  Export en icône) — impact réel/produit, ticket dédié séparé.
- **`ProcessOxoFileService`, double mécanisme d'archivage** (Lot K + Lot 034) — dépendance croisée
  hors périmètre d'un lot polish, à traiter une fois le test d'intégration WebAPI concerné
  ré-évalué, pas à la légère.
- **`DirectCell` (Domain), code jamais construit** — nécessite l'arbitrage de Simon (code mort à
  supprimer, ou brique préparée pour un usage futur) avant toute action.
- **`IWorkbookReader.SheetExists` jamais appelée par les 5 services d'extraction** — nécessite une
  vérification côté Infrastructure/WebAPI avant de conclure à du code mort, non fait dans cet
  audit Application.
- **`legacy/ExcelProcessingClientService.Tests` hors `ExcelETL.slnx`** — déjà résolu selon
  `etat-avancement-global-2026-07-25.md` §6.1, à confirmer en 35.0 plutôt qu'à retraiter ici.
- **Mise à jour du template `audit-qualite-code-TEMPLATE.md`** (référence obsolète au typed
  HttpClient de `/upload-test`, supprimé au Lot K4) — action documentaire, pas un ticket TDD,
  traitée directement par Claude AI hors de ce document.

---

## Ordre recommandé

1. **35.0** (investigation, conditionne la validité des localisations citées par les autres
   sous-tickets)
2. **35.1, 35.2, 35.3, 35.4** (Domain/Application — indépendants entre eux, aucune dépendance sur
   la partie BlazorAdmin, peuvent être livrés en parallèle ou dans n'importe quel ordre)
3. **35.5, 35.6, 35.7, 35.8** (BlazorAdmin — indépendants entre eux, peuvent suivre en parallèle
   des points 1-4 ci-dessus)

## Note d'efficacité d'implémentation

- Ce lot est délibérément composé de sous-tickets **indépendants et à faible risque** (aucun ne
  change de comportement observable, sauf 35.7/35.8 qui ajoutent une capacité déjà présente
  ailleurs dans le projet sous une forme quasi identique — donc un risque d'implémentation
  minimal, le patron à suivre existe déjà en exemple dans le code).
- Pour 35.1/35.2/35.3/35.4 (purs refactos à comportement figé) : la preuve de non-régression est
  la suite de tests **existante** qui reste verte sans modification — ne pas écrire de nouveau
  test pour ces 4 sous-tickets sauf si un test référence directement un identifiant renommé
  (35.3) ou une constante déplacée (35.2), auquel cas seule la référence change, pas l'assertion.
- Pour 35.5/35.6/35.7/35.8, réutiliser strictement le patron déjà présent ailleurs dans le projet
  (respectivement : structure `Shared/` existante si elle existe déjà, tests d'absence DOM déjà
  majoritaires dans `NavMenuTests.cs`, tests Modifier/Supprimer déjà écrits pour
  `PointColumnDefinition`, pattern du Lot W) plutôt que d'en concevoir un nouveau.
- Aucun sous-ticket de ce lot ne nécessite de toucher aux fixtures Excel réelles (C7401/D8570/
  G6306B) ni à une migration EF Core.

---

## Check-list de clôture du lot

- [ ] Les 813 (ou nombre réel confirmé en 35.0) tests existants avant ce lot restent tous verts.
- [ ] Aucun sous-ticket n'a changé un message d'erreur visible utilisateur, un contrat API, ou une
  règle de validation métier.
- [ ] Le template `audit-qualite-code-TEMPLATE.md` est mis à jour pour retirer la référence
  obsolète au typed HttpClient de `/upload-test` (action documentaire, hors TDD).
