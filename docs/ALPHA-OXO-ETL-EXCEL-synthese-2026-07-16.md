# ALPHA - OXO ETL EXCEL — Synthèse de conception : Module Extraction

*Document de référence à tenir à jour au fil des échanges. Dernière mise à jour : 2026-07-16 — spec client feuille par feuille finalisée et validée, modèle de domaine figé, découpage en tickets TDD produit et prêt pour Claude Code.*

## 1. Contexte établi

- Architecture Clean Architecture / .NET 10 déjà posée, POC fonctionnel (Web API + Blazor Web App Server Interactivity)
- Fonctionnalités de base déjà en place : login Blazor (ASP.NET Core Identity / SQL Server), visualisation des logs
- Chantier en cours : le cœur métier — extraction de données Excel selon des **profils d'import configurables**
- Budget : 15 jours/homme pour ce module (hors autres lots, hors module aval de traitement)
- Fichier Excel source : 11 feuilles au total, 8-9 concernées par l'extraction
- Format du fichier source susceptible d'évoluer dans le temps → nécessité de plusieurs profils d'import coexistants
- Schéma de sortie (fichier plat, 4-5 feuilles) : **fixe et contractualisé** avec le module aval (hors scope ici) — le fichier d'entrée bouge, pas le format de sortie
- Contraintes techniques rappelées : pas de macros/chiffrement en entrée, ClosedXML obligatoire (licence OSS), CSV/ZIP bannis, 100% OSS (MIT/Apache 2.0)

## 2. Statut de l'existant — POC `ExtractionConfig`/`SheetConfig`/`CellMapping`

Un premier jet existe déjà dans le code (produit par Claude Code, milestone antérieur), documenté dans `etat-des-lieux-technique.md` : entités `ExtractionConfig`/`SheetConfig`/`CellMapping`, `IExtractionConfigRepository`, persistance EF Core.

**Statut clarifié** : c'est un **POC technique**, pas un modèle de domaine à respecter. Il valide uniquement la capacité à ouvrir un `.xlsx` (ClosedXML) et lire des cellules — le temps investi dessus est faible. `CellMapping` est strictement **"cellule → valeur brute → champ"**, sans aucune notion de transformation (pas de `SubstringAfter`/`Concat`/etc.) et sans notion de plage de lignes ou de portée/broadcast.

**Décision actée** : carte blanche pour les nouvelles spécifications du modèle pivot (`ImportProfile`, primitives, hiérarchie). Pas d'obligation de coller à l'existant à tout prix — le risque de complexifier pour réutiliser une brique POC coûterait plus cher (temps, tokens, dette) que de la remplacer proprement. Priorité : garder le projet **clean**, sans usine à gaz pour préserver de l'existant qui ne le mérite pas.

**Ce qui reste valable et à conserver** : le squelette projet/solution (Clean Architecture, câblage des dépendances Domain/Application/Infrastructure/WebAPI/BlazorAdmin), les conventions transverses déjà adoptées (repository via `IDbContextFactory`, exceptions typées avec `ErrorCode`, tests EF InMemory/Moq/bUnit selon le type de dépendance) — voir `etat-des-lieux-technique.md`. Seule la brique métier `ExtractionConfig`/`SheetConfig`/`CellMapping` elle-même est remise en question, pas le reste de l'architecture technique.

**Statut au 2026-07-16** : le modèle pivot cible est désormais **figé** (voir §4) — le verdict "entité par entité, à garder/étendre/remplacer" annoncé le 15/07 est tranché : l'ensemble `ExtractionConfig`/`SheetConfig`/`CellMapping` est **remplacé** par le nouveau modèle (`ImportProfile`/`SheetExtractionRule`/primitives), aucune brique du POC n'est réutilisée telle quelle.

## 3. Décisions d'architecture actées

