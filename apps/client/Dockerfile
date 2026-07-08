FROM node:22-alpine AS build
WORKDIR /src

COPY apps/client/package*.json apps/client/
WORKDIR /src/apps/client
RUN npm install

COPY apps/client/ .

# Use a placeholder that gets replaced at runtime by entrypoint.sh,
# keeping the image generic and self-hostable.
ENV VITE_DISCORD_CLIENT_ID=__VITE_DISCORD_CLIENT_ID__

RUN npm run build

FROM caddy:alpine

COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/apps/client/dist /srv
COPY apps/client/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
