import { baseDiscordCDNUrl } from './init';

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
