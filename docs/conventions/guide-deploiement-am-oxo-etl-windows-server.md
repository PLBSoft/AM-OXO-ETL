# Guide de déploiement — AM-OXO-ETL (Windows Server 2022 + SQL Server)

*Document de travail — hébergement IIS.*

---

## 0. Décisions et contexte actés

- **Hébergement : IIS** (pas d'alternative Kestrel/service Windows).
- **Deux sites IIS distincts** pour WebAPI et BlazorAdmin (deux hostnames/bindings séparés,
  pas un seul site avec des chemins différents) — permet de retirer BlazorAdmin plus tard sans
  toucher au WebAPI en prod.
- **Hostnames** : `oxo-etl-api.alphamaintenance.fr` (WebAPI) et `oxo-etl-admin.alphamaintenance.fr`
  (BlazorAdmin). À réserver en DNS.
- **Certificat TLS auto-signé**, un par site.
- **SQL Server local au serveur cible, instance `.\MSSQLSERVER2019`** — confirmé.
- **Colocalisation confirmée** : le serveur héberge déjà une autre application via IIS.
- Mots de passe des comptes admin seedés : lus séparément de l'identité du compte, via
  `AdminSeedPasswords:{UserName}` → variables d'environnement `AdminSeedPasswords__SLB` /
  `__J2M` / `__JPN` (valeurs de prod à générer, ne jamais réutiliser celles du poste de dev).
- Vérification EF Core contre un vrai SQL Server : déjà faite en local avec succès
  (`audit-verification-base-de-donnees-2026-07-27.md` — 10/10 migrations, seeding OK,
  `SystemLogs` actif). À refaire sur le serveur cible avant le go-live.

---

## 1. Prérequis serveur

- [ ] Rôle IIS activé (contenu statique, doc. par défaut, erreurs HTTP, filtrage requêtes)
- [ ] **ASP.NET Core Hosting Bundle** (.NET 10) installé — inclut le module ANCM, redémarre IIS
- [ ] **WebSockets activé dans IIS** — indispensable pour BlazorAdmin (SignalR) ; vérifier la
  compatibilité avec la configuration IIS existante du serveur, sans effet de bord sur les sites
  déjà en place
- [ ] SQL Server accessible, TCP/IP activé si connexion réseau
- [ ] Certificats TLS auto-signés générés/disponibles pour les deux sites
- [ ] Chaque certificat importé dans le magasin "Autorités de certification racines de confiance"
  de tous les postes client (client final + postes admin), sinon avertissement navigateur
- [ ] Certificat du site WebAPI également approuvé **sur le serveur lui-même**, sinon l'appel
  interne BlazorAdmin → WebAPI (`OxoApiTestClient`, page `/api-test`) échouera en validation TLS
- [ ] Ports ouverts dans le pare-feu (443, 80 en redirection si besoin)

---

## 2. Préparation SQL Server

- [x] SQL Server local au serveur cible, instance `.\MSSQLSERVER2019` — confirmé.
- [ ] Authentification : **Windows intégrée** (`Trusted_Connection=True`) — pas d'authentification
  SQL par identifiant/mot de passe
- [ ] Identité des pools IIS reconnue par SQL Server — **identité de pool virtuelle IIS par
  défaut** (pas de compte de service dédié à créer/maintenir) : `CREATE LOGIN
  [IIS APPPOOL\AM-OXO-ETL-WebAPI] FROM WINDOWS` et `CREATE LOGIN [IIS APPPOOL\AM-OXO-ETL-BlazorAdmin]
  FROM WINDOWS` (un login par pool)
- [ ] Droits `db_owner` accordés sur `AM-OXO-ETL-MAD-REL` au(x) login(s) retenu(s)
- [ ] Ne pas créer la base à la main — l'auto-application des migrations au démarrage la crée
- [ ] Nom de base attendu : `AM-OXO-ETL-MAD-REL`
- [ ] Essai à blanc sur le SQL Server cible (mêmes vérifications que l'audit du 27/07 : tables,
  migrations, comptes admin, profils, `SystemLogs`)

---

## 3. Publication (Visual Studio)

Pour chaque projet (`ExcelETL.WebAPI` et `ExcelETL.BlazorAdmin`), depuis Visual Studio :

- [ ] Clic droit sur le projet → **Publier**
- [ ] Cible : **Dossier** (Folder)
- [ ] Configuration : **Release**
- [ ] Mode de déploiement : **Dépendant du framework** (le runtime .NET 10 est déjà présent sur
  le serveur via le Hosting Bundle — pas besoin d'auto-suffisant)
- [ ] Dossier cible ex. `C:\publish\webapi` et `C:\publish\blazoradmin`
- [ ] Cliquer **Publier** pour chaque projet
- [ ] Copier chaque dossier publié vers le serveur (ex. `C:\inetpub\AM-OXO-ETL\WebAPI\` et
  `...\BlazorAdmin\`)
- [ ] Vérifier qu'aucun `appsettings.Production.json` n'est présent sur le disque

*Astuce : une fois le profil de publication créé une première fois (fichier `.pubxml` sous
`Properties\PublishProfiles\`), les publications suivantes se font en un clic sur le profil
existant, sans repasser par l'assistant complet.*

---

## 4. Configuration IIS

**Pools d'applications** (un par hôte) :
- [ ] `AM-OXO-ETL-WebAPI` — .NET CLR : Sans code managé
- [ ] `AM-OXO-ETL-BlazorAdmin` — .NET CLR : Sans code managé
- [ ] Identité de pool = `ApplicationPoolIdentity` par défaut (cohérent avec le choix de login SQL
  Server `IIS APPPOOL\...`, §2)
- [ ] Noms de pools distincts de ceux déjà utilisés par les autres sites du serveur — pas de
  collision de nom
- [ ] Démarrage automatique = toujours en cours d'exécution (évite le cold start)

**Sites** (deux sites distincts, cf. décision §0) :
- [ ] Site WebAPI → `C:\inetpub\AM-OXO-ETL\WebAPI\`, pool dédié, binding HTTPS sur `oxo-etl-api.alphamaintenance.fr`
- [ ] Site BlazorAdmin → `C:\inetpub\AM-OXO-ETL\BlazorAdmin\`, pool dédié, binding HTTPS sur `oxo-etl-admin.alphamaintenance.fr`
- [ ] Les deux nouveaux bindings ajoutés en SNI, sans modifier les bindings déjà en place sur ce
  serveur (colocalisation, §0)
- [ ] Vérifier `hostingModel` = InProcess dans chaque `web.config` généré

---

## 5. Variables d'environnement (secrets — niveau pool IIS, jamais en fichier)

| Hôte | Variable | Contenu |
|---|---|---|
| WebAPI | `ApiKeyAuthentication__ApiKey` | clé API de prod (nouvelle, pas celle de dev) |
| WebAPI | `ConnectionStrings__DefaultConnection` | `Server=.\MSSQLSERVER2019;Database=AM-OXO-ETL-MAD-REL;Trusted_Connection=True;TrustServerCertificate=True;` |
| BlazorAdmin | `OxoApiTestClient__ApiKey` | **même clé** que `ApiKeyAuthentication__ApiKey` |
| BlazorAdmin | `OxoApiTestClient__BaseUrl` | URL HTTPS réelle du site WebAPI (jamais `localhost`) |
| BlazorAdmin | `ConnectionStrings__DefaultConnection` | même chaîne de connexion que WebAPI |
| BlazorAdmin | `AdminSeedPasswords__SLB` / `__J2M` / `__JPN` | mots de passe admin de prod |

- [ ] Dossier `GeneratedFilesArchive:RootPath` (ex. `D:\AM-OXO-ETL\generated-files`) créé, droits
  d'écriture pour l'identité du pool BlazorAdmin (archivage best-effort — échoue
  silencieusement si le dossier est inaccessible)

Rappel : secret manquant (clé API, chaîne de connexion) → l'app refuse de démarrer (fail-fast
voulu). Mot de passe admin manquant → `LogWarning` seulement, compte simplement ignoré.

---

## 6. Premier démarrage — vérifications

- [ ] Démarrer BlazorAdmin en premier (porte les seeders)
- [ ] Logs : base créée, migrations appliquées sans erreur
- [ ] Logs : aucun `Warning` sur le seeding des 3 comptes admin
- [ ] Les 2 profils par défaut (import/export OXO standard) sont bien seedés
- [ ] Démarrer WebAPI, vérifier le health check (200)
- [ ] Se connecter à BlazorAdmin, changer les mots de passe si nécessaire
- [ ] `ImportProfileTest.razor`/`ExportProfileTest.razor` : test avec un fichier fixture réel
- [ ] `/api-test` (si livré) : appel HTTP réel bout-en-bout vers le WebAPI
- [ ] `SystemLogs` reçoit bien des entrées (confirme le sink Serilog MSSqlServer)
- [ ] Page « Journaux » (`/logs`) : l'heure affichée correspond à l'heure locale du poste client,
  pas à celle du serveur (Lot 064 — conversion navigateur, non testable automatiquement)

---

## 7. Checklist finale avant accès client

- [ ] Essai à blanc SQL Server fait sur le serveur cible (pas seulement en local)
- [ ] Mots de passe admin seedés changés après premier login
- [ ] Clé API de prod générée, identique des deux côtés
- [ ] Certificat TLS installé et approuvé sur les postes client + sur le serveur (auto-signé : pas
  de blocage navigateur ni d'échec d'appel interne BlazorAdmin → WebAPI)
- [ ] WebSockets activé dans IIS
- [ ] Dossier d'archivage créé avec droits d'écriture
- [ ] Test desktop uniquement pour ce jalon — accessibilité clavier en cours de finalisation,
  non bloquante

---

## Hors périmètre

- Sauvegarde/plan de reprise SQL Server
- Supervision applicative au-delà de Serilog/`SystemLogs`
- Procédure de mise à jour d'une version déjà déployée
