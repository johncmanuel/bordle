FROM node:22-alpine AS build
WORKDIR /src

COPY apps/client/package*.json apps/client/
WORKDIR /src/apps/client
RUN npm install

COPY apps/client/ .
ARG VITE_DISCORD_CLIENT_ID
ENV VITE_DISCORD_CLIENT_ID=$VITE_DISCORD_CLIENT_ID
RUN npm run build

FROM caddy:alpine

COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/apps/client/dist /srv

EXPOSE 8080