| Sujet | Décision |
|---|---|
| Moteur d'extraction | Générique, à base de primitives composables finies (pas de scripting Roslyn/Lua/JS — trop complexe, trop cher à sécuriser et à exposer en UI, risque d'usine à gaz vu le budget) |
| Mapping de sortie | Peut rester typé/métier car le schéma cible est fixe |
| Persistance des profils | EF Core Code-First Fluent API, tables normalisées (auditables, requêtables) — **peut être différée** dans le séquencement des tickets (démarrer avec un profil codé en dur pour valider les règles métier plus vite, brancher la persistance ensuite) |
| Sélection du profil actif | Manuelle, dropdown "profil actif" à l'écran d'import — pas de détection automatique de version de fichier |
| UI de construction de profil (Blazor) | Saisie manuelle du nom de feuille + adresse de cellule (pas de grille interactive/scan de fichier) — justifié par le profil utilisateur : analyste métier technique, à l'aise avec les références de cellules Excel |
| Gestion d'erreurs | Techniques et précises assumées (ex : `Cellule C6 introuvable ou vide`) — pas de vulgarisation nécessaire |
| Test de profil | Bouton "tester sur fichier exemple" → exécution du pipeline → affichage JSON/tableau en lecture seule (pas de rendu Excel interactif riche, pour tenir le budget) |
| Stratégie de découverte du catalogue de primitives | Ne PAS figer le catalogue à l'avance — le construire feuille par feuille au fur et à mesure de l'analyse des specs réelles, pour éviter la sur-ingénierie. **Catalogue figé au 2026-07-16, voir §4** |
| **[NOUVEAU 2026-07-16]** Découplage extraction / écriture cible | Un objet pivot intermédiaire (Domain/Application, zéro dépendance ClosedXML) sépare l'extraction (peut démarrer immédiatement) de l'écriture du fichier Excel cible (bloquée sur le format exact, en attente du client). Cet objet pivot est directement réutilisable pour l'écran "tester profil" (JSON/tableau, déjà décidé ci-dessus) |
| **[NOUVEAU 2026-07-16]** Politique d'erreur | Au niveau Isolement/Point/TacheMultiple : extraire les blocs valides, signaler les blocs invalides dans un rapport groupé (toutes les erreurs du fichier, pas d'arrêt à la première). **Exception** : si l'Équipement parent (feuille PROCEDURE) est lui-même invalide, rejet du fichier entier (tout le reste en dépend) — voir `modele-domaine-import-profile-2026-07-16.md` §3 |
| **[NOUVEAU 2026-07-16]** Conditions de création de Points | Égalité/inégalité stricte suffit (`ConditionOperator.Equals`/`NotEquals`), pas de moteur de conditions plus riche pour l'instant |
| **[NOUVEAU 2026-07-16]** `TypeElement` — Equipement parent vs Isolement enfant | L'Équipement parent (feuille PROCEDURE) porte `TypeElement.Code` (`"MAD"` ou `"REL"` selon le dossier traité) ; les Isolements enfants portent `TypeElement.Nom` (valeurs données : `INSTRUMENTATION`, `ZERO ENERGIE`, `SOUPAPE`, `POINT FEU`...). Deux champs distincts, à ne pas confondre |
| **[NOUVEAU 2026-07-16]** Correspondance de nom introuvable côté legacy | Si un nom de `Colonne`/`TypeElement` produit par AM-OXO-ETL ne correspond à rien côté application legacy, le moteur d'import legacy affiche une erreur explicite dans son résultat — **ce n'est pas un problème pour AM-OXO-ETL**, le client reste responsable d'aligner ses données. S'applique notamment aux variantes `FIN` de PLATINES (volontairement exclues) et à l'orthographe `"POINT FEU"` (retenue telle quelle) |

## 4. Modèle pivot — figé (2026-07-16)

Le squelette provisoire esquissé dans les versions précédentes de ce document est remplacé par un modèle finalisé, détaillé intégralement dans **`modele-domaine-import-profile-2026-07-16.md`**. Résumé :

