# Deployment: Oracle Cloud + Caddy

The app runs as a systemd service on an Oracle Cloud Always Free ARM server (Ubuntu, Frankfurt), with Caddy
(HTTPS) in front and Valkey (Redis) on the same machine. The database is Neon (Frankfurt), e-mail is Resend.

## First set-up (once, on the server)

```sh
sudo apt install valkey-server
# Apply the lines in valkey.conf to /etc/valkey/valkey.conf (with a long random password), then:
sudo systemctl enable --now valkey-server
sudo mkdir -p /opt/spygame /etc/spygame
sudo install -m 600 /dev/null /etc/spygame/spygame.env   # then fill it with sudoedit
sudo cp spygame.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable spygame
# Add the block in Caddyfile to /etc/caddy/Caddyfile, then: sudo systemctl reload caddy
```

`/etc/spygame/spygame.env`:

```
DATABASE_URL=postgresql://...                     # Neon, direct connection
REDIS_URL=redis://default:PASSWORD@127.0.0.1:6379  # the requirepass in valkey.conf
RESEND_API_KEY=re_...
ADMIN_EMAILS=...
EMAIL_FROM=Casus <casus@miracdeprem.com>
PUBLIC_ORIGIN=https://spygame.miracdeprem.com
AllowedHosts=spygame.miracdeprem.com
```

## New version

**Update the SDK first.** The package carries the .NET runtime inside it, and the SDK on the computer decides its
version. The app logs it at start-up and warns below 10.0.13 (`journalctl -u spygame | grep runtime`).

```powershell
winget install Microsoft.DotNet.SDK.10
dotnet --list-runtimes
```

On the computer (repo root), a single-folder Linux ARM build that needs no .NET installed. ReadyToRun compiles
the code ahead of time, so the first requests are fast on a small CPU.

```sh
dotnet publish src/SpyGame.Web -c Release -r linux-arm64 --self-contained -p:PublishReadyToRun=true -o dist/linux-arm64
tar -czf dist/spygame.tar.gz -C dist/linux-arm64 .
scp dist/spygame.tar.gz ubuntu@SERVER:/tmp/
ssh ubuntu@SERVER 'sudo rm -rf /opt/spygame/* && sudo tar --no-same-owner -xzf /tmp/spygame.tar.gz -C /opt/spygame && sudo chown -R root:root /opt/spygame && sudo chmod 755 /opt/spygame/SpyGame.Web && sudo systemctl restart spygame'
```

An archive made on Windows does not carry the "executable" flag or the owner: without `chmod` the service fails
with `status=203/EXEC`, and without `--no-same-owner` and `chown` the files belong to a Windows user number.

Migrations run when the app starts. Check: `curl -s https://spygame.miracdeprem.com/healthz` → `Healthy`.
