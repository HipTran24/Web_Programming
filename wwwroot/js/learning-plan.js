(function () {
  const notice = document.getElementById("learningPlanNotice");
  const weekProgressEl = document.getElementById("learningPlanWeekProgress");
  const activeDaysEl = document.getElementById("learningPlanActiveDays");
  const averageScoreEl = document.getElementById("learningPlanAverageScore");
  const focusTopicEl = document.getElementById("learningPlanFocusTopic");
  const focusTopicMetaEl = document.getElementById("learningPlanFocusTopicMeta");
  const taskCountEl = document.getElementById("learningPlanTaskCount");
  const tasksEl = document.getElementById("learningPlanTasks");
  const recommendationsEl = document.getElementById("learningPlanRecommendations");

  if (!tasksEl || !recommendationsEl) {
    return;
  }

  const escapeHtml = (text) =>
    String(text || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

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

  const getAuthHeaders = () => {
    const token = window.AuthClient?.getAccessToken?.() || "";
    return token
      ? { Authorization: `Bearer ${token}` }
      : {};
  };

  const renderTasks = (tasks) => {
    const items = Array.isArray(tasks) ? tasks : [];

    if (taskCountEl) {
      taskCountEl.textContent = `${items.length} nhiệm vụ`;
    }

    if (items.length === 0) {
      tasksEl.innerHTML = `
        <article class="learning-plan-task is-empty">
          <div class="learning-plan-task-title">Chưa có nhiệm vụ khả dụng</div>
          <div class="learning-plan-task-detail">Vui lòng thử lại sau.</div>
        </article>
      `;
      return;
    }

    tasksEl.innerHTML = items
      .map((item) => {
        const current = Number(item?.current || 0);
        const target = Math.max(1, Number(item?.target || 1));
        const percent = Math.max(0, Math.min(100, Math.round((current * 100) / target)));
        const doneClass = item?.isCompleted ? "is-completed" : "";

        return `
          <article class="learning-plan-task ${doneClass}">
            <div class="learning-plan-task-head">
              <h6 class="learning-plan-task-title mb-0">${escapeHtml(item?.title || "Nhiệm vụ")}</h6>
              <span class="badge ${item?.isCompleted ? "text-bg-success" : "text-bg-secondary"}">${item?.isCompleted ? "Hoàn thành" : "Đang làm"}</span>
            </div>
            <div class="learning-plan-task-detail">${escapeHtml(item?.detail || "")}</div>
            <div class="learning-plan-progress mt-2">
              <div class="learning-plan-progress-bar" style="width:${percent}%"></div>
            </div>
            <div class="learning-plan-task-footer">
              <span class="small text-muted-2">${current}/${target}</span>
              <a class="btn btn-outline-light btn-sm" href="${escapeHtml(item?.actionUrl || "/home/dashboard.html")}">${escapeHtml(item?.actionText || "Mở")}</a>
            </div>
          </article>
        `;
      })
      .join("");
  };

  const renderRecommendations = (items) => {
    const source = Array.isArray(items) ? items : [];

    if (source.length === 0) {
      recommendationsEl.innerHTML = `
        <article class="learning-plan-recommendation is-empty">
          <div class="learning-plan-recommendation-title">Bạn đang đi đúng lộ trình</div>
          <div class="learning-plan-recommendation-detail">Tiếp tục giữ nhịp học hiện tại.</div>
        </article>
      `;
      return;
    }

    recommendationsEl.innerHTML = source
      .map((item) => `
        <article class="learning-plan-recommendation">
          <div class="learning-plan-recommendation-title">${escapeHtml(item?.title || "Gợi ý")}</div>
          <div class="learning-plan-recommendation-detail">${escapeHtml(item?.detail || "")}</div>
          <a class="btn btn-soft btn-sm mt-2" href="${escapeHtml(item?.actionUrl || "/home/dashboard.html")}">${escapeHtml(item?.actionText || "Xem")}</a>
        </article>
      `)
      .join("");
  };

  const render = (payload) => {
    const weekProgressPercent = Math.max(0, Math.min(100, Number(payload?.weekProgressPercent || 0)));
    const activeDaysCurrentWeek = Number(payload?.activeDaysCurrentWeek || 0);
    const weeklyGoalSessions = Math.max(1, Number(payload?.weeklyGoalSessions || 7));
    const averageScoreRecent = Number(payload?.averageScoreRecent || 0);

    const focusTopic = Array.isArray(payload?.focusTopics) && payload.focusTopics.length > 0
      ? payload.focusTopics[0]
      : null;

    if (weekProgressEl) {
      weekProgressEl.textContent = `${weekProgressPercent}%`;
    }

    if (activeDaysEl) {
      activeDaysEl.textContent = `${activeDaysCurrentWeek}/${weeklyGoalSessions} ngày hoạt động`;
    }

    if (averageScoreEl) {
      averageScoreEl.textContent = averageScoreRecent.toFixed(1);
    }

    if (focusTopicEl) {
      focusTopicEl.textContent = focusTopic?.name || "Tổng quan";
    }

    if (focusTopicMetaEl) {
      const accuracy = Number(focusTopic?.accuracyPercent ?? focusTopic?.AccuracyPercent ?? 0);
      const attempts = Number(focusTopic?.attempts ?? focusTopic?.Attempts ?? 0);
      focusTopicMetaEl.textContent = `Độ chính xác: ${accuracy}% • ${attempts} lượt`;
    }

    renderTasks(payload?.tasks || []);
    renderRecommendations(payload?.recommendations || []);
  };

  const loadLearningPlan = async () => {
    setNotice("");

    try {
      const response = await fetch("/api/dashboard/learning-plan", {
        method: "GET",
        headers: getAuthHeaders(),
        cache: "no-store",
      });

      if (response.status === 401) {
        setNotice("Bạn cần đăng nhập để xem lộ trình học tập.", "error");
        return;
      }

      const payload = await response.json().catch(() => null);
      if (!response.ok || !payload) {
        throw new Error(payload?.message || payload?.detail || payload?.title || "Không tải được lộ trình học tập.");
      }

      render(payload);
    } catch (error) {
      setNotice(error instanceof Error ? error.message : "Không tải được lộ trình học tập.", "error");
      render({
        tasks: [],
        recommendations: [],
      });
    }
  };

  const boot = async () => {
    const me = await window.AuthClient?.requireAuth?.();
    if (!me) {
      return;
    }

    window.AuthClient?.bindUserUi?.(me);
    loadLearningPlan();
  };

  boot();
})();
