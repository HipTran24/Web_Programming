(function () {
  const guestQuizTokenKey = "quiz.guestToken";
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

  const quizConfigForm = document.getElementById("quizConfigForm");
  const quizQuestionCount = document.getElementById("quizQuestionCount");
  const quizDifficulty = document.getElementById("quizDifficulty");
  const generateQuizButton = document.getElementById("generateQuizButton");
  const reloadQuizButton = document.getElementById("reloadQuizButton");
  const quizMessage = document.getElementById("quizMessage");
  const quizFormBlock = document.getElementById("quizFormBlock");
  const quizMeta = document.getElementById("quizMeta");
  const quizQuestions = document.getElementById("quizQuestions");
  const submitQuizButton = document.getElementById("submitQuizButton");
  const quizResultModal = document.getElementById("quizResultModal");
  const quizScoreHeadline = document.getElementById("quizScoreHeadline");
  const quizScoreSubline = document.getElementById("quizScoreSubline");
  const quizWrongList = document.getElementById("quizWrongList");
  const quizResultModalInstance =
    quizResultModal && window.bootstrap?.Modal
      ? new window.bootstrap.Modal(quizResultModal)
      : null;

  let pending = false;
  let selectedFile = null;
  let latestContentId = 0;
  let generatingQuiz = false;
  let submittingQuiz = false;
  let currentQuiz = null;

  const escapeHtml = (text) =>
    String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

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

  const setMessage = (message, type) => {
    if (!formMessage) {
      return;
    }

    if (!message) {
      formMessage.className = "alert d-none mt-3 mb-0";
      formMessage.textContent = "";
      return;
    }

    formMessage.className = `alert ${type === "success" ? "alert-success" : "alert-danger"} mt-3 mb-0`;
    formMessage.textContent = message;
  };

  const setQuizMessage = (message, type) => {
    if (!quizMessage) {
      return;
    }

    if (!message) {
      quizMessage.className = "alert d-none mt-3 mb-0";
      quizMessage.textContent = "";
      return;
    }

    quizMessage.className = `alert ${type === "success" ? "alert-success" : "alert-danger"} mt-3 mb-0`;
    quizMessage.textContent = message;
  };

  const setPending = (value) => {
    pending = value;
    if (processButton) {
      processButton.disabled = value;
      processButton.textContent = value ? "Đang xử lý..." : "Bắt đầu xử lý AI";
    }
  };

  const setQuizButtonsState = () => {
    const canGenerate = latestContentId > 0 && !generatingQuiz;
    const hasQuiz = !!currentQuiz;

    if (generateQuizButton) {
      generateQuizButton.disabled = !canGenerate;
      generateQuizButton.textContent = generatingQuiz ? "Đang tạo đề..." : "Tạo bộ đề";
    }

    if (reloadQuizButton) {
      reloadQuizButton.disabled = !(canGenerate && hasQuiz);
    }

    if (submitQuizButton) {
      submitQuizButton.disabled = !(hasQuiz && !submittingQuiz);
      submitQuizButton.textContent = submittingQuiz ? "Đang chấm điểm..." : "Nộp bài & chấm điểm";
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
        const ext = selectedFile.name.includes(".") ? selectedFile.name.split(".").pop() : "file";
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

  const splitNameAndExtension = (value) => {
    const raw = String(value || "").trim();
    if (!raw) {
      return { base: "", ext: "" };
    }
    const lastDot = raw.lastIndexOf(".");
    if (lastDot <= 0 || lastDot === raw.length - 1) {
      return { base: raw, ext: "" };
    }
    return { base: raw.slice(0, lastDot), ext: raw.slice(lastDot + 1).toLowerCase() };
  };

  const titleCase = (value) =>
    String(value || "")
      .split(" ")
      .filter(Boolean)
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");

  const prettifyFileName = (name) => {
    const { base, ext } = splitNameAndExtension(name);
    if (!base && !ext) {
      return "(không có tên)";
    }

    const cleanedBase = titleCase(String(base).replaceAll("_", " ").replaceAll("-", " ").replace(/\s+/g, " ").trim());
    return ext ? `${cleanedBase}.${ext}` : cleanedBase;
  };

  const getSourceVisual = (rawSource, mode) => {
    if (mode === "url" || isLikelyUrl(rawSource)) {
      return { kind: "url", icon: "URL", badge: "link" };
    }
    const ext = splitNameAndExtension(rawSource).ext;
    if (["jpg", "jpeg", "png", "gif", "bmp", "webp"].includes(ext)) {
      return { kind: "image", icon: "IMG", badge: ext || "img" };
    }
    if (["mp4", "mov", "avi", "mkv", "webm", "m4v"].includes(ext)) {
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

  const normalizeSummaryPayload = (data) => {
    const summary = String(data?.summary || "").trim() || "Không có tóm tắt.";
    const keyPoints = Array.isArray(data?.keyPoints)
      ? data.keyPoints.map((point) => String(point || "").trim()).filter(Boolean)
      : [];
    return {
      summary,
      keyPoints: keyPoints.length ? keyPoints : ["Không có ý chính."],
    };
  };

  const resetQuizUi = () => {
    currentQuiz = null;
    if (quizFormBlock) {
      quizFormBlock.classList.add("d-none");
    }
    if (quizQuestions) {
      quizQuestions.innerHTML = "";
    }
    if (quizWrongList) {
      quizWrongList.innerHTML = "";
    }
    if (quizMeta) {
      quizMeta.textContent = "Chưa có đề.";
    }
  };

  const showResult = (data, mode) => {
    if (!resultPanel) {
      return;
    }
    if (resultPlaceholderBox) {
      resultPlaceholderBox.classList.add("d-none");
    }
    if (resultType) {
      resultType.textContent = data?.inputType || mode || "unknown";
    }

    const normalized = normalizeSummaryPayload(data);
    const rawSource = selectedFile?.name || data?.fileName || data?.url || "";
    const displayName = mode === "url" ? rawSource : prettifyFileName(rawSource);
    const visual = getSourceVisual(rawSource, mode);

    if (resultFileName) {
      resultFileName.textContent = displayName;
    }
    if (resultFileMeta) {
      resultFileMeta.textContent = mode === "url" ? "Nguồn URL đã xử lý" : "Tệp nguồn đã xử lý";
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
      resultKeyPoints.innerHTML = normalized.keyPoints.map((point) => `<li>${escapeHtml(point)}</li>`).join("");
    }

    latestContentId = Number(data?.contentId || 0);
    resetQuizUi();
    setQuizMessage("", "error");
    setQuizButtonsState();
  };

  const renderQuizForm = (quiz) => {
    if (!quizQuestions || !quizFormBlock) {
      return;
    }

    const questions = Array.isArray(quiz?.questions) ? quiz.questions : [];
    quizQuestions.innerHTML = questions
      .map((question, index) => {
        const qid = Number(question.questionId || 0);
        const options = [
          { key: "A", value: question.optionA },
          { key: "B", value: question.optionB },
          { key: "C", value: question.optionC },
          { key: "D", value: question.optionD },
        ];

        const optionMarkup = options
          .map((option) => `
            <label class="quiz-option">
              <input type="radio" name="quiz-q-${qid}" value="${option.key}" />
              <span>${option.key}. ${escapeHtml(option.value || "")}</span>
            </label>
          `)
          .join("");

        return `
          <article class="quiz-question-card">
            <h6 class="fw-semibold mb-2">Câu ${index + 1}: ${escapeHtml(question.questionText || "")}</h6>
            <div class="quiz-options-grid">${optionMarkup}</div>
          </article>
        `;
      })
      .join("");

    if (quizMeta) {
      quizMeta.textContent = `Quiz #${quiz.quizId} • ${questions.length} câu • Độ khó: ${quiz.difficulty || "medium"}`;
    }

    quizFormBlock.classList.remove("d-none");
  };

  const renderQuizResult = (result, submittedAnswers) => {
    if (!quizScoreHeadline || !quizScoreSubline || !quizWrongList) {
      return;
    }

    const totalQuestions = Number(result?.totalQuestions || 0);
    const correctCount = Number(result?.correctCount || 0);
    const wrongCount = Number(result?.wrongCount || 0);
    const numericScore = Number(result?.score || 0);
    const safeScore = Number.isFinite(numericScore) ? numericScore : 0;

    if (quizScoreHeadline) {
      quizScoreHeadline.textContent = `${correctCount}/${totalQuestions} • ${safeScore.toFixed(2)} điểm`;
    }
    if (quizScoreSubline) {
      quizScoreSubline.textContent = `Đúng ${correctCount} • Sai ${wrongCount}`;
    }

    const wrong = Array.isArray(result?.wrongQuestions) ? result.wrongQuestions : [];
    const wrongById = new Map(
      wrong.map((item) => [Number(item?.questionId || 0), item])
    );
    const answerByQuestionId = new Map(
      (Array.isArray(submittedAnswers) ? submittedAnswers : []).map((answer) => [
        Number(answer?.questionId || 0),
        String(answer?.selectedAnswer || ""),
      ])
    );

    const questions = Array.isArray(currentQuiz?.questions) ? currentQuiz.questions : [];
    if (quizWrongList) {
      quizWrongList.innerHTML = questions.length
        ? questions
          .map((question, index) => {
            const questionId = Number(question?.questionId || 0);
            const wrongDetail = wrongById.get(questionId);
            const selectedAnswer = answerByQuestionId.get(questionId) || "";
            const isWrong = !!wrongDetail;
            const statusText = isWrong ? "Sai" : "Đúng";

            return `
              <article class="quiz-review-item ${isWrong ? "quiz-review-item--wrong" : "quiz-review-item--correct"}">
                <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
                  <div class="quiz-wrong-title">Câu ${index + 1}: ${escapeHtml(question?.questionText || "")}</div>
                  <span class="quiz-review-badge ${isWrong ? "quiz-review-badge--wrong" : "quiz-review-badge--correct"}">${statusText}</span>
                </div>
                <div class="small text-muted-2">Bạn chọn: ${escapeHtml(selectedAnswer || "(bỏ trống)")}</div>
                ${isWrong
                  ? `<div class="small text-success-emphasis">Đáp án đúng: ${escapeHtml(wrongDetail.correctAnswer || "")}</div>
                     <div class="small text-muted-2 mt-1">Giải thích: ${escapeHtml(wrongDetail.explanation || "Không có giải thích.")}</div>`
                  : '<div class="small text-success-emphasis">Bạn trả lời đúng.</div>'}
              </article>
            `;
          })
          .join("")
        : '<div class="small text-muted-2">Không có dữ liệu chi tiết câu hỏi.</div>';
    }

    if (quizResultModalInstance) {
      quizResultModalInstance.show();
      return;
    }

    // Fallback for environments where Bootstrap modal is unavailable.
    setQuizMessage("Đã chấm điểm thành công. Không mở được popup, vui lòng kiểm tra Bootstrap bundle.", "success");
  };

  const collectQuizAnswers = () => {
    if (!currentQuiz?.questions) {
      return [];
    }

    return currentQuiz.questions.map((question) => {
      const qid = Number(question.questionId || 0);
      const checked = document.querySelector(`input[name="quiz-q-${qid}"]:checked`);
      return {
        questionId: qid,
        selectedAnswer: checked ? checked.value : "",
      };
    });
  };

  const getAuthHeaders = () => {
    const headers = { "Content-Type": "application/json" };
    const token = window.AuthClient?.getAccessToken?.() || "";
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    return headers;
  };

  const generateQuiz = async (useReloadNonce) => {
    if (latestContentId <= 0) {
      throw new Error("Bạn cần xử lý nội dung trước khi tạo đề trắc nghiệm.");
    }

    const count = Math.max(0, Math.min(30, Number(quizQuestionCount?.value || 10)));
    const difficulty = String(quizDifficulty?.value || "medium");
    const variationNonce = useReloadNonce ? `${Date.now()}-${Math.random().toString(36).slice(2, 8)}` : `${Date.now()}`;

    const response = await fetch("/api/quiz/generate", {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({
        contentId: latestContentId,
        totalQuestions: count,
        difficulty,
        variationNonce,
        guestToken: window.localStorage.getItem(guestQuizTokenKey) || "",
      }),
    });

    const data = await readJson(response);
    if (!response.ok) {
      throw new Error(data?.message || "Không thể tạo đề trắc nghiệm.");
    }

    if (data?.guestToken) {
      window.localStorage.setItem(guestQuizTokenKey, String(data.guestToken));
    }

    currentQuiz = data;
    renderQuizForm(data);
    if (quizResultModalInstance) {
      quizResultModalInstance.hide();
    }
    setQuizMessage("Đã tạo bộ đề mới thành công.", "success");
  };

  const submitQuiz = async () => {
    if (!currentQuiz?.quizId) {
      throw new Error("Chưa có đề để nộp bài.");
    }

    const submittedAnswers = collectQuizAnswers();

    const response = await fetch("/api/quiz/submit", {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({
        quizId: currentQuiz.quizId,
        guestToken: window.localStorage.getItem(guestQuizTokenKey) || "",
        answers: submittedAnswers,
      }),
    });

    const data = await readJson(response);
    if (!response.ok) {
      throw new Error(data?.message || "Không thể nộp bài.");
    }

    renderQuizResult(data, submittedAnswers);
    setQuizMessage("Đã chấm điểm thành công.", "success");
  };

  const submitFile = async () => {
    if (!selectedFile) {
      throw new Error("Vui lòng chọn file trước khi xử lý.");
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
      throw new Error("Vui lòng nhập văn bản hoặc URL.");
    }

    const response = await fetch("/api/summary/text", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text, sourceHint: "unified-input" }),
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
      throw new Error("Vui lòng nhập URL.");
    }

    const response = await fetch("/api/summary/from-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
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

  if (quizConfigForm) {
    quizConfigForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      if (generatingQuiz) {
        return;
      }

      generatingQuiz = true;
      setQuizButtonsState();
      setQuizMessage("", "error");
      try {
        await generateQuiz(false);
      } catch (error) {
        setQuizMessage(error instanceof Error ? error.message : "Không thể tạo đề.", "error");
      } finally {
        generatingQuiz = false;
        setQuizButtonsState();
      }
    });
  }

  if (reloadQuizButton) {
    reloadQuizButton.addEventListener("click", async () => {
      if (generatingQuiz) {
        return;
      }

      generatingQuiz = true;
      setQuizButtonsState();
      setQuizMessage("", "error");
      try {
        await generateQuiz(true);
      } catch (error) {
        setQuizMessage(error instanceof Error ? error.message : "Không thể reload đề.", "error");
      } finally {
        generatingQuiz = false;
        setQuizButtonsState();
      }
    });
  }

  if (submitQuizButton) {
    submitQuizButton.addEventListener("click", async () => {
      if (submittingQuiz) {
        return;
      }

      submittingQuiz = true;
      setQuizButtonsState();
      setQuizMessage("", "error");
      try {
        await submitQuiz();
      } catch (error) {
        setQuizMessage(error instanceof Error ? error.message : "Không thể nộp bài.", "error");
      } finally {
        submittingQuiz = false;
        setQuizButtonsState();
      }
    });
  }

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
  resetQuizUi();
  setQuizButtonsState();
})();
