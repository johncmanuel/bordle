FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY apps/server/server.csproj apps/server/
RUN dotnet restore apps/server/server.csproj

COPY apps/server/ apps/server/
WORKDIR /src/apps/server

ARG CONFIGURATION=Release
RUN dotnet publish -c $CONFIGURATION -o /app/publish -p:RunNSwag=False

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["./server"]
