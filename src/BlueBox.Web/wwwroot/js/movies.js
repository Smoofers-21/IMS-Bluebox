(() => {
  const form = document.getElementById("item-form");
  if (!form) return;
  const type = document.getElementById("Input_Type");
  const lookup = document.getElementById("movie-lookup");
  const title = document.getElementById("movie-title");
  const status = document.getElementById("lookup-status");
  const matches = document.getElementById("movie-matches");
  const button = document.getElementById("lookup-button");
  let pending;
  let generation = 0;

  function reset() {
    generation++;
    pending?.abort();
    button.disabled = false;
    matches.replaceChildren();
    status.textContent = "";
    lookup.hidden = type.value !== "MOVIE";
  }
  async function request(handler, parameter, value) {
    pending?.abort();
    pending = new AbortController();
    const url = new URL(form.action || window.location.href);
    url.search = new URLSearchParams({ handler, [parameter]: value });
    const response = await fetch(url, { signal: pending.signal, cache: "no-store" });
    if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) throw new Error("Lookup unavailable");
    return response.json();
  }
  button.addEventListener("click", async () => {
    const current = ++generation;
    matches.replaceChildren();
    status.textContent = "Searching movies…";
    button.disabled = true;
    try {
      const result = await request("Lookup", "title", title.value || document.getElementById("Input_Name").value);
      if (current !== generation) return;
      status.textContent = result.message || "Select the correct title and year.";
      for (const movie of result.matches) {
        const choice = document.createElement("button");
        choice.type = "button";
        choice.className = "button small movie-match";
        choice.textContent = `${movie.title} (${movie.year})`;
        choice.addEventListener("click", async () => {
          const selection = ++generation;
          status.textContent = "Loading movie details…";
          try {
            const detail = await request("Movie", "movieId", movie.id);
            if (selection !== generation) return;
            if (!detail.movie) { status.textContent = detail.message; return; }
            document.getElementById("Input_Name").value = detail.movie.title.slice(0, 150);
            document.getElementById("Input_PosterUrl").value = detail.movie.posterUrl || "";
            document.getElementById("Input_AgeRating").value = detail.movie.ageRating || "";
            status.textContent = "Movie metadata filled in. Review the fields and save when ready.";
            matches.replaceChildren();
          } catch (error) {
            if (error.name !== "AbortError" && selection === generation) status.textContent = "Details unavailable. You can enter the item manually.";
          }
        });
        matches.append(choice);
      }
    } catch (error) {
      if (error.name !== "AbortError" && current === generation) status.textContent = "Lookup unavailable. You can enter the item manually.";
    } finally {
      if (current === generation) button.disabled = false;
    }
  });
  title.addEventListener("keydown", event => {
    if (event.key === "Enter") { event.preventDefault(); button.click(); }
  });
  type.addEventListener("change", reset);
  reset();
})();
