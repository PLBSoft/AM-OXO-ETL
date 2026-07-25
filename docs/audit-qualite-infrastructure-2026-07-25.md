# Demande à Claude Code — Audit qualité / refacto : `ExcelETL.Infrastructure`

## Métadonnées de la demande

- **Date de la demande** : 2026-07-25
- **Demandeur** : Simon
- **Contexte / déclencheur** : pause entre le Lot K/L/M et la reprise active — audit exécuté en
  parallèle avec 4 autres audits (Domain, Application, WebAPI, BlazorAdmin).
- **Périmètre exact** : projet `ExcelETL.Infrastructure` uniquement (EF Core, ClosedXML,
  ASP.NET Core Identity vivent ici et seulement ici) — repositories, `DbContext`,
  configurations EF (Fluent API), lecteurs/écrivains ClosedXML
  (`ClosedXmlWorkbookReader`/`ClosedXmlGeneratorService` et équivalents), seeders
  (`IdentitySeeder`, `DefaultProfileSeeder`), migrations. Ne pas lire Domain, Application,
  WebAPI, BlazorAdmin.
- **Version/commit de référence** : `main`, dernier commit connu `d018a90` (2026-07-24,
  812/812 tests slnx verts) — Claude Code confirme le commit exact réellement audité. Noter
  explicitement si la migration `20260724005133_AddTableauxApplicationsToProfiles` est bien
  présente et cohérente dans l'historique de migrations au moment de l'audit (point de
  vigilance déjà identifié le 24/07, pas encore appliqué sur toutes les bases locales).

## Cadrage du périmètre

Cadrage retenu : **par projet** (`ExcelETL.Infrastructure`). C'est la seule couche autorisée à
connaître EF Core/ClosedXML/Identity — la vigilance principale porte sur les fuites
d'implémentation (types EF Core, ClosedXML qui remonteraient dans une interface exposée aux
couches supérieures) et sur le respect strict du pattern `IDbContextFactory` déjà acté (pas de
`DbContext` scoped partagé).

## Consigne pour Claude Code

Tu es Claude Code et tu as accès au repository de la solution. Produis un document Markdown
intitulé **"Audit qualité — ExcelETL.Infrastructure"**, factuel, basé sur une lecture réelle du
code (pas de suppositions), destiné à Claude AI dans une autre session pour trier et
prioriser.

**Reste dans `ExcelETL.Infrastructure`.** Si un problème implique une autre couche, le noter en
une ligne dans "Hors périmètre — observé en passant" sans aller lire le code concerné.

### Grille de critères à évaluer (répondre à chaque point, même "RAS")

1. **Respect de Clean Architecture / Onion**
   - Vérifier qu'aucun type concret EF Core/ClosedXML ne fuit à travers une interface consommée
     par Application/WebAPI/BlazorAdmin (les interfaces exposées doivent rester dans
     Domain/Application).
   - Confirmer strictement le pattern `IDbContextFactory<T>` injecté par repository, DbContext
     court ouvert par méthode — aucune classe Unit-of-Work, aucun `DbContext` scoped partagé,
     y compris côté WebAPI (choix délibéré de cohérence même si WebAPI pourrait tolérer scoped).

2. **Règles métier câblées en dur vs profile-driven**
   - Vérifier qu'aucune règle métier (extraction/génération) n'a été dupliquée ou réimplémentée
     ici alors qu'elle devrait rester dans Application — l'Infrastructure ne doit porter que la
     mécanique I/O, pas la logique métier.

3. **Duplication**
   - Logique répétée entre repositories similaires (import/export) qui pourrait être factorisée
     via un repository générique sans perdre en clarté — attention : un repository générique
     n'est pas forcément souhaitable si les besoins réels divergent, à documenter si écarté.

4. **Cohérence des conventions déjà actées**
   - Stratégie de nommage des migrations, cohérence des configurations Fluent API entre
     entités, idempotence des seeders (`IdentitySeeder`, `DefaultProfileSeeder`) — Guid stables
     plutôt que lookup par nom, pas d'écrasement de données modifiées par un admin.

5. **Dette de test**
   - Zones avec couverture plus faible que la moyenne (121 tests sur ce projet au 24/07).
   - Confirmer l'usage de EF Core InMemory pour les tests de repository (jamais de mock au
     niveau DbContext) — signaler tout écart à cette règle déjà actée.
   - Statut du projet `Legacy.ExcelProcessingClientService.Tests` (15 tests, toujours hors
     `ExcelETL.slnx` au 24/07) — confirmer si ce trou de couverture CI est toujours d'actualité.

6. **Gestion des erreurs et logs**
   - Cohérence avec Serilog (sinks Console + MSSqlServer, table `SystemLogs` partagée
     WebAPI/BlazorAdmin) — absence de mécanisme de persistance parallèle non documenté par
     ticket.

7. **Lisibilité / complexité**
   - Repositories ou services d'infrastructure significativement plus complexes que leurs pairs
     sans raison technique documentée.

### Format de sortie attendu pour chaque point relevé

Pour chaque problème : **localisation**, **constat factuel**, **impact estimé**, **refacto
envisageable** (non implémentée). Terminer par **"Non couvert / incertain"**.

## Nommage du fichier de sortie

`audit-qualite-infrastructure-AAAA-MM-JJ.md` (instantané daté, catégorie 2 — jamais mis à jour
en place).

## Ce que ce document ne déclenche pas

Aucun refacto listé n'est engagé avant relecture/priorisation par Claude AI, validation
explicite de Simon, puis ticket TDD dédié.
