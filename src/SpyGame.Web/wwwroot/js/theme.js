// Runs in <head> before the page paints, so the saved theme never flashes.
// Dark is the default: the game is designed dark-first.
(function () {
  var theme = null;
  try { theme = localStorage.getItem("sg-theme"); } catch (e) { /* storage blocked */ }
  if (theme !== "light" && theme !== "dark") {
    theme = window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }
  document.documentElement.dataset.theme = theme;
})();
