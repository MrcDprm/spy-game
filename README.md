# Casus — Spy Game

**English** | [Türkçe](README.tr.md)

> Work in progress: a rewrite of my 2025 ASP.NET Core MVC spy game as a real-time, multi-device party game.

Everyone joins a room from their own phone. All players get the same secret word except the spy (or spies), who only see the category. Players ask each other questions, then vote on who the spy is. If the spy is caught, they get one last chance to guess the word.

**Live:** spygame.miracdeprem.com (coming soon)

## Features (planned)

- **Rooms:** create a room with a 6-character code, a link or a QR code. When creating it, choose the categories, the round length and the number of spies (1–3). Up to 12 players. Rejoin after a page refresh or a lost connection.
- **Rounds:** the server picks the spies and the word. Each player gets only their own card, shown while it is held down. The spy sees the list of possible words, never the word itself.
- **Voting:** everyone votes from their own phone and the result appears live. A caught spy can still win by guessing the word.
- **Scores:**
  - A scoreboard for the room across rounds, kept in Redis.
  - A weekly "Spies of the week" leaderboard. Only accounts with a confirmed e-mail appear on it, and the admin can remove an account or a score.
- **Accounts (optional):** play as a guest with a nickname, or sign up with a confirmed e-mail to keep your name and appear on the leaderboard.
- **Nicknames:**
  - Lowercase letters `a-z` and digits only, 3–20 characters, starting with a letter.
  - An account's name is reserved for good; a guest's name is reserved while their session is active.
- **10 categories and about 150 words**, in Turkish and English.
- **Turkish and English**, dark and light theme, designed for phones first.

## Tech Stack

- ASP.NET Core MVC (Models, Views, Controllers) with Razor views
- SignalR for the live parts, with the game rules on the server
- Redis for live rooms and the leaderboard
- PostgreSQL (Neon) with Entity Framework Core for accounts and score history
- ASP.NET Core Identity, with e-mail confirmation through Resend
- xUnit tests
- Docker on Render

## Future Plans

- Custom word lists per room
- Spectator mode
- Other game modes
