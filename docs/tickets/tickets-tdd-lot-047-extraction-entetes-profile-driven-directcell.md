# Tickets TDD — Lot 047 : extraction des cellules d'en-tête profile-driven (`DirectCell`, moteur)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Après le lot 046
(`tickets-tdd-lot-046-nettoyage-filestorageservice-sheetexists.md`). Dérivé de la spec de conception
`spec-migration-entetes-profile-driven-directcell.md` (décisions figées avec Simon le 27/07).*

**Objet** : rendre profile-driven les lectures de cellules d'en-tête aujourd'hui codées en dur dans
les services Application (coordonnées `M2:O2`/`P2:Q2`/`R2:T2` de PROCEDURE, écho `N6` d'AUTRES
JOINTS TOUCHES/DIVERS), en câblant enfin la primitive `DirectCell`. **Lot « moteur », sans UI** :
domaine + résolveur + persistance + migration + seed + bascule des services + non-régression. L'UI
d'édition de ces règles fait l'objet du **lot 048** (dépend du modèle stabilisé ici).

**Invariant central, non négociable** : les 3 fixtures réelles (C7401, D8570, G6306B) produisent un
résultat d'extraction **identique** avant/après. Ce lot ne change **aucune valeur extraite** — il
déplace l'origine de la configuration (code → profil seedé). Toute divergence sur une valeur
d'en-tête est un échec du lot, pas un ajustement.

**Modèle retenu (spec §3, rappel)** — sur chaque `SheetExtractionRule`, deux collections plates :
- `HeaderFieldRule(Name, DirectCell Cell, bool StripReperePrefix = false, string? DateFormat = null)`
  — lecture directe d'une cellule d'en-tête + transformations minimales (retrait du `ReperePrefix`
  du profil ; reformatage de date de sortie).
- `HeaderCompositeRule(Name, string Template)` — champ dérivé d'un gabarit texte à placeholders
  nommés (`"Rév {revision} du {dateRev}"`), chaque `{placeholder}` référençant le `Name` d'un
  `HeaderFieldRule` de la même feuille.

Pas de système de transformation général : volontairement limité à ces trois besoins concrets
(préfixe, format de date, gabarit). L'arbre `TextTransform` récursif n'est **plus utilisé** par ce
modèle mais **n'est pas supprimé** par ce lot (nettoyage séparé — hors périmètre).

**Conventions déjà en place à respecter** :
- Clean Architecture / Onion stricte : `HeaderFieldRule`/`HeaderCompositeRule`/`DirectCell` dans
  Domain (zéro dépendance) ; le résolveur d'en-tête dans Application (via `IWorkbookReader`, jamais
  ClosedXML directement) ; EF Core/config/migration dans Infrastructure uniquement.
- Domain à validation stricte au constructeur (comme le reste du modèle) : `Name` non vide,
  `DirectCell` valide (format de plage), `Template` non vide et placeholders référençant des noms
  existants — `DomainValidationException` + `DomainErrorCode` dédié, même patron que les primitives
  existantes.
- Persistance EF Core : `IDbContextFactory<T>`, config dédiée dans
  `Persistence/Configurations/`, migration associée ; tests repository sur EF Core InMemory réel,
  jamais mocké au niveau `DbContext`.
- xUnit 2.9.3 + FluentAssertions 7.x (jamais v8+) + Moq ; tests d'intégration d'extraction contre
  les 3 fixtures réelles (patron déjà en place dans `ExcelETL.Infrastructure.Tests`).
- Garde-fou anti-hardcoding : tester avec deux profils portant des coordonnées/gabarits différents
  que le service restitue bien la valeur du profil (même patron que le test `EquipementTypeElementNom`
  du Lot C1 — à ne jamais laisser redevenir une constante de service).
- Strict Red-Green-Refactor : test qui échoue d'abord, toujours.

---

## Hors périmètre explicite de ce lot

- **UI d'édition** des règles d'en-tête (éditeur de profil Blazor) — c'est le **lot 048**.
- Suppression des types `TextTransform` (`Concat`/`Literal`/`FieldRef`/`SubstringAfter`/`RawValue`)
  devenus inutilisés — nettoyage séparé, à décider une fois leur non-usage confirmé (même logique
  d'arbitrage que `DirectCell`). Ne pas les supprimer ici.
- Parsing d'un format de date d'**entrée** : seul le format de **sortie** (`DateFormat`) est
  configurable ; la source est supposée être une date (à confirmer en 47.0).
- Le mapping d'alias `MapTypeTacheMultipleAlias` (`"MAD"→"TM_PROC_MAD"`, audit Application §2) —
  autre hardcode distinct, non couvert.
- Toute modification d'une valeur extraite — invariant de non-régression.
- Généralisation à d'autres transformations que préfixe/date/gabarit — YAGNI, aucune demande.

---

## 47.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `ProcedureExtractionService` : coordonnées exactes actuellement en dur (`M2:O2` pour
  `nomMAD`, `P2:Q2`/`R2:T2` pour la Désignation), logique de retrait du préfixe repère (confirmer
  qu'elle utilise bien le `ReperePrefix` du profil), et construction de la Désignation (gabarit
  littéral + reformatage de date).
