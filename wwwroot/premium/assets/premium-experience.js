(() => {
  const PREMIUM_SIDEBAR_ID = "premium-sidebar";
  const PREMIUM_SIDEBAR_BACKDROP_ID = "premium-sidebar-backdrop";
  const PREMIUM_SIDEBAR_GROUPS = [
    {
      title: "Command Center",
      links: [
        { section: "dashboard", href: "dashboard.html", label: "Dashboard", meta: "Tổng quan ưu tiên hôm nay", short: "DB" },
        { section: "workspace", href: "study-workspace.html", label: "Workspace", meta: "Phiên học sâu đang chạy", short: "WS" },
      ],
    },
    {
      title: "Nội dung",
      links: [
        { section: "library", href: "content-library.html", label: "Library", meta: "Kho tài liệu và bộ lọc học tập", short: "LB" },
        { section: "detail", href: "content-detail.html", label: "Detail", meta: "Tóm tắt, metadata và bước tiếp", short: "DT" },
      ],
    },
    {
      title: "Luyện tập",
      links: [
        { section: "quiz", href: "quiz-experience.html", label: "Quiz", meta: "Focus mode cho phiên làm bài", short: "QZ" },
        { section: "result", href: "quiz-result.html", label: "Review", meta: "Đúng sai, lỗi lặp và gợi ý ôn", short: "RV" },
      ],
    },
    {
      title: "Chiến lược",
      links: [
        { section: "analytics", href: "analytics.html", label: "Analytics", meta: "Insight hiệu suất có thể hành động", short: "AN" },
        { section: "plan", href: "learning-plan.html", label: "Learning Plan", meta: "Lộ trình ngày và tuần", short: "LP" },
      ],
    },
    {
      title: "Gói học tập",
      links: [
        { section: "upgrade", href: "upgrade.html", label: "Nâng cấp", meta: "So sánh 200k và 500k token/ngày", short: "UP" },
        { section: "account", href: "account.html", label: "Tài khoản", meta: "Quản lý gói và token hôm nay", short: "AC" },
      ],
    },
  ];

  const applyBootstrapSkin = () => {
    document.querySelectorAll(".premium-shell").forEach((item) => {
      item.classList.add("container-xxl");
    });

    document.querySelectorAll(".premium-nav-list").forEach((item) => {
      item.classList.add("nav", "nav-pills");
    });

    document.querySelectorAll(".premium-nav-list li").forEach((item) => {
      item.classList.add("nav-item");
    });

    document.querySelectorAll(".premium-nav-list a").forEach((item) => {
      item.classList.add("nav-link");
    });

    document.querySelectorAll(".premium-account").forEach((item) => {
      item.classList.add("d-flex", "flex-wrap", "align-items-center");
    });

    document.querySelectorAll(".premium-plan-pill, .premium-chip, .premium-status-chip, .premium-mini-chip, .premium-inline-kpi").forEach((item) => {
      item.classList.add("badge");
    });

    document.querySelectorAll(".premium-action-primary").forEach((item) => {
      item.classList.add("btn", "btn-premium-primary");
    });

    document.querySelectorAll(".premium-action-secondary").forEach((item) => {
      item.classList.add("btn", "btn-premium-secondary");
    });

    document.querySelectorAll(".premium-action-muted").forEach((item) => {
      item.classList.add("btn", "btn-premium-muted");
    });

    document.querySelectorAll(".premium-filter-chip, .premium-tab-button").forEach((item) => {
      item.classList.add("btn", "btn-outline-light");
    });

    document.querySelectorAll(".premium-question-nav button").forEach((item) => {
      item.classList.add("btn", "btn-outline-light");
    });

    document.querySelectorAll(".premium-surface, .premium-card, .premium-library-item, .premium-review-card").forEach((item) => {
      item.classList.add("card", "border-0", "shadow-sm");
    });

    document.querySelectorAll(".premium-metric-strip").forEach((item) => {
      item.classList.add("list-unstyled");
    });
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
    if (normalized === "easy") return "De";
    if (normalized === "hard") return "Kho";
    return "Trung binh";
  };

  const formatQuizType = (value) => {
    const normalized = String(value || "").trim().toLowerCase();
    if (normalized === "multiple-choice") return "Trac nghiem";
    if (!normalized) return "--";
    return normalized;
  };

  const formatDate = (value) => {
    if (!value) return "--";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "--";
    return date.toLocaleDateString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  };

  const getAuthToken = () => window.AuthClient?.getAccessToken?.() || "";

  const buildAuthHeaders = () => {
    const headers = { Accept: "application/json" };
    const token = getAuthToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    return headers;
  };

  const setStatus = (selector, message, tone) => {
    const el = document.querySelector(selector);
    if (!el) {
      return;
    }

    if (!message) {
      el.textContent = "";
      el.style.display = "none";
      el.removeAttribute("data-tone");
      return;
    }

    el.textContent = message;
    el.style.display = "block";
    if (tone) {
      el.setAttribute("data-tone", tone);
    } else {
      el.removeAttribute("data-tone");
    }
  };

  const setQuizStatus = (message, tone) => setStatus("[data-quiz-status]", message, tone);
  const setPageStatus = (message, tone) => setStatus("[data-premium-status]", message, tone);

  const setText = (selector, value, fallback) => {
    const el = document.querySelector(selector);
    if (!el) {
      return;
    }
    const text = String(value || "").trim();
    el.textContent = text || fallback || "--";
  };

  const setLink = (selector, href) => {
    const el = document.querySelector(selector);
    if (!el) {
      return;
    }
    el.setAttribute("href", href);
  };

  const setImage = (selector, src, altText) => {
    const el = document.querySelector(selector);
    if (!el) {
      return;
    }

    const safeSrc = String(src || "").trim();
    if (!safeSrc) {
      el.setAttribute("hidden", "hidden");
      el.removeAttribute("src");
      el.removeAttribute("alt");
      return;
    }

    el.removeAttribute("hidden");
    el.setAttribute("src", safeSrc);
    el.setAttribute("alt", String(altText || "").trim());
  };

  const resolveDisplayName = (fullName, email) => {
    const name = String(fullName || "").trim();
    if (name) {
      return name;
    }
    const emailText = String(email || "").trim();
    if (!emailText) {
      return "Nguoi dung";
    }
    return emailText.split("@")[0] || emailText;
  };

  const getInitials = (fullName, email) => {
    const source = String(fullName || email || "").trim();
    if (!source) {
      return "US";
    }
    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }
    return source.slice(0, 2).toUpperCase();
  };

  const fetchCurrentUser = async () => {
    const response = await fetch("/api/auth/me", {
      method: "GET",
      cache: "no-store",
      credentials: "include",
      headers: buildAuthHeaders(),
    });

    if (!response.ok) {
      return null;
    }

    return response.json().catch(() => null);
  };

  const hydratePremiumUser = async () => {
    let me = await window.AuthClient?.requireAuth?.();
    if (!me) {
      me = await fetchCurrentUser();
    }

    if (me && window.AuthClient?.bindUserUi) {
      window.AuthClient.bindUserUi(me, {
        nameSelector: "[data-auth-name]",
        avatarSelector: "[data-auth-avatar]",
      });
    }

    if (me && !window.AuthClient?.bindUserUi) {
      setText("[data-auth-name]", resolveDisplayName(me.fullName, me.email));
      setText("[data-auth-avatar]", getInitials(me.fullName, me.email));
    }

    const meta = document.querySelector("[data-user-meta]");
    if (meta && me) {
      const roleLabel = String(me.role || "premium").toUpperCase();
      const email = String(me.email || "").trim();
      meta.textContent = email ? `${roleLabel} • ${email}` : `${roleLabel} account`;
    }

    return me;
  };

  const fetchJson = async (url) => {
    const response = await fetch(url, {
      method: "GET",
      cache: "no-store",
      credentials: "include",
      headers: buildAuthHeaders(),
    });

    const data = response.ok ? await response.json().catch(() => null) : null;
    return { ok: response.ok, status: response.status, data };
  };

  const renderQuestions = (questions) => {
    const nav = document.querySelector("[data-quiz-nav]");
    const cards = document.querySelector("[data-quiz-cards]");
    if (!nav || !cards) {
      return;
    }

    if (!Array.isArray(questions) || questions.length === 0) {
      nav.innerHTML = "";
      cards.innerHTML = "<div class=\"premium-copy-muted\">Chua co cau hoi nao duoc tao.</div>";
      return;
    }

    nav.innerHTML = questions
      .map((_, index) => {
        const label = String(index + 1).padStart(2, "0");
        const classes = ["btn", "btn-outline-light"];
        if (index === 0) {
          classes.push("is-active");
        }
        return `<button class=\"${classes.join(" ")}\" type=\"button\" data-question-button=\"q${index + 1}\">${label}</button>`;
      })
      .join("");

    cards.innerHTML = questions
      .map((question, index) => {
        const options = [
          { key: "A", value: question?.optionA },
          { key: "B", value: question?.optionB },
          { key: "C", value: question?.optionC },
          { key: "D", value: question?.optionD },
        ];
        const optionMarkup = options
          .map(
            (option) => `
              <li class=\"premium-option\">
                <span class=\"premium-option-key\">${option.key}</span>
                <span>${escapeHtml(option.value)}</span>
              </li>
            `,
          )
          .join("");

        const active = index === 0 ? " is-active" : "";
        return `
          <article class=\"premium-question-card${active}\" data-question-card=\"q${index + 1}\">
            <div class=\"premium-question-label\">Question ${String(index + 1).padStart(2, "0")}</div>
            <h3 class=\"premium-question-title\">${escapeHtml(question?.questionText)}</h3>
            <ul class=\"premium-option-list\">${optionMarkup}</ul>
          </article>
        `;
      })
      .join("");
  };

  const buildSummaryItems = (text) => {
    const raw = String(text || "").trim();
    if (!raw) {
      return [];
    }

    const sentences = raw
      .split(/\.(\s+|$)/)
      .map((item) => item.trim())
      .filter(Boolean);

    if (sentences.length === 0) {
      return [raw];
    }

    return sentences.slice(0, 4).map((item) => `${item}.`);
  };

  const splitKeyPoints = (value) =>
    String(value || "")
      .split(/\r?\n|\u2022|\-/)
      .map((item) => item.trim())
      .filter(Boolean);

  const renderDashboardPage = async () => {
    setPageStatus("Dang tai dashboard...", "warning");
    const me = await hydratePremiumUser();

    const { ok, data } = await fetchJson("/api/dashboard/overview");
    if (!ok || !data) {
      setPageStatus("Khong the tai dashboard.", "danger");
      return;
    }

    setText("[data-premium-plan]", "Premium active");
    setText("[data-dashboard-eyebrow]", "Premium Dashboard");
    setText("[data-dashboard-title]", `Chao ${data.greetingName || "ban"}, san sang hoc?`);
    setText("[data-dashboard-subtitle]", data.suggestions?.[0] || "Cap nhat hoat dong hoc tap gan nhat.");
    setText("[data-dashboard-chip=goal]", `Muc tieu tuan: ${data.kpis?.weeklyGoalPercent ?? 0}%`);
    setText("[data-dashboard-chip=sessions]", `Quiz da lam: ${data.kpis?.completedQuizzes ?? 0}`);
    setText("[data-dashboard-chip=weak]", `Chu de yeu: ${data.weakTopics?.length ?? 0}`);

    setText("[data-dashboard-metric=streak]", data.streakDays ?? "0");
    setText("[data-dashboard-metric-meta=streak]", `Tuan nay +${data.streakDelta ?? 0} ngay`);
    setText("[data-dashboard-metric=goal]", `${data.kpis?.weeklyGoalPercent ?? 0}%`);
    setText("[data-dashboard-metric-meta=goal]", "Tien do muc tieu tuan" );
    setText("[data-dashboard-metric=contents]", data.kpis?.totalContents ?? 0);
    setText("[data-dashboard-metric-meta=contents]", `${data.kpis?.totalContentsLast7Days ?? 0} noi dung moi / 7 ngay`);
    setText("[data-dashboard-metric=score]", data.kpis?.averageScoreRecent ?? 0);
    setText("[data-dashboard-metric-meta=score]", "Trung binh 20 bai gan nhat" );

    setText("[data-dashboard-coach=goal]", `${data.kpis?.weeklyGoalPercent ?? 0}%`);
    setText("[data-dashboard-coach-meta=goal]", "Tien do muc tieu" );
    setText("[data-dashboard-coach=score]", data.kpis?.averageScoreRecent ?? 0);
    setText("[data-dashboard-coach-meta=score]", "Diem trung binh gan day" );
    setText("[data-dashboard-coach-callout]", data.suggestions?.[1] || data.suggestions?.[0] || "--");

    const priorityList = document.querySelector("[data-dashboard-priority-list]");
    if (priorityList) {
      const items = Array.isArray(data.recentActivities) ? data.recentActivities : [];
      if (items.length === 0) {
        priorityList.innerHTML = "<div class=\"premium-copy-muted\">Chua co hoat dong gan day.</div>";
      } else {
        priorityList.innerHTML = items.slice(0, 3).map((item, idx) => `
          <div class=\"premium-queue-item\">
            <div class=\"premium-queue-index\">${String(idx + 1).padStart(2, "0")}</div>
            <div>
              <div class=\"premium-line-title\">${escapeHtml(item.title)}</div>
              <div class=\"premium-line-meta\">${escapeHtml(item.kind)} • ${escapeHtml(item.result)}</div>
            </div>
            <div class=\"premium-queue-actions\">
              <span class=\"premium-inline-kpi\">${escapeHtml(item.result)}</span>
              <a class=\"premium-link\" href=\"${item.actionUrl || "#"}\">${escapeHtml(item.actionText || "Mo")}</a>
            </div>
          </div>
        `).join("");
      }
    }

    const topicChart = document.querySelector("[data-dashboard-topic-chart]");
    if (topicChart) {
      const topics = Array.isArray(data.weakTopics) ? data.weakTopics : [];
      if (topics.length === 0) {
        topicChart.innerHTML = "<div class=\"premium-copy-muted\">Chua co du lieu chu de.</div>";
      } else {
        topicChart.innerHTML = topics.map((topic) => `
          <div class=\"premium-chart-row\">
            <div class=\"premium-chart-label\">${escapeHtml(topic.name)}</div>
            <div class=\"premium-chart-bar\"><div class=\"premium-chart-fill\" style=\"width:${topic.accuracyPercent}%;\"></div></div>
            <div class=\"premium-chart-value\">${topic.accuracyPercent}%</div>
          </div>
        `).join("");
      }
    }

    const contentCards = document.querySelector("[data-dashboard-content-cards]");
    if (contentCards) {
      const items = Array.isArray(data.recentActivities) ? data.recentActivities.filter((item) => item.kind !== "Quiz") : [];
      if (items.length === 0) {
        contentCards.innerHTML = "<div class=\"premium-copy-muted\">Chua co noi dung nao.</div>";
      } else {
        contentCards.innerHTML = items.slice(0, 2).map((item) => `
          <article class=\"premium-card\">
            <div class=\"premium-card-body\">
              <h3 class=\"premium-card-title\">${escapeHtml(item.title)}</h3>
              <div class=\"premium-card-copy\">${escapeHtml(item.kind)} • ${escapeHtml(item.result)}</div>
              <div class=\"premium-card-meta\">
                <span class=\"premium-mini-chip\">${formatDate(item.at)}</span>
              </div>
            </div>
          </article>
        `).join("");
      }
    }

    const weeklyList = document.querySelector("[data-dashboard-weekly-list]");
    if (weeklyList) {
      const plan = await fetchJson("/api/dashboard/learning-plan");
      const recommendations = Array.isArray(plan.data?.recommendations) ? plan.data.recommendations : [];
      if (recommendations.length === 0) {
        weeklyList.innerHTML = "<li><span class=\"premium-line-meta\">Chua co de xuat.</span></li>";
      } else {
        weeklyList.innerHTML = recommendations.slice(0, 3).map((item) => `
          <li>
            <strong>${escapeHtml(item)}</strong>
          </li>
        `).join("");
      }
    }

    setText("[data-dashboard-footer]", "Dashboard premium cap nhat theo hoat dong gan nhat.");
    setPageStatus("");
  };

  const renderAnalyticsPage = async () => {
    setPageStatus("Dang tai analytics...", "warning");
    await hydratePremiumUser();
    const { ok, data } = await fetchJson("/api/dashboard/analytics");
    if (!ok || !data) {
      setPageStatus("Khong the tai analytics.", "danger");
      return;
    }

    setText("[data-premium-plan]", "Analytics view" );
    setText("[data-analytics-eyebrow]", "Premium Analytics" );
    setText("[data-analytics-title]", "Tong hop hieu suat hoc tap" );
    setText("[data-analytics-subtitle]", data.suggestions?.[0] || "Cap nhat du lieu gan nhat." );
    setText("[data-analytics-chip=window]", "7 ngay gan nhat" );
    setText("[data-analytics-chip=attempts]", `Quiz 7 ngay: ${data.kpis?.attemptsLast7Days ?? 0}` );
    setText("[data-analytics-chip=wrong]", `Cau sai: ${data.kpis?.wrongAnswersCount ?? 0}` );

    setText("[data-analytics-metric=average]", `${data.kpis?.averageScorePercent ?? 0}%`);
    setText("[data-analytics-metric-meta=average]", `Chenh lech ${data.kpis?.averageScoreDeltaPercent ?? 0}%`);
    setText("[data-analytics-metric=active]", data.kpis?.activeDaysCurrentWeek ?? 0);
    setText("[data-analytics-metric-meta=active]", `Tuan truoc: ${data.kpis?.activeDaysPreviousWeek ?? 0}`);
    setText("[data-analytics-metric=attempts]", data.kpis?.totalAttempts ?? 0);
    setText("[data-analytics-metric-meta=attempts]", "Tong quiz da lam" );
    setText("[data-analytics-metric=wrong]", data.kpis?.wrongAnswersCount ?? 0);
    setText("[data-analytics-metric-meta=wrong]", data.kpis?.consistencyLabel || "" );

    const topicChart = document.querySelector("[data-analytics-topic-chart]");
    if (topicChart) {
      const topics = Array.isArray(data.topicAccuracy) ? data.topicAccuracy : [];
      if (topics.length === 0) {
        topicChart.innerHTML = "<div class=\"premium-copy-muted\">Chua co du lieu chu de.</div>";
      } else {
        topicChart.innerHTML = topics.map((topic) => `
          <div class=\"premium-chart-row\">
            <div class=\"premium-chart-label\">${escapeHtml(topic.topic)}</div>
            <div class=\"premium-chart-bar\"><div class=\"premium-chart-fill\" style=\"width:${topic.accuracyPercent}%;\"></div></div>
            <div class=\"premium-chart-value\">${topic.accuracyPercent}%</div>
          </div>
        `).join("");
      }
    }

    const heatmap = document.querySelector("[data-analytics-heatmap]");
    if (heatmap) {
      const trend = Array.isArray(data.dailyTrend) ? data.dailyTrend : [];
      heatmap.innerHTML = trend.map((item) => {
        const level = item.scorePercent >= 80 ? "level-3" : item.scorePercent >= 60 ? "level-2" : item.scorePercent > 0 ? "level-1" : "";
        return `<div class=\"premium-heat-cell ${level}\"></div>`;
      }).join("");
    }
    setText("[data-analytics-heatmap-note]", data.kpis?.consistencyLabel || "--");

    const suggestionList = document.querySelector("[data-analytics-suggestions]");
    if (suggestionList) {
      const suggestions = Array.isArray(data.suggestions) ? data.suggestions : [];
      if (suggestions.length === 0) {
        suggestionList.innerHTML = "<li><span class=\"premium-line-meta\">Chua co goi y.</span></li>";
      } else {
        suggestionList.innerHTML = suggestions.slice(0, 3).map((item) => `
          <li>
            <span class=\"premium-line-title\">${escapeHtml(item)}</span>
          </li>
        `).join("");
      }
    }

    const trendChart = document.querySelector("[data-analytics-trend]");
    if (trendChart) {
      const trend = Array.isArray(data.dailyTrend) ? data.dailyTrend : [];
      trendChart.innerHTML = trend.map((item) => `
        <div class=\"premium-chart-row\">
          <div class=\"premium-chart-label\">${escapeHtml(item.day)}</div>
          <div class=\"premium-chart-bar\"><div class=\"premium-chart-fill\" style=\"width:${item.scorePercent}%;\"></div></div>
          <div class=\"premium-chart-value\">${item.scorePercent}%</div>
        </div>
      `).join("");
    }

    setText("[data-analytics-footer]", `Cap nhat luc ${formatDate(data.lastUpdatedAt)}`);
    setPageStatus("");
  };

  const renderWorkspacePage = async () => {
    setPageStatus("Dang tai workspace...", "warning");
    await hydratePremiumUser();

    const params = new URLSearchParams(window.location.search);
    const contentId = Number(params.get("contentId") || 0);
    let content = null;

    if (Number.isFinite(contentId) && contentId > 0) {
      const response = await fetchJson(`/api/contents/${contentId}`);
      if (response.ok) {
        content = response.data;
      }
    }

    if (!content) {
      const response = await fetchJson("/api/contents?page=1&pageSize=1");
      const first = response.data?.items?.[0];
      if (first?.contentId) {
        const detail = await fetchJson(`/api/contents/${first.contentId}`);
        if (detail.ok) {
          content = detail.data;
        }
      }
    }

    if (!content) {
      setPageStatus("Chua co noi dung de mo workspace.", "danger");
      return;
    }

    const quizLatest = await fetchJson("/api/quiz/latest");
    const quiz = quizLatest.ok ? quizLatest.data : null;

    const fileName = String(content.fileName || "Noi dung hoc tap").trim();
    const subject = String(content.ai_DetectedSubject || content.aiDetectedSubject || "Noi dung").trim();
    const keyPoints = splitKeyPoints(content.aiProcess?.keyPoints || content.aiProcess?.KeyPoints || "");
    const summaryItems = buildSummaryItems(content.aiProcess?.summary || content.aiProcess?.Summary || "");

    setText("[data-premium-plan]", "Workspace premium" );
    setText("[data-workspace-title]", `Workspace cho ${fileName}`);
    setText("[data-workspace-subtitle]", content.aiProcess?.summary || content.aiProcess?.Summary || "Chua co tom tat." );
    setText("[data-workspace-topic]", `Tai lieu: ${fileName}`);
    setText("[data-workspace-difficulty]", `Do kho: ${formatDifficulty(quiz?.difficulty)}`);
    setText("[data-workspace-goal]", `Chu de: ${subject}`);

    setText("[data-workspace-metric=keypoints]", keyPoints.length || 0);
    setText("[data-workspace-metric-meta=keypoints]", "So key points" );
    setText("[data-workspace-metric=quiz]", quiz?.totalQuestions ?? 0);
    setText("[data-workspace-metric-meta=quiz]", "Tong so cau quiz gan nhat" );
    setText("[data-workspace-metric=created]", formatDate(content.createdAt));
    setText("[data-workspace-metric-meta=created]", "Ngay tao noi dung" );
    setText("[data-workspace-metric=type]", content.sourceType || content.fileType || "--");
    setText("[data-workspace-metric-meta=type]", "Loai noi dung" );

    setLink("[data-workspace-content-link]", `content-detail.html?contentId=${content.contentId}`);
    if (quiz?.quizId) {
      setLink("[data-workspace-quiz-link]", `quiz-experience.html?quizId=${quiz.quizId}`);
    }

    const summaryList = document.querySelector("[data-workspace-summary]");
    if (summaryList) {
      const items = summaryItems.length > 0 ? summaryItems : ["Chua co tom tat."];
      summaryList.innerHTML = items.map((item) => `
        <li>
          <strong>${escapeHtml(item)}</strong>
        </li>
      `).join("");
    }

    const keypointList = document.querySelector("[data-workspace-keypoints]");
    if (keypointList) {
      const items = keyPoints.length > 0 ? keyPoints : ["Chua co key points."];
      keypointList.innerHTML = items.map((item) => `
        <li>
          <strong>${escapeHtml(item)}</strong>
        </li>
      `).join("");
    }

    const steps = document.querySelector("[data-workspace-steps]");
    if (steps) {
      const plan = await fetchJson("/api/dashboard/learning-plan");
      const tasks = Array.isArray(plan.data?.tasks) ? plan.data.tasks : [];
      if (tasks.length === 0) {
        steps.innerHTML = "<li><span class=\"premium-line-meta\">Chua co ke hoach.</span></li>";
      } else {
        steps.innerHTML = tasks.slice(0, 3).map((task, idx) => `
          <li>
            <span class=\"premium-line-title\">${idx + 1}. ${escapeHtml(task.title)}</span>
            <span class=\"premium-line-meta\">${escapeHtml(task.detail)}</span>
          </li>
        `).join("");
      }
    }

    setText("[data-workspace-callout]", "Hoan thanh mot buoc va chuyen sang quiz khi san sang." );

    const quizList = document.querySelector("[data-workspace-quiz-list]");
    if (quizList) {
      if (!quiz) {
        quizList.innerHTML = "<li><span class=\"premium-line-meta\">Chua co quiz gan day.</span></li>";
      } else {
        quizList.innerHTML = `
          <li>
            <strong>${quiz.totalQuestions} cau trac nghiem</strong>
            <span class=\"premium-line-meta\">Do kho ${formatDifficulty(quiz.difficulty)}</span>
          </li>
        `;
      }
    }

    setText("[data-workspace-source-note]", content.aiProcess?.summary || content.aiProcess?.Summary || "Chua co tom tat noi dung." );
    setImage("[data-workspace-source-image]", "", "");
    setText("[data-workspace-footer]", "Workspace premium cap nhat theo noi dung gan nhat." );
    setPageStatus("");
  };

  const hydratePremiumQuiz = async () => {
    try {
      await hydratePremiumUser();

      const params = new URLSearchParams(window.location.search);
      const quizId = Number(params.get("quizId") || 0);
      const headers = buildAuthHeaders();

      setQuizStatus("Dang tai du lieu quiz...", "warning");
      const quizResponse = await fetch(
        Number.isFinite(quizId) && quizId > 0 ? `/api/quiz/${quizId}` : "/api/quiz/latest",
        {
        method: "GET",
        cache: "no-store",
        credentials: "include",
        headers,
        },
      );

      if (!quizResponse.ok) {
        const message = quizResponse.status === 404
          ? "Chua co quiz nao. Hay tao quiz tu noi dung hoc tap truoc."
          : "Khong the tai quiz. Vui long thu lai.";
        setQuizStatus(message, "danger");
        return;
      }

      const quiz = await quizResponse.json().catch(() => null);
      if (!quiz) {
        setQuizStatus("Khong co du lieu quiz hop le.", "danger");
        return;
      }

      let content = null;
      if (quiz.contentId) {
        const contentResponse = await fetch(`/api/contents/${quiz.contentId}`, {
          method: "GET",
          cache: "no-store",
          credentials: "include",
          headers,
        });
        if (contentResponse.ok) {
          content = await contentResponse.json().catch(() => null);
        }
      }

      const sourceUrl = String(content?.sourceUrl || content?.SourceUrl || "").trim();
      const subject = String(
        content?.aiDetectedSubject ||
        content?.ai_DetectedSubject ||
        content?.ai_detectedSubject ||
        content?.fileName ||
        "Noi dung",
      ).trim();
      const fileName = String(content?.fileName || content?.FileName || "Noi dung hoc tap").trim();
      const summary = String(
        content?.aiProcess?.summary ||
        content?.aiProcess?.Summary ||
        content?.AiProcess?.summary ||
        content?.AiProcess?.Summary ||
        "",
      ).trim();

      const imageUrl = (/\.(png|jpg|jpeg|gif|webp)$/i).test(sourceUrl) ? sourceUrl : "";

      setText("[data-quiz-plan]", `Quiz #${quiz.quizId} • ${quiz.totalQuestions} cau • ${formatDifficulty(quiz.difficulty)}`);
      setText("[data-quiz-eyebrow]", `Premium Quiz • ${subject}`);
      setText("[data-quiz-title]", `Quiz tu ${fileName}`);
      setText("[data-quiz-subtitle]", summary || "Bo de duoc tao tu noi dung cua ban.");

      setText("[data-quiz-topic]", `Chuyen de: ${subject}`);
      setText("[data-quiz-difficulty]", `Do kho: ${formatDifficulty(quiz.difficulty)}`);
      setText("[data-quiz-count]", `Tong cau: ${quiz.totalQuestions}`);

      setText("[data-quiz-metric=total]", quiz.totalQuestions);
      setText("[data-quiz-metric=difficulty]", formatDifficulty(quiz.difficulty));
      setText("[data-quiz-metric=type]", formatQuizType(quiz.quizType));
      setText("[data-quiz-metric=created]", formatDate(quiz.createdAt));

      setText("[data-content-name]", fileName);
      setText("[data-content-subject]", subject);
      setText("[data-content-quiz-count]", content?.quizCount ? `${content.quizCount} bo` : "0 bo");

      const summaryBox = document.querySelector("[data-quiz-summary-box]");
      if (summaryBox) {
        if (summary) {
          summaryBox.style.display = "block";
          setText("[data-quiz-summary]", summary);
        } else {
          summaryBox.style.display = "none";
        }
      }

      setText("[data-quiz-caption]", `Noi dung: ${fileName}`);
      setText("[data-quiz-footer]", `Quiz #${quiz.quizId} tao luc ${formatDate(quiz.createdAt)} tu ${fileName}.`);

      setImage("[data-quiz-image]", imageUrl, fileName);

      setLink("[data-quiz-content-link]", `content-detail.html?contentId=${quiz.contentId}`);
      setLink("[data-quiz-result-link]", "quiz-result.html");

      renderQuestions(quiz.questions || []);
      setQuizStatus("");
    } catch {
      setQuizStatus("Co loi khi dong bo du lieu quiz.", "danger");
    }
  };

  const renderPremiumSidebar = (currentPage) => {
    const groupsMarkup = PREMIUM_SIDEBAR_GROUPS.map((group) => {
      const linksMarkup = group.links.map((link) => {
        const activeClass = link.section === currentPage ? " is-active" : "";
        return `
          <a class="premium-sidebar-link${activeClass}" href="${link.href}" data-premium-sidebar-link>
            <span class="premium-sidebar-icon">${link.short}</span>
            <span class="premium-sidebar-copy">
              <span class="premium-sidebar-link-label">${link.label}</span>
              <span class="premium-sidebar-link-meta">${link.meta}</span>
            </span>
          </a>
        `;
      }).join("");

      return `
        <section class="premium-sidebar-group">
          <div class="premium-sidebar-group-title">${group.title}</div>
          ${linksMarkup}
        </section>
      `;
    }).join("");

    return `
      <div class="premium-sidebar-head">
        <div>
          <div class="premium-sidebar-eyebrow">Premium navigation</div>
          <div class="premium-sidebar-title">Student workspace</div>
        </div>
        <button
          type="button"
          class="premium-sidebar-close"
          aria-label="Đóng menu premium"
          data-premium-sidebar-close
        >
          <span></span>
          <span></span>
        </button>
      </div>
      <div class="premium-sidebar-body">
        ${groupsMarkup}
      </div>
      <div class="premium-sidebar-footer">
        <div class="premium-sidebar-status">Premium active</div>
        <p>Không gian điều hướng riêng cho user học sinh, sinh viên đã có gói.</p>
      </div>
    `;
  };

  const mountPremiumSidebar = (currentPage) => {
    const topbar = document.querySelector(".premium-topbar-inner");
    const brand = topbar?.querySelector(".premium-brand");
    if (topbar && brand && !topbar.querySelector(".premium-start-cluster")) {
      const cluster = document.createElement("div");
      cluster.className = "premium-start-cluster";
      const toggle = document.createElement("button");
      toggle.type = "button";
      toggle.className = "premium-menu-toggle";
      toggle.setAttribute("aria-label", "Mở menu premium");
      toggle.setAttribute("aria-controls", PREMIUM_SIDEBAR_ID);
      toggle.setAttribute("aria-expanded", "false");
      toggle.innerHTML = `
        <span></span>
        <span></span>
        <span></span>
      `;

      brand.before(cluster);
      cluster.appendChild(toggle);
      cluster.appendChild(brand);
    }

    let sidebar = document.getElementById(PREMIUM_SIDEBAR_ID);
    if (!sidebar) {
      sidebar = document.createElement("aside");
      sidebar.id = PREMIUM_SIDEBAR_ID;
      document.body.appendChild(sidebar);
    }
    sidebar.innerHTML = renderPremiumSidebar(currentPage);

    let backdrop = document.getElementById(PREMIUM_SIDEBAR_BACKDROP_ID);
    if (!backdrop) {
      backdrop = document.createElement("div");
      backdrop.id = PREMIUM_SIDEBAR_BACKDROP_ID;
      document.body.appendChild(backdrop);
    }

    const toggles = document.querySelectorAll(".premium-menu-toggle");
    const closeSidebar = () => {
      sidebar.classList.remove("open");
      backdrop.classList.remove("show");
      document.body.classList.remove("is-sidebar-open");
      toggles.forEach((toggle) => {
        toggle.setAttribute("aria-expanded", "false");
      });
    };

    const openSidebar = () => {
      sidebar.classList.add("open");
      backdrop.classList.add("show");
      document.body.classList.add("is-sidebar-open");
      toggles.forEach((toggle) => {
        toggle.setAttribute("aria-expanded", "true");
      });
    };

    toggles.forEach((toggle) => {
      toggle.addEventListener("click", () => {
        if (sidebar.classList.contains("open")) {
          closeSidebar();
          return;
        }

        openSidebar();
      });
    });

    backdrop.addEventListener("click", closeSidebar);
    sidebar.querySelector("[data-premium-sidebar-close]")?.addEventListener("click", closeSidebar);
    sidebar.querySelectorAll("[data-premium-sidebar-link]").forEach((link) => {
      link.addEventListener("click", closeSidebar);
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && sidebar.classList.contains("open")) {
        closeSidebar();
      }
    });
  };

  applyBootstrapSkin();

  const currentPage = document.body.dataset.page || "";
  mountPremiumSidebar(currentPage);

  document.querySelectorAll("[data-page-link]").forEach((link) => {
    if (link.dataset.pageLink === currentPage) {
      link.classList.add("is-active");
    }
  });

  document.querySelectorAll("[data-chip-group]").forEach((group) => {
    group.addEventListener("click", (event) => {
      const target = event.target.closest("[data-chip]");
      if (!target) {
        return;
      }

      group.querySelectorAll("[data-chip]").forEach((chip) => {
        chip.classList.toggle("is-active", chip === target);
      });
    });
  });

  document.querySelectorAll("[data-tab-group]").forEach((group) => {
    const buttons = group.querySelectorAll("[data-tab-button]");
    const panels = group.querySelectorAll("[data-tab-panel]");

    group.addEventListener("click", (event) => {
      const button = event.target.closest("[data-tab-button]");
      if (!button) {
        return;
      }

      const targetId = button.dataset.tabButton;
      buttons.forEach((item) => {
        item.classList.toggle("is-active", item === button);
      });

      panels.forEach((panel) => {
        panel.classList.toggle("is-active", panel.dataset.tabPanel === targetId);
      });
    });
  });

  const questionGroup = document.querySelector("[data-question-group]");
  if (questionGroup) {
    questionGroup.addEventListener("click", (event) => {
      const button = event.target.closest("[data-question-button]");
      if (!button) {
        return;
      }

      const buttons = questionGroup.querySelectorAll("[data-question-button]");
      const cards = document.querySelectorAll("[data-question-card]");
      const questionId = button.dataset.questionButton;
      buttons.forEach((item) => {
        item.classList.toggle("is-active", item === button);
      });

      cards.forEach((card) => {
        card.classList.toggle("is-active", card.dataset.questionCard === questionId);
      });
    });
  }

  document.querySelectorAll("[data-plan-item]").forEach((item) => {
    const toggle = item.querySelector("[data-plan-toggle]");
    if (!toggle) {
      return;
    }

    toggle.addEventListener("click", () => {
      item.classList.toggle("is-done");
    });
  });

  void hydratePremiumQuiz();
})();
