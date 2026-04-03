(function () {
  const titleEl = document.getElementById("contentDetailTitle");
  if (!titleEl) {
    return;
  }

  const metaEl = document.getElementById("contentDetailMeta");
  const statusEl = document.getElementById("contentDetailStatus");
  const noticeEl = document.getElementById("contentDetailNotice");
  const summaryEl = document.getElementById("contentDetailSummary");
  const keyPointsEl = document.getElementById("contentDetailKeyPoints");
  const textAreaEl = document.getElementById("contentDetailTextArea");
  const downloadButton = document.getElementById("contentDetailDownloadButton");
  const deleteButton = document.getElementById("contentDetailDeleteButton");
  const quizButton = document.getElementById("contentDetailQuizButton");
  const aiTimeEl = document.getElementById("contentDetailAiTime");
  const quizCountEl = document.getElementById("contentDetailQuizCount");
  const sourceTypeEl = document.getElementById("contentDetailSourceType");

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

  const setNotice = (message, type) => {
    if (!noticeEl) {
      return;
    }

    if (!message) {
      noticeEl.className = "alert d-none mt-3 mb-0";
      noticeEl.textContent = "";
      return;
    }

    noticeEl.className = `alert ${type === "error" ? "alert-danger" : "alert-success"} mt-3 mb-0`;
    noticeEl.textContent = message;
  };

  const getAuthHeaders = () => {
    const token = window.AuthClient?.getAccessToken?.() || "";
    return token ? { Authorization: `Bearer ${token}` } : {};
  };

  const getContentId = () => {
    const params = new URLSearchParams(window.location.search);
    const raw = Number(params.get("contentId") || 0);
    return Number.isFinite(raw) && raw > 0 ? Math.trunc(raw) : 0;
  };

  const parseKeyPoints = (rawKeyPoints) => {
    const text = String(rawKeyPoints || "").trim();
    if (!text) {
      return [];
    }

    const decodeEscapedUnicode = (value) =>
      String(value || "")
        .replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) => String.fromCharCode(parseInt(hex, 16)))
        .replace(/\\n/g, "\n")
        .replace(/\\t/g, "\t")
        .replace(/\\r/g, "");

    const tryParseAsJsonArray = (value) => {
      const raw = String(value || "").trim();
      if (!raw.startsWith("[") || !raw.endsWith("]")) {
        return null;
      }

      try {
        const parsed = JSON.parse(raw);
        if (!Array.isArray(parsed)) {
          return null;
        }

        const normalized = parsed
          .map((item) => decodeEscapedUnicode(item).trim())
          .filter(Boolean)
          .map((line) => line.replace(/^[-*•]\s*/, ""));

        return normalized.length ? normalized : null;
      } catch {
        return null;
      }
    };

    const parsedJsonArray = tryParseAsJsonArray(text);
    if (parsedJsonArray) {
      return parsedJsonArray;
    }

    return decodeEscapedUnicode(text)
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean)
      .map((line) => line.replace(/^[-*•]\s*/, ""));
  };

  const normalizeVideoTagLabel = (tag) => {
    const normalized = String(tag || "").trim().toLowerCase();
    if (!normalized) {
      return "Thông tin";
    }

    if (normalized.includes("thuật ngữ") || normalized.includes("thuat ngu")) {
      return "Thuật ngữ";
    }

    if (normalized.includes("thông tin") || normalized.includes("thong tin") || normalized.includes("fact")) {
      return "Thông tin";
    }

    return "Kiến thức";
  };

  const parseTaggedPoint = (point) => {
    const raw = String(point || "").trim();
    const tagged = raw.match(/^\[(.+?)\]\s*(.+)$/);
    if (tagged) {
      return {
        tag: normalizeVideoTagLabel(tagged[1]),
        text: tagged[2] || "-",
      };
    }

    return {
      tag: "Kiến thức",
      text: raw || "-",
    };
  };

  const keypointTagClass = (tag) => {
    const normalized = String(tag || "").toLowerCase();
    if (normalized.includes("thông tin")) {
      return "upload-keypoint-tag upload-keypoint-tag--info";
    }

    return "upload-keypoint-tag upload-keypoint-tag--knowledge";
  };

  const applyStatus = (item) => {
    if (!statusEl) {
      return;
    }

    const moderationStatus = String(item?.moderation?.status || "");
    if (moderationStatus === "Pending") {
      statusEl.className = "badge text-bg-warning";
      statusEl.textContent = "Chờ admin duyệt";
      return;
    }

    if (moderationStatus === "Rejected") {
      statusEl.className = "badge text-bg-danger";
      statusEl.textContent = "Bị từ chối";
      return;
    }

    if (item?.aiProcess) {
      statusEl.className = "badge text-bg-success";
      statusEl.textContent = "Hoàn tất";
      return;
    }

    statusEl.className = "badge text-bg-warning";
    statusEl.textContent = String(item?.fetchStatus || "Đang xử lý");
  };

  const renderDetail = (item) => {
    const fileName = String(item?.fileName || "(không có tên)");
    const fileType = String(item?.fileType || "-");
    const sourceType = String(item?.sourceType || "");
    const sourceLabel = sourceTypeLabels[sourceType] || sourceType || "Khác";
    const createdAt = toLocalDateTime(item?.createdAt);
    const summary = String(item?.aiProcess?.summary || "").trim() || "Chưa có tóm tắt cho nội dung này.";
    const extractedText = String(item?.extractedText || "").trim() || "Chưa có nội dung trích xuất.";
    const keyPoints = parseKeyPoints(item?.aiProcess?.keyPoints);

    titleEl.textContent = fileName;
    if (metaEl) {
      metaEl.textContent = `Nguồn: ${fileType} • ${sourceLabel} • Tạo lúc ${createdAt}`;
    }

    applyStatus(item);

    if (summaryEl) {
      summaryEl.textContent = summary;
    }

    if (keyPointsEl) {
      keyPointsEl.innerHTML = keyPoints.length
        ? keyPoints
          .map((point) => {
            const parsed = parseTaggedPoint(point);
            return `<li class="upload-keypoint-item"><span class="${keypointTagClass(parsed.tag)}">${escapeHtml(parsed.tag)}</span><span class="upload-keypoint-text">${escapeHtml(parsed.text)}</span></li>`;
          })
          .join("")
        : "<li>Chưa có ý chính.</li>";
    }

    if (textAreaEl) {
      textAreaEl.value = extractedText;
    }

    if (aiTimeEl) {
      const processing = Number(item?.aiProcess?.processingTime || 0);
      aiTimeEl.textContent = processing > 0
        ? `Thời gian xử lý: ${processing.toFixed(2)} giây`
        : "Thời gian xử lý: -";
    }

    if (quizCountEl) {
      quizCountEl.textContent = `Số quiz đã tạo: ${Number(item?.quizCount || 0)}`;
    }

    if (sourceTypeEl) {
      sourceTypeEl.textContent = `Nguồn: ${sourceLabel}`;
    }

    if (quizButton) {
      const moderationStatus = String(item?.moderation?.status || "");
      const blocked = moderationStatus === "Pending" || moderationStatus === "Rejected";
      quizButton.href = blocked ? "#" : `quiz.html?contentId=${Number(item?.contentId || 0)}`;
      quizButton.classList.toggle("disabled", blocked);
      quizButton.setAttribute("aria-disabled", blocked ? "true" : "false");
    }

    const moderationStatus = String(item?.moderation?.status || "");
    if (moderationStatus === "Pending") {
      setNotice(item?.moderation?.reason || "Nội dung này đang chờ admin duyệt vì có dấu hiệu nhạy cảm.", "error");
    } else if (moderationStatus === "Rejected") {
      setNotice(item?.moderation?.reason || "Nội dung này đã bị từ chối sau kiểm duyệt.", "error");
    } else {
      setNotice("", "success");
    }

    if (downloadButton) {
      downloadButton.disabled = !String(extractedText || "").trim();
      downloadButton.onclick = () => {
        const blob = new Blob([extractedText], { type: "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = `${fileName}.txt`;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
      };
    }

    if (deleteButton) {
      deleteButton.onclick = async () => {
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
          const response = await fetch(`/api/contents/${item.contentId}`, {
            method: "DELETE",
            headers: getAuthHeaders(),
          });

          const data = await response.json().catch(() => null);
          if (!response.ok) {
            throw new Error(data?.message || "Không thể xoá nội dung.");
          }

          window.location.href = "history.html";
        } catch (error) {
          setNotice(error instanceof Error ? error.message : "Không thể xoá nội dung.", "error");
        }
      };
    }
  };

  const loadDetail = async () => {
    const contentId = getContentId();
    if (contentId <= 0) {
      setNotice("Thiếu contentId hợp lệ trong URL.", "error");
      return;
    }

    setNotice("", "success");

    try {
      const response = await fetch(`/api/contents/${contentId}`, {
        method: "GET",
        headers: getAuthHeaders(),
      });

      if (response.status === 401) {
        setNotice("Bạn cần đăng nhập để xem chi tiết nội dung.", "error");
        return;
      }

      const data = await response.json().catch(() => null);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không tải được chi tiết nội dung.");
      }

      renderDetail(data);
    } catch (error) {
      setNotice(error instanceof Error ? error.message : "Không tải được dữ liệu nội dung.", "error");
    }
  };

  loadDetail();
})();
