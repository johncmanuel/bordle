import { GameBoard } from './board';
import type { GameKeyboard } from './keyboard';
import type { KeyState } from '../types/state';
import type { Client, DailyPuzzleResponse } from '../api/client';


export class GameApp {
  private board: GameBoard;
  private keyboard: GameKeyboard;
  private apiClient: Client;
  private puzzleId: number;

  constructor(board: GameBoard, keyboard: GameKeyboard, apiClient: Client, puzzle: DailyPuzzleResponse) {
    this.board = board;
    this.keyboard = keyboard;
    this.apiClient = apiClient;
    this.puzzleId = puzzle.puzzle_id!;

    this.keyboard.addEventListener('key-pressed', this.handleKeyPress.bind(this) as EventListener);

    this.restoreExistingGuesses(puzzle);

    if (puzzle.is_finished) {
      this.handleGameOver(puzzle.answer!, puzzle.author_username);
    }
  }

  private restoreExistingGuesses(puzzle: DailyPuzzleResponse) {
    if (!puzzle.guesses || puzzle.guesses.length === 0) return;

    for (const guess of puzzle.guesses) {
      const word = guess.word!;
      const states = guess.states as KeyState[];

      for (const letter of word) {
        this.board.addLetter(letter);
      }

      this.board.revealRowInstant(states);

      for (let i = 0; i < 5; i++) {
        this.keyboard.updateKey(word[i], states[i]);
      }
    }
  }

  private async handleKeyPress(e: Event) {
    const { key } = (e as CustomEvent<{ key: string }>).detail;

    if (this.board.isGameOver()) return;

    if (key === 'BACKSPACE') {
      this.board.removeLetter();
    } else if (key === 'ENTER') {
      if (!this.board.isRowFull()) {
        this.board.shakeCurrentRow();
        return;
      }

      const guess = this.board.getCurrentGuess();

      try {
        const result = await this.apiClient.postApiPuzzlesGuess(this.puzzleId, { word: guess });
        const pattern = result.states as KeyState[];

        await this.board.revealRow(pattern);

        for (let i = 0; i < 5; i++) {
          this.keyboard.updateKey(guess[i], pattern[i]);
        }

        if (result.is_finished) {
          // wait for reveal animation to finish before showing toast
          setTimeout(() => {
            this.handleGameOver(result.answer!, result.author_username);
          }, 300 * 5 + 500); 
        }

      } catch (err) {
        console.error('Guess rejected:', err);
        this.board.shakeCurrentRow();
      }
    } else {
      this.board.addLetter(key);
    }
  }

  private async handleGameOver(answer: string, authorUsername?: string | null) {
    let authorName = authorUsername || "Bordle";

    this.showToast(`The word was ${answer.toUpperCase()}, submitted by ${authorName}`);
  }

  private showToast(message: string) {
    const toast = document.createElement('div');
    toast.className = 'toast-notification';
    toast.textContent = message;
    document.body.appendChild(toast);

    setTimeout(() => {
      toast.classList.add('show');
    }, 100);

    const hideToast = () => {
      toast.classList.remove('show');
      setTimeout(() => {
        if (document.body.contains(toast)) {
          document.body.removeChild(toast);
        }
      }, 300); // match transition duration
    };

    const autoHideTimeout = setTimeout(hideToast, 5000);

    toast.addEventListener('click', () => {
      clearTimeout(autoHideTimeout);
      hideToast();
    });
  }
}
