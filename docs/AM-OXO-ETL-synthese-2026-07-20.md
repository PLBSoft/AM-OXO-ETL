# AM-OXO-ETL — Synthèse de relance (mise à jour 2026-07-20)

*Document à partager en début de nouvelle session (chat ou Claude Code) pour reprendre le
projet sans avoir à tout réexpliquer. Remplace la version précédente (rédigée avant le premier
audit de cohérence du 17/07). Reflète l'état des lieux au 20/07/2026, basé sur le second audit
de cohérence globale du 19/07/2026 (`docs/audit-coherence-globale-2026-07-19.md`, code lu
directement, 613/613 tests confirmés verts par exécution réelle) — si un audit plus récent
existe, il prévaut sur ce résumé.*

## Le projet en une phrase

Micro-service .NET 10 (Clean Architecture, EF Core, ASP.NET Core Web API + Blazor Web App) qui
décharge une application legacy (ASP.NET MVC5/.NET Framework 4.8, `AvancementRecette`) de
l'extraction de données depuis des fichiers Excel OXO fortement structurés, produit un objet
pivot exploitable, **et génère désormais un fichier Excel cible paramétrable à partir de ce
pivot** — l'écriture cible, hors périmètre au moment de la précédente synthèse, est maintenant
livrée (Lot I) avec son écran d'administration (Lot J).

## Documents de référence (catégorie "vivants", jamais datés)

- `etat-des-lieux-technique.md` — architecture générale, conventions de code et de test
- `glossaire-ef6-legacy-AMAR-ModelCF.md` — correspondance terme métier ↔ code legacy
- `spec-extraction-fichier-source-oxo.md` — spec d'extraction feuille par feuille (6 feuilles)
- `modele-domaine-import-profile.md` — modèle pivot, primitives, section §1.4 dédiée à
  `UnconditionalColonneNames` (ajoutée depuis la version précédente)
