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
  let statusTimer;
  const announce = (text, immediate = false) => {
    clearTimeout(statusTimer);
    const update = () => { if (status.textContent !== text) status.textContent = text; };
    if (immediate) update();
    else statusTimer = setTimeout(update, 300);
  };
  const close = () => {
    clearTimeout(statusTimer);
    suggestions.hidden = true;
    search.setAttribute("aria-expanded", "false");
    search.removeAttribute("aria-activedescendant");
    active = -1;
  };
  const titleAndFormat = (label) => label.replace(/ · #\d+$/, "").toLowerCase();
  const resolve = () => {
    const text = search.value.trim();
    const labelId = byLabel.get(text.toLowerCase());
    if (labelId) return labelId;
    const match = /^(#?)(\d+)$/.exec(text);
    if (!match) return "";
    const id = String(Number(match[2]));
    if (!byId.has(id)) return "";
    // Bare numbers can be titles (for example, 300 or 1917). Require a choice
    // when a title/format also matches; # explicitly identifies an item ID.
    if (!match[1] && [...byId.values()].some((label) => titleAndFormat(label).includes(text))) return "";
    return id;
  };
  const choose = (id) => {
    search.value = byId.get(id);
    search.setCustomValidity("");
    if (select.value !== id) {
      select.value = id;
      select.dispatchEvent(new Event("change", { bubbles: true }));
    }
    close();
    announce(`Selected: ${search.value}`, true);
  };
  const showMatches = () => {
    const text = search.value.trim().toLowerCase();
    suggestions.replaceChildren();
    close();
    if (!text) { announce("", true); return; }
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
    announce(matches.length > 10
      ? `Showing 10 of ${matches.length} matches. Keep typing to narrow the results.`
      : matches.length ? `${matches.length} matching item${matches.length === 1 ? "" : "s"}.` : "No matching active items.");
    suggestions.hidden = matches.length === 0;
    search.setAttribute("aria-expanded", String(matches.length > 0));
  };
  search.value = byId.get(select.value) || "";
  search.disabled = false;
  search.required = true;
  picker.hidden = false;
  // Keep the select as the sole submitted item ID; search text is presentation only.
  select.hidden = true;
  select.required = false;
  document.getElementById("movement-item-select-label").hidden = true;
  search.addEventListener("input", () => {
    // Typing invalidates the previous choice immediately but never selects a new
    // item or requests its balance until Enter, a result click, or unambiguous blur.
    if (select.value) {
      select.value = "";
      select.dispatchEvent(new Event("change", { bubbles: true }));
    }
    search.setCustomValidity(search.value ? "Choose a matching item, or confirm an exact item ID with Enter." : "");
    showMatches();
  });
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
      const id = resolve();
      if (id) choose(id);
      else close();
    }
  });
})();
