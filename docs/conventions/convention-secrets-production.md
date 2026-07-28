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

## Hors périmètre de ce document

- Le choix définitif entre hébergement IIS et service Windows autonome — non tranché ici, sans
  impact sur la convention elle-même.
- Un gestionnaire de secrets externe (Key Vault ou équivalent) — explicitement écarté pour
  l'instant (voir "Décision actée" ci-dessus), à revisiter seulement si un besoin concret apparaît.
- La procédure pas-à-pas de déploiement elle-même (script, checklist IIS) — objet d'un futur
  document dédié le jour où le déploiement réel est préparé en détail.
