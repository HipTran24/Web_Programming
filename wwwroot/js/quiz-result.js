(function () {
  const storageKey = "quiz.latestResult.v1";

  const resultScoreMain = document.getElementById("resultScoreMain");
  const resultPerformanceBadge = document.getElementById("resultPerformanceBadge");
  const resultSavedAt = document.getElementById("resultSavedAt");
  const resultSourceName = document.getElementById("resultSourceName");
  const resultTotalQuestions = document.getElementById("resultTotalQuestions");
  const resultCorrectCount = document.getElementById("resultCorrectCount");
  const resultWrongCount = document.getElementById("resultWrongCount");
  const resultDifficulty = document.getElementById("resultDifficulty");
  const resultAccuracyText = document.getElementById("resultAccuracyText");
  const resultAccuracyBar = document.getElementById("resultAccuracyBar");
  const resultAdviceList = document.getElementById("resultAdviceList");
  const reviewWrongButton = document.getElementById("reviewWrongButton");
  const downloadResultReportButton = document.getElementById("downloadResultReportButton");
  const questionFilterGroup = document.getElementById("questionFilterGroup");
  const questionReviewList = document.getElementById("questionReviewList");
  const quizResultEmptyState = document.getElementById("quizResultEmptyState");

  let currentPayload = null;

  const scheduleIdle = (callback) => {
    if (typeof window.requestIdleCallback === "function") {
      window.requestIdleCallback(() => callback());
      return;
    }

    window.setTimeout(callback, 0);
  };

  const escapeHtml = (text) =>
    String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const formatDifficulty = (value) => {
    const normalized = String(value || "").trim().toLowerCase();
    if (normalized === "easy") return "Dễ";
    if (normalized === "hard") return "Khó";
    return "Trung bình";
  };

  const getPerformance = (accuracy) => {
    if (accuracy >= 90) {
      return { label: "Xuất sắc", css: "is-excellent" };
    }

    if (accuracy >= 75) {
      return { label: "Rất tốt", css: "is-good" };
    }

    if (accuracy >= 60) {
      return { label: "Ổn định", css: "is-fair" };
    }

    return { label: "Cần ôn thêm", css: "is-needs-work" };
  };

  const buildAdvice = (wrongCount, totalQuestions, accuracy) => {
    const tips = [];

    if (wrongCount <= 0) {
      tips.push("Bạn đang nắm rất chắc nội dung. Hãy tăng độ khó ở lượt kế tiếp.");
      tips.push("Thử tạo bộ đề mới để kiểm tra độ bền kiến thức sau vài giờ.");
      return tips;
    }

    const wrongRate = totalQuestions > 0 ? (wrongCount / totalQuestions) * 100 : 0;
    if (wrongRate >= 50) {
      tips.push("Ưu tiên đọc lại phần tóm tắt và ý chính trước khi làm lại quiz.");
      tips.push("Tập trung vào từng câu sai, viết lại đáp án đúng theo cách hiểu của bạn.");
    } else {
      tips.push("Bạn đã nắm phần lớn kiến thức, chỉ cần xử lý nhóm lỗi còn lại.");
      tips.push("Làm lại quiz ngay trong 10-15 phút để củng cố trí nhớ ngắn hạn.");
    }

    if (accuracy < 70) {
      tips.push("Giảm tốc độ làm bài ở lượt sau để đọc kỹ từng lựa chọn.");
    } else {
      tips.push("Thử tăng số lượng câu hỏi để kiểm tra độ bao phủ kiến thức.");
    }

    return tips;
  };

  const parseStoredPayload = () => {
    const candidates = [
      window.sessionStorage.getItem(storageKey),
      window.localStorage.getItem(storageKey),
    ].filter(Boolean);

    for (const raw of candidates) {
      try {
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed === "object") {
          return parsed;
        }
      } catch {
        // Ignore malformed payload and fallback to next candidate.
      }
    }

    return null;
  };

  const normalizeReviewItems = (payload) => {
    const quiz = payload?.quiz || {};
    const result = payload?.result || {};

    const questions = Array.isArray(quiz.questions) ? quiz.questions : [];
    const submittedAnswers = Array.isArray(payload?.submittedAnswers) ? payload.submittedAnswers : [];
    const wrongQuestions = Array.isArray(result.wrongQuestions) ? result.wrongQuestions : [];

    const answerByQuestionId = new Map(
      submittedAnswers.map((item) => [Number(item?.questionId || 0), String(item?.selectedAnswer || "")])
    );

    const wrongByQuestionId = new Map(
      wrongQuestions.map((item) => [Number(item?.questionId || 0), item])
    );

    if (questions.length > 0) {
      return questions.map((question, index) => {
        const questionId = Number(question?.questionId || 0);
        const wrongDetail = wrongByQuestionId.get(questionId);
        const selectedAnswer = answerByQuestionId.get(questionId) || "";
        const isWrong = !!wrongDetail;

        return {
          index: index + 1,
          questionText: String(question?.questionText || ""),
          selectedAnswer,
          correctAnswer: String(
            isWrong
              ? wrongDetail?.correctAnswer || ""
              : question?.correctAnswer || selectedAnswer || ""
          ),
          explanation: String(
            isWrong
              ? wrongDetail?.explanation || ""
              : "Bạn trả lời chính xác ở câu này."
          ),
          isWrong,
        };
      });
    }

    return wrongQuestions.map((item, index) => ({
      index: index + 1,
      questionText: String(item?.questionText || ""),
      selectedAnswer: String(item?.selectedAnswer || ""),
      correctAnswer: String(item?.correctAnswer || ""),
      explanation: String(item?.explanation || ""),
      isWrong: true,
    }));
  };

  const buildReviewItemMarkup = (item, index) => {
    const statusText = item.isWrong ? "Sai" : "Đúng";
    const statusClass = item.isWrong
      ? "quiz-review-badge quiz-review-badge--wrong"
      : "quiz-review-badge quiz-review-badge--correct";
    const delay = Math.min(index, 10) * 36;

    return `
      <article class="quiz-review-item quiz-review-item-reveal ${item.isWrong ? "quiz-review-item--wrong" : "quiz-review-item--correct"}" style="--row-delay:${delay}ms">
        <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
          <div class="quiz-wrong-title">Câu ${item.index}: ${escapeHtml(item.questionText)}</div>
          <span class="${statusClass}">${statusText}</span>
        </div>
        <div class="small text-muted-2">Bạn chọn: ${escapeHtml(item.selectedAnswer || "(bỏ trống)")}</div>
        <div class="quiz-correct-answer-line">Đáp án đúng: ${escapeHtml(item.correctAnswer || "-")}</div>
        <div class="small text-muted-2 mt-1">Giải thích: ${escapeHtml(item.explanation || "Không có giải thích.")}</div>
      </article>
    `;
  };

  const chunkedRender = (items) => {
    if (!questionReviewList) {
      return;
    }

    questionReviewList.innerHTML = "<div class=\"small text-muted-2\">Đang dựng chi tiết kết quả...</div>";

    const chunkSize = 8;
    let cursor = 0;
    const chunks = [];

    while (cursor < items.length) {
      const slice = items.slice(cursor, cursor + chunkSize);
      const markup = slice
        .map((item, offset) => buildReviewItemMarkup(item, cursor + offset))
        .join("");
      chunks.push(markup);
      cursor += chunkSize;
    }

    questionReviewList.innerHTML = "";

    const pump = () => {
      if (chunks.length === 0) {
        return;
      }

      const next = chunks.shift();
      if (next) {
        questionReviewList.insertAdjacentHTML("beforeend", next);
      }

      if (chunks.length > 0) {
        requestAnimationFrame(pump);
      }
    };

    requestAnimationFrame(pump);
  };

  const renderReviewList = (items, filterMode) => {
    if (!questionReviewList) {
      return;
    }

    const filtered = items.filter((item) => {
      if (filterMode === "wrong") return item.isWrong;
      if (filterMode === "correct") return !item.isWrong;
      return true;
    });

    if (filtered.length === 0) {
      questionReviewList.innerHTML = '<div class="small text-muted-2">Không có câu hỏi phù hợp với bộ lọc hiện tại.</div>';
      return;
    }

    chunkedRender(filtered);
  };

  const updateFilterButtons = (filterMode) => {
    if (!questionFilterGroup) {
      return;
    }

    const buttons = questionFilterGroup.querySelectorAll("[data-filter]");
    buttons.forEach((button) => {
      const isActive = String(button.getAttribute("data-filter") || "all") === filterMode;
      button.classList.toggle("is-active", isActive);
    });
  };

  const renderEmptyState = () => {
    if (quizResultEmptyState) {
      quizResultEmptyState.classList.remove("d-none");
    }
    if (questionReviewList) {
      questionReviewList.innerHTML = "";
    }
  };

  const renderPage = () => {
    const payload = parseStoredPayload();
    currentPayload = payload;
    if (!payload) {
      renderEmptyState();
      return;
    }

    const result = payload?.result || {};
    const quiz = payload?.quiz || {};
    const content = payload?.content || {};
    const totalQuestions = Number(result.totalQuestions || 0);
    const correctCount = Number(result.correctCount || 0);
    const wrongCount = Number(result.wrongCount || 0);
    const score = Number(result.score || 0);
    const safeScore = Number.isFinite(score) ? score : 0;
    const accuracy = totalQuestions > 0 ? Math.max(0, Math.min(100, (correctCount / totalQuestions) * 100)) : 0;

    if (quizResultEmptyState) {
      quizResultEmptyState.classList.add("d-none");
    }

    if (resultScoreMain) {
      resultScoreMain.textContent = safeScore.toFixed(2);
    }

    if (resultTotalQuestions) {
      resultTotalQuestions.textContent = String(totalQuestions);
    }

    if (resultCorrectCount) {
      resultCorrectCount.textContent = String(correctCount);
    }

    if (resultWrongCount) {
      resultWrongCount.textContent = String(wrongCount);
    }

    if (resultDifficulty) {
      resultDifficulty.textContent = formatDifficulty(quiz?.difficulty || "medium");
    }

    if (resultAccuracyText) {
      resultAccuracyText.textContent = `${accuracy.toFixed(1)}%`;
    }

    if (resultAccuracyBar) {
      resultAccuracyBar.style.width = `${accuracy}%`;
      resultAccuracyBar.setAttribute("aria-valuenow", String(Math.round(accuracy)));
    }

    const performance = getPerformance(accuracy);
    if (resultPerformanceBadge) {
      resultPerformanceBadge.textContent = performance.label;
      resultPerformanceBadge.className = `quiz-performance-badge ${performance.css}`;
    }

    const savedAtIso = payload?.savedAt;
    if (resultSavedAt) {
      const date = savedAtIso ? new Date(savedAtIso) : null;
      resultSavedAt.textContent = date && !Number.isNaN(date.getTime())
        ? `Lần nộp: ${date.toLocaleString("vi-VN")}`
        : "Lần nộp gần nhất";
    }

    if (resultSourceName) {
      const sourceName = String(content?.name || "").trim();
      resultSourceName.textContent = sourceName
        ? `Nguồn nội dung: ${sourceName}`
        : "Nguồn nội dung: quiz gần nhất";
    }

    if (resultAdviceList) {
      const advice = buildAdvice(wrongCount, totalQuestions, accuracy);
      resultAdviceList.innerHTML = advice.map((item) => `<li>${escapeHtml(item)}</li>`).join("");
    }

    const reviewItems = normalizeReviewItems(payload);
    let activeFilter = "all";

    const applyFilter = (nextFilter) => {
      activeFilter = nextFilter;
      updateFilterButtons(activeFilter);
      renderReviewList(reviewItems, activeFilter);
    };

    scheduleIdle(() => {
      applyFilter("all");
    });

    if (questionFilterGroup) {
      questionFilterGroup.addEventListener("click", (event) => {
        const button = event.target instanceof Element
          ? event.target.closest("[data-filter]")
          : null;

        if (!button) {
          return;
        }

        const nextFilter = String(button.getAttribute("data-filter") || "all");
        applyFilter(nextFilter);
      });
    }

    if (reviewWrongButton) {
      reviewWrongButton.addEventListener("click", () => {
        applyFilter("wrong");
        questionReviewList?.scrollIntoView({ behavior: "smooth", block: "start" });
      });
    }

    if (downloadResultReportButton) {
      downloadResultReportButton.addEventListener("click", () => {
        if (!currentPayload) {
          return;
        }

        const reportItems = normalizeReviewItems(currentPayload);
        const lines = [];
        lines.push("SYNAPLEARN - BAO CAO KET QUA QUIZ");
        lines.push(`Thoi gian luu: ${new Date().toLocaleString("vi-VN")}`);
        lines.push(`Nguon noi dung: ${String(content?.name || "quiz gan nhat")}`);
        lines.push(`Diem: ${safeScore.toFixed(2)} | Dung: ${correctCount}/${totalQuestions} | Sai: ${wrongCount}`);
        lines.push("-");

        reportItems.forEach((item) => {
          lines.push(`Cau ${item.index}: ${item.questionText}`);
          lines.push(`- Ban chon: ${item.selectedAnswer || "(bo trong)"}`);
          lines.push(`- Dap an dung: ${item.correctAnswer || "-"}`);
          lines.push(`- Giai thich: ${item.explanation || "Khong co"}`);
          lines.push("");
        });

        const blob = new Blob([lines.join("\n")], { type: "text/plain;charset=utf-8" });
        const href = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = href;
        anchor.download = `quiz-report-${Date.now()}.txt`;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(href);
      });
    }
  };

  renderPage();
})();
