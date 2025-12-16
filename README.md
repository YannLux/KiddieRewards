# KiddieRewards

## Pré-requis
- .NET 8 SDK
- SQL Server (local ou via conteneur Docker)

## Base de données SQL Server
1. Lancer une instance locale (exemple Docker) :
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Your_password123" -p 1433:1433 --name kiddierewards-sql -d mcr.microsoft.com/mssql/server:2022-latest
   ```
2. Mettre à jour la chaîne de connexion `DefaultConnection` dans `appsettings.Development.json` si nécessaire, par exemple :
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost,1433;Database=KiddieRewards;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
   }
   ```

## Migrations et mise en place
1. Restaurer les dépendances :
   ```bash
   dotnet restore
   ```
2. Appliquer les migrations et créer la base :
   ```bash
   dotnet ef database update --project PcA.KiddieRewards/PcA.KiddieRewards.Web --startup-project PcA.KiddieRewards/PcA.KiddieRewards.Web
   ```
3. Lancer l'application :
   ```bash
   dotnet run --project PcA.KiddieRewards/PcA.KiddieRewards.Web
   ```

Au démarrage, un service de seed EF Core crée automatiquement les données de démonstration si elles n'existent pas déjà (famille, membres et compte Identity).

## Comptes de test
- Parent owner (Identity) : `owner@demo.local` / `P@ssw0rd!`
- Parent (PIN) : `1234`
- Enfant 1 (PIN) : `1111`
- Enfant 2 (PIN) : `2222`

## Avatars prédéfinis
Les avatars sont référencés par clé :
- `parent-star`
- `lion`
- `panda`
