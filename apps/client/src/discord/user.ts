import type { TTLDiscordUserEntry, DiscordUser} from '../types/discord';
import { baseDiscordCDNUrl } from './init';
import { discordSDK } from './sdk';

// "In the case of the Default User Avatar endpoint, the value for index depends on whether the user 
// is migrated to the new username system. For users on the new username system, index will be (user_id >> 22) % 6.
// For users on the legacy username system, index will be discriminator % 5."
// https://docs.discord.com/developers/reference#image-formatting
function getDefaultAvatarIndex(id: string, discriminator?: string): number {
  // legacy username system 
  if (discriminator && discriminator !== "0") {
    return Number(discriminator) % 5;
  }

  // migrated / new username system
  return Number((BigInt(id) >> BigInt(22)) % BigInt(6)); 
}

export function getUserAvatar(user: Pick<DiscordUser, 'id'> & Partial<Pick<DiscordUser, 'avatar' | 'discriminator'>>): string {
  if (user.avatar != null) {
    // "In the case of endpoints that support GIFs, the hash will begin with a_ if it is available in an animated format
    // (example: a_1269e74af4df7417b13759eae50c83dc). These images can be retrieved as animated WebP using the .webp file 
    // extension and ?animated=true querystring parameter."
    // https://docs.discord.com/developers/reference#image-formatting
    const isAnimated = user.avatar.startsWith('a_');
    const url = `${baseDiscordCDNUrl}/avatars/${user.id}/${user.avatar}.webp` 
    return isAnimated ? `${url}?animated=true` : url;
  } else {
    const defaultAvatarIndex = getDefaultAvatarIndex(user.id, user.discriminator);
    return `${baseDiscordCDNUrl}/embed/avatars/${defaultAvatarIndex}.png`; // default user avatar is PNG only
  }
}

const TTL_MS = 10 * 60 * 1000;
const usernameCache = new Map<string, TTLDiscordUserEntry>();

export async function getDiscordUsername(userId: string): Promise<string> {
  const now = Date.now();
  const cached = usernameCache.get(userId);

  if (cached && cached.expiresAt > now) {
    return cached.username;
  }

  try {
    const response = await discordSDK.commands.getUser({ id: userId });
    if (!response) throw new Error(`No response from Discord SDK for user ID: ${userId}`);

    const username = response.username;
    
    usernameCache.set(userId, {
      username,
      expiresAt: now + TTL_MS
    });

    return username;
  } catch (error) {
    console.error(`Failed to fetch user ${userId} from Discord SDK:`, error);
    return "Unknown User";
  }
}
