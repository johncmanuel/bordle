import './style.css';
import { GameBoard } from './components/board';
import { GameKeyboard } from './components/keyboard';
import { GameApp } from './components/app';
import { setupDiscordSdk } from './discord/init';

setupDiscordSdk().then(() => {
  console.log('Discord SDK setup complete');
}).catch((error) => {
  console.error('Error setting up Discord SDK:', error);
});

const app = document.querySelector<HTMLDivElement>('#app')!;
app.innerHTML = `
  <game-board></game-board>
  <game-keyboard></game-keyboard>
`;

const board = app.querySelector('game-board') as GameBoard;
const keyboard = app.querySelector('game-keyboard') as GameKeyboard;

new GameApp(board, keyboard);