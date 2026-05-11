(function () {
  const FALLBACK_FAVICON_PATH = "/images/favicon.svg?v=20260402-tab3";
  const FALLBACK_SHORTCUT_ICON_PATH = "/favicon.ico?v=20260402-tab3";
  const FALLBACK_TOUCH_ICON_PATH = "/images/favicon.svg?v=20260402-tab3";
  const NAV_ID = "app-shell-navbar";
  const SIDEBAR_ID = "app-shell-sidebar";
  const BACKDROP_ID = "app-shell-backdrop";
  const SEARCH_DEBOUNCE_MS = 90;
  const SEARCH_MAX_RESULTS = 7;
  const NOTIFICATION_BADGE_POLL_MS = 10000;
  const NOTIFICATION_FLYOUT_LIMIT = 3;
  let activeSearchController = null;
  let searchHotkeyBound = false;
  let activeProfileOutsideClickHandler = null;
  let activeEscapeKeyHandler = null;
  let activeNotificationPollTimer = 0;
  let activeNotificationVisibilityHandler = null;
  let activeNotificationResizeHandler = null;
  let activeShellStateKey = "";
  let latestUnreadCount = 0;
  let latestNotificationInbox = [];

  const NAV_LINKS = [
    { section: "home", href: "/home/index.html", label: "Trang chủ" },
    { section: "upload", href: "/home/upload.html", label: "Upload" },
    { section: "guide", href: "/home/guide.html", label: "Hướng dẫn" },
    { section: "about", href: "/home/about.html", label: "Giới thiệu" },
  ];

  const NAV_LINKS_ADMIN = [
    { section: "admin", href: "/admin", label: "Bảng điều khiển Admin" },
    { section: "admin-users", href: "/admin#users", label: "Người dùng" },
    { section: "admin-moderation", href: "/admin#moderation", label: "Kiểm duyệt" },
    { section: "admin-ai-system", href: "/admin#aiSystem", label: "AI System" },
    { section: "admin-ai", href: "/admin#aiLogs", label: "Giám sát AI" },
    { section: "admin-audit", href: "/admin#audit", label: "Audit" },
  ];

  const SIDEBAR_GROUPS = [
    {
      title: "Học tập",
      links: [
        { section: "dashboard", href: "/dashboard", label: "Dashboard" },
        { section: "history", href: "/home/history.html", label: "Lịch sử" },
        { section: "quiz", href: "/home/quiz.html", label: "Câu hỏi" },
      ],
    },
    {
      title: "Quản lý",
      links: [
        { section: "upload", href: "/home/upload.html", label: "Upload" },
        { section: "learning-plan", href: "/home/learning-plan.html", label: "Lộ trình" },
        { section: "analytics", href: "/home/analytics.html", label: "Phân tích" },
        { section: "premium", href: "/home/premium-upgrade.html", label: "Premium" },
      ],
    },
    {
      title: "Tài khoản",
      links: [
        { section: "profile", href: "/home/profile.html", label: "Hồ sơ" },
      ],
    },
  ];

  const SIDEBAR_GROUPS_ADMIN = [
    {
      title: "Admin Console",
      links: [
        { section: "admin", href: "/admin", label: "Tổng quan" },
        { section: "admin-users", href: "/admin#users", label: "Người dùng" },
        { section: "admin-moderation", href: "/admin#moderation", label: "Kiểm duyệt" },
        { section: "admin-ai-system", href: "/admin#aiSystem", label: "AI System" },
        { section: "admin-ai", href: "/admin#aiLogs", label: "AI Logs" },
        { section: "admin-audit", href: "/admin#audit", label: "Audit Logs" },
      ],
    },
  ];

  function ensureSiteFavicon() {
    if (typeof window.ensureSiteFavicon === "function") {
      window.ensureSiteFavicon();
      return;
    }

    const iconHref = new URL(FALLBACK_FAVICON_PATH, window.location.origin).href;
    const shortcutIconHref = new URL(FALLBACK_SHORTCUT_ICON_PATH, window.location.origin).href;
    const touchIconHref = new URL(FALLBACK_TOUCH_ICON_PATH, window.location.origin).href;

    const ensureLink = (rel, href, type) => {
      let link = document.head.querySelector(`link[rel="${rel}"]`);
      if (!link) {
        link = document.createElement("link");
        link.setAttribute("rel", rel);
        document.head.appendChild(link);
      }

      link.setAttribute("href", href);
      if (type) {
        link.setAttribute("type", type);
      } else {
        link.removeAttribute("type");
      }
    };

    ensureLink("icon", iconHref, "image/svg+xml");
    ensureLink("shortcut icon", shortcutIconHref, "image/x-icon");
    ensureLink("apple-touch-icon", touchIconHref, "image/svg+xml");
  }

  ensureSiteFavicon();

  function getAuthHeaders() {
    const token = window.AuthClient?.getAccessToken?.() || "";
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  function applyNotificationBadge(unreadCount) {
    latestUnreadCount = Number.isFinite(Number(unreadCount)) ? Math.max(0, Number(unreadCount)) : 0;
    const hasUnread = latestUnreadCount > 0;

    document.getElementById("appShellNotificationDot")?.toggleAttribute("hidden", !hasUnread);
    document.getElementById("appShellNotificationMenuDot")?.toggleAttribute("hidden", !hasUnread);
  }

  async function refreshNotificationBadge() {
    const token = window.AuthClient?.getAccessToken?.();
    if (!token) {
      applyNotificationBadge(0);
      return;
    }

    try {
      const response = await fetch("/api/profile/system-notifications/unread", {
        method: "GET",
        headers: getAuthHeaders(),
        cache: "no-store",
      });

      if (!response.ok) {
        applyNotificationBadge(0);
        return;
      }

      const payload = await response.json().catch(() => null);
      applyNotificationBadge(payload?.unreadCount || 0);
    } catch {
      applyNotificationBadge(0);
    }
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function formatNotificationDateTime(value) {
    const date = new Date(value || "");
    if (Number.isNaN(date.getTime())) {
      return "--";
    }

    return date.toLocaleString("vi-VN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  function getNotificationSeverityLabel(severity) {
    const key = String(severity || "").toLowerCase();
    if (key === "success") return "Thành công";
    if (key === "warning") return "Cảnh báo";
    if (key === "danger") return "Khẩn cấp";
    return "Thông tin";
  }

  function getNotificationCategoryLabel(category) {
    const key = String(category || "").toLowerCase();
    if (key === "moderation") return "Kiểm duyệt";
    if (key === "account") return "Tài khoản";
    if (key === "security") return "Bảo mật";
    if (key === "quiz") return "Quiz";
    return "Hệ thống";
  }

  async function fetchSystemNotificationInbox() {
    const token = window.AuthClient?.getAccessToken?.();
    if (!token) {
      return {
        unreadCount: 0,
        totalItems: 0,
        items: [],
      };
    }

    const response = await fetch("/api/profile/system-notifications", {
      method: "GET",
      headers: getAuthHeaders(),
      cache: "no-store",
    });

    if (!response.ok) {
      throw new Error("Không tải được thông báo hệ thống.");
    }

    const payload = await response.json().catch(() => null);
    return {
      unreadCount: Number(payload?.unreadCount || 0),
      totalItems: Number(payload?.totalItems || 0),
      items: Array.isArray(payload?.items) ? payload.items : [],
    };
  }

  async function markSystemNotificationRead(notificationId, markAll) {
    const token = window.AuthClient?.getAccessToken?.();
    if (!token) {
      return 0;
    }

    const response = await fetch("/api/profile/system-notifications/read", {
      method: "PUT",
      headers: {
        ...getAuthHeaders(),
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        notificationId: notificationId || "",
        markAll: Boolean(markAll),
      }),
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(payload?.message || "Không thể cập nhật trạng thái thông báo.");
    }

    const unreadCount = Number(payload?.unreadCount || 0);
    applyNotificationBadge(unreadCount);
    window.dispatchEvent(new CustomEvent("system-notifications:changed", {
      detail: { unreadCount },
    }));
    return unreadCount;
  }

  function normalizeSearchText(value) {
    return String(value || "")
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/đ/g, "d")
      .replace(/[^a-z0-9\s]/g, " ")
      .replace(/\s+/g, " ")
      .trim();
  }

  function getSearchKeywordMap() {
    return {
      "/home/index.html": ["trang chu", "home", "bat dau"],
      "/home/upload.html": ["upload", "tai len", "tom tat", "xu ly noi dung"],
      "/home/guide.html": ["huong dan", "help", "tro giup"],
      "/home/about.html": ["gioi thieu", "about", "thong tin"],
      "/dashboard": ["dashboard", "tong quan", "tong hop"],
      "/home/history.html": ["lich su", "history", "noi dung da hoc"],
      "/home/quiz.html": ["quiz", "cau hoi", "kiem tra", "trac nghiem"],
      "/home/analytics.html": ["phan tich", "analytics", "thong ke"],
      "/home/learning-plan.html": ["lo trinh", "ke hoach hoc", "learning plan"],
      "/home/premium-upgrade.html": ["premium", "momo", "thanh toan", "nang cap"],
      "/home/profile.html": ["ho so", "tai khoan", "trang ca nhan", "bao mat"],
      "/admin": ["admin", "quan tri"],
    };
  }

  function buildSearchIndex() {
    const keywordMap = getSearchKeywordMap();
    const rawItems = [];

    NAV_LINKS.forEach((item) => {
      rawItems.push({
        href: item.href,
        label: item.label,
        group: "Điều hướng chính",
      });
    });

    SIDEBAR_GROUPS.forEach((group) => {
      group.links.forEach((item) => {
        rawItems.push({
          href: item.href,
          label: item.label,
          group: group.title,
        });
      });
    });

    rawItems.push(
      {
        href: "/home/profile.html",
        label: "Trang cá nhân",
        group: "Tài khoản",
      },
      {
        href: "/home/analytics.html",
        label: "Tiến độ học tập",
        group: "Tài khoản",
      },
      {
        href: "/home/learning-plan.html",
        label: "Lộ trình học",
        group: "Tài khoản",
      }
    );

    const uniqueByHref = new Map();
    rawItems.forEach((item) => {
      if (!uniqueByHref.has(item.href)) {
        uniqueByHref.set(item.href, item);
      }
    });

    return Array.from(uniqueByHref.values()).map((item) => {
      const aliases = keywordMap[item.href] || [];
      const normalizedLabel = normalizeSearchText(item.label);
      const normalizedAliases = aliases.map((alias) => normalizeSearchText(alias)).filter(Boolean);
      return {
        ...item,
        aliases,
        normalizedLabel,
        normalizedAliases,
      };
    });
  }

  function scoreSearchCandidate(candidate, query) {
    if (!query) {
      return 0;
    }

    const label = candidate.normalizedLabel;
    if (label === query) {
      return 1000;
    }

    if (label.startsWith(query)) {
      return 820;
    }

    if (label.includes(query)) {
      return 680;
    }

    for (const alias of candidate.normalizedAliases) {
      if (alias === query) {
        return 760;
      }

      if (alias.startsWith(query)) {
        return 720;
      }

      if (alias.includes(query)) {
        return 620;
      }
    }

    const queryTokens = query.split(" ").filter(Boolean);
    if (queryTokens.length > 1) {
      const matched = queryTokens.every((token) =>
        label.includes(token) || candidate.normalizedAliases.some((alias) => alias.includes(token))
      );

      if (matched) {
        return 560;
      }
    }

    return -1;
  }

  function createSearchUi(searchRoot) {
    const panel = document.createElement("div");
    panel.className = "app-shell-search-results";
    panel.id = "appShellSearchResults";
    panel.hidden = true;
    panel.setAttribute("role", "listbox");
    panel.setAttribute("aria-label", "Kết quả tìm kiếm điều hướng");

    const list = document.createElement("ul");
    list.className = "app-shell-search-results-list";

    panel.appendChild(list);
    searchRoot.appendChild(panel);

    return { panel, list };
  }

  function createSearchController(searchRoot, searchInput, openResult) {
    const index = buildSearchIndex();
    const { panel, list } = createSearchUi(searchRoot);
    const state = {
      results: [],
      selectedIndex: -1,
      debounceTimer: 0,
      isOpen: false,
    };

    const closeResults = () => {
      state.selectedIndex = -1;
      state.isOpen = false;
      panel.hidden = true;
      panel.classList.remove("show");
      list.innerHTML = "";
      searchInput.removeAttribute("aria-expanded");
      searchInput.removeAttribute("aria-activedescendant");
    };

    const updateActiveItem = () => {
      const items = Array.from(list.querySelectorAll("[data-result-index]"));
      items.forEach((item, idx) => {
        const active = idx === state.selectedIndex;
        item.classList.toggle("active", active);
        item.setAttribute("aria-selected", active ? "true" : "false");
        if (active) {
          searchInput.setAttribute("aria-activedescendant", item.id);
        }
      });
    };

    const renderResults = () => {
      if (state.results.length === 0) {
        list.innerHTML = "";
        closeResults();
        return;
      }

      list.innerHTML = state.results
        .map((item, idx) => `
          <li>
            <button
              type="button"
              id="appShellResult-${idx}"
              class="app-shell-search-result"
              role="option"
              aria-selected="false"
              data-result-index="${idx}"
            >
              <span class="app-shell-search-result-label">${item.label}</span>
              <span class="app-shell-search-result-meta">${item.group}</span>
            </button>
          </li>
        `)
        .join("");

      state.isOpen = true;
      panel.hidden = false;
      panel.classList.add("show");
      searchInput.setAttribute("aria-expanded", "true");

      if (state.selectedIndex >= state.results.length) {
        state.selectedIndex = state.results.length - 1;
      }

      if (state.selectedIndex < 0) {
        state.selectedIndex = 0;
      }

      updateActiveItem();
    };

    const runSearch = () => {
      const raw = String(searchInput.value || "").trim();
      const query = normalizeSearchText(raw);
      if (!query) {
        closeResults();
        return;
      }

      const ranked = index
        .map((candidate) => ({
          candidate,
          score: scoreSearchCandidate(candidate, query),
        }))
        .filter((x) => x.score >= 0)
        .sort((a, b) => b.score - a.score || a.candidate.label.localeCompare(b.candidate.label, "vi"))
        .slice(0, SEARCH_MAX_RESULTS)
        .map((x) => x.candidate);

      state.results = ranked;
      state.selectedIndex = ranked.length > 0 ? 0 : -1;
      renderResults();
    };

    const scheduleSearch = () => {
      window.clearTimeout(state.debounceTimer);
      state.debounceTimer = window.setTimeout(runSearch, SEARCH_DEBOUNCE_MS);
    };

    const selectAndOpen = (indexToOpen) => {
      const target = state.results[indexToOpen];
      if (!target) {
        return;
      }

      openResult(target.href);
    };

    list.addEventListener("click", function (event) {
      const button = event.target instanceof Element
        ? event.target.closest("[data-result-index]")
        : null;

      if (!button) {
        return;
      }

      const idx = Number(button.getAttribute("data-result-index") || -1);
      if (idx < 0) {
        return;
      }

      selectAndOpen(idx);
    });

    searchInput.addEventListener("focus", function () {
      if (state.results.length > 0) {
        panel.hidden = false;
        panel.classList.add("show");
        searchInput.setAttribute("aria-expanded", "true");
      }
    });

    searchInput.addEventListener("keydown", function (event) {
      if (!state.isOpen && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
        runSearch();
      }

      if (!state.isOpen) {
        if (event.key === "Enter") {
          runSearch();
          if (state.results.length > 0) {
            event.preventDefault();
            selectAndOpen(0);
          }
        }
        return;
      }

      if (event.key === "ArrowDown") {
        event.preventDefault();
        state.selectedIndex = (state.selectedIndex + 1) % state.results.length;
        updateActiveItem();
        return;
      }

      if (event.key === "ArrowUp") {
        event.preventDefault();
        state.selectedIndex = (state.selectedIndex - 1 + state.results.length) % state.results.length;
        updateActiveItem();
        return;
      }

      if (event.key === "Enter") {
        event.preventDefault();
        const openIndex = state.selectedIndex >= 0 ? state.selectedIndex : 0;
        selectAndOpen(openIndex);
        return;
      }

      if (event.key === "Escape") {
        event.preventDefault();
        closeResults();
      }
    });

    const onDocClick = (event) => {
      if (searchRoot.contains(event.target)) {
        return;
      }

      closeResults();
    };

    document.addEventListener("click", onDocClick);

    return {
      closeResults,
      handleInput: scheduleSearch,
      destroy: () => {
        window.clearTimeout(state.debounceTimer);
        document.removeEventListener("click", onDocClick);
      },
    };
  }

  function getActiveSection(pathname) {
    const p = (pathname || "").toLowerCase();
    if (!p || p === "/" || p.includes("/index")) return "home";
    if (p.includes("/guide")) return "guide";
    if (p.includes("/about")) return "about";
    if (p.includes("/dashboard")) return "dashboard";
    if (p.includes("/upload")) return "upload";
    if (p.includes("/content-list") || p.includes("/history") || p.includes("/content-detail")) return "history";
    if (p.includes("/learning-plan")) return "learning-plan";
    if (p.includes("/premium")) return "premium";
    if (p.includes("/quiz")) return "quiz";
    if (p.includes("/admin")) return "admin";
    if (p.includes("/analytics")) return "analytics";
    if (p.includes("/profile") || p.includes("/user")) return "profile";
    return "";
  }

  function getInitials(name) {
    const parts = (name || "").trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return (name || "US").slice(0, 2).toUpperCase();
  }

  function resolveDisplayName(fullName, email) {
    if (String(fullName || "").trim()) {
      return String(fullName).trim();
    }

    const emailText = String(email || "").trim();
    if (!emailText) {
      return "Người dùng";
    }

    const localPart = emailText.split("@")[0] || "";
    return localPart || emailText;
  }

  function getUser() {
    if (window.AuthClient?.isAuthenticated?.() && window.AuthClient?.getCurrentUser) {
      const currentUser = window.AuthClient.getCurrentUser();
      const token = window.AuthClient.getAccessToken?.() || "";
      if (!currentUser) {
        return null;
      }

      const subscriptionTier = String(currentUser.subscriptionTier || "").trim();
      const isPremium = Boolean(currentUser.isPremium) || subscriptionTier.toLowerCase() === "premium";

      return {
        token,
        name: resolveDisplayName(currentUser.fullName, currentUser.email),
        fullName: String(currentUser.fullName || "").trim(),
        role: currentUser.role || "",
        avatarUrl: currentUser.avatarUrl || "",
        isPremium,
        subscriptionTier: isPremium ? "Premium" : subscriptionTier,
      };
    }

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

    const name = resolveDisplayName(currentUser?.fullName, currentUser?.email);
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
    const subscriptionTier = String(currentUser?.subscriptionTier || "").trim();
    const isPremium = Boolean(currentUser?.isPremium) || subscriptionTier.toLowerCase() === "premium" || String(role || "").trim().toLowerCase() === "premium";

    if (!role && !currentUser) return null;
    return {
      token,
      name,
      fullName: String(currentUser?.fullName || "").trim(),
      role: role || "",
      avatarUrl,
      isPremium,
      subscriptionTier: isPremium ? "Premium" : subscriptionTier,
    };
  }

  function clearAuth() {
    if (window.AuthClient?.logout) {
      void window.AuthClient.logout();
      return;
    }

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

  async function hydrateUserRealName(user) {
    if (!user || String(user.fullName || "").trim()) {
      return user;
    }

    try {
      const headers = {
        Accept: "application/json",
      };
      if (user.token) {
        headers.Authorization = `Bearer ${user.token}`;
      }

      const response = await fetch("/api/auth/me", {
        method: "GET",
        cache: "no-store",
        headers,
      });

      if (!response.ok) {
        return user;
      }

      const payload = await response.json().catch(() => null);
      const fullName = String(payload?.fullName || "").trim();
      if (!fullName) {
        return user;
      }

      const patchStorage = (storage) => {
        const raw = storage.getItem("auth.currentUser");
        if (!raw) {
          return;
        }

        try {
          const parsed = JSON.parse(raw);
          parsed.fullName = fullName;
          storage.setItem("auth.currentUser", JSON.stringify(parsed));
          storage.setItem("name", fullName);
        } catch {
          // Ignore malformed storage payload.
        }
      };

      patchStorage(localStorage);
      patchStorage(sessionStorage);

      return {
        ...user,
        fullName,
        name: fullName,
      };
    } catch {
      return user;
    }
  }

  function renderNav(section, user) {
    const isAdminShell = section === "admin" || document.body.classList.contains("page-admin");
    const isPremium = Boolean(user?.isPremium);
    const navLinks = (isAdminShell ? NAV_LINKS_ADMIN : NAV_LINKS).map((link) => {
      if (link.section !== "premium") {
        return link;
      }

      if (isPremium) {
        return { ...link, href: "/premium/account.html", label: "Premium" };
      }

      return link;
    });
    const links = navLinks.map((link) => {
      const active = link.section === section ? " class=\"active\"" : "";
      return `<li><a href="${link.href}"${active}>${link.label}</a></li>`;
    }).join("");

    const normalizedRole = String(user?.role || "").trim().toLowerCase();
    const hasPrivateShell = Boolean(user) && normalizedRole !== "guest";
    const hideSearch = !hasPrivateShell;
    const menuToggleMarkup = hasPrivateShell
      ? `<button
          type="button"
          class="app-shell-menu-toggle"
          id="appShellToggle"
          aria-label="Mở menu điều hướng"
          aria-controls="${SIDEBAR_ID}"
          aria-expanded="false"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 7h16M4 12h16M4 17h16" />
          </svg>
        </button>`
      : "";
    const searchMarkup = hideSearch
      ? ""
      : `<div class="app-shell-search" role="search">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M21 21l-4.35-4.35M10.5 18a7.5 7.5 0 1 1 0-15 7.5 7.5 0 0 1 0 15z" />
          </svg>
          <input id="appShellSearchInput" type="text" placeholder="Tìm chủ đề, tài liệu, chức năng..." />
          <button
            type="button"
            class="app-shell-search-clear"
            id="appShellSearchClear"
            aria-label="Xóa nội dung tìm kiếm"
            hidden
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" />
            </svg>
          </button>
        </div>`;

    const avatarMarkup = hasPrivateShell
      ? (user?.avatarUrl
          ? `<img class="app-shell-avatar-img" src="${user.avatarUrl}" alt="Avatar" />`
          : `<span class="app-shell-avatar">${getInitials(user?.name || "Người dùng")}</span>`)
      : "";

    const actions = hasPrivateShell
      ? `<div class="app-shell-profile" id="appShellProfile">
          <button
            type="button"
            class="app-shell-profile-toggle"
            id="appShellProfileToggle"
            aria-label="Mở menu người dùng"
            aria-expanded="false"
          >
            ${avatarMarkup}
            <span class="app-shell-notification-dot" id="appShellNotificationDot" hidden></span>
          </button>
          <div class="app-shell-profile-menu" id="appShellProfileMenu" role="menu" aria-hidden="true">
            <div class="app-shell-profile-heading">
              <span class="app-shell-profile-heading-avatar-wrap">${avatarMarkup}</span>
              <span class="app-shell-profile-heading-name">${user.fullName || user.name}</span>
            </div>
            <button type="button" class="app-shell-profile-item" id="appShellProfileOpen" role="menuitem">Hồ sơ người dùng</button>
            <button type="button" class="app-shell-profile-item app-shell-profile-item--notify" id="appShellNotificationButton" role="menuitem" aria-expanded="false"><span class="app-shell-profile-item-label" id="appShellNotificationButtonLabel">Thông báo hệ thống</span><span class="app-shell-item-dot" id="appShellNotificationMenuDot" hidden></span></button>
            <a class="app-shell-profile-item" href="/home/analytics.html" role="menuitem">Tiến độ</a>
            <a class="app-shell-profile-item" href="/home/learning-plan.html" role="menuitem">Lộ trình học</a>
            <button type="button" class="app-shell-profile-item logout" id="appShellLogout" role="menuitem">Đăng xuất</button>
          </div>
          <div class="app-shell-notify-flyout" id="appShellNotificationFlyout" aria-hidden="true">
            <div class="app-shell-notify-flyout-head">
              <div>
                <div class="app-shell-notify-title">Thông báo hệ thống</div>
                <div class="app-shell-notify-meta" id="appShellNotificationMeta">Đang tải thông báo...</div>
              </div>
            </div>
            <div class="app-shell-notify-actions">
              <button type="button" class="app-shell-notify-secondary" id="appShellNotificationMarkAll">Đánh dấu đã đọc</button>
            </div>
            <div class="app-shell-notify-list" id="appShellNotificationList"></div>
          </div>
          <div class="app-shell-notify-modal" id="appShellNotificationModal" hidden>
            <div class="app-shell-notify-modal-backdrop" id="appShellNotificationModalBackdrop"></div>
            <div class="app-shell-notify-modal-card" role="dialog" aria-modal="true" aria-labelledby="appShellNotificationModalTitle">
              <button type="button" class="app-shell-notify-modal-close" id="appShellNotificationModalClose" aria-label="Đóng chi tiết thông báo">×</button>
              <div class="app-shell-notify-modal-kicker">Thông báo hệ thống</div>
              <h3 class="app-shell-notify-modal-title" id="appShellNotificationModalTitle">Chi tiết thông báo</h3>
              <div class="app-shell-notify-modal-meta" id="appShellNotificationModalMeta"></div>
              <div class="app-shell-notify-modal-message" id="appShellNotificationModalMessage"></div>
              <div class="app-shell-notify-modal-actions">
                <button type="button" class="app-shell-notify-secondary" id="appShellNotificationModalDismiss">Đóng</button>
              </div>
            </div>
          </div>

          <div class="app-shell-notify-modal" id="appShellProfileModal" hidden>
            <div class="app-shell-notify-modal-backdrop" id="appShellProfileModalBackdrop"></div>
            <div class="app-shell-notify-modal-card" role="dialog" aria-modal="true" aria-labelledby="appShellProfileModalTitle">
              <button type="button" class="app-shell-notify-modal-close" id="appShellProfileModalClose" aria-label="Đóng hồ sơ">×</button>
              <div class="app-shell-notify-modal-kicker">Tài khoản</div>
              <h3 class="app-shell-notify-modal-title" id="appShellProfileModalTitle">Hồ sơ người dùng</h3>
              <div class="app-shell-notify-modal-meta" id="appShellProfileModalMeta">Đang tải dữ liệu...</div>
              <div class="app-shell-notify-modal-message">
                <form id="appShellProfileForm">
                  <div class="mb-3">
                    <label class="form-label">Họ và tên</label>
                    <input class="form-control" id="appShellProfileFullName" type="text" placeholder="Nhập họ và tên" />
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Email</label>
                    <input class="form-control" id="appShellProfileEmail" type="email" placeholder="name@gmail.com" />
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Số điện thoại</label>
                    <input class="form-control" id="appShellProfilePhone" type="tel" placeholder="+84 ..." />
                  </div>
                  <div class="mb-0">
                    <label class="form-label">Giới thiệu</label>
                    <textarea class="form-control" id="appShellProfileBio" rows="3" placeholder="Viết vài dòng về bạn..."></textarea>
                  </div>
                  <div class="text-danger small mt-2" id="appShellProfileError" style="display:none;"></div>
                </form>
              </div>
              <div class="app-shell-notify-modal-actions">
                <a class="app-shell-notify-secondary" href="/home/profile.html">Mở trang hồ sơ</a>
                <button type="button" class="app-shell-notify-primary" id="appShellProfileSave">Lưu</button>
              </div>
            </div>
          </div>
        </div>`
      : `<div class="app-shell-action-group app-shell-action-group--guest">
          <a class="app-shell-btn ghost" href="/home/login.html">Đăng nhập</a>
          <a class="app-shell-btn primary" href="/home/register.html">Đăng ký</a>
        </div>`;

    return `
      <div class="app-shell-frame">
        <div class="app-shell-inner">
          <div class="app-shell-start">
            ${menuToggleMarkup}
            <a class="app-shell-brand" href="/home/index.html" aria-label="SynapLearn">
              <span class="app-shell-logo-slot" aria-hidden="true">
                <svg viewBox="0 0 24 24" role="presentation" focusable="false">
                  <defs>
                    <linearGradient id="shellBrandGradient" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stop-color="#65d9cb" />
                      <stop offset="100%" stop-color="#f0b45a" />
                    </linearGradient>
                  </defs>
                  <path d="M6 5.5C6 4.12 7.12 3 8.5 3h7C16.88 3 18 4.12 18 5.5v2.34c0 .66-.26 1.3-.73 1.77l-3.5 3.5a2.5 2.5 0 0 1-3.54 0l-3.5-3.5A2.5 2.5 0 0 1 6 7.84V5.5Z" fill="url(#shellBrandGradient)"></path>
                  <path d="M6 18.5C6 19.88 7.12 21 8.5 21h7c1.38 0 2.5-1.12 2.5-2.5v-2.34c0-.66-.26-1.3-.73-1.77l-3.5-3.5a2.5 2.5 0 0 0-3.54 0l-3.5 3.5A2.5 2.5 0 0 0 6 16.16v2.34Z" fill="rgba(101, 217, 203, 0.18)" stroke="rgba(101, 217, 203, 0.65)" stroke-width="1.1"></path>
                  <circle cx="12" cy="12" r="1.7" fill="#edf7f7"></circle>
                </svg>
              </span>
              <span class="app-shell-brand-text">SynapLearn</span>
            </a>
          </div>
          <ul class="app-shell-links">${links}</ul>
          <div class="app-shell-actions">
            ${searchMarkup}
            ${actions}
          </div>
        </div>
      </div>
    `;
  }

  function renderSidebar(user, activeSection) {
    const isPremium = Boolean(user?.isPremium);

    return SIDEBAR_GROUPS.map((group) => {
      const linksMarkup = (group.links || []).map((link) => {
        const isActive = link.section === activeSection;
        const resolvedLink = { ...link };

        if (resolvedLink.section === "premium") {
          if (isPremium) {
            resolvedLink.label = "Xem gói";
            resolvedLink.href = "/premium/account.html";
          } else {
            resolvedLink.label = "Nâng cấp tài khoản";
            resolvedLink.href = "/premium/upgrade.html";
          }
        }

        return `<a href="${resolvedLink.href}"${isActive ? " class=\"active\"" : ""}>${escapeHtml(resolvedLink.label)}</a>`;
      }).join("");

      return `<div class="app-shell-group"><div class="app-shell-group-title">${escapeHtml(group.title)}</div>${linksMarkup}</div>`;
    }).join("");
  }

  function getShellStateKey(pageAccessKind, hasPrivateShell, normalizedRole, user) {
    return JSON.stringify({
      pageAccessKind: String(pageAccessKind || ""),
      hasPrivateShell: Boolean(hasPrivateShell),
      normalizedRole: String(normalizedRole || ""),
      displayName: String(user?.name || ""),
      fullName: String(user?.fullName || ""),
      avatarUrl: String(user?.avatarUrl || ""),
    });
  }

  function closeTransientShellUi() {
    document.getElementById(SIDEBAR_ID)?.classList.remove("open");
    document.getElementById(BACKDROP_ID)?.classList.remove("show");
    document.getElementById("appShellToggle")?.setAttribute("aria-expanded", "false");
    document.getElementById("appShellProfileMenu")?.classList.remove("show");
    document.getElementById("appShellProfileMenu")?.setAttribute("aria-hidden", "true");
    document.getElementById("appShellProfileToggle")?.setAttribute("aria-expanded", "false");
    document.getElementById("appShellNotificationFlyout")?.classList.remove("show");
    document.getElementById("appShellNotificationFlyout")?.setAttribute("aria-hidden", "true");
    document.getElementById("appShellNotificationButton")?.setAttribute("aria-expanded", "false");

    const notificationModal = document.getElementById("appShellNotificationModal");
    if (notificationModal) {
      notificationModal.hidden = true;
    }
  }

  function syncShellActiveLinks(section) {
    const syncLinks = (rootSelector) => {
      document.querySelectorAll(`${rootSelector} a[href]`).forEach((link) => {
        let targetSection = "";
        try {
          targetSection = getActiveSection(new URL(link.getAttribute("href") || "", window.location.origin).pathname);
        } catch {
          targetSection = "";
        }

        link.classList.toggle("active", Boolean(section) && targetSection === section);
      });
    };

    syncLinks(`#${NAV_ID} .app-shell-links`);
    syncLinks(`#${SIDEBAR_ID}`);
  }

  function removeShellArtifacts() {
    document.getElementById(NAV_ID)?.remove();
    document.getElementById(SIDEBAR_ID)?.remove();
    document.getElementById(BACKDROP_ID)?.remove();
    activeShellStateKey = "";
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

  async function mountShell() {
    hideLegacyNavAndSidebar();

    if (document.body.classList.contains("page-login") || document.body.classList.contains("page-register")) {
      document.body.removeAttribute("data-shell-page");
      removeShellArtifacts();
      applyNotificationBadge(0);
      return;
    }

    if (document.body.classList.contains("page-admin")) {
      document.body.removeAttribute("data-shell-page");
      removeShellArtifacts();
      return;
    }

    if (window.AuthClient?.whenReady) {
      try {
        await window.AuthClient.whenReady();
      } catch {
        // AuthClient already handles redirects and cleanup.
      }
    }

    const pageAccess = window.AuthClient?.getPageAccess?.() || { kind: "public", roles: [] };
    if (pageAccess.kind === "auth") {
      document.body.removeAttribute("data-shell-page");
      removeShellArtifacts();
      applyNotificationBadge(0);
      return;
    }

    const section = getActiveSection(window.location.pathname);
    let user = getUser();
    user = await hydrateUserRealName(user);
    const normalizedRole = String(user?.role || "").trim().toLowerCase();
    const hasPrivateShell = Boolean(user) && normalizedRole !== "guest";
    if (pageAccess.kind === "protected" && !hasPrivateShell) {
      document.body.removeAttribute("data-shell-page");
      removeShellArtifacts();
      applyNotificationBadge(0);
      return;
    }

    document.body.setAttribute("data-shell-page", "true");
    const nextShellStateKey = getShellStateKey(pageAccess.kind, hasPrivateShell, normalizedRole, user);
    const canReuseMountedShell = activeShellStateKey === nextShellStateKey && Boolean(document.getElementById(NAV_ID));
    if (canReuseMountedShell) {
      closeTransientShellUi();
      syncShellActiveLinks(section);
      applyNotificationBadge(latestUnreadCount);
      return;
    }

    if (activeSearchController?.destroy) {
      activeSearchController.destroy();
      activeSearchController = null;
    }

    if (activeProfileOutsideClickHandler) {
      document.removeEventListener("click", activeProfileOutsideClickHandler);
      activeProfileOutsideClickHandler = null;
    }

    if (activeEscapeKeyHandler) {
      document.removeEventListener("keydown", activeEscapeKeyHandler);
      activeEscapeKeyHandler = null;
    }

    if (activeNotificationPollTimer) {
      window.clearInterval(activeNotificationPollTimer);
      activeNotificationPollTimer = 0;
    }

    if (activeNotificationVisibilityHandler) {
      document.removeEventListener("visibilitychange", activeNotificationVisibilityHandler);
      activeNotificationVisibilityHandler = null;
    }

    if (activeNotificationResizeHandler) {
      window.removeEventListener("resize", activeNotificationResizeHandler);
      activeNotificationResizeHandler = null;
    }

    let nav = document.getElementById(NAV_ID);
    if (!nav) {
      nav = document.createElement("nav");
      nav.id = NAV_ID;
      document.body.prepend(nav);
    }
    nav.innerHTML = renderNav(section, user);
    applyNotificationBadge(latestUnreadCount);

    let sidebar = document.getElementById(SIDEBAR_ID);
    let backdrop = document.getElementById(BACKDROP_ID);
    if (hasPrivateShell) {
      if (!sidebar) {
        sidebar = document.createElement("aside");
        sidebar.id = SIDEBAR_ID;
        document.body.appendChild(sidebar);
      }
      sidebar.innerHTML = renderSidebar(user, section);

      if (!backdrop) {
        backdrop = document.createElement("div");
        backdrop.id = BACKDROP_ID;
        document.body.appendChild(backdrop);
      }
    } else {
      if (sidebar) {
        sidebar.remove();
      }

      if (backdrop) {
        backdrop.remove();
      }
    }

    const toggle = document.getElementById("appShellToggle");
    const closeSidebar = function () {
      if (!sidebar || !backdrop) {
        return;
      }

      sidebar.classList.remove("open");
      backdrop.classList.remove("show");
      if (toggle) {
        toggle.setAttribute("aria-expanded", "false");
      }
    };

    if (toggle) {
      toggle.addEventListener("click", function () {
        const willOpen = !sidebar.classList.contains("open");
        sidebar.classList.toggle("open", willOpen);
        backdrop.classList.toggle("show", willOpen);
        toggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
      });
    }

    if (backdrop) {
      if (backdrop._appShellClickHandler) {
        backdrop.removeEventListener("click", backdrop._appShellClickHandler);
      }

      backdrop._appShellClickHandler = closeSidebar;
      backdrop.addEventListener("click", closeSidebar);
    }

    const profileWrapper = document.getElementById("appShellProfile");
    const profileToggle = document.getElementById("appShellProfileToggle");
    const profileMenu = document.getElementById("appShellProfileMenu");
    const notificationButton = document.getElementById("appShellNotificationButton");
    const notificationFlyout = document.getElementById("appShellNotificationFlyout");
    const notificationButtonLabel = document.getElementById("appShellNotificationButtonLabel");
    const notificationMeta = document.getElementById("appShellNotificationMeta");
    const notificationList = document.getElementById("appShellNotificationList");
    const notificationMarkAll = document.getElementById("appShellNotificationMarkAll");
    const notificationModal = document.getElementById("appShellNotificationModal");
    const notificationModalBackdrop = document.getElementById("appShellNotificationModalBackdrop");
    const notificationModalClose = document.getElementById("appShellNotificationModalClose");
    const notificationModalDismiss = document.getElementById("appShellNotificationModalDismiss");
    const notificationModalTitle = document.getElementById("appShellNotificationModalTitle");
    const notificationModalMeta = document.getElementById("appShellNotificationModalMeta");
    const notificationModalMessage = document.getElementById("appShellNotificationModalMessage");
    const profileOpen = document.getElementById("appShellProfileOpen");
    const profileModal = document.getElementById("appShellProfileModal");
    const profileModalBackdrop = document.getElementById("appShellProfileModalBackdrop");
    const profileModalClose = document.getElementById("appShellProfileModalClose");
    const profileModalMeta = document.getElementById("appShellProfileModalMeta");
    const profileForm = document.getElementById("appShellProfileForm");
    const profileFullName = document.getElementById("appShellProfileFullName");
    const profileEmail = document.getElementById("appShellProfileEmail");
    const profilePhone = document.getElementById("appShellProfilePhone");
    const profileBio = document.getElementById("appShellProfileBio");
    const profileError = document.getElementById("appShellProfileError");
    const profileSave = document.getElementById("appShellProfileSave");
    const searchRoot = nav.querySelector(".app-shell-search");
    const searchInput = document.getElementById("appShellSearchInput");
    const searchClearButton = document.getElementById("appShellSearchClear");

    const showProfileError = (message) => {
      if (!profileError) return;
      const msg = String(message || "").trim();
      profileError.textContent = msg;
      profileError.style.display = msg ? "block" : "none";
    };

    const openProfileModal = () => {
      if (!profileModal) return;
      profileModal.hidden = false;
      showProfileError("");
      if (profileModalMeta) profileModalMeta.textContent = "Đang tải dữ liệu...";
    };

    const closeProfileModal = () => {
      if (!profileModal) return;
      profileModal.hidden = true;
      showProfileError("");
    };

    const loadProfileIntoModal = async () => {
      if (!profileModal) return;
      try {
        const response = await fetch("/api/profile", {
          method: "GET",
          headers: getAuthHeaders(),
          cache: "no-store",
        });
        const payload = await response.json().catch(() => null);
        if (!response.ok || !payload) {
          throw new Error(payload?.message || "Không tải được hồ sơ tài khoản.");
        }

        if (profileModalMeta) {
          const role = String(payload?.role || "").trim();
          const verified = payload?.isEmailVerified ? "Đã xác thực email" : "Chưa xác thực email";
          profileModalMeta.textContent = [role, verified].filter(Boolean).join(" • ") || "Đã đồng bộ";
        }

        if (profileFullName) profileFullName.value = String(payload?.fullName || "").trim();
        if (profileEmail) profileEmail.value = String(payload?.email || "").trim();
        if (profilePhone) profilePhone.value = String(payload?.phone || "").trim();
        if (profileBio) profileBio.value = String(payload?.bio || "").trim();
      } catch (error) {
        if (profileModalMeta) profileModalMeta.textContent = "Không tải được dữ liệu";
        showProfileError(error instanceof Error ? error.message : "Không tải được hồ sơ.");
      }
    };

    const saveProfileFromModal = async () => {
      if (!profileSave) return;
      profileSave.disabled = true;
      showProfileError("");
      const originalText = profileSave.textContent;
      profileSave.textContent = "Đang lưu...";

      try {
        const response = await fetch("/api/profile", {
          method: "PUT",
          headers: {
            ...getAuthHeaders(),
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            fullName: String(profileFullName?.value || "").trim(),
            email: String(profileEmail?.value || "").trim(),
            phone: String(profilePhone?.value || "").trim(),
            bio: String(profileBio?.value || "").trim(),
            avatarUrl: String(user?.avatarUrl || ""),
          }),
        });
        const payload = await response.json().catch(() => null);
        if (!response.ok) {
          throw new Error(payload?.message || "Không thể cập nhật hồ sơ.");
        }

        if (window.AuthClient?.validateSession) {
          await window.AuthClient.validateSession();
        }

        if (profileModalMeta) profileModalMeta.textContent = "Đã lưu thay đổi";
        closeProfileModal();
      } catch (error) {
        showProfileError(error instanceof Error ? error.message : "Không thể cập nhật hồ sơ.");
      } finally {
        profileSave.disabled = false;
        profileSave.textContent = originalText || "Lưu";
      }
    };

    if (sidebar) {
      sidebar.addEventListener("click", async (event) => {
        const target = event.target instanceof Element
          ? event.target.closest("[data-shell-section='profile']")
          : null;
        if (!target) {
          return;
        }

        event.preventDefault();
        openProfileModal();
        closeSidebar();
        await loadProfileIntoModal();
      });
    }

    if (searchInput && searchClearButton && searchRoot) {
      const openSearchResult = (href) => {
        if (!href) {
          return;
        }

        if (window.AjaxNavigation?.navigate) {
          void window.AjaxNavigation.navigate(href);
          return;
        }

        window.location.assign(href);
      };

      const searchController = createSearchController(searchRoot, searchInput, openSearchResult);
      activeSearchController = searchController;

      const syncSearchClearVisibility = function () {
        searchClearButton.hidden = !String(searchInput.value || "").trim();
      };

      searchInput.setAttribute("autocomplete", "off");
      searchInput.setAttribute("spellcheck", "false");
      searchInput.setAttribute("aria-expanded", "false");

      searchInput.addEventListener("input", function () {
        syncSearchClearVisibility();
        searchController.handleInput();
      });

      searchClearButton.addEventListener("click", function () {
        searchInput.value = "";
        searchInput.dispatchEvent(new Event("input", { bubbles: true }));
        searchController.closeResults();
        searchInput.focus();
      });

      syncSearchClearVisibility();

      if (!searchHotkeyBound) {
        document.addEventListener("keydown", function (event) {
          const input = document.getElementById("appShellSearchInput");
          if (!input) {
            return;
          }

          const isCmdOrCtrl = event.metaKey || event.ctrlKey;
          if (!isCmdOrCtrl || event.key.toLowerCase() !== "k") {
            return;
          }

          event.preventDefault();
          input.focus();
          input.select();
        });

        searchHotkeyBound = true;
      }
    }

    const closeProfileMenu = function () {
      if (!profileMenu || !profileToggle) return;
      profileMenu.classList.remove("show");
      profileMenu.setAttribute("aria-hidden", "true");
      profileToggle.setAttribute("aria-expanded", "false");
      if (notificationFlyout && notificationButton) {
        notificationFlyout.classList.remove("show");
        notificationFlyout.setAttribute("aria-hidden", "true");
        notificationButton.setAttribute("aria-expanded", "false");
      }
    };

    const positionNotificationFlyout = function () {
      if (!notificationFlyout || !profileWrapper || !profileMenu || !notificationButton) {
        return;
      }

      const viewportGutter = window.innerWidth <= 640 ? 12 : 16;
      const desktopViewport = window.innerWidth > 991;
      const menuRect = profileMenu.getBoundingClientRect();
      const buttonRect = notificationButton.getBoundingClientRect();
      const labelRect = notificationButtonLabel?.getBoundingClientRect?.() || buttonRect;
      const availableWidth = Math.max(220, window.innerWidth - (viewportGutter * 2));
      const preferredWidth = desktopViewport
        ? Math.min(336, Math.max(248, window.innerWidth - labelRect.right - viewportGutter - 6))
        : Math.min(336, Math.max(272, availableWidth));
      notificationFlyout.style.width = `${Math.min(preferredWidth, availableWidth)}px`;
      notificationFlyout.style.right = "auto";
      notificationFlyout.style.left = `${viewportGutter}px`;
      notificationFlyout.style.top = `${viewportGutter}px`;
      notificationFlyout.style.visibility = "hidden";

      const flyoutRect = notificationFlyout.getBoundingClientRect();
      const flyoutWidth = Math.min(flyoutRect.width || preferredWidth, window.innerWidth - (viewportGutter * 2));
      const flyoutHeight = flyoutRect.height || 0;
      const preferredLeft = desktopViewport
        ? labelRect.right + 8
        : menuRect.right - flyoutWidth;
      const preferredTop = desktopViewport
        ? labelRect.top - 18
        : menuRect.bottom + 10;
      const maxLeft = Math.max(viewportGutter, window.innerWidth - viewportGutter - flyoutWidth);
      const maxTop = Math.max(viewportGutter, window.innerHeight - viewportGutter - flyoutHeight);
      const flyoutLeft = Math.min(Math.max(preferredLeft, viewportGutter), maxLeft);
      const flyoutTop = Math.min(Math.max(preferredTop, viewportGutter), maxTop);
      const pointerTop = Math.max(
        20,
        Math.min(76, labelRect.top - flyoutTop + (labelRect.height / 2) - 8)
      );

      notificationFlyout.style.top = `${flyoutTop}px`;
      notificationFlyout.style.left = `${flyoutLeft}px`;
      notificationFlyout.style.setProperty("--app-shell-notify-pointer-top", `${pointerTop}px`);
      notificationFlyout.style.visibility = "";
    };

    const openNotificationModal = function (item) {
      if (!notificationModal || !notificationModalTitle || !notificationModalMeta || !notificationModalMessage) {
        return;
      }

      notificationModal.hidden = false;
      notificationModalTitle.textContent = item?.title || "Thông báo hệ thống";
      notificationModalMeta.innerHTML = `
        <span class="app-shell-notify-modal-pill" data-severity="${escapeHtml(item?.severity || "info")}">${escapeHtml(getNotificationSeverityLabel(item?.severity))}</span>
        <span class="app-shell-notify-modal-pill">${escapeHtml(getNotificationCategoryLabel(item?.category))}</span>
        <span>${escapeHtml(formatNotificationDateTime(item?.createdAt))}</span>
      `;
      notificationModalMessage.textContent = item?.message || "";
    };

    const closeNotificationModal = function () {
      if (!notificationModal) {
        return;
      }

      notificationModal.hidden = true;
    };

    const renderNotificationFlyout = function (payload, stateLabel) {
      if (!notificationList || !notificationMeta) {
        return;
      }

      const items = Array.isArray(payload?.items) ? payload.items.slice(0, NOTIFICATION_FLYOUT_LIMIT) : [];
      latestNotificationInbox = Array.isArray(payload?.items) ? payload.items : [];
      notificationMeta.textContent = stateLabel || (
        payload?.unreadCount > 0
          ? `${payload.unreadCount} chưa đọc • ${payload.totalItems || items.length} tổng`
          : `${payload.totalItems || items.length} thông báo`
      );

      if (items.length === 0) {
        notificationList.innerHTML = '<div class="app-shell-notify-empty">Chưa có thông báo hệ thống nào.</div>';
        return;
      }

      notificationList.innerHTML = items.map((item) => `
        <button type="button" class="app-shell-notify-item ${item.isRead ? "" : "is-unread"}" data-notification-id="${escapeHtml(item.notificationId)}">
          <span class="app-shell-notify-item-top">
            <span class="app-shell-notify-item-title">${escapeHtml(item.title || "Thông báo hệ thống")}</span>
            ${item.isRead ? "" : '<span class="app-shell-notify-item-dot"></span>'}
          </span>
          <span class="app-shell-notify-item-message">${escapeHtml(item.message || "")}</span>
          <span class="app-shell-notify-item-meta">${escapeHtml(getNotificationCategoryLabel(item.category))} • ${escapeHtml(formatNotificationDateTime(item.createdAt))}</span>
        </button>
      `).join("");

      Array.from(notificationList.querySelectorAll("[data-notification-id]")).forEach((button) => {
        button.addEventListener("click", async () => {
          const notificationId = button.getAttribute("data-notification-id") || "";
          const item = latestNotificationInbox.find((entry) => entry.notificationId === notificationId);
          if (!item) {
            return;
          }

          if (!item.isRead) {
            try {
              await markSystemNotificationRead(notificationId, false);
              item.isRead = true;
            } catch {
              // Keep modal open even if read-status sync fails.
            }
          }

          openNotificationModal(item);
          closeProfileMenu();
        });
      });
    };

    const loadNotificationFlyout = async function (options = {}) {
      if (!notificationList || !notificationMeta) {
        return;
      }

      if (!options.silent) {
        notificationMeta.textContent = "Đang tải thông báo...";
        notificationList.innerHTML = '<div class="app-shell-notify-empty">Đang đồng bộ danh sách thông báo...</div>';
      }

      try {
        const payload = await fetchSystemNotificationInbox();
        applyNotificationBadge(payload.unreadCount || 0);
        renderNotificationFlyout(payload);
        positionNotificationFlyout();
      } catch (error) {
        notificationMeta.textContent = "Không tải được thông báo";
        notificationList.innerHTML = `<div class="app-shell-notify-empty">${escapeHtml(error instanceof Error ? error.message : "Không tải được thông báo hệ thống.")}</div>`;
        positionNotificationFlyout();
      }
    };

    if (profileToggle && profileMenu) {
      profileToggle.addEventListener("click", function (e) {
        e.stopPropagation();
        const open = profileMenu.classList.toggle("show");
        profileMenu.setAttribute("aria-hidden", open ? "false" : "true");
        profileToggle.setAttribute("aria-expanded", open ? "true" : "false");
      });

      activeProfileOutsideClickHandler = function (e) {
        if (!profileWrapper || profileWrapper.contains(e.target)) return;
        closeProfileMenu();
      };

      document.addEventListener("click", activeProfileOutsideClickHandler);
    }

    if (notificationButton && notificationFlyout) {
      notificationButton.addEventListener("click", async function (event) {
        event.preventDefault();
        event.stopPropagation();

        const willOpen = !notificationFlyout.classList.contains("show");
        notificationFlyout.classList.toggle("show", willOpen);
        notificationFlyout.setAttribute("aria-hidden", willOpen ? "false" : "true");
        notificationButton.setAttribute("aria-expanded", willOpen ? "true" : "false");

        if (willOpen) {
          positionNotificationFlyout();
          await loadNotificationFlyout();
        }
      });
    }

    notificationMarkAll?.addEventListener("click", async function (event) {
      event.preventDefault();
      event.stopPropagation();

      try {
        await markSystemNotificationRead("", true);
        await loadNotificationFlyout({ silent: true });
      } catch {
        await loadNotificationFlyout();
      }
    });

    notificationModalBackdrop?.addEventListener("click", closeNotificationModal);
    notificationModalClose?.addEventListener("click", closeNotificationModal);
    notificationModalDismiss?.addEventListener("click", closeNotificationModal);

    profileOpen?.addEventListener("click", async (event) => {
      event.preventDefault();
      event.stopPropagation();
      openProfileModal();
      closeProfileMenu();
      await loadProfileIntoModal();
    });

    profileModalBackdrop?.addEventListener("click", closeProfileModal);
    profileModalClose?.addEventListener("click", closeProfileModal);
    profileForm?.addEventListener("submit", (event) => {
      event.preventDefault();
      void saveProfileFromModal();
    });
    profileSave?.addEventListener("click", () => void saveProfileFromModal());

    activeEscapeKeyHandler = function (e) {
      if (e.key === "Escape") {
        closeNotificationModal();
        closeProfileModal();
        closeSidebar();
        closeProfileMenu();
      }
    };

    document.addEventListener("keydown", activeEscapeKeyHandler);

    const logoutBtn = document.getElementById("appShellLogout");
    if (logoutBtn) {
      logoutBtn.addEventListener("click", async function (e) {
        e.preventDefault();
        closeProfileMenu();
        if (window.AuthClient?.logout) {
          await window.AuthClient.logout();
        } else {
          clearAuth();
        }
        const query = new URLSearchParams({
          loggedOut: "1",
          message: "Bạn đã đăng xuất.",
        });
        window.location.replace(`/home/login.html?${query.toString()}`);
      });
    }

    if (hasPrivateShell) {
      void refreshNotificationBadge();
      void loadNotificationFlyout({ silent: true });
      activeNotificationPollTimer = window.setInterval(() => {
        if (document.visibilityState !== "visible") {
          return;
        }

        void refreshNotificationBadge();
        if (notificationFlyout?.classList.contains("show")) {
          void loadNotificationFlyout({ silent: true });
        }
      }, NOTIFICATION_BADGE_POLL_MS);

      activeNotificationVisibilityHandler = () => {
        if (document.visibilityState === "visible") {
          void refreshNotificationBadge();
          positionNotificationFlyout();
          if (notificationFlyout?.classList.contains("show")) {
            void loadNotificationFlyout({ silent: true });
          }
        }
      };

      document.addEventListener("visibilitychange", activeNotificationVisibilityHandler);

      activeNotificationResizeHandler = () => {
        if (notificationFlyout?.classList.contains("show")) {
          positionNotificationFlyout();
        }
      };

      window.addEventListener("resize", activeNotificationResizeHandler);
    } else {
      applyNotificationBadge(0);
    }

    activeShellStateKey = nextShellStateKey;
  }

  window.addEventListener("system-notifications:changed", function (event) {
    applyNotificationBadge(event?.detail?.unreadCount || 0);
  });

  window.addEventListener("auth:changed", function () {
    void mountShell();
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      void mountShell();
    }, { once: true });
  } else {
    void mountShell();
  }

  window.AppShell = window.AppShell || {};
  window.AppShell.mount = mountShell;
})();
