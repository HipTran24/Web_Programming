(function () {
  const tableBody = document.getElementById("historyTableBody");
  if (!tableBody) {
    return;
  }

  const searchInput = document.getElementById("historySearchInput");
  const sourceTypeSelect = document.getElementById("historySourceTypeSelect");
  const sortSelect = document.getElementById("historySortSelect");
  const refreshButton = document.getElementById("historyRefreshButton");
  const metaText = document.getElementById("historyMetaText");
  const notice = document.getElementById("historyNotice");

  const kpiTotalUploads = document.getElementById("kpiTotalUploads");
  const kpiFileUploads = document.getElementById("kpiFileUploads");
  const kpiUrlUploads = document.getElementById("kpiUrlUploads");
  const kpiAiCompleted = document.getElementById("kpiAiCompleted");

  const state = {
    pageSize: 1000,
    totalItems: 0,
    loading: false,
    searchTimer: null,
  };

  const sourceTypeLabels = {
    FileUpload: "Tệp tải lên",
    TextUrl: "URL văn bản",
    DocumentUrl: "URL tài liệu",
    VideoUrl: "URL video",
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

  const normalizeDisplayName = (fileName) => {
    const raw = String(fileName || "").trim();
    if (!raw) {
      return "(không có tên)";
    }

    let decoded = raw;
    try {
      decoded = decodeURIComponent(raw);
    } catch {
      decoded = raw;
    }

    decoded = decoded.replace(/\+/g, " ");
    const withSpaces = decoded.replaceAll("_", " ").replaceAll("-", " ").replace(/\s+/g, " ").trim();
    return withSpaces || decoded;
  };

  const stripFileExtension = (value) => {
    const text = String(value || "").trim();
    if (!text) {
      return text;
    }

    return text.replace(/\.[a-z0-9]{1,8}$/i, "").trim();
  };

  const isGenericName = (value) => {
    const normalized = String(value || "").toLowerCase().trim();
    return ["inline text", "inline-text", "inline text txt", "inline-text.txt", "untitled", "khong co ten", "(không có tên)"]
      .some((item) => normalized.includes(item));
  };

  const buildDisplayName = (rawName, summaryText) => {
    const normalizedName = normalizeDisplayName(rawName);
    const baseName = stripFileExtension(normalizedName);
    if (baseName && !isGenericName(baseName)) {
      return baseName;
    }

    const summary = String(summaryText || "").trim();
    if (!summary) {
      return baseName || normalizedName || "Nội dung tải lên";
    }

    const plain = summary.replace(/\s+/g, " ").trim();
    const sentenceHead = plain.split(/[.!?]/)[0].trim();
    const shortHead = sentenceHead.split(" ").slice(0, 10).join(" ").trim();
    return shortHead || "Nội dung tải lên";
  };

  const sourceBadgeClass = (sourceType) => {
    switch (sourceType) {
      case "FileUpload":
        return "history-source-badge history-source-badge--file";
      case "VideoUrl":
        return "history-source-badge history-source-badge--video";
      default:
        return "history-source-badge history-source-badge--url";
    }
  };

  const aiBadgeClass = (item) => {
    if (item.hasAiProcess) {
      return "history-ai-badge history-ai-badge--success";
    }
    if (item.fetchStatus && String(item.fetchStatus).toLowerCase() === "failed") {
      return "history-ai-badge history-ai-badge--failed";
    }
    return "history-ai-badge history-ai-badge--pending";
  };

  const aiBadgeText = (item) => {
    if (item.hasAiProcess) {
      return "Hoàn tất";
    }
    if (item.fetchStatus && String(item.fetchStatus).toLowerCase() === "failed") {
      return "Thất bại";
    }
    return "Đang xử lý";
  };

  const setNotice = (message, type) => {
    if (!notice) {
      return;
    }

    if (!message) {
      notice.className = "alert d-none mb-3";
      notice.textContent = "";
      return;
    }

    notice.className = `alert ${type === "error" ? "alert-danger" : "alert-success"} mb-3`;
    notice.textContent = message;
  };

  const setLoading = (value) => {
    state.loading = value;
    if (refreshButton) {
      refreshButton.disabled = value;
      refreshButton.textContent = value ? "Đang tải..." : "Làm mới";
    }
  };

  const getAuthHeaders = () => {
    const headers = { "Content-Type": "application/json" };
    const token = window.AuthClient?.getAccessToken?.() || "";
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    return headers;
  };

  const buildQueryString = () => {
    const params = new URLSearchParams();
    params.set("page", "1");
    params.set("pageSize", String(state.pageSize));
    params.set("sort", String(sortSelect?.value || "latest"));

    const sourceType = String(sourceTypeSelect?.value || "all");
    if (sourceType && sourceType !== "all") {
      params.set("sourceType", sourceType);
    }

    const search = String(searchInput?.value || "").trim();
    if (search) {
      params.set("query", search);
    }

    return params.toString();
  };

  const renderKpis = (stats) => {
    if (kpiTotalUploads) {
      kpiTotalUploads.textContent = String(stats?.totalUploads || 0);
    }

    if (kpiFileUploads) {
      kpiFileUploads.textContent = String(stats?.fileUploads || 0);
    }

    if (kpiUrlUploads) {
      kpiUrlUploads.textContent = String(stats?.urlUploads || 0);
    }

    if (kpiAiCompleted) {
      kpiAiCompleted.textContent = String(stats?.aiCompleted || 0);
    }
  };

  const renderRows = (items) => {
    if (!Array.isArray(items) || items.length === 0) {
      tableBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted-2 py-5">Chưa có dữ liệu upload cho bộ lọc hiện tại.</td></tr>';
      return;
    }

    tableBody.innerHTML = items
      .map((item) => {
        const summary = String(item.summaryPreview || "").trim();
        const rawName = item.sourceType === "FileUpload"
          ? String(item.filePath || item.fileName || "")
          : String(item.fileName || "");
        const name = escapeHtml(buildDisplayName(rawName, summary));
        const sourceType = String(item.sourceType || "");
        const sourceLabel = sourceTypeLabels[sourceType] || sourceType || "Khác";
        const summaryMarkup = summary
          ? `<div class="history-summary mt-1">${escapeHtml(summary)}</div>`
          : '<div class="history-summary mt-1 text-muted-2">Chưa có tóm tắt AI.</div>';

        return `
          <tr>
            <td>
              <div class="history-file-name">${name}</div>
              ${summaryMarkup}
            </td>
            <td>
              <span class="${sourceBadgeClass(sourceType)}">${escapeHtml(sourceLabel)}</span>
            </td>
            <td>
              <span class="${aiBadgeClass(item)}">${aiBadgeText(item)}</span>
            </td>
            <td class="text-muted-2">${toLocalDateTime(item.createdAt)}</td>
          </tr>
        `;
      })
      .join("");
  };

  const renderMeta = () => {
    if (!metaText) {
      return;
    }

    metaText.textContent = `${state.totalItems} bản ghi`; 
  };

  const loadHistory = async () => {
    setLoading(true);
    setNotice("", "success");

    try {
      const queryString = buildQueryString();
      const response = await fetch(`/api/summary/upload-history?${queryString}`, {
        method: "GET",
        headers: getAuthHeaders(),
      });

      if (response.status === 401) {
        setNotice("Bạn cần đăng nhập để xem lịch sử upload.", "error");
        tableBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted-2 py-5">Vui lòng đăng nhập để tiếp tục.</td></tr>';
        return;
      }

      const data = await response.json().catch(() => null);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không tải được lịch sử upload.");
      }

      state.totalItems = Number(data.totalItems || 0);

      renderKpis(data.stats || {});
      renderRows(data.items || []);
      renderMeta();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Có lỗi khi tải lịch sử.";
      setNotice(message, "error");
      tableBody.innerHTML = '<tr><td colspan="4" class="text-center text-danger py-5">Không tải được dữ liệu. Thử lại sau.</td></tr>';
      state.totalItems = 0;
      renderMeta();
    } finally {
      setLoading(false);
    }
  };

  refreshButton?.addEventListener("click", () => {
    loadHistory();
  });

  sourceTypeSelect?.addEventListener("change", () => {
    loadHistory();
  });

  sortSelect?.addEventListener("change", () => {
    loadHistory();
  });

  searchInput?.addEventListener("input", () => {
    if (state.searchTimer) {
      window.clearTimeout(state.searchTimer);
    }

    state.searchTimer = window.setTimeout(() => {
      loadHistory();
    }, 350);
  });

  const boot = async () => {
    const me = await window.AuthClient?.requireAuth?.();
    if (!me) {
      return;
    }

    window.AuthClient?.bindUserUi?.(me);
    loadHistory();
  };

  boot();
})();
