(function () {
  const notice = document.getElementById("historyNotice");
  const timeline = document.getElementById("historyTimeline");
  const activityCount = document.getElementById("historyActivityCount");
  const filterGroup = document.getElementById("historyFilterGroup");
  const prevPageButton = document.getElementById("historyPrevPageButton");
  const nextPageButton = document.getElementById("historyNextPageButton");
  const pageMetaText = document.getElementById("historyPageMetaText");
  const pageSizeSelect = document.getElementById("historyPageSizeSelect");

  const totalContentsEl = document.getElementById("historyTotalContents");
  const totalContentsMetaEl = document.getElementById("historyTotalContentsMeta");
  const totalQuizzesEl = document.getElementById("historyTotalQuizzes");
  const totalQuizzesMetaEl = document.getElementById("historyTotalQuizzesMeta");
  const weeklyGoalEl = document.getElementById("historyWeeklyGoal");
  const weeklyGoalMetaEl = document.getElementById("historyWeeklyGoalMeta");

  let allActivities = [];
  let activeFilter = "all";
  let page = 1;
  let pageSize = 20;
  let totalPages = 1;
  let totalItems = 0;
  let isLoadingActivities = false;
  let shouldRestoreScroll = true;
  let shouldScrollToTimeline = false;

  const allowedPageSizes = new Set([10, 20, 50]);
  const scrollStateKey = "history:scroll-state";
  let scrollListener = null;
  let pageHideListener = null;
  let beforeUnloadListener = null;

  const toSafeInt = (value, fallback) => {
    const parsed = Number.parseInt(String(value || ""), 10);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      return fallback;
    }

    return parsed;
  };

  const initStateFromQuery = () => {
    const params = new URLSearchParams(window.location.search);
    const rawFilter = String(params.get("filter") || "all").trim().toLowerCase();
    activeFilter = rawFilter === "quiz" || rawFilter === "content" ? rawFilter : "all";

    page = toSafeInt(params.get("page"), 1);

    const requestedPageSize = toSafeInt(params.get("pageSize"), 20);
    pageSize = allowedPageSizes.has(requestedPageSize) ? requestedPageSize : 20;

    if (pageSizeSelect) {
      pageSizeSelect.value = String(pageSize);
    }
  };

  const syncQueryState = () => {
    const params = new URLSearchParams();

    if (activeFilter !== "all") {
      params.set("filter", activeFilter);
    }

    if (page > 1) {
      params.set("page", String(page));
    }

    if (pageSize !== 20) {
      params.set("pageSize", String(pageSize));
    }

    const target = params.toString();
    const nextUrl = target
      ? `${window.location.pathname}?${target}`
      : window.location.pathname;

    window.history.replaceState(null, "", nextUrl);
  };

  const scrollBehavior = () => {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches
      ? "auto"
      : "smooth";
  };

  const saveScrollState = () => {
    try {
      const payload = {
        path: `${window.location.pathname}${window.location.search}`,
        y: Math.max(0, Math.round(window.scrollY || 0)),
      };
      window.sessionStorage.setItem(scrollStateKey, JSON.stringify(payload));
    } catch {
      // Ignore storage issues silently.
    }
  };

  const restoreScrollState = () => {
    try {
      const raw = window.sessionStorage.getItem(scrollStateKey);
      if (!raw) {
        return;
      }

      const payload = JSON.parse(raw);
      const expectedPath = `${window.location.pathname}${window.location.search}`;
      const y = Number(payload?.y || 0);

      if (payload?.path !== expectedPath || y <= 0) {
        return;
      }

      window.requestAnimationFrame(() => {
        window.scrollTo({ top: y, behavior: "auto" });
      });
    } catch {
      // Ignore malformed or inaccessible storage.
    }
  };

  const scrollToTimelineTop = () => {
    const container = timeline?.closest("section");
    if (!container) {
      return;
    }

    container.scrollIntoView({ block: "start", behavior: scrollBehavior() });
  };

  const bindScrollPersistence = () => {
    if (scrollListener) {
      return;
    }

    let ticking = false;

    scrollListener = () => {
      if (ticking) {
        return;
      }

      ticking = true;
      window.requestAnimationFrame(() => {
        saveScrollState();
        ticking = false;
      });
    };

    pageHideListener = saveScrollState;
    beforeUnloadListener = saveScrollState;

    window.addEventListener("scroll", scrollListener, { passive: true });
    window.addEventListener("pagehide", pageHideListener);
    window.addEventListener("beforeunload", beforeUnloadListener);
  };

  const disposePage = () => {
    saveScrollState();

    if (scrollListener) {
      window.removeEventListener("scroll", scrollListener);
      scrollListener = null;
    }

    if (pageHideListener) {
      window.removeEventListener("pagehide", pageHideListener);
      pageHideListener = null;
    }

    if (beforeUnloadListener) {
      window.removeEventListener("beforeunload", beforeUnloadListener);
      beforeUnloadListener = null;
    }
  };

  const escapeHtml = (text) =>
    String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const setNotice = (message, kind) => {
    if (!notice) {
      return;
    }

    if (!message) {
      notice.className = "alert d-none mt-3 mb-0";
      notice.textContent = "";
      return;
    }

    notice.className = `alert alert-${kind || "danger"} mt-3 mb-0`;
    notice.textContent = message;
  };

  const getAuthHeaders = () => {
    const headers = {
      Accept: "application/json",
    };

    const token = window.AuthClient?.getAccessToken?.() || "";
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    return headers;
  };

  const toDateText = (value) => {
    const date = new Date(value || "");
    if (Number.isNaN(date.getTime())) {
      return "Không rõ thời gian";
    }

    return date.toLocaleString("vi-VN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const toRelativeText = (value) => {
    const date = new Date(value || "");
    if (Number.isNaN(date.getTime())) {
      return "Vừa cập nhật";
    }

    const diffMs = Date.now() - date.getTime();
    const diffMinutes = Math.max(1, Math.floor(diffMs / 60000));

    if (diffMinutes < 60) {
      return `${diffMinutes} phút trước`;
    }

    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) {
      return `${diffHours} giờ trước`;
    }

    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} ngày trước`;
  };

  const normalizeType = (kind) => {
    const raw = String(kind || "").toLowerCase();
    if (raw === "quiz") {
      return "Quiz";
    }
    if (raw === "videourl") {
      return "URL video";
    }
    if (raw === "documenturl") {
      return "URL tài liệu";
    }
    if (raw === "texturl") {
      return "URL văn bản";
    }
    if (raw === "fileupload") {
      return "Tệp tải lên";
    }
    return kind || "Hoạt động";
  };

  const classifyActivity = (item) => {
    const raw = String(item?.kind || "").toLowerCase();
    return raw === "quiz" ? "quiz" : "content";
  };

  const applyFilterButtons = () => {
    if (!filterGroup) {
      return;
    }

    const buttons = filterGroup.querySelectorAll("[data-filter]");
    buttons.forEach((button) => {
      const isActive = String(button.getAttribute("data-filter") || "all") === activeFilter;
      button.classList.toggle("is-active", isActive);
    });
  };

  const getFilteredActivities = () => {
    return Array.isArray(allActivities) ? allActivities : [];
  };

  const renderLoadingSkeleton = (count = 5) => {
    if (!timeline) {
      return;
    }

    timeline.innerHTML = Array.from({ length: count })
      .map(() => `
        <article class="history-item">
          <div class="history-item-dot" aria-hidden="true"></div>
          <div class="history-item-main">
            <div class="list-skeleton-line list-skeleton-line--wide"></div>
            <div class="list-skeleton-line list-skeleton-line--short mt-2"></div>
          </div>
        </article>
      `)
      .join("");
  };

  const updatePaginationMeta = () => {
    if (activityCount) {
      activityCount.textContent = `${totalItems} hoạt động`;
    }

    if (pageMetaText) {
      pageMetaText.textContent = `Trang ${page} / ${totalPages}`;
    }

    if (prevPageButton) {
      prevPageButton.disabled = isLoadingActivities || page <= 1;
    }

    if (nextPageButton) {
      nextPageButton.disabled = isLoadingActivities || page >= totalPages;
    }
  };

  const renderKpis = (kpis) => {
    const totalContents = Number(kpis?.totalContents || 0);
    const totalContentsLast7Days = Number(kpis?.totalContentsLast7Days || 0);
    const completedQuizzes = Number(kpis?.completedQuizzes || 0);
    const weeklyGoalPercent = Math.max(0, Math.min(100, Number(kpis?.weeklyGoalPercent || 0)));

    if (totalContentsEl) {
      totalContentsEl.textContent = String(totalContents);
    }

    if (totalContentsMetaEl) {
      totalContentsMetaEl.textContent = `7 ngày gần nhất: +${totalContentsLast7Days}`;
    }

    if (totalQuizzesEl) {
      totalQuizzesEl.textContent = String(completedQuizzes);
    }

    if (totalQuizzesMetaEl) {
      totalQuizzesMetaEl.textContent = completedQuizzes > 0
        ? "Bạn đang duy trì nhịp học tốt"
        : "Bắt đầu quiz đầu tiên để có dữ liệu";
    }

    if (weeklyGoalEl) {
      weeklyGoalEl.textContent = `${weeklyGoalPercent}%`;
    }

    if (weeklyGoalMetaEl) {
      const remainingSessions = Math.max(0, Math.ceil(((100 - weeklyGoalPercent) / 100) * 7));
      weeklyGoalMetaEl.textContent = remainingSessions === 0
        ? "Bạn đã hoàn thành mục tiêu tuần này"
        : `Cần thêm ${remainingSessions} phiên học để đạt 100%`;
    }
  };

  const renderTimeline = () => {
    const list = getFilteredActivities();

    updatePaginationMeta();

    if (!timeline) {
      return;
    }

    if (list.length === 0) {
      timeline.innerHTML = `
        <article class="history-item is-empty">
          <div class="history-item-main">
            <div class="history-item-title">Chưa có lịch sử hoạt động</div>
            <div class="history-item-meta">Hãy upload tài liệu hoặc làm quiz để bắt đầu.</div>
          </div>
        </article>
      `;
      return;
    }

    const rows = list.map((item) => {
        const title = escapeHtml(item?.title || "Hoạt động học tập");
        const kind = escapeHtml(normalizeType(item?.kind));
        const result = escapeHtml(item?.result || "Đã cập nhật");
        const at = toDateText(item?.at);
        const relative = toRelativeText(item?.at);
        const actionText = escapeHtml(item?.actionText || "Xem chi tiết");
        const actionUrl = String(item?.actionUrl || "/home/history.html");

        return `
          <article class="history-item">
            <div class="history-item-dot" aria-hidden="true"></div>
            <div class="history-item-main">
              <div class="history-item-head">
                <h6 class="history-item-title">${title}</h6>
                <span class="history-item-time" title="${escapeHtml(at)}">${escapeHtml(relative)}</span>
              </div>
              <div class="history-item-meta">Loại: ${kind} • Kết quả: ${result}</div>
              <div class="history-item-footer">
                <span class="history-item-date">${escapeHtml(at)}</span>
                <a class="btn btn-soft btn-sm" href="${escapeHtml(actionUrl)}">${actionText}</a>
              </div>
            </div>
          </article>
        `;
      });

    timeline.innerHTML = '<div class="small text-muted-2">Đang dựng lịch sử hoạt động...</div>';

    const chunkSize = 10;
    let cursor = 0;
    timeline.innerHTML = "";

    const renderNextChunk = () => {
      const chunk = rows.slice(cursor, cursor + chunkSize).join("");
      timeline.insertAdjacentHTML("beforeend", chunk);
      cursor += chunkSize;

      if (cursor < rows.length) {
        window.requestAnimationFrame(renderNextChunk);
      }
    };

    window.requestAnimationFrame(renderNextChunk);
  };

  const loadOverview = async () => {
    setNotice("");

    try {
      const response = await fetch("/api/dashboard/overview", {
        method: "GET",
        cache: "no-store",
        headers: getAuthHeaders(),
      });

      const payload = await response.json().catch(() => null);

      if (!response.ok) {
        const message = String(payload?.message || "Không thể tải lịch sử học tập.");
        throw new Error(message);
      }

      renderKpis(payload?.kpis || {});
    } catch (error) {
      setNotice(error?.message || "Không thể tải lịch sử học tập.");
    }
  };

  const loadActivities = async () => {
    setNotice("");
    isLoadingActivities = true;
    renderLoadingSkeleton();
    updatePaginationMeta();

    try {
      const query = new URLSearchParams({
        filter: activeFilter,
        page: String(page),
        pageSize: String(pageSize),
      });

      const response = await fetch(`/api/dashboard/history-activities?${query.toString()}`, {
        method: "GET",
        cache: "no-store",
        headers: getAuthHeaders(),
      });

      const payload = await response.json().catch(() => null);

      if (!response.ok) {
        const message = String(payload?.message || "Không thể tải lịch sử học tập.");
        throw new Error(message);
      }

      allActivities = Array.isArray(payload?.items) ? payload.items : [];
      totalItems = Number(payload?.totalItems || allActivities.length);
      totalPages = Math.max(1, Number(payload?.totalPages || 1));
      page = Math.min(page, totalPages);
      renderTimeline();
      syncQueryState();

      if (shouldRestoreScroll) {
        restoreScrollState();
        shouldRestoreScroll = false;
      } else if (shouldScrollToTimeline) {
        scrollToTimelineTop();
      }

      shouldScrollToTimeline = false;
    } catch (error) {
      allActivities = [];
      totalItems = 0;
      totalPages = 1;
      renderTimeline();
      setNotice(error?.message || "Không thể tải lịch sử học tập.");
      shouldScrollToTimeline = false;
    } finally {
      isLoadingActivities = false;
      updatePaginationMeta();
    }
  };

  const bindFilters = () => {
    if (!filterGroup) {
      return;
    }

    filterGroup.addEventListener("click", (event) => {
      const target = event.target instanceof Element
        ? event.target.closest("[data-filter]")
        : null;

      if (!target) {
        return;
      }

      const nextFilter = String(target.getAttribute("data-filter") || "all");
      activeFilter = nextFilter;
      page = 1;
      shouldScrollToTimeline = true;
      applyFilterButtons();
      loadActivities();
    });
  };

  const bindPagination = () => {
    prevPageButton?.addEventListener("click", () => {
      if (page <= 1 || isLoadingActivities) {
        return;
      }

      page -= 1;
      shouldScrollToTimeline = true;
      loadActivities();
    });

    nextPageButton?.addEventListener("click", () => {
      if (page >= totalPages || isLoadingActivities) {
        return;
      }

      page += 1;
      shouldScrollToTimeline = true;
      loadActivities();
    });

    pageSizeSelect?.addEventListener("change", () => {
      const nextSize = toSafeInt(pageSizeSelect.value, 20);
      pageSize = allowedPageSizes.has(nextSize) ? nextSize : 20;
      page = 1;
      shouldScrollToTimeline = true;
      loadActivities();
    });
  };

  const boot = async () => {
    const me = await window.AuthClient?.requireAuth?.();
    if (!me) {
      return;
    }

    initStateFromQuery();
    window.AuthClient?.bindUserUi?.(me);
    bindFilters();
    bindPagination();
    bindScrollPersistence();
    applyFilterButtons();
    loadOverview();
    loadActivities();
  };

  document.addEventListener("ajax:before-swap", disposePage, { once: true });
  boot();
})();
