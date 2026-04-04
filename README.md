# Web_Programming
ASP.NET MVC web application.

##Hash
- PBKDF2-HMAC-SHA256

## Docker Deploy
1. Copy `.env.docker.example` to `.env.docker`.
2. Update at least `DB_SA_PASSWORD` and `JWT_SECRET_KEY` with strong secrets.
3. Optional: fill `SMTP_*`, `GROQ_API_KEY`, `GEMINI_API_KEY`, `GOOGLE_AUTH_CLIENT_ID`, and `ADMIN_ACCOUNT_*`.
4. Build and start:

```bash
docker compose --env-file .env.docker up -d --build
```

5. View logs:

```bash
docker compose --env-file .env.docker logs -f web
```

6. Stop services:

```bash
docker compose --env-file .env.docker down
```

Notes:
- App runs on port `8080` by default.
- SQL Server data is persisted in the `sqlserver_data` Docker volume.
- `appsettings.Development.json` is excluded from Docker build/publish so development secrets are not baked into the image.
- Current compose setup uses a strong symmetric JWT secret for deployment. Keep it at least 32 characters long.
