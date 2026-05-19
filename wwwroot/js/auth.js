(function () {
  const tokenStorageKey = "auth.accessToken";
  const userStorageKey = "auth.currentUser";
  const avatarStorageKey = "auth.avatar";
  const loginPage = "/home/login.html";
  const defaultLandingPage = "/dashboard";
  const adminLandingPage = "/admin";
  const authPages = new Set([
    "/home/login.html",
    "/home/register.html",
    "/home/otp.html",
  ]);
  const publicPages = new Set([
    "/home/index.html",
    "/home/about.html",
    "/home/guide.html",
    "/home/upload.html",
    "/home/unauthorized.html",
    "/premium/upgrade.html",
  ]);
  const premiumPublicPages = new Set([
    "/premium/upgrade.html",
  ]);
  const premiumPaymentPages = new Set([
    "/premium/checkout.html",
    "/premium/payment-success.html",
    "/premium/payment-failed.html",
  ]);
  const premiumBlockedPages = new Set([
    "/premium/upgrade.html",
    "/premium/checkout.html",
    "/premium/payment-success.html",
    "/premium/payment-failed.html",
  ]);
  const premiumFeatureRedirects = new Map([
    ["/", "/premium/dashboard.html"],
    ["/home", "/premium/dashboard.html"],
    ["/home/index.html", "/premium/dashboard.html"],
    ["/dashboard", "/premium/dashboard.html"],
    ["/home/dashboard.html", "/premium/dashboard.html"],
    ["/home/upload.html", "/premium/study-workspace.html"],
    ["/home/content-list.html", "/premium/content-library.html"],
    ["/home/content-detail.html", "/premium/content-detail.html"],
    ["/home/history.html", "/premium/content-library.html"],
    ["/home/quiz.html", "/premium/quiz-experience.html"],
    ["/home/quiz-result.html", "/premium/quiz-result.html"],
    ["/home/analytics.html", "/premium/analytics.html"],
    ["/home/learning-plan.html", "/premium/learning-plan.html"],
  ]);
  const protectedPageRoles = new Map([
    ["/admin", ["admin"]],
    ["/dashboard", ["user", "premium"]],
    ["/home/admin.html", ["admin"]],
    ["/home/dashboard.html", ["user", "premium"]],
    ["/premium/checkout.html", ["user", "premium"]],
    ["/premium/payment-success.html", ["user", "premium"]],
    ["/premium/payment-failed.html", ["user", "premium"]],
  ]);
  const sessionKeys = [
    tokenStorageKey,
    userStorageKey,
    avatarStorageKey,
    "token",
    "role",
    "name",
    "avatar",
    "notifCount",
    "progress",
    "auth.google.returnUrl",
  ];

  let validatedUser = null;
  let validationPromise = null;
  let bootPromise = Promise.resolve(null);
  let bootCompleted = false;

  const normalizePath = (value) => {
    const raw = String(value || "").trim().toLowerCase();
    if (!raw) {
      return "/";
    }

    const normalized = raw.replace(/\/+/g, "/");
    if (normalized.length > 1 && normalized.endsWith("/")) {
      return normalized.slice(0, -1);
    }

    return normalized;
  };

  const currentPath = () => normalizePath(window.location.pathname);

  const getDefaultLandingByRole = (role) => {
    const normalizedRole = String(role || "").trim().toLowerCase();
    if (normalizedRole === "admin") {
      return adminLandingPage;
    }

    if (normalizedRole === "premium") {
      return "/premium/dashboard.html";
    }

    return defaultLandingPage;
  };

  const getPageAccess = (pathname) => {
    const path = normalizePath(pathname || currentPath());

    if (authPages.has(path)) {
      return { path, kind: "auth", roles: [] };
    }

    if (publicPages.has(path) || path === "/" || path === "/home" || path === "/unauthorized") {
      return { path, kind: "public", roles: [] };
    }

    if (protectedPageRoles.has(path)) {
      return { path, kind: "protected", roles: protectedPageRoles.get(path) || [] };
    }

    if (premiumPublicPages.has(path)) {
      return { path, kind: "public", roles: [] };
    }

    if (premiumPaymentPages.has(path)) {
      return { path, kind: "protected", roles: ["user", "premium"] };
    }

    if (path === "/premium/account.html") {
      return { path, kind: "protected", roles: ["premium"] };
    }

    if (path.startsWith("/admin/")) {
      return { path, kind: "protected", roles: ["admin"] };
    }

    if (path.startsWith("/premium/") && path.endsWith(".html")) {
      return { path, kind: "protected", roles: ["premium"] };
    }

    if (path.startsWith("/home/") && path.endsWith(".html")) {
      return { path, kind: "protected", roles: ["user", "premium"] };
    }

    return { path, kind: "public", roles: [] };
  };

  const readFromStorages = (key) => {
    const localValue = window.localStorage.getItem(key);
    if (localValue) {
      return { value: localValue, storage: window.localStorage };
    }

    const sessionValue = window.sessionStorage.getItem(key);
    if (sessionValue) {
      return { value: sessionValue, storage: window.sessionStorage };
    }

    return { value: null, storage: null };
  };

  const getAccessToken = () => readFromStorages(tokenStorageKey).value;

  const parseCurrentUser = () => {
    const raw = readFromStorages(userStorageKey).value;
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  };

  const normalizeUser = (user) => {
    const rawRole = String(user?.role || "").trim();
    const subscriptionTier = String(user?.subscriptionTier || "").trim();
    const isPremium = Boolean(user?.isPremium) || subscriptionTier.toLowerCase() === "premium";
    const resolvedRole = rawRole.toLowerCase() === "admin"
      ? "Admin"
      : isPremium
        ? "Premium"
        : rawRole;

    return {
      userId: user?.userId ?? null,
      username: user?.username ?? "",
      fullName: user?.fullName ?? "",
      email: user?.email ?? "",
      role: resolvedRole,
      isPremium,
      subscriptionTier: isPremium ? "Premium" : subscriptionTier,
      avatarUrl: user?.avatarUrl ?? readFromStorages(avatarStorageKey).value ?? "",
      expiresAt: user?.expiresAt ?? null,
    };
  };

  const getCurrentUser = () => {
    if (validatedUser) {
      return validatedUser;
    }

    if (!bootCompleted) {
      return null;
    }

    const parsed = parseCurrentUser();
    return parsed ? normalizeUser(parsed) : null;
  };

  const getActiveSessionStorage = () => {
    const tokenInfo = readFromStorages(tokenStorageKey);
    if (tokenInfo.storage) {
      return tokenInfo.storage;
    }

    const userInfo = readFromStorages(userStorageKey);
    if (userInfo.storage) {
      return userInfo.storage;
    }

    return null;
  };

  const dispatchAuthChanged = () => {
    window.dispatchEvent(
      new CustomEvent("auth:changed", {
        detail: {
          authenticated: Boolean(validatedUser),
          user: validatedUser,
          page: getPageAccess(),
        },
      }),
    );
  };

  const clearSession = () => {
    validatedUser = null;

    sessionKeys.forEach((key) => {
      window.localStorage.removeItem(key);
      window.sessionStorage.removeItem(key);
    });

    applyAuthVisibility();
    dispatchAuthChanged();
  };

  const logout = async () => {
    try {
      await fetch("/api/auth/logout", {
        method: "POST",
        keepalive: true,
        headers: {
          Accept: "application/json",
        },
      });
    } catch {
      // Ignore logout transport issues and still clear local session.
    }

    clearSession();
  };

  const syncStoredUser = (user) => {
    const token = getAccessToken();
    const normalized = normalizeUser(user);
    const storage = getActiveSessionStorage() || window.sessionStorage;
    const otherStorage = storage === window.localStorage
      ? window.sessionStorage
      : window.localStorage;
    const displayName = resolveDisplayName(normalized.fullName, normalized.email);

    if (token) {
      storage.setItem(tokenStorageKey, token);
      storage.setItem("token", token);
    } else {
      storage.removeItem(tokenStorageKey);
      storage.removeItem("token");
    }
    storage.setItem(userStorageKey, JSON.stringify(normalized));
    storage.setItem("role", normalized.role || "");
    storage.setItem("name", displayName);

    if (normalized.avatarUrl) {
      storage.setItem(avatarStorageKey, normalized.avatarUrl);
      storage.setItem("avatar", normalized.avatarUrl);
    } else {
      storage.removeItem(avatarStorageKey);
      storage.removeItem("avatar");
    }

    [tokenStorageKey, "token", userStorageKey, "role", "name", avatarStorageKey, "avatar"].forEach((key) => {
      otherStorage.removeItem(key);
    });

    validatedUser = normalized;
    applyAuthVisibility();
    dispatchAuthChanged();
    return normalized;
  };

  const storeSession = (data, rememberMe) => {
    const token = String(data?.accessToken || "").trim();
    if (!token) {
      return null;
    }

    const storage = rememberMe ? window.localStorage : window.sessionStorage;
    const otherStorage = rememberMe ? window.sessionStorage : window.localStorage;
    const user = normalizeUser(data);
    const displayName = resolveDisplayName(user.fullName, user.email);

    sessionKeys.forEach((key) => {
      window.localStorage.removeItem(key);
      window.sessionStorage.removeItem(key);
    });

    storage.setItem(tokenStorageKey, token);
    storage.setItem("token", token);
    storage.setItem(userStorageKey, JSON.stringify(user));
    storage.setItem("role", user.role || "");
    storage.setItem("name", displayName);

    if (user.avatarUrl) {
      storage.setItem(avatarStorageKey, user.avatarUrl);
      storage.setItem("avatar", user.avatarUrl);
    }

    [tokenStorageKey, "token", userStorageKey, "role", "name", avatarStorageKey, "avatar"].forEach((key) => {
      if (otherStorage.getItem(key)) {
        otherStorage.removeItem(key);
      }
    });

    validatedUser = user;
    applyAuthVisibility();
    dispatchAuthChanged();
    return user;
  };

  const hasSessionToken = () => Boolean(getAccessToken());
  const hasSessionArtifacts = () => Boolean(getAccessToken() || parseCurrentUser());

  const isAuthenticated = () => Boolean(validatedUser);

  const setElementVisible = (element, visible) => {
    if (!element) {
      return;
    }

    if (visible) {
      element.style.removeProperty("display");
      return;
    }

    element.style.setProperty("display", "none", "important");
  };

  function applyAuthVisibility() {
    const authed = isAuthenticated();

    document.querySelectorAll("[data-auth-guest]").forEach((el) => {
      setElementVisible(el, !authed);
    });

    document.querySelectorAll("[data-auth-user]").forEach((el) => {
      setElementVisible(el, authed);
    });
  }

  const buildLoginUrl = (message) => {
    const current = `${window.location.pathname}${window.location.search}`;
    const query = new URLSearchParams();
    query.set("returnUrl", current);

    if (message) {
      query.set("message", message);
    }

    return `${loginPage}?${query.toString()}`;
  };

  const redirectToLogin = (message) => {
    const target = buildLoginUrl(message);
    if (currentPath() === normalizePath(loginPage)) {
      return;
    }

    window.location.replace(target);
  };

  const apiGetMe = async () => {
    const token = getAccessToken();

    try {
      const headers = {
        Accept: "application/json",
        "Cache-Control": "no-store",
        Pragma: "no-cache",
      };
      if (token) {
        headers.Authorization = `Bearer ${token}`;
      }

      const response = await fetch("/api/auth/me", {
        method: "GET",
        cache: "no-store",
        headers,
      });

      const contentType = response.headers.get("content-type") || "";
      const data = contentType.toLowerCase().includes("application/json")
        ? await response.json().catch(() => null)
        : null;

      return { ok: response.ok, status: response.status, data };
    } catch {
      return { ok: false, status: 0, data: null };
    }
  };

  const validateSession = async () => {
    if (validatedUser) {
      return validatedUser;
    }

    if (validationPromise) {
      return validationPromise;
    }

    validationPromise = (async () => {
      const me = await apiGetMe();
      if (!me.ok || !me.data) {
        clearSession();
        return null;
      }

      return syncStoredUser(me.data);
    })().finally(() => {
      validationPromise = null;
    });

    return validationPromise;
  };

  
  const roleMatches = (actualRole, requiredRoles) => {
    const normalizedActualRole = String(actualRole || "").trim().toLowerCase();
    const normalizedRequiredRoles = requiredRoles
      .map((role) => String(role || "").trim().toLowerCase())
      .filter(Boolean);

    if (normalizedRequiredRoles.length === 0) {
      return true;
    }

    if (normalizedRequiredRoles.includes(normalizedActualRole)) {
      return true;
    }

    if (normalizedActualRole === "premium" && normalizedRequiredRoles.includes("user")) {
      return true;
    }

    return false;
  };

  const isPremiumOnlyPage = (path) =>
    path.startsWith("/premium/") &&
    path.endsWith(".html") &&
    !premiumPublicPages.has(path) &&
    !premiumPaymentPages.has(path);

  const redirectIfPremiumOnBlockedPage = (me, path) => {
    if (!me?.isPremium) {
      return false;
    }

    if (!premiumBlockedPages.has(path)) {
      return false;
    }

    window.location.replace("/premium/account.html");
    return true;
  };

  const redirectPremiumToPremiumFeature = (me, path) => {
    if (!me?.isPremium) {
      return false;
    }

    const target = premiumFeatureRedirects.get(path);
    if (!target || target === path) {
      return false;
    }

    window.location.replace(`${target}${window.location.search || ""}${window.location.hash || ""}`);
    return true;
  };

  const requireAuth = async (options) => {
    const opts = options || {};
    const requiredRoles = Array.isArray(opts.roles) ? opts.roles : [];
    const onForbidden = typeof opts.onForbidden === "function" ? opts.onForbidden : null;
    const shouldRedirect = opts.redirect !== false;

    const me = await validateSession();
    if (!me) {
      if (shouldRedirect) {
        if (requiredRoles.includes("admin")) {
          const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
          window.location.replace(`/home/login.html?returnUrl=${returnUrl}`);
        } else {
          redirectToLogin("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }
      }
      return null;
    }

    if (!roleMatches(me.role, requiredRoles)) {
      if (onForbidden) {
        onForbidden(me);
        return null;
      }

      window.location.replace(requiredRoles.includes("admin") ? "/unauthorized" : getDefaultLandingByRole(me.role));
      return null;
    }

    return me;
  };

  const getInitials = (name, email) => {
    const source = String(name || email || "").trim();
    if (!source) {
      return "US";
    }

    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }

    return source.slice(0, 2).toUpperCase();
  };

  const resolveDisplayName = (fullName, email) => {
    const fullNameText = String(fullName || "").trim();
    if (fullNameText) {
      return fullNameText;
    }

    const emailText = String(email || "").trim();
    if (!emailText) {
      return "Người dùng";
    }

    return emailText.split("@")[0] || emailText;
  };

  const bindUserUi = (me, options) => {
    const opts = options || {};
    const nameSelector = opts.nameSelector || "[data-auth-name]";
    const avatarSelector = opts.avatarSelector || "[data-auth-avatar]";
    const roleSelector = opts.roleSelector || "[data-auth-role]";
    const logoutSelector = opts.logoutSelector || "[data-auth-logout]";

    document.querySelectorAll(nameSelector).forEach((el) => {
      el.textContent = resolveDisplayName(me.fullName, me.email);
    });

    document.querySelectorAll(avatarSelector).forEach((el) => {
      el.textContent = getInitials(me.fullName, me.email);
    });

    document.querySelectorAll(roleSelector).forEach((el) => {
      el.textContent = me.role || "User";
    });

    document.querySelectorAll(logoutSelector).forEach((el) => {
      el.addEventListener("click", (event) => {
        event.preventDefault();
        void logout();
        window.location.replace(loginPage);
      });
    });
  };

  const guardCurrentPage = async () => {
    const page = getPageAccess();
    const path = page.path;

    if (page.kind === "auth") {
      const me = await validateSession();
      if (!me) {
        clearSession();
        return null;
      }

      window.location.replace(getDefaultLandingByRole(me.role));
      return me;
    }

    if (page.kind === "protected") {
      const needsPremium = isPremiumOnlyPage(path);
      const requiredRoles = needsPremium ? ["premium"] : page.roles;
      const me = await requireAuth({
        roles: requiredRoles,
        onForbidden: needsPremium
          ? () => window.location.replace("/premium/upgrade.html")
          : null,
      });

      if (me && redirectIfPremiumOnBlockedPage(me, path)) {
        return me;
      }

      if (me && redirectPremiumToPremiumFeature(me, path)) {
        return me;
      }

      return me;
    }

    if (premiumBlockedPages.has(path)) {
      const me = await validateSession();
      if (me && redirectIfPremiumOnBlockedPage(me, path)) {
        return me;
      }
      return me;
    }

    if (premiumFeatureRedirects.has(path)) {
      const me = await validateSession();
      if (me && redirectPremiumToPremiumFeature(me, path)) {
        return me;
      }
      return me;
    }

    if (!hasSessionArtifacts()) {
      clearSession();
      return null;
    }

    return validateSession();
  };

  const boot = () => {
    bootPromise = guardCurrentPage()
      .catch(() => {
        clearSession();
        return null;
      })
      .finally(() => {
        bootCompleted = true;
        applyAuthVisibility();
        dispatchAuthChanged();
      });

    return bootPromise;
  };

  const refreshFromPageShow = () => {
    const page = getPageAccess();
    void guardCurrentPage();
  };

  const handleStorageChange = (event) => {
    if (!event || !sessionKeys.includes(String(event.key || ""))) {
      return;
    }

    const page = getPageAccess();
    if (page.kind === "protected" && !hasSessionArtifacts()) {
      clearSession();
      redirectToLogin("Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.");
      return;
    }

    void guardCurrentPage();
  };

  window.AuthClient = {
    getAccessToken,
    getCurrentUser,
    clearSession,
    logout,
    storeSession,
    isAuthenticated,
    requireAuth,
    validateSession,
    applyAuthVisibility,
    bindUserUi,
    getPageAccess,
    whenReady: () => bootPromise,
  };

  window.addEventListener("pageshow", refreshFromPageShow);
  window.addEventListener("storage", handleStorageChange);

  boot();
})();
