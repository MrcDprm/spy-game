// Shared page behaviour. The room page loads its own script (room.js).
document.addEventListener("DOMContentLoaded", function () {
  var toggle = document.querySelector("[data-theme-toggle]");
  if (toggle) {
    toggle.addEventListener("click", function () {
      var next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
      document.documentElement.dataset.theme = next;
      try { localStorage.setItem("sg-theme", next); } catch (e) { /* storage blocked */ }
    });
  }

  // "Select all" / "Clear" for the category chips.
  function setAll(checked) {
    document.querySelectorAll("[data-chips] input[type=checkbox]").forEach(function (box) { box.checked = checked; });
  }
  var all = document.querySelector("[data-chips-all]");
  var none = document.querySelector("[data-chips-none]");
  if (all) all.addEventListener("click", function () { setAll(true); });
  if (none) none.addEventListener("click", function () { setAll(false); });

  // Keep what is typed in the allowed shape: nicknames in lowercase, room codes in capitals.
  // The server checks again; this only saves people from an avoidable error.
  function keepCase(input, transform) {
    input.addEventListener("input", function () {
      var start = input.selectionStart, end = input.selectionEnd;
      var next = transform(input.value);
      if (next !== input.value) {
        input.value = next;
        if (start !== null) input.setSelectionRange(start, end);
      }
    });
  }
  document.querySelectorAll("[data-nickname]").forEach(function (input) {
    keepCase(input, function (v) { return v.toLocaleLowerCase("en").replace(/\s/g, ""); });
  });
  document.querySelectorAll("[data-room-code]").forEach(function (input) {
    keepCase(input, function (v) { return v.toLocaleUpperCase("en").replace(/[^A-Z0-9]/g, ""); });
  });
});
