# Demande à Claude Code — Audit qualité / refacto : `ExcelETL.Application`

## Métadonnées de la demande

- **Date de la demande** : 2026-07-25
- **Demandeur** : Simon
- **Contexte / déclencheur** : pause entre le Lot K/L/M et la reprise active — audit exécuté en
  parallèle avec 4 autres audits (Domain, Infrastructure, WebAPI, BlazorAdmin).
- **Périmètre exact** : projet `ExcelETL.Application` uniquement (référence Domain +
  abstractions logging/localisation seulement, "framework-free" au sens strict) — services
  d'extraction (`ProcedureExtractionService`, `DiversExtractionService`, etc.), moteur de
  génération (`SheetGenerationEngine`), primitives d'extraction (`RowRangeLocator` et
  équivalents), sanitizers (`ExcelSheetNameSanitizer`). Ne pas lire Domain, Infrastructure,
  WebAPI, BlazorAdmin — noter en une ligne dans "Hors périmètre — observé en passant" si un
  point y fait écho.
- **Version/commit de référence** : `main`, dernier commit connu `d018a90` (2026-07-24,
  812/812 tests slnx verts) — Claude Code confirme le commit exact réellement audité.

## Cadrage du périmètre

Cadrage retenu : **par projet** (`ExcelETL.Application`). C'est la couche la plus dense en
règles métier opérationnelles (extraction, génération) — la vigilance principale porte sur la
frontière profile-driven vs câblé en dur, puisque plusieurs exceptions volontaires y sont déjà
actées (PROCEDURE, DIVERS) et ne doivent pas être re-signalées comme des défauts.

## Consigne pour Claude Code

Tu es Claude Code et tu as accès au repository de la solution. Produis un document Markdown
intitulé **"Audit qualité — ExcelETL.Application"**, factuel, basé sur une lecture réelle du
code (pas de suppositions), destiné à Claude AI dans une autre session pour trier et
prioriser.

**Reste dans `ExcelETL.Application`.** Si un problème implique une autre couche, le noter en
une ligne dans "Hors périmètre — observé en passant" sans aller lire le code concerné.

### Grille de critères à évaluer (répondre à chaque point, même "RAS")

1. **Respect de Clean Architecture / Onion**
   - Confirmer l'absence de dépendance à un framework concret (EF Core, ClosedXML, ASP.NET) —
     seules des abstractions doivent apparaître ici.
   - Toute logique qui présuppose un détail d'implémentation d'Infrastructure sans passer par
     une interface.

2. **Règles métier câblées en dur vs profile-driven**
   - Recenser tous les cas de logique hardcodée (comme PROCEDURE/DIVERS déjà actés) et vérifier
     qu'ils sont bien documentés comme exceptions délibérées quelque part (commentaire, doc) —
     sinon le signaler comme dette de documentation, pas de code.
   - Chercher d'éventuels **nouveaux** cas hardcodés introduits depuis, qui ne seraient pas
     déjà actés comme exceptions volontaires.

3. **Duplication**
   - Logique d'extraction dupliquée entre plusieurs `*ExtractionService`, ou logique de
     génération dupliquée entre règles de feuilles similaires dans `SheetGenerationEngine`.

4. **Cohérence des conventions déjà actées**
   - Cohérence de nommage entre services (`XxxExtractionService`, `XxxGenerationService`),
     structure de dossiers par feature vs par couche technique.

5. **Dette de test**
   - Zones avec couverture plus faible que la moyenne (121 tests sur ce projet au 24/07).
   - Tests qui masquent un comportement réel via des mocks trop permissifs (risque déjà vécu
     sur ce projet : le bug `IBrowserFile.OpenReadStream()` synchrone du 23/07 n'avait pas été
     détecté par les doubles de test — chercher d'autres zones où le double de test pourrait
     diverger silencieusement du comportement réel).

6. **Gestion des erreurs et logs**
   - Usage cohérent d'`ExtractionErrorCode` et équivalents ; absence d'exceptions génériques
     avalées silencieusement ; cohérence avec Serilog (pas de mécanisme de log parallèle).

7. **Lisibilité / complexité**
   - Services significativement plus complexes que leurs pairs, sans raison métier
     documentée — en particulier le moteur de génération, point de convergence de plusieurs
     règles.

### Format de sortie attendu pour chaque point relevé

Pour chaque problème : **localisation**, **constat factuel**, **impact estimé**, **refacto
envisageable** (non implémentée). Terminer par **"Non couvert / incertain"**.

## Nommage du fichier de sortie

`audit-qualite-application-AAAA-MM-JJ.md` (instantané daté, catégorie 2 — jamais mis à jour en
place).

## Ce que ce document ne déclenche pas

Aucun refacto listé n'est engagé avant relecture/priorisation par Claude AI, validation
explicite de Simon, puis ticket TDD dédié.
