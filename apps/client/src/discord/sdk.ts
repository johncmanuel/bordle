// very useful reference for Discord SDK usage:
// https://github.com/colyseus/discord-activity/blob/main/apps/client/src/utils/DiscordSDK.ts

import { DiscordSDK, DiscordSDKMock } from '@discord/embedded-app-sdk';

const DISCORD_CLIENT_ID = import.meta.env.VITE_DISCORD_CLIENT_ID;

if (!DISCORD_CLIENT_ID) {
  throw new Error("VITE_DISCORD_CLIENT_ID is not defined in environment variables");
}

const queryParams = new URLSearchParams(window.location.search);
const isEmbedded = queryParams.get("frame_id") != null;

let discordSDK: DiscordSDK | DiscordSDKMock;

if (isEmbedded) {
  discordSDK = new DiscordSDK(DISCORD_CLIENT_ID);
} else {
  // @ts-ignore: shouldn't be a problem with the typings here
  enum SessionStorageQueryParam {
    user_id = "user_id",
    guild_id = "guild_id",
    channel_id = "channel_id",
  }

  function getOverrideOrRandomSessionValue(queryParam: `${SessionStorageQueryParam}`) {
    const overrideValue = queryParams.get(queryParam);
    if (overrideValue != null) {
      return overrideValue;
    }

    const currentStoredValue = sessionStorage.getItem(queryParam);
    if (currentStoredValue != null) {
      return currentStoredValue;
    }

    // Set queryParam to a mock Discord snowflake
    // https://docs.discord.com/developers/reference#snowflakes
    //
    // A Discord Snowflake is a 64-bit integer that is composed of several parts:
    // 1. Timestamp (42 bits): Milliseconds since the Discord Epoch (the first second of 2015).
    // 2. Worker ID (5 bits): Internal Discord worker ID.
    // 3. Process ID (5 bits): Internal Discord process ID.
    // 4. Increment (12 bits): For every ID generated on that process, this number is incremented.
    //
    // Data contained within the snowflake like worker id isn't relevant for mocks
    // but it's good to know where it's coming from tbh.
    const discordEpoch = BigInt("1420070400000"); // first second of 2015
    const timestamp = BigInt(Date.now()) - discordEpoch;
    // 2^22 is composed of the last 3 components of the snowflake (worker id, process id, increment)
    // (5 bits + 5 bits + 12 bits = 22 bits)
    const randomBits = BigInt(Math.floor(Math.random() * (2**22))); 
    const mockSnowflake = ((timestamp << BigInt(22)) | randomBits).toString(); // timestamp occupies the first 42 bits, the rest of the bits (22) is random 
    
    sessionStorage.setItem(queryParam, mockSnowflake);

    return mockSnowflake;
  }

  const mockUserId = getOverrideOrRandomSessionValue("user_id");
  const mockGuildId = getOverrideOrRandomSessionValue("guild_id");
  const mockChannelId = "dummyChannelId";

  discordSDK = new DiscordSDKMock(DISCORD_CLIENT_ID, mockGuildId, mockChannelId, "en");
  const discriminator = String(mockUserId.charCodeAt(0) % 5);

  discordSDK._updateCommandMocks({
    authenticate: async () => {
      return {
        access_token: "mock_token",
        user: {
          username: mockUserId.slice(0, 8),
          discriminator,
          id: mockUserId,
          avatar: null,
          public_flags: 1,
        },
        scopes: [],
        expires: new Date(2112, 1, 1).toString(),
        application: {
          description: "mock_app_description",
          icon: "mock_app_icon",
          id: "mock_app_id",
          name: "mock_app_name",
        },
      };
    },
  });
}

export { discordSDK, isEmbedded };