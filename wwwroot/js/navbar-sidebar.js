(function () {
  const SHELL_STYLE_ID = "global-nav-shell-style";
  const SIDEBAR_ID = "globalAppSidebar";
  const BACKDROP_ID = "globalAppSidebarBackdrop";
  const TOGGLE_ID = "globalSidebarToggle";

  const TOP_LINKS = [
    { section: "home", href: "/home/index.html", label: "Trang ch&#7911;" },
    { section: "dashboard", href: "/home/dashboard.html", label: "Dashboard" },
    { section: "content", href: "/home/content-list.html", label: "N&#7897;i dung" },
    { section: "quiz", href: "/home/quiz.html", label: "C&acirc;u h&#7887;i" },
    { section: "upload", href: "/home/upload.html", label: "Upload" },
    { section: "history", href: "/home/history.html", label: "L&#7883;ch s&#7917;" },
    { section: "analytics", href: "/home/analytics.html", label: "Ph&acirc;n t&iacute;ch" },
    { section: "profile", href: "/home/profile.html", label: "H&#7891; s&#417;" },
  ];

  const SIDEBAR_GROUPS = [
    {
      heading: "H&#7885;c t&#7853;p",
      links: [
        { section: "dashboard", href: "/home/dashboard.html", label: "Dashboard" },
        { section: "content", href: "/home/content-list.html", label: "N&#7897;i dung" },
        { section: "quiz", href: "/home/quiz.html", label: "C&acirc;u h&#7887;i" },
      ],
    },
    {
      heading: "Qu&#7843;n l&#253;",
      links: [
        { section: "upload", href: "/home/upload.html", label: "Upload" },
        { section: "history", href: "/home/history.html", label: "L&#7883;ch s&#7917;" },
        { section: "analytics", href: "/home/analytics.html", label: "Ph&acirc;n t&iacute;ch" },
      ],
    },
    {
      heading: "T&agrave;i kho&#7843;n",
      links: [{ section: "profile", href: "/home/profile.html", label: "H&#7891; s&#417;" }],
    },
  ];

  function ensureStyle() {
    if (document.getElementById(SHELL_STYLE_ID)) {
      return;
    }

    const style = document.createElement("style");
    style.id = SHELL_STYLE_ID;
    style.textContent = `
      .global-shell-nav {
        position: sticky;
        top: 0;
        z-index: 1046;
        background: rgba(7, 12, 24, 0.88);
        border-bottom: 1px solid rgba(255, 255, 255, 0.13);
        backdrop-filter: blur(10px);
      }

      .global-shell-nav-inner {
        max-width: 1220px;
        margin: 0 auto;
        padding: 12px 16px;
        display: flex;
        align-items: center;
        gap: 10px;
      }

      .global-shell-brand {
        text-decoration: none;
        color: rgba(255, 255, 255, 0.96);
        font-weight: 700;
        letter-spacing: 0.02em;
        margin-right: 4px;
      }

      .global-shell-links {
        display: flex;
        align-items: center;
        gap: 6px;
        flex: 1;
        min-width: 0;
        overflow-x: auto;
        scrollbar-width: none;
      }

      .global-shell-links::-webkit-scrollbar {
        display: none;
      }

      .global-shell-link {
        text-decoration: none;
        color: rgba(255, 255, 255, 0.78);
        font-size: 0.82rem;
        font-weight: 500;
        padding: 7px 10px;
        border-radius: 8px;
        border: 1px solid transparent;
        white-space: nowrap;
      }

      .global-shell-link:hover {
        color: #fff;
        background: rgba(255, 255, 255, 0.08);
      }

      .global-shell-link.active {
        color: #fff;
        background: rgba(13, 110, 253, 0.2);
        border-color: rgba(13, 110, 253, 0.36);
      }

      .global-shell-actions,
      .global-shell-guest,
      .global-shell-user {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .global-shell-actions {
        margin-left: auto;
        flex-shrink: 0;
      }

      .global-shell-btn {
        text-decoration: none;
        color: rgba(255, 255, 255, 0.86);
        border: 1px solid rgba(255, 255, 255, 0.2);
        padding: 6px 11px;
        border-radius: 8px;
        font-size: 0.8rem;
      }

      .global-shell-btn:hover {
        color: #fff;
        border-color: rgba(255, 255, 255, 0.36);
      }

      .global-shell-btn.is-primary {
        background: rgba(13, 110, 253, 0.22);
        border-color: rgba(13, 110, 253, 0.38);
      }

      .global-shell-user-name {
        color: rgba(255, 255, 255, 0.86);
        font-size: 0.82rem;
        white-space: nowrap;
      }

      .global-shell-avatar {
        width: 30px;
        height: 30px;
        border-radius: 50%;
        border: 1px dashed rgba(255, 255, 255, 0.35);
        display: inline-flex;
        align-items: center;
        justify-content: center;
        font-size: 0.7rem;
        color: rgba(255, 255, 255, 0.82);
      }

      .global-sidebar-toggle-btn {
        width: 38px;
        height: 38px;
        border: 1px solid rgba(255, 255, 255, 0.24);
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.04);
        display: inline-flex;
        flex-direction: column;
        justify-content: center;
        gap: 5px;
        padding: 0 10px;
        cursor: pointer;
      }

      .global-sidebar-toggle-btn span {
        display: block;
        width: 100%;
        height: 2px;
        background: rgba(255, 255, 255, 0.92);
      }

      .global-app-sidebar {
        position: fixed;
        top: 80px;
        left: 14px;
        width: min(88vw, 290px);
        padding: 14px;
        font-family: inherit;
        background: rgba(16, 26, 46, 0.96);
        border: 1px solid rgba(255, 255, 255, 0.14);
        border-radius: 14px;
        z-index: 1045;
        transform: translateX(-120%);
        opacity: 0;
        pointer-events: none;
        transition: transform 0.22s ease, opacity 0.22s ease;
        box-shadow: 0 18px 40px rgba(0, 0, 0, 0.35);
      }

      .global-app-sidebar.is-open {
        transform: translateX(0);
        opacity: 1;
        pointer-events: auto;
      }

      .global-app-sidebar-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.36);
        opacity: 0;
        pointer-events: none;
        transition: opacity 0.2s ease;
        z-index: 1040;
      }

      .global-app-sidebar-backdrop.is-visible {
        opacity: 1;
        pointer-events: auto;
      }

      .global-app-sidebar-avatar-wrap {
        display: flex;
        flex-direction: column;
        align-items: center;
        margin: 6px auto 14px;
      }

      .global-app-sidebar-avatar {
        width: 58px;
        height: 58px;
        border-radius: 50%;
        border: 1px dashed rgba(255, 255, 255, 0.32);
        display: inline-flex;
        align-items: center;
        justify-content: center;
        text-align: center;
        font-size: 0.72rem;
        color: rgba(255, 255, 255, 0.72);
      }

      .global-app-sidebar-avatar-name {
        margin-top: 8px;
        font-size: 0.92rem;
        font-weight: 500;
        color: rgba(255, 255, 255, 0.86);
        line-height: 1.25;
      }

      .global-app-sidebar-menu {
        display: flex;
        flex-direction: column;
        gap: 14px;
      }

      .global-app-sidebar-group + .global-app-sidebar-group {
        padding-top: 12px;
        border-top: 1px solid rgba(255, 255, 255, 0.08);
      }

      .global-app-sidebar-heading {
        margin: 0 4px 8px;
        font-size: 0.72rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: rgba(255, 255, 255, 0.46);
      }

      .global-app-sidebar-menu a {
        display: block;
        padding: 10px 12px;
        border-radius: 10px;
        font-size: 0.92rem;
        font-weight: 500;
        line-height: 1.35;
        color: rgba(255, 255, 255, 0.82);
        margin-bottom: 6px;
        text-decoration: none;
        font-family: inherit;
      }

      .global-app-sidebar-menu a:hover {
        background: rgba(255, 255, 255, 0.08);
        color: #fff;
      }

      .global-app-sidebar-menu a.active {
        background: rgba(13, 110, 253, 0.2);
        border: 1px solid rgba(13, 110, 253, 0.35);
        color: #fff;
      }

      @media (max-width: 1200px) {
        .global-shell-nav-inner {
          flex-wrap: wrap;
          row-gap: 8px;
        }

        .global-shell-links {
          order: 3;
          width: 100%;
          padding-top: 2px;
        }
      }

      @media (max-width: 991px) {
        .global-shell-links {
          display: none;
        }

        .global-shell-actions {
          margin-left: auto;
        }
      }

      @media (max-width: 580px) {
        .global-shell-user-name {
          display: none;
        }

        .global-shell-guest .global-shell-btn.is-primary {
          display: none;
        }
      }
    `;

    document.head.appendChild(style);
  }

  function getActiveSection(pathname) {
    const path = (pathname || "").toLowerCase();

    if (!path || path === "/" || path === "/home" || path === "/home/") {
      return "home";
    }

    if (path.includes("/index")) {
      return "home";
    }

    if (path.includes("/dashboard")) {
      return "dashboard";
    }

    if (path.includes("/content-list") || path.includes("/content-detail")) {
      return "content";
    }

    if (path.includes("/quiz")) {
      return "quiz";
    }

    if (path.includes("/upload")) {
      return "upload";
    }

    if (path.includes("/history")) {
      return "history";
    }

    if (path.includes("/analytics") || path.includes("/admin")) {
      return "analytics";
    }

    if (path.includes("/profile") || path.includes("/user")) {
      return "profile";
    }

    if (path.includes("/guide")) {
      return "home";
    }

    if (path.includes("/login") || path.includes("/register") || path.includes("/otp")) {
      return "home";
    }

    return "";
  }

  function getStoredCurrentUser() {
    if (window.AuthClient && typeof window.AuthClient.getCurrentUser === "function") {
      return window.AuthClient.getCurrentUser();
    }

    const raw =
      window.localStorage.getItem("auth.currentUser") ||
      window.sessionStorage.getItem("auth.currentUser") ||
      window.localStorage.getItem("currentUser") ||
      window.sessionStorage.getItem("currentUser");

    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  function hasSessionToken() {
    if (window.AuthClient && typeof window.AuthClient.isAuthenticated === "function") {
      return window.AuthClient.isAuthenticated();
    }

    return Boolean(
      window.localStorage.getItem("auth.accessToken") ||
        window.sessionStorage.getItem("auth.accessToken") ||
        window.localStorage.getItem("token") ||
        window.sessionStorage.getItem("token")
    );
  }

  function isAuthenticated() {
    return hasSessionToken() || Boolean(getStoredCurrentUser());
  }

  function getInitials(name, email) {
    const source = String(name || email || "").trim();
    if (!source) {
      return "US";
    }

    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }

    return source.slice(0, 2).toUpperCase();
  }

  function clearSession() {
    if (window.AuthClient && typeof window.AuthClient.clearSession === "function") {
      window.AuthClient.clearSession();
    }

    const keys = ["auth.accessToken", "auth.currentUser", "token", "currentUser"];
    keys.forEach((key) => {
      window.localStorage.removeItem(key);
      window.sessionStorage.removeItem(key);
    });
  }

  function bindLogoutHandlers(scope) {
    scope.querySelectorAll("[data-auth-logout]").forEach((element) => {
      if (element.dataset.logoutBound === "true") {
        return;
      }

      element.dataset.logoutBound = "true";
      element.addEventListener("click", (event) => {
        event.preventDefault();
        clearSession();
        window.location.href = "/home/login.html";
      });
    });
  }

  function setAuthVisibility(scope, authenticated) {
    scope.querySelectorAll("[data-auth-guest]").forEach((element) => {
      if (authenticated) {
        element.style.setProperty("display", "none", "important");
      } else {
        element.style.removeProperty("display");
      }
    });

    scope.querySelectorAll("[data-auth-user]").forEach((element) => {
      if (authenticated) {
        element.style.removeProperty("display");
      } else {
        element.style.setProperty("display", "none", "important");
      }
    });
  }

  function hydrateUser(scope, user) {
    if (!user) {
      return;
    }

    const displayName = user.fullName || user.username || user.email || "Nguoi dung";
    const avatar = getInitials(user.fullName || user.username, user.email);

    scope.querySelectorAll("[data-auth-name]").forEach((element) => {
      element.textContent = displayName;
    });

    scope.querySelectorAll("[data-auth-avatar]").forEach((element) => {
      element.textContent = avatar;
    });
  }

  function applyAuthUi(scope) {
    const me = getStoredCurrentUser();
    const authenticated = isAuthenticated();

    setAuthVisibility(scope, authenticated);
    hydrateUser(scope, me);
    bindLogoutHandlers(scope);

    if (window.AuthClient && typeof window.AuthClient.applyAuthVisibility === "function") {
      window.AuthClient.applyAuthVisibility();
    }

    if (window.AuthClient && me && typeof window.AuthClient.bindUserUi === "function") {
      window.AuthClient.bindUserUi(me);
    }
  }

  function markActiveLinks(root, section) {
    root.querySelectorAll("a[data-section]").forEach((link) => {
      link.classList.toggle("active", link.dataset.section === section);
    });

    root.querySelectorAll("a[data-nav-section]").forEach((link) => {
      link.classList.toggle("active", link.dataset.navSection === section);
    });
  }

  function buildTopLinksMarkup() {
    return TOP_LINKS.map((link) => {
      return `<a class="global-shell-link" data-nav-section="${link.section}" href="${link.href}">${link.label}</a>`;
    }).join("");
  }

  function buildSidebarMarkup() {
    const groups = SIDEBAR_GROUPS.map((group) => {
      const links = group.links
        .map((link) => `<a href="${link.href}" data-section="${link.section}">${link.label}</a>`)
        .join("");

      return `
        <div class="global-app-sidebar-group">
          <div class="global-app-sidebar-heading">${group.heading}</div>
          ${links}
        </div>
      `;
    }).join("");

    return `
      <div class="global-app-sidebar-avatar-wrap">
        <div class="global-app-sidebar-avatar" data-auth-avatar>US</div>
        <div class="global-app-sidebar-avatar-name" data-auth-name>Nguoi dung</div>
      </div>
      <nav class="global-app-sidebar-menu">
        ${groups}
      </nav>
    `;
  }

  function mountGlobalNav() {
    let nav = document.querySelector('[data-global-shell-nav="mounted"]');

    if (!nav) {
      nav = document.querySelector("nav.navbar, nav#navbar, nav");
    }

    if (!nav) {
      nav = document.querySelector("header");
    }

    if (!nav) {
      nav = document.createElement("nav");
      document.body.prepend(nav);
    }

    const preservedId = nav.id || "";
    nav.className = "global-shell-nav";
    nav.setAttribute("data-global-shell-nav", "mounted");

    if (preservedId) {
      nav.id = preservedId;
    }

    nav.innerHTML = `
      <div class="global-shell-nav-inner">
        <button id="${TOGGLE_ID}" type="button" class="global-sidebar-toggle-btn" aria-label="Toggle sidebar" aria-controls="${SIDEBAR_ID}" aria-expanded="false">
          <span></span><span></span><span></span>
        </button>
        <a class="global-shell-brand" href="/home/index.html">AI Study</a>
        <div class="global-shell-links">${buildTopLinksMarkup()}</div>
        <div class="global-shell-actions">
          <div class="global-shell-guest" data-auth-guest>
            <a class="global-shell-btn" href="/home/login.html">&#272;&#259;ng nh&#7853;p</a>
            <a class="global-shell-btn is-primary" href="/home/register.html">&#272;&#259;ng k&#253;</a>
          </div>
          <div class="global-shell-user" data-auth-user>
            <span class="global-shell-user-name" data-auth-name>Nguoi dung</span>
            <span class="global-shell-avatar" data-auth-avatar>US</span>
            <a class="global-shell-btn" href="/home/login.html" data-auth-logout>&#272;&#259;ng xu&#7845;t</a>
          </div>
        </div>
      </div>
    `;

    return nav;
  }

  function mountSidebar() {
    let sidebar = document.getElementById(SIDEBAR_ID);
    if (!sidebar) {
      sidebar = document.createElement("aside");
      sidebar.id = SIDEBAR_ID;
      sidebar.className = "global-app-sidebar";
      sidebar.setAttribute("aria-hidden", "true");
      document.body.appendChild(sidebar);
    }

    sidebar.innerHTML = buildSidebarMarkup();

    let backdrop = document.getElementById(BACKDROP_ID);
    if (!backdrop) {
      backdrop = document.createElement("div");
      backdrop.id = BACKDROP_ID;
      backdrop.className = "global-app-sidebar-backdrop";
      document.body.appendChild(backdrop);
    }

    return { sidebar, backdrop };
  }

  function closeSidebar(sidebar, backdrop, toggle) {
    sidebar.classList.remove("is-open");
    backdrop.classList.remove("is-visible");
    sidebar.setAttribute("aria-hidden", "true");
    toggle.setAttribute("aria-expanded", "false");
  }

  function openSidebar(sidebar, backdrop, toggle) {
    sidebar.classList.add("is-open");
    backdrop.classList.add("is-visible");
    sidebar.setAttribute("aria-hidden", "false");
    toggle.setAttribute("aria-expanded", "true");
  }

  function bindSidebarInteractions(nav, sidebar, backdrop) {
    const toggle = nav.querySelector(`#${TOGGLE_ID}`);
    if (!toggle) {
      return;
    }

    toggle.addEventListener("click", () => {
      const opening = !sidebar.classList.contains("is-open");
      if (opening) {
        openSidebar(sidebar, backdrop, toggle);
      } else {
        closeSidebar(sidebar, backdrop, toggle);
      }
    });

    backdrop.addEventListener("click", () => {
      closeSidebar(sidebar, backdrop, toggle);
    });

    sidebar.querySelectorAll("a").forEach((link) => {
      link.addEventListener("click", () => closeSidebar(sidebar, backdrop, toggle));
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        closeSidebar(sidebar, backdrop, toggle);
      }
    });
  }

  function hideLegacySidebars() {
    const legacySidebars = document.querySelectorAll(".sidebar, #appSidebar, .app-sidebar");

    legacySidebars.forEach((element) => {
      if (element.id === SIDEBAR_ID || element.classList.contains("global-app-sidebar")) {
        return;
      }

      if (element.closest(`#${SIDEBAR_ID}`)) {
        return;
      }

      element.style.setProperty("display", "none", "important");
      element.setAttribute("data-global-shell-hidden", "true");

      const aside = element.closest("aside");
      if (aside && !aside.closest(`#${SIDEBAR_ID}`)) {
        aside.style.setProperty("display", "none", "important");

        const parent = aside.parentElement;
        if (parent && parent.classList.contains("layout")) {
          parent.style.gridTemplateColumns = "1fr";
        }
      }
    });
  }

  function initGlobalShell() {
    ensureStyle();

    const nav = mountGlobalNav();
    const { sidebar, backdrop } = mountSidebar();

    const activeSection = getActiveSection(window.location.pathname);
    markActiveLinks(nav, activeSection);
    markActiveLinks(sidebar, activeSection);

    applyAuthUi(document);
    hideLegacySidebars();
    bindSidebarInteractions(nav, sidebar, backdrop);

    window.__aiStudySidebarMounted = true;
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initGlobalShell, { once: true });
  } else {
    initGlobalShell();
  }
})();
