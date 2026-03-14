(function () {
  const form = document.getElementById("uploadForm");
  if (!form) {
    return;
  }

  const dropzone = document.getElementById("unifiedDropzone");
  const unifiedInput = document.getElementById("unifiedInput");
  const fileInput = document.getElementById("fileInput");
  const pickFileButton = document.getElementById("pickFileButton");
  const clearFileButton = document.getElementById("clearFileButton");
  const selectedFileChip = document.getElementById("selectedFileChip");
  const selectedFileChipExt = document.getElementById("selectedFileChipExt");
  const selectedFileChipName = document.getElementById("selectedFileChipName");

  const processButton = document.getElementById("processButton");
  const formMessage = document.getElementById("formMessage");

  const resultType = document.getElementById("resultType");
  const resultPlaceholderBox = document.getElementById("resultPlaceholderBox");
  const resultPanel = document.getElementById("resultPanel");
  const resultFileName = document.getElementById("resultFileName");
  const resultFileMeta = document.getElementById("resultFileMeta");
  const resultFileIcon = document.getElementById("resultFileIcon");
  const resultFileBadge = document.getElementById("resultFileBadge");
  const resultSummary = document.getElementById("resultSummary");
  const resultKeyPoints = document.getElementById("resultKeyPoints");

  let pending = false;
  let selectedFile = null;

  const setMessage = (message, type) => {
    if (!formMessage) {
      return;
    }

    if (!message) {
      formMessage.className = "alert d-none mt-3 mb-0";
      formMessage.textContent = "";
      return;
    }

    const alertType = type === "success" ? "alert-success" : "alert-danger";
    formMessage.className = `alert ${alertType} mt-3 mb-0`;
    formMessage.textContent = message;
  };

  const setPending = (value) => {
    pending = value;

    if (processButton) {
      processButton.disabled = value;
      processButton.textContent = value ? "Đang xử lý..." : "Bắt đầu xử lý AI";
    }
  };

  const setSelectedFile = (file) => {
    selectedFile = file || null;

    if (selectedFileChip && selectedFileChipName && selectedFileChipExt) {
      if (!selectedFile) {
        selectedFileChip.classList.add("d-none");
        selectedFileChipName.textContent = "";
        selectedFileChipExt.textContent = "FILE";
      } else {
        const ext = selectedFile.name.includes(".")
          ? selectedFile.name.split(".").pop()
          : "file";
        selectedFileChipExt.textContent = String(ext || "file").toUpperCase();
        selectedFileChipName.textContent = selectedFile.name;
        selectedFileChip.classList.remove("d-none");
      }
    }

    if (clearFileButton) {
      clearFileButton.classList.toggle("d-none", !selectedFile);
    }
  };

  const isLikelyUrl = (value) => {
    const input = String(value || "").trim();
    if (!input || /\s/.test(input)) {
      return false;
    }

    return /^https?:\/\//i.test(input);
  };

  const escapeHtml = (text) => {
    return String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  };

  const splitNameAndExtension = (value) => {
    const raw = String(value || "").trim();
    if (!raw) {
      return { base: "", ext: "" };
    }

    const lastDot = raw.lastIndexOf(".");
    if (lastDot <= 0 || lastDot === raw.length - 1) {
      return { base: raw, ext: "" };
    }

    return {
      base: raw.slice(0, lastDot),
      ext: raw.slice(lastDot + 1).toLowerCase(),
    };
  };

  const titleCase = (value) => {
    return String(value || "")
      .split(" ")
      .filter(Boolean)
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");
  };

  const prettifyFileName = (name) => {
    const { base, ext } = splitNameAndExtension(name);
    if (!base && !ext) {
      return "(không có tên)";
    }

    const cleanedBase = titleCase(
      String(base)
        .replaceAll("_", " ")
        .replaceAll("-", " ")
        .replace(/\s+/g, " ")
        .trim()
    );

    return ext ? `${cleanedBase}.${ext}` : cleanedBase;
  };

  const hasVietnameseDiacritics = (value) => {
    return /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/i.test(
      String(value || "")
    );
  };

  const buildVietnameseNameFromSummary = (summary, ext) => {
    const text = String(summary || "")
      .replace(/\s+/g, " ")
      .trim();

    if (!text) {
      return "";
    }

    const words = text
      .split(" ")
      .map((word) => word.replace(/^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$/gu, ""))
      .filter(Boolean)
      .slice(0, 8);

    if (words.length < 3) {
      return "";
    }

    const base = titleCase(words.join(" "));
    return ext ? `${base}.${ext}` : base;
  };

  const getSourceVisual = (rawSource, mode) => {
    if (mode === "url" || isLikelyUrl(rawSource)) {
      return { kind: "url", icon: "URL", badge: "link" };
    }

    const source = String(rawSource || "");
    const ext = splitNameAndExtension(source).ext;
    const imageExt = ["jpg", "jpeg", "png", "gif", "bmp", "webp"];
    const videoExt = ["mp4", "mov", "avi", "mkv", "webm", "m4v"];

    if (imageExt.includes(ext)) {
      return { kind: "image", icon: "IMG", badge: ext || "img" };
    }

    if (videoExt.includes(ext)) {
      return { kind: "video", icon: "VID", badge: ext || "video" };
    }

    if (ext === "pdf") {
      return { kind: "file", icon: "PDF", badge: "pdf" };
    }

    if (ext === "docx" || ext === "doc") {
      return { kind: "file", icon: "DOC", badge: ext };
    }

    if (ext === "txt" || ext === "md") {
      return { kind: "file", icon: "TXT", badge: ext || "text" };
    }

    return { kind: "file", icon: "FILE", badge: ext || "file" };
  };

  const parseSummaryJson = (raw) => {
    const value = String(raw || "").trim();
    if (!value) {
      return null;
    }

    const candidates = [];
    candidates.push(value);

    const firstBrace = value.indexOf("{");
    const lastBrace = value.lastIndexOf("}");
    if (firstBrace >= 0 && lastBrace > firstBrace) {
      candidates.push(value.slice(firstBrace, lastBrace + 1));
    }

    for (const candidate of candidates) {
      try {
        const parsed = JSON.parse(candidate);
        if (parsed && typeof parsed === "object") {
          return parsed;
        }
      } catch {
        // ignore malformed candidate and try next one
      }
    }

    return null;
  };

  const buildFallbackKeyPoints = (summaryText) => {
    const sentences = String(summaryText || "")
      .split(/(?<=[.!?])\s+/)
      .map((sentence) => sentence.trim())
      .filter((sentence) => sentence.length > 0);

    return sentences.slice(0, 4);
  };

  const normalizeSummaryPayload = (data) => {
    const rawSummary = String(data?.summary || "").trim();
    const parsed = parseSummaryJson(rawSummary);

    let summary = rawSummary;
    let keyPoints = Array.isArray(data?.keyPoints)
      ? data.keyPoints.filter((point) => String(point || "").trim().length > 0)
      : [];

    if (parsed) {
      const parsedSummary = String(parsed.summary || "").trim();
      if (parsedSummary) {
        summary = parsedSummary;
      }

      if (keyPoints.length === 0 && Array.isArray(parsed.keyPoints)) {
        keyPoints = parsed.keyPoints
          .map((point) => String(point || "").trim())
          .filter((point) => point.length > 0);
      }
    }

    if (/^"?summary"?\s*:/i.test(summary)) {
      summary = summary.replace(/^"?summary"?\s*:\s*/i, "").trim();
    }

    if (summary.startsWith('"') && summary.endsWith('"')) {
      summary = summary.slice(1, -1).trim();
    }

    if (keyPoints.length === 0 && summary) {
      keyPoints = buildFallbackKeyPoints(summary);
    }

    return {
      summary: summary || "Không có tóm tắt.",
      keyPoints,
    };
  };

  const showResult = (data, mode) => {
    if (!resultPanel) {
      return;
    }

    if (resultPlaceholderBox) {
      resultPlaceholderBox.classList.add("d-none");
    }

    if (resultType) {
      const inputType = data?.inputType || mode || "unknown";
      resultType.textContent = inputType;
    }

    const normalized = normalizeSummaryPayload(data);
    const rawSource = selectedFile?.name || data?.fileName || data?.url || "";
    const prettyName = mode === "url" ? rawSource : prettifyFileName(rawSource);
    const sourceExt = splitNameAndExtension(rawSource).ext;
    const generatedVietnameseName = buildVietnameseNameFromSummary(normalized.summary, sourceExt);
    const displayName = !hasVietnameseDiacritics(rawSource) && generatedVietnameseName
      ? generatedVietnameseName
      : prettyName;
    const visual = getSourceVisual(rawSource, mode);

    if (resultFileName) {
      resultFileName.textContent = displayName;
    }

    if (resultFileMeta) {
      if (rawSource && rawSource !== prettyName) {
        resultFileMeta.textContent = `Tên gốc: ${rawSource}`;
      } else {
        resultFileMeta.textContent = mode === "url"
          ? "Nguồn URL đã xử lý"
          : "Tệp nguồn đã xử lý";
      }
    }

    if (resultFileIcon) {
      resultFileIcon.textContent = visual.icon;
      resultFileIcon.setAttribute("data-kind", visual.kind);
    }

    if (resultFileBadge) {
      resultFileBadge.textContent = visual.badge;
    }

    if (resultSummary) {
      resultSummary.textContent = normalized.summary;
    }

    if (resultKeyPoints) {
      const points = normalized.keyPoints;
      resultKeyPoints.innerHTML = points.length
        ? points.map((point) => `<li>${escapeHtml(point)}</li>`).join("")
        : "<li>Không có ý chính.</li>";
    }
  };

  const readJson = async (response) => {
    const contentType = response.headers.get("content-type") || "";
    if (!contentType.toLowerCase().includes("application/json")) {
      return null;
    }

    try {
      return await response.json();
    } catch {
      return null;
    }
  };

  const submitFile = async () => {
    if (!selectedFile) {
      setMessage("Vui lòng chọn file trước khi xử lý.", "error");
      return;
    }

    const formData = new FormData();
    formData.append("file", selectedFile);

    const response = await fetch("/api/summary/upload", {
      method: "POST",
      body: formData,
    });

    const data = await readJson(response);
    if (!response.ok) {
      throw new Error(data?.message || "Upload thất bại.");
    }

    showResult(data, "file");
    setMessage("Xử lý file thành công.", "success");
  };

  const submitText = async () => {
    const text = (unifiedInput?.value || "").trim();
    if (!text) {
      setMessage("Vui lòng nhập văn bản hoặc URL.", "error");
      return;
    }

    const response = await fetch("/api/summary/text", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        text,
        sourceHint: "unified-input",
      }),
    });

    const data = await readJson(response);
    if (!response.ok) {
      throw new Error(data?.message || "Xử lý văn bản thất bại.");
    }

    showResult(data, "text");
    setMessage("Xử lý văn bản thành công.", "success");
  };

  const submitUrl = async () => {
    const url = (unifiedInput?.value || "").trim();
    if (!url) {
      setMessage("Vui lòng nhập URL.", "error");
      return;
    }

    const response = await fetch("/api/summary/from-url", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ url }),
    });

    const data = await readJson(response);
    if (!response.ok) {
      throw new Error(data?.message || "Xử lý URL thất bại.");
    }

    showResult(data, "url");
    setMessage("Xử lý URL thành công.", "success");
  };

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (pending) {
      return;
    }

    setPending(true);
    setMessage("", "error");

    try {
      if (selectedFile) {
        await submitFile();
      } else {
        const value = (unifiedInput?.value || "").trim();
        if (isLikelyUrl(value)) {
          await submitUrl();
        } else {
          await submitText();
        }
      }
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không thể xử lý yêu cầu.", "error");
    } finally {
      setPending(false);
    }
  });

  if (pickFileButton && fileInput) {
    pickFileButton.addEventListener("click", () => fileInput.click());
  }

  if (fileInput) {
    fileInput.addEventListener("change", () => {
      const file = fileInput.files && fileInput.files.length > 0 ? fileInput.files[0] : null;
      setSelectedFile(file);
    });
  }

  if (clearFileButton && fileInput) {
    clearFileButton.addEventListener("click", () => {
      fileInput.value = "";
      setSelectedFile(null);
    });
  }

  if (dropzone) {
    dropzone.addEventListener("dragover", (event) => {
      event.preventDefault();
      dropzone.classList.add("border", "border-primary");
    });

    dropzone.addEventListener("dragleave", () => {
      dropzone.classList.remove("border", "border-primary");
    });

    dropzone.addEventListener("drop", (event) => {
      event.preventDefault();
      dropzone.classList.remove("border", "border-primary");

      const files = event.dataTransfer && event.dataTransfer.files ? event.dataTransfer.files : null;
      if (files && files.length > 0) {
        setSelectedFile(files[0]);
        if (fileInput) {
          fileInput.files = files;
        }
      }
    });
  }

  if (unifiedInput) {
    unifiedInput.addEventListener("paste", (event) => {
      const clipboard = event.clipboardData;
      if (!clipboard || !clipboard.files || clipboard.files.length === 0) {
        return;
      }

      const file = clipboard.files[0];
      if (!file) {
        return;
      }

      event.preventDefault();
      setSelectedFile(file);
      if (fileInput) {
        const transfer = new DataTransfer();
        transfer.items.add(file);
        fileInput.files = transfer.files;
      }
    });
  }

  setSelectedFile(null);
})();
