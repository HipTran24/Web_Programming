# MoMo Premium Payment Demo

This project uses a simple real MoMo sandbox flow for the Premium package.

## Flow

1. User opens `/home/premium-upgrade.html`.
2. Frontend calls `POST /api/payments/momo/create`.
3. Backend creates a pending `PaymentTransaction`, signs the MoMo request, and returns `payUrl`.
4. User pays on MoMo sandbox.
5. MoMo calls `POST /api/payments/momo/ipn`.
6. Backend verifies the IPN signature and grants `PREMIUM` for the configured number of days.
7. `/premium/*` pages are unlocked while the subscription is active.

## Required configuration

Set these values in `appsettings.Development.json`, environment variables, or deployment settings:

```json
"MoMo": {
  "PartnerCode": "YOUR_SANDBOX_PARTNER_CODE",
  "AccessKey": "YOUR_SANDBOX_ACCESS_KEY",
  "SecretKey": "YOUR_SANDBOX_SECRET_KEY",
  "PublicBaseUrl": "https://your-public-ngrok-url.ngrok-free.app",
  "PremiumAmount": 10000,
  "PremiumDays": 30
}
```

`PublicBaseUrl` must be reachable from the internet. If you run locally, expose the app with a tunnel such as ngrok, otherwise MoMo cannot call the IPN endpoint.

## Demo URL

After login as a normal user, open:

```text
/home/premium-upgrade.html
```

If payment succeeds and IPN is received, open:

```text
/premium/dashboard.html
```
