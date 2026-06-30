import type {KeyState} from "../types/state"

const MAX_ROWS = 6;
const MAX_COLS = 5;

export class GameBoard extends HTMLElement {
  private currRow = 0;
  private currCol = 0;
  private tiles: HTMLDivElement[][] = [];
  private gameOver = false;

  constructor() {
    super();
  }

  connectedCallback() {
    this.render();
  }

  private render() {
    const board = document.createElement('div');
    board.classList.add('board');

    for (let r = 0; r < MAX_ROWS; r++) {
      const row: HTMLDivElement[] = [];
      for (let c = 0; c < MAX_COLS; c++) {
        const tile = document.createElement('div');
        tile.classList.add('tile');
        tile.dataset.row = String(r);
        tile.dataset.col = String(c);
        board.appendChild(tile);
        row.push(tile);
      }
      this.tiles.push(row);
    }

    this.appendChild(board);
  }

  addLetter(letter: string) {
    if (this.gameOver || this.currCol >= MAX_COLS) return;

    const tile = this.tiles[this.currRow][this.currCol];
    tile.textContent = letter.toUpperCase();
    tile.dataset.state = 'tbd';

    // play pop animation then force reflow so the animation replays if called rapidly
    tile.classList.remove('pop');
    void tile.offsetWidth;
    tile.classList.add('pop');

    this.currCol++;
  }

  removeLetter() {
    if (this.gameOver || this.currCol <= 0) return;

    this.currCol--;

    const tile = this.tiles[this.currRow][this.currCol];
    tile.textContent = '';
    delete tile.dataset.state;
    tile.classList.remove('pop');
  }

  getCurrentGuess(): string {
    return this.tiles[this.currRow]
      .map(t => t.textContent ?? '')
      .join('');
  }

  isRowFull(): boolean {
    return this.currCol === MAX_COLS;
  }

  isGameOver(): boolean {
    return this.gameOver;
  }

  // shakes the current row to indicate an invalid guess
  shakeCurrentRow() {
    const row = this.tiles[this.currRow];
    const board = this.querySelector('.board')!;
    
    board.classList.remove('row-shake');
    void (board as HTMLElement).offsetWidth;
    
    row.forEach(tile => {
      tile.style.animation = 'none';
      void tile.offsetWidth;
      tile.style.animation = 'shake 250ms ease-in-out';
    });

    setTimeout(() => {
      row.forEach(tile => {
        tile.style.animation = '';
      });
    }, 300);
  }

  sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  async revealTile(tile: HTMLElement, state: KeyState, delayMs: number): Promise<void> {
    const flipHalfMs = 250;
    await this.sleep(delayMs);
    tile.classList.add('flip-in');

    await this.sleep(flipHalfMs);
    tile.classList.remove('flip-in');
    tile.dataset.state = state;
    tile.classList.add('flip-out');

    await this.sleep(flipHalfMs);
    tile.classList.remove('flip-out');
  }

  async revealRow(pattern: KeyState[]): Promise<void> {
    if (this.gameOver) return;

    const row = this.tiles[this.currRow];
    const staggerMs = 300;

    await Promise.all(
      row.map((tile, i) => this.revealTile(tile, pattern[i], i * staggerMs))
    );

    const won = pattern.every(s => s === 'correct');
    this.currRow++;
    this.currCol = 0;
    if (won || this.currRow >= MAX_ROWS) {
      this.gameOver = true;
    }
  }

  reset() {
    this.currRow = 0;
    this.currCol = 0;
    this.gameOver = false;

    for (const row of this.tiles) {
      for (const tile of row) {
        tile.textContent = '';
        delete tile.dataset.state;
        tile.classList.remove('pop', 'flip-in', 'flip-out');
        tile.style.animation = '';
      }
    }
  }
}

customElements.define('game-board', GameBoard);
