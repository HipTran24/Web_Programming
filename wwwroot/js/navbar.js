(function () {
  const CSS_ID = "ai-navbar-css";
  const DROPDOWN_ID = "aiNavDropdown";
  const AVATAR_BTN_ID = "aiNavAvatarBtn";
  const NAV_LINKS = [
    { section: "home", href: "/home/index.html", label: "Trang chủ" },
    { section: "guide", href: "/home/guide.html", label: "Hướng dẫn" },
    { section: "pricing", href: "/home/pricing.html", label: "Gói sử dụng" },
  ];

  function ensureCss() {
    if (document.getElementById(CSS_ID)) return;

    const scriptSrc = (document.currentScript || {}).src || "";
    const cssHref = scriptSrc
      ? scriptSrc.replace(/navbar\.js(\?.*)?$/, "navbar.css")
      : "../css/navbar.css";

    const link = document.createElement("link");
    link.id = CSS_ID;
    link.rel = "stylesheet";
    link.href = cssHref;
    document.head.appendChild(link);
  }

  function getActiveSection(pathname) {
    const p = (pathname || "").toLowerCase();
    if (!p || p === "/" || p.includes("/index")) return "home";
    if (p.includes("/guide")) return "guide";
    if (p.includes("/pricing")) return "pricing";
    if (p.includes("/profile") || p.includes("/user")) return "profile";
    if (p.includes("/history")) return "history";
    if (p.includes("/notifications")) return "notifications";
    if (p.includes("/progress") || p.includes("/analytics")) return "progress";
    if (p.includes("/dashboard")) return "dashboard";
    if (p.includes("/upload")) return "upload";
    if (p.includes("/quiz")) return "quiz";
    if (p.includes("/admin")) return "admin";
    if (p.includes("/login") || p.includes("/register") || p.includes("/otp"))
      return "";
    return "home";
  }

  function getCurrentUser() {
    const token = localStorage.getItem("token");
    const role = localStorage.getItem("role");
    const name = localStorage.getItem("name");
    const avatar = localStorage.getItem("avatar");
    const notifCount = parseInt(localStorage.getItem("notifCount") || "0");
    const progress = parseInt(localStorage.getItem("progress") || "0"); // 0–100

    if (!token || (role !== "user" && role !== "admin")) return null;

    return {
      token,
      role,
      name: name || "Người dùng",
      avatar: avatar || "",
      notifCount,
      progress,
    };
  }

  function clearSession() {
    ["token", "role", "name", "avatar", "notifCount", "progress"].forEach(
      (k) => {
        localStorage.removeItem(k);
        sessionStorage.removeItem(k);
      },
    );
  }
  function getInitials(name) {
    const parts = (name || "").trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2)
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return (name || "U").slice(0, 1).toUpperCase();
  }

  function picHTML(cls, size, avatar, initial) {
    const st = `width:${size}px;height:${size}px;font-size:${Math.round(size * 0.42)}px`;
    return avatar
      ? `<div class="${cls}" style="${st}"><img src="${avatar}" alt="av"></div>`
      : `<div class="${cls}" style="${st}">${initial}</div>`;
  }

  const LOGO_SVG = `
    <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
      <defs>
        <linearGradient id="ai-nav-lg" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stop-color="#5aa2ff"/>
          <stop offset="100%" stop-color="#0d6efd"/>
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="9" fill="url(#ai-nav-lg)"/>
      <rect x="1.5" y="1.5" width="29" height="29" rx="7.5"
        fill="none" stroke="rgba(255,255,255,0.2)" stroke-width="1"/>
      <path d="M8 25L16 8L24 25"
        stroke="white" stroke-width="2.4"
        stroke-linecap="round" stroke-linejoin="round" fill="none"/>
      <line x1="11" y1="20" x2="21" y2="20"
        stroke="white" stroke-width="2.2" stroke-linecap="round"/>
      <circle cx="24.5" cy="24.5" r="3.5" fill="#3ef5c8"/>
    </svg>`;

  function buildNavLinks(section) {
    return NAV_LINKS.map(
      (l) =>
        `<li><a href="${l.href}" data-section="${l.section}"${section === l.section ? ' class="active"' : ""}>${l.label}</a></li>`,
    ).join("");
  }

  function buildActions(user, section) {
    /* Guest */
    if (!user) {
      return `
        <div data-auth-guest style="display:flex;gap:10px;align-items:center">
          <a class="nb-btn nb-ghost"   href="/home/login.html">Đăng nhập</a>
          <a class="nb-btn nb-primary" href="/home/register.html">Đăng ký miễn phí</a>
        </div>`;
    }

    /* Logged in */
    const { name, role, avatar, notifCount, progress } = user;
    const initial = getInitials(name);
    const shortName = name.split(" ").slice(-1)[0] || name;

    const dropHTML = `
      <div class="nb-dd" id="${DROPDOWN_ID}">
 
        <!-- ── HEADER: avatar + tên + role + tiến độ nhỏ ── -->
        <div class="dd-head">
          ${picHTML("dd-head-pic", 44, avatar, initial)}
          <div style="overflow:hidden;flex:1">
            <div class="dd-head-name">${name}</div>
            <div class="dd-head-role ${role === "admin" ? "dd-role-admin" : "dd-role-user"}">
              ${
                role === "admin"
                  ? `<svg width="10" height="10" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="2" d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg> Admin`
                  : `<svg width="10" height="10" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="2" d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4" stroke="currentColor" stroke-width="2"/></svg> User`
              }
            </div>
            <div class="dd-head-progress">
              <div class="dd-progress-label">
                <span>Tiến độ</span>
                <span>${progress}%</span>
              </div>
              <div class="dd-progress-track">
                <div class="dd-progress-fill" style="width:${progress}%"></div>
              </div>
            </div>
          </div>
        </div>
 
        <!-- ── MENU ── -->
        <div class="dd-body">
 
          <!-- Hồ sơ người dùng -->
          <a class="dd-item${section === "profile" ? " active-page" : ""}" href="/home/profile.html">
            <span class="dd-icon dd-icon-blue">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <path stroke="#4d9fff" stroke-width="2" d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                <circle cx="12" cy="7" r="4" stroke="#4d9fff" stroke-width="2"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Hồ sơ người dùng</div>
              <div class="dd-item-sub">Thông tin, mật khẩu, cài đặt</div>
            </div>
          </a>
 
          <!-- Thông báo hệ thống -->
          <a class="dd-item${section === "notifications" ? " active-page" : ""}" href="/home/notifications.html">
            <span class="dd-icon dd-icon-teal">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <path stroke="#3ef5c8" stroke-width="2" d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                <path stroke="#3ef5c8" stroke-width="2" d="M13.73 21a2 2 0 0 1-3.46 0"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Thông báo hệ thống</div>
              <div class="dd-item-sub">Cập nhật &amp; nhắc nhở</div>
            </div>
            ${notifCount > 0 ? `<span class="dd-count">${notifCount}</span>` : ""}
          </a>
 
          <!-- Tiến độ học tập -->
          <a class="dd-item${section === "progress" ? " active-page" : ""}" href="/home/analytics.html">
            <span class="dd-icon dd-icon-green">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <path stroke="#4ade80" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
                  d="M3 3v18h18"/>
                <path stroke="#4ade80" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
                  d="M7 16l4-4 4 4 4-6"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Tiến độ học tập</div>
              <div class="dd-item-sub">Thống kê, phân tích kết quả</div>
            </div>
            <!-- Badge % tiến độ -->
            <span style="
              font-size:0.7rem;font-weight:700;padding:2px 7px;
              border-radius:100px;background:rgba(74,222,128,0.14);
              border:1px solid rgba(74,222,128,0.28);color:#4ade80;
              white-space:nowrap;flex-shrink:0
            ">${progress}%</span>
          </a>
 
          <!-- Lịch sử sử dụng -->
          <a class="dd-item${section === "history" ? " active-page" : ""}" href="/home/history.html">
            <span class="dd-icon dd-icon-orange">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <circle cx="12" cy="12" r="10" stroke="#ffb060" stroke-width="2"/>
                <path stroke="#ffb060" stroke-width="2" stroke-linecap="round" d="M12 6v6l4 2"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Lịch sử sử dụng</div>
              <div class="dd-item-sub">Bài đã làm, kết quả lưu</div>
            </div>
          </a>
 
        </div>
 
        <!-- ── ĐĂNG XUẤT ── -->
        <div class="dd-footer">
          <button class="dd-item dd-logout" id="aiNavLogout">
            <span class="dd-icon dd-icon-red">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <path stroke="#f87171" stroke-width="2" stroke-linecap="round"
                  d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
                <polyline stroke="#f87171" stroke-width="2" stroke-linecap="round"
                  points="16 17 21 12 16 7"/>
                <line stroke="#f87171" stroke-width="2" stroke-linecap="round"
                  x1="21" y1="12" x2="9" y2="12"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Đăng xuất</div>
              <div class="dd-item-sub">Thoát khỏi tài khoản</div>
            </div>
          </button>
        </div>
 
      </div>`;

    return `
      <div class="nb-av-wrap" data-auth-user>
        <button class="nb-av-btn" id="${AVATAR_BTN_ID}"
          aria-haspopup="true" aria-expanded="false">
          ${picHTML("nb-av-pic", 30, avatar, initial)}
          <span class="nb-av-name">${shortName}</span>
          <svg class="nb-av-chevron" fill="none" viewBox="0 0 24 24">
            <path stroke="currentColor" stroke-width="2.5"
              stroke-linecap="round" d="M6 9l6 6 6-6"/>
          </svg>
        </button>
        <span class="nb-notif-dot${notifCount > 0 ? " show" : ""}"></span>
        ${dropHTML}
      </div>`;
  }

  function mountNav(section, user) {
    let nav =
      document.getElementById("navbar") || document.getElementById("ai-navbar");

    if (nav) {
      const actions = nav.querySelector(".nb-actions");
      if (actions) actions.innerHTML = buildActions(user, section);
      nav.id = "ai-navbar";
    } else {
      nav = document.createElement("nav");
      nav.id = "ai-navbar";
      nav.innerHTML = `
        <div class="nb-inner">
          <a class="nb-brand" href="/home/index.html">${LOGO_SVG} AI Study</a>
          <ul class="nb-links">${buildNavLinks(section)}</ul>
          <div class="nb-actions">${buildActions(user, section)}</div>
        </div>`;
      document.body.prepend(nav);
    }

    return nav;
  }

  function markActiveLinks(nav, section) {
    nav.querySelectorAll("a[data-section]").forEach((a) => {
      a.classList.toggle("active", a.dataset.section === section);
    });
  }

  function bindDropdown() {
    const btn = document.getElementById(AVATAR_BTN_ID);
    const dd = document.getElementById(DROPDOWN_ID);
    if (!btn || !dd) return;

    const open = () => {
      btn.classList.add("open");
      dd.classList.add("open");
      btn.setAttribute("aria-expanded", "true");
    };
    const close = () => {
      btn.classList.remove("open");
      dd.classList.remove("open");
      btn.setAttribute("aria-expanded", "false");
    };
    const toggle = () => (dd.classList.contains("open") ? close() : open());

    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      toggle();
    });
    document.addEventListener("click", close);
    dd.addEventListener("click", (e) => e.stopPropagation());
    document.addEventListener("keydown", (e) => e.key === "Escape" && close());
  }

  function bindLogout() {
    document.getElementById("aiNavLogout")?.addEventListener("click", (e) => {
      e.preventDefault();
      clearSession();
      window.location.href = "/home/login.html";
    });

    /* Tương thích data-auth-logout cũ */
    document.querySelectorAll("[data-auth-logout]").forEach((el) => {
      if (el.dataset.logoutBound) return;
      el.dataset.logoutBound = "true";
      el.addEventListener("click", (e) => {
        e.preventDefault();
        clearSession();
        window.location.href = "/home/login.html";
      });
    });
  }

  function bindScroll(nav) {
    window.addEventListener(
      "scroll",
      () => nav.classList.toggle("scrolled", scrollY > 40),
      { passive: true },
    );
  }

  function init() {
    ensureCss();

    const section = getActiveSection(window.location.pathname);
    const user = getCurrentUser();
    const nav = mountNav(section, user);

    markActiveLinks(nav, section);
    bindDropdown();
    bindLogout();
    bindScroll(nav);
  }

  /* Chạy sau khi DOM sẵn sàng */
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init, { once: true });
  } else {
    init();
  }
})();
