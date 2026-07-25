# Demande à Claude Code — Audit qualité / refacto : `ExcelETL.WebAPI`

## Métadonnées de la demande

- **Date de la demande** : 2026-07-25
- **Demandeur** : Simon
- **Contexte / déclencheur** : pause post Lot K (migration `POST /api/oxo/process`, retrait du
  pipeline POC) — audit exécuté en parallèle avec 4 autres audits (Domain, Application,
  Infrastructure, BlazorAdmin). Ce projet n'a reçu aucun changement depuis le Lot K (13/13
  tests, inchangés depuis le 22/07), c'est donc un bon candidat pour vérifier que le cutover K3/K4
  n'a rien laissé de partiel.
- **Périmètre exact** : projet `ExcelETL.WebAPI` uniquement — `OxoController`,
  `ProcessOxoFileService` (ou équivalent), `ApiKeyAuthenticationHandler`, `Program.cs`/DI. Ne
  pas lire Domain, Application, Infrastructure, BlazorAdmin.
- **Version/commit de référence** : `main`, dernier commit connu `d018a90` (2026-07-24,
  812/812 tests slnx verts) — Claude Code confirme le commit exact réellement audité.

## Cadrage du périmètre

Cadrage retenu : **par projet** (`ExcelETL.WebAPI`), avec un angle explicite sur le nettoyage
post-migration : confirmer qu'il ne reste aucune trace du pipeline POC
(`ExcelController`/`ClosedXmlExtractionService`) retiré par le Lot K4, au-delà des 3
commentaires obsolètes déjà corrigés le 23/07 (commit `596ea46`).

## Consigne pour Claude Code

Tu es Claude Code et tu as accès au repository de la solution. Produis un document Markdown
intitulé **"Audit qualité — ExcelETL.WebAPI"**, factuel, basé sur une lecture réelle du code
(pas de suppositions), destiné à Claude AI dans une autre session pour trier et prioriser.

**Reste dans `ExcelETL.WebAPI`.** Si un problème implique une autre couche, le noter en une
ligne dans "Hors périmètre — observé en passant" sans aller lire le code concerné.

### Grille de critères à évaluer (répondre à chaque point, même "RAS")

1. **Respect de Clean Architecture / Onion**
   - Confirmer que `ExcelETL.WebAPI` référence bien Application + Infrastructure et rien de
     plus problématique (pas de logique métier écrite directement dans un contrôleur).

2. **Résidus du pipeline POC retiré (Lot K4)**
   - Recherche exhaustive de toute référence résiduelle à `ExcelController`,
     `ClosedXmlExtractionService`, ou aux 4 tables retirées (`CellMappings`/
     `ExtractionHistories`/`SheetConfigs`/`ExtractionConfigs`) — configuration DI, routes,
     fichiers de test orphelins, appsettings, Swagger/OpenAPI.
   - Confirmer qu'aucune route ou middleware ne référence encore l'ancien pipeline, même de
     façon commentée ou désactivée.

3. **Contrat de `POST /api/oxo/process`**
   - Vérifier que `ImportProfileId`/`ExportProfileId` restent des paramètres explicites et
     obligatoires (pas de valeur par défaut implicite qui romprait la règle "aucun profil
     déduit implicitement").
   - Confirmer le comportement synchrone bout-en-bout (réception du fichier, génération,
     réponse HTTP) — pas de trace d'un mécanisme d'archivage/persistance non explicitement
     demandé par un ticket.

4. **Duplication**
   - Logique de contrôleur dupliquée qui pourrait être remontée dans un service Application
     déjà existant.

5. **Cohérence des conventions déjà actées**
   - `ApiKeyAuthenticationHandler` réutilisé tel quel, pas réinventé par route.

6. **Dette de test**
   - 13/13 tests inchangés depuis le 22/07 — vérifier si ce chiffre reflète une couverture
     suffisante du contrat `/api/oxo/process` (cas nominal, profils invalides, fichier
     malformé) ou un simple statu quo non retouché depuis la migration.
   - Statut du projet legacy `ExcelProcessingClientService.Tests` (15 tests, historiquement
     hors `ExcelETL.slnx`) — si ce projet teste un client HTTP consommant cette API, le
     signaler comme lié même si le code du projet legacy n'est pas dans le périmètre de cet
     audit.

7. **Gestion des erreurs et logs**
   - Codes de statut HTTP retournés pour les cas d'erreur (profil introuvable, fichier
     invalide) — cohérence avec `ProblemDetails` ou équivalent déjà en usage.
   - Cohérence avec Serilog (table `SystemLogs` partagée).

### Format de sortie attendu pour chaque point relevé

Pour chaque problème : **localisation**, **constat factuel**, **impact estimé**, **refacto
envisageable** (non implémentée). Terminer par **"Non couvert / incertain"**.

## Nommage du fichier de sortie

`audit-qualite-webapi-AAAA-MM-JJ.md` (instantané daté, catégorie 2 — jamais mis à jour en
place).

## Ce que ce document ne déclenche pas

Aucun refacto listé n'est engagé avant relecture/priorisation par Claude AI, validation
explicite de Simon, puis ticket TDD dédié.
