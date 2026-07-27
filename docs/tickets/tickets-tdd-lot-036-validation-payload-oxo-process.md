# Tickets TDD — Lot 036 : validation explicite du contrat `POST /api/oxo/process`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Cinquième lot
numérique, après le Lot 035 (`tickets-tdd-lot-035-polish-dette-legere.md`, terminé). Fait suite à
`audit-qualite-webapi-2026-07-25.md` §3 et §6, trié et classé "impact réel" (contrairement au Lot
035, ce lot **change un comportement observable** de l'API : certaines requêtes qui produisent
aujourd'hui un code HTTP ambigu ou non déterministe en produiront un explicite et testé).*

**Contexte** : le contrat `POST /api/oxo/process` (Lot K, non rouvert dans ses décisions
structurantes) a trois angles morts identifiés par l'audit du 25/07 :
1. `ImportProfileId`/`ExportProfileId` absents du corps multipart (plutôt qu'invalides) tombent
   silencieusement sur `Guid.Empty`, indiscernable côté appelant d'un "profil supprimé
   entretemps" (404) plutôt qu'un rejet explicite de requête malformée.
2. Un fichier dont le contenu n'est pas un classeur Excel valide (octets non-xlsx) n'est intercepté
   par aucun type d'exception connu de `GlobalExceptionHandler.StatusCodeFor` — comportement par
   défaut non vérifié, probablement un 500 générique.
3. Le chemin "fichier vide/absent" (`request.File is null || request.File.Length == 0` → 400)
   existe dans `OxoController` mais n'a aucun test HTTP.

**Décisions actées pour ce lot** :
- **Le nom de route, la sécurité (`ApiKeyAuthenticationHandler`), et le caractère
  synchrone/bout-en-bout de l'endpoint ne sont pas rouverts** — ce lot ajoute de la validation en
  entrée, il ne redessine pas le contrat.
- **`ImportProfileId`/`ExportProfileId` absents du multipart doivent produire un rejet HTTP
  explicite (400), distinct du 404 "profil inconnu"** — un identifiant structurellement absent
  n'est pas la même erreur qu'un identifiant syntaxiquement valide mais ne correspondant à aucun
  profil. Le passage du type `Guid` à `Guid?` dans `ProcessOxoFileRequest` est la voie retenue par
  défaut (voir 36.1) — à confirmer en investigation si une contrainte de binding `[FromForm]`
  s'y oppose.
- **Un fichier dont le format n'est pas un classeur Excel valide doit produire une réponse 400
  avec un message explicite**, pas un 500 non maîtrisé — c'est une entrée malformée, pas une
  erreur serveur.

**Conventions déjà en place à respecter** : `ProblemDetails` pour toute réponse d'erreur (déjà en
place via `GlobalExceptionHandler`/usages directs dans `OxoController`) ; xUnit 2.9.3 +
FluentAssertions 7.x + Moq ; `WebApplicationFactory` pour les tests d'intégration Web API ; ne
jamais introduire de mécanisme de log parallèle à Serilog.

---

## 36.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `Contracts/ProcessOxoFileRequest.cs` : confirmer le type exact actuel de
  `ImportProfileId`/`ExportProfileId` (`Guid` non-nullable, `[FromForm]`) et si un attribut de
  validation (`[Required]`, `IValidatableObject`) est déjà présent ou absent.
- [ ] Lire `OxoController` intégralement pour localiser le point exact où ces deux propriétés
  sont consommées (probablement passées telles quelles à `IProcessOxoFileService`) — confirmer
  qu'aucune logique de validation n'existe déjà avant l'appel au service.
- [ ] Reproduire en isolation (test unitaire ou script local) le comportement réel d'
  `XLWorkbook`/`ClosedXmlWorkbookReader` face à un flux de contenu non-xlsx (ex. tableau d'octets
  aléatoires, ou un fichier texte brut renommé `.xlsx`) : quel type d'exception est levée
  concrètement (`InvalidDataException`, exception ClosedXML propre, autre) — condition
  nécessaire pour écrire 36.2 correctement, ne pas deviner le type.
- [ ] Confirmer si `GlobalExceptionHandler.StatusCodeFor` a un cas `default` explicite ou si
  l'absence de correspondance retombe sur le comportement standard `AddProblemDetails()`
  (probable 500) — vérifier le code réel, pas supposer d'après l'audit.
- [ ] Vérifier si `Contracts/ProcessOxoFileRequest.cs` ou `OxoController` sont consommés ailleurs
  dans la solution (BlazorAdmin ne les consomme pas selon l'audit BlazorAdmin — confirmer que ça
  reste vrai) avant de changer le type d'une propriété du contrat.

---

## 36.1. `ImportProfileId`/`ExportProfileId` absents du multipart → 400 explicite

**Comportement attendu** : si le champ `ImportProfileId` ou `ExportProfileId` est totalement
absent du corps multipart de la requête (pas juste syntaxiquement invalide — ce dernier cas est
déjà rejeté par le model binding ASP.NET Core avant d'atteindre le contrôleur), la réponse est un
`400 Bad Request` avec un `ProblemDetails` explicite nommant le paramètre manquant — jamais un
`404`.

**Approche technique retenue par défaut** (à confirmer en 36.0) : `ImportProfileId`/
`ExportProfileId` deviennent `Guid?` dans `ProcessOxoFileRequest`. `OxoController` vérifie
explicitement leur présence en tout début de traitement (avant tout appel à
`IImportProfileStore`/`IExportProfileStore`) et retourne un 400 dédié si l'un des deux est `null`,
avec un message distinct pour chaque paramètre (ne pas fusionner en un message générique
"paramètres manquants" si les deux peuvent manquer indépendamment).

**Tests** (`OxoProcessEndpointTests`, `WebApplicationFactory`) :
- [ ] Requête multipart sans le champ `ImportProfileId` du tout (mais `ExportProfileId` présent
  et valide, fichier présent) → 400, message explicite mentionnant `ImportProfileId`.
- [ ] Requête multipart sans le champ `ExportProfileId` du tout (symétrique) → 400, message
  explicite mentionnant `ExportProfileId`.
- [ ] Requête avec les deux champs absents → 400 (vérifier si les deux messages sont présents,
  ou si le premier rencontré suffit — comportement à documenter explicitement dans le test, pas
  supposé).
- [ ] **Non-régression** : requête avec les deux champs présents mais correspondant à des profils
  inexistants (`Guid` valide, aucun profil en base) → toujours 404, comportement inchangé (le
  test existant `OxoProcessEndpointTests` couvrant ce cas doit rester vert sans modification).
- [ ] **Non-régression** : requête nominale complète (fixture C7401) → toujours 200, comportement
  inchangé.

---

## 36.2. Fichier au contenu non-Excel valide → 400 explicite (pas 500)

**Comportement attendu** : si le flux fourni comme fichier source ne peut pas être ouvert comme
classeur Excel valide (échec de construction d'`XLWorkbook`/`ClosedXmlWorkbookReader`), la réponse
est un `400 Bad Request` avec un message explicite ("fichier Excel invalide ou corrompu" ou
équivalent), jamais un `500` non qualifié.

**Approche technique** : selon ce que révèle 36.0 sur le type d'exception réel levé par ClosedXML
sur un contenu invalide, soit (a) ce type est ajouté au `switch` de `GlobalExceptionHandler.StatusCodeFor`
s'il est stable et spécifique, soit (b) `OxoController` capture explicitement autour du point de
construction du reader et traduit en 400 lui-même (cohérent avec le traitement déjà en place pour
fichier vide/absent, au même niveau du contrôleur) — la décision entre (a) et (b) dépend de la
nature de l'exception trouvée en 36.0, à trancher à ce moment-là plutôt qu'anticipée ici.

**Tests** :
- [ ] Requête avec un fichier dont le contenu est un flux d'octets non-Excel (ex. texte brut
  encodé en UTF-8, nommé avec l'extension `.xlsx` dans le multipart) → 400, message explicite,
  pas de trace d'exception non gérée dans les logs de test.
- [ ] **Non-régression** : les 3 fixtures réelles (C7401, D8570, G6306B) continuent de produire
  leur comportement actuel exact (200 ou 422 selon le cas déjà couvert) — aucun de ces tests
  existants ne doit être affecté par ce changement.

---

## 36.3. Comblement de couverture — fichier vide/absent

**Comportement attendu** : aucun changement de code de production — le comportement existe déjà
(`request.File is null || request.File.Length == 0` → 400 dans `OxoController`). Ce sous-ticket
ajoute uniquement les tests manquants.

**Tests** :
- [ ] Requête multipart sans champ fichier du tout → 400, message explicite (vérifier le message
  réel produit par le code existant, ne pas en inventer un nouveau).
- [ ] Requête multipart avec un champ fichier présent mais de taille 0 → 400, même chemin que
  ci-dessus (vérifier si le code actuel traite les deux cas de façon strictement identique ou
  légèrement différente — documenter ce qui est observé).

---

## Hors périmètre explicite

- Toute modification du nom de route, de `ApiKeyAuthenticationHandler`, ou du caractère
  synchrone de l'endpoint.
- Le mécanisme d'archivage best-effort (Lot 034) et sa coexistence avec l'archivage `IFileStorageService`
  préexistant (Lot K) — sujet distinct, non rouvert ici.
- `legacy/ExcelProcessingClientService` (client HTTP consommant ce contrat) — hors solution .NET
  10, non modifié par ce lot ; si le changement de type `Guid` → `Guid?` a un impact sur ce
  client, le signaler à Simon en fin de lot plutôt que de modifier le projet legacy sans validation
  explicite.
- Toute nouvelle règle de validation métier sur le contenu du fichier au-delà de "peut être ouvert
  comme classeur Excel" (les règles métier existantes — PROCEDURE vide, etc. — ne sont pas
  rouvertes).
- La question de savoir si `DomainRuleViolationException` (409) est réellement atteignable depuis
  cette route (signalée en "non couvert" par l'audit) — hors périmètre de ce lot, à investiguer
  séparément si jugé utile.

---

## Ordre recommandé

1. **36.0** (investigation — conditionne le choix technique de 36.1 et 36.2)
2. **36.3** (le plus simple, aucun changement de production, à livrer en premier pour confirmer
   le comportement actuel exact avant de le faire évoluer ailleurs dans le lot)
3. **36.1** puis **36.2** (changements de comportement réels, à traiter l'un après l'autre pour
   isoler toute régression)

## Note d'efficacité d'implémentation

- Ne pas grouper 36.1 et 36.2 dans le même commit : ce sont deux chemins d'erreur distincts,
  les isoler facilite la relecture et la localisation d'une éventuelle régression.
- 36.0 doit impérativement produire une preuve concrète (nom exact du type d'exception ClosedXML)
  avant d'écrire 36.2 — ne pas écrire de `catch (Exception)` générique dans `OxoController` sans
  savoir précisément ce qui doit être intercepté, au risque de masquer une vraie erreur serveur
  future sous un faux 400.
- Après ce lot, mettre à jour `instructions-systeme-claude-code.md`/`CLAUDE.md` si le contrat de
  `ProcessOxoFileRequest` change de type (`Guid` → `Guid?`) — c'est un changement de contrat
  public, pas un détail d'implémentation interne.
