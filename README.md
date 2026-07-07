# Bordle

## Set up dev environment

### Env variables

Create a `.env` file at root based on `.env.example`

### PostgreSQL

Run `docker compose -f docker-compose.dev.yaml up -d` to start the database.

Don't forget to run `cd apps/server`, `dotnet tool run dotnet-ef migrations add <YourMigrationName>`, and `dotnet tool run dotnet-ef database update` if making changes to the DB schema and want to perform migrations.

## Test

### Test files for server

For the server, run `cd apps/server && dotnet test` 

### Test environment in docker (with Tailscale)

To set up and expose the test environment publicly using Tailscale:

1. Start the test containers
   ```bash
   docker compose -f docker-compose.test.yaml up -d --build
   ```
2. Expose the client
   ```bash
   tailscale funnel --bg 8081
   ```
   *(Note: The client application binds to port 8081 in the test environment.)*
3. Under Activites -> URL Mappings -> Root Mapping, map `/` to your designated Tailscale domain on Discord (see for more info: https://docs.discord.com/developers/activities/building-an-activity#set-up-your-activity-url-mapping)
4. Under Activites -> Settings, enable activities on Discord (see for more info: https://docs.discord.com/developers/activities/building-an-activity#enable-activities)
5. Under Overview -> OAuth2 -> Redirects, add your Tailscale domain
5. Start the activity on Discord
