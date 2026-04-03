(function () {
  const notice = document.getElementById("dashboardNotice");
  const streakDays = document.getElementById("streakDays");
  const streakDelta = document.getElementById("streakDelta");
  const kpiContents = document.getElementById("kpiContents");
  const kpiContentsMeta = document.getElementById("kpiContentsMeta");
  const kpiQuizzes = document.getElementById("kpiQuizzes");
  const kpiGoal = document.getElementById("kpiGoal");
  const kpiGoalMeta = document.getElementById("kpiGoalMeta");
  const kpiQuizzesMeta = document.getElementById("kpiQuizzesMeta");
  const continueTitle = document.getElementById("continueTitle");
  const continueMeta = document.getElementById("continueMeta");
  const continueViewLink = document.getElementById("continueViewLink");
  const continueQuizLink = document.getElementById("continueQuizLink");
  const continueProgressBar = document.getElementById("continueProgressBar");
  const continueProgressText = document.getElementById("continueProgressText");
  const smartSuggestionsList = document.getElementById("smartSuggestionsList");
  const dailyPlanChecklist = document.getElementById("dailyPlanChecklist");
  const greetingNameElements = document.querySelectorAll("[data-auth-name]");

  const vietnameseWordMap = {
    dang: "đảng",
    cong: "cộng",
    san: "sản",
    viet: "việt",
    nam: "nam",
    duoc: "được",
    thanh: "thành",
    lap: "lập",
    vao: "vào",
    ngay: "ngày",
  };

  const escapeHtml = (text) =>
    String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const setNotice = (message) => {
    if (!notice) {
      return;
    }

    if (!message) {
      notice.classList.add("d-none");
      notice.textContent = "";
      return;
    }

    notice.classList.remove("d-none");
    notice.textContent = message;
  };

  const getAuthHeaders = () => {
    const headers = { "Content-Type": "application/json" };
    const token = window.AuthClient?.getAccessToken?.() || "";
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    return headers;
  };

  const prettifyTitle = (value) => {
    const fallback = "Nội dung chưa đặt tên";
    const raw = String(value || "").trim();
    if (!raw) {
      return fallback;
    }

    const match = raw.match(/^(.*?)(\.[a-z0-9]{1,8})$/i);
    const stem = (match?.[1] || raw).replace(/[_-]+/g, " ").replace(/\s+/g, " ").trim();
    const extension = match?.[2] || "";
    if (!stem) {
      return fallback;
    }

    const parts = stem
      .split(" ")
      .filter(Boolean)
      .map((word) => {
        const lower = word.toLowerCase();
        return vietnameseWordMap[lower] || lower;
      });

    const normalized = parts.join(" ");
    const withUppercase = normalized.charAt(0).toUpperCase() + normalized.slice(1);
    return `${withUppercase}${extension.toLowerCase()}`;
  };

  const renderGreetingName = (name) => {
    const raw = String(name || "").trim();
    if (!raw || greetingNameElements.length === 0) {
      return;
    }

    greetingNameElements.forEach((el) => {
      el.textContent = raw;
    });
  };

  const toRelativeLabel = (value) => {
    if (!value) {
      return "Vừa cập nhật";
    }

    const raw = String(value).trim();
    const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw);
    const normalized = hasTimezone ? raw : `${raw}Z`;
    const date = new Date(normalized);
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

  const renderStreak = (days, delta) => {
    if (streakDays) {
      streakDays.textContent = String(days || 0);
    }

    if (!streakDelta) {
      return;
    }

    const safeDelta = Number(delta || 0);
    if (safeDelta > 0) {
      streakDelta.textContent = `+${safeDelta} ngày so với tuần trước`;
      return;
    }

    if (safeDelta < 0) {
      streakDelta.textContent = `${safeDelta} ngày so với tuần trước`;
      return;
    }

    streakDelta.textContent = "Không đổi so với tuần trước";
  };

  const renderKpis = (kpis) => {
    const totalContents = Number(kpis?.totalContents || 0);
    const totalContentsLast7Days = Number(kpis?.totalContentsLast7Days || 0);
    const completedQuizzes = Number(kpis?.completedQuizzes || 0);
    const weeklyGoalPercent = Number(kpis?.weeklyGoalPercent || 0);

    if (kpiContents) {
      kpiContents.textContent = String(totalContents);
    }

    if (kpiContentsMeta) {
      kpiContentsMeta.textContent = `+${totalContentsLast7Days} trong 7 ngày gần nhất`;
    }

    if (kpiQuizzes) {
      kpiQuizzes.textContent = String(completedQuizzes);
    }

    if (kpiQuizzesMeta) {
      kpiQuizzesMeta.textContent = completedQuizzes > 0
        ? "Bạn đang giữ nhịp học tốt"
        : "Bắt đầu quiz đầu tiên để có thống kê";
    }

    if (kpiGoal) {
      kpiGoal.textContent = `${weeklyGoalPercent}%`;
    }

    if (kpiGoalMeta) {
      const remainingSessions = Math.max(0, Math.ceil((100 - weeklyGoalPercent) / 100 * 7));
      kpiGoalMeta.textContent = remainingSessions === 0
        ? "Bạn đã hoàn thành mục tiêu tuần này"
        : `Cần thêm ${remainingSessions} phiên học để đạt 100%`;
    }
  };

  const renderContinueLearning = (item) => {
    if (!continueTitle || !continueMeta || !continueViewLink || !continueQuizLink) {
      return;
    }

    if (!item) {
      continueTitle.textContent = "Chưa có nội dung gần đây";
      continueMeta.textContent = "Sẵn sàng tiếp tục • Hãy tải tài liệu đầu tiên để bắt đầu";
      continueViewLink.href = "history.html";
      continueQuizLink.href = "upload.html";
      return;
    }

    continueTitle.textContent = prettifyTitle(item.title);
    const statusTag = item.hasAiProcess ? "Sẵn sàng tiếp tục" : "AI đang chuẩn bị nội dung";
    continueMeta.textContent = `${statusTag} • Cập nhật ${toRelativeLabel(item.updatedAt)}`;
    continueViewLink.href = String(item.viewUrl || "history.html");
    continueQuizLink.href = String(item.quizUrl || "upload.html");
  };

  const renderContinueProgress = (kpis) => {
    if (!continueProgressBar || !continueProgressText) {
      return;
    }

    const percent = Math.max(0, Math.min(100, Number(kpis?.weeklyGoalPercent || 0)));
    continueProgressBar.style.width = `${percent}%`;
    continueProgressText.textContent = `Tiến độ ${percent}%`;
  };

  const suggestionIconMap = ["🎯", "🔁", "✨", "🧠"];

  const renderSmartSuggestions = (items) => {
    if (!smartSuggestionsList) {
      return;
    }

    const source = Array.isArray(items) && items.length > 0
      ? items.slice(0, 4)
      : [
          "Ôn lại các chủ đề yếu từ quiz gần nhất.",
          "Làm lại một quiz điểm thấp để cải thiện độ chính xác.",
          "Tạo một quiz mới từ tài liệu gần nhất.",
        ];

    smartSuggestionsList.innerHTML = source
      .map((text, idx) => {
        const icon = suggestionIconMap[idx % suggestionIconMap.length];
        return `<li><span class="dash-suggestion-icon">${icon}</span><span>${escapeHtml(text)}</span></li>`;
      })
      .join("");
  };

  const renderDailyPlan = ({ kpis, streakDays, continueLearning }) => {
    if (!dailyPlanChecklist) {
      return;
    }

    const completedQuizzes = Number(kpis?.completedQuizzes || 0);
    const safeStreakDays = Number(streakDays || 0);
    const actions = [];

    actions.push(
      continueLearning
        ? "Hoàn thành một lượt ôn nhanh từ tài liệu gần nhất"
        : "Tải lên một tài liệu mới để bắt đầu học",
    );

    actions.push(
      completedQuizzes > 0
        ? "Hoàn thành một quiz nhanh trong hôm nay"
        : "Làm quiz đầu tiên trong hôm nay",
    );

    actions.push("Ôn lại một câu sai gần đây");

    actions.push(
      safeStreakDays > 0
        ? "Duy trì chuỗi học tập hằng ngày"
        : "Bắt đầu một chuỗi học tập đều đặn mỗi ngày",
    );

    dailyPlanChecklist.innerHTML = actions
      .slice(0, 4)
      .map((text, idx) => `
        <li>
          <label class="dash-check-item">
            <input type="checkbox" ${idx === 0 ? "" : ""} />
            <span>${escapeHtml(text)}</span>
          </label>
        </li>
      `)
      .join("");
  };

  const loadDashboard = async () => {
    setNotice("");
    try {
      const response = await fetch("/api/dashboard/overview", {
        method: "GET",
        headers: getAuthHeaders(),
      });

      if (response.status === 401) {
        setNotice("Bạn cần đăng nhập để xem dashboard.");
        return;
      }

      const data = await response.json().catch(() => null);
      if (!response.ok || !data) {
        throw new Error(data?.message || "Không tải được dữ liệu dashboard.");
      }

      renderGreetingName(data.greetingName);
      renderStreak(data.streakDays, data.streakDelta);
      renderKpis(data.kpis);
      renderContinueLearning(data.continueLearning);
      renderContinueProgress(data.kpis);
      renderSmartSuggestions(data.suggestions);
      renderDailyPlan({
        kpis: data.kpis,
        streakDays: data.streakDays,
        continueLearning: data.continueLearning,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : "Có lỗi khi tải dashboard.";
      setNotice(message);
      renderStreak(0, 0);
      renderContinueProgress(null);
      renderSmartSuggestions([]);
      renderDailyPlan({
        kpis: null,
        streakDays: 0,
        continueLearning: null,
      });
    }
  };

  const boot = async () => {
    if (!window.AuthClient) {
      return;
    }

    const me = await window.AuthClient.requireAuth();
    if (!me) {
      return;
    }

    window.AuthClient.bindUserUi(me);
    loadDashboard();
  };

  boot();
})();
