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
    if (remote()) renderCompose(); // the "ask someone else" wait runs out on its own
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
    renderTalk();
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
      var kick = state.youAreHost && p.id !== state.youId
        ? el("button", {
            className: "link-btn players__kick", text: t("kick"),
            attrs: { type: "button", "aria-label": t("kick.label", p.nickname) },
            on: { click: function () { if (window.confirm(t("kick.confirm", p.nickname))) invoke("Kick", p.id); } },
          })
        : null;
      playersList.appendChild(el("li", { className: "players__item" + (p.connected ? "" : " players__item--away") },
        el("span", { className: "players__name", text: p.nickname }), tags,
        el("span", { className: "players__score mono", text: String(p.score) }), kick));
    });
  }

  function settingsSummary() {
    var s = state.settings;
    return el("p", { className: "muted", text: [t(s.remote ? "lobby.remote" : "lobby.together"), t("lobby.places", s.places.length - s.struck.length), t("lobby.minutes", s.roundSeconds / 60), t("lobby.spies", s.spyCount)].join(" · ") });
  }

  // Between rounds: the room's places, struck ones crossed out. The host taps one to strike it out
  // of the next rounds or bring it back; everyone else sees the same list without buttons.
  var placesOpen = false;
  function placesBoard() {
    var s = state.settings;
    var list = el("ul", { className: "places" });
    sorted(s.places).forEach(function (w) {
      var struck = s.struck.indexOf(w.id) >= 0;
      var cls = "place" + (struck ? " place--struck" : "");
      var item = state.youAreHost
        ? el("button", {
            className: cls + " place--pick", text: word(w),
            attrs: { type: "button", "aria-pressed": String(struck) },
            on: { click: function () { invoke("StrikePlace", w.id); } },
          })
        : el("span", { className: cls, text: word(w) });
      list.appendChild(el("li", {}, item));
    });
    var box = el("details", { className: "card places-box", attrs: placesOpen ? { open: "" } : {} },
      el("summary", { text: t("places.title", s.places.length - s.struck.length, s.places.length) }),
      el("p", { className: "muted small", text: t(state.youAreHost ? "places.hostHint" : "places.hint") }),
      list);
    box.addEventListener("toggle", function () { placesOpen = box.open; });
    return box;
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
    stage.appendChild(el("div", { className: "stack" },
      el("div", { className: "card card--paper" },
        el("h1", { className: "stage__title", text: t("lobby.title") }),
        settingsSummary(),
        hostButton(t("lobby.start"))),
      placesBoard()));
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

  // Together: who asks the first question. Apart: whose turn it is now.
  function askingLine() {
    var q = state.questions;
    if (!q) return t("asking.first", nameOf(state.firstAskerId));
    return q.turnId === state.youId ? t("asking.turnYou") : t("asking.turnOf", nameOf(q.turnId));
  }

  function renderAsking() {
    var me = player(state.youId);
    var inRound = state.players.filter(function (p) { return p.inRound && p.connected; });
    var ready = inRound.filter(function (p) { return p.ready; }).length;
    var readyBtn = button(me.ready ? t("asking.readyDone") : t("asking.ready"), function () { invoke("ReadyToVote"); }, "btn btn--ghost btn--block");
    readyBtn.disabled = me.ready;
    stage.appendChild(el("div", { className: "stack" },
      holdCard(),
      el("p", { className: "first-asker", text: askingLine() }),
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
      placesBoard(),
      el("a", { className: "board-link", text: t("result.board") + " →", attrs: { href: "/leaderboard", target: "_blank", rel: "noopener" } })));
  }

  // --- questions and chat (groups playing apart) ---
  // The panel lives outside the stage. Its lists are redrawn when they change, but the input
  // boxes are only rebuilt when what this player can do changes, so typing survives everyone
  // else's messages. On phones it is a sheet at the bottom; on wide screens a card in the side.

  var talk = root.querySelector("[data-talk]");
  var talkToggle = talk.querySelector("[data-talk-toggle]");
  var talkStatus = talk.querySelector("[data-talk-status]");
  var talkBadge = talk.querySelector("[data-talk-badge]");
  var questionBox = talk.querySelector('[data-compose="questions"]');
  var chatForm = talk.querySelector('[data-compose="chat"]');
  var logs = { questions: talk.querySelector('[data-log="questions"]'), chat: talk.querySelector('[data-log="chat"]') };
  var panes = { questions: talk.querySelector('[data-pane="questions"]'), chat: talk.querySelector('[data-pane="chat"]') };
  var tabBadges = { questions: talk.querySelector('[data-tab-badge="questions"]'), chat: talk.querySelector('[data-tab-badge="chat"]') };
  var wide = window.matchMedia("(min-width: 960px)");

  var talkOpen = false;
  var tab = "questions";
  var shown = { questions: "", chat: "" }; // what each log shows now, to skip needless redraws
  var composeKey = "";
  var lastNeed = "";                        // the last time this player had to act (opens the sheet once)
  var askTarget = null;
  var countdown = null;
  var seenKey = "sg-seen-" + code;
  var seen = { round: 0, questions: 0, chat: 0 };
  try { seen = JSON.parse(sessionStorage.getItem(seenKey)) || seen; } catch (e) { /* storage blocked */ }

  function remote() { return !!(state && state.settings.remote); }
  function showing(name) { return (wide.matches || talkOpen) && tab === name && !document.hidden; }
  function nearBottom(log) { return log.scrollHeight - log.scrollTop - log.clientHeight < 40; }
  function scrollDown(log, stick) { if (stick) log.scrollTop = log.scrollHeight; }

  function setOpen(open) {
    talkOpen = open;
    talk.classList.toggle("talk--open", open);
    talkToggle.setAttribute("aria-expanded", String(open));
    placeTalk();
    if (open) scrollDown(logs[tab], true);
    markSeen();
  }

  function setTab(name) {
    tab = name;
    Object.keys(panes).forEach(function (k) { panes[k].hidden = k !== name; });
    talk.querySelectorAll("[data-tab]").forEach(function (b) { b.setAttribute("aria-selected", String(b.dataset.tab === name)); });
    scrollDown(logs[name], true);
    markSeen();
  }

  talkToggle.addEventListener("click", function () { if (!wide.matches) setOpen(!talkOpen); });
  talk.querySelectorAll("[data-tab]").forEach(function (b) { b.addEventListener("click", function () { setTab(b.dataset.tab); }); });
  talk.addEventListener("keydown", function (e) { if (e.key === "Escape" && talkOpen) { setOpen(false); talkToggle.focus(); } });
  wide.addEventListener("change", function () { placeTalk(); markSeen(); });
  document.addEventListener("visibilitychange", markSeen);

  // With the on-screen keyboard open, keep the sheet just above it (iOS leaves fixed elements behind it).
  function placeTalk() {
    var vv = window.visualViewport;
    var lift = wide.matches || !vv ? 0 : Math.max(0, window.innerHeight - vv.height - vv.offsetTop);
    talk.style.setProperty("--kb", lift + "px");
  }
  if (window.visualViewport) {
    window.visualViewport.addEventListener("resize", placeTalk);
    window.visualViewport.addEventListener("scroll", placeTalk);
  }

  // --- unread counts ---

  function setBadge(node, n) { node.hidden = n <= 0; node.textContent = n > 9 ? "9+" : String(n); }

  function markSeen() {
    if (!remote()) return;
    if (seen.round !== state.round) { seen.round = state.round; seen.questions = 0; }
    var questions = state.questions ? state.questions.entries.length : 0;
    var lastChat = state.chat.length ? state.chat[state.chat.length - 1].id : 0;
    if (showing("questions")) seen.questions = questions;
    if (showing("chat")) seen.chat = lastChat;
    try { sessionStorage.setItem(seenKey, JSON.stringify(seen)); } catch (e) { /* storage blocked */ }
    var q = Math.max(0, questions - seen.questions);
    var c = state.chat.filter(function (m) { return m.id > seen.chat && m.playerId !== state.youId; }).length;
    setBadge(tabBadges.questions, q);
    setBadge(tabBadges.chat, c);
    setBadge(talkBadge, wide.matches || talkOpen ? 0 : q + c);
  }

  // --- the logs ---

  function renderQuestionLog() {
    var q = state.questions;
    var key = JSON.stringify(q ? q.entries : null) + "|" + state.players.map(function (p) { return p.id; }).join();
    if (key === shown.questions) return;
    shown.questions = key;
    var log = logs.questions, stick = nearBottom(log);
    log.replaceChildren();
    if (!q || q.entries.length === 0) {
      log.appendChild(el("li", { className: "talk__empty muted small", text: t(q ? "talk.noQuestionsYet" : "talk.noQuestions") }));
      return;
    }
    q.entries.forEach(function (e) {
      var toMe = e.answer == null && !e.unanswered && e.targetId === state.youId && state.phase === "asking";
      var answer = e.answer != null
        ? el("p", { className: "qa__answer" }, el("strong", { text: nameOf(e.targetId) + ": " }), document.createTextNode(e.answer))
        : el("p", { className: "qa__answer qa__answer--none", text: t(e.unanswered ? "talk.unanswered" : "talk.waiting") });
      log.appendChild(el("li", { className: "qa" + (toMe ? " qa--ask" : "") },
        el("p", { className: "qa__who small", text: nameOf(e.askerId) + " → " + nameOf(e.targetId) }),
        el("p", { className: "qa__question", text: e.question }),
        answer));
    });
    scrollDown(log, stick);
  }

  function renderChatLog() {
    var key = JSON.stringify(state.chat) + state.youAreHost;
    if (key === shown.chat) return;
    shown.chat = key;
    var log = logs.chat, stick = nearBottom(log);
    log.replaceChildren();
    if (state.chat.length === 0) {
      log.appendChild(el("li", { className: "talk__empty muted small", text: t("talk.noChat") }));
      return;
    }
    state.chat.forEach(function (m) {
      var mine = m.playerId === state.youId;
      var line = el("li", { className: "chat" + (mine ? " chat--mine" : "") }, el("span", { className: "chat__name", text: m.nickname }));
      if (m.hidden) {
        line.appendChild(el("span", { className: "chat__text chat__text--hidden", text: t("talk.hidden") }));
      } else {
        line.appendChild(el("span", { className: "chat__text", text: m.text }));
        if (state.youAreHost && !mine) {
          line.appendChild(el("button", {
            className: "link-btn chat__hide", text: t("talk.hide"), attrs: { type: "button" },
            on: { click: function () { invoke("HideMessage", m.id); } },
          }));
        }
      }
      log.appendChild(line);
    });
    scrollDown(log, stick);
  }

  chatForm.addEventListener("submit", function (e) {
    e.preventDefault();
    var input = chatForm.elements.message;
    if (!input.value.trim()) return;
    invoke("SendChat", input.value).then(function (error) { if (error === null) input.value = ""; });
  });

  // --- whose turn it is, and what this player can do ---

  function questionAction() {
    var q = state.questions;
    var me = player(state.youId);
    if (state.phase !== "asking" || !q) return { kind: "none" };
    if (!(me && me.inRound)) return { kind: "watch" };
    var last = q.entries[q.entries.length - 1];
    var pending = last && last.answer == null && !last.unanswered ? last : null;
    if (pending && pending.targetId === state.youId) return { kind: "answer", entry: pending };
    if (q.turnId === state.youId) {
      if (!pending) return { kind: "ask" };
      var left = Math.ceil((Date.parse(q.reaskAt) - (Date.now() + clockOffset)) / 1000);
      return left <= 0 ? { kind: "ask", reask: true } : { kind: "wait", pending: pending, left: left };
    }
    return { kind: "wait", pending: pending };
  }

  function statusText(a) {
    if (a.kind === "ask") return t("talk.yourTurn");
    if (a.kind === "answer") return t("talk.askedYou", nameOf(a.entry.askerId));
    if (a.kind === "wait") return a.pending ? t("talk.waitingFor", nameOf(a.pending.targetId)) : t("talk.turnOf", nameOf(state.questions.turnId));
    return t("talk.title");
  }

  function textForm(placeholder, label, send) {
    var input = el("input", { className: "talk__input", attrs: { type: "text", maxlength: "200", enterkeyhint: "send", placeholder: placeholder, "aria-label": placeholder } });
    var form = el("form", { className: "talk__row", attrs: { autocomplete: "off" } }, input, el("button", { className: "btn talk__send", text: label, attrs: { type: "submit" } }));
    form.addEventListener("submit", function (e) {
      e.preventDefault();
      if (input.value.trim()) send(input);
    });
    return form;
  }

  function renderCompose() {
    var a = questionAction();
    var others = state.players.filter(function (p) { return p.inRound && p.id !== state.youId; });
    var q = state.questions;
    var key = [a.kind, state.round, q ? q.entries.length : 0, q ? q.turnId : "", a.reask ? 1 : 0, state.youAreHost,
      a.kind === "ask" ? others.map(function (p) { return p.id + (p.connected ? "" : "~"); }).join() : ""].join("|");

    talkStatus.textContent = statusText(a);
    talkToggle.classList.toggle("talk__handle--alert", a.kind === "ask" || a.kind === "answer");
    if (countdown && a.kind === "wait" && a.left) countdown.textContent = t("talk.reaskIn", a.left);
    if (key === composeKey) return;

    // Keep what was being typed when only the details changed (someone joined, the round moved on).
    var old = questionBox.querySelector("input.talk__input");
    var draft = old && composeKey.split("|")[0] === a.kind ? old.value : "";
    composeKey = key;
    countdown = null;
    questionBox.replaceChildren();
    questionBox.hidden = a.kind === "none";

    if (a.kind === "watch") questionBox.appendChild(el("p", { className: "talk__note", text: t("talk.watching") }));

    if (a.kind === "wait") {
      // Whose turn it is already heads the panel; here only the countdown and the host's way out.
      if (a.pending && a.pending.askerId === state.youId && a.left) {
        countdown = el("p", { className: "talk__note small", text: t("talk.reaskIn", a.left) });
        questionBox.appendChild(countdown);
      }
      if (state.youAreHost) questionBox.appendChild(button(t("talk.skip"), function () { invoke("SkipTurn"); }, "btn btn--ghost talk__skip"));
    }

    if (a.kind === "ask") {
      if (a.reask) questionBox.appendChild(el("p", { className: "talk__note", text: t("talk.reaskNow") }));
      if (!others.some(function (p) { return p.id === askTarget; })) askTarget = null;
      var targets = el("div", { className: "chips targets", attrs: { role: "radiogroup", "aria-label": t("talk.pickTarget") } });
      others.forEach(function (p) {
        var radio = el("input", { attrs: { type: "radio", name: "ask-target", value: p.id } });
        radio.checked = p.id === askTarget;
        radio.addEventListener("change", function () { askTarget = p.id; });
        targets.appendChild(el("label", { className: "chip" + (p.connected ? "" : " chip--away") }, radio, el("span", { text: p.nickname })));
      });
      var form = textForm(t("talk.questionPlaceholder"), t("talk.ask"), function (input) {
        if (!askTarget) { showToast(t("talk.pickTarget")); return; }
        invoke("Ask", askTarget, input.value).then(function (error) { if (error === null) { input.value = ""; askTarget = null; } });
      });
      form.querySelector("input").value = draft;
      questionBox.append(el("p", { className: "talk__label", text: t("talk.pickTarget") }), targets, form);
    }

    if (a.kind === "answer") {
      // The question itself is right above, marked in the log.
      var answerForm = textForm(t("talk.answerPlaceholder"), t("talk.answer"), function (input) {
        invoke("Answer", input.value).then(function (error) { if (error === null) input.value = ""; });
      });
      answerForm.querySelector("input").value = draft;
      questionBox.appendChild(answerForm);
    }

    // The moment this player has to act, bring the questions to them (once per question).
    var need = a.kind === "ask" || a.kind === "answer" ? [a.kind, state.round, q.entries.length, a.reask ? 1 : 0].join("|") : "";
    if (need && need !== lastNeed) {
      setTab("questions");
      if (!wide.matches) setOpen(true);
      if (navigator.vibrate) navigator.vibrate(120);
    }
    lastNeed = need;
  }

  function renderTalk() {
    var on = remote();
    talk.hidden = !on;
    root.classList.toggle("room--talk", on);
    if (!on) return;
    renderQuestionLog();
    renderChatLog();
    renderCompose();
    markSeen();
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
  // The host removed this player: the room is gone for them, and so is the connection.
  var kicked = false;
  connection.on("kicked", function () {
    kicked = true; // closing the connection below must not replace this message
    state = null;
    connection.stop();
    stage.replaceChildren(el("div", { className: "card" },
      el("p", { text: t("kicked") }), el("a", { className: "btn", text: t("home"), attrs: { href: "/" } })));
    invitePanel.hidden = true;
    timer.hidden = true;
  });
  connection.onreconnecting(function () { banner.textContent = t("connection.lost"); banner.hidden = false; });
  connection.onreconnected(function () { banner.hidden = true; });
  connection.onclose(function () {
    if (kicked) return;
    if (!state) { showGone(); return; } // the server refused us: not (or no longer) in this room
    banner.replaceChildren(document.createTextNode(t("connection.closed") + " "),
      el("button", { className: "link-btn", text: t("connection.retry"), attrs: { type: "button" }, on: { click: function () { window.location.reload(); } } }));
    banner.hidden = false;
  });

  connection.start().catch(showGone);
})();
