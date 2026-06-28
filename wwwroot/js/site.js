// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
  const pickers = document.querySelectorAll('[data-audit-action-picker]');

  pickers.forEach((picker) => {
    const trigger = picker.querySelector('[data-audit-action-trigger]');
    const menu = picker.querySelector('[data-audit-action-menu]');
    const input = picker.querySelector('[data-audit-action-input]');
    const label = picker.querySelector('[data-audit-action-label]');
    const options = picker.querySelectorAll('[data-value]');

    if (!trigger || !menu || !input || !label) {
      return;
    }

    const setOpen = (open) => {
      picker.classList.toggle('open', open);
      trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
    };

    const syncSelection = (value, text) => {
      input.value = value;
      label.textContent = text;
      options.forEach((option) => {
        option.classList.toggle('active', option.dataset.value === value);
      });
    };

    trigger.addEventListener('click', (event) => {
      event.preventDefault();
      setOpen(!picker.classList.contains('open'));
    });

    options.forEach((option) => {
      option.addEventListener('click', () => {
        const value = option.dataset.value || '';
        const text = option.textContent?.trim() || 'All Actions';
        syncSelection(value, text);
        setOpen(false);
      });
    });

    document.addEventListener('click', (event) => {
      if (!picker.contains(event.target)) {
        setOpen(false);
      }
    });

    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    });

    syncSelection(input.value, label.textContent?.trim() || 'All Actions');
  });
});