- `tickets-tdd-extraction.md` — Lots A-E (extraction), tous terminés
- `tickets-tdd-blazor-profil-import.md` — Lot F (F1/F2) + **F3 (édition de profil, nouveau)**,
  tous terminés — ce document **existe désormais réellement** (il avait été référencé mais
  jamais créé jusqu'au 18/07, écart comblé rétroactivement)
- `tickets-tdd-ecriture-fichier-cible.md` — **Lot I (nouveau)**, écriture du fichier Excel cible
  à partir du pivot, I1-I6 tous terminés
- `tickets-tdd-blazor-profil-export.md` — **Lot J (nouveau)**, écran Blazor du profil d'export,
  J1-J4 tous terminés, édition de profil incluse dès la livraison (contrairement à F1 à
  l'origine — écart corrigé avant même la livraison, voir "Décisions actées" ci-dessous)
- `tickets-tdd-corrections-audit-coherence.md` — 4 corrections issues du premier audit : **G1/G2
  (logging pipeline OXO) et G3 (config Serilog partagée entre hôtes) terminés** ; H1
  (`MaxRequestBodySize` doc) et H3 (doc logging WebAPI) terminés ; H2 comblé par la création de
  `tickets-tdd-blazor-profil-import.md`

## Instantanés d'état des lieux les plus récents (catégorie "datée")

- `audit-coherence-globale-2026-07-17.md` — premier audit, confirmait Lots A-E et F1/F2 conformes
- `etat-avancement-lot-j-blazor-profil-export-2026-07-18.md` — Lot J conforme
- `etat-avancement-lot-g-logging-oxo-2026-07-19.md` — G1/G2 conformes, **G3 non fait à cette
  date** (décision d'architecture jamais tranchée avant que G1/G2 ne démarrent)
- `audit-coherence-globale-2026-07-19.md` — **second audit, le plus à jour**, confirme tout ce
  qui précède + G3 (terminé depuis, via un nouveau projet `ExcelETL.Hosting` partagé) + F3,
  613/613 tests exécutés réellement et confirmés verts, **aucun écart bloquant**

## Ce qui est déjà tranché et ne devrait plus rouvrir de débat

- Tout ce qui était déjà acté dans la synthèse précédente (`TypeElement.Nom`, pas de fichier REL
  séparé, `"VANNE"` absent en avertissement non bloquant, `DEBUT`/`POINT FEU`, politique d'erreur)
- **Séparation stricte "colonnes descriptives" (sélecteur typé `PivotFieldRef`) vs "colonnes
  Points" (matching `ColonneNom`)** dans `ExportProfile` — deux primitives distinctes, jamais
  confondues (Lot I1)
- **Colonne cible non mappée = en-tête présent, cellule vide, jamais absente** — `Source = null`
  est un cas valide du domaine, pas une erreur (Lot I1)
- **Ensemble et ordre des colonnes Points figés dans le profil**, jamais déduits dynamiquement
  des données d'un run — évite un schéma de sortie instable d'un fichier à l'autre (Lot I1)
- **Aucune persistance dédiée type `ExtractionHistory` pour le logging OXO** — le pipeline OXO
  reste sans dépendance directe à Serilog (`ILogger<T>` abstrait uniquement), le binding réel se
  fait via une méthode d'extension partagée (`AddOxoHostLogging`, projet `ExcelETL.Hosting`)
  appelée par les deux hosts (WebAPI + BlazorAdmin) plutôt que dupliquée (Lot G3)
- **L'édition d'un profil existant (import et export) est une fonctionnalité normale, pas une
  simplification à écarter par défaut** — corrigé sur `ExportProfile` avant même la livraison de
  J, puis rattrapé rétroactivement sur `ImportProfile` via F3. Ne plus reproduire cette
  limitation par mimétisme sur un futur profil similaire.

## Ce qui reste ouvert, sans plan d'action figé

- **Format exact du fichier Excel cible** (`OXO_TRAME_IMPORT_MAD.xlsx`) — toujours non figé côté
  client : pas de feuille Tâches Multiples (le pivot `TacheMultiplePivot` existe et attend d'être
  consommé), colonnes descriptives non mappées encore nombreuses des deux côtés (Parents/Enfants)
- **Retrait du POC legacy** (`ExtractionConfig`/`Mappings.razor`/`UploadTest.razor`) — décision de
  dépréciation actée depuis longtemps, toujours pas exécutée
- **Exposition Web API M2M du pipeline OXO** — toujours hors périmètre, aucune route HTTP créée ;
  `ExcelETL.WebAPI/Controllers/` ne contient toujours que `ExcelController`/`HealthController`
  (protégé par API Key, mais celle-ci ne couvre que l'ancien pipeline POC)
- **Application effective des migrations EF sur une vraie base SQL Server** — toujours non
  vérifiable depuis le dépôt seul (`AddImportProfile` et `AddExportProfile`), nécessiterait un
  accès à l'environnement Windows Server 2022 cible

## Point mineur non bloquant, à corriger un jour sans urgence

- `ExcelETL.Hosting`/`ExcelETL.Hosting.Tests` (nouveau projet du Lot G3) absents de
  `ExcelETL.slnx` — sans impact aujourd'hui (pas de CI en place), mais à ajouter avant qu'une CI
  basée sur la solution ne soit configurée, pour éviter un trou de couverture silencieux
- `tickets-tdd-extraction.md` ne reflète pas encore le renommage
  `PlatinesExtractionService`→`UnconditionalIsolementSheetExtractionService` (cosmétique, hérité
  du premier audit, `CLAUDE.md` est déjà correct)

## Action recommandée avant toute nouvelle décision de fond

Contrairement au moment de la première synthèse, **la cohérence globale vient d'être vérifiée et
confirmée à jour** (19/07, 613/613 tests, aucun écart bloquant) — inutile de relancer un audit
complet dans l'immédiat. La prochaine vraie décision de fond, c'est le **format définitif du
fichier Excel cible côté client** : c'est la seule chose qui bloque encore un travail de fond
(compléter les colonnes non mappées, ajouter la feuille Tâches Multiples). Un nouvel audit
complet redeviendra utile soit avant d'attaquer ce chantier une fois le format figé, soit si
plusieurs nouveaux lots s'accumulent à nouveau sans vérification entre-temps.

## Modèle recommandé pour cette phase

Sonnet 5 reste approprié pour la suite (corrections mineures, documentation, petits tickets de
type édition/logging). Réserver un modèle de tier supérieur pour le jour où le format du fichier
cible sera figé côté client et où la feuille Tâches Multiples devra être ajoutée au modèle de
génération — potentiellement plus complexe si le client demande des règles de transformation
non triviales à ce moment-là.
