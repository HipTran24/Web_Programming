document.documentElement.classList.add("js");

let pageRevealed = false;
let loaderRemoved = false;
const prefetchedPageUrls = new Set();
const SITE_FAVICON_PATH = "/images/favicon.svg?v=20260402-tab3";
const SITE_SHORTCUT_ICON_PATH = "/favicon.ico?v=20260402-tab3";
const SITE_TOUCH_ICON_PATH = "/images/Logo.png";
const SITE_TITLE = "SynapLearn";
const AJAX_NAV_OVERLAY_ID = "ajax-navigation-overlay";
const AJAX_NAV_STYLE_ID = "ajax-navigation-overlay-style";
const AJAX_PAGE_CACHE_TTL_MS = 120000;

function ensureAjaxNavigationOverlay() {
  if (!document.getElementById(AJAX_NAV_STYLE_ID)) {
    const style = document.createElement("style");
    style.id = AJAX_NAV_STYLE_ID;
    style.textContent = `
      html, body {
        background-color: #08131b;
      }

      #${AJAX_NAV_OVERLAY_ID} {
        position: fixed;
        inset: 0;
        z-index: 9998;
        pointer-events: none;
        opacity: 0;
        background:
          radial-gradient(circle at 10% 12%, rgba(103, 216, 203, 0.12), transparent 26%),
          radial-gradient(circle at 84% 10%, rgba(243, 195, 108, 0.12), transparent 24%),
          radial-gradient(circle at 50% 90%, rgba(93, 159, 255, 0.08), transparent 28%),
          linear-gradient(180deg, #08131b 0%, #0a1720 40%, #0d1d26 100%);
        transition: opacity 0.18s ease;
        will-change: opacity;
      }

      #${AJAX_NAV_OVERLAY_ID}.is-visible {
        opacity: 1;
      }
    `;
    document.head.appendChild(style);
  }

  if (!document.getElementById(AJAX_NAV_OVERLAY_ID)) {
    const overlay = document.createElement("div");
    overlay.id = AJAX_NAV_OVERLAY_ID;
    overlay.setAttribute("aria-hidden", "true");
    document.body.appendChild(overlay);
  }
}

function getAjaxNavigationOverlay() {
  ensureAjaxNavigationOverlay();
  return document.getElementById(AJAX_NAV_OVERLAY_ID);
}

function showAjaxNavigationOverlay() {
  const overlay = getAjaxNavigationOverlay();
  if (!overlay) {
    return;
  }

  overlay.style.transition = "none";
  overlay.classList.add("is-visible");
  void overlay.offsetHeight;
  requestAnimationFrame(() => {
    overlay.style.transition = "";
  });
}

function hideAjaxNavigationOverlay() {
  const overlay = getAjaxNavigationOverlay();
  if (!overlay) {
    return;
  }

  overlay.style.transition = "";
  overlay.classList.remove("is-visible");
}

function ensureSiteFavicon() {
  const iconHref = new URL(SITE_FAVICON_PATH, window.location.origin).href;
  const shortcutIconHref = new URL(SITE_SHORTCUT_ICON_PATH, window.location.origin).href;
  const touchIconHref = new URL(SITE_TOUCH_ICON_PATH, window.location.origin).href;

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
  ensureLink("apple-touch-icon", touchIconHref, "image/png");
}

window.ensureSiteFavicon = ensureSiteFavicon;

function normalizeSiteTitle(rawTitle) {
  const title = String(rawTitle || "").trim();
  if (!title) {
    return SITE_TITLE;
  }

  if (title === SITE_TITLE) {
    return SITE_TITLE;
  }

  const separators = [" — ", " – ", " - "];
  for (const separator of separators) {
    const siteSuffix = `${separator}${SITE_TITLE}`;
    if (title.endsWith(siteSuffix)) {
      const pageTitle = title.slice(0, -siteSuffix.length).trim();
      return pageTitle ? `${SITE_TITLE} - ${pageTitle}` : SITE_TITLE;
    }

    const sitePrefix = `${SITE_TITLE}${separator}`;
    if (title.startsWith(sitePrefix)) {
      const pageTitle = title.slice(sitePrefix.length).trim();
      return pageTitle ? `${SITE_TITLE} - ${pageTitle}` : SITE_TITLE;
    }
  }

  return title.includes(SITE_TITLE) ? title : `${SITE_TITLE} - ${title}`;
}

