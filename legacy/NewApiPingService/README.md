# NewApiPingService — Guide de connectivité Legacy ↔ WebAPI

Ce projet est une bibliothèque de classes ciblant **.NET Framework 4.8**, destinée à être intégrée
dans l'application legacy (ASP.NET MVC 5). Elle représente le client HTTP que la legacy application
utilisera pour dialoguer avec la nouvelle Web API (.NET 10) — synchrone, sécurisé par clé API.

Ce document explique :
1. Comment reproduire en local le test de connectivité (le "poke test") réalisé pendant le Milestone 3.
2. Comment intégrer ce client dans l'application legacy réelle et le faire fonctionner en production.

---

## 1. Reproduire le test de connectivité en local

### Prérequis
- SDK .NET 10 installé (`dotnet --list-sdks` doit afficher une version `10.x`)
- Le pack de ciblage .NET Framework 4.8 installé (fourni avec Visual Studio ou le
  "Developer Pack .NET Framework 4.8" téléchargeable séparément)

### Étape 1 — Démarrer la Web API

```powershell
cd C:\AM-OXO-ETL
dotnet run --project src/ExcelETL.WebAPI/ExcelETL.WebAPI.csproj
```

Notez l'URL affichée dans la console (ex. `http://localhost:5112`) et la clé API de développement
définie dans `src/ExcelETL.WebAPI/appsettings.Development.json` (`ApiKeyAuthentication:ApiKey`).

### Étape 2 — Écrire un petit harnais net48

Créez un projet console temporaire ciblant `net48` qui référence `NewApiPingService.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System.Net.Http" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\NewApiPingService\NewApiPingService.csproj" />
  </ItemGroup>
</Project>
```

```csharp
using System;
using Legacy.NewApiPingService;

var baseUrl = "http://localhost:5112/";   // URL affichée à l'étape 1
var apiKey = "dev-local-api-key-CHANGE-ME"; // valeur de appsettings.Development.json

using (var service = new NewApiPingService(baseUrl, apiKey))
{
    var result = service.PingAsync().GetAwaiter().GetResult();
    Console.WriteLine("SUCCESS: " + result);
}
```

### Étape 3 — Exécuter et vérifier

```powershell
dotnet run --project MonHarnaisDePoke.csproj
```

Résultat attendu :

```
SUCCESS: {"status":"Pong","timestampUtc":"2026-07-10T14:35:36.20Z"}
```

Testez également avec une clé API invalide : vous devez obtenir une `HttpRequestException`
avec le message indiquant un statut `401 (Unauthorized)` — cela confirme que la protection
par clé API fonctionne de bout en bout, pas seulement en test in-process.

> Ce harnais est volontairement **temporaire** (non commité) : son seul but est de prouver la
> connectivité réseau réelle entre un binaire .NET Framework 4.8 et le process .NET 10, ce qu'un
> test `WebApplicationFactory` in-process ne peut pas faire (il ne peut pas être référencé
> depuis un projet net48).

---

## 2. Intégrer dans l'application legacy (production)

### Étape 1 — Référencer la bibliothèque

Depuis la solution ASP.NET MVC 5, ajoutez une référence de projet (ou packagez `NewApiPingService`
en `.dll`/NuGet interne si les deux solutions ne sont pas dans le même repository) :

```
legacy\NewApiPingService\NewApiPingService.csproj
```

### Étape 2 — Configurer l'URL et la clé API dans `Web.config`

Ne jamais coder en dur l'URL ou la clé API. Utilisez `appSettings`, idéalement avec la
section chiffrée (`aspnet_regiis -pe`) en production :

```xml
<appSettings>
  <add key="ExcelEtlApi:BaseUrl" value="https://excel-etl.monentreprise.local/" />
  <add key="ExcelEtlApi:ApiKey" value="{{à définir via variable d'environnement ou coffre-fort de secrets}}" />
</appSettings>
```

### Étape 3 — Activer TLS 1.2 au démarrage de l'application

**Point d'attention critique** : .NET Framework 4.8 ne négocie pas toujours TLS 1.2/1.3 par défaut
selon la configuration du serveur. Ajoutez ceci dans `Global.asax.cs`, dans `Application_Start()`,
**avant** tout appel HTTP sortant :

