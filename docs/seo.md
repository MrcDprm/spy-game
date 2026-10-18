# SEO: Casus (Spy Game)

Site: https://spygame.miracdeprem.com (Turkish by default, English with `?lang=en`)

Only the home page and the weekly leaderboard are for search. Rooms, joining, accounts and the admin page are private or change every minute: they carry `noindex` and are closed in `robots.txt`.

## Keywords

| Type | Turkish | English |
|---|---|---|
| General | casus oyunu, online parti oyunu, arkadaşlarla oynanacak oyunlar | spy game, online party game, games to play with friends |
| Specific | casus oyunu online, spyfall Türkçe, casus kim oyunu, telefondan oynanan grup oyunu | spyfall online, who is the spy game, party game on phones, spy word game |
| Long | arkadaşlarla telefondan oynanan ücretsiz oyun, kalabalık grup oyunu online | free party game to play with friends on phones |

## Where they are used

- `<title>` "Casus oyunu online: arkadaşlarınla telefondan oyna (Spyfall)" and the leaderboard title; meta description per page
- Canonical + `hreflang` (tr, en, x-default); `?lang=en` wins over the cookie
- Open Graph and Twitter card with `/og-image.png`
- JSON-LD `VideoGame` (party game, multiplayer, free) on the home page, `WebPage` on the leaderboard
- `/sitemap.xml` and `/robots.txt`, GET and HEAD

## Google Search Console

The `miracdeprem.com` domain property covers this subdomain. Submit `https://spygame.miracdeprem.com/sitemap.xml` while the site is awake (Render free plan).
