const ROWS = [
  ['Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P'],
  ['A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L'],
  ['ENTER', 'Z', 'X', 'C', 'V', 'B', 'N', 'M', 'BACKSPACE'],
];

type KeyState = 'correct' | 'present' | 'absent';

const STATE_PRIORITY: Record<string, number> = {
  absent: 1,
  present: 2,
  correct: 3,
};

export class GameKeyboard extends HTMLElement {
  private keys = new Map<string, HTMLButtonElement>();
  private keyStates = new Map<string, string>();
  private boundKeyHandler: (e: KeyboardEvent) => void;

  constructor() {
    super();
    this.boundKeyHandler = this.handlePhysicalKey.bind(this);
  }

  connectedCallback() {
    this.render();
    document.addEventListener('keydown', this.boundKeyHandler);
  }

  disconnectedCallback() {
    document.removeEventListener('keydown', this.boundKeyHandler);
  }

  private render() {
    const keyboard = document.createElement('div');
    keyboard.classList.add('keyboard');

    for (const row of ROWS) {
      const rowEl = document.createElement('div');
      rowEl.classList.add('keyboard-row');

      for (const key of row) {
        const btn = document.createElement('button');
        btn.classList.add('key');
        btn.dataset.key = key;

        if (key === 'ENTER' || key === 'BACKSPACE') {
          btn.classList.add('wide');
          btn.textContent = key === 'BACKSPACE' ? '⌫' : 'ENTER';
        } else {
          btn.textContent = key;
        }

        btn.addEventListener('pointerup', () => {
          this.emitKey(key);
        });

        this.keys.set(key, btn);
        rowEl.appendChild(btn);
      }

      keyboard.appendChild(rowEl);
    }

    this.appendChild(keyboard);
  }

  private handlePhysicalKey(e: KeyboardEvent) {
    if (e.ctrlKey || e.metaKey || e.altKey) return;

    // Prevent repeat events from held keys
    if (e.repeat) return;

    // Ignore keystrokes if the user is typing somewhere else like an input field 
    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
      return;
    }

    const key = e.key.toUpperCase();

    if (key === 'ENTER') {
      this.emitKey('ENTER');
    } else if (key === 'BACKSPACE') {
      this.emitKey('BACKSPACE');
    } else if (/^[A-Z]$/.test(key)) {
      // allow only A-Z letters
      this.emitKey(key);
    }
  }

  private emitKey(key: string) {
    this.dispatchEvent(
      new CustomEvent('key-pressed', {
        detail: { key },
        bubbles: true,
        composed: true,
      })
    );
  }

  updateKey(letter: string, state: KeyState) {
    const key = letter.toUpperCase();
    const btn = this.keys.get(key);
    if (!btn) return;

    const currentState = this.keyStates.get(key);

    // do not downgrade to previous state (correct > present > absent)
    if (
      currentState &&
      STATE_PRIORITY[currentState] >= STATE_PRIORITY[state]
    ) {
      return; 
    }

    this.keyStates.set(key, state);
    btn.dataset.state = state;
  }

  reset() {
    this.keyStates.clear();
    for (const [, btn] of this.keys) {
      delete btn.dataset.state;
    }
  }
}

customElements.define('game-keyboard', GameKeyboard);
