# Casus — Spy Game

**English** | [Türkçe](README.tr.md)

A real-time party game for 3 to 12 friends, each playing on their own phone. Everyone gets the same secret place except the spy (or spies), who only sees the list of possible places. Players ask each other questions, vote on who the spy is, and a caught spy gets one last chance to name the place.

This is a rewrite of a game I first built in 2025 with ASP.NET Core MVC. That version passed one phone around the table. Every visitor shared the same single game, and the server sent each player's role to the whole room.

**Live:** [spygame.miracdeprem.com](https://spygame.miracdeprem.com)

![A civilian's card, held down: the secret place and the round timer](docs/screenshot-card.png)

## Features

- **Rooms:**
  - Create a room and share its 6-character code, its link or its QR code.
  - The person who creates the room chooses the places, the round length (1–15 minutes) and the number of spies (1–3). The number of spies is lowered at the start if too few people came.
  - Between rounds the host can strike places out of the next rounds with a tap, or bring them back. Any place can be struck, not only one that has come up; at least 10 stay in play.
  - Anyone who reloads the page or loses the connection keeps their seat.
- **Secret cards:**
  - Each phone receives only its own card, and the card shows only while it is held down.
  - The spy never sees the place itself. Everyone in the round gets the same list of the room's places, alphabetical and without groups, so it gives no one away.
  - While the cards are closed, a civilian's screen and a spy's screen look exactly the same.
- **A live round:**
  - A server-side clock counts down the round.
  - "Let's vote" ends the questions early once more than half of the players agree.
  - Everyone votes from their own phone, and votes stay secret until the results.
  - A caught spy who names the place still wins.
- **Scores:** a scoreboard for the room, and "Spies of the week", a weekly leaderboard for confirmed accounts.
- **Accounts (optional):**
  - Sign up with e-mail confirmation to keep your nickname for good and appear on the leaderboard.
  - Guests can play with a nickname that stays theirs for 24 hours.
- **Admin page:** remove a week's points, or take an account off the leaderboard and let it back on.
- **The place list:** 102 places in four numbered dossiers, in Turkish and English. The creator ticks whole dossiers or opens one and picks single places (at least 10); the last choice is remembered on that device.
- **Interface:** Turkish and English, dark and light themes, designed for phones first.

## Screenshots

| Lobby with the invite QR code | The spy's card | Voting |
|---|---|---|
| ![Lobby with players, settings and an invite QR code](docs/screenshot-lobby.png) | ![The spy's card and the list of possible places](docs/screenshot-spy.png) | ![Voting screen with the players as buttons](docs/screenshot-voting.png) |

| Results | Spies of the week | Start page |
|---|---|---|
| ![Results: the civilians win, the place, the votes and the points](docs/screenshot-results.png) | ![Weekly leaderboard](docs/screenshot-leaderboard.png) | ![Start page: the create form with the place dossiers, and the join form](docs/screenshot-home.png) |

| Striking places out between rounds |
|---|
| ![The host's place list after a round: three places struck out](docs/screenshot-places.png) |

## Tech Stack

- **Web:** ASP.NET Core MVC (Models, Views, Controllers) with Razor views, on .NET 10
- **Live updates:** SignalR. The hub only passes messages on; the game rules live in the model layer.
- **Live data:** Redis (Upstash locally, Render Key Value in production) for rooms, sessions, nicknames, deadlines and the weekly sorted sets
- **Lasting data:** PostgreSQL (Neon) with Entity Framework Core, for accounts, score history and data protection keys
- **Accounts:** ASP.NET Core Identity, with e-mail through Resend
- **Other libraries:** QRCoder for the invite QR code, and the SignalR browser client
- **Tests:** xUnit, 137 tests, including whole rounds played over real SignalR connections
- **Hosting:** Docker on Render

## Installation

The quickest way to play is the [live site](https://spygame.miracdeprem.com): open it on three phones and create a room.

To run it locally you need the .NET 10 SDK, a PostgreSQL database (a free [Neon](https://neon.tech) one is enough) and, optionally, a Redis database (a free [Upstash](https://upstash.com) one is enough).

```bash
git clone https://github.com/MrcDprm/spy-game.git
cd spy-game
dotnet user-secrets set DATABASE_URL "postgresql://user:password@host/db?sslmode=require" --project src/SpyGame.Web
dotnet user-secrets set REDIS_URL "rediss://default:password@host:6379" --project src/SpyGame.Web   # optional
dotnet run --project src/SpyGame.Web
```

The tables are created on the first start.

- **Without `REDIS_URL`:** the live game is kept in memory.
- **Without `RESEND_API_KEY`:** e-mails (and their confirmation links) are written to the log.
- **Admin access:** set `ADMIN_EMAILS` to your address.

### Tests

```bash
dotnet test --solution SpyGame.slnx
```

The tests need no outside service. They run the whole app in memory, with SQLite instead of Postgres and an in-memory store instead of Redis. The Redis store tests also run against a real Redis when `REDIS_URL` is set.

### Project structure

```
src/SpyGame.Web/
  Models/Game/          The rules: nicknames, room codes, the place list, dealing cards,
                        voting, scoring, the room state machine and what each player may see
  Models/ViewModels/    Form and page models
  Controllers/          Start page, rooms, accounts, leaderboard, admin
  Views/                Razor views
  Hubs/                 The SignalR channel, per-player messages, a call limit per connection
  Services/             RoomService (every game action), the game clock, sessions,
                        the leaderboard, e-mail, and the Redis / in-memory stores
  Data/                 Entity Framework: accounts, score history, migrations
  wwwroot/              Styles, the room page script, fonts, the SignalR client
tests/SpyGame.Tests/    Model, service and end-to-end tests
```

### Security

- **What reaches each phone:** the server decides everything and sends each player only their own view. The spy's phone never receives the place, and nobody's phone receives who the spies are until the round ends. A test checks that a spy's data is byte for byte the same whatever the place is.
- **Who is who:** a player's identity comes from an HttpOnly session cookie when the connection opens, never from what the page sends, so nobody can vote or guess for someone else.
- **Cookies:** sessions are stored in Redis only as a hash of the cookie.
- **The game channel:** only pages of this site may open it. Browsers send cookies with WebSocket connections too, and CORS does not cover them.
- **Limits:** creating rooms, trying room codes, account forms and hub calls are rate-limited. The visitor's address comes from Cloudflare's `CF-Connecting-IP` header.
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

- **Never trusting the browser with a secret.** The first version sent every role to the whole room, so anyone with the developer tools open could see the spy. Now the server builds a separate view for every player. A test compares what a spy receives for two different places and checks that they are byte for byte equal. Building the screen, I found the same leak in a different form: a "possible places" box outside the card was visible only on the spy's phone, so a glance at the screen gave the spy away. It now lives inside the hidden card.
- **Shared state is the real bug.** The old game kept its state in static fields, so every visitor played the same single game. Now each room is its own object in Redis, saved with a compare-and-set script. Two players voting in the same millisecond cannot overwrite each other, and a test makes 11 players join at once.
- **Rules as a state machine.** Lobby → questions → voting → last guess → results is a set of plain C# functions that take the time as a parameter. That made every rule easy to test, and it found a real bug: after a pause (for example a server waking up), the next phase started from "now" and gave players time that had already passed. Phases now start from the previous deadline.
- **A clock in Redis.** Deadlines live in a sorted set, so the game clock finds every overdue room with one query twice a second.
- **A leaderboard that survives Redis.** Weekly points are a Redis sorted set for fast reads and a database table for history. If the sorted set is missing, it is rebuilt from the database before anything is added, so points are never lost.
- **Testing the whole thing.** The end-to-end tests run the real app in memory and play rounds with several "browsers" over real SignalR connections, checking the raw JSON each one receives.

## Future Plans

- Custom word lists per room
- A spectator mode
- Other game modes, such as spies who know each other

## Credits

- Fonts: [Archivo](https://github.com/Omnibus-Type/Archivo) and [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono) (SIL Open Font License), served from the site itself
- [SignalR JavaScript client](https://github.com/dotnet/aspnetcore) (MIT) and [QRCoder](https://github.com/codebude/QRCoder) (MIT)

## License

[MIT](LICENSE)
