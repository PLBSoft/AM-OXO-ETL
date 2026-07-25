# Demande à Claude Code — Audit qualité / refacto : `ExcelETL.BlazorAdmin`

## Métadonnées de la demande

- **Date de la demande** : 2026-07-25
- **Demandeur** : Simon
- **Contexte / déclencheur** : pause après une succession dense de lots UI (R, S, T6/T7, U1, V1→V13,
  W, X, Y) — c'est le projet le plus volumineux en tests (278 au 24/07) et celui qui a le plus
  bougé récemment, donc le plus exposé à une dérive de convention non détectée. Audit exécuté en
  parallèle avec 4 autres audits (Domain, Application, Infrastructure, WebAPI).
- **Périmètre exact** : projet `ExcelETL.BlazorAdmin` uniquement — composants Razor, `NavMenu.razor`,
  éditeurs de profils (`ImportProfileEditor.razor`/`ExportProfileEditor.razor`), pages de test
  (`ImportProfileTest.razor`/`ExportProfileTest.razor`), listes, `Logs.razor`, `MainLayout.razor`,
  fichiers `.razor.css`. Ne pas lire Domain, Application, Infrastructure, WebAPI (le typed
  HttpClient documenté comme seule exception d'appel à WebAPI reste dans le périmètre, puisqu'il
  vit dans ce projet).
- **Version/commit de référence** : `main`, dernier commit connu `d018a90` (2026-07-24,
  812/812 tests slnx verts) — Claude Code confirme le commit exact réellement audité.

## Cadrage du périmètre

Cadrage retenu : **par projet** (`ExcelETL.BlazorAdmin`). Vu le volume de lots UI successifs sur
une courte période, la vigilance principale porte sur la **cohérence entre pages** plutôt que sur
la qualité d'une page isolée — en particulier la parité Import/Export déjà actée à plusieurs
reprises (Lots Q, R, 030) et le respect des conventions transverses
(`convention-ui-blazor-alignement-boutons.md`, `convention-ui-blazor-icones-boutons.md`).

## Consigne pour Claude Code

Tu es Claude Code et tu as accès au repository de la solution. Produis un document Markdown
intitulé **"Audit qualité — ExcelETL.BlazorAdmin"**, factuel, basé sur une lecture réelle du code
(pas de suppositions), destiné à Claude AI dans une autre session pour trier et prioriser.

**Reste dans `ExcelETL.BlazorAdmin`.** Si un problème implique une autre couche, le noter en une
ligne dans "Hors périmètre — observé en passant" sans aller lire le code concerné.

### Grille de critères à évaluer (répondre à chaque point, même "RAS")

1. **Respect de Clean Architecture / Onion**
   - Confirmer qu'aucun composant n'appelle `ExcelETL.WebAPI` directement, à l'exception
     documentée du typed HttpClient de la page `/upload-test`. Tout autre appel HTTP direct vers
     WebAPI serait une violation de la règle actée.

2. **Parité Import/Export**
   - Vérifier que les correctifs actés comme devant s'appliquer aux deux éditeurs (grille
     responsive Lot R, `form-floating` Lot 030, boutons Lot X/Y) sont bien présents et
     **identiques** des deux côtés, pas seulement l'un des deux — c'est le type de régression
     silencieuse déjà rencontré sur ce projet (cf. Lot 030 justement déclenché par une telle
     dérive détectée par screenshot).
   - Toute divergence non documentée par une exclusion explicite (comme la page Journaux qui
     garde son tableau natif) est un signal à remonter.

3. **NavMenu / visibilité selon rôle**
   - Confirmer la vérification par absence DOM réelle (pas seulement absence dans une liste de
     menu) pour tout lien conditionné par rôle — point déjà source de deux régressions au Lot L1
     (lien de connexion dupliqué, lien "Journaux" non masqué), corrigées par L2.

4. **Duplication**
   - Markup ou logique de composant dupliquée entre pages de liste (`ImportProfiles.razor`/
     `ExportProfiles.razor`/`Users.razor`) qui pourrait être factorisée en composant partagé,
     sans casser le principe "un seul jeu de données, deux gabarits d'affichage" déjà acté pour
     le responsive (Lot V2).

5. **Cohérence des conventions déjà actées**
   - IDs HTML stables sur tout élément interactif (jamais de sélection par texte/position en
     bUnit) — recherche de tests qui violeraient cette règle.
   - Respect de `convention-ui-blazor-alignement-boutons.md` (boutons à droite) et
     `convention-ui-blazor-icones-boutons.md` (icônes Bootstrap Icons, `aria-label`/`title`
     sur les boutons icône seule).
   - Confirmer qu'aucun composant n'utilise `<InputFile>` d'une façon qui casserait
     `FindComponent<InputFile>()` en test (contrainte explicite actée au Lot V, partie B).

6. **Dette de test**
   - Zones avec couverture plus faible que la moyenne (278 tests sur ce projet, le plus gros
     volume de la solution) — en particulier les pages les plus récemment modifiées (Lot X/Y).
   - Tests bUnit qui vérifient une classe CSS sans vérifier le comportement réel associé, ou
     l'inverse.

7. **Lisibilité / complexité**
   - Composants significativement plus longs/complexes que leurs pairs (`.razor` de plusieurs
     centaines de lignes sans découpage) sans raison fonctionnelle documentée.

### Format de sortie attendu pour chaque point relevé

Pour chaque problème : **localisation**, **constat factuel**, **impact estimé**, **refacto
envisageable** (non implémentée). Terminer par **"Non couvert / incertain"**.

## Nommage du fichier de sortie

`audit-qualite-blazoradmin-AAAA-MM-JJ.md` (instantané daté, catégorie 2 — jamais mis à jour en
place).

## Ce que ce document ne déclenche pas

Aucun refacto listé n'est engagé avant relecture/priorisation par Claude AI, validation
explicite de Simon, puis ticket TDD dédié.
