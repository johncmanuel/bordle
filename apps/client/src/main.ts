import './style.css';
import { GameBoard } from './components/board';
import { GameKeyboard } from './components/keyboard';
import { GameApp } from './components/app';
import { SubmitWordForm } from './components/submitForm';
import { setupDiscordSdk } from './discord/init';
import { getUserAvatar } from './discord/user';
import { createIcons, Settings, Lightbulb } from 'lucide';

const header = document.createElement('header');
header.className = 'top-bar';
header.innerHTML = `
  <div class="top-bar-title">Bordle</div>
  <div class="top-bar-actions">
    <i data-lucide="lightbulb" class="icon-btn" id="hints-btn" title="Hints"></i>
    <i data-lucide="settings" class="icon-btn" id="settings-btn" title="Settings"></i>
  </div>
`;
document.body.insertBefore(header, document.body.firstChild);

createIcons({
  icons: {
    Settings,
    Lightbulb
  }
});

const app = document.querySelector<HTMLDivElement>('#app')!;
app.innerHTML = `
  <div id="profile" style="display: flex; align-items: center; gap: 12px; margin-bottom: -16px; align-self: flex-start; margin-left: 16px;">
    <img id="avatar" src="" alt="Avatar" style="width: 32px; height: 32px; border-radius: 50%; display: none;">
    <span id="username" style="font-weight: bold; font-size: 1.1rem; color: #fff;">Connecting...</span>
    <button id="submit-word-btn" style="display: none;">Submit Word</button>
  </div>
  <game-board></game-board>
  <game-keyboard></game-keyboard>
`;

const submitForm = document.createElement('submit-word-form') as SubmitWordForm;
submitForm.classList.add('view-hidden');
document.body.appendChild(submitForm);

const board = app.querySelector('game-board') as GameBoard;
const keyboard = app.querySelector('game-keyboard') as GameKeyboard;
const submitWordBtn = app.querySelector<HTMLButtonElement>('#submit-word-btn')!;

new GameApp(board, keyboard);

function showGameView() {
  const overlay = submitForm.querySelector('.modal-overlay');
  if (overlay) {
    overlay.classList.add('closing');
    setTimeout(() => {
      submitForm.classList.add('view-hidden');
      overlay.classList.remove('closing');
    }, 200); 
  } else {
    submitForm.classList.add('view-hidden');
  }
}

function showSubmitView() {
  const overlay = submitForm.querySelector('.modal-overlay');
  if (overlay) overlay.classList.remove('closing');
  submitForm.classList.remove('view-hidden');
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
  submitWordBtn.style.display = 'block';
  
}).catch((error) => {
  console.error('Error setting up Discord SDK:', error);
  const usernameSpan = app.querySelector<HTMLSpanElement>('#username')!;
  if (usernameSpan) usernameSpan.textContent = 'Failed to connect';
  // TODO: prevent game functionality and other stuff if not authenticated, or maybe show a message to the user
});