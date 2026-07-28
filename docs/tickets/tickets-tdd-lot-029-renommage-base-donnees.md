# Tickets TDD — Lot 029 : renommage du nom de base de données par défaut

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Premier lot
utilisant la nouvelle convention de nommage numérique à trois chiffres (§3 de
`convention-nommage-documents.md`) — les lots 027 et 028 sont créés en parallèle, dans des
documents distincts, sans lien fonctionnel avec celui-ci.*

**Demande client/Simon** : le nom par défaut de la base de données SQL Server (actuellement
`ExcelEtl`, visible en LocalDB) doit devenir **`AM-OXO-ETL-MAD-REL`**.

**Portée** : changement de configuration (chaîne de connexion), pas de changement de schéma. Ce
lot ne touche à aucune entité Domain, aucune migration EF Core existante, aucun seeder.

**Conventions déjà en place à respecter** : deux `DbContext` (identité + métier) partagent la même
base physique (voir `etat-avancement-global-2026-07-24.md` §3) ; `IDbContextFactory<T>`, jamais de
`DbContext` scoped injecté directement ; xUnit 2.9.3 + FluentAssertions 7.x + Moq.

---

## 029.0. Investigation préalable (obligatoire avant tout code)

- [ ] Localiser toutes les occurrences littérales de `ExcelEtl` dans les chaînes de connexion :
  `appsettings.json`/`appsettings.Development.json` de `ExcelETL.WebAPI` et
  `ExcelETL.BlazorAdmin` (les deux hôtes possèdent chacun leur propre configuration — vérifier
  qu'aucun des deux n'a été oublié).
- [ ] Vérifier si un design-time factory (`IDesignTimeDbContextFactory<T>`, utilisé par les
  commandes `dotnet ef migrations add`) contient une chaîne de connexion codée en dur distincte de
  celle des `appsettings.json` — si oui, elle doit être alignée aussi, sinon les futures
  migrations seraient générées contre une base différente de celle réellement utilisée à
  l'exécution.
- [ ] Localiser où le nom des tables d'historique de migration personnalisées
  (`__EFMigrationsHistory_ExcelEtl`/`__EFMigrationsHistory_Identity`) est configuré (probablement
  `.ToTable("__EFMigrationsHistory_...")` dans la configuration du `HistoryRepository` ou dans
  `OnConfiguring`/`OnModelCreating`). **Ne pas les renommer sans confirmation explicite** — ce nom
  est dérivé du nom du `DbContext`, pas de la base physique ; à documenter comme hors périmètre
  sauf décision contraire.
- [ ] Vérifier si un projet de test (`WebApplicationFactory`, tests d'intégration) référence
  littéralement `ExcelEtl` dans sa configuration — les tests utilisant EF Core InMemory ne sont
  pas concernés (pas de nom de catalogue SQL Server), mais tout test qui pointerait par erreur
  vers une vraie chaîne de connexion doit être signalé.
- [ ] Confirmer qu'aucun script SQL, document `.md`, ou commentaire de code ne fige `ExcelEtl` en
  dur ailleurs (recherche texte globale dans `src/`/`docs/`).

---

## 029.1. Renommage de la chaîne de connexion (les deux hôtes)

**Comportement attendu** :
- `Initial Catalog=ExcelEtl` (ou équivalent `Database=ExcelEtl` selon la syntaxe utilisée) devient
  `Initial Catalog=AM-OXO-ETL-MAD-REL` dans **les deux** `appsettings.json` (`WebAPI` et
  `BlazorAdmin`), et dans le design-time factory si 029.0 en a trouvé un distinct.
- Aucune autre partie de la chaîne de connexion (serveur, authentification) n'est modifiée par ce
  ticket.
- Le mécanisme d'auto-application des migrations au démarrage (Lot G4, inchangé) créera la
  nouvelle base `AM-OXO-ETL-MAD-REL` de toutes pièces au prochain démarrage réel d'un hôte — **la
  base `ExcelEtl` existante n'est pas migrée/renommée par ce ticket**, elle est simplement
  abandonnée (à supprimer manuellement en LocalDB si besoin, hors périmètre applicatif). C'est
  cohérent avec le fait que ce ticket ne vise qu'un environnement de développement local, pas une
  base de production déjà peuplée.

**Tests** :
- Test (xUnit, pas d'accès réseau réel) qui charge la configuration via `ConfigurationBuilder`
  pointant sur chaque `appsettings.json` (WebAPI et BlazorAdmin) et vérifie que la chaîne de
  connexion contient bien `AM-OXO-ETL-MAD-REL` et ne contient plus `ExcelEtl` — un test par hôte,
  pour garantir qu'aucun des deux n'a été oublié (piège explicite visé par 029.0).
- Si un design-time factory dédié existe : même assertion appliquée à sa chaîne de connexion.
- Non-régression : les tests d'intégration existants (`WebApplicationFactory`, bUnit) continuent
  de passer sans modification — ils utilisent EF Core InMemory et ne dépendent pas du nom de
  catalogue SQL Server réel.

**Dossier** : `src/ExcelETL.WebAPI/appsettings.json`, `src/ExcelETL.BlazorAdmin/appsettings.json`
(+ `appsettings.Development.json` si distinct), et tout design-time factory identifié en 029.0.

---

# Hors périmètre explicite

- Renommage des tables d'historique de migration (`__EFMigrationsHistory_ExcelEtl`) — décision à
  confirmer séparément, non tranchée par ce ticket (voir 029.0).
- Migration/renommage de la base LocalDB existante contenant déjà des données de développement
  (profils seedés, comptes admin) — abandonnée au profit d'une base fraîche sous le nouveau nom.
- Toute base de données autre que celle de ce microservice (`AM-LASARA2028`, `AM-MT`, etc.,
  visibles dans le même serveur LocalDB) — hors périmètre, non concernées.
- Changement de serveur ou de mode d'authentification SQL Server.

---

# Note d'efficacité d'implémentation

1. Traiter 029.0 intégralement en premier — un seul passage de recherche texte (`ExcelEtl`) dans
   tout le dépôt suffit à couvrir tous les emplacements à corriger en 029.1, y compris le point
   ouvert sur les tables d'historique.
2. 029.1 est un changement mécanique une fois 029.0 terminé — pas de refactor architectural
   attendu ici, effort standard suffit pour l'ensemble du ticket (pas de mode de réflexion élevé
   nécessaire).
