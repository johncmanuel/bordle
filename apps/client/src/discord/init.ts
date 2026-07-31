import { discordSDK } from "./sdk";
import type { CommandResponse } from "@discord/embedded-app-sdk";
import { Client } from "../api/client";

let auth: CommandResponse<"authenticate">;

export const baseDiscordCDNUrl = "https://cdn.discordapp.com";
export const baseDiscordApiUrl = "https://discord.com/api";
export const SESSION_TOKEN_KEY = "bordle_session_token";

export async function setupDiscordSdk() {
  console.log("[Discord] Initialized with Client ID:", import.meta.env.VITE_DISCORD_CLIENT_ID);
  console.log("[Discord] SDK clientId property:", (discordSDK as any).clientId);

  let isReady = false;
  let attempts = 0;
  while (!isReady && attempts < 5) {
    try {
      attempts++;
      console.log(`[Discord] Waiting for SDK ready (attempt ${attempts})...`);

      // The SDK constructor sends HANDSHAKE immediately, which gets dropped if the app loads too fast.
      // Resend it here manually on every request.
      if (attempts > 1) {
        console.log("[Discord] Re-sending handshake to parent...");
        (discordSDK as any).handshake();
      }

      // The SDK doesn't have a built-in timeout for ready(), so race it
      await Promise.race([
        discordSDK.ready(),
        new Promise((_, reject) =>
          setTimeout(() => reject(new Error("Timeout waiting for handshake")), 2000),
        ),
      ]);

      isReady = true;
    } catch (e) {
      console.warn(`[Discord] SDK ready attempt ${attempts} failed:`, e);
      await new Promise((r) => setTimeout(r, 500));
    }
  }

  if (!isReady) {
    throw new Error(
      "Discord SDK failed to become ready after multiple attempts. The handshake was ignored.",
    );
  }

  console.log("[Discord] SDK ready.");

  console.log("[Discord] Requesting authorization...");
  const { code } = await discordSDK.commands.authorize({
    client_id: import.meta.env.VITE_DISCORD_CLIENT_ID,
    response_type: "code",
    state: "",
    prompt: "none",
    // More info on scopes here: https://discord.com/developers/docs/topics/oauth2#shared-resources-oauth2-scopes
    scope: [
      // Activities will launch through app commands and interactions of user-installable apps.
      // https://discord.com/developers/docs/tutorials/developing-a-user-installable-app#configuring-default-install-settings-adding-default-install-settings
      "applications.commands",
      "identify",
      "guilds",
      "guilds.members.read",
    ],
  });
  console.log("[Discord] Authorization code received:", code?.slice(0, 8) + "...");

  const guildId = discordSDK.guildId;
  if (!guildId) {
    throw new Error("Guild ID is not available from Discord SDK");
  }

  console.log("[Discord] Fetching token from server...");
  const apiClient = new Client(""); // Base URL is empty for relative paths
  const tokenResponse = await apiClient.postApiDiscordToken({
    code,
    guild_id: guildId,
  });

  console.log("[Discord] Token received from server.");

  const { access_token, session_token } = tokenResponse;

  if (!access_token || !session_token) {
    throw new Error("Failed to parse access token from server response.");
  }

  sessionStorage.setItem(SESSION_TOKEN_KEY, session_token);

  console.log("[Discord] Authenticating with Discord SDK...");
  auth = await discordSDK.commands.authenticate({
    access_token,
  });

  if (auth == null) {
    throw new Error("Authenticate command failed");
  }
  console.log("[Discord] Authentication complete.");

  return auth;
}
