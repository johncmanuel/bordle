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
import { isEmbedded } from './discord/sdk';
import { Client } from './api/client';

// TODO: modularize various parts of this file into separate pieces 

const sidebarEl = document.createElement('aside');
sidebarEl.id = 'players-sidebar';
sidebarEl.innerHTML = `<div class="players-list"></div>`;
document.body.appendChild(sidebarEl);

const sidebarOverlay = document.createElement('div');
sidebarOverlay.className = 'sidebar-overlay';
document.body.appendChild(sidebarOverlay);

if (isEmbedded) {
  document.body.classList.add('is-embedded');
}
document.body.classList.add('is-connecting');

const connectingOverlay = document.createElement('div');
connectingOverlay.id = 'connecting-overlay';
connectingOverlay.innerHTML = `
  <div style="font-size: 1.2rem; font-weight: bold; color: rgba(255, 255, 255, 0.7);">Connecting...</div>
`;
document.body.appendChild(connectingOverlay);

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
  <div id="puzzle-info" style="display: flex; justify-content: center; flex-wrap: wrap; gap: 6px; font-size: 0.8rem; color: #818384; margin-top: 12px; margin-bottom: 8px; font-weight: bold; font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;"></div>
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
  document.body.classList.remove('is-connecting');
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
    hintsForm.setHints(puzzle.hints ?? [], puzzle.puzzle_id!);

    const puzzleInfo = app.querySelector<HTMLDivElement>('#puzzle-info')!;
    const today = new Date().toLocaleDateString(undefined, {
      month: 'long', day: 'numeric', year: 'numeric'
    });
    puzzleInfo.innerHTML = `<span>No. ${puzzle.sequence_number}</span><span>&bull;</span><span>${today}</span>`;

    new PlayersSidebar(sidebarEl, apiClient, puzzle.puzzle_id!);
    new GameApp(board, keyboard, apiClient, puzzle);
  } catch (err) {
    console.warn('No daily puzzle available:', err);
    const noPuzzleOverlay = document.createElement('div');
    noPuzzleOverlay.className = 'modal-overlay';
    noPuzzleOverlay.innerHTML = `
      <div class="modal-content" style="text-align: center; padding: 32px 24px;">
        <h2 class="submit-title">Bordle</h2>
        <p style="margin-top: 24px; font-size: 1.2rem; color: var(--text-primary);">No puzzle today!</p>
        <p style="margin-top: 12px; font-size: 0.95rem; color: #818384;">Check back tomorrow for a new word.</p>
      </div>
    `;
    document.body.appendChild(noPuzzleOverlay);
  }
}).catch((error) => {
  document.body.classList.remove('is-connecting');
  console.error('Error setting up Discord SDK:', error);
  const failedOverlay = document.createElement('div');
  failedOverlay.className = 'modal-overlay';
  failedOverlay.innerHTML = `
    <div class="modal-content" style="text-align: center; padding: 32px 24px;">
      <h2 class="submit-title">Bordle</h2>
      <p style="margin-top: 24px; font-size: 1.2rem; color: var(--text-primary);">Failed to connect</p>
      <p style="margin-top: 12px; font-size: 0.95rem; color: #818384;">Could not connect to Discord. Please try again later.</p>
    </div>
  `;
  document.body.appendChild(failedOverlay);
});