window.normalizeSiteTitle = normalizeSiteTitle;

function isShelllessBody(body) {
  return Boolean(
    body?.classList?.contains("page-login") ||
    body?.classList?.contains("page-register") ||
    body?.classList?.contains("page-admin")
  );
}

function removeShellArtifactsForShelllessPages() {
  document.body.removeAttribute("data-shell-page");
  document.getElementById("app-shell-navbar")?.remove();
  document.getElementById("app-shell-sidebar")?.remove();
  document.getElementById("app-shell-backdrop")?.remove();
  document.querySelectorAll("nav.navbar, #navbar, #appSidebar, .app-sidebar").forEach((node) => {
    node.remove();
  });
}

function shouldDisablePrefetch() {
  const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
  if (!connection) {
    return false;
  }

  if (connection.saveData) {
    return true;
  }

  const effectiveType = String(connection.effectiveType || "").toLowerCase();
  return effectiveType.includes("2g");
}

function canPrefetchUrl(url) {
  if (!url) {
    return false;
  }

  if (url.origin !== window.location.origin) {
    return false;
  }

  if (url.pathname === window.location.pathname) {
    return false;
  }

  const path = String(url.pathname || "").toLowerCase();
  return path === "/" ||
    path === "/home" ||
    path === "/home/" ||
    path === "/dashboard" ||
    path === "/dashboard/" ||
    path === "/unauthorized" ||
    path === "/unauthorized/" ||
    path === "/admin" ||
    path === "/admin/" ||
    path.startsWith("/admin/") ||
    (path.startsWith("/home/") && path.endsWith(".html"));
}

function prefetchPageHref(href) {
  if (shouldDisablePrefetch()) {
    return;
  }

  let url;
  try {
    url = new URL(href, window.location.origin);
  } catch {
    return;
  }

  if (!canPrefetchUrl(url)) {
    return;
  }

  const key = `${url.pathname}${url.search}`;
  if (prefetchedPageUrls.has(key)) {
    return;
  }

  prefetchedPageUrls.add(key);
  const link = document.createElement("link");
  link.rel = "prefetch";
  link.href = key;
  link.as = "document";
  document.head.appendChild(link);

  if (window.AjaxNavigation?.prefetch) {
    void window.AjaxNavigation.prefetch(key);
  }
}

function setupSmartPagePrefetch() {
  const scheduleIdle = (callback) => {
    if (typeof window.requestIdleCallback === "function") {
      window.requestIdleCallback(callback, { timeout: 700 });
      return;
    }

    setTimeout(callback, 220);
  };

  const onIntent = (event) => {
    const target = event.target instanceof Element
      ? event.target.closest("a[href]")
      : null;

    if (!target) {
      return;
    }

    const href = target.getAttribute("href") || "";
    prefetchPageHref(href);
  };

  document.addEventListener("pointerenter", onIntent, { capture: true, passive: true });
  document.addEventListener("focusin", onIntent, { capture: true, passive: true });
  document.addEventListener("touchstart", onIntent, { capture: true, passive: true });

  scheduleIdle(() => {
    const candidates = Array.from(document.querySelectorAll("a[href]"))
      .map((anchor) => anchor.getAttribute("href") || "")
      .filter(Boolean)
      .slice(0, 12);

    candidates.forEach((href) => prefetchPageHref(href));
  });
}

function revealPageContent() {
  if (pageRevealed) return;

  pageRevealed = true;

  const loader = document.getElementById("page-loader");
  const content = document.querySelector(".page-content");

  // Hiện nội dung
  if (content) {
    requestAnimationFrame(() => {
      content.classList.add("show");
      document.documentElement.classList.add("page-hydrated");
    });
  } else {
    document.documentElement.classList.add("page-hydrated");
  }

  // Ẩn loader
  if (loader) {
    loader.classList.add("hide");

    const cleanup = () => {
      if (loaderRemoved) return;
      loaderRemoved = true;
      loader.remove();
    };

    loader.addEventListener("transitionend", cleanup, { once: true });
    setTimeout(cleanup, 160);
  }
}