### Catalogue de primitives — 5, pas plus
1. **`DirectCell(sheet, range)`** — lecture directe d'une cellule/plage fusionnée
2. **`RepeatingBlockLocator`** — bloc répétitif générique (feuille, ligne de départ, pas, champ d'arrêt, liste de champs avec offsets de ligne/colonne) : couvre les 6 feuilles, y compris PROCEDURE (pas=1)
3. **Transformations de texte** — `RawValue`/`SubstringAfter(prefix)`/`Concat(parties)`
4. **`ConditionalPointRule`** — règle "si TypeElement = X alors créer Point Y", égalité/inégalité stricte
5. **Portée globale/broadcast** — une valeur extraite une fois (ex. `loc1`), appliquée à tous les enregistrements du run

### Objet pivot (résultat d'extraction, Domain/Application, zéro dépendance ClosedXML)
`EquipementPivot` / `IsolementPivot` / `PointPivot` / `TacheMultiplePivot`, agrégés dans un `ImportResult` qui porte aussi la liste des `ExtractionError` (rapport groupé). Ce modèle d'erreurs est une **deuxième exception documentée** au principe "pas de Result pattern générique" posé dans `etat-des-lieux-technique.md` §2 (la première étant `IdentityOperationResult`) — justifiée par un vrai besoin de rapport de traitement par lot, distinct de la validation d'invariants métier sur une entité.

### `ImportProfile` (persistance EF Core, cohérent avec le POC existant)
`ImportProfile` → `RepereePrefix` (paramétrable, défaut `"MAD-OXO-"`) → collection de `SheetExtractionRule` (nom de feuille paramétrable, `RepeatingBlockLocator`, liste de `ConditionalPointRule`).

## 5. Points en suspens — tous résolus au 2026-07-16

*Analyse initiale menée sur `Dossier_de_MaD_IDL_-_C7401.xlsx`, `Dossier_de_MaD_IDL_-_D8570_chgt_plateaux.xlsx`, `Dossier_de_MaD_IDL_-_G6306B_REV.xlsx` (2026-07-15), puis complétée et confirmée par la relecture de la spec client retravaillée (2026-07-16).*

### 5.1 Où se trouve la table qui génère les 1 à 100 BE ? — Élucidé, pas de lecture confirmés

Pas de table centrale unique. La donnée est répartie sur **5 feuilles-listes** (`ISOLEMENT`, `PLATINES`, `ORIFICES CAPACITES`, `AUTRES JOINTS TOUCHES`, `DIVERS`), chacune organisée en **blocs répétitifs de hauteur fixe**, confirmés par le client :

| Feuille | Pas de lecture (hauteur de bloc) |
|---|---|
| PROCEDURE (TachesMultiples) | 1 |
| ISOLEMENT | 7 |
| PLATINES | 8 |
| ORIFICES CAPACITES | 8 |
| AUTRES JOINTS TOUCHES | 7 *(l'estimation initiale de 3 lignes, déduite de la seule analyse structurelle des 3 fichiers, était incorrecte — 7 est la valeur confirmée par le client)* |
| DIVERS | 3 |

**Condition d'arrêt** : dès que la cellule "Identification" (ou "Action" pour PROCEDURE) du bloc en cours est vide, arrêt de la lecture — pas de déduction possible à partir du plus grand numéro de ligne pré-imprimé (celui-ci est fixe quel que soit le contenu réel, voir constat initial du 2026-07-15).

### 5.2 Représentation parent/enfant — Élucidé

Structure à deux niveaux : **MAD** (1 par fichier, feuille PROCEDURE, ex. `C7401`) → **BE individuel** (chaque bloc rempli dans les 5 feuilles-listes = un enregistrement enfant), avec son propre n°, son "identification" (tag technique) et sa "localisation" (texte libre).

### 5.3 Profondeur hiérarchique — Élucidé

2 niveaux confirmés (MAD → BE), pas de 3e niveau détecté à l'intérieur d'un bloc.

### 5.4 `loc1` — Résolu (2026-07-16)

L'anomalie initiale (cellule `DIVERS!C6` vide dans les 3 fichiers réels, en-tête de la feuille ne correspondant pas à l'exemple de spec) est close. **Confirmé par le client** : `loc1` (→ `BaseElement.Localisation.Loc1.Nom`) est lu en feuille `DIVERS`, cellule `B6:E6` (pas `C6`), et sa portée est bien globale — applicable à **tous** les Equipement et Isolement extraits du fichier.

## 6. Spécification complète feuille par feuille

La spécification détaillée des 6 feuilles source (PROCEDURE, ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS) — plages de cellules, règles métier, conditions de création de Points, primitives associées — vit désormais dans un document dédié : **`spec-extraction-fichier-source-oxo-2026-07-16.md`**. Ce document remplace l'exemple isolé de la feuille PROCEDURE qui figurait ici dans les versions précédentes de cette synthèse.

Points clés actés dans cette spec (au-delà de ce qui est déjà en §3/§5) :
- Règle `TacheMultiple.Ordre` / lignes de présentation : si la colonne `B` (à partir de B9, PROCEDURE) est vide, une `TacheMultiple` factice déjà validée est créée pour respecter la structure du fichier d'entrée — décision client assumée, câblée en dur pour la feuille PROCEDURE (pas généralisée en primitive)
- Variantes `DEB`/`FIN` de PLATINES : seules les variantes `DEBUT` sont couvertes, exclusion volontaire des variantes `FIN`
- Orthographe `"POINT FEU"` (pas `"POINT DE FEU"`) retenue pour la feuille DIVERS
- Cas symétrique `"REL"` pour l'Équipement parent : non hors scope, même mécanisme que MAD (`TypeElement.Code`), mais aucun fichier Excel exemple de dossier REL n'a encore été analysé — point de vigilance résiduel (voir §8)

## 7. Prochaines étapes

1. ~~**Client/utilisateur** : retravaille la spec client feuille par feuille...~~ — **Fait** : spec complète des 6 feuilles produite et validée avec le client (`spec-extraction-fichier-source-oxo-2026-07-16.md`).
2. ~~**Client** : trancher l'anomalie `loc1`/feuille `DIVERS`~~ — **Fait**, voir §5.4.
3. ~~**Claude** : catalogue final des primitives + modèle de domaine complet~~ — **Fait** : `modele-domaine-import-profile-2026-07-16.md`.
4. ~~**Claude** : découpage en tâches TDD~~ — **Fait** : `tickets-tdd-extraction-2026-07-16.md`, 5 lots (A: primitives Domain, B: moteur générique, C: 6 services par feuille, D: orchestrateur + tests d'intégration contre les 3 fichiers réels, E: Infrastructure ClosedXML/persistance).
5. **En cours** : transmission des documents et des tickets à Claude Code. Point opérationnel important — les documents markdown et les 3 fichiers Excel de fixtures doivent être **committés dans le repo Git** avant que Claude Code puisse s'appuyer dessus (il n'a accès qu'au code du dépôt, pas à l'historique de cette conversation).
6. **Client** : fournir le format exact du fichier Excel cible (5 feuilles) — ne bloque que le lot E (écriture du fichier cible), pas l'extraction (lots A-D), qui peut être développée et testée dès maintenant contre les 3 fichiers réels.
7. **Client (si possible)** : fournir un premier fichier Excel exemple d'un dossier **REL** (aucun n'a encore été analysé — voir §8), pour vérifier que sa structure suit les mêmes conventions que les fichiers MAD déjà audités.

## 8. À trancher plus tard (pas maintenant)

- Détail de l'écran Blazor de construction de profil (pas urgent tant que le modèle de domaine n'est pas persisté en base — voir séquencement §3)
- Format exact du fichier Excel cible (5 feuilles : Equipement MAD Parent, Isolements enfants, Points parent, Points enfants, Tâches multiples) — en attente du client, bloque uniquement l'écriture finale (lot E des tickets TDD)
- Structure d'un fichier Excel de dossier **REL** — aucun exemple disponible à ce jour ; le principe (`TypeElement.Code = "REL"`, même mécanisme que MAD) est acquis, mais rien ne garantit que la structure des feuilles/plages/pas de lecture soit identique tant qu'un exemple n'a pas été inspecté
- Découpage exact des couches Clean Architecture pour ce module (rappel, déjà largement précisé par le modèle de domaine et les tickets TDD) :
  - Domain : modèle de règles (`ImportProfile`, primitives d'extraction, règles de mapping, objet pivot)
  - Application : moteur d'exécution du pipeline + orchestration + abstraction `IWorkbookReader`
  - Infrastructure : lecture ClosedXML, persistance EF Core (différée, voir §3)