- [ ] **Confirmer la nature réelle de la cellule de date** (`R2:T2`) : vraie date Excel (`DateTime`
  via `IWorkbookReader`) que l'on reformate, ou texte déjà formaté ? Conditionne le comportement de
  `DateFormat` (spec §5). Si c'est du texte, remonter le point avant d'implémenter `DateFormat`.
- [ ] Lire le(s) service(s) AUTRES JOINTS TOUCHES / DIVERS : usage exact de l'écho de repère `N6`.
- [ ] Lire la forme actuelle de `SheetExtractionRule` (Domain) et sa config EF Core
  (`ImportProfileSheetRules` + tables filles) pour savoir où rattacher les deux nouvelles collections
  filles et sur quel patron écrire la migration.
- [ ] Lire `DefaultProfileSeeder` : comment les `SheetExtractionRule` sont seedées, pour y ajouter
  les règles d'en-tête dans le même style (valeurs littérales actuelles).
- [ ] Relever, depuis les tests d'intégration existants sur les 3 fixtures, les **valeurs exactes
  attendues** de `nomMAD`, `Designation` et de l'écho de repère — elles deviennent les assertions de
  non-régression de 47.6.

---

## 47.1. Domain — `HeaderFieldRule` / `HeaderCompositeRule` sur `SheetExtractionRule`

**Comportement attendu** : ajout des deux types plats (spec §3) et de leurs collections sur
`SheetExtractionRule`. `DirectCell` est désormais référencé par `HeaderFieldRule`. Validation stricte
au constructeur.

**Tests** (xUnit, Domain, zéro dépendance) :
- Construction valide d'un `HeaderFieldRule` (avec/ sans `StripReperePrefix`, avec/ sans
  `DateFormat`) → propriétés assignées, égalité structurelle du record.
- `Name` vide/blanc, `DirectCell` de plage invalide → `DomainValidationException` + code dédié.
- Construction valide d'un `HeaderCompositeRule` ; `Template` vide → exception.
- Validation des placeholders : un `HeaderCompositeRule` dont le `Template` référence un `{nom}`
  absent des `HeaderFieldRule` de la même feuille → `DomainValidationException` dédiée (décider en
  47.1 si la validation croisée vit sur `SheetExtractionRule` à la construction, ou au niveau du
  résolveur 47.2 — documenter le choix ; recommandation : au niveau `SheetExtractionRule`, cohérent
  avec la validation croisée déjà faite sur les autres agrégats).
- `SheetExtractionRule` : construction avec 0..n règles d'en-tête (rétrocompatibilité — une feuille
  sans en-tête reste valide, listes vides par défaut, non-régression des feuilles isolement).

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/` (+ miroir tests).

---

## 47.2. Application — résolveur d'en-tête

**Comportement attendu** : un composant Application qui, pour une `SheetExtractionRule` et un
`IWorkbookReader`, produit les valeurs d'en-tête résolues :
1. lit chaque `HeaderFieldRule.Cell` via `IWorkbookReader` ;
2. applique `StripReperePrefix` (retrait du `ReperePrefix` du profil) puis `DateFormat` (reformatage
   de la date de sortie) si renseignés ;
3. résout chaque `HeaderCompositeRule.Template` en substituant les `{placeholder}` par les valeurs
   des `HeaderFieldRule` déjà résolus.
Erreur explicite (pas d'exception non typée) si un placeholder ne peut être résolu.

**Tests** (xUnit, Application, `Mock<IWorkbookReader>` fidèle au contrat) :
- Champ direct simple → valeur brute lue restituée.
- `StripReperePrefix = true` → préfixe du profil retiré (et seulement lui).
- `DateFormat = "dd/MM/yyyy"` → date reformatée exactement ; comportement documenté si la valeur
  n'est pas une date (selon conclusion 47.0).
- Gabarit `"Rév {revision} du {dateRev}"` → texte assemblé à partir des champs résolus.
- Placeholder inconnu → erreur typée, pas d'exception brute.

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/` (nom aligné sur les composants existants).

---

## 47.3. Infrastructure — persistance EF Core + migration

