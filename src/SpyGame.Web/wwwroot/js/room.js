// The room page: one SignalR connection, and the screen redrawn from every "room" message.
// The server sends this player's own view only; the page never decides anything, it shows
// the state and passes clicks on. Everything is built with createElement and textContent,
// never from HTML strings.
(function () {
  "use strict";

  var root = document.querySelector("[data-room]");
  if (!root || !window.signalR) return;

  var code = root.dataset.room;
  var invite = root.dataset.invite;
  var lang = document.documentElement.lang === "en" ? "en" : "tr";
  var texts = JSON.parse(document.getElementById("texts").textContent);

  var stage = root.querySelector("[data-stage]");
  var playersList = root.querySelector("[data-players]");
  var invitePanel = root.querySelector("[data-invite-panel]");
  var roundLabel = root.querySelector("[data-round]");
  var timer = root.querySelector("[data-timer]");
  var banner = root.querySelector("[data-banner]");
  var toast = root.querySelector("[data-toast]");

  var state = null;     // the latest view from the server
  var clockOffset = 0;  // server time minus this device's time
  var wakeLock = null;

  // --- helpers ---

  function t(key) {
    var text = texts[key] || key;
    for (var i = 1; i < arguments.length; i++) text = text.split("{" + (i - 1) + "}").join(String(arguments[i]));
    return text;
  }

  function el(tag, options) {
    var node = document.createElement(tag);
    options = options || {};
    if (options.className) node.className = options.className;
    if (options.text !== undefined) node.textContent = options.text;
    if (options.attrs) Object.keys(options.attrs).forEach(function (k) { node.setAttribute(k, options.attrs[k]); });
    if (options.on) Object.keys(options.on).forEach(function (k) { node.addEventListener(k, options.on[k]); });
    for (var i = 2; i < arguments.length; i++) if (arguments[i]) node.appendChild(arguments[i]);
    return node;
  }

  function button(label, onClick, className) {
    return el("button", { className: className || "btn btn--block", text: label, attrs: { type: "button" }, on: { click: onClick } });
  }

  function word(w) { return w ? (lang === "en" ? w.en : w.tr) : ""; }
  // The room's places, alphabetical in the reader's language (the server keeps the chosen order).
  function sorted(places) {
    return places.slice().sort(function (a, b) { return word(a).localeCompare(word(b), lang); });
  }
  function player(id) { return state.players.find(function (p) { return p.id === id; }); }
  function nameOf(id) { var p = player(id); return p ? p.nickname : "?"; }

  var toastTimer = null;
  function showToast(message) {
    toast.textContent = message;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toast.hidden = true; }, 3500);
  }

  function invoke(method) {
    var args = Array.prototype.slice.call(arguments);
    return connection.invoke.apply(connection, args)
      .then(function (error) { if (error) showToast(t("error." + error)); return error; })
      .catch(function () { showToast(t("connection.lost")); });
  }

  // --- the clock ---

  function tick() {
    if (!state || !state.endsAt) { timer.hidden = true; return; }
    var left = Math.max(0, Math.ceil((Date.parse(state.endsAt) - (Date.now() + clockOffset)) / 1000));
    timer.hidden = false;
    timer.textContent = Math.floor(left / 60) + ":" + String(left % 60).padStart(2, "0");
    timer.classList.toggle("timer--urgent", left <= 15);
  }
  setInterval(tick, 250);

  // Keep the phone awake while a round is on (where the browser allows it).
  function keepAwake(on) {
    if (!("wakeLock" in navigator)) return;
    if (on && !wakeLock) {
      navigator.wakeLock.request("screen").then(function (lock) { wakeLock = lock; lock.addEventListener("release", function () { wakeLock = null; }); }).catch(function () {});
    } else if (!on && wakeLock) {
      wakeLock.release();
    }
  }

  // --- drawing ---

  function render() {
    var me = player(state.youId);
    var active = state.phase === "asking" || state.phase === "voting" || state.phase === "lastGuess";
    roundLabel.textContent = state.round > 0 ? t("round", state.round) : "";
    invitePanel.hidden = active;
    keepAwake(active);
    renderPlayers();
    tick();

    stage.replaceChildren();
    if (active && !(me && me.inRound) && state.phase !== "lastGuess") {
      stage.appendChild(el("div", { className: "card" }, el("p", { className: "muted", text: t("waitingNext") })));
      return;
    }
    if (state.phase === "lobby") renderLobby();
    else if (state.phase === "asking") renderAsking();
    else if (state.phase === "voting") renderVoting();
    else if (state.phase === "lastGuess") renderGuess();
    else if (state.phase === "results") renderResults();
  }

  function renderPlayers() {
    playersList.replaceChildren();
    var sorted = state.phase === "results" ? state.players.slice().sort(function (a, b) { return b.score - a.score; }) : state.players;
    sorted.forEach(function (p) {
      var tags = el("span", { className: "players__tags" });
      if (p.id === state.youId) tags.appendChild(el("span", { className: "tag", text: t("you") }));
      if (p.isHost) tags.appendChild(el("span", { className: "tag tag--host", text: t("host") }));
      if (!p.connected) tags.appendChild(el("span", { className: "tag tag--away", text: t("away") }));
      if (p.ready) tags.appendChild(el("span", { className: "tag tag--ok", text: t("ready") }));
      if (p.hasVoted) tags.appendChild(el("span", { className: "tag tag--ok", text: t("voted") }));
      playersList.appendChild(el("li", { className: "players__item" + (p.connected ? "" : " players__item--away") },
        el("span", { className: "players__name", text: p.nickname }), tags,
        el("span", { className: "players__score mono", text: String(p.score) })));
    });
  }

  function settingsSummary() {
    var s = state.settings;
    return el("p", { className: "muted", text: [t("lobby.places", s.placeCount), t("lobby.minutes", s.roundSeconds / 60), t("lobby.spies", s.spyCount)].join(" · ") });
  }

  function hostButton(label) {
    var host = state.players.find(function (p) { return p.isHost; });
    if (!state.youAreHost) return el("p", { className: "muted", text: t(state.phase === "results" ? "result.waitHost" : "lobby.waitHost", host ? host.nickname : "") });
    var present = state.players.filter(function (p) { return p.connected; }).length;
    var box = el("div", { className: "stack" });
    var start = button(label, function () { invoke("StartRound"); });
    if (present < 3) {
      start.disabled = true;
      box.appendChild(el("p", { className: "muted small", text: t("lobby.needMore", present) }));
    }
    box.appendChild(start);
    return box;
  }

  function renderLobby() {
    stage.appendChild(el("div", { className: "card card--paper" },
      el("h1", { className: "stage__title", text: t("lobby.title") }),
      settingsSummary(),
      hostButton(t("lobby.start"))));
  }

  // The card shows only while it is held down, so a neighbour cannot read it.
  function holdCard() {
    var card = state.card;
    var face = el("div", { className: "secret__face" });
    if (card.isSpy) {
      face.appendChild(el("p", { className: "stamp", text: t("card.spy") }));
      face.appendChild(el("p", { className: "small", text: t("card.spyHint") }));
      if (card.spyCount > 1) face.appendChild(el("p", { className: "small", text: t("card.spies", card.spyCount) }));
    } else {
      face.appendChild(el("p", { className: "secret__label", text: t("card.place") }));
      face.appendChild(el("p", { className: "secret__place", text: word(card.place) }));
    }
    var cover = el("div", { className: "secret__cover" },
      el("span", { className: "secret__icon", attrs: { "aria-hidden": "true" }, text: "?" }),
      el("span", { text: t("card.hold") }));
    var secret = el("button", { className: "secret", attrs: { type: "button", "aria-pressed": "false" } }, cover, face);

    function show(e) { if (e) e.preventDefault(); secret.classList.add("secret--open"); secret.setAttribute("aria-pressed", "true"); }
    function hide() { secret.classList.remove("secret--open"); secret.setAttribute("aria-pressed", "false"); }
    secret.addEventListener("pointerdown", show);
    ["pointerup", "pointerleave", "pointercancel", "blur"].forEach(function (n) { secret.addEventListener(n, hide); });
    secret.addEventListener("contextmenu", function (e) { e.preventDefault(); });
    secret.addEventListener("keydown", function (e) { if (e.key === " " || e.key === "Enter") show(e); });
    secret.addEventListener("keyup", function (e) { if (e.key === " " || e.key === "Enter") hide(); });
    document.addEventListener("visibilitychange", hide);
    return el("div", { className: "stack" }, secret, el("p", { className: "hint center", text: t("card.holdHint") }));
  }

  function renderAsking() {
    var me = player(state.youId);
    var inRound = state.players.filter(function (p) { return p.inRound && p.connected; });
    var ready = inRound.filter(function (p) { return p.ready; }).length;
    var readyBtn = button(me.ready ? t("asking.readyDone") : t("asking.ready"), function () { invoke("ReadyToVote"); }, "btn btn--ghost btn--block");
    readyBtn.disabled = me.ready;
    stage.appendChild(el("div", { className: "stack" },
      holdCard(),
      el("p", { className: "first-asker", text: t("asking.first", nameOf(state.firstAskerId)) }),
      placesList(state.card.places),
      el("div", { className: "card" }, readyBtn, el("p", { className: "muted small center", text: t("asking.readyCount", ready, inRound.length) }))));
  }

  // The room's places, the same for everyone; clickable only for the spy's last guess.
  function placesList(places, onPick) {
    var list = el("ul", { className: "places" });
    sorted(places).forEach(function (w) {
      var item = onPick
        ? button(word(w), function () { onPick(w.id); }, "place place--pick")
        : el("span", { className: "place", text: word(w) });
      list.appendChild(el("li", {}, item));
    });
    return el("details", { className: "card places-box", attrs: onPick ? { open: "" } : {} },
      el("summary", { text: t("card.possible", places.length) }), list);
  }

  function renderVoting() {
    var box = el("div", { className: "card" }, el("h1", { className: "stage__title", text: t("voting.title") }));
    if (!state.voting) {
      box.appendChild(el("p", { className: "muted", text: t("voting.watch") }));
      stage.appendChild(box);
      return;
    }
    box.appendChild(el("p", { className: "muted", text: t("voting.hint") }));
    var list = el("div", { className: "vote-list" });
    state.players.filter(function (p) { return p.inRound && p.id !== state.youId; }).forEach(function (p) {
      var chosen = state.voting.yourVote === p.id;
      list.appendChild(el("button", {
        className: "vote" + (chosen ? " vote--chosen" : ""),
        text: p.nickname,
        attrs: { type: "button", "aria-pressed": String(chosen) },
        on: { click: function () { invoke("Vote", p.id); } },
      }));
    });
    box.appendChild(list);
    box.appendChild(el("p", { className: "muted small center", text: t("voting.count", state.voting.votesCast, state.voting.voters) }));
    stage.appendChild(box);
  }

  function renderGuess() {
    var g = state.guess;
    if (g.youAreAccused) {
      stage.appendChild(el("div", { className: "stack" },
        el("div", { className: "card card--paper" },
          el("p", { className: "stamp", text: t("guess.youTitle") }),
          el("p", { text: t("guess.youHint") })),
        placesList(g.places, function (id) { invoke("Guess", id); })));
    } else {
      stage.appendChild(el("div", { className: "card" },
        el("h1", { className: "stage__title", text: t("guess.othersTitle", nameOf(g.accusedId)) }),
        el("p", { className: "muted", text: t("guess.othersHint") })));
    }
  }

  function renderResults() {
    var r = state.result;
    var civilians = r.winner === "civilians";
    var story = [];
    if (!r.accusedId) story.push(t("result.noAccusation"));
    else if (r.spyIds.indexOf(r.accusedId) < 0) story.push(t("result.accusedCivilian", nameOf(r.accusedId)));
    else {
      story.push(t("result.caught", nameOf(r.accusedId)));
      story.push(r.spyGuessed ? t("result.guessRight", word(r.guess)) : r.guess ? t("result.guessWrong", word(r.guess)) : t("result.noGuess"));
    }

    var votes = el("ul", { className: "votes" });
    Object.keys(r.points).forEach(function (id) {
      var target = r.votes[id];
      votes.appendChild(el("li", {},
        el("span", { text: nameOf(id) }),
        el("span", { className: "muted", text: target ? "→ " + nameOf(target) : t("result.noVote") }),
        el("span", { className: "mono", text: "+" + r.points[id] })));
    });

    stage.appendChild(el("div", { className: "stack" },
      el("div", { className: "card card--paper result" },
        el("p", { className: "stamp" + (civilians ? " stamp--ok" : ""), text: t(civilians ? "result.civilians" : "result.spies") }),
        el("p", { className: "secret__label", text: t("result.place") }),
        el("p", { className: "secret__place", text: word(r.place) }),
        el("p", { text: t(r.spyIds.length > 1 ? "result.spiesWere" : "result.spyWas") + ": " + r.spyIds.map(nameOf).join(", ") }),
        el("p", { className: "muted", text: story.join(" ") })),
      el("div", { className: "card" }, el("h2", { text: t("result.votes") }), votes),
      hostButton(t("result.next")),
      el("a", { className: "board-link", text: t("result.board") + " →", attrs: { href: "/leaderboard", target: "_blank", rel: "noopener" } })));
  }

  // --- invite and leave ---

  root.querySelector("[data-copy]").addEventListener("click", function (e) {
    var btn = e.currentTarget;
    navigator.clipboard.writeText(invite).then(function () {
      btn.textContent = t("lobby.copied");
      setTimeout(function () { btn.textContent = t("lobby.copy"); }, 2000);
    }).catch(function () {});
  });
  var share = root.querySelector("[data-share]");
  if (navigator.share) {
    share.hidden = false;
    share.addEventListener("click", function () {
      navigator.share({ title: "Casus", text: t("lobby.shareText", code), url: invite }).catch(function () {});
    });
  }
  root.querySelector("[data-leave]").addEventListener("click", function () {
    if (!window.confirm(t("leaveConfirm"))) return;
    invoke("Leave").then(function () { window.location.href = "/"; });
  });

  // --- the connection ---

  function showGone() {
    stage.replaceChildren(el("div", { className: "card" },
      el("p", { text: t("gone") }), el("a", { className: "btn", text: t("home"), attrs: { href: "/" } })));
    invitePanel.hidden = true;
    timer.hidden = true;
  }

  var connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game?room=" + encodeURIComponent(code))
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 10000, 30000])
    .configureLogging(signalR.LogLevel.None)
    .build();

  connection.on("room", function (view) {
    state = view;
    clockOffset = Date.parse(view.serverNow) - Date.now();
    banner.hidden = true;
    render();
  });
  connection.onreconnecting(function () { banner.textContent = t("connection.lost"); banner.hidden = false; });
  connection.onreconnected(function () { banner.hidden = true; });
  connection.onclose(function () {
    if (!state) { showGone(); return; } // the server refused us: not (or no longer) in this room
    banner.replaceChildren(document.createTextNode(t("connection.closed") + " "),
      el("button", { className: "link-btn", text: t("connection.retry"), attrs: { type: "button" }, on: { click: function () { window.location.reload(); } } }));
    banner.hidden = false;
  });

  connection.start().catch(showGone);
})();
