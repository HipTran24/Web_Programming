(function () {
  const SIDEBAR_STYLE_ID = "global-nav-sidebar-style";
  const SIDEBAR_ID = "globalAppSidebar";
  const BACKDROP_ID = "globalAppSidebarBackdrop";
  const TOGGLE_ID = "globalSidebarToggle";

  function ensureStyle() {
    if (document.getElementById(SIDEBAR_STYLE_ID)) {
      return;
    }

    const style = document.createElement("style");
    style.id = SIDEBAR_STYLE_ID;
    style.textContent = `
      .global-sidebar-toggle-btn {
        width: 40px;
        height: 40px;
        border: 1px solid rgba(255, 255, 255, 0.22);
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.04);
        display: inline-flex;
        flex-direction: column;
        justify-content: center;
        gap: 5px;
        padding: 0 10px;
        margin-right: 10px;
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
        top: 86px;
        left: 16px;
        width: min(86vw, 280px);
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
        gap: 12px;
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
        font-size: 0.95rem;
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
    `;

    document.head.appendChild(style);
  }

  function getActiveSection(pathname) {
    if (pathname.includes("/dashboard")) {
      return "dashboard";
    }

    if (pathname.includes("/content-list") || pathname.includes("/content-detail")) {
      return "content";
    }

    if (pathname.includes("/quiz")) {
      return "questions";
    }

    if (pathname.includes("/upload")) {
      return "upload";
    }

    if (pathname.includes("/history")) {
      return "history";
    }

    if (pathname.includes("/analytics") || pathname.includes("/admin")) {
      return "analytics";
    }

    if (pathname.includes("/profile") || pathname.includes("/user")) {
      return "profile";
    }

    return "";
  }

  function setActiveLink(sidebar) {
    const pathname = (window.location.pathname || "").toLowerCase();
    const links = sidebar.querySelectorAll("a[data-section]");

    links.forEach((link) => link.classList.remove("active"));

    const section = getActiveSection(pathname);

    if (!section) {
      return;
    }

    const active = sidebar.querySelector(`a[data-section="${section}"]`);
    if (active) {
      active.classList.add("active");
    }
  }

  function getStoredCurrentUser() {
    if (window.AuthClient && typeof window.AuthClient.getCurrentUser === "function") {
      return window.AuthClient.getCurrentUser();
    }

    const raw =
      window.localStorage.getItem("auth.currentUser") ||
      window.sessionStorage.getItem("auth.currentUser");

    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
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

  function hydrateSidebarUser(sidebar) {
    const me = getStoredCurrentUser();
    if (!me) {
      return;
    }

    const displayName = me.fullName || me.username || me.email || "User";
    const avatar = getInitials(me.fullName || me.username, me.email);

    sidebar.querySelectorAll("[data-auth-name]").forEach((element) => {
      element.textContent = displayName;
    });

    sidebar.querySelectorAll("[data-auth-avatar]").forEach((element) => {
      element.textContent = avatar;
    });
  }

  function initGlobalSidebar() {
    if (window.__aiStudySidebarMounted) {
      return;
    }

    if (document.getElementById("appSidebar") || document.getElementById(SIDEBAR_ID)) {
      return;
    }

    const nav = document.querySelector("nav.navbar, nav#navbar, nav");
    if (!nav) {
      return;
    }

    ensureStyle();

    const navContainer = nav.querySelector(".container, .nav-container") || nav.firstElementChild || nav;
    if (!navContainer) {
      return;
    }

    if (document.getElementById(TOGGLE_ID)) {
      return;
    }

    const toggle = document.createElement("button");
    toggle.id = TOGGLE_ID;
    toggle.type = "button";
    toggle.className = "global-sidebar-toggle-btn";
    toggle.setAttribute("aria-label", "Toggle sidebar");
    toggle.setAttribute("aria-controls", SIDEBAR_ID);
    toggle.setAttribute("aria-expanded", "false");
    toggle.innerHTML = "<span></span><span></span><span></span>";

    const brand = navContainer.querySelector(".navbar-brand, .logo, .brand, a[href*='dashboard'], a[href*='index']");
    if (brand && brand.parentElement === navContainer) {
      navContainer.insertBefore(toggle, brand);
    } else {
      navContainer.insertBefore(toggle, navContainer.firstChild);
    }

    const sidebar = document.createElement("aside");
    sidebar.id = SIDEBAR_ID;
    sidebar.className = "global-app-sidebar";
    sidebar.setAttribute("aria-hidden", "true");
    sidebar.innerHTML = `
      <div class="global-app-sidebar-avatar-wrap">
        <div class="global-app-sidebar-avatar" data-auth-avatar>US</div>
        <div class="global-app-sidebar-avatar-name" data-auth-name>Ng&#432;&#7901;i d&ugrave;ng</div>
      </div>
      <nav class="global-app-sidebar-menu">
        <div class="global-app-sidebar-group">
          <div class="global-app-sidebar-heading">H&#7885;c t&#7853;p</div>
          <a href="/home/dashboard.html" data-section="dashboard">Dashboard</a>
          <a href="/home/content-list.html" data-section="content">N&#7897;i dung</a>
          <a href="/home/quiz.html" data-section="questions">C&acirc;u h&#7887;i</a>
        </div>
        <div class="global-app-sidebar-group">
          <div class="global-app-sidebar-heading">Qu&#7843;n l&#253;</div>
          <a href="/home/upload.html" data-section="upload">Upload</a>
          <a href="/home/history.html" data-section="history">L&#7883;ch s&#7917;</a>
          <a href="/home/analytics.html" data-section="analytics">Ph&acirc;n t&iacute;ch</a>
        </div>
        <div class="global-app-sidebar-group">
          <div class="global-app-sidebar-heading">T&agrave;i kho&#7843;n</div>
          <a href="/home/profile.html" data-section="profile">H&#7891; s&#417;</a>
        </div>
      </nav>
    `;

    const backdrop = document.createElement("div");
    backdrop.id = BACKDROP_ID;
    backdrop.className = "global-app-sidebar-backdrop";

    document.body.appendChild(sidebar);
    document.body.appendChild(backdrop);

    setActiveLink(sidebar);
    hydrateSidebarUser(sidebar);

    const setSidebarState = (open) => {
      sidebar.classList.toggle("is-open", open);
      backdrop.classList.toggle("is-visible", open);
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
      sidebar.setAttribute("aria-hidden", open ? "false" : "true");
    };

    toggle.addEventListener("click", () => {
      setSidebarState(!sidebar.classList.contains("is-open"));
    });

    backdrop.addEventListener("click", () => {
      setSidebarState(false);
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        setSidebarState(false);
      }
    });

    sidebar.querySelectorAll("a").forEach((link) => {
      link.addEventListener("click", () => setSidebarState(false));
    });

    window.__aiStudySidebarMounted = true;
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initGlobalSidebar, { once: true });
  } else {
    initGlobalSidebar();
  }
})();
