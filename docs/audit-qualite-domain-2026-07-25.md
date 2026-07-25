# Demande à Claude Code — Audit qualité / refacto : `ExcelETL.Domain`

## Métadonnées de la demande

- **Date de la demande** : 2026-07-25
- **Demandeur** : Simon
- **Contexte / déclencheur** : pause entre le Lot K/L/M et la reprise active — première passe
  d'audit qualité par périmètre, exécutée en parallèle avec 4 autres audits sur les autres
  projets de la solution.
- **Périmètre exact** : projet `ExcelETL.Domain` uniquement (zéro `PackageReference`, zéro
  `ProjectReference` — cœur du modèle métier : `ImportProfile`, `ExportProfile`, pivots,
  `SheetExtractionRule`/`SheetGenerationRule`, `ConditionalPointRule`,
  `UnconditionalColonneNames`, `PivotFieldRef`, etc.). Ne pas lire `ExcelETL.Application` ni
  les autres projets, même si un point y fait écho — le noter en une ligne dans "Hors
  périmètre — observé en passant" si nécessaire.
- **Version/commit de référence** : `main`, commit de référence le plus récent connu à ce jour
  est `d018a90` (2026-07-24, 812/812 tests slnx verts) — Claude Code confirme le commit exact
  réellement audité en tête du rapport produit.

## Cadrage du périmètre

Cadrage retenu : **par projet** (`ExcelETL.Domain`). Ce projet porte le modèle de domaine —
c'est le périmètre le plus sensible à un audit soigné, puisque toute règle métier câblée en dur
ailleurs (Application/Infrastructure) qui aurait dû vivre ici sera invisible depuis ce seul
projet ; signaler ce cas comme absence plutôt que comme silence.

## Consigne pour Claude Code

Tu es Claude Code et tu as accès au repository de la solution. Produis un document Markdown
intitulé **"Audit qualité — ExcelETL.Domain"**, factuel, basé sur une lecture réelle du code
(pas de suppositions), destiné à être lu par Claude AI dans une autre session (sans accès au
code) pour trier et prioriser.

**Reste dans `ExcelETL.Domain`.** Si un problème observé implique une autre couche (ex. une
règle qui devrait être dans le Domain mais est câblée ailleurs), le noter en une ligne dans
"Hors périmètre — observé en passant" sans aller lire le code concerné.

### Grille de critères à évaluer (répondre à chaque point, même "RAS")

1. **Respect de Clean Architecture / Onion**
   - Confirmer l'absence totale de `PackageReference`/`ProjectReference` (contrainte
     architecturale non-négociable du projet) — signaler tout écart comme critique, pas
     cosmétique.
   - Toute logique qui dépend implicitement d'un détail d'infrastructure (ex. format Excel,
     ClosedXML) sans abstraction propre.

2. **Règles métier câblées en dur vs profile-driven**
   - Vérifier qu'aucune règle d'extraction/génération n'est en dur dans une entité de domaine
     alors qu'elle devrait être portée par `ImportProfile`/`ExportProfile` et leurs collections
     de règles.
   - Cas déjà actés comme exceptions volontaires côté Application (PROCEDURE, DIVERS) : si leur
     pendant existe côté Domain, le signaler comme conforme, pas comme un défaut.

3. **Duplication**
   - Doublons entre pivots (`EquipementPivot`/`IsolementPivot`/`PivotSource`/`TacheMultiplePivot`)
     ou entre règles similaires (`ConditionalPointRule` vs `UnconditionalColonneNames`) qui
     pourraient être factorisés sans perdre en clarté métier.

4. **Cohérence des conventions déjà actées**
   - Nommage des entités/value objects, cohérence avec le vocabulaire métier déjà documenté
     (glossaire EF6 legacy si des correspondances existent).

5. **Dette de test**
   - Zones du Domain avec couverture visiblement plus faible que la moyenne (264 tests sur ce
     projet au 24/07 — signaler tout écart net entre entités bien couvertes et entités
     peu couvertes).
   - Tests qui testent l'implémentation plutôt que l'invariant métier.

6. **Gestion des erreurs**
   - Cohérence des types d'erreur/validation métier (`ExtractionErrorCode` et équivalents) —
     absence de logique de validation qui devrait être une garde de constructeur/invariant du
     domaine et ne l'est pas.

7. **Lisibilité / complexité**
   - Entités ou value objects significativement plus complexes que leurs équivalents dans le
     projet sans justification métier documentée.

### Format de sortie attendu pour chaque point relevé

Pour chaque problème : **localisation** (fichier + classe/méthode), **constat factuel**,
**impact estimé** (cosmétique / dette légère / risque réel), **refacto envisageable** (description
courte, non implémentée). Terminer par **"Non couvert / incertain"**.

## Nommage du fichier de sortie

`audit-qualite-domain-AAAA-MM-JJ.md` (instantané daté, catégorie 2 de
`convention-nommage-documents.md` — jamais mis à jour en place).

## Ce que ce document ne déclenche pas

Aucun refacto listé n'est engagé avant relecture/priorisation par Claude AI, validation
explicite de Simon, puis ticket TDD dédié.
