"use strict";
(() => {
  const select = document.getElementById("Input_ItemId");
  const picker = document.getElementById("movement-item-picker");
  const search = document.getElementById("movement-item-search");
  const suggestions = document.getElementById("movement-item-options");
  const status = document.getElementById("movement-item-status");
  if (!select || !picker || !search || !suggestions || !status) return;

  const byLabel = new Map();
  const byId = new Map();
  for (const option of select.options) {
    if (!option.value || option.disabled) continue;
    const label = option.textContent.trim();
    byLabel.set(label.toLowerCase(), option.value);
    byId.set(option.value, label);
  }
  if (byId.size === 0) return; // Preserve the original empty-state control.
  let active = -1;
  const close = () => {
    suggestions.hidden = true;
    search.setAttribute("aria-expanded", "false");
    search.removeAttribute("aria-activedescendant");
    active = -1;
  };
  const resolve = () => {
    const text = search.value.trim();
    const numericId = /^#?\d+$/.test(text) ? String(Number(text.replace(/^#/, ""))) : "";
    return byLabel.get(text.toLowerCase()) || (byId.has(numericId) ? numericId : "");
  };
  const sync = () => {
    const id = resolve();
    search.setCustomValidity(search.value && !id
      ? "Choose a matching item or enter its exact item ID." : "");
    if (select.value !== id) {
      select.value = id;
      select.dispatchEvent(new Event("change", { bubbles: true }));
    }
  };
  const choose = (id) => {
    search.value = byId.get(id);
    sync();
    close();
    status.textContent = `Selected: ${search.value}`;
  };
  const showMatches = () => {
    const text = search.value.trim().toLowerCase();
    suggestions.replaceChildren();
    close();
    if (!text) { status.textContent = ""; return; }
    const matches = [...byId].filter(([id, label]) => label.toLowerCase().includes(text) || id === text.replace(/^#/, ""));
    for (const [id, label] of matches.slice(0, 10)) {
      const option = document.createElement("li");
      option.id = `movement-item-option-${id}`;
      option.dataset.itemId = id;
      option.setAttribute("role", "option");
      option.setAttribute("aria-selected", "false");
      option.textContent = label;
      suggestions.append(option);
    }
    status.textContent = matches.length > 10
      ? `Showing 10 of ${matches.length} matches. Keep typing to narrow the results.`
      : matches.length ? `${matches.length} matching item${matches.length === 1 ? "" : "s"}.` : "No matching active items.";
    suggestions.hidden = matches.length === 0;
    search.setAttribute("aria-expanded", String(matches.length > 0));
  };
  search.value = byId.get(select.value) || "";
  search.disabled = false;
  search.required = true;
  picker.hidden = false;
  select.hidden = true;
  select.required = false;
  document.getElementById("movement-item-select-label").hidden = true;
  search.addEventListener("input", () => { sync(); showMatches(); });
  search.addEventListener("focus", showMatches);
  search.addEventListener("keydown", (event) => {
    if (event.key === "Escape") { event.preventDefault(); close(); return; }
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (suggestions.hidden) showMatches();
      const options = [...suggestions.children];
      if (suggestions.hidden || !options.length) return;
      active = event.key === "ArrowDown" ? (active + 1) % options.length : (active <= 0 ? options.length - 1 : active - 1);
      options.forEach((option, index) => option.setAttribute("aria-selected", String(index === active)));
      search.setAttribute("aria-activedescendant", options[active].id);
      options[active].scrollIntoView({ block: "nearest" });
    } else if (event.key === "Enter") {
      event.preventDefault(); // Choosing an item must not submit a stock movement.
      if (!suggestions.hidden && active >= 0) choose(suggestions.children[active].dataset.itemId);
      else if (resolve()) choose(resolve());
      else if (!suggestions.hidden && suggestions.children.length === 1) choose(suggestions.firstElementChild.dataset.itemId);
      else search.reportValidity();
    }
  });
  suggestions.addEventListener("mousedown", (event) => event.preventDefault()); // Keep input/keyboard focus when choosing.
  suggestions.addEventListener("click", (event) => {
    const option = event.target.closest("[data-item-id]");
    if (option) choose(option.dataset.itemId);
  });
  picker.addEventListener("focusout", (event) => {
    if (!picker.contains(event.relatedTarget)) {
      if (select.value) search.value = byId.get(select.value);
      close();
    }
  });
})();
