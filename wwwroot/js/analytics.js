(function () {
  const notice = document.getElementById("analyticsNotice");
  const kpiAverage = document.getElementById("analyticsKpiAverage");
  const kpiAverageMeta = document.getElementById("analyticsKpiAverageMeta");
  const kpiAttempts = document.getElementById("analyticsKpiAttempts");
  const kpiAttemptsMeta = document.getElementById("analyticsKpiAttemptsMeta");
  const kpiWrong = document.getElementById("analyticsKpiWrong");
  const kpiWrongMeta = document.getElementById("analyticsKpiWrongMeta");
  const kpiConsistency = document.getElementById("analyticsKpiConsistency");
  const kpiConsistencyMeta = document.getElementById("analyticsKpiConsistencyMeta");
  const updatedChip = document.getElementById("analyticsUpdatedChip");
  const barsContainer = document.getElementById("analyticsBars");
  const topicList = document.getElementById("analyticsTopicList");
  const suggestionList = document.getElementById("analyticsSuggestionList");
  const questionTableBody = document.querySelector(".analytics-table tbody");

  if (!barsContainer || !questionTableBody) {
    return;
  }

  const escapeHtml = (text) =>
    String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const getAuthHeaders = () => {
    const headers = { "Content-Type": "application/json" };
    const token = window.AuthClient?.getAccessToken?.() || "";
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    return headers;
  };

  const setNotice = (message, type = "error") => {
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

  const normalizeQuestion = (text) => {
    const raw = String(text || "").replace(/\s+/g, " ").trim();
    if (!raw) {
      return "Câu hỏi chưa có nội dung";
    }

    return raw.length > 220 ? `${raw.slice(0, 217)}...` : raw;
  };

  const normalizeTopic = (text) => {
    const raw = String(text || "").replace(/\s+/g, " ").trim();
    if (!raw) {
      return "Khác";
    }

    if (raw.length <= 80) {
      return raw;
    }

    return `${raw.slice(0, 77)}...`;
  };

  const badgeClassByWrongCount = (wrongCount) => {
    if (wrongCount >= 5) {
      return "analytics-badge-danger";
    }

    return "analytics-badge-warn";
  };

  const renderKpis = (kpis) => {
    const average = Number(kpis?.averageScorePercent || 0);
    const averageDelta = Number(kpis?.averageScoreDeltaPercent || 0);
    const totalAttempts = Number(kpis?.totalAttempts || 0);
    const attemptsLast7Days = Number(kpis?.attemptsLast7Days || 0);
    const wrongAnswersCount = Number(kpis?.wrongAnswersCount || 0);
    const consistencyLabel = String(kpis?.consistencyLabel || "Chưa có dữ liệu");
    const activeDaysCurrentWeek = Number(kpis?.activeDaysCurrentWeek || 0);
    const activeDaysPreviousWeek = Number(kpis?.activeDaysPreviousWeek || 0);

    if (kpiAverage) {
      kpiAverage.textContent = `${average}%`;
    }

    if (kpiAverageMeta) {
      const deltaText = averageDelta > 0 ? `+${averageDelta}%` : `${averageDelta}%`;
      kpiAverageMeta.textContent = `${deltaText} so với 7 ngày trước`;
    }

    if (kpiAttempts) {
      kpiAttempts.textContent = String(totalAttempts);
    }

    if (kpiAttemptsMeta) {
      kpiAttemptsMeta.textContent = `${attemptsLast7Days} quiz trong 7 ngày gần nhất`;
    }

    if (kpiWrong) {
      kpiWrong.textContent = String(wrongAnswersCount);
    }

    if (kpiWrongMeta) {
      kpiWrongMeta.textContent = wrongAnswersCount > 0
        ? "Các câu sai đang được theo dõi để ưu tiên ôn"
        : "Hiện chưa có câu sai được ghi nhận";
    }

    if (kpiConsistency) {
      kpiConsistency.textContent = consistencyLabel;
    }

    if (kpiConsistencyMeta) {
      const delta = activeDaysCurrentWeek - activeDaysPreviousWeek;
      if (delta > 0) {
        kpiConsistencyMeta.textContent = `+${delta} ngày học chủ động so với tuần trước`;
      } else if (delta < 0) {
        kpiConsistencyMeta.textContent = `${delta} ngày so với tuần trước`;
      } else {
        kpiConsistencyMeta.textContent = "Không đổi so với tuần trước";
      }
    }
  };

  const renderDailyTrend = (items) => {
    const source = Array.isArray(items) ? items : [];
    barsContainer.innerHTML = source
      .slice(0, 7)
      .map((row) => {
        const percent = Math.max(0, Math.min(100, Number(row?.scorePercent || 0)));
        const safePercent = percent === 0 ? 6 : percent;
        const barClass = row?.hasData ? "analytics-bar" : "analytics-bar analytics-bar--empty";

        return `<div class="${barClass}" style="--score:${safePercent}%" title="${percent}%"><span>${escapeHtml(row?.day || "-")}</span></div>`;
      })
      .join("");
  };

  const renderTopicAccuracy = (items) => {
    if (!topicList) {
      return;
    }

    const source = Array.isArray(items) ? items : [];
    if (source.length === 0) {
      topicList.innerHTML = '<div class="text-muted-2 small">Chưa đủ dữ liệu để phân tích theo chủ đề.</div>';
      return;
    }

    topicList.innerHTML = source
      .slice(0, 5)
      .map((item) => {
        const topic = String(item?.topic || "Khác");
        const accuracy = Math.max(0, Math.min(100, Number(item?.accuracyPercent || 0)));
        return `
          <div class="analytics-topic-row">
            <div class="analytics-topic-label">${escapeHtml(topic)}</div>
            <div class="analytics-topic-value">${accuracy}%</div>
            <div class="analytics-topic-track"><span style="width:${accuracy}%"></span></div>
          </div>
        `;
      })
      .join("");
  };

  const renderSuggestions = (items) => {
    if (!suggestionList) {
      return;
    }

    const source = Array.isArray(items) && items.length > 0
      ? items
      : ["Duy trì đều đặn tối thiểu 1 quiz/ngày để hệ thống phân tích chính xác hơn."];

    suggestionList.innerHTML = source
      .slice(0, 4)
      .map((text, index) => `
        <li>
          <strong>Ưu tiên ${index + 1}</strong>
          <span>${escapeHtml(text)}</span>
        </li>
      `)
      .join("");
  };

  const renderWrongQuestions = (items) => {
    const source = Array.isArray(items) ? items : [];
    if (source.length === 0) {
      questionTableBody.innerHTML = `
        <tr>
          <td colspan="4" class="text-center text-muted-2 py-4">Chưa có dữ liệu câu sai.</td>
        </tr>
      `;
      return;
    }

    questionTableBody.innerHTML = source
      .map((item) => {
        const question = normalizeQuestion(item?.questionText);
        const topic = normalizeTopic(item?.topic);
        const wrongCount = Math.max(0, Number(item?.wrongCount || 0));

        return `
          <tr>
            <td class="analytics-col-question" title="${escapeHtml(question)}">${escapeHtml(question)}</td>
            <td class="analytics-col-topic"><span class="badge analytics-badge" title="${escapeHtml(topic)}">${escapeHtml(topic)}</span></td>
            <td class="analytics-col-wrong"><span class="badge ${badgeClassByWrongCount(wrongCount)}">${wrongCount}</span></td>
            <td class="text-end analytics-col-action"><a class="btn analytics-btn-soft btn-sm" href="quiz.html">Ôn lại</a></td>
          </tr>
        `;
      })
      .join("");
  };

  const renderLastUpdated = (value) => {
    if (!updatedChip) {
      return;
    }

    updatedChip.textContent = `Cập nhật: ${toLocalDateTime(value)}`;
  };

  const setFallbackState = (message) => {
    setNotice(message, "error");
    renderKpis({});
    renderDailyTrend([]);
    renderTopicAccuracy([]);
    renderSuggestions([]);
    renderWrongQuestions([]);
  };

  const loadAnalytics = async () => {
    setNotice("");

    try {
      const response = await fetch("/api/dashboard/analytics", {
        method: "GET",
        headers: getAuthHeaders(),
      });

      if (response.status === 401) {
        setFallbackState("Bạn cần đăng nhập để xem trang phân tích.");
        return;
      }

      const data = await response.json().catch(() => null);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không tải được dữ liệu analytics.");
      }

      renderKpis(data.kpis || {});
      renderDailyTrend(data.dailyTrend || []);
      renderTopicAccuracy(data.topicAccuracy || []);
      renderSuggestions(data.suggestions || []);
      renderWrongQuestions(data.topWrongQuestions || []);
      renderLastUpdated(data.lastUpdatedAt);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Có lỗi khi tải analytics.";
      setFallbackState(message);
    }
  };

  const boot = async () => {
    const me = await window.AuthClient?.requireAuth?.();
    if (!me) {
      return;
    }

    window.AuthClient?.bindUserUi?.(me);
    loadAnalytics();
  };

  boot();
})();
