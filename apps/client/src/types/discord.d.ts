import type { CommandResponse } from '@discord/embedded-app-sdk';

type Auth = CommandResponse<'authenticate'>;

export type DiscordUser = Auth['user'];

export interface TTLDiscordUserEntry {
  username: string;
  expiresAt: number;
} 
