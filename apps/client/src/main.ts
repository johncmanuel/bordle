import './style.css';
import { GameBoard } from './components/board';
import { GameKeyboard } from './components/keyboard';
import { GameApp } from './components/app';
import { setupDiscordSdk } from './discord/init';
import { getUserAvatar } from './discord/user';

const app = document.querySelector<HTMLDivElement>('#app')!;
app.innerHTML = `
  <div id="profile" style="display: flex; align-items: center; gap: 12px; margin-bottom: -16px; align-self: flex-start; margin-left: 16px;">
    <img id="avatar" src="" alt="Avatar" style="width: 32px; height: 32px; border-radius: 50%; display: none;">
    <span id="username" style="font-weight: bold; font-size: 1.1rem; color: #fff;">Connecting...</span>
  </div>
  <game-board></game-board>
  <game-keyboard></game-keyboard>
`;

const board = app.querySelector('game-board') as GameBoard;
const keyboard = app.querySelector('game-keyboard') as GameKeyboard;

new GameApp(board, keyboard);

setupDiscordSdk().then((auth) => {
  console.log('Discord SDK setup complete');
  const user = auth.user;
  
  const profileDiv = app.querySelector<HTMLDivElement>('#profile')!;
  const avatarImg = profileDiv.querySelector<HTMLImageElement>('#avatar')!;
  const usernameSpan = profileDiv.querySelector<HTMLSpanElement>('#username')!;

  usernameSpan.textContent = user.username;
 
  const avatarUrl = getUserAvatar(user);

  avatarImg.src = avatarUrl;
  avatarImg.style.display = 'block';
  
}).catch((error) => {
  console.error('Error setting up Discord SDK:', error);
  const usernameSpan = app.querySelector<HTMLSpanElement>('#username')!;
  if (usernameSpan) usernameSpan.textContent = 'Failed to connect';
});