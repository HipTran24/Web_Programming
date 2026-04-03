(function () {
  const tableBody = document.getElementById("contentListBody");
  if (!tableBody) {
    return;
  }

  const searchInput = document.getElementById("contentSearchInput");
  const sourceTypeSelect = document.getElementById("contentSourceTypeSelect");
  const sortSelect = document.getElementById("contentSortSelect");
  const metaText = document.getElementById("contentListMetaText");
  const notice = document.getElementById("contentListNotice");
  const prevPageButton = document.getElementById("contentPrevPageButton");
  const nextPageButton = document.getElementById("contentNextPageButton");
  const pageMetaText = document.getElementById("contentPageMetaText");
  const pageSizeSelect = document.getElementById("contentPageSizeSelect");

  const state = {
    loading: false,
    debounceTimer: null,
    items: [],
    totalItems: 0,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  };

  const scrollStateKey = "content-list:scroll-state";
  let shouldRestoreScroll = true;
  let shouldScrollToList = false;
  let scrollListener = null;
  let pageHideListener = null;
  let beforeUnloadListener = null;

  const sourceTypeLabels = {
    FileUpload: "Tệp tải lên",
    TextUrl: "URL văn bản",
    DocumentUrl: "URL tài liệu",
    VideoUrl: "URL video",
  };

  const allowedPageSizes = new Set([10, 20, 50]);

  const toSafeInt = (value, fallback) => {
    const parsed = Number.parseInt(String(value || ""), 10);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      return fallback;
    }

    return parsed;
  };

  const initStateFromQuery = () => {
    const params = new URLSearchParams(window.location.search);
    state.page = toSafeInt(params.get("page"), 1);

    const requestedPageSize = toSafeInt(params.get("pageSize"), state.pageSize);
    state.pageSize = allowedPageSizes.has(requestedPageSize) ? requestedPageSize : 20;

    const query = String(params.get("query") || "").trim();
    const sourceType = String(params.get("sourceType") || "all").trim();
    const sort = String(params.get("sort") || "latest").trim().toLowerCase();

    if (searchInput) {
      searchInput.value = query;
    }

    if (sourceTypeSelect) {
      const canApplySourceType = Array.from(sourceTypeSelect.options).some((opt) => opt.value === sourceType);
      sourceTypeSelect.value = canApplySourceType ? sourceType : "all";
    }

    if (sortSelect) {
      sortSelect.value = sort === "oldest" ? "oldest" : "latest";
    }

    if (pageSizeSelect) {
      pageSizeSelect.value = String(state.pageSize);
    }
  };

  const syncQueryState = () => {
    const params = new URLSearchParams();

    const query = String(searchInput?.value || "").trim();
    const sourceType = String(sourceTypeSelect?.value || "all");
    const sort = String(sortSelect?.value || "latest");

    if (query) {
      params.set("query", query);
    }

    if (sourceType !== "all") {
      params.set("sourceType", sourceType);
    }

    if (sort !== "latest") {
      params.set("sort", sort);
    }

    if (state.page > 1) {
      params.set("page", String(state.page));
    }

    if (state.pageSize !== 20) {
      params.set("pageSize", String(state.pageSize));
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

  const scrollToListTop = () => {
    const container = tableBody.closest(".glass");
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

  const toLocalDateTime = (value) => {
    if (!value) {
      return "-";
    }

    const raw = String(value).trim();
    const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw);
    const normalized = hasTimezone ? raw : `${raw}Z`;
    const date = new Date(normalized);
    if (Number.isNaN(date.getTime())) {
      return "-";
    }

    return new Intl.DateTimeFormat("vi-VN", {
      dateStyle: "medium",
      timeStyle: "short",
      timeZone: "Asia/Ho_Chi_Minh",
    }).format(date);
  };

  const getAuthHeaders = () => {
    const token = window.AuthClient?.getAccessToken?.() || "";
    return token ? { Authorization: `Bearer ${token}` } : {};
  };

  const setNotice = (message, type) => {
    if (!notice) {
      return;
    }

    if (!message) {
      notice.className = "alert d-none mt-3 mb-0";
      notice.textContent = "";
      return;
    }

    notice.className = `alert ${type === "error" ? "alert-danger" : "alert-success"} mt-3 mb-0`;
    notice.textContent = message;
  };

  const setMeta = () => {
    if (!metaText) {
      return;
    }

    metaText.textContent = `${state.totalItems} nội dung • Trang ${state.page}/${state.totalPages}`;

    if (pageMetaText) {
      pageMetaText.textContent = `Trang ${state.page} / ${state.totalPages}`;
    }

    if (prevPageButton) {
      prevPageButton.disabled = state.loading || state.page <= 1;
    }

    if (nextPageButton) {
      nextPageButton.disabled = state.loading || state.page >= state.totalPages;
    }
  };

  const renderLoadingSkeletonRows = (count = 6) => {
    const rows = Array.from({ length: count })
      .map(() => `
        <tr>
          <td colspan="5" class="py-2">
            <div class="list-skeleton-line list-skeleton-line--wide"></div>
            <div class="list-skeleton-line list-skeleton-line--short mt-2"></div>
          </td>
        </tr>
      `)
      .join("");

    tableBody.innerHTML = rows;
  };

  const buildQueryString = () => {
    const params = new URLSearchParams();
    params.set("page", String(state.page));
    params.set("pageSize", String(state.pageSize));
    params.set("sort", String(sortSelect?.value || "latest"));

    const sourceType = String(sourceTypeSelect?.value || "all");
    if (sourceType && sourceType !== "all") {
      params.set("sourceType", sourceType);
    }

    const query = String(searchInput?.value || "").trim();
    if (query) {
      params.set("query", query);
    }

    return params.toString();
  };

  const renderRows = () => {
    if (!Array.isArray(state.items) || state.items.length === 0) {
      tableBody.innerHTML = `
        <tr>
          <td colspan="5" class="text-center text-muted-2 py-4">Chưa có nội dung phù hợp bộ lọc hiện tại.</td>
        </tr>
      `;
      return;
    }

    const rows = state.items.map((item) => {
        const id = Number(item?.contentId || 0);
        const sourceType = String(item?.sourceType || "");
        const sourceLabel = sourceTypeLabels[sourceType] || sourceType || "Khác";
        const fileName = escapeHtml(item?.fileName || "(không có tên)");
        const fileType = escapeHtml(item?.fileType || "-");
        const createdAt = escapeHtml(toLocalDateTime(item?.createdAt));
        const hasAi = Boolean(item?.hasAiProcess);
        const moderationStatus = String(item?.moderation?.status || "");
        const statusText = moderationStatus === "Pending"
          ? "Chờ admin duyệt"
          : moderationStatus === "Rejected"
            ? "Bị từ chối"
            : hasAi
              ? "Hoàn tất"
              : String(item?.fetchStatus || "Đang xử lý");
        const statusClass = moderationStatus === "Pending"
          ? "text-bg-warning"
          : moderationStatus === "Rejected"
            ? "text-bg-danger"
            : hasAi
              ? "text-bg-success"
              : "text-bg-warning";

        return `
          <tr>
            <td>
              <div class="fw-semibold">${fileName}</div>
              <div class="small text-muted-2">${escapeHtml(sourceLabel)}</div>
            </td>
            <td><span class="badge badge-soft">${fileType}</span></td>
            <td class="text-muted-2">${createdAt}</td>
            <td><span class="badge ${statusClass}">${escapeHtml(statusText)}</span></td>
            <td class="text-end">
              <a class="btn btn-outline-light btn-sm" href="content-detail.html?contentId=${id}">Xem</a>
              <a class="btn btn-soft btn-sm" href="quiz.html?contentId=${id}">${moderationStatus === "Pending" ? "Chờ duyệt" : moderationStatus === "Rejected" ? "Đã chặn" : "Làm bài"}</a>
              <button class="btn btn-danger btn-sm" data-action="delete" data-content-id="${id}">Xoá</button>
            </td>
          </tr>
        `;
      });

    tableBody.innerHTML = '<tr><td colspan="5" class="text-center text-muted-2 py-3">Đang dựng danh sách...</td></tr>';

    const chunkSize = 20;
    let cursor = 0;
    tableBody.innerHTML = "";

    const renderNextChunk = () => {
      const chunk = rows.slice(cursor, cursor + chunkSize).join("");
      tableBody.insertAdjacentHTML("beforeend", chunk);
      cursor += chunkSize;

      if (cursor < rows.length) {
        window.requestAnimationFrame(renderNextChunk);
      }
    };

    window.requestAnimationFrame(renderNextChunk);
  };

  const loadContents = async () => {
    if (state.loading) {
      return;
    }

    state.loading = true;
    setNotice("", "success");
    renderLoadingSkeletonRows();
    setMeta();

    try {
      const queryString = buildQueryString();
      const response = await fetch(`/api/contents?${queryString}`, {
        method: "GET",
        headers: getAuthHeaders(),
      });

      if (response.status === 401) {
        setNotice("Bạn cần đăng nhập để xem nội dung.", "error");
        state.items = [];
        state.totalItems = 0;
        renderRows();
        setMeta();
        return;
      }

      const data = await response.json().catch(() => null);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không tải được danh sách nội dung.");
      }

      state.items = Array.isArray(data?.items) ? data.items : [];
      state.totalItems = Number(data?.totalItems || state.items.length || 0);
      state.totalPages = Math.max(1, Number(data?.totalPages || 1));
      state.page = Math.min(state.page, state.totalPages);
      renderRows();
      setMeta();
      syncQueryState();

      if (shouldRestoreScroll) {
        restoreScrollState();
        shouldRestoreScroll = false;
      } else if (shouldScrollToList) {
        scrollToListTop();
      }

      shouldScrollToList = false;
    } catch (error) {
      const message = error instanceof Error ? error.message : "Có lỗi khi tải danh sách nội dung.";
      setNotice(message, "error");
      state.items = [];
      state.totalItems = 0;
      state.totalPages = 1;
      renderRows();
      setMeta();
      shouldScrollToList = false;
    } finally {
      state.loading = false;
    }
  };

  const deleteContent = async (contentId) => {
    const id = Number(contentId || 0);
    if (id <= 0) {
      return;
    }

    const ok = window.UiDialog?.confirmDanger
      ? await window.UiDialog.confirmDanger({
        title: "Xóa nội dung",
        message: "Bạn có chắc muốn xóa nội dung này không? Hành động này không thể hoàn tác.",
        confirmText: "Xóa ngay",
        cancelText: "Giữ lại",
      })
      : window.confirm("Bạn có chắc muốn xoá nội dung này không?");
    if (!ok) {
      return;
    }

    try {
      const response = await fetch(`/api/contents/${id}`, {
        method: "DELETE",
        headers: getAuthHeaders(),
      });

      const data = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(data?.message || "Không thể xoá nội dung.");
      }

      setNotice("Đã xoá nội dung thành công.", "success");
      await loadContents();
    } catch (error) {
      setNotice(error instanceof Error ? error.message : "Không thể xoá nội dung.", "error");
    }
  };

  searchInput?.addEventListener("input", () => {
    if (state.debounceTimer) {
      window.clearTimeout(state.debounceTimer);
    }

    state.debounceTimer = window.setTimeout(() => {
      state.page = 1;
      shouldScrollToList = true;
      loadContents();
    }, 300);
  });

  sourceTypeSelect?.addEventListener("change", () => {
    state.page = 1;
    shouldScrollToList = true;
    loadContents();
  });

  sortSelect?.addEventListener("change", () => {
    state.page = 1;
    shouldScrollToList = true;
    loadContents();
  });

  prevPageButton?.addEventListener("click", () => {
    if (state.page <= 1 || state.loading) {
      return;
    }

    state.page -= 1;
    shouldScrollToList = true;
    loadContents();
  });

  nextPageButton?.addEventListener("click", () => {
    if (state.page >= state.totalPages || state.loading) {
      return;
    }

    state.page += 1;
    shouldScrollToList = true;
    loadContents();
  });

  pageSizeSelect?.addEventListener("change", () => {
    const nextSize = toSafeInt(pageSizeSelect.value, 20);
    state.pageSize = allowedPageSizes.has(nextSize) ? nextSize : 20;
    state.page = 1;
    shouldScrollToList = true;
    loadContents();
  });

  tableBody.addEventListener("click", (event) => {
    const target = event.target instanceof Element
      ? event.target.closest("[data-action='delete']")
      : null;

    if (!target) {
      return;
    }

    const contentId = target.getAttribute("data-content-id");
    deleteContent(contentId);
  });

  initStateFromQuery();
  bindScrollPersistence();
  loadContents();
  document.addEventListener("ajax:before-swap", disposePage, { once: true });
})();
