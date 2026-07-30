# Demande à Claude Code — Audit qualité / refacto (périmètre ciblé)

> **Usage** : trame réutilisable, sur le même principe que
> `etat-des-lieux-technique-TEMPLATE.md`. Copier ce fichier, remplir les champs
> `[À COMPLETER]`, donner tel quel à Claude Code. **Ne jamais lancer cet audit
> sur "toute la solution" en une fois** — voir section "Cadrage du périmètre"
> ci-dessous, c'est la règle la plus importante de ce template.

---

## Métadonnées de la demande

- **Date de la demande** : [À COMPLETER]
- **Demandeur** : [À COMPLETER]
- **Contexte / déclencheur** : [À COMPLETER — ex. "pause entre le Lot K et le Lot L,
  avant de reprendre le développement actif"]
- **Périmètre exact** : [À COMPLETER — un seul projet, un seul dossier, ou une seule
  feature transversale (ex. "extraction OXO" à travers Domain/Application/Infrastructure).
  Jamais "toute la solution" — voir ci-dessous]
- **Version/commit de référence** : [À COMPLETER]

---

## Cadrage du périmètre (obligatoire, ne pas sauter cette section)

Un audit sans périmètre borné produit un rapport trop long pour être actionnable, ou une
lecture superficielle qui rate les vrais problèmes. Choisir **un seul** des cadrages
suivants avant de lancer l'audit :

- **Par projet** : ex. `ExcelETL.Infrastructure` seul (couche EF Core / ClosedXML).
- **Par feature transversale** : ex. tout ce qui touche à l'extraction OXO, à travers
  les couches concernées, mais rien d'autre.
- **Par lot déjà livré** : ex. relire uniquement le code produit par les Lots K/L/M,
  pas l'historique complet du projet.

Si le périmètre naturel dépasse ~10-15 fichiers significatifs, le découper en plusieurs
audits séquentiels plutôt qu'un seul.

---

## Consigne pour Claude Code

Tu es Claude Code et tu as accès au repository de la solution. Produis un document
Markdown intitulé **"Audit qualité — [périmètre]"**, factuel, basé sur une lecture réelle
du code (pas de suppositions), destiné à être lu par Claude AI dans une autre session
(sans accès au code) pour trier et prioriser.

**Reste dans le périmètre déclaré ci-dessus.** Si tu identifies un problème en dehors du
périmètre en cours de lecture, le noter en une ligne dans une section "Hors périmètre —
observé en passant" plutôt que de partir l'investiguer.

### Grille de critères à évaluer (répondre à chaque point, même "RAS")

1. **Respect de Clean Architecture / Onion**
   - Dépendances qui remontent vers l'extérieur (ex. Domain qui référence Infrastructure).
   - Logique métier qui a fui dans une couche qui ne devrait pas la porter.

2. **Règles métier câblées en dur vs profile-driven**
   - Endroits où une règle (extraction, génération) est codée en dur alors que le modèle
     de domaine (`ImportProfile`/`ExportProfile`) prévoit qu'elle soit paramétrable.
   - Si un cas hardcodé est délibéré (cf. `ProcedureExtractionService`, `DiversExtractionService`
     déjà actés comme exceptions), le signaler comme conforme, pas comme un défaut.

3. **Duplication**
   - Logique similaire répétée entre deux services/composants qui pourrait être factorisée
     sans violer SRP.
   - Ne pas signaler une duplication déjà connue et assumée (ex. WebAPI et BlazorAdmin qui
     répètent volontairement le pattern `IDbContextFactory` pour cohérence).

4. **Cohérence des conventions déjà actées**
   - Nommage, structure des dossiers, IDs HTML stables, sélection bUnit par ID (jamais texte/
     position) — cf. documents de convention du projet.
   - Toute dérive détectée par rapport à un document de convention existant est citée avec
     le nom du document de référence.

5. **Dette de test**
   - Zones avec couverture visiblement faible ou absente.
   - Tests fragiles (assertions trop larges, dépendance à l'ordre d'exécution, mocks qui
     masquent un vrai comportement).

6. **Gestion des erreurs et logs**
   - Cohérence avec le mécanisme Serilog existant (pas de mécanisme parallèle inventé).
   - Erreurs avalées silencieusement, exceptions génériques là où un type dédié existe déjà.

7. **Lisibilité / complexité**
   - Méthodes/classes significativement plus complexes que leurs équivalents dans le
     projet, sans raison métier justifiant l'écart.

### Format de sortie attendu pour chaque point relevé

Pour chaque problème identifié, fournir :
- **Localisation** : fichier(s) + classe/méthode.
- **Constat factuel** : ce qui est observé dans le code, pas une reformulation générique.
- **Impact estimé** : cosmétique / dette légère / risque réel (bug latent, incohérence
  fonctionnelle) — Claude Code propose une estimation, Claude AI tranche ensuite.
- **Refacto envisageable** : description courte, sans l'implémenter.
- **Ne pas coder le refacto dans ce document** — l'audit est un constat, pas une PR.

Terminer par une section **"Non couvert / incertain"** (comme les états des lieux
techniques) listant ce qui n'a pas pu être déterminé avec certitude depuis le code seul.

---

## Nommage du fichier de sortie

Un audit est un **instantané daté** (catégorie 2 de `convention-nommage-documents.md`),
jamais un document vivant : `audit-qualite-[périmètre]-AAAA-MM-JJ.md`. Ne jamais mettre à
jour un ancien audit en place — un nouvel audit est toujours un nouveau fichier.

---

## Ce que ce document ne déclenche pas

Produire l'audit ne vaut pas décision d'implémenter quoi que ce soit. Aucun refacto listé
dans le rapport n'est engagé avant :
1. Relecture et priorisation par Claude AI (tri des points par impact réel).
2. Validation explicite de Simon sur les points retenus.
3. Rédaction d'un ticket TDD dédié (red-green-refactor), au même format que les autres
   lots — un audit n'est jamais directement exécuté comme un ticket.

---

## Check-list avant envoi à Claude Code

- [ ] Le périmètre est borné à un seul projet/feature/lot, pas "toute la solution"
- [ ] La grille de critères (7 points) est incluse telle quelle dans le prompt
- [ ] Le nom de fichier de sortie attendu est précisé (`audit-qualite-...-AAAA-MM-JJ.md`)
- [ ] Il est rappelé explicitement que l'audit ne déclenche aucune implémentation directe
