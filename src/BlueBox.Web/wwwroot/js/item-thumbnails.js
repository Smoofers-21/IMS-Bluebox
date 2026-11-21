"use strict";
(() => {
  const hideFailedImage = (image) => {
    if (image instanceof HTMLImageElement && image.hasAttribute("data-item-thumbnail"))
      image.hidden = true;
  };
  // Capturing catches image errors without inline handlers, preserving the CSP.
  document.addEventListener("error", (event) => hideFailedImage(event.target), true);
  document.querySelectorAll("img[data-item-thumbnail]").forEach((image) => {
    if (image.complete && image.naturalWidth === 0) hideFailedImage(image);
  });
})();
