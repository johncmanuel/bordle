import { createIcons, X } from 'lucide';

const template = document.createElement('template');
template.innerHTML = `
  <div class="modal-overlay">
    <div class="modal-content">
      <div class="modal-header">
        <h2 class="submit-title">HINTS</h2>
        <i data-lucide="x" class="icon-btn close-btn" title="Close"></i>
      </div>
      
      <div id="hints-container" style="display: flex; flex-direction: column; gap: 12px; margin-top: 16px;">
      </div>
    </div>
  </div>
`;

export class HintsForm extends HTMLElement {
  private closeBtn!: HTMLElement;
  private overlay!: HTMLDivElement;
  private hintsContainer!: HTMLDivElement;

  connectedCallback() {
    this.appendChild(template.content.cloneNode(true));

    createIcons({
      root: this,
      icons: { X }
    });

    this.closeBtn = this.querySelector('.close-btn')!;
    this.overlay = this.querySelector('.modal-overlay')!;
    this.hintsContainer = this.querySelector('#hints-container')!;

    const closeHandler = () => {
      this.dispatchEvent(new CustomEvent('hints-close', { bubbles: true }));
    };
    
    this.closeBtn.addEventListener('click', closeHandler);
    this.overlay.addEventListener('click', (e) => {
      if (e.target === this.overlay) closeHandler();
    });
  }

  public setHints(hints: string[]) {
    this.hintsContainer.innerHTML = '';

    if (!hints || hints.length === 0) {
      const p = document.createElement('p');
      p.className = 'subtitle'; 
      p.style.textAlign = 'center';
      p.textContent = 'No hints available!';
      this.hintsContainer.appendChild(p);
      return;
    }

    hints.forEach((hint, index) => {
      const hintEl = document.createElement('div');
      hintEl.style.padding = '12px';
      hintEl.style.backgroundColor = '#3a3a3c';
      hintEl.style.borderRadius = '8px';
      hintEl.style.color = '#fff';
      hintEl.style.fontWeight = 'bold';
      hintEl.style.fontSize = '1.1rem';
      
      const numSpan = document.createElement('span');
      numSpan.style.color = '#818384';
      numSpan.style.marginRight = '8px';
      numSpan.textContent = `${index + 1}.`;
      
      hintEl.appendChild(numSpan);
      hintEl.appendChild(document.createTextNode(hint));
      this.hintsContainer.appendChild(hintEl);
    });
  }
}

customElements.define('hints-form', HintsForm);
