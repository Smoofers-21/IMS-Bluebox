"use strict";
(() => {
  const form = document.getElementById("movement-form");
  if (!form) return;
  const item = document.getElementById("Input_ItemId");
  const location = document.getElementById("Input_LocationId");
  const quantity = document.getElementById("Input_Quantity");
  const confirm = document.getElementById("confirm-movement");
  const next = document.getElementById("next-balance");
  const message = document.getElementById("preview-message");
  let current = null;
  let loadedKey = "";
  let pending = null;
  let lookupFailed = false;
  const render = () => {
    const amount = Number(quantity.value);
    const direction = form.querySelector(
      'input[name="Input.Direction"]:checked',
    )?.value;
    const delta = direction === "CHECK IN" ? amount : -amount;
    const validDirection =
      direction === "CHECK IN" || direction === "CHECK OUT";
    const validQuantity =
      quantity.value !== "" &&
      Number.isInteger(amount) &&
      amount > 0 &&
      amount <= 2147483647;
    const result =
      current === null || !validQuantity || !validDirection
        ? null
        : current + delta;
    document.getElementById("preview-item").textContent =
      item.value && location.value
        ? item.options[item.selectedIndex].text +
          " · " +
          location.options[location.selectedIndex].text
        : "Select an item and warehouse";
    document.getElementById("current-balance").textContent = current ?? "—";
    document.getElementById("quantity-change").textContent =
      validQuantity && validDirection && current !== null
        ? (delta > 0 ? "+" : "") + delta
        : "—";
    next.textContent =
      result === null ? "—" : result.toLocaleString() + " units";
    const invalid = result !== null && (result < 0 || result > 2147483647);
    next.closest(".result-card").classList.toggle("invalid", invalid);
    message.textContent = lookupFailed
      ? "Preview unavailable. Reload the page to try again."
      : result === null
        ? "Choose a movement, item, warehouse, and positive quantity."
        : result < 0
          ? "Not enough stock for this check-out."
          : result > 2147483647
            ? "Quantity exceeds the supported maximum."
            : "Ready to confirm.";
    confirm.disabled =
      current === null || !validQuantity || !validDirection || invalid;
  };
  const loadBalance = async () => {
    const key = item.value + ":" + location.value;
    if (key === loadedKey) return;
    loadedKey = key;
    pending?.abort();
    current = null;
    lookupFailed = false;
    render();
    if (!item.value || !location.value) return;
    pending = new AbortController();
    try {
      const response = await fetch(
        `/api/inventory/balance?itemId=${encodeURIComponent(item.value)}&locationId=${encodeURIComponent(location.value)}`,
        {
          signal: pending.signal,
          cache: "no-store",
          headers: { Accept: "application/json" },
        },
      );
      if (!response.ok || response.redirected)
        throw new Error("Balance unavailable");
      const data = await response.json();
      if (!Number.isInteger(data.quantity) || data.quantity < 0)
        throw new Error("Invalid balance");
      if (key !== loadedKey) return;
      current = data.quantity;
    } catch (error) {
      if (error.name === "AbortError" || key !== loadedKey) return;
      lookupFailed = true;
    }
    render();
  };
  item.addEventListener("change", loadBalance);
  location.addEventListener("change", loadBalance);
  form.addEventListener("input", render);
  form.addEventListener("change", render);
  form.addEventListener("submit", () => {
    confirm.disabled = true;
    confirm.textContent = "Recording…";
  });
  loadBalance();
})();
