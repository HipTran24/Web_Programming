(function () {
  const NAV_ID = "app-shell-navbar";
  const SIDEBAR_ID = "app-shell-sidebar";
  const BACKDROP_ID = "app-shell-backdrop";

  const NAV_LINKS = [
    { section: "home", href: "/home/index.html", label: "Trang chủ" },
    { section: "upload", href: "/home/upload.html", label: "Upload" },
    { section: "guide", href: "/home/guide.html", label: "Hướng dẫn" },
    { section: "about", href: "/home/about.html", label: "Giới thiệu" },
  ];

  const SIDEBAR_GROUPS = [
    {
      title: "Học tập",
      links: [
        { section: "dashboard", href: "/home/dashboard.html", label: "Dashboard" },
        { section: "content", href: "/home/content-list.html", label: "Nội dung" },
        { section: "quiz", href: "/home/quiz.html", label: "Câu hỏi" },
      ],
    },
    {
      title: "Quản lý",
      links: [
        { section: "upload", href: "/home/upload.html", label: "Upload" },
        { section: "history", href: "/home/history.html", label: "Lịch sử" },
        { section: "analytics", href: "/home/analytics.html", label: "Phân tích" },
      ],
    },
    {
      title: "Tài khoản",
      links: [
        { section: "profile", href: "/home/profile.html", label: "Hồ sơ" },
      ],
    },
  ];

  function getActiveSection(pathname) {
    const p = (pathname || "").toLowerCase();
    if (!p || p === "/" || p.includes("/index")) return "home";
    if (p.includes("/guide")) return "guide";
    if (p.includes("/about")) return "about";
    if (p.includes("/dashboard")) return "dashboard";
    if (p.includes("/upload")) return "upload";
    if (p.includes("/content-list") || p.includes("/content-detail")) return "content";
    if (p.includes("/quiz")) return "quiz";
    if (p.includes("/history")) return "history";
    if (p.includes("/analytics") || p.includes("/admin")) return "analytics";
    if (p.includes("/profile") || p.includes("/user")) return "profile";
    return "";
  }

  function getInitials(name) {
    const parts = (name || "").trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return (name || "US").slice(0, 2).toUpperCase();
  }

  function getUser() {
    const token =
      localStorage.getItem("auth.accessToken") ||
      sessionStorage.getItem("auth.accessToken") ||
      localStorage.getItem("token") ||
      sessionStorage.getItem("token");

    let currentUser = null;
    const userRaw =
      localStorage.getItem("auth.currentUser") ||
      sessionStorage.getItem("auth.currentUser");
    if (userRaw) {
      try {
        currentUser = JSON.parse(userRaw);
      } catch {
        currentUser = null;
      }
    }

    const name =
      currentUser?.fullName ||
      currentUser?.username ||
      currentUser?.email ||
      localStorage.getItem("name") ||
      sessionStorage.getItem("name") ||
      "Người dùng";
    const role =
      currentUser?.role ||
      localStorage.getItem("role") ||
      sessionStorage.getItem("role");
    const avatarUrl =
      currentUser?.avatarUrl ||
      localStorage.getItem("auth.avatar") ||
      sessionStorage.getItem("auth.avatar") ||
      localStorage.getItem("avatar") ||
      sessionStorage.getItem("avatar") ||
      "";

    if (!token || !role) return null;
    return { token, name, role, avatarUrl };
  }

  function clearAuth() {
    [
      "auth.accessToken",
      "auth.currentUser",
      "token",
      "role",
      "name",
      "avatar",
      "notifCount",
      "progress",
    ].forEach((k) => {
      localStorage.removeItem(k);
      sessionStorage.removeItem(k);
    });
  }

  function renderNav(section, user) {
    const links = NAV_LINKS.map((l) => {
      const active = l.section === section ? " class=\"active\"" : "";
      return `<li><a href="${l.href}"${active}>${l.label}</a></li>`;
    }).join("");

    const normalizedRole = String(user?.role || "").trim().toLowerCase();
    const hideSearch = !user || normalizedRole === "guest";

    const searchMarkup = hideSearch
      ? ""
      : `<div class="app-shell-search">
          <input type="text" placeholder="Tìm chủ đề, tài liệu, chức năng..." />
          <svg width="18" height="18" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="2" d="M21 21l-4.35-4.35M10.5 18a7.5 7.5 0 1 1 0-15 7.5 7.5 0 0 1 0 15z"/></svg>
        </div>`;

    const avatarMarkup = user?.avatarUrl
      ? `<img class="app-shell-avatar-img" src="${user.avatarUrl}" alt="Avatar" />`
      : `<span class="app-shell-avatar">${getInitials(user.name)}</span>`;

    const actions = user
      ? `<div class="app-shell-profile" id="appShellProfile">
          <button
            type="button"
            class="app-shell-profile-toggle"
            id="appShellProfileToggle"
            aria-label="Mở menu người dùng"
            aria-expanded="false"
          >
            ${avatarMarkup}
          </button>
          <div class="app-shell-profile-menu" id="appShellProfileMenu" role="menu" aria-hidden="true">
            <div class="app-shell-profile-heading">
              <span class="app-shell-profile-heading-avatar-wrap">${avatarMarkup}</span>
              <span class="app-shell-profile-heading-name">${user.name}</span>
            </div>
            <a class="app-shell-profile-item" href="/home/profile.html" role="menuitem">Hồ sơ người dùng</a>
            <a class="app-shell-profile-item" href="/home/guide.html#system" role="menuitem">Thông báo hệ thống</a>
            <a class="app-shell-profile-item" href="/home/analytics.html" role="menuitem">Tiến độ</a>
            <a class="app-shell-profile-item" href="/home/history.html" role="menuitem">Lịch sử dùng</a>
            <button type="button" class="app-shell-profile-item logout" id="appShellLogout" role="menuitem">Đăng xuất</button>
          </div>
        </div>`
      : `<a class="app-shell-btn ghost" href="/home/login.html">Đăng nhập</a><a class="app-shell-btn primary" href="/home/register.html">Đăng ký</a>`;

    return `
      <div class="app-shell-inner">
        <a class="app-shell-brand" href="/home/index.html">
          <span class="app-shell-logo-slot" aria-hidden="true"></span>
          <span>SynapLearn</span>
        </a>
        <ul class="app-shell-links">${links}</ul>
        ${searchMarkup}
        <div class="app-shell-actions">${actions}</div>
      </div>
    `;
  }

  function renderSidebar(section) {
    return SIDEBAR_GROUPS.map((g) => {
      const links = g.links
        .map((l) => `<a href="${l.href}"${l.section === section ? " class=\"active\"" : ""}>${l.label}</a>`)
        .join("");
      return `<div class="app-shell-group"><div class="app-shell-group-title">${g.title}</div>${links}</div>`;
    }).join("");
  }

  function hideLegacyNavAndSidebar() {
    document.querySelectorAll("nav.navbar").forEach((el) => {
      el.remove();
    });

    document.querySelectorAll("#navbar").forEach((el) => {
      if (el.id !== NAV_ID) {
        el.remove();
      }
    });

    document.querySelectorAll("#appSidebar, .app-sidebar").forEach((el) => {
      if (el.id !== SIDEBAR_ID && !el.closest(`#${SIDEBAR_ID}`)) {
        el.remove();
      }
    });
  }

  function mountShell() {
    document.body.setAttribute("data-shell-page", "true");
    hideLegacyNavAndSidebar();

    const section = getActiveSection(window.location.pathname);
    const user = getUser();

    let nav = document.getElementById(NAV_ID);
    if (!nav) {
      nav = document.createElement("nav");
      nav.id = NAV_ID;
      document.body.prepend(nav);
    }
    nav.innerHTML = renderNav(section, user);

    let floatingToggle = document.getElementById("appShellToggle");
    if (user) {
      if (!floatingToggle) {
        floatingToggle = document.createElement("button");
        floatingToggle.type = "button";
        floatingToggle.id = "appShellToggle";
        floatingToggle.className = "app-shell-menu-toggle-floating";
        floatingToggle.setAttribute("aria-label", "Mở menu");
        floatingToggle.setAttribute("aria-controls", SIDEBAR_ID);
        floatingToggle.setAttribute("aria-expanded", "false");
        floatingToggle.textContent = "☰";
        document.body.appendChild(floatingToggle);
      }
    } else if (floatingToggle) {
      floatingToggle.remove();
    }

    let sidebar = document.getElementById(SIDEBAR_ID);
    if (!sidebar) {
      sidebar = document.createElement("aside");
      sidebar.id = SIDEBAR_ID;
      document.body.appendChild(sidebar);
    }
    sidebar.innerHTML = renderSidebar(section);

    let backdrop = document.getElementById(BACKDROP_ID);
    if (!backdrop) {
      backdrop = document.createElement("div");
      backdrop.id = BACKDROP_ID;
      document.body.appendChild(backdrop);
    }

    const toggle = document.getElementById("appShellToggle");
    const closeSidebar = function () {
      sidebar.classList.remove("open");
      backdrop.classList.remove("show");
    };

    if (toggle) {
      toggle.addEventListener("click", function () {
        const willOpen = !sidebar.classList.contains("open");
        sidebar.classList.toggle("open", willOpen);
        backdrop.classList.toggle("show", willOpen);
        toggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
      });
    }

    backdrop.addEventListener("click", closeSidebar);

    const profileWrapper = document.getElementById("appShellProfile");
    const profileToggle = document.getElementById("appShellProfileToggle");
    const profileMenu = document.getElementById("appShellProfileMenu");

    const closeProfileMenu = function () {
      if (!profileMenu || !profileToggle) return;
      profileMenu.classList.remove("show");
      profileMenu.setAttribute("aria-hidden", "true");
      profileToggle.setAttribute("aria-expanded", "false");
    };

    if (profileToggle && profileMenu) {
      profileToggle.addEventListener("click", function (e) {
        e.stopPropagation();
        const open = profileMenu.classList.toggle("show");
        profileMenu.setAttribute("aria-hidden", open ? "false" : "true");
        profileToggle.setAttribute("aria-expanded", open ? "true" : "false");
      });

      document.addEventListener("click", function (e) {
        if (!profileWrapper || profileWrapper.contains(e.target)) return;
        closeProfileMenu();
      });
    }

    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape") {
        closeSidebar();
        closeProfileMenu();
      }
    });

    const logoutBtn = document.getElementById("appShellLogout");
    if (logoutBtn) {
      logoutBtn.addEventListener("click", function (e) {
        e.preventDefault();
        closeProfileMenu();
        clearAuth();
        window.location.href = "/home/login.html";
      });
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", mountShell, { once: true });
  } else {
    mountShell();
  }
})();