**Comportement attendu** : config EF Core des deux collections filles de `SheetExtractionRule`
(tables `ImportProfileSheetRuleHeaderFields` / `ImportProfileSheetRuleHeaderComposites`, noms à
aligner sur la convention existante `ImportProfileSheetRule*`), migration associée. `DirectCell`
persisté à plat (feuille + plage) ; `Template` en colonne texte.

**Tests** (xUnit, EF Core InMemory réel) :
- Round-trip complet d'un `SheetExtractionRule` portant des règles d'en-tête (directes + composées) →
  relu à l'identique, listes vides relues comme vides.
- Test de migration : colonnes/tables présentes ; un profil existant sans règle d'en-tête reste
  valide (collections vides), non-régression des profils déjà seedés.

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Configurations/` + migration.

---

## 47.4. Infrastructure — extension du `DefaultProfileSeeder`

**Comportement attendu** : le seed reproduit **exactement** la configuration actuellement en dur —
PROCEDURE : `nomMAD` (`M2:O2`, `StripReperePrefix = true`), `revision` (`P2:Q2`), `dateRev`
(`R2:T2`, `DateFormat = "dd/MM/yyyy"`), composite `Designation` (`"Rév {revision} du {dateRev}"`) ;
AJT/DIVERS : écho de repère (`N6`). Aucune valeur inventée : ce sont les coordonnées/gabarits
existants, simplement déplacés dans le seed.

**Tests** (xUnit) :
- Le profil seedé par défaut contient les règles d'en-tête attendues, par feuille, avec les valeurs
  ci-dessus (assertions explicites sur coordonnées, flags, gabarit).

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs`.

---

## 47.5. Application — bascule des services vers le profil (anti-hardcoding)

**Comportement attendu** : `ProcedureExtractionService` et le(s) service(s) AJT/DIVERS n'utilisent
plus de coordonnée/gabarit en dur : ils obtiennent les valeurs d'en-tête via le résolveur (47.2)
appliqué aux règles de la `SheetExtractionRule` du profil actif. Suppression des constantes
correspondantes.

**Tests** (xUnit, `Mock<IWorkbookReader>`) :
- Garde-fou anti-hardcoding : deux profils avec des coordonnées d'en-tête **différentes** → le
  service restitue la valeur dictée par chaque profil (jamais une constante). Même patron que le test
  `EquipementTypeElementNom` du Lot C1.
- Même exigence pour le gabarit de Désignation (deux profils, deux gabarits → deux résultats).
- Non-régression des tests unitaires existants de ces services (valeurs inchangées avec le profil
  équivalent à l'ancien hardcode).

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/...` (+ miroir tests).

---

## 47.6. Non-régression d'intégration sur les 3 fixtures réelles

**Comportement attendu** : avec le profil **seedé par défaut** (47.4), l'extraction des 3 fixtures
produit exactement les mêmes `nomMAD`, `Designation` et écho de repère qu'avant le lot.

**Tests** (xUnit, intégration, fixtures réelles — patron `ExcelETL.Infrastructure.Tests`) :
- C7401, D8570 (dont cas VANNE non bloquant), G6306B → valeurs d'en-tête identiques aux valeurs
  relevées en 47.0. C'est l'assertion qui garantit que le déplacement code → profil n'a rien cassé.
- La suite d'intégration d'extraction existante reste intégralement verte, sans modification de ses
  assertions.

---

## Ordre recommandé

1. **47.0** (investigation — verrouille la nature de la date et les valeurs de non-régression)
2. **47.1** (domaine — base de tout le reste)
3. **47.2** (résolveur Application — pur, testable avec `Mock<IWorkbookReader>`)
4. **47.3** (EF Core + migration) puis **47.4** (seeder — dépend de 47.1/47.3)
5. **47.5** (bascule des services + anti-hardcoding — dépend de 47.2 et du profil seedé)
6. **47.6** (non-régression fixtures — dernier, valide l'invariant central)

## Note d'efficacité d'implémentation (Claude Code)

- **47.0 est réellement gating** : la nature de la cellule de date change le comportement de
  `DateFormat`, et les valeurs relevées deviennent les assertions de 47.6. Ne pas coder avant.
- **47.1 → 47.2 → 47.5** est la colonne vertébrale ; 47.3/47.4 (persistance/seed) peuvent avancer en
  parallèle dès que 47.1 compile.
- **Invariant de non-régression au centre** : à chaque étape, se demander « une valeur extraite
  change-t-elle ? » — la réponse doit toujours être non. 47.6 est le filet final, mais l'esprit doit
  guider tout le lot.
- Ne pas céder à la tentation d'un système de transformation générique « tant qu'on y est » : le
  modèle plat (préfixe / date / gabarit) est délibérément limité, c'est ce qui le garde persistable
  et éditable (lot 048).
- Ne pas supprimer les types `TextTransform` devenus inutilisés dans ce lot — périmètre séparé.
