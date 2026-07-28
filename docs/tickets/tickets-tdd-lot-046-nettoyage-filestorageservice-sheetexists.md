# Tickets TDD — Lot 046 : nettoyage de dette — retrait de `IFileStorageService` et de `IWorkbookReader.SheetExists`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Après le lot 045
(`tickets-tdd-lot-045-application-verrou-mot-de-passe-temporaire.md`).*

**Origine (état des lieux post-Lot 044, `etat-des-lieux-technique-2026-07-27.md` §5)** : deux
éléments de code sont confirmés morts ou redondants par lecture directe du code, et ont été
**arbitrés explicitement avec Simon (session du 27/07)** :

1. **Double mécanisme d'archivage dans `ProcessOxoFileService`.** Deux systèmes écrivent sur disque
   à chaque appel de `POST /api/oxo/process` : l'ancien `IFileStorageService` (Lot K — écrit
   uniquement le fichier **cible**, à plat, sans métadonnées ni horodatage structuré) et le
   mécanisme complet du Lot 034 (`IGeneratedFileWriter` + `IGeneratedFileArchiveStore` — source ET
   cible, arborescence `{yyyy}\{MM}\`, nom horodaté milliseconde, métadonnées SQL, page de
   consultation `/generated-files`). Le Lot 034 couvre strictement plus que le Lot K.
   **Décision Simon (27/07)** : le Lot 034 devient le mécanisme unique ; `IFileStorageService` est
   retiré du pipeline et supprimé (aucun consommateur externe ne lit la copie plate — confirmé par
   Simon).

2. **`IWorkbookReader.SheetExists`.** Déclarée sur l'interface, implémentée
   (`ClosedXmlWorkbookReader.SheetExists`), mais **appelée par aucun des 5 services d'extraction ni
   par aucun contrôleur/page** (état des lieux §5 ; audit Application du 25/07 §5). Morte en
   pratique. **Décision Simon (27/07)** : suppression après vérification qu'aucun appelant
   Infrastructure/WebAPI ne l'utilise et que le cas « feuille manquante » ne repose pas sur elle.

**Nature du lot** : pure réduction de dette. **Aucun comportement observable côté client ne doit
changer** — même réponse HTTP, mêmes fichiers archivés (ceux du Lot 034), même gestion du cas
« feuille manquante ». Le seul effet concret est la disparition de la copie plate redondante du
Lot K et la simplification de la surface d'`IWorkbookReader`.

**Conventions déjà en place à respecter** : xUnit 2.9.3 + FluentAssertions 7.x + Moq ;
`WebApplicationFactory` pour l'intégration Web API ; EF Core InMemory pour les repositories ; pas
de mock du filesystem au niveau I/O (dossier temporaire réel) ; Clean Architecture / Onion (aucune
fuite de type) ; Red-Green-Refactor strict — ici, le « test qui échoue d'abord » prend souvent la
forme d'un test caractérisant l'état cible (ex. « le service ne dépend plus de `IFileStorageService` »)
plus une non-régression exhaustive.

---

## Hors périmètre explicite de ce lot

- Le mécanisme d'archivage du Lot 034 lui-même (`IGeneratedFileWriter`/`IGeneratedFileArchiveStore`,
  page `/generated-files`) — **conservé tel quel**, non modifié.
- Toute évolution de la politique de rétention/purge des fichiers archivés (déjà hors périmètre du
  Lot 034, inchangé).
- Toute modification du contrat HTTP de `/api/oxo/process` (codes retour, corps de réponse).
- La migration des coordonnées d'en-tête vers le profil via `DirectCell` — sujet distinct, traité
  par sa propre spec (`spec-migration-entetes-profile-driven-directcell.md`) puis ses tickets ;
  **`DirectCell` n'est PAS supprimé par ce lot** (décision Simon : on le conserve et on le câble).
- Résorption de l'avertissement `CS8604` sur `_profileNamePendingDeletion` (état des lieux §3.2) —
  à corriger en passant lors d'un lot touchant `ImportProfiles.razor`/`ExportProfiles.razor`, pas ici.

---

## 46.0. Investigation préalable (obligatoire avant tout code)

- [ ] Localiser `IFileStorageService` et son implémentation (nom exact, projet, dossier) et
  **recenser tous ses appelants** par recherche exhaustive : confirmer que le seul point d'usage en
  production est `ProcessOxoFileService` (aucune page Blazor, aucun autre service, aucun contrôleur).
- [ ] Confirmer qu'aucun **test** ne s'appuie sur la sortie plate du Lot K comme vérité de
  référence (au-delà des tests propres du service, qui seront supprimés avec lui) — en particulier
  vérifier que les tests d'intégration `OxoProcessEndpoint*` n'assertent pas sur les fichiers plats.
- [ ] Repérer l'enregistrement DI de `IFileStorageService` dans `src/ExcelETL.WebAPI/Program.cs`
  (et `BlazorAdmin/Program.cs` s'il y figure) et la **section de configuration** éventuelle qu'il
  consomme (ex. un `RootPath` dédié dans `appsettings*.json`), pour un retrait complet sans résidu.
- [ ] Pour `SheetExists` : confirmer par recherche exhaustive l'absence d'appelant dans les 5
  services d'extraction, l'Infrastructure et la WebAPI. **Localiser le point de levée réel de
  `WorksheetNotFoundInWorkbookException`** (probablement `ClosedXmlWorkbookReader.ReadCellValue` sur
  feuille absente, pas via `SheetExists`) et vérifier que le retrait de `SheetExists` **ne modifie
  pas** ce comportement. Recenser les configurations de mock (`Mock<IWorkbookReader>`) qui poseraient
  `SheetExists` pour les nettoyer.
- [ ] Confirmer qu'aucune documentation vivante ne décrit `IFileStorageService`/`SheetExists` comme
  un contrat attendu (sinon, mettre à jour `CLAUDE.md` en conséquence dans le même lot).

---

## 46.1. Débranchement de `IFileStorageService` du pipeline

**Comportement attendu** : `ProcessOxoFileService` ne référence plus `IFileStorageService` (retrait
du paramètre de constructeur et de l'appel d'écriture cible plate). L'archivage Lot 034 reste
intégralement en place et inchangé. Le contrat HTTP et les fichiers du Lot 034 sont identiques.

**Tests** (xUnit + `WebApplicationFactory`) :
- Test caractérisant l'état cible : `ProcessOxoFileService` ne dépend plus de `IFileStorageService`
  (constructeur / dépendances vérifiées) — rouge tant que la dépendance existe.
- Requête réussie (fixture C7401) → réponse 200 inchangée, `GeneratedFileRecord` du Lot 034
  persisté, fichiers source+cible du Lot 034 présents — **aucune** copie plate écrite.
- Cas VANNE (D8570) et cas rejet (`Equipement is null`) → comportements Lot 034 strictement
  inchangés.
- Non-régression complète des tests d'intégration `OxoProcessEndpoint*` existants (aucune assertion
  modifiée, hors celles qui portaient spécifiquement sur la sortie plate — qui disparaissent avec le
  service).

---

## 46.2. Suppression de `IFileStorageService` (interface, implémentation, tests, DI, config)

**Comportement attendu** : une fois 46.1 vert et le service sans appelant, suppression de
l'interface, de son implémentation, de ses tests propres, de son enregistrement DI dans le(s)
`Program.cs`, et de toute section de configuration `appsettings*.json` qui lui était dédiée. Build
et suite complète verts.

**Tests** :
- Suite complète verte après suppression (aucun symbole orphelin, aucune référence résiduelle).
- Vérification légère DI (même style que les lots précédents) : le conteneur se construit sans
  `IFileStorageService`, l'archivage Lot 034 reste enregistré.

---

## 46.3. Retrait de `IWorkbookReader.SheetExists`

**Comportement attendu** : suppression de la méthode `SheetExists` de l'interface `IWorkbookReader`
et de son implémentation `ClosedXmlWorkbookReader.SheetExists`, **sous réserve** de la confirmation
46.0 (aucun appelant, cas « feuille manquante » indépendant de cette méthode). Nettoyage des
configurations de mock qui la posaient. Le comportement en cas de feuille absente
(`WorksheetNotFoundInWorkbookException` levée là où elle l'est déjà) est **inchangé**.

**Tests** (xUnit) :
- Suite complète verte après retrait (aucun appelant orphelin).
- Test de non-régression du cas « feuille manquante » : une lecture ciblant une feuille inexistante
  produit toujours le même comportement qu'avant (même exception au même point) — caractérise que le
  retrait de `SheetExists` n'a rien changé à ce chemin.
- Si 46.0 révèle un appelant réel de `SheetExists` : **ne pas supprimer**, documenter l'usage trouvé
  et clore ce sous-ticket comme « conservé, contrairement à l'hypothèse » (l'arbitrage Simon portait
  sur « supprimer *après vérif* »).

---

## Ordre recommandé

1. **46.0** (investigation — gating pour 46.2 et 46.3)
2. **46.1** (débranchement pipeline — non-régression d'abord)
3. **46.2** (suppression complète de `IFileStorageService`)
4. **46.3** (retrait de `SheetExists`, indépendant de 46.1/46.2 — peut être fait en parallèle)

## Note d'efficacité d'implémentation (Claude Code)

- Les deux nettoyages (46.1-46.2 d'une part, 46.3 d'autre part) sont **indépendants** : découpables
  en deux commits séparés (convention un commit par sous-ticket), livrables dans n'importe quel ordre.
- Ne pas profiter de ce lot pour toucher au mécanisme Lot 034 (« tant qu'on y est ») — le périmètre
  est strictement le retrait des deux éléments morts/redondants, la non-régression du reste est
  l'invariant central.
- `46.3` est un vrai « supprimer *après vérif* » : la vérification 46.0 est la partie qui compte, la
  suppression elle-même est triviale. Si le doute subsiste sur le point de levée de
  `WorksheetNotFoundInWorkbookException`, préférer conserver et documenter plutôt que supprimer à
  l'aveugle.
