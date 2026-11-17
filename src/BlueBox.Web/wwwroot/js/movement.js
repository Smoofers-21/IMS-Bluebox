"use strict";
(() => {
  const form = document.getElementById("movement-form");
  if (!form) return;
  const balances = JSON.parse(
    document.getElementById("stock-balances").textContent,
  );
  const item = document.getElementById("Input_ItemId");
  const location = document.getElementById("Input_LocationId");
  const quantity = document.getElementById("Input_Quantity");
  const confirm = document.getElementById("confirm-movement");
  const next = document.getElementById("next-balance");
  const message = document.getElementById("preview-message");
  const update = () => {
    const selected = item.value && location.value;
    const amount = Number(quantity.value);
    const current = selected
      ? (balances.find(
          (b) =>
            b.ItemId === Number(item.value) &&
            b.LocationId === Number(location.value),
        )?.Quantity ?? 0)
      : null;
    const direction = form.querySelector(
      'input[name="Input.Direction"]:checked',
    )?.value;
    const delta = direction === "CHECK IN" ? amount : -amount;
    const validQuantity =
      quantity.value !== "" &&
      Number.isInteger(amount) &&
      amount > 0 &&
      amount <= 2147483647;
    const result = current === null || !validQuantity ? null : current + delta;
    document.getElementById("preview-item").textContent = selected
      ? item.options[item.selectedIndex].text +
        " · " +
        location.options[location.selectedIndex].text
      : "Select an item and warehouse";
    document.getElementById("current-balance").textContent = current ?? "—";
    document.getElementById("quantity-change").textContent =
      validQuantity && selected ? (delta > 0 ? "+" : "") + delta : "—";
    next.textContent =
      result === null ? "—" : result.toLocaleString() + " units";
    const invalid = result !== null && (result < 0 || result > 2147483647);
    next.closest(".result-card").classList.toggle("invalid", invalid);
    message.textContent =
      result === null
        ? "Choose an item, warehouse, and positive quantity."
        : result < 0
          ? "Not enough stock for this check-out."
          : result > 2147483647
            ? "Quantity exceeds the supported maximum."
            : "Ready to confirm.";
    confirm.disabled = !selected || !validQuantity || invalid;
  };
  form.addEventListener("input", update);
  form.addEventListener("change", update);
  form.addEventListener("submit", () => {
    confirm.disabled = true;
    confirm.textContent = "Recording…";
  });
  update();
})();
