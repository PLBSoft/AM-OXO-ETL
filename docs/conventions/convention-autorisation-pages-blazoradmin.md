# Convention — autorisation des pages de `ExcelETL.BlazorAdmin`

*Document vivant (pas de suffixe de date, mis à jour en place — voir
`convention-nommage-documents.md`). Décisions actées avec Simon le 28/07, à la suite du constat
qu'un compte non-Admin fraîchement créé n'avait accès à aucune page fonctionnelle.*

Ce document est la **référence unique** de la posture d'autorisation de l'application. Les tickets
y renvoient plutôt que de la reformuler. Toute page ajoutée à `BlazorAdmin` doit s'y conformer et,
si elle introduit un cas non prévu, ce document est mis à jour **avant** l'écriture du code.

---

## 1. Modèle : deux niveaux, binaire

Il n'existe que **deux** niveaux d'accès, et il n'en existera pas de troisième :

| Niveau | Qui | Portée |
| :--- | :--- | :--- |
| **Admin** | Les comptes du seed (`AdminSeedUsers`) exclusivement | Administration de l'application : utilisateurs et journaux |
| **Authentifié** | Tout compte connecté, avec ou sans rôle | L'intégralité des fonctions métier |

La ligne de partage tient en une phrase : **le rôle Admin gouverne l'administration de
l'application, pas l'usage de l'application.** Un collègue à qui l'administrateur crée un compte
doit pouvoir travailler immédiatement et intégralement ; il ne doit simplement pas pouvoir gérer les
comptes ni consulter les journaux système.

### Ce que ce modèle exclut explicitement

- **Aucun rôle supplémentaire** (« consultation », « opérateur », « lecture seule »). Principe YAGNI
  assumé.
- **Aucune attribution de rôle depuis l'interface.** Les Admin sont ceux du seed, un point c'est
  tout. Décision du lot 044, confirmée le 28/07.
- **Aucune permission intermédiaire de type lecture-seule.** Distinguer « voir un profil » de
  « modifier un profil » reviendrait à créer un second rôle sous forme de règle d'affichage. Un
  compte authentifié qui accède à une page métier y accède **pleinement**, actions destructrices
  comprises.

---

## 2. Inventaire des routes et de leur niveau

| Route | Niveau | Remarque |
| :--- | :--- | :--- |
| `/` et `/import-profiles` | Authentifié | `/` est la route d'accueil ; elle **doit** rester accessible à tout compte connecté (voir §5) |
| `/import-profiles/new`, `/import-profiles/{Id}/edit` | Authentifié | Éditeur complet |
| `/export-profiles` | Authentifié | |
| `/export-profiles/new`, `/export-profiles/{Id:guid}/edit` | Authentifié | Éditeur complet |
| `/import-profiles/test`, `/export-profiles/test` | Authentifié | Pages de test des pipelines d'import/export ; c'est l'outil de travail principal d'un utilisateur non-Admin |
| `/api-test` | Authentifié | Page de test M2M (appel HTTP réel vers le Web API, lot 038) |
| `/generated-files` | Authentifié | Consultation des fichiers archivés (lot 034) |
| `/profile` | Authentifié | Auto-édition, chacun la sienne |
| Déconnexion (`Account/Logout`) | Authentifié | |
| `/users` | **Admin** | Gestion des comptes |
| `/logs` | **Admin** | Journaux système (restriction posée au lot 44.4, réaffirmée au niveau de la page elle-même au lot 052 — voir §6 note) |
| `/Account/Login`, `/Account/AccessDenied` | Anonyme | Page de refus créée au lot 052 (52.3) |
| `/Account/ForcePasswordChange` | Authentifié | Étape obligatoire du premier accès (lots 045/049) |

---

## 3. Deux couches obligatoires, jamais une seule

**Masquer un lien de navigation n'est pas une autorisation.** Toute route protégée l'est à
**deux** niveaux, indépendants et tous deux obligatoires :

1. **Autorisation de la page** — attribut `[Authorize]` (avec `Roles` le cas échéant) sur le
   composant routable. C'est la seule couche qui protège réellement : elle s'applique à une URL
   saisie à la main, à un favori, à un lien partagé.
2. **Visibilité du lien** — `<AuthorizeView>` dans `NavMenu.razor`. C'est du confort d'interface :
   ne pas proposer ce qui mènerait à un refus.

Une page correctement autorisée mais dont le lien reste visible est un défaut d'ergonomie. Un lien
correctement masqué sur une page non autorisée est une **faille**. Les deux couches se testent
séparément : autorisation par test HTTP réel, visibilité par test bUnit.

---

## 4. Règle pour toute page ajoutée

- Toute route **déclare explicitement** son niveau, même quand il coïncide avec le comportement par
  défaut. Aucune page ne repose sur la `FallbackPolicy` globale par omission : ce qui n'est pas
  écrit n'est pas une décision.
- La ligne de conduite en cas de doute : une page qui sert à **utiliser** l'outil est Authentifié ;
  une page qui sert à **administrer l'outil ou ses comptes** est Admin.
- Ce document est mis à jour dans le même lot que la page ajoutée, pas après.

---

## 5. Piège connu : la redirection post-connexion

Après connexion, et après un changement de mot de passe forcé, l'utilisateur est redirigé vers `/`.
**Si `/` n'est pas accessible à son niveau, il est immédiatement refoulé vers `AccessDenied`** —
défaut réellement observé le 28/07 avec le premier compte non-Admin créé.

Conséquence permanente : **`/` doit rester accessible à tout compte authentifié.** Si la page
d'accueil devait un jour être réservée aux Admin, la cible de redirection post-connexion devrait
changer dans le même lot.

---

## 6. Comportement en cas de refus

Un accès refusé mène à `/Account/AccessDenied`, page qui **doit exister** et annoncer un refus
d'accès — jamais une page « Introuvable ».

Les deux situations sont distinctes et ne doivent jamais produire le même message :

| Situation | Sortie attendue |
| :--- | :--- |
| Non authentifié, route quelconque | Redirection vers `/Account/Login` (`FallbackPolicy` globale) |
| Authentifié, droits insuffisants | `/Account/AccessDenied` — « accès refusé » |
| Authentifié, route inexistante | Page « Introuvable » |

Confondre le deuxième et le troisième cas induit en erreur : la ressource existe, c'est l'accès qui
est refusé.

**Note (52.0)** — contradiction documentaire levée : avant ce lot, le lien `#nav-logs-link` de
`NavMenu.razor` était bien dans le bloc `<AuthorizeView Roles="Admin">` (visibilité correcte, conforme
à `etat-des-lieux-technique-2026-07-27.md`), mais l'attribut de la page `/logs` elle-même n'était
qu'un `[Authorize]` sans rôle — un compte authentifié sans rôle pouvait donc atteindre `/logs` en
tapant l'URL directement, malgré un lien masqué. Exactement le piège décrit au §3 : masquer un lien
n'est pas une autorisation. Corrigé au lot 052 (52.1) : `/logs` porte désormais
`[Authorize(Roles = IdentitySeeder.AdminRoleName)]`.
