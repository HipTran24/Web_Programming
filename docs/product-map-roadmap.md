# Product Map And Roadmap

## 1) Page Map (Current State)

### Core learning flow
- /home/upload.html
  - Status: LIVE
  - Data/API: /api/summary/upload, /api/summary/text, /api/summary/from-url, /api/quiz/generate, /api/quiz/submit
  - JS: /js/upload.js
- /home/quiz-result.html
  - Status: LIVE
  - Data source: browser storage key `quiz.latestResult.v1`
  - JS: /js/quiz-result.js
- /home/content-list.html
  - Status: LIVE (unified content + history center)
  - Data/API: GET /api/contents, DELETE /api/contents/{id}
  - JS: /js/content-list.js
- /home/content-detail.html
  - Status: LIVE (upgraded from mock)
  - Data/API: GET /api/contents/{id}, DELETE /api/contents/{id}
  - JS: /js/content-detail.js

### Tracking and analytics
- /home/dashboard.html
  - Status: LIVE
  - Data/API: /api/dashboard/overview
  - JS: /js/dashboard.inline.js
- /home/analytics.html
  - Status: LIVE
  - Data/API: /api/dashboard/analytics
  - JS: /js/analytics.js
- /home/history.html
  - Status: REMOVED (merged into /home/content-list.html)

### Auth
- /home/login.html
  - Status: LIVE
  - Data/API: /api/auth/login, /api/auth/google-login, /api/auth/google-config
  - JS: /js/login.js
- /home/register.html
  - Status: LIVE
  - Data/API: /api/auth/register
  - JS: /js/register.js
- /home/otp.html
  - Status: LIVE
  - Data/API: /api/auth/verify-email-otp, /api/auth/resend-email-otp
  - JS: /js/otp.js

### Informational/static pages
- /home/index.html: Marketing page (mostly static, with app shell)
- /home/about.html: Static info page
- /home/guide.html: Static guide page

### Partially complete pages
- /home/quiz.html
  - Status: PARTIAL (UI exists, missing full data-driven flow)
- /home/admin.html
  - Status: PARTIAL/STATIC (no dedicated data JS yet)
- /home/user.html, /home/profile.html
  - Status: PARTIAL (profile has inline behavior, still needs deeper account settings and API-backed preferences)

## 2) API Map (Learning Product)

- Auth
  - POST /api/auth/login
  - POST /api/auth/google-login
  - GET /api/auth/google-config
  - GET /api/auth/me
  - POST /api/auth/register
  - POST /api/auth/verify-email-otp
  - POST /api/auth/resend-email-otp

- Summary and history
  - POST /api/summary/upload
  - POST /api/summary/text
  - POST /api/summary/from-url
  - GET /api/summary/upload-history

- Quiz
  - POST /api/quiz/generate
  - POST /api/quiz/submit
  - GET /api/quiz/{quizId}

- Dashboard
  - GET /api/dashboard/overview
  - GET /api/dashboard/analytics

- Contents
  - GET /api/contents
  - GET /api/contents/{contentId}
  - DELETE /api/contents/{contentId}
  - POST /api/contents/from-url

## 3) Roadmap To Full Product

### Sprint 1 (done in this update)
- Move content-list from mock to live API data.
- Move content-detail from mock to live API data.
- Add content CRUD baseline (list/detail/delete).

### Sprint 2 (next)
- Complete /home/quiz.html as a full first-class quiz workspace:
  - Load quiz by id
  - Resume-in-progress
  - Save draft answers
  - Time tracking and auto-submit option
- Add deep links from content-detail/content-list to quiz generation for selected content.

### Sprint 2 (progress update)
- /home/quiz.html has been upgraded from static demo to live workspace:
  - Load content options from GET /api/contents
  - Generate and reload quiz via POST /api/quiz/generate
  - Submit answers via POST /api/quiz/submit
  - Persist latest result for /home/quiz-result.html
- Deep links are active:
  - content-list -> quiz.html?contentId=...
  - content-detail -> quiz.html?contentId=...

### Sprint 3
- Replace static /home/admin.html with live governance data (usage, failures, moderation flags).
- Add role-based guard and admin-only actions.

### Sprint 4
- Performance and UX hardening:
  - Request caching for list/detail pages
  - Virtualized rendering for long lists
  - Incremental hydration for heavy blocks
  - Bundle pruning and lazy loading for non-critical scripts

### Sprint 5
- Product polish and reliability:
  - End-to-end tests for core funnel (upload -> quiz -> result -> review)
  - Error telemetry + actionable retry UX
  - Accessibility pass (keyboard flow, focus states, contrast)
