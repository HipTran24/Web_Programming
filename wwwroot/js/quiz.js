(function () {
  const guestQuizTokenKey = "quiz.guestToken";
  const guestTokenHeaderName = "X-Guest-Token";
  const latestQuizResultStorageKey = "quiz.latestResult.v1";

  const notice = document.getElementById("quizWorkspaceNotice");
  const subline = document.getElementById("quizWorkspaceSubline");
  const contentSelect = document.getElementById("quizContentSelect");
  const questionCountInput = document.getElementById("quizQuestionCountInput");
  const difficultySelect = document.getElementById("quizDifficultySelect");
  const generateButton = document.getElementById("quizGenerateButton");
  const reloadButton = document.getElementById("quizReloadButton");
  const submitButton = document.getElementById("quizSubmitButton");
  const questionSection = document.getElementById("quizQuestionSection");
  const questionMeta = document.getElementById("quizQuestionMeta");
  const questionList = document.getElementById("quizQuestionList");
  const navigator = document.getElementById("quizNavigator");
  const resultModalElement = document.getElementById("quizResultModal");
  const resultHeadline = document.getElementById("quizResultHeadline");
  const resultSubline = document.getElementById("quizResultSubline");
  const reviewList = document.getElementById("quizReviewList");
  const resultModalInstance =
    resultModalElement && window.bootstrap?.Modal
      ? new window.bootstrap.Modal(resultModalElement)
      : null;

  if (!contentSelect || !questionCountInput || !generateButton || !questionList) {
    return;
  }

  const state = {
    loadingContents: false,
    generating: false,
    submitting: false,
    quiz: null,
    contentItems: [],
  };

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

  const getAuthToken = () => window.AuthClient?.getAccessToken?.() || "";
  const isAuthenticated = () => !!(window.AuthClient?.isAuthenticated?.() && getAuthToken());

  const getAuthHeaders = (json) => {
    const headers = {};
    if (json) {
      headers["Content-Type"] = "application/json";
    }

    const token = getAuthToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    return headers;
  };

  const createGuestToken = () => {
    if (window.crypto?.randomUUID) {
      return window.crypto.randomUUID().replaceAll("-", "");
    }

    return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 12)}`;
  };

  const ensureGuestToken = () => {
    const existing = window.localStorage.getItem(guestQuizTokenKey) || "";
    if (existing) {
      return existing;
    }

    const created = createGuestToken();
    window.localStorage.setItem(guestQuizTokenKey, created);
    return created;
  };

  const buildGuestHeaders = () => {
    if (isAuthenticated()) {
      return {};
    }

    return { [guestTokenHeaderName]: ensureGuestToken() };
  };

  const syncGuestTokenFromResponse = (response) => {
    if (isAuthenticated() || !response?.headers) {
      return;
    }

    const token = String(response.headers.get(guestTokenHeaderName) || "").trim();
    if (token) {
      window.localStorage.setItem(guestQuizTokenKey, token);
    }
  };

  const getCount = () => {
    const numeric = Number(questionCountInput.value || 10);
    const normalized = Number.isFinite(numeric) ? Math.trunc(numeric) : 10;
    const bounded = Math.max(0, Math.min(30, normalized));
    questionCountInput.value = String(bounded);
    return bounded;
  };

  const getContentIdFromQuery = () => {
    const params = new URLSearchParams(window.location.search);
    const value = Number(params.get("contentId") || 0);
    return Number.isFinite(value) && value > 0 ? Math.trunc(value) : 0;
  };

  const normalizeFileName = (value) => {
    const raw = String(value || "").trim();
    return raw || "(không có tên)";
  };

  const setButtons = () => {
    generateButton.disabled = state.loadingContents || state.generating || state.contentItems.length === 0;
    generateButton.textContent = state.generating ? "Đang tạo đề..." : "Tạo bộ đề";

    if (reloadButton) {
      reloadButton.disabled = state.loadingContents || state.generating || !state.quiz;
    }

    if (submitButton) {
      submitButton.disabled = state.submitting || !state.quiz;
      submitButton.textContent = state.submitting ? "Đang chấm điểm..." : "Nộp bài & chấm điểm";
    }
  };

  const renderNavigator = (questions) => {
    if (!navigator) {
      return;
    }

    navigator.innerHTML = (questions || [])
      .map((_, index) => {
        const questionIndex = index + 1;
        return `<button type="button" class="btn btn-outline-light btn-sm" data-nav-index="${questionIndex}">${questionIndex}</button>`;
      })
      .join("");
  };

  const renderQuiz = (quiz) => {
    state.quiz = quiz;
    const questions = Array.isArray(quiz?.questions) ? quiz.questions : [];

    if (questionMeta) {
      questionMeta.textContent = `${questions.length} câu • Độ khó: ${quiz?.difficulty || "medium"}`;
    }

    questionList.innerHTML = questions
      .map((question, index) => {
        const qid = Number(question?.questionId || 0);
        const options = [
          { key: "A", value: question?.optionA },
          { key: "B", value: question?.optionB },
          { key: "C", value: question?.optionC },
          { key: "D", value: question?.optionD },
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
          <article id="quiz-question-${index + 1}" class="quiz-question-card">
            <h6 class="fw-semibold mb-2">Câu ${index + 1}: ${escapeHtml(question?.questionText || "")}</h6>
            <div class="quiz-options-grid">${optionMarkup}</div>
          </article>
        `;
      })
      .join("");

    renderNavigator(questions);
    questionSection?.classList.remove("d-none");
    resultModalInstance?.hide();
    if (subline) {
      subline.textContent = `Đang làm bộ đề #${quiz?.quizId || "-"}`;
    }
  };

  const collectAnswers = () => {
    const questions = Array.isArray(state.quiz?.questions) ? state.quiz.questions : [];
    return questions.map((question) => {
      const qid = Number(question?.questionId || 0);
      const checked = document.querySelector(`input[name="quiz-q-${qid}"]:checked`);
      return {
        questionId: qid,
        selectedAnswer: checked ? checked.value : "",
      };
    });
  };

  const persistLatestQuizResult = (result, submittedAnswers) => {
    const selectedContentId = Number(contentSelect.value || 0);
    const selectedContent = state.contentItems.find((item) => Number(item?.contentId || 0) === selectedContentId);

    const payload = {
      savedAt: new Date().toISOString(),
      content: {
        id: selectedContentId,
        name: normalizeFileName(selectedContent?.fileName),
      },
      quiz: {
        quizId: Number(state.quiz?.quizId || 0),
        difficulty: String(state.quiz?.difficulty || "medium"),
        totalQuestions: Number(state.quiz?.totalQuestions || 0),
        questions: Array.isArray(state.quiz?.questions)
          ? state.quiz.questions.map((question) => ({
            questionId: Number(question?.questionId || 0),
            questionText: String(question?.questionText || ""),
            optionA: String(question?.optionA || ""),
            optionB: String(question?.optionB || ""),
            optionC: String(question?.optionC || ""),
            optionD: String(question?.optionD || ""),
            correctAnswer: String(question?.correctAnswer || ""),
          }))
          : [],
      },
      result: {
        quizId: Number(result?.quizId || 0),
        attemptId: Number(result?.attemptId || 0),
        totalQuestions: Number(result?.totalQuestions || 0),
        correctCount: Number(result?.correctCount || 0),
        wrongCount: Number(result?.wrongCount || 0),
        score: Number(result?.score || 0),
        wrongQuestions: Array.isArray(result?.wrongQuestions)
          ? result.wrongQuestions.map((item) => ({
            questionId: Number(item?.questionId || 0),
            questionText: String(item?.questionText || ""),
            selectedAnswer: String(item?.selectedAnswer || ""),
            correctAnswer: String(item?.correctAnswer || ""),
            explanation: String(item?.explanation || ""),
          }))
          : [],
      },
      submittedAnswers: Array.isArray(submittedAnswers)
        ? submittedAnswers.map((answer) => ({
          questionId: Number(answer?.questionId || 0),
          selectedAnswer: String(answer?.selectedAnswer || ""),
        }))
        : [],
    };

    const serialized = JSON.stringify(payload);
    window.sessionStorage.setItem(latestQuizResultStorageKey, serialized);
    window.localStorage.setItem(latestQuizResultStorageKey, serialized);
  };

  const renderSubmitResult = (result, submittedAnswers) => {
    const totalQuestions = Number(result?.totalQuestions || 0);
    const correctCount = Number(result?.correctCount || 0);
    const wrongCount = Number(result?.wrongCount || 0);
    const score = Number(result?.score || 0);

    if (resultHeadline) {
      resultHeadline.textContent = `${correctCount}/${totalQuestions} • ${Number.isFinite(score) ? score.toFixed(2) : "0.00"} điểm`;
    }

    if (resultSubline) {
      resultSubline.textContent = `Đúng ${correctCount} • Sai ${wrongCount}`;
    }

    const wrong = Array.isArray(result?.wrongQuestions) ? result.wrongQuestions : [];
    const wrongById = new Map(wrong.map((item) => [Number(item?.questionId || 0), item]));
    const answersById = new Map((submittedAnswers || []).map((item) => [Number(item?.questionId || 0), String(item?.selectedAnswer || "")]));
    const questions = Array.isArray(state.quiz?.questions) ? state.quiz.questions : [];

    if (reviewList) {
      reviewList.innerHTML = questions
        .map((question, index) => {
          const qid = Number(question?.questionId || 0);
          const wrongItem = wrongById.get(qid);
          const selected = answersById.get(qid) || "";
          const isWrong = !!wrongItem;
          const status = isWrong ? "Sai" : "Đúng";

          return `
            <article class="quiz-review-item ${isWrong ? "quiz-review-item--wrong" : "quiz-review-item--correct"}">
              <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
                <div class="quiz-wrong-title">Câu ${index + 1}: ${escapeHtml(question?.questionText || "")}</div>
                <span class="quiz-review-badge ${isWrong ? "quiz-review-badge--wrong" : "quiz-review-badge--correct"}">${status}</span>
              </div>
              <div class="small text-muted-2">Bạn chọn: ${escapeHtml(selected || "(bỏ trống)")}</div>
              ${isWrong
                ? `<div class="quiz-correct-answer-line">Đáp án đúng: ${escapeHtml(wrongItem?.correctAnswer || "")}</div>
                   <div class="small text-muted-2 mt-1">Giải thích: ${escapeHtml(wrongItem?.explanation || "Không có giải thích.")}</div>`
                : '<div class="quiz-correct-status-line">Bạn trả lời đúng.</div>'}
            </article>
          `;
        })
        .join("");
    }

    if (resultModalInstance) {
      resultModalInstance.show();
    }
  };

  const loadContents = async () => {
    if (state.loadingContents) {
      return;
    }

    state.loadingContents = true;
    setButtons();

    try {
      const response = await fetch("/api/contents?page=1&pageSize=100&sort=latest", {
        method: "GET",
        headers: getAuthHeaders(false),
      });

      if (response.status === 401) {
        state.contentItems = [];
        contentSelect.innerHTML = '<option value="">Đăng nhập để xem nội dung</option>';
        setNotice("Trang quiz yêu cầu bạn đăng nhập để chọn nội dung từ lịch sử học tập.", "error");
        return;
      }

      const data = await readJson(response);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không tải được danh sách nội dung.");
      }

      state.contentItems = Array.isArray(data?.items) ? data.items : [];
      if (state.contentItems.length === 0) {
        contentSelect.innerHTML = '<option value="">Chưa có nội dung. Hãy upload trước.</option>';
        setNotice("Bạn chưa có nội dung để tạo quiz. Hãy xử lý nội dung ở trang Upload.", "error");
        return;
      }

      const selectedFromQuery = getContentIdFromQuery();
      contentSelect.innerHTML = state.contentItems
        .map((item) => {
          const id = Number(item?.contentId || 0);
          const selected = selectedFromQuery > 0 && id === selectedFromQuery ? " selected" : "";
          return `<option value="${id}"${selected}>${escapeHtml(normalizeFileName(item?.fileName))}</option>`;
        })
        .join("");

      if (!contentSelect.value && state.contentItems[0]) {
        contentSelect.value = String(state.contentItems[0].contentId);
      }

      setNotice("", "success");
    } catch (error) {
      setNotice(error instanceof Error ? error.message : "Không tải được danh sách nội dung.", "error");
    } finally {
      state.loadingContents = false;
      setButtons();
    }
  };

  const generateQuiz = async (isReload) => {
    if (state.generating) {
      return;
    }

    const contentId = Number(contentSelect.value || 0);
    if (contentId <= 0) {
      setNotice("Vui lòng chọn nội dung hợp lệ để tạo quiz.", "error");
      return;
    }

    state.generating = true;
    setButtons();
    setNotice("", "success");

    try {
      const variationNonce = isReload
        ? `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
        : `${Date.now()}`;

      const response = await fetch("/api/quiz/generate", {
        method: "POST",
        headers: {
          ...getAuthHeaders(true),
          ...buildGuestHeaders(),
        },
        body: JSON.stringify({
          contentId,
          totalQuestions: getCount(),
          difficulty: String(difficultySelect?.value || "medium"),
          variationNonce,
          guestToken: isAuthenticated() ? "" : ensureGuestToken(),
        }),
      });

      syncGuestTokenFromResponse(response);
      const data = await readJson(response);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không thể tạo bộ đề.");
      }

      if (data?.guestToken) {
        window.localStorage.setItem(guestQuizTokenKey, String(data.guestToken));
      }

      renderQuiz(data);
      setNotice("Đã tạo bộ đề thành công.", "success");
    } catch (error) {
      setNotice(error instanceof Error ? error.message : "Không thể tạo quiz.", "error");
    } finally {
      state.generating = false;
      setButtons();
    }
  };

  const submitQuiz = async () => {
    if (!state.quiz?.quizId || state.submitting) {
      return;
    }

    state.submitting = true;
    setButtons();
    setNotice("", "success");

    const submittedAnswers = collectAnswers();

    try {
      const response = await fetch("/api/quiz/submit", {
        method: "POST",
        headers: {
          ...getAuthHeaders(true),
          ...buildGuestHeaders(),
        },
        body: JSON.stringify({
          quizId: Number(state.quiz.quizId),
          guestToken: isAuthenticated() ? "" : ensureGuestToken(),
          answers: submittedAnswers,
        }),
      });

      syncGuestTokenFromResponse(response);
      const data = await readJson(response);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không thể nộp bài.");
      }

      persistLatestQuizResult(data, submittedAnswers);
      renderSubmitResult(data, submittedAnswers);
      setNotice("Đã nộp bài và chấm điểm thành công.", "success");
    } catch (error) {
      setNotice(error instanceof Error ? error.message : "Không thể nộp bài.", "error");
    } finally {
      state.submitting = false;
      setButtons();
    }
  };

  navigator?.addEventListener("click", (event) => {
    const target = event.target instanceof Element
      ? event.target.closest("[data-nav-index]")
      : null;

    if (!target) {
      return;
    }

    const index = Number(target.getAttribute("data-nav-index") || 0);
    if (index <= 0) {
      return;
    }

    const questionEl = document.getElementById(`quiz-question-${index}`);
    questionEl?.scrollIntoView({ behavior: "smooth", block: "start" });
  });

  generateButton.addEventListener("click", () => {
    generateQuiz(false);
  });

  reloadButton?.addEventListener("click", () => {
    generateQuiz(true);
  });

  submitButton?.addEventListener("click", () => {
    submitQuiz();
  });

  questionCountInput.addEventListener("blur", () => {
    getCount();
  });

  setButtons();
  loadContents();
})();
