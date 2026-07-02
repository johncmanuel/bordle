import './style.css';
import { GameBoard } from './components/board';
import { GameKeyboard } from './components/keyboard';
import { GameApp } from './components/app';
import { SubmitWordForm } from './components/submitForm';
import { HintsForm } from './components/hintsForm';
import { PlayersSidebar } from './components/playersSidebar';
import { setupDiscordSdk, SESSION_TOKEN_KEY } from './discord/init';
import { getUserAvatar } from './discord/user';
import { createIcons, Settings, Lightbulb, CirclePlus, Menu, X } from 'lucide';
import { Client } from './api/client';

// TODO: modularize various parts of this file into separate pieces 

const sidebarEl = document.createElement('aside');
sidebarEl.id = 'players-sidebar';
sidebarEl.innerHTML = `<div class="players-list"></div>`;
document.body.appendChild(sidebarEl);

const sidebarOverlay = document.createElement('div');
sidebarOverlay.className = 'sidebar-overlay';
document.body.appendChild(sidebarOverlay);

const header = document.createElement('header');
header.className = 'top-bar';
header.innerHTML = `
  <div class="top-bar-left">
    <i data-lucide="menu" class="icon-btn hamburger-btn" id="hamburger-btn" title="Players"></i>
    <div class="top-bar-title">Bordle</div>
  </div>
  <div class="top-bar-center" id="profile">
    <img id="avatar" src="" alt="Avatar" style="width: 24px; height: 24px; border-radius: 50%; display: none;">
    <span id="username" style="font-weight: 600; font-size: 0.9rem; color: #fff;">Connecting...</span>
  </div>
  <div class="top-bar-actions">
    <i data-lucide="circle-plus" class="icon-btn" id="submit-word-btn" title="Submit Word"></i>
    <i data-lucide="lightbulb" class="icon-btn" id="hints-btn" title="Hints"></i>
    <i data-lucide="settings" class="icon-btn" id="settings-btn" title="Settings"></i>
  </div>
`;
document.body.insertBefore(header, document.body.firstChild);

createIcons({
  icons: {
    Settings,
    Lightbulb,
    CirclePlus,
    Menu,
    X
  }
});

const hamburgerBtn = document.querySelector<HTMLElement>('#hamburger-btn')!;
hamburgerBtn.addEventListener('click', () => {
  const isOpen = sidebarEl.classList.contains('open');
  if (isOpen) {
    sidebarEl.classList.remove('open');
    sidebarOverlay.classList.remove('active');
  } else {
    sidebarEl.classList.add('open');
    sidebarOverlay.classList.add('active');
  }
});

sidebarOverlay.addEventListener('click', () => {
  sidebarEl.classList.remove('open');
  sidebarOverlay.classList.remove('active');
});

const app = document.querySelector<HTMLDivElement>('#app')!;
app.innerHTML = `
  <game-board></game-board>
  <game-keyboard></game-keyboard>
`;

const submitForm = document.createElement('submit-word-form') as SubmitWordForm;
submitForm.classList.add('view-hidden');
document.body.appendChild(submitForm);

const hintsForm = document.createElement('hints-form') as HintsForm;
hintsForm.classList.add('view-hidden');
document.body.appendChild(hintsForm);

const board = app.querySelector('game-board') as GameBoard;
const keyboard = app.querySelector('game-keyboard') as GameKeyboard;
const submitWordBtn = document.querySelector<HTMLElement>('#submit-word-btn')!;

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

const hintsBtn = document.querySelector<HTMLElement>('#hints-btn')!;
hintsBtn.addEventListener('click', () => {
  const isHintsVisible = !hintsForm.classList.contains('view-hidden');
  if (isHintsVisible) {
    const overlay = hintsForm.querySelector('.modal-overlay');
    if (overlay) {
      overlay.classList.add('closing');
      setTimeout(() => {
        hintsForm.classList.add('view-hidden');
        overlay.classList.remove('closing');
      }, 200); 
    } else {
      hintsForm.classList.add('view-hidden');
    }
  } else {
    const overlay = hintsForm.querySelector('.modal-overlay');
    if (overlay) overlay.classList.remove('closing');
    hintsForm.classList.remove('view-hidden');
  }
});

hintsForm.addEventListener('hints-close', () => {
  const overlay = hintsForm.querySelector('.modal-overlay');
  if (overlay) {
    overlay.classList.add('closing');
    setTimeout(() => {
      hintsForm.classList.add('view-hidden');
      overlay.classList.remove('closing');
    }, 200); 
  } else {
    hintsForm.classList.add('view-hidden');
  }
});

setupDiscordSdk().then(async (auth) => {
  console.log('Discord SDK setup complete');
  const user = auth.user;
  
  const profileDiv = header.querySelector<HTMLDivElement>('#profile')!;
  const avatarImg = profileDiv.querySelector<HTMLImageElement>('#avatar')!;
  const usernameSpan = profileDiv.querySelector<HTMLSpanElement>('#username')!;

  usernameSpan.textContent = user.username;
 
  const avatarUrl = getUserAvatar(user);

  avatarImg.src = avatarUrl;

  avatarImg.style.display = 'block';

  const sessionToken = sessionStorage.getItem(SESSION_TOKEN_KEY);
  const apiClient = new Client('', {
    fetch: (url: RequestInfo, init?: RequestInit) => {
      const headers = new Headers(init?.headers);
      if (sessionToken) {
        headers.set('Authorization', `Bearer ${sessionToken}`);
      }
      return window.fetch(url, { ...init, cache: 'no-store', headers });
    }
  });

  try {
    const puzzle = await apiClient.getApiPuzzlesDaily();
    console.log("puzzle fetched:", puzzle);
    hintsForm.setHints(puzzle.hints ?? []);

    new PlayersSidebar(sidebarEl, apiClient, puzzle.puzzleId!);
    new GameApp(board, keyboard, apiClient, puzzle);
  } catch (err) {
    console.warn('No daily puzzle available:', err);
    usernameSpan.textContent = `No puzzle today!`;
  }

}).catch((error) => {
  console.error('Error setting up Discord SDK:', error);
  const usernameSpan = app.querySelector<HTMLSpanElement>('#username')!;
  if (usernameSpan) usernameSpan.textContent = 'Failed to connect';
  // TODO: prevent game functionality and other stuff if not authenticated, or maybe show a message to the user
});