document.addEventListener("DOMContentLoaded", function () {
  ensureSiteFavicon();
  ensureAjaxNavigationOverlay();
  document.title = normalizeSiteTitle(document.title);

  /* ================= DEMO BUTTON ================= */
  document.querySelectorAll("[data-demo-save]").forEach((btn) => {
    btn.addEventListener("click", () => {
      alert("Đã lưu cấu hình thành công (demo).");
    });
  });

  document.querySelectorAll("[data-demo-submit]").forEach((btn) => {
    btn.addEventListener("click", () => {
      alert("Thao tác thành công (demo).");
    });
  });

  /* ================= TOGGLE PASSWORD ================= */
  const passwordInput = document.getElementById("password");
  const togglePassword = document.getElementById("togglePassword");
  const eyeOpen = document.getElementById("eyeOpen");
  const eyeClosed = document.getElementById("eyeClosed");

  if (passwordInput && togglePassword) {
    togglePassword.addEventListener("click", function () {
      const isPassword = passwordInput.type === "password";
      passwordInput.type = isPassword ? "text" : "password";

      if (eyeOpen && eyeClosed) {
        eyeOpen.style.display = isPassword ? "none" : "block";
        eyeClosed.style.display = isPassword ? "block" : "none";
      }
    });
  }

  document.querySelectorAll("[data-demo-danger-confirm]").forEach((btn) => {
    btn.addEventListener("click", function (e) {
      if (!confirm("Bạn có chắc muốn xóa mục này không?")) {
        e.preventDefault();
      }
    });
  });

  /* ================= SCROLL REVEAL ================= */
  const reveals = document.querySelectorAll(".reveal");

  if (reveals.length > 0) {
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("visible");
          }
        });
      },
      { threshold: 0.15 }
    );

    reveals.forEach((el) => io.observe(el));
  }

  // Reveal immediately after DOM is ready for fastest perceived load.
  revealPageContent();
  setupSmartPagePrefetch();
});

if (document.readyState !== "loading") {
  ensureSiteFavicon();
  ensureAjaxNavigationOverlay();
  document.title = normalizeSiteTitle(document.title);
  revealPageContent();
}

window.addEventListener("pageshow", revealPageContent, { once: true });

// Short fallback to avoid a blocked screen in unexpected script execution order.
setTimeout(revealPageContent, 300);

