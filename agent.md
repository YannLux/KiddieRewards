# Application : Gestion de bons et mauvais points (Famille)

## Vision
Application web permettant aux parents de gérer des bons et mauvais points pour les enfants d’une famille.
Les enfants ne peuvent que consulter leurs informations.

## Stack technique
- ASP.NET Core MVC
- .NET 10
- Bootstrap 5
- SQL Server
- Entity Framework Core (Code First)
- ASP.NET Core Identity (email + mot de passe pour au moins un parent)

## Périmètre fonctionnel (MVP)
- Gestion des enfants (pseudo + avatar prédéfini).
- Ajout et correction de lignes de points positives ou négatives par les parents.
- Consultation des totaux (+ / - / net) et de l’historique par les enfants.
- Historique éditable par les parents.
- Système de reset via ligne d’historique.

## Hors périmètre explicite
- Aucun objectif.
- Aucune statistique.
- Aucune notification.
- Aucun export.
- Aucune récompense dédiée (pour l’instant).
- Aucun upload d’avatar (liste prédéfinie uniquement).

## Concepts clés
- Une ligne = un événement historique (PointEntry).
- Les points sont SIGNÉS (positif / négatif).
- Aucune suppression hard, uniquement désactivation (IsActive).
- Les totaux sont calculés uniquement sur les lignes actives.

## Rôles
- Parent :
  - Gère les enfants.
  - Ajoute / modifie des points.
  - Peut déclencher un reset.
- Enfant :
  - Consultation uniquement.

## Authentification
- Au moins un parent owner via email + mot de passe (Identity).
- Connexion alternative via code PIN pour les membres.
- Chaque membre possède un PIN hashé.
- Le PIN est unique au sein d’une famille.

## Avatars
- Avatars prédéfinis côté site.
- Stockés en statique (wwwroot).
- Référencés via une clé (AvatarKey).

## Contraintes générales
- Application MVC classique (Controllers + Views Razor).
- Code simple, lisible, maintenable.
- Pas de sur-architecture.
