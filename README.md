# Bordle

## Set up dev environment

### Env variables

Create a `.env` file at root based on `.env.example`

### PostgreSQL

Since database migrations are ran automatically in the entry point, `Program.cs`, ensure the Postgres DB is started with:

```sh
docker compose -f docker-compose.dev.yaml up -d
```

> If you want to apply migrations beforehand, run `cd apps/server/`, `dotnet tool run dotnet-ef migrations add <YourMigrationName>` and `dotnet tool run dotnet-ef database update`. 

## .NET server

If you want to run the server without NSwag, use:

```sh
dotnet run --project=./apps/server/ -p:RunNSwag=False
```

If you want to run the server and register the Discord slash commands:

```sh
# we use -v d for detailed logging just incase errors occur
dotnet run --project=./apps/server/  --register-commands -p:RunNSwag=False -v d`
```

## Test

### Test files for server

For the server, run `cd apps/server && dotnet test` 

### Test environment in docker (with Tailscale)

Tailscale is one of the tools that can let us publicly expose the client URL with HTTPS, which Discord requires.

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
