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

  // Place groups on the "new room" form: a box per group (all / some / none), a running total,
  // and the last choice remembered on this device. Without script the plain checkboxes still work.
  var places = document.querySelector("[data-places]");
  if (places) {
    var STORE = "sg-places";
    var min = Number(places.dataset.min);
    var boxes = function (scope) { return Array.prototype.slice.call(scope.querySelectorAll("input[name='Create.PlaceIds']")); };
    var total = places.querySelector("[data-places-total]");

    var refresh = function () {
      places.querySelectorAll("[data-group]").forEach(function (group) {
        var items = boxes(group);
        var on = items.filter(function (b) { return b.checked; }).length;
        var toggle = group.querySelector("[data-group-toggle]");
        toggle.checked = on === items.length;
        toggle.indeterminate = on > 0 && on < items.length;
        group.querySelector("[data-group-count]").textContent = on + "/" + items.length;
      });
      var count = boxes(places).filter(function (b) { return b.checked; }).length;
      total.textContent = places.dataset.total.replace("{0}", count);
      total.classList.toggle("place-total--low", count < min);
    };

    // Bring back the last choice, unless the form came back with an error (then the server's copy wins).
    if (places.dataset.restore === "true") {
      try {
        var saved = JSON.parse(localStorage.getItem(STORE) || "null");
        if (Array.isArray(saved) && saved.length >= min) {
          boxes(places).forEach(function (b) { b.checked = saved.indexOf(b.value) >= 0; });
        }
      } catch (e) { /* storage blocked or damaged: keep the defaults */ }
    }

    places.querySelectorAll("[data-group]").forEach(function (group) {
      var toggle = group.querySelector("[data-group-toggle]");
      toggle.hidden = false;
      toggle.addEventListener("change", function () {
        boxes(group).forEach(function (b) { b.checked = toggle.checked; });
        refresh();
      });
    });
    places.addEventListener("change", function (e) { if (e.target.name === "Create.PlaceIds") refresh(); });

    var setAll = function (checked) { boxes(places).forEach(function (b) { b.checked = checked; }); refresh(); };
    var all = places.querySelector("[data-places-all]");
    var none = places.querySelector("[data-places-none]");
    all.hidden = none.hidden = false;
    all.addEventListener("click", function () { setAll(true); });
    none.addEventListener("click", function () { setAll(false); });

    places.closest("form").addEventListener("submit", function () {
      try {
        var chosen = boxes(places).filter(function (b) { return b.checked; }).map(function (b) { return b.value; });
        localStorage.setItem(STORE, JSON.stringify(chosen));
      } catch (e) { /* storage blocked */ }
    });
    refresh();
  }

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
