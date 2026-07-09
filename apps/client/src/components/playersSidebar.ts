import type { Client } from '../api/client';
import { getUserAvatar } from '../discord/user';

export class PlayersSidebar {
  private container: HTMLElement;
  private listEl: HTMLElement;
  private apiClient: Client;
  private puzzleId: number;

  constructor(container: HTMLElement, apiClient: Client, puzzleId: number) {
    this.container = container;
    this.apiClient = apiClient;
    this.puzzleId = puzzleId;

    this.listEl = this.container.querySelector('.players-list')!;

    this.refresh();
  }

  private async refresh() {
    try {
      const data = await this.apiClient.getApiPuzzlesPlayers(this.puzzleId);

      const serverPlayers = data.players || [];

      const playerEntries = serverPlayers.map((player) => {
        const username = player.username || "Unknown User";
        const avatarUrl = getUserAvatar({ id: player.userId!, avatar: player.avatar });
        return { username, avatarUrl, guessStates: player.guessStates ?? [] };
      });

      // add some dummy data for dev
      if (import.meta.env.DEV) {
        playerEntries.push(
          {
            username: "Bob",
            avatarUrl: getUserAvatar({ id: "123456789" }),
            guessStates: [
              ['absent', 'present', 'absent', 'absent', 'absent'],
              ['absent', 'absent', 'correct', 'present', 'correct']
            ]
          },
          {
            username: "AliceTheGreat",
            avatarUrl: getUserAvatar({ id: "987654321" }),
            guessStates: [
              ['absent', 'absent', 'absent', 'absent', 'absent'],
              ['present', 'present', 'present', 'absent', 'absent'],
              ['correct', 'correct', 'correct', 'correct', 'correct']
            ]
          },
          {
            username: "Charlie",
            avatarUrl: getUserAvatar({ id: "111222333" }),
            guessStates: [
              ['absent', 'absent', 'absent', 'absent', 'absent']
            ]
          }
        );
      }

      this.listEl.innerHTML = '';

      if (playerEntries.length === 0) {
        this.listEl.innerHTML = '<div class="players-empty">No players found.</div>';
        return;
      }

      this.listEl.innerHTML = '';

      for (const entry of playerEntries) {
        const row = document.createElement('div');
        row.className = 'player-row';

        const profileEl = document.createElement('div');
        profileEl.className = 'player-profile';
        
        const avatarEl = document.createElement('img');
        avatarEl.className = 'player-avatar';
        avatarEl.src = entry.avatarUrl;
        avatarEl.alt = entry.username ? `${entry.username}'s avatar` : 'Discord avatar';
        profileEl.appendChild(avatarEl);
        
        row.appendChild(profileEl);

        const gridEl = document.createElement('div');
        gridEl.className = 'mini-grid';

        for (let i = 0; i < 6; i++) {
          const rowEl = document.createElement('div');
          rowEl.className = 'mini-row';
          const guessRow = entry.guessStates[i] || [];
          for (let j = 0; j < 5; j++) {
            const state = guessRow[j] || 'empty';
            const tile = document.createElement('div');
            tile.className = `mini-tile mini-tile-${state}`;
            rowEl.appendChild(tile);
          }
          gridEl.appendChild(rowEl);
        }

        row.appendChild(gridEl);
        this.listEl.appendChild(row);
      }
    } catch (err) {
      console.error('Failed to fetch players:', err);
      this.listEl.innerHTML = '<div class="players-empty">Failed to load players.</div>';
    }
  }

  setPuzzleId(puzzleId: number) {
    this.puzzleId = puzzleId;
    this.refresh();
  }
}