(function () {
  const MANAGED_HEAD_ATTR = "data-ajax-managed-head";
  const RUNTIME_SCRIPT_ATTR = "data-ajax-runtime-script";
  const HEAD_SELECTOR = 'link[rel="stylesheet"], link[rel="preconnect"], link[rel="icon"], style';
  const SHARED_SCRIPT_URLS = new Set([
    toAbsoluteUrl("https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"),
    toAbsoluteUrl("/js/auth.js"),
    toAbsoluteUrl("/assets/js/main.js"),
    toAbsoluteUrl("/js/app-shell.js"),
  ]);
  const loadedSharedScripts = new Set();
  const pageResponseCache = new Map();
  let activeNavigationController = null;
  let isNavigating = false;

  function waitForNextPaint() {
    return new Promise((resolve) => {
      requestAnimationFrame(() => {
        requestAnimationFrame(resolve);
      });
    });
  }

  function toAbsoluteUrl(value, baseUrl) {
    try {
      return new URL(value, baseUrl || window.location.href).href;
    } catch {
      return String(value || "");
    }
  }

  function getPageCacheKey(input) {
    return toAbsoluteUrl(input, window.location.href);
  }

  function getCachedPage(input) {
    const key = getPageCacheKey(input);
    const entry = pageResponseCache.get(key);
    if (!entry) {
      return null;
    }

    if ((Date.now() - Number(entry.cachedAt || 0)) > AJAX_PAGE_CACHE_TTL_MS) {
      pageResponseCache.delete(key);
      return null;
    }

    return entry;
  }

  function storeCachedPage(requestUrl, responseUrl, html) {
    const normalizedResponseUrl = toAbsoluteUrl(responseUrl || requestUrl, window.location.href);
    const entry = {
      requestUrl: getPageCacheKey(requestUrl),
      responseUrl: normalizedResponseUrl,
      html: String(html || ""),
      cachedAt: Date.now(),
    };

    pageResponseCache.set(entry.requestUrl, entry);
    pageResponseCache.set(normalizedResponseUrl, entry);
    return entry;
  }

  async function prefetchPageDocument(input) {
    const targetUrl = new URL(input, window.location.href);
    if (!isAjaxNavigableUrl(targetUrl) || getCachedPage(targetUrl)) {
      return false;
    }

    try {
      const response = await fetch(targetUrl.toString(), {
        method: "GET",
        credentials: "same-origin",
        headers: {
          Accept: "text/html",
          "X-Requested-With": "XMLHttpRequest",
        },
        cache: "force-cache",
      });

      const responseType = String(response.headers.get("content-type") || "").toLowerCase();
      if (!response.ok || !responseType.includes("text/html")) {
        return false;
      }

      const html = await response.text();
      const responseUrl = new URL(response.url || targetUrl.toString(), window.location.origin);
      storeCachedPage(targetUrl, responseUrl, html);
      return true;
    } catch {
      return false;
    }
  }

  function markInitialManagedHeadNodes() {
    document.head.querySelectorAll(HEAD_SELECTOR).forEach((node) => {
      node.setAttribute(MANAGED_HEAD_ATTR, "true");
    });

    document.querySelectorAll("script[src]").forEach((script) => {
      const absoluteSrc = toAbsoluteUrl(script.getAttribute("src"));
      if (SHARED_SCRIPT_URLS.has(absoluteSrc)) {
        loadedSharedScripts.add(absoluteSrc);
      }
    });
  }

  function isAjaxNavigableUrl(url) {
    if (!url || url.origin !== window.location.origin) {
      return false;
    }

    const path = String(url.pathname || "").toLowerCase();
    if (!path || path.startsWith("/api/")) {
      return false;
    }

    return path === "/" ||
      path === "/home" ||
      path === "/home/" ||
      path === "/dashboard" ||
      path === "/dashboard/" ||
      path === "/admin" ||
      path === "/admin/" ||
      path === "/unauthorized" ||
      path === "/unauthorized/" ||
      path.startsWith("/admin/") ||
      (path.startsWith("/home/") && path.endsWith(".html"));
  }

  function shouldHandleLinkClick(anchor, event) {
    if (!anchor || event.defaultPrevented) {
      return false;
    }

    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return false;
    }

    if (anchor.target && anchor.target !== "_self") {
      return false;
    }

    if (anchor.hasAttribute("download") || anchor.getAttribute("rel") === "external") {
      return false;
    }

    if (anchor.dataset.noAjax === "true") {
      return false;
    }

    const rawHref = String(anchor.getAttribute("href") || "").trim();
    if (!rawHref || rawHref.startsWith("#") || rawHref.startsWith("javascript:")) {
      return false;
    }

    const targetUrl = new URL(anchor.href, window.location.href);
    if (!isAjaxNavigableUrl(targetUrl)) {
      return false;
    }

    if (
      targetUrl.pathname === window.location.pathname &&
      targetUrl.search === window.location.search &&
      targetUrl.hash
    ) {
      return false;
    }

    return true;
  }

  function buildHeadSignature(node, baseUrl) {
    const tagName = String(node.tagName || "").toLowerCase();
    if (tagName === "style") {
      return `style::${String(node.textContent || "").trim()}`;
    }

    const rel = String(node.getAttribute("rel") || "").toLowerCase();
    const href = toAbsoluteUrl(node.getAttribute("href"), baseUrl);
    const crossorigin = String(node.getAttribute("crossorigin") || "");
    return `${tagName}::${rel}::${href}::${crossorigin}`;
  }

  function cloneHeadNode(node, baseUrl) {
    const clone = document.createElement(node.tagName.toLowerCase());
    Array.from(node.attributes).forEach((attribute) => {
      if (attribute.name === "href") {
        clone.setAttribute("href", toAbsoluteUrl(attribute.value, baseUrl));
        return;
      }

      clone.setAttribute(attribute.name, attribute.value);
    });

    if (clone.tagName.toLowerCase() === "style") {
      clone.textContent = node.textContent || "";
    }

    clone.setAttribute(MANAGED_HEAD_ATTR, "true");
    return clone;
  }

  async function waitForStylesheet(node) {
    if (!node || node.tagName.toLowerCase() !== "link") {
      return;
    }

    if (String(node.getAttribute("rel") || "").toLowerCase() !== "stylesheet") {
      return;
    }

    if (node.sheet) {
      return;
    }

    await new Promise((resolve) => {
      let settled = false;
      const finalize = () => {
        if (settled) {
          return;
        }

        settled = true;
        node.removeEventListener("load", finalize);
        node.removeEventListener("error", finalize);
        resolve();
      };

      node.addEventListener("load", finalize, { once: true });
      node.addEventListener("error", finalize, { once: true });
      window.setTimeout(finalize, 1500);
    });
  }

  async function syncHeadAssets(nextDocument, nextUrl) {
    const desiredNodes = Array.from(nextDocument.head.querySelectorAll(HEAD_SELECTOR));
    const currentNodes = Array.from(document.head.querySelectorAll(`[${MANAGED_HEAD_ATTR}="true"]`));
    const currentSignatures = new Set(
      currentNodes.map((node) => buildHeadSignature(node))
    );
    const insertionAnchor = document.head.querySelector("script");

    for (const desiredNode of desiredNodes) {
      const signature = buildHeadSignature(desiredNode, nextUrl);
      if (currentSignatures.has(signature)) {
        continue;
      }

      const currentNode = cloneHeadNode(desiredNode, nextUrl);
      document.head.insertBefore(currentNode, insertionAnchor || null);
      await waitForStylesheet(currentNode);
      currentSignatures.add(signature);
    }
  }

  function syncBodyAttributes(nextBody) {
    Array.from(document.body.attributes).forEach((attribute) => {
      document.body.removeAttribute(attribute.name);
    });

    Array.from(nextBody.attributes).forEach((attribute) => {
      document.body.setAttribute(attribute.name, attribute.value);
    });

    if (isShelllessBody(nextBody)) {
      removeShellArtifactsForShelllessPages();
      return;
    }

    if (document.getElementById("app-shell-navbar") && !document.body.classList.contains("page-admin")) {
      document.body.setAttribute("data-shell-page", "true");
    }
  }

  function hardNavigate(url) {
    showAjaxNavigationOverlay();
    window.location.assign(url);
  }

  function cleanupTransientUi() {
    document.querySelectorAll(".modal-backdrop, .offcanvas-backdrop").forEach((node) => node.remove());
    document.body.classList.remove("modal-open");
    document.body.style.removeProperty("overflow");
    document.body.style.removeProperty("padding-right");
  }

  function cleanupOrphanedInteractiveLayers() {
    const hasVisibleModal = Boolean(document.querySelector(".modal.show, .offcanvas.show"));
    const hasVisibleLoader = Boolean(document.querySelector("#page-loader:not(.hide)"));
    const hasVisibleAdminDrawer = Boolean(document.querySelector(".details-drawer.is-open"));
    if (hasVisibleModal || hasVisibleLoader || hasVisibleAdminDrawer) {
      return;
    }

    document.querySelectorAll(".modal-backdrop, .offcanvas-backdrop").forEach((node) => node.remove());
    document.body.classList.remove("modal-open");
    document.body.style.removeProperty("overflow");
    document.body.style.removeProperty("padding-right");
  }

  function syncAdminSidebar(nextUrl) {
    const currentPath = String(nextUrl.pathname || "").replace(/\/+$/, "") || "/admin";
    document.querySelectorAll(".portal-sidebar .portal-nav a").forEach((link) => {
      const href = String(link.getAttribute("href") || "").trim();
      if (!href) {
        link.classList.remove("is-active");
        return;
      }

      const targetUrl = new URL(href, window.location.origin);
      const targetPath = String(targetUrl.pathname || "").replace(/\/+$/, "") || "/admin";
      const isActive = currentPath === targetPath;
      link.classList.toggle("is-active", isActive);
    });
  }

  function syncAdminAuxiliaryNodes(nextDocument) {
    const adminAuxiliaryIds = [
      "adminActionModal",
      "userEditorModal",
      "adminPasswordModal",
      "adminCreateModal",
      "overviewWindowModal",
      "detailsDrawer",
    ];

    adminAuxiliaryIds.forEach((id) => {
      document.querySelectorAll(`#${id}`).forEach((node) => node.remove());
    });

    adminAuxiliaryIds.forEach((id) => {
      const nextNode = nextDocument.getElementById(id);
      if (!nextNode) {
        return;
      }

      document.body.appendChild(nextNode.cloneNode(true));
    });
  }

  function swapContent(nextDocument, nextUrl) {
    const currentAdminShell = document.querySelector(".page-content.portal-shell");
    const nextAdminShell = nextDocument.querySelector(".page-content.portal-shell");

    if (document.body.classList.contains("page-admin") && nextDocument.body.classList.contains("page-admin") && currentAdminShell && nextAdminShell) {
      nextAdminShell.classList.add("show");
      currentAdminShell.replaceWith(nextAdminShell);
      syncAdminAuxiliaryNodes(nextDocument);
      return true;
    }

    const currentPageContent = document.querySelector(".page-content");
    const nextPageContent = nextDocument.querySelector(".page-content");
    if (!currentPageContent || !nextPageContent) {
      return false;
    }

    nextPageContent.classList.add("show");
    currentPageContent.replaceWith(nextPageContent);
    return true;
  }

  function removeRuntimeScripts() {
    document.querySelectorAll(`script[${RUNTIME_SCRIPT_ATTR}="true"]`).forEach((node) => node.remove());
  }

  async function executeExternalScript(sourceNode, nextUrl, forceReload) {
    const absoluteSrc = toAbsoluteUrl(sourceNode.getAttribute("src"), nextUrl);
    const isShared = SHARED_SCRIPT_URLS.has(absoluteSrc);
    if (isShared && loadedSharedScripts.has(absoluteSrc) && !forceReload) {
      return;
    }

    const script = document.createElement("script");
    Array.from(sourceNode.attributes).forEach((attribute) => {
      if (attribute.name === "src") {
        script.setAttribute("src", absoluteSrc);
        return;
      }

      script.setAttribute(attribute.name, attribute.value);
    });

    if (!isShared || forceReload) {
      script.setAttribute(RUNTIME_SCRIPT_ATTR, "true");
    }

    await new Promise((resolve, reject) => {
      script.addEventListener("load", resolve, { once: true });
      script.addEventListener("error", reject, { once: true });
      document.body.appendChild(script);
    });

    if (isShared) {
      loadedSharedScripts.add(absoluteSrc);
    }
  }

  async function executeInlineScript(sourceNode) {
    const script = document.createElement("script");
    Array.from(sourceNode.attributes).forEach((attribute) => {
      script.setAttribute(attribute.name, attribute.value);
    });
    script.setAttribute(RUNTIME_SCRIPT_ATTR, "true");
    script.textContent = sourceNode.textContent || "";
    document.body.appendChild(script);
  }

  async function executePageScripts(nextDocument, nextUrl) {
    removeRuntimeScripts();

    const scripts = Array.from(nextDocument.querySelectorAll("body script"));
    for (const sourceNode of scripts) {
      if (sourceNode.src) {
        const absoluteSrc = toAbsoluteUrl(sourceNode.getAttribute("src"), nextUrl);
        const isShared = SHARED_SCRIPT_URLS.has(absoluteSrc);
        await executeExternalScript(sourceNode, nextUrl, !isShared);
        continue;
      }

      await executeInlineScript(sourceNode);
    }
  }

  function scrollAfterNavigation(url) {
    if (url.hash) {
      const target = document.getElementById(url.hash.slice(1));
      if (target) {
        target.scrollIntoView({ behavior: "auto", block: "start" });
        return;
      }
    }

    window.scrollTo({ top: 0, left: 0, behavior: "auto" });
  }

  async function finalizeNavigation(nextDocument, nextUrl, options) {
    ensureAjaxNavigationOverlay();

    document.dispatchEvent(new CustomEvent("ajax:before-swap", {
      detail: {
        url: nextUrl.toString(),
      },
    }));

    await syncHeadAssets(nextDocument, nextUrl);
    ensureSiteFavicon();
    cleanupTransientUi();

    const didSwap = swapContent(nextDocument, nextUrl);
    if (!didSwap) {
      hardNavigate(nextUrl.toString());
      return false;
    }

    syncBodyAttributes(nextDocument.body);
    if (isShelllessBody(nextDocument.body)) {
      removeShellArtifactsForShelllessPages();
    }
    document.title = normalizeSiteTitle(nextDocument.title || document.title);

    if (options.replaceHistory) {
      window.history.replaceState({ ajax: true }, "", nextUrl.toString());
    } else if (!options.fromPopState) {
      window.history.pushState({ ajax: true }, "", nextUrl.toString());
    }

    await executePageScripts(nextDocument, nextUrl);

    if (window.AppShell?.mount) {
      await window.AppShell.mount();
    }

    scrollAfterNavigation(nextUrl);
    document.dispatchEvent(new CustomEvent("ajax:navigated", {
      detail: {
        url: nextUrl.toString(),
      },
    }));

    await waitForNextPaint();

    return true;
  }

  async function navigate(input, options = {}) {
    const targetUrl = new URL(input, window.location.href);
    if (!isAjaxNavigableUrl(targetUrl)) {
      hardNavigate(targetUrl.toString());
      return false;
    }

    if (
      targetUrl.pathname === window.location.pathname &&
      targetUrl.search === window.location.search &&
      targetUrl.hash !== window.location.hash
    ) {
      if (options.replaceHistory) {
        window.history.replaceState({ ajax: true }, "", targetUrl.toString());
      } else if (!options.fromPopState) {
        window.history.pushState({ ajax: true }, "", targetUrl.toString());
      }

      scrollAfterNavigation(targetUrl);
      return true;
    }

    if (
      targetUrl.pathname === window.location.pathname &&
      targetUrl.search === window.location.search &&
      targetUrl.hash === window.location.hash
    ) {
      return false;
    }

    if (activeNavigationController) {
      activeNavigationController.abort();
      activeNavigationController = null;
    }

    const controller = new AbortController();
    activeNavigationController = controller;
    isNavigating = true;
    document.documentElement.classList.add("is-ajax-navigating");

    try {
      const cachedPage = getCachedPage(targetUrl);
      if (cachedPage?.html) {
        const nextDocument = new DOMParser().parseFromString(cachedPage.html, "text/html");
        const responseUrl = new URL(cachedPage.responseUrl || targetUrl.toString(), window.location.origin);
        return await finalizeNavigation(nextDocument, responseUrl, options);
      }

      const response = await fetch(targetUrl.toString(), {
        method: "GET",
        credentials: "same-origin",
        headers: {
          Accept: "text/html",
          "X-Requested-With": "XMLHttpRequest",
        },
        signal: controller.signal,
      });

      const responseUrl = new URL(response.url || targetUrl.toString(), window.location.origin);
      const responseType = String(response.headers.get("content-type") || "").toLowerCase();
      if (!response.ok || !responseType.includes("text/html")) {
        hardNavigate(responseUrl.toString());
        return false;
      }

      const html = await response.text();
      if (controller.signal.aborted) {
        return false;
      }

      storeCachedPage(targetUrl, responseUrl, html);
      const nextDocument = new DOMParser().parseFromString(html, "text/html");
      return await finalizeNavigation(nextDocument, responseUrl, options);
    } catch (error) {
      if (error?.name === "AbortError") {
        return false;
      }

      hardNavigate(targetUrl.toString());
      return false;
    } finally {
      if (activeNavigationController === controller) {
        activeNavigationController = null;
      }

      isNavigating = false;
      document.documentElement.classList.remove("is-ajax-navigating");
    }
  }

  function installNavigationHooks() {
    document.addEventListener("pointerdown", function (event) {
      const target = event.target instanceof Element
        ? event.target.closest("button, a, [role='button'], input, select, textarea")
        : null;

      if (!target) {
        return;
      }

      cleanupOrphanedInteractiveLayers();
    }, true);

    document.addEventListener("click", function (event) {
      const anchor = event.target instanceof Element
        ? event.target.closest("a[href]")
        : null;

      if (!shouldHandleLinkClick(anchor, event)) {
        return;
      }

      event.preventDefault();
      void navigate(anchor.href);
    }, true);

    window.addEventListener("popstate", function () {
      if (isNavigating) {
        return;
      }

      void navigate(window.location.href, {
        replaceHistory: true,
        fromPopState: true,
      });
    });

    window.addEventListener("beforeunload", function () {
      showAjaxNavigationOverlay();
    });
  }

  markInitialManagedHeadNodes();
  installNavigationHooks();

  window.AjaxNavigation = {
    navigate,
    prefetch: prefetchPageDocument,
  };
})();
