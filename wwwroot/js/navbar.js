(function () {
  /*
     LOGO SVG
  */
  const LOGO_SVG = `
    <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
      <defs>
        <linearGradient id="nav-lg" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stop-color="#5aa2ff"/>
          <stop offset="100%" stop-color="#0d6efd"/>
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="9" fill="url(#nav-lg)"/>
      <rect x="1.5" y="1.5" width="29" height="29" rx="7.5"
        fill="none" stroke="rgba(255,255,255,0.2)" stroke-width="1"/>
      <path d="M8 25L16 8L24 25"
        stroke="white" stroke-width="2.4"
        stroke-linecap="round" stroke-linejoin="round" fill="none"/>
      <line x1="11" y1="20" x2="21" y2="20"
        stroke="white" stroke-width="2.2" stroke-linecap="round"/>
      <circle cx="24.5" cy="24.5" r="3.5" fill="#3ef5c8"/>
    </svg>`;

  /*
     DATA
  */
  const scriptEl = document.currentScript;
  const page = scriptEl?.dataset.page || "home";
  const token = localStorage.getItem("token");
  const role = localStorage.getItem("role");
  const name = localStorage.getItem("name") || "Người dùng";
  const avatar = localStorage.getItem("avatar") || "";
  const notifCount = parseInt(localStorage.getItem("notifCount") || "0");
  const isLogged = !!(token && (role === "user" || role === "admin"));

  const initial = name.trim() ? name.trim()[0].toUpperCase() : "U";
  const shortName = name.split(" ").slice(-1)[0] || name;

  /* Helper: render ảnh hoặc chữ cái đại diện */
  const picHTML = (cls, size) => {
    const st = `width:${size}px;height:${size}px;font-size:${Math.round(size * 0.42)}px`;
    return avatar
      ? `<div class="${cls}" style="${st}"><img src="${avatar}" alt="av"></div>`
      : `<div class="${cls}" style="${st}">${initial}</div>`;
  };

  /*
     NAV LINKS (chỉ dùng khi tạo navbar mới)
  */
  const LINKS = [
    { href: "index.html", label: "Trang chủ", key: "home" },
    { href: "guide.html", label: "Hướng dẫn", key: "guide" },
    { href: "pricing.html", label: "Gói sử dụng", key: "pricing" },
  ];

  const linksHTML = LINKS.map(
    (l) =>
      `<li><a href="${l.href}"${page === l.key ? ' class="active"' : ""}>${l.label}</a></li>`,
  ).join("");

  /*
     ACTIONS HTML
  */
  let actionsHTML = "";

  if (isLogged) {
    /* ── Dropdown ── */
    const dropHTML = `
      <div class="nb-dd" id="nbDd">

        <!-- Header: avatar lớn + tên + role -->
        <div class="dd-head">
          ${picHTML("dd-head-pic", 44)}
          <div style="overflow:hidden">
            <div class="dd-head-name">${name}</div>
            <div class="dd-head-role ${role === "admin" ? "dd-role-admin" : "dd-role-user"}">
              ${
                role === "admin"
                  ? `<svg width="10" height="10" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="2" d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg> Admin`
                  : `<svg width="10" height="10" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="2" d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4" stroke="currentColor" stroke-width="2"/></svg> User`
              }
            </div>
          </div>
        </div>

        <!-- Menu -->
        <div class="dd-body">

          <!-- Hồ sơ cá nhân -->
          <a class="dd-item${page === "profile" ? " active-page" : ""}" href="profile.html">
            <span class="dd-icon dd-icon-blue">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <path stroke="#4d9fff" stroke-width="2" d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                <circle cx="12" cy="7" r="4" stroke="#4d9fff" stroke-width="2"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Hồ sơ cá nhân</div>
              <div class="dd-item-sub">Thông tin, mật khẩu, cài đặt</div>
            </div>
          </a>

          <!-- Thông báo -->
          <a class="dd-item${page === "notifications" ? " active-page" : ""}" href="notifications.html">
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

          <!-- Lịch sử -->
          <a class="dd-item${page === "history" ? " active-page" : ""}" href="history.html">
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

        <!-- Đăng xuất -->
        <div class="dd-footer">
          <button class="dd-item dd-item-logout" id="nbLogout">
            <span class="dd-icon dd-icon-red">
              <svg width="17" height="17" fill="none" viewBox="0 0 24 24">
                <path stroke="#f87171" stroke-width="2" stroke-linecap="round" d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
                <polyline stroke="#f87171" stroke-width="2" stroke-linecap="round" points="16 17 21 12 16 7"/>
                <line stroke="#f87171" stroke-width="2" stroke-linecap="round" x1="21" y1="12" x2="9" y2="12"/>
              </svg>
            </span>
            <div class="dd-item-text">
              <div>Đăng xuất</div>
              <div class="dd-item-sub">Thoát khỏi tài khoản</div>
            </div>
          </button>
        </div>

      </div>`;

    actionsHTML = `
      <div class="nb-av-wrap">
        <button class="nb-av-btn" id="nbAvBtn" aria-haspopup="true" aria-expanded="false">
          ${picHTML("nb-av-pic", 30)}
          <span class="nb-av-name">${shortName}</span>
          <svg class="nb-av-chevron" fill="none" viewBox="0 0 24 24">
            <path stroke="currentColor" stroke-width="2.5" stroke-linecap="round" d="M6 9l6 6 6-6"/>
          </svg>
        </button>
        <span class="nb-notif-dot${notifCount > 0 ? " show" : ""}" id="nbNotifDot"></span>
        ${dropHTML}
      </div>`;
  } else {
    /* Guest */
    actionsHTML = `
      <a class="nb-btn nb-ghost" href="login.html">Đăng nhập</a>
      <a class="nb-btn nb-primary" href="register.html">Đăng ký miễn phí</a>`;
  }

  /*
     RENDER
     Có <nav id="navbar"> sẵn → chỉ điền nb-actions
     Chưa có                 → tạo navbar mới hoàn chỉnh
  */
  const existingNav = document.getElementById("navbar");

  if (existingNav) {
    /* Tìm .nb-actions trong nav có sẵn */
    const nbActions = existingNav.querySelector(".nb-actions");
    if (nbActions) nbActions.innerHTML = actionsHTML;
  } else {
    /* Tạo navbar mới hoàn chỉnh */
    document.body.insertAdjacentHTML(
      "afterbegin",
      `
      <nav id="navbar">
        <div class="nb-inner">
          <a class="nb-brand" href="index.html">${LOGO_SVG} AI Study</a>
          <ul class="nb-links">${linksHTML}</ul>
          <div class="nb-actions">${actionsHTML}</div>
        </div>
      </nav>`,
    );
  }

  /* ── Scroll effect ── */
  const navbar = document.getElementById("navbar");
  window.addEventListener(
    "scroll",
    () => navbar.classList.toggle("scrolled", scrollY > 40),
    { passive: true },
  );

  /*
     DROPDOWN LOGIC
  */
  if (isLogged) {
    const btn = document.getElementById("nbAvBtn");
    const dd = document.getElementById("nbDd");

    if (!btn || !dd) return;

    const open = () => {
      btn.classList.add("active");
      dd.classList.add("open");
      btn.setAttribute("aria-expanded", "true");
    };
    const close = () => {
      btn.classList.remove("active");
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

    document.getElementById("nbLogout")?.addEventListener("click", () => {
      ["token", "role", "name", "avatar", "notifCount"].forEach((k) =>
        localStorage.removeItem(k),
      );
      window.location.href = "index.html";
    });
  }
})();
