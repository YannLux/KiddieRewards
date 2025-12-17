# Flux d'Onboarding - Kiddie Rewards

## Vue d'ensemble

Lorsqu'un utilisateur se connecte pour la première fois, s'il n'est pas assigné à une famille, il sera automatiquement redirigé vers une page d'onboarding où il peut :

1. **Créer une nouvelle famille** - Devenir le propriétaire de la famille
2. **Rejoindre une famille existante** - Via un code d'invitation

## Architecture

### Middleware : EnsureFamilyMiddleware

Le middleware `EnsureFamilyMiddleware` vérifie pour chaque requête entrante :
- Si l'utilisateur est authentifié
- Si l'utilisateur a un membre assigné dans la base de données
- Si non, il redirige vers `/Home/OnboardingFamily`

Routes exclues du middleware :
- `/identity/*` - Pages d'authentification Identity
- `/home/onboarding*` - Pages d'onboarding
- `/home/createfamily` - Page de création de famille
- `/home/joinfamily` - Page de rejoindre famille
- `/home/privacy` - Page de confidentialité
- `/api/*` - Routes API
- `/static/*` - Assets statiques

### Contrôleur : HomeController

Contient 4 actions principales :

#### 1. Index (Page d'accueil)
- `GET /` - Page d'accueil publique avec présentation
- Redirige automatiquement les utilisateurs connectés sans famille vers `OnboardingFamily`

#### 2. OnboardingFamily
- `GET /Home/OnboardingFamily` - Page d'onboarding avec deux options
- Permet de choisir entre créer ou rejoindre une famille

#### 3. CreateFamily
- `GET /Home/CreateFamily` - Formulaire de création de famille
- `POST /Home/CreateFamily` - Traitement de la création
  - Crée une nouvelle `Family`
  - Crée un nouveau `Member` associé à l'utilisateur authentifié
  - Assigne le rôle `Parent`
  - Redirige vers le tableau de bord parent

#### 4. JoinFamily
- `GET /Home/JoinFamily` - Formulaire pour rejoindre une famille
- `POST /Home/JoinFamily` - Traitement de l'adhésion
  - Valide le code d'invitation
  - Crée un nouveau `Member` dans la famille existante
  - Assigne le rôle `Parent`
  - Redirige vers le tableau de bord parent

### ViewModels

#### CreateFamilyViewModel
```csharp
public record CreateFamilyViewModel
{
    public string FamilyName { get; init; }              // Nom de la famille
    public string ParentDisplayName { get; init; }       // Nom du parent
    public string ParentPin { get; init; }               // Code PIN (4-10 chiffres)
    public string? AvatarKey { get; init; }              // Avatar optionnel
}
```

#### JoinFamilyViewModel
```csharp
public record JoinFamilyViewModel
{
    public string FamilyInvitationCode { get; init; }    // Code d'invitation
    public string DisplayName { get; init; }             // Nom du parent
    public string Pin { get; init; }                     // Code PIN (4-10 chiffres)
    public string? AvatarKey { get; init; }              // Avatar optionnel
}
```

## Flux utilisateur

### Scénario 1 : Créer une nouvelle famille

1. L'utilisateur se connecte (Identity)
2. Il accède à une page protégée (ex: `/Parent/Dashboard`)
3. Le middleware le redirige vers `/Home/OnboardingFamily`
4. Il clique sur "Créer une famille"
5. Il remplit le formulaire :
   - Nom de la famille
   - Son nom (parent)
   - Son code PIN
   - Avatar optionnel
6. La famille et le membre sont créés en base
7. Il est redirigé vers le tableau de bord parent

### Scénario 2 : Rejoindre une famille existante

1. L'utilisateur se connecte (Identity)
2. Il accède à une page protégée
3. Le middleware le redirige vers `/Home/OnboardingFamily`
4. Il clique sur "Rejoindre une famille"
5. Il remplit le formulaire :
   - Code d'invitation (peut être le nom de la famille pour MVP)
   - Son nom (parent)
   - Son code PIN
   - Avatar optionnel
6. Le membre est créé dans la famille existante en base
7. Il est redirigé vers le tableau de bord parent

## Modifications au contrôleur

Les contrôleurs parents (`ParentDashboardController`, `ParentMembersController`) ont été mis à jour pour utiliser une méthode `TryGetFamilyId()` améliorée qui :

1. Essaie d'abord d'utiliser le claim `FamilyId` (pour compatibilité)
2. Sinon, interroge la base de données pour retrouver la FamilyId à partir de l'ID du membre actuel

Cela rend le système plus robuste et ne dépend pas de claims spécifiques.

## Points clés

- ? Utilisateurs connectés sans famille ? Onboarding automatique
- ? Support de création de nouvelle famille
- ? Support de rejoindre une famille existante
- ? Assignation automatique du rôle Parent
- ? Stockage sécurisé des PIN avec hash
- ? Validation du PIN (4-10 chiffres)
- ? Interface utilisateur claire et intuitive
- ? Routes d'onboarding exclues du middleware
