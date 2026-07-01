import { SESSION_TOKEN_KEY } from '../discord/init';
import { createIcons, X } from 'lucide';

const template = document.createElement('template');
template.innerHTML = `
  <div class="modal-overlay">
    <div class="modal-content">
      <div class="modal-header">
        <h2 class="submit-title">SUBMIT A WORD</h2>
        <i data-lucide="x" class="icon-btn close-btn" title="Close"></i>
      </div>

      <div class="submit-field">
        <label for="word-input">Word</label>
        <input id="word-input" type="text" maxlength="5" placeholder="HELLO" autocomplete="off" spellcheck="false" />
      </div>

      <div class="submit-field">
        <label>Hints <span class="hint-optional">(optional, max 3)</span></label>
        <input class="hint-input" type="text" maxlength="25" placeholder="Hint 1" autocomplete="off" />
        <input class="hint-input" type="text" maxlength="25" placeholder="Hint 2" autocomplete="off" />
        <input class="hint-input" type="text" maxlength="25" placeholder="Hint 3" autocomplete="off" />
      </div>

      <div class="submit-status" id="submit-status"></div>

      <div class="submit-actions">
        <button class="btn btn-submit" id="submit-btn">Submit</button>
      </div>
    </div>
  </div>
`;

export class SubmitWordForm extends HTMLElement {
  private wordInput!: HTMLInputElement;
  private hintInputs!: NodeListOf<HTMLInputElement>;
  private submitBtn!: HTMLButtonElement;
  private closeBtn!: HTMLElement;
  private statusEl!: HTMLDivElement;
  private overlay!: HTMLDivElement;

  connectedCallback() {
    this.appendChild(template.content.cloneNode(true));

    createIcons({
      root: this,
      icons: { X }
    });

    this.wordInput = this.querySelector('#word-input')!;
    this.hintInputs = this.querySelectorAll('.hint-input');
    this.submitBtn = this.querySelector('#submit-btn')!;
    this.closeBtn = this.querySelector('.close-btn')!;
    this.statusEl = this.querySelector('#submit-status')!;
    this.overlay = this.querySelector('.modal-overlay')!;

    // Auto-uppercase word input and restrict to letters only
    this.wordInput.addEventListener('input', () => {
      this.wordInput.value = this.wordInput.value.replace(/[^a-zA-Z]/g, '').toUpperCase();
    });

    this.submitBtn.addEventListener('click', () => this.handleSubmit());
    
    // Close modal handlers
    const closeHandler = () => {
      this.resetForm();
      this.dispatchEvent(new CustomEvent('submit-cancel', { bubbles: true }));
    };
    
    this.closeBtn.addEventListener('click', closeHandler);
    this.overlay.addEventListener('click', (e) => {
      if (e.target === this.overlay) closeHandler();
    });
  }

  private async handleSubmit() {
    const word = this.wordInput.value.trim();

    if (word.length !== 5) {
      this.showStatus('Word must be exactly 5 letters.', 'error');
      return;
    }

    const hints = Array.from(this.hintInputs)
      .map(input => input.value.trim())
      .filter(h => h.length > 0);

    const token = sessionStorage.getItem(SESSION_TOKEN_KEY);
    if (!token) {
      this.showStatus('Not authenticated. Please refresh.', 'error');
      return;
    }

    this.submitBtn.disabled = true;
    this.submitBtn.textContent = 'Submitting...';

    try {
      const response = await fetch('/api/submissions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({ word, hints }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        this.showStatus(errorText || 'Submission failed.', 'error');
        return;
      }

      this.showStatus('Word submitted successfully!', 'success');

      setTimeout(() => {
        this.resetForm();
        this.dispatchEvent(new CustomEvent('submit-success', { bubbles: true }));
      }, 1200);
    } catch {
      this.showStatus('Network error. Please try again.', 'error');
    } finally {
      this.submitBtn.disabled = false;
      this.submitBtn.textContent = 'Submit';
    }
  }

  private showStatus(message: string, type: 'success' | 'error') {
    this.statusEl.textContent = message;
    this.statusEl.className = `submit-status ${type}`;
  }

  private resetForm() {
    this.wordInput.value = '';
    this.hintInputs.forEach(input => (input.value = ''));
    this.statusEl.textContent = '';
    this.statusEl.className = 'submit-status';
  }
}

customElements.define('submit-word-form', SubmitWordForm);
