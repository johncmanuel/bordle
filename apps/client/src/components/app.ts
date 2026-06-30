import { GameBoard } from './board';
import type { GameKeyboard } from './keyboard';
import type { KeyState } from '../types/state';

// temp
const ANSWER = 'GAMES'; 

export class GameApp {
  private board: GameBoard;
  private keyboard: GameKeyboard;
  private consumedCharMarker = '#'; 
  private matchedCharMarker = '*';

  constructor(board: GameBoard, keyboard: GameKeyboard) {
    this.board = board;
    this.keyboard = keyboard;
    this.keyboard.addEventListener('key-pressed', this.handleKeyPress.bind(this) as EventListener);
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
      const pattern = this.evaluate(guess, ANSWER);

      await this.board.revealRow(pattern);

      for (let i = 0; i < 5; i++) {
        this.keyboard.updateKey(guess[i], pattern[i]);
      }
    } else {
      this.board.addLetter(key);
    }
  }

  private evaluate(guess: string, answer: string): KeyState[] {
    const result: KeyState[] = Array(5).fill('absent');
    const answerChars = answer.split('');
    const guessChars = guess.split('');

    // find any correct letters 
    for (let i = 0; i < 5; i++) {
      if (guessChars[i] === answerChars[i]) {
        result[i] = 'correct';
        answerChars[i] = this.consumedCharMarker;
        guessChars[i] = this.matchedCharMarker;
      }
    }

    // then find any present letters
    for (let i = 0; i < 5; i++) {
      if (guessChars[i] === this.matchedCharMarker) continue;
      const idx = answerChars.indexOf(guessChars[i]);
      if (idx !== -1) {
        result[i] = 'present';
        answerChars[idx] = this.consumedCharMarker; 
      }
    }

    return result;
  }
}
