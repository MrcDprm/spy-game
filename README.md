# Casus — Spy Game

**English** | [Türkçe](README.tr.md)

A real-time spy party game for 3 to 12 friends, each playing on their own phone. Everyone gets the same secret place and their own role there, except the spy, who only sees the list of possible places. Players ask each other questions and vote on who the spy is, while the spy listens for a slip: naming the place during the questions or after being caught wins the round for the spies.

This is a rewrite of a game I first built in 2025 with ASP.NET Core MVC. That version passed one phone around the table, every visitor shared the same single game, and the server sent each player's role to the whole room.

**Live:** [spygame.miracdeprem.com](https://spygame.miracdeprem.com)

![A civilian's card, held down: the place, the role and the round timer](docs/screenshot-card.png)

## Features

- **Rooms:**
  - Create a room and share its 6-character code, its link or its QR code.
  - The person who creates the room chooses the places, the round length of 1 to 15 minutes, the number of spies from 1 to 3 and whether the group plays together or apart. The number of spies is lowered at the start if too few people came.
  - Between rounds the host can strike places out of the next rounds with a tap, or bring them back. Any place can be struck, not only one that has come up; at least 10 stay in play.
  - Anyone who reloads the page or loses the connection keeps their seat.
- **Places and roles:**
  - 202 places in eight numbered dossiers, in Turkish and English. Everyday ones such as a hospital, a barber's or a ferry, and unusual ones such as Inside the Fridge, an alien spaceship or a residents' meeting.
  - Every place has seven roles. Each civilian gets one at random, a nurse or a patient's relative at the hospital, so the answers differ naturally and the spy can blend in.
  - The creator ticks whole dossiers or opens one and picks single places; at least 10 are needed. A new room starts with 25 well-known places, and the last choice is remembered on that device.
- **Secret cards:**
  - Each phone receives only its own card, and the card shows only while it is held down.
  - A civilian's card shows the place and their role. The spy's card shows neither, in the same layout and colours, so an open card seen across the table does not give the spy away.
  - Everyone in the round gets the same list of possible places, open on every phone, so reading it singles no one out. While the cards are closed, every screen looks exactly the same.
- **A live round:**
  - A server-side clock counts down the round.
  - The first question moves around: whoever asked first in the last two rounds sits out that draw. The spy is drawn purely at random every round. A take-turns rule would let players work out who is due.
  - The spy can stop the questions at any moment and name the place. Right, and the spies win; wrong, and the civilians win. This keeps everyone careful, since a question that gives the place away can lose the round.
  - Suggest a question gives anyone who runs out of ideas a question that works for any place.
  - Let's vote ends the questions early once more than half of the players agree. Everyone votes from their own phone, and votes stay secret until the results.
  - A caught spy who names the place still wins. The results show every civilian's role and every vote.
- **Playing apart:** optional, for friends who are not in the same place.
  - **Questions in turn:** whoever has the turn picks someone and types a question, or fills the box with a suggested one. Everyone sees the question and the answer, and the turn passes to the one who answered. If no answer comes within a minute, the asker can turn to someone else, and the host can pass the turn on. The whole log is shown with the results.
  - **Room chat:** open all the time, kept only with the room, the last 50 lines, with a per-player rate limit.
  - **Built for phones:** a sheet at the bottom with Questions and Chat tabs and unread counts. It opens by itself when it is your turn, stays above the on-screen keyboard, and never loses what you are typing when others write.
  - **Host controls:** hide a chat line, or remove a player, who then cannot join that room again.
- **Scores:** a scoreboard for the room that also shows, between rounds, how many rounds each player has been the spy. Spies of the week is a weekly leaderboard for confirmed accounts.
- **Accounts:** optional.
  - Sign up with e-mail confirmation to keep your nickname for good and appear on the leaderboard.
  - Guests can play with a nickname that stays theirs for 24 hours.
- **Admin page:** remove a week's points, or take an account off the leaderboard and let it back on.
- **What's new:** a short note about new features opens once per browser, never over a round in play.
- **Interface:** Turkish and English, dark and light themes, designed for phones first.

## Screenshots

| Lobby with the invite QR code | The spy's card | Voting |
|---|---|---|
| ![Lobby with players, settings and an invite QR code](docs/screenshot-lobby.png) | ![The spy's card held down: no place, the role Spy, and the open list of possible places](docs/screenshot-spy.png) | ![Voting screen with the players as buttons](docs/screenshot-voting.png) |

| The spy names the place | Results | Spies of the week |
|---|---|---|
| ![The spy's list for naming the place during the questions](docs/screenshot-reveal.png) | ![Results: the civilians win, the place, everyone's role, the votes and the points](docs/screenshot-results.png) | ![Weekly leaderboard](docs/screenshot-leaderboard.png) |

| Start page | Striking places out between rounds |
|---|---|
| ![Start page: the create form with the place dossiers, and the join form](docs/screenshot-home.png) | ![The host's place list after a round: three places struck out](docs/screenshot-places.png) |

| Playing apart: your turn to ask | The questions so far | Room chat |
|---|---|---|
| ![The bottom sheet on a phone: pick a player, a suggested question in the box](docs/screenshot-apart-ask.png) | ![Two questions and their answers, and whose turn it is](docs/screenshot-apart-questions.png) | ![Chat messages during a round](docs/screenshot-apart-chat.png) |

## Tech Stack

- **Web:** ASP.NET Core MVC with Razor views, on .NET
- **Live updates:** SignalR. The hub only passes messages on; the game rules live in the model layer.
- **Live data:** Redis for rooms, sessions, nicknames, deadlines and the weekly sorted sets: Upstash on a development machine, Valkey on the server
- **Lasting data:** PostgreSQL on Neon with Entity Framework Core, for accounts, score history and data protection keys
- **Game content:** the places and their roles in one JSON file built into the app and checked when it starts
- **Accounts:** ASP.NET Core Identity, with e-mail through Resend
- **Other libraries:** QRCoder for the invite QR code, and the SignalR browser client
- **Tests:** xUnit, 205 tests, including whole rounds played over real SignalR connections and statistical checks of the spy draw
- **Hosting:** an Oracle Cloud server with Caddy in front and a systemd service, see [deploy/README.md](deploy/README.md)

## Installation

The quickest way to play is the [live site](https://spygame.miracdeprem.com): open it on three phones and create a room.

To run it locally you need the .NET SDK, a PostgreSQL database and, optionally, a Redis database. Free [Neon](https://neon.tech) and [Upstash](https://upstash.com) databases are enough.

```bash
git clone https://github.com/MrcDprm/spy-game.git
cd spy-game
dotnet user-secrets set DATABASE_URL "postgresql://user:password@host/db?sslmode=require" --project src/SpyGame.Web
dotnet user-secrets set REDIS_URL "rediss://default:password@host:6379" --project src/SpyGame.Web   # optional
dotnet run --project src/SpyGame.Web
```

The tables are created on the first start.

- **Without `REDIS_URL`:** the live game is kept in memory.
- **Without `RESEND_API_KEY`:** e-mails and their confirmation links are written to the log.
- **Admin access:** set `ADMIN_EMAILS` to your address.

### Tests

```bash
dotnet test --solution SpyGame.slnx
```

The tests need no outside service. They run the whole app in memory, with SQLite instead of Postgres and an in-memory store instead of Redis. The Redis store tests also run against a real Redis when `REDIS_URL` is set.

### Project structure

```
src/SpyGame.Web/
  Models/Game/          The rules: nicknames, room codes, the places and roles in places.json,
                        dealing cards and roles, voting, scoring, the room state machine
                        and what each player may see
  Models/ViewModels/    Form and page models
  Controllers/          Start page, rooms, accounts, leaderboard, admin
  Views/                Razor views
  Hubs/                 The SignalR channel, per-player messages, a call limit per connection
  Services/             RoomService for every game action, the game clock, sessions,
                        the leaderboard, e-mail, and the Redis and in-memory stores
  Data/                 Entity Framework: accounts, score history, migrations
  Resources/            The interface texts in Turkish and English
  wwwroot/              Styles, the room page script, fonts, the SignalR client
tests/SpyGame.Tests/    Model, service and end-to-end tests
```

### Security

- **What reaches each phone:** the server decides everything and sends each player only their own view. The spy's phone never receives the place or a role, a civilian's phone receives only its own role, and nobody's phone receives who the spies are until the round ends. A test checks that a spy's data is byte for byte the same whatever the place is.
- **Nothing to read from the scoreboard:** spy counts change only when a round ends, so they never hint at the spy of the round being played.
- **Who is who:** a player's identity comes from an HttpOnly session cookie when the connection opens, never from what the page sends, so nobody can vote or guess for someone else. Only a spy can name the place during the questions; the server checks it.
- **Cookies:** sessions are stored in Redis only as a hash of the cookie.
- **The game channel:** only pages of this site may open it. Browsers send cookies with WebSocket connections too, and CORS does not cover them.
- **Limits:** creating rooms, trying room codes, account forms and hub calls are rate-limited. The visitor's address is the one Caddy sees; forwarded headers are believed only from Caddy on the same machine, and a test checks that forged ones do not reset the limit.
- **Accounts:**
  - Hashed passwords, confirmed e-mail before logging in, and a lockout after 5 wrong passwords.
  - Sign-up and password reset never reveal whether an address is registered.
- **Forms:** all of them use anti-forgery tokens.
- **Content Security Policy:**
  - No inline scripts, no outside sources and Trusted Types.
  - The page builds everything with `createElement` and `textContent`.
  - The only connection allowed is back to this site.
- **Errors:** visitors see friendly error pages; details stay in the log.

## What I Learned

- **Never trusting the browser with a secret.** The first version sent every role to the whole room, so anyone with the developer tools open could see the spy. Now the server builds a separate view for every player. A test compares what a spy receives for two different places and checks that they are byte for byte equal. Building the screen, I found the same leak in a different form: a box of possible places outside the card was visible only on the spy's phone, so a glance at the screen gave the spy away. Now everyone in the round gets the same list, so it tells no one anything.
- **A secret can leak through the look of a screen.** Friends said the spy was easy to spot. Part of it was the screen: an open spy card had a large tilted red stamp where a civilian's had plain text, so a glance across the table was enough, and only the spy ever opened the list of places. Now both cards have the same lines in the same style, the list starts open on every phone, and the spy's guess button sits on every screen.
- **Game balance lives in the code too.** The spy could only guess after being caught, so civilians lost nothing by asking questions that gave the place away, and without roles every civilian answered from the same point of view. Two rules that games of this kind use fixed it: the spy may name the place at any moment, and every civilian gets a role. A civilian's role depends only on their seat and one shuffle, never on who the spies are, so it gives nothing away about them.
- **Content as data.** 202 places and 1,414 roles live in one JSON file built into the app. It is checked when the app starts: unique ids and names, seven different roles for every place, both languages. A broken file stops the start instead of failing in the middle of a game, and a test checks that none of the original 102 places went missing.
- **Shared state is the real bug.** The old game kept its state in static fields, so every visitor played the same single game. Now each room is its own object in Redis, saved with a compare-and-set script. Two players voting in the same millisecond cannot overwrite each other, and a test makes 11 players join at once.
- **Rules as a state machine.** Lobby, questions, voting, last guess and results are plain C# functions that take the time as a parameter. That made every rule easy to test, and it found a real bug: after a pause, for example a server waking up, the next phase started from the current time and gave players time that had already passed. Phases now start from the previous deadline.
- **A clock in Redis.** Deadlines live in a sorted set, so the game clock finds every overdue room with one query twice a second.
- **A leaderboard that survives Redis.** Weekly points are a Redis sorted set for fast reads and a database table for history. If the sorted set is missing, it is rebuilt from the database before anything is added, so points are never lost.
- **A redraw that ate what you typed.** The room page redraws its screen from every update, which was fine until players could type: each chat line from someone else wiped the question being written. The questions and chat panel now lives outside the redrawn area; its lists are refreshed, but an input box is only rebuilt when what the player can do changes.
- **Measuring a feeling of unfairness, twice.** First a friend kept asking the first question, then another said they had been the spy nine times in a row. Both times I played the real engine: 1.3 million rounds the second time. Every player's share stayed within half a point of the expected one, and being the spy in one round changed nothing about the next. Nine in a row with four players happens about once in 4,000 evenings, while three or four in a row happen almost every evening, and those are the runs people remember. So the spy stays purely random, a test checks that rounds are independent, and the scoreboard shows how often each player has been the spy.
- **Playing the screens, not only testing the code.** A script plays whole rounds with headless phones and takes the screenshots on this page. It found what the unit tests could not: a button that was invisible on the paper card in the dark theme, a dialog that opened scrolled to its last line, and a new phase that kept the old scroll position.
- **Testing the whole thing.** The end-to-end tests run the real app in memory and play rounds with several browsers over real SignalR connections, checking the raw JSON each one receives.

## Credits

- Fonts: [Archivo](https://github.com/Omnibus-Type/Archivo) and [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono), under the SIL Open Font License, served from the site itself
- [SignalR JavaScript client](https://github.com/dotnet/aspnetcore) and [QRCoder](https://github.com/codebude/QRCoder), both under the MIT license

## License

[MIT](LICENSE)
