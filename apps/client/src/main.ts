import './style.css';
import { GameBoard } from './components/board';
import { GameKeyboard } from './components/keyboard';
import { GameApp } from './components/app';
import { SubmitWordForm } from './components/submitForm';
import { setupDiscordSdk } from './discord/init';
import { getUserAvatar } from './discord/user';

const app = document.querySelector<HTMLDivElement>('#app')!;
app.innerHTML = `
  <div id="profile" style="display: flex; align-items: center; gap: 12px; margin-bottom: -16px; align-self: flex-start; margin-left: 16px;">
    <img id="avatar" src="" alt="Avatar" style="width: 32px; height: 32px; border-radius: 50%; display: none;">
    <span id="username" style="font-weight: bold; font-size: 1.1rem; color: #fff;">Connecting...</span>
    <button id="submit-word-btn" style="display: none;">Submit Word</button>
  </div>
  <game-board></game-board>
  <game-keyboard></game-keyboard>
  <submit-word-form class="view-hidden"></submit-word-form>
`;

const board = app.querySelector('game-board') as GameBoard;
const keyboard = app.querySelector('game-keyboard') as GameKeyboard;
const submitForm = app.querySelector('submit-word-form') as SubmitWordForm;
const submitWordBtn = app.querySelector<HTMLButtonElement>('#submit-word-btn')!;

new GameApp(board, keyboard);

function showGameView() {
  board.classList.remove('view-hidden');
  keyboard.classList.remove('view-hidden');
  submitForm.classList.add('view-hidden');
  submitWordBtn.textContent = 'Submit Word';
}

function showSubmitView() {
  board.classList.add('view-hidden');
  keyboard.classList.add('view-hidden');
  submitForm.classList.remove('view-hidden');
  submitWordBtn.textContent = 'Back to Game';
}

submitWordBtn.addEventListener('click', () => {
  const isSubmitVisible = !submitForm.classList.contains('view-hidden');
  if (isSubmitVisible) {
    showGameView();
  } else {
    showSubmitView();
  }
});

submitForm.addEventListener('submit-cancel', () => showGameView());
submitForm.addEventListener('submit-success', () => showGameView());

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

  // Show submit button once authenticated
  submitWordBtn.style.display = 'block';
  
}).catch((error) => {
  console.error('Error setting up Discord SDK:', error);
  const usernameSpan = app.querySelector<HTMLSpanElement>('#username')!;
  if (usernameSpan) usernameSpan.textContent = 'Failed to connect';
  // TODO: prevent game functionality if not authenticated, or maybe show a message to the user
});