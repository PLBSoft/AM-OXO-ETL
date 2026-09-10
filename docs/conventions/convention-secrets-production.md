# Convention — secrets de production (AM-OXO-ETL)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fixe une
convention transverse aux deux hôtes (`ExcelETL.WebAPI`, `ExcelETL.BlazorAdmin`) et à tout futur
secret de production, tranchée au moment de la préparation du premier déploiement réel du Web API
(26/07) — avant qu'aucun secret réel n'existe encore sur un serveur.*

## Décision actée

**Les secrets de production sont fournis par variables d'environnement, jamais par un fichier
`appsettings.Production.json` committé ou même présent sur le disque du serveur.**

Raisons :
- ASP.NET Core lit nativement les variables d'environnement et les superpose à
  `appsettings.json`/`appsettings.{Environment}.json` — aucun code supplémentaire à écrire, le
  mécanisme `IOptions<T>`/`IConfiguration` déjà en place dans le projet fonctionne tel quel.
- Élimine le risque de committer un secret par erreur, ou de le laisser traîner dans un fichier de
  configuration sur le disque du serveur/une sauvegarde.
- Rotation simple (changer la variable + redémarrer le pool IIS/service Windows), sans fichier à
  retrouver et éditer sur le serveur.
- Un gestionnaire de secrets externe (Azure Key Vault ou équivalent) serait disproportionné à ce
  stade pour un unique serveur on-premise avec un seul client connu (YAGNI, cohérent avec les
  autres décisions du projet) — à revisiter uniquement si un besoin concret l'impose.

## Convention de nommage

Mapping standard ASP.NET Core : le séparateur hiérarchique `:` d'une clé de configuration devient
`__` (double underscore) en variable d'environnement.

| Section de configuration | Variable d'environnement |
|---|---|
| `ApiKeyAuthentication:ApiKey` (WebAPI) | `ApiKeyAuthentication__ApiKey` |
| `OxoApiTestClient:ApiKey` (BlazorAdmin) | `OxoApiTestClient__ApiKey` |
| `OxoApiTestClient:BaseUrl` (BlazorAdmin) | `OxoApiTestClient__BaseUrl` |
| `GeneratedFilesArchive:RootPath` (WebAPI **et** BlazorAdmin) | `GeneratedFilesArchive__RootPath` |

`GeneratedFilesArchive:RootPath` n'est pas un secret au sens strict (pas de credential), mais
c'est une valeur spécifique à l'environnement/au serveur (chemin disque local) — elle suit la même
convention pour la même raison que les autres : ne jamais dépendre d'un `appsettings.Production.json`
committé pour la porter. **Trouvé manquant de cette table le 10/09** (incident du même jour, voir
plus bas) — la valeur réelle de production (`c:\inetpub\Alpha\AM-OXO-ETL\GeneratedFiles\`) vivait
uniquement dans les fichiers `appsettings.Production.json` supprimés du suivi Git ; sans variable
d'environnement posée sur les deux pools IIS pour remplacer ça, les deux hôtes retombent
silencieusement sur les valeurs de repli de leur `appsettings.json` respectif (deux chemins
différents entre WebAPI et BlazorAdmin, aucun des deux n'étant le chemin réel de production) — à
vérifier/corriger sur le serveur avant le prochain déploiement.

Toute future section de configuration contenant un secret ou une valeur spécifique à
l'environnement de production suit le même mapping — pas de nouvelle convention à inventer.

## Conséquence sur les fichiers du dépôt

- **Aucun `appsettings.Production.json` n'existe ni ne doit exister dans le dépôt**, pour aucun
  des deux hôtes. Ce n'est pas un oubli à corriger — c'est la conséquence directe de cette
  décision.
- `appsettings.json`/`appsettings.Development.json` peuvent continuer à porter des valeurs de
  développement (ex. clé API de dev, `BaseUrl` en `https://localhost:...`) — ces fichiers ne
  contiennent jamais de secret réel de production.
- Le comportement fail-fast déjà en place (`OxoApiTestClientOptionsValidator`, Lot 038 ;
  mécanisme équivalent pour `ApiKeyAuthentication`) est le comportement final voulu, pas un état
  transitoire : tant que les variables d'environnement ne sont pas positionnées sur le serveur au
  moment du déploiement, l'application doit refuser de démarrer plutôt que de tourner avec une
  configuration incomplète ou une valeur par défaut silencieuse.

## Où les variables sont définies sur le serveur

Au niveau du Pool d'applications IIS (ou du service Windows, selon le mode d'hébergement retenu)
sur le serveur cible — jamais dans un fichier versionné du dépôt. Ce point d'ancrage exact (IIS
vs service Windows autonome) reste à confirmer au moment du déploiement réel ; cette convention
s'applique identiquement dans les deux cas, seul l'écran de configuration change.

## Incident réel (2026-09-10) — la décision violée, puis corrigée

Malgré cette convention, `src/ExcelETL.WebAPI/appsettings.Production.json` et
`src/ExcelETL.BlazorAdmin/appsettings.Production.json` ont été forcés au suivi Git
(`git add -f`, commit `7716dc4`, 28/07) et committaient en clair la vraie
`ApiKeyAuthentication:ApiKey`/`OxoApiTestClient:ApiKey` de production. Poussé sur `origin/main`,
exposé ~6 semaines avant d'être découvert (en vérifiant où vivait réellement la clé pour câbler
l'intégration M2M avec l'app legacy — la variable de pool IIS attendue n'existait pas, seule
`ASPNETCORE_ENVIRONMENT` y était posée).

**Remédiation** : rotation de la clé sur les deux pools IIS, puis `git rm --cached` sur les deux
fichiers (conservés en local, non committables). Historique Git non réécrit — une fois la clé
tournée, l'ancienne valeur committée est inerte. Détail complet dans `CLAUDE.md` (section dédiée)
et `guide-deploiement-am-oxo-etl-windows-server.md` §5 (incident du même jour).

**Ce que ça change concrètement à cette convention** : rien sur le fond — la décision actée plus
haut reste la bonne, l'incident est une violation de la convention, pas une preuve qu'elle était
mal conçue. Seul ajout pratique : vérifier systématiquement `git ls-files | grep -i
appsettings.Production` (doit toujours renvoyer une liste vide) lors de tout futur déploiement ou
dépannage — un résultat non vide est cette même classe d'incident qui se reproduit.

## Hors périmètre de ce document

- Le choix définitif entre hébergement IIS et service Windows autonome — non tranché ici, sans
  impact sur la convention elle-même.
- Un gestionnaire de secrets externe (Key Vault ou équivalent) — explicitement écarté pour
  l'instant (voir "Décision actée" ci-dessus), à revisiter seulement si un besoin concret apparaît.
- La procédure pas-à-pas de déploiement elle-même (script, checklist IIS) — objet d'un futur
  document dédié le jour où le déploiement réel est préparé en détail.
