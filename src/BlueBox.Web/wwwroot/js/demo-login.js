"use strict";
(() => {
  const panel = document.getElementById("demo-access");
  if (!panel) return;
  const status = panel.querySelector('[role="status"]');
  for (const button of panel.querySelectorAll("[data-demo-copy]")) {
    const field = document.getElementById(button.dataset.demoCopy);
    button.hidden = false;
    button.addEventListener("click", async () => {
      status.textContent = "";
      try {
        if (!navigator.clipboard?.writeText) throw new Error("Clipboard unavailable");
        await navigator.clipboard.writeText(field.value);
        status.textContent = `${button.dataset.demoLabel} copied. Paste it into the sign-in form.`;
      } catch {
        field.focus();
        field.select();
        field.setSelectionRange(0, field.value.length);
        status.textContent = `${button.dataset.demoLabel} selected. Use your browser or keyboard to copy it.`;
      }
    });
  }
})();