```csharp
using System.Net;

protected void Application_Start()
{
    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
    // ... reste de l'initialisation existante
}
```

Sans cette ligne, les appels vers la Web API peuvent échouer silencieusement avec une
`WebException` de type "connexion sous-jacente fermée" sur un serveur qui n'autorise pas
les protocoles TLS obsolètes (SSL3, TLS 1.0/1.1) — ce qui sera le cas sur Windows Server 2022
correctement durci.

### Étape 4 — Instancier et appeler le service

`NewApiPingService` implémente `IDisposable`. Pour un appel ponctuel (ex. contrôle de santé) :

```csharp
using Legacy.NewApiPingService;

var baseUrl = ConfigurationManager.AppSettings["ExcelEtlApi:BaseUrl"];
var apiKey = ConfigurationManager.AppSettings["ExcelEtlApi:ApiKey"];

using (var pingService = new NewApiPingService(baseUrl, apiKey))
{
    try
    {
        var result = await pingService.PingAsync();
        // journaliser le succès / exposer sur une page de diagnostic interne
    }
    catch (HttpRequestException ex)
    {
        // journaliser l'échec (clé invalide, service indisponible, etc.)
    }
    catch (TaskCanceledException ex)
    {
        // le timeout de 2 minutes a été atteint sans réponse — investiguer réseau/pare-feu
    }
}
```

> Pour un usage réel (au-delà du ping), privilégiez une instance **réutilisée** (singleton applicatif
> ou résolue via votre conteneur IoC) plutôt qu'une instance par requête : `HttpClient` est conçu pour
> être réutilisé et son instanciation répétée peut épuiser les sockets disponibles sous charge
> (problème bien documenté de "socket exhaustion" en .NET Framework).

### Étape 5 — Vérifier depuis le serveur legacy

Une fois déployé, validez la connectivité réseau *depuis le serveur legacy lui-même* (et pas
seulement depuis votre poste de dev) :

```powershell
# Test bas niveau (résolution DNS + port ouvert), depuis le serveur legacy
Test-NetConnection -ComputerName excel-etl.monentreprise.local -Port 443

# Test applicatif complet (auth incluse), depuis le serveur legacy
Invoke-RestMethod -Uri "https://excel-etl.monentreprise.local/api/health/ping" `
  -Headers @{ "X-Api-Key" = "<clé configurée>" }
```

Un `200 OK` avec un corps `{"status":"Pong", ...}` confirme que le chemin réseau complet
(DNS, pare-feu, TLS, authentification applicative) fonctionne entre les deux serveurs.

---

## 3. Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| `401 Unauthorized` | Clé API absente/incorrecte, ou mauvais header (`X-Api-Key` requis) | Vérifier `appSettings` et la configuration côté Web API |
| `TaskCanceledException` après ~2 min | Le serveur ne répond pas dans le délai du `Timeout` | Vérifier que la Web API est démarrée et joignable ; vérifier les pare-feux réseau entre les deux serveurs |
| `WebException` — "connexion sous-jacente fermée" | TLS 1.2/1.3 non négocié par le client .NET Framework | Vérifier `ServicePointManager.SecurityProtocol` (voir Étape 3) |
| `HttpRequestException` — nom d'hôte introuvable | Problème DNS entre le serveur legacy et le serveur de la Web API | Vérifier la résolution DNS interne, ou utiliser une entrée `hosts` temporaire pour isoler le problème |
| Timeouts intermittents sous charge | Épuisement de sockets dû à une instanciation répétée de `HttpClient` | Réutiliser une seule instance de `NewApiPingService` / `HttpClient` (voir Étape 4) |

---

## Fichiers de référence

- Implémentation : [`NewApiPingService.cs`](NewApiPingService.cs)
- Tests unitaires (configuration `BaseAddress`/`Timeout`/header) :
  [`../NewApiPingService.Tests/NewApiPingServiceTests.cs`](../NewApiPingService.Tests/NewApiPingServiceTests.cs)
- Endpoint testé côté Web API : [`HealthController.cs`](../../src/ExcelETL.WebAPI/Controllers/HealthController.cs)
- Tests d'intégration de l'endpoint : [`HealthPingTests.cs`](../../tests/ExcelETL.WebAPI.Tests/Health/HealthPingTests.cs)
