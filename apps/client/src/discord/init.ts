import {discordSDK} from './sdk';
import type { CommandResponse } from '@discord/embedded-app-sdk';

let auth: CommandResponse<'authenticate'>;

export async function setupDiscordSdk() {
	await discordSDK.ready();

	const { code } = await discordSDK.commands.authorize({
		client_id: import.meta.env.VITE_CLIENT_ID,
		response_type: 'code',
		state: '',
		prompt: 'none',
		// More info on scopes here: https://discord.com/developers/docs/topics/oauth2#shared-resources-oauth2-scopes
		scope: [
			// Activities will launch through app commands and interactions of user-installable apps.
			// https://discord.com/developers/docs/tutorials/developing-a-user-installable-app#configuring-default-install-settings-adding-default-install-settings
			'applications.commands',
			'identify',
            'guilds',
			'guilds.members.read',
            'rpc.voice.read',
		],
	});

	const response = await fetch('/api/discord/token', {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
		},
		body: JSON.stringify({
			code,
		}),
	});

    if (!response.ok) {
        throw new Error(`Failed to retrieve access token: ${response.statusText}`);
    }

	const { access_token } = await response.json();

	auth = await discordSDK.commands.authenticate({
		access_token,
	});

	if (auth == null) {
		throw new Error('Authenticate command failed');
	}
}