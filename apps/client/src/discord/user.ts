import type { TTLDiscordUserEntry } from '../types/discord';
import { baseDiscordCDNUrl } from './init';
import { discordSDK } from './sdk';

export function getUserAvatar(user: { id: string; avatar?: string | null }): string {
  let avatarUrl = '';
  if (user.avatar != null) {
    avatarUrl = `${baseDiscordCDNUrl}/avatars/${user.id}/${user.avatar}.webp`;
  } else {
    // "In the case of the Default User Avatar endpoint, the value for index depends on whether the user 
    // is migrated to the new username system. For users on the new username system, index will be (user_id >> 22) % 6.
    // For users on the legacy username system, index will be discriminator % 5."
    const defaultAvatarIndex = Math.abs(Number(user.id) >> 22) % 6 || 0;
    avatarUrl = `${baseDiscordCDNUrl}/embed/avatars/${defaultAvatarIndex}.webp`;
  }
  return avatarUrl;
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
