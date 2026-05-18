(() => {
  const ensureStylesheet = (href) => {
    const absoluteHref = new URL(href, window.location.origin).href;
    const exists = Array.from(document.querySelectorAll('link[rel="stylesheet"]'))
      .some((link) => link.href === absoluteHref);
    if (exists) {
      return;
    }

    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = href;
    document.head.appendChild(link);
  };

  const loadScriptOnce = (src, isLoaded) => new Promise((resolve, reject) => {
    if (isLoaded()) {
      resolve();
      return;
    }

    const absoluteSrc = new URL(src, window.location.origin).href;
    const existing = Array.from(document.scripts).find((script) => script.src === absoluteSrc);
    if (existing) {
      existing.addEventListener("load", () => resolve(), { once: true });
      existing.addEventListener("error", () => reject(new Error(`Không tải được ${src}`)), { once: true });
      return;
    }

    const script = document.createElement("script");
    script.src = src;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Không tải được ${src}`));
    document.body.appendChild(script);
  });

  const mountStandardShell = async () => {
    ensureStylesheet("/css/app-shell.css");
    try {
      await loadScriptOnce("/js/auth.js?v=20260511-1", () => Boolean(window.AuthClient));
      await loadScriptOnce("/js/app-shell.js?v=20260515-smoothnav", () => Boolean(window.AppShell?.mount));
      await window.AppShell?.mount?.();
    } catch {
      // Premium content remains usable if the shared shell cannot be mounted.
    }
  };

  void mountStandardShell();

  const PREMIUM_SIDEBAR_ID = "premium-sidebar";
  const PREMIUM_SIDEBAR_BACKDROP_ID = "premium-sidebar-backdrop";
  const PREMIUM_SIDEBAR_GROUPS = [
    {
      title: "Trung tâm điều phối",
      links: [
        { section: "dashboard", href: "dashboard.html", label: "Dashboard", meta: "Tổng quan ưu tiên hôm nay", short: "DB" },
        { section: "workspace", href: "study-workspace.html", label: "Không gian học", meta: "Phiên học sâu đang chạy", short: "WS" },
      ],
    },
    {
      title: "Nội dung",
      links: [
        { section: "library", href: "content-library.html", label: "Thư viện", meta: "Kho tài liệu và bộ lọc học tập", short: "LB" },
        { section: "detail", href: "content-detail.html", label: "Chi tiết", meta: "Tóm tắt, metadata và bước tiếp", short: "DT" },
      ],
    },
    {
      title: "Luyện tập",
      links: [
        { section: "quiz", href: "quiz-experience.html", label: "Bài kiểm tra", meta: "Chế độ tập trung cho phiên làm bài", short: "QZ" },
        { section: "result", href: "quiz-result.html", label: "Ôn tập", meta: "Đúng sai, lỗi lặp và gợi ý ôn", short: "RV" },
      ],
    },
    {
      title: "Chiến lược",
      links: [
        { section: "analytics", href: "analytics.html", label: "Phân tích", meta: "Insight hiệu suất có thể hành động", short: "AN" },
        { section: "plan", href: "learning-plan.html", label: "Kế hoạch học", meta: "Lộ trình ngày và tuần", short: "LP" },
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

  const nf = new Intl.NumberFormat("vi-VN");

  const formatDifficulty = (value) => {
    const normalized = String(value || "").trim().toLowerCase();
    if (normalized === "easy") return "Dễ";
    if (normalized === "hard") return "Khó";
    return "Trung bình";
  };

  const formatQuizType = (value) => {
    const normalized = String(value || "").trim().toLowerCase();
    if (normalized === "multiple-choice") return "Trắc nghiệm";
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
      return "Người dùng";
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

  const toTierLabel = (status) => {
    if (!status) return "Đang đồng bộ gói...";
    if (status.isPremium) {
      return status.subscriptionTier ? String(status.subscriptionTier).trim() : "Premium";
    }
    return "Gói thường";
  };

  const hydratePremiumTier = async () => {
    const { ok, data } = await fetchJson("/api/premium/status");
    if (!ok || !data) {
      return null;
    }

    const tierLabel = toTierLabel(data);
    document.querySelectorAll("[data-premium-plan]").forEach((node) => {
      node.textContent = tierLabel;
    });
    document.querySelectorAll("[data-premium-tier]").forEach((node) => {
      node.textContent = tierLabel;
    });
    document.querySelectorAll(".premium-sidebar-status").forEach((node) => {
      node.textContent = tierLabel;
    });

    document.body.classList.toggle("is-premium-active", Boolean(data.isPremium));
    document.querySelectorAll("[data-normal-only]").forEach((node) => {
      node.hidden = Boolean(data.isPremium);
    });
    document.querySelectorAll("[data-premium-only]").forEach((node) => {
      node.hidden = !data.isPremium;
    });

    if (data.isPremium) {
      document.querySelectorAll('[data-page-link="upgrade"]').forEach((link) => {
        const parent = link.closest("li");
        if (parent) {
          parent.remove();
          return;
        }
        link.remove();
      });

      document.querySelectorAll('#premium-sidebar a[href="upgrade.html"]').forEach((link) => {
        link.remove();
      });

      if (document.body.dataset.page === "upgrade") {
        window.location.replace("account.html");
      }
    }

    return data;
  };

  const renderQuestions = (questions) => {
    const nav = document.querySelector("[data-quiz-nav]");
    const cards = document.querySelector("[data-quiz-cards]");
    if (!nav || !cards) {
      return;
    }

    if (!Array.isArray(questions) || questions.length === 0) {
      nav.innerHTML = "";
      cards.innerHTML = "<div class=\"premium-copy-muted\">Chưa có câu hỏi nào được tạo.</div>";
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
              <li class="premium-option">
                <span class="premium-option-key">${option.key}</span>
                <span>${escapeHtml(option.value)}</span>
              </li>
            `,
          )
          .join("");

        const active = index === 0 ? " is-active" : "";
        return `
          <article class="premium-question-card${active}" data-question-card="q${index + 1}">
            <div class="premium-question-label">Question ${String(index + 1).padStart(2, "0")}</div>
            <h3 class="premium-question-title">${escapeHtml(question?.questionText)}</h3>
            <ul class="premium-option-list">${optionMarkup}</ul>
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
    setPageStatus("Đang tải dashboard...", "warning");
    const me = await hydratePremiumUser();
    await hydratePremiumTier();

    const { ok, data } = await fetchJson("/api/dashboard/overview");
    if (!ok || !data) {
      setPageStatus("Không thể tải dashboard.", "danger");
      return;
    }

    setText("[data-dashboard-eyebrow]", "Premium Dashboard");
    setText("[data-dashboard-title]", `Chào ${data.greetingName || "bạn"}, sẵn sàng học?`);
    setText("[data-dashboard-subtitle]", data.suggestions?.[0] || "Cập nhật hoạt động học tập gần nhất.");
    setText("[data-dashboard-chip=goal]", `Mục tiêu tuần: ${data.kpis?.weeklyGoalPercent ?? 0}%`);
    setText("[data-dashboard-chip=sessions]", `Quiz đã làm: ${data.kpis?.completedQuizzes ?? 0}`);
    setText("[data-dashboard-chip=weak]", `Chủ đề yếu: ${data.weakTopics?.length ?? 0}`);

    setText("[data-dashboard-metric=streak]", data.streakDays ?? "0");
    setText("[data-dashboard-metric-meta=streak]", `Tuần này +${data.streakDelta ?? 0} ngày`);
    setText("[data-dashboard-metric=goal]", `${data.kpis?.weeklyGoalPercent ?? 0}%`);
    setText("[data-dashboard-metric-meta=goal]", "Tiến độ mục tiêu tuần" );
    setText("[data-dashboard-metric=contents]", data.kpis?.totalContents ?? 0);
    setText("[data-dashboard-metric-meta=contents]", `${data.kpis?.totalContentsLast7Days ?? 0} noi dung moi / 7 ngày`);
    setText("[data-dashboard-metric=score]", data.kpis?.averageScoreRecent ?? 0);
    setText("[data-dashboard-metric-meta=score]", "Trung bình 20 bài gần nhất" );

    setText("[data-dashboard-coach=goal]", `${data.kpis?.weeklyGoalPercent ?? 0}%`);
    setText("[data-dashboard-coach-meta=goal]", "Tiến độ mục tiêu" );
    setText("[data-dashboard-coach=score]", data.kpis?.averageScoreRecent ?? 0);
    setText("[data-dashboard-coach-meta=score]", "Điểm trung bình gần đây" );
    setText("[data-dashboard-coach-callout]", data.suggestions?.[1] || data.suggestions?.[0] || "--");

    const priorityList = document.querySelector("[data-dashboard-priority-list]");
    if (priorityList) {
      const items = Array.isArray(data.recentActivities) ? data.recentActivities : [];
      if (items.length === 0) {
        priorityList.innerHTML = "<div class=\"premium-copy-muted\">Chưa có hoạt động gần đây.</div>";
      } else {
        priorityList.innerHTML = items.slice(0, 3).map((item, idx) => `
          <div class="premium-queue-item">
            <div class="premium-queue-index">${String(idx + 1).padStart(2, "0")}</div>
            <div>
              <div class="premium-line-title">${escapeHtml(item.title)}</div>
              <div class="premium-line-meta">${escapeHtml(item.kind)} • ${escapeHtml(item.result)}</div>
            </div>
            <div class="premium-queue-actions">
              <span class="premium-inline-kpi">${escapeHtml(item.result)}</span>
              <a class="premium-link" href="${item.actionUrl || "#"}">${escapeHtml(item.actionText || "Mở")}</a>
            </div>
          </div>
        `).join("");
      }
    }

    const topicChart = document.querySelector("[data-dashboard-topic-chart]");
    if (topicChart) {
      const topics = Array.isArray(data.weakTopics) ? data.weakTopics : [];
      if (topics.length === 0) {
        topicChart.innerHTML = "<div class=\"premium-copy-muted\">Chưa có dữ liệu chủ đề.</div>";
      } else {
        topicChart.innerHTML = topics.map((topic) => `
          <div class="premium-chart-row">
            <div class="premium-chart-label">${escapeHtml(topic.name)}</div>
            <div class="premium-chart-bar"><div class="premium-chart-fill" style="width:${topic.accuracyPercent}%;"></div></div>
            <div class="premium-chart-value">${topic.accuracyPercent}%</div>
          </div>
        `).join("");
      }
    }

    const contentCards = document.querySelector("[data-dashboard-content-cards]");
    if (contentCards) {
      const items = Array.isArray(data.recentActivities) ? data.recentActivities.filter((item) => item.kind !== "Quiz") : [];
      if (items.length === 0) {
        contentCards.innerHTML = "<div class=\"premium-copy-muted\">Chưa có nội dung nào.</div>";
      } else {
        contentCards.innerHTML = items.slice(0, 2).map((item) => `
          <article class="premium-card">
            <div class="premium-card-body">
              <h3 class="premium-card-title">${escapeHtml(item.title)}</h3>
              <div class="premium-card-copy">${escapeHtml(item.kind)} • ${escapeHtml(item.result)}</div>
              <div class="premium-card-meta">
                <span class="premium-mini-chip">${formatDate(item.at)}</span>
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
        weeklyList.innerHTML = "<li><span class=\"premium-line-meta\">Chưa có đề xuất.</span></li>";
      } else {
        weeklyList.innerHTML = recommendations.slice(0, 3).map((item) => `
          <li>
            <strong>${escapeHtml(item)}</strong>
          </li>
        `).join("");
      }
    }

    setText("[data-dashboard-footer]", "Dashboard Premium cập nhật theo hoạt động gần nhất.");
    setPageStatus("");
  };

  const renderAnalyticsPage = async () => {
    setPageStatus("Đang tải analytics...", "warning");
    await hydratePremiumUser();
    await hydratePremiumTier();
    const { ok, data } = await fetchJson("/api/dashboard/analytics");
    if (!ok || !data) {
      setPageStatus("Không thể tải analytics.", "danger");
      return;
    }

    setText("[data-analytics-eyebrow]", "Premium Analytics" );
    setText("[data-analytics-title]", "Tổng hợp hiệu suất học tập" );
    setText("[data-analytics-subtitle]", data.suggestions?.[0] || "Cập nhật dữ liệu gần nhất." );
    setText("[data-analytics-chip=window]", "7 ngày gần nhất" );
    setText("[data-analytics-chip=attempts]", `Quiz 7 ngày: ${data.kpis?.attemptsLast7Days ?? 0}` );
    setText("[data-analytics-chip=wrong]", `Câu sai: ${data.kpis?.wrongAnswersCount ?? 0}` );

    setText("[data-analytics-metric=average]", `${data.kpis?.averageScorePercent ?? 0}%`);
    setText("[data-analytics-metric-meta=average]", `Chênh lệch ${data.kpis?.averageScoreDeltaPercent ?? 0}%`);
    setText("[data-analytics-metric=active]", data.kpis?.activeDaysCurrentWeek ?? 0);
    setText("[data-analytics-metric-meta=active]", `Tuần trước: ${data.kpis?.activeDaysPreviousWeek ?? 0}`);
    setText("[data-analytics-metric=attempts]", data.kpis?.totalAttempts ?? 0);
    setText("[data-analytics-metric-meta=attempts]", "Tổng quiz đã làm" );
    setText("[data-analytics-metric=wrong]", data.kpis?.wrongAnswersCount ?? 0);
    setText("[data-analytics-metric-meta=wrong]", data.kpis?.consistencyLabel || "" );

    const topicChart = document.querySelector("[data-analytics-topic-chart]");
    if (topicChart) {
      const topics = Array.isArray(data.topicAccuracy) ? data.topicAccuracy : [];
      if (topics.length === 0) {
        topicChart.innerHTML = "<div class=\"premium-copy-muted\">Chưa có dữ liệu chủ đề.</div>";
      } else {
        topicChart.innerHTML = topics.map((topic) => `
          <div class="premium-chart-row">
            <div class="premium-chart-label">${escapeHtml(topic.topic)}</div>
            <div class="premium-chart-bar"><div class="premium-chart-fill" style="width:${topic.accuracyPercent}%;"></div></div>
            <div class="premium-chart-value">${topic.accuracyPercent}%</div>
          </div>
        `).join("");
      }
    }

    const heatmap = document.querySelector("[data-analytics-heatmap]");
    if (heatmap) {
      const trend = Array.isArray(data.dailyTrend) ? data.dailyTrend : [];
      heatmap.innerHTML = trend.map((item) => {
        const level = item.scorePercent >= 80 ? "level-3" : item.scorePercent >= 60 ? "level-2" : item.scorePercent > 0 ? "level-1" : "";
        return `<div class="premium-heat-cell ${level}\"></div>`;
      }).join("");
    }
    setText("[data-analytics-heatmap-note]", data.kpis?.consistencyLabel || "--");

    const suggestionList = document.querySelector("[data-analytics-suggestions]");
    if (suggestionList) {
      const suggestions = Array.isArray(data.suggestions) ? data.suggestions : [];
      if (suggestions.length === 0) {
        suggestionList.innerHTML = "<li><span class=\"premium-line-meta\">Chưa có gợi ý.</span></li>";
      } else {
        suggestionList.innerHTML = suggestions.slice(0, 3).map((item) => `
          <li>
            <span class="premium-line-title">${escapeHtml(item)}</span>
          </li>
        `).join("");
      }
    }

    const trendChart = document.querySelector("[data-analytics-trend]");
    if (trendChart) {
      const trend = Array.isArray(data.dailyTrend) ? data.dailyTrend : [];
      trendChart.innerHTML = trend.map((item) => `
        <div class="premium-chart-row">
          <div class="premium-chart-label">${escapeHtml(item.day)}</div>
          <div class="premium-chart-bar"><div class="premium-chart-fill" style="width:${item.scorePercent}%;"></div></div>
          <div class="premium-chart-value">${item.scorePercent}%</div>
        </div>
      `).join("");
    }

    setText("[data-analytics-footer]", `Cập nhật lúc ${formatDate(data.lastUpdatedAt)}`);
    setPageStatus("");
  };

  const renderWorkspacePage = async () => {
    setPageStatus("Đang tải workspace...", "warning");
    await hydratePremiumUser();
    await hydratePremiumTier();

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
      setPageStatus("Chưa có nội dung để mở workspace.", "danger");
      return;
    }

    const quizLatest = await fetchJson("/api/quiz/latest");
    const quiz = quizLatest.ok ? quizLatest.data : null;

    const fileName = String(content.fileName || "Nội dung học tập").trim();
    const subject = String(content.ai_DetectedSubject || content.aiDetectedSubject || "Nội dung").trim();
    const keyPoints = splitKeyPoints(content.aiProcess?.keyPoints || content.aiProcess?.KeyPoints || "");
    const summaryItems = buildSummaryItems(content.aiProcess?.summary || content.aiProcess?.Summary || "");

    setText("[data-workspace-title]", `Workspace cho ${fileName}`);
    setText("[data-workspace-subtitle]", content.aiProcess?.summary || content.aiProcess?.Summary || "Chưa có tóm tắt." );
    setText("[data-workspace-topic]", `Tài liệu: ${fileName}`);
    setText("[data-workspace-difficulty]", `Độ khó: ${formatDifficulty(quiz?.difficulty)}`);
    setText("[data-workspace-goal]", `Chủ đề: ${subject}`);

    setText("[data-workspace-metric=keypoints]", keyPoints.length || 0);
    setText("[data-workspace-metric-meta=keypoints]", "Số key points" );
    setText("[data-workspace-metric=quiz]", quiz?.totalQuestions ?? 0);
    setText("[data-workspace-metric-meta=quiz]", "Tổng số câu quiz gần nhất" );
    setText("[data-workspace-metric=created]", formatDate(content.createdAt));
    setText("[data-workspace-metric-meta=created]", "Ngày tạo nội dung" );
    setText("[data-workspace-metric=type]", content.sourceType || content.fileType || "--");
    setText("[data-workspace-metric-meta=type]", "Loại nội dung" );

    setLink("[data-workspace-content-link]", `content-detail.html?contentId=${content.contentId}`);
    if (quiz?.quizId) {
      setLink("[data-workspace-quiz-link]", `quiz-experience.html?quizId=${quiz.quizId}`);
    }

    const summaryList = document.querySelector("[data-workspace-summary]");
    if (summaryList) {
      const items = summaryItems.length > 0 ? summaryItems : ["Chưa có tóm tắt."];
      summaryList.innerHTML = items.map((item) => `
        <li>
          <strong>${escapeHtml(item)}</strong>
        </li>
      `).join("");
    }

    const keypointList = document.querySelector("[data-workspace-keypoints]");
    if (keypointList) {
      const items = keyPoints.length > 0 ? keyPoints : ["Chưa có key points."];
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
        steps.innerHTML = "<li><span class=\"premium-line-meta\">Chưa có kế hoạch.</span></li>";
      } else {
        steps.innerHTML = tasks.slice(0, 3).map((task, idx) => `
          <li>
            <span class="premium-line-title">${idx + 1}. ${escapeHtml(task.title)}</span>
            <span class="premium-line-meta">${escapeHtml(task.detail)}</span>
          </li>
        `).join("");
      }
    }

    setText("[data-workspace-callout]", "Hoàn thành một bước và chuyển sang quiz khi sẵn sàng." );

    const quizList = document.querySelector("[data-workspace-quiz-list]");
    if (quizList) {
      if (!quiz) {
        quizList.innerHTML = "<li><span class=\"premium-line-meta\">Chưa có quiz gần đây.</span></li>";
      } else {
        quizList.innerHTML = `
          <li>
            <strong>${quiz.totalQuestions} câu trắc nghiệm</strong>
            <span class="premium-line-meta">Độ khó ${formatDifficulty(quiz.difficulty)}</span>
          </li>
        `;
      }
    }

    setText("[data-workspace-source-note]", content.aiProcess?.summary || content.aiProcess?.Summary || "Chưa có tóm tắt nội dung." );
    setImage("[data-workspace-source-image]", "", "");
    setText("[data-workspace-footer]", "Workspace Premium cập nhật theo nội dung gần nhất." );
    setPageStatus("");
  };

  const renderLibraryPage = async () => {
    setPageStatus("Đang tải thư viện...", "warning");
    await hydratePremiumUser();
    await hydratePremiumTier();

    const { ok, data } = await fetchJson("/api/contents?page=1&pageSize=6&sort=latest");
    if (!ok || !data) {
      setPageStatus("Không thể tải thư viện nội dung.", "danger");
      return;
    }

    const items = Array.isArray(data.items) ? data.items : [];
    const totalItems = Number(data.totalItems ?? items.length ?? 0);

    const subjects = new Set(
      items
        .map((item) => String(item?.ai_DetectedSubject || item?.aiDetectedSubject || "").trim())
        .filter(Boolean),
    );

    const withQuiz = items.filter((item) => Number(item?.quizCount || 0) > 0).length;
    const hasAiCount = items.filter((item) => Boolean(item?.hasAiProcess)).length;
    const coverage = items.length > 0 ? Math.round((withQuiz / items.length) * 100) : 0;
    const reviewQueue = items.filter((item) => !item?.hasAiProcess).length;

    setText("[data-library-meta]", `${totalItems} nội dung • Trang ${Number(data.page || 1)}/${Number(data.totalPages || 1)}`);
    setText("[data-library-metric=total]", nf.format(totalItems), "0");
    setText("[data-library-metric=review]", nf.format(reviewQueue), "0");
    setText("[data-library-metric=subjects]", nf.format(subjects.size), "0");
    setText("[data-library-metric=coverage]", `${coverage}%`, "0%");

    const grid = document.querySelector("[data-library-items]");
    if (grid) {
      if (items.length === 0) {
        grid.innerHTML = "<div class=\"premium-copy-muted\">Chưa có nội dung. Hãy upload tài liệu ở khu user thường trước.</div>";
      } else {
        grid.innerHTML = items.slice(0, 4).map((item) => {
          const id = Number(item?.contentId || 0);
          const fileName = escapeHtml(item?.fileName || "Nội dung chưa đặt tên");
          const sourceType = escapeHtml(item?.sourceType || "");
          const createdAt = formatDate(item?.createdAt);
          const quizCount = Number(item?.quizCount || 0);
          const hasAi = Boolean(item?.hasAiProcess);
          const statusChip = hasAi ? "Hoàn tất" : "Đang xử lý";
          const quizChip = quizCount > 0 ? `${quizCount} bộ quiz` : "Chưa có quiz";
          return `
            <article class="premium-library-item">
              <div class="premium-library-copy">
                <h3>${fileName}</h3>
                <p>${sourceType ? `${sourceType} • ` : ""}Tạo lúc ${createdAt}</p>
                <div class="premium-library-footer">
                  <span class="premium-mini-chip">${statusChip}</span>
                  <span class="premium-mini-chip">${quizChip}</span>
                  <a class="premium-link" href="content-detail.html?contentId=${id}">Xem detail</a>
                </div>
              </div>
            </article>
          `;
        }).join("");
      }
    }

    const queue = document.querySelector("[data-library-queue]");
    if (queue) {
      if (items.length === 0) {
        queue.innerHTML = "<div class=\"premium-copy-muted\">Chưa có gợi ý review.</div>";
      } else {
        queue.innerHTML = items.slice(0, 3).map((item, idx) => {
          const id = Number(item?.contentId || 0);
          const fileName = escapeHtml(item?.fileName || "Nội dung");
          const key = String.fromCharCode(65 + idx) + String(idx + 1);
          const meta = item?.hasAiProcess ? "Đã xử lý AI" : "Đang xử lý";
          return `
            <div class="premium-queue-item">
              <div class="premium-queue-index">${key}</div>
              <div>
                <div class="premium-line-title">${fileName}</div>
                <div class="premium-line-meta">${meta}</div>
              </div>
              <a class="premium-link" href="content-detail.html?contentId=${id}">Mở</a>
            </div>
          `;
        }).join("");
      }
    }

    setPageStatus("");
  };

  const renderDetailPage = async () => {
    setPageStatus("Đang tải chi tiết...", "warning");
    await hydratePremiumUser();
    await hydratePremiumTier();

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
      const response = await fetchJson("/api/contents?page=1&pageSize=1&sort=latest");
      const first = response.data?.items?.[0];
      if (first?.contentId) {
        const detail = await fetchJson(`/api/contents/${first.contentId}`);
        if (detail.ok) {
          content = detail.data;
        }
      }
    }

    if (!content) {
      setPageStatus("Chưa có nội dung để hiển thị detail.", "danger");
      return;
    }

    const fileName = String(content.fileName || "Nội dung").trim();
    const subject = String(content.ai_DetectedSubject || content.aiDetectedSubject || "Tổng quan").trim();
    const summaryRaw = String(content.aiProcess?.summary || content.aiProcess?.Summary || "").trim();
    const summaryItems = buildSummaryItems(summaryRaw);
    const quizCount = Number(content.quizCount || 0);
    const hasAi = Boolean(content.aiProcess);

    setText("[data-detail-meta]", `${subject} • ${formatDate(content.createdAt)}`);
    setText("[data-detail-title]", fileName);
    setText("[data-detail-subtitle]", summaryRaw || "Chưa có tóm tắt cho nội dung này.");
    setText("[data-detail-chip=subject]", subject);
    setText("[data-detail-chip=status]", hasAi ? "Đã có summary" : "Đang xử lý");
    setText("[data-detail-chip=quiz]", quizCount > 0 ? `${quizCount} bộ quiz` : "Chưa có quiz");

    setText("[data-detail-box=subject]", subject);
    setText("[data-detail-box=status]", hasAi ? "Hoàn tất" : "Đang xử lý");
    setText("[data-detail-box=quiz]", quizCount > 0 ? `${quizCount} bộ` : "0 bộ");
    setText("[data-detail-box=priority]", "Theo lịch sử học gần nhất");

    const quizLink = document.querySelector("[data-detail-quiz-link]");
    if (quizLink) {
      quizLink.setAttribute("href", `quiz-experience.html?contentId=${content.contentId}`);
    }

    const summaryList = document.querySelector("[data-detail-summary]");
    if (summaryList) {
      const items = summaryItems.length > 0 ? summaryItems : ["Chưa có tóm tắt."];
      summaryList.innerHTML = items.map((item) => `
        <li>
          <strong>${escapeHtml(item)}</strong>
        </li>
      `).join("");
    }

    setPageStatus("");
  };

  const renderPlanPage = async () => {
    setPageStatus("Đang tải learning plan...", "warning");
    await hydratePremiumUser();
    await hydratePremiumTier();

    const { ok, data } = await fetchJson("/api/dashboard/learning-plan");
    if (!ok || !data) {
      setPageStatus("Không thể tải learning plan.", "danger");
      return;
    }

    const tasks = Array.isArray(data.tasks) ? data.tasks : [];
    const recommendations = Array.isArray(data.recommendations) ? data.recommendations : [];
    const focusTopics = Array.isArray(data.focusTopics) ? data.focusTopics : [];
    const focusTopic = String(focusTopics?.[0]?.name || "Tổng quan").trim();

    setText("[data-plan-meta]", `${data.activeDaysCurrentWeek ?? 0}/${data.weeklyGoalSessions ?? 7} ngày hoạt động`);
    setText("[data-plan-chip=progress]", `Tuần này: ${data.weekProgressPercent ?? 0}%`);
    setText("[data-plan-chip=focus]", `Chủ đề ưu tiên: ${focusTopic}`);
    setText("[data-plan-chip=active]", `Ngày hoạt động: ${data.activeDaysCurrentWeek ?? 0}`);

    setText("[data-plan-metric=goal]", nf.format(data.weeklyGoalSessions ?? 7));
    setText("[data-plan-metric=active]", `${nf.format(data.activeDaysCurrentWeek ?? 0)}`);
    setText("[data-plan-metric=topic]", focusTopic);
    setText("[data-plan-metric=window]", "Tối (gợi ý)");

    const tasksRoot = document.querySelector("[data-plan-tasks]");
    if (tasksRoot) {
      if (tasks.length === 0) {
        tasksRoot.innerHTML = "<div class=\"premium-copy-muted\">Chưa có nhiệm vụ cho hôm nay.</div>";
      } else {
        tasksRoot.innerHTML = tasks.slice(0, 4).map((task) => `
          <article class="premium-plan-item${task.isCompleted ? " is-done" : ""}" data-plan-item>
            <button class="premium-plan-toggle" type="button" data-plan-toggle></button>
            <div>
              <div class="premium-line-title">${escapeHtml(task.title)}</div>
              <div class="premium-line-meta">${escapeHtml(task.detail)}</div>
            </div>
            <span class="premium-inline-kpi">${task.isCompleted ? "Done" : `${task.current ?? 0}/${task.target ?? 0}`}</span>
          </article>
        `).join("");
      }
    }

    const recRoot = document.querySelector("[data-plan-recommendations]");
    if (recRoot) {
      if (recommendations.length === 0) {
        recRoot.innerHTML = "<div class=\"premium-copy-muted\">Chưa có gợi ý cho tuần này.</div>";
      } else {
        recRoot.innerHTML = recommendations.slice(0, 3).map((rec, idx) => {
          const title = escapeHtml(rec.title || `Gợi ý ${idx + 1}`);
          const detail = escapeHtml(rec.detail || "");
          return `
            <article class="premium-roadmap-step">
              <div class="premium-roadmap-index">${String(idx + 1).padStart(2, "0")}</div>
              <div class="premium-roadmap-copy">
                <strong>${title}</strong>
                <p>${detail}</p>
              </div>
            </article>
          `;
        }).join("");
      }
    }

    setPageStatus("");
  };

  const getQuizPerformance = (accuracy) => {
    if (accuracy >= 90) return { label: "Xuất sắc" };
    if (accuracy >= 75) return { label: "Rất tốt" };
    if (accuracy >= 60) return { label: "Ổn định" };
    return { label: "Cần ôn thêm" };
  };

  const renderResultPage = async () => {
    await hydratePremiumUser();
    await hydratePremiumTier();

    const storageKey = "quiz.latestResult.v1";
    const payloadRaw =
      window.sessionStorage.getItem(storageKey) ||
      window.localStorage.getItem(storageKey) ||
      "";
    let payload = null;
    try {
      payload = payloadRaw ? JSON.parse(payloadRaw) : null;
    } catch {
      payload = null;
    }

    if (!payload) {
      setText("[data-result-summary]", "Chưa có dữ liệu kết quả. Hãy làm quiz và nộp bài để hệ thống lưu phân tích.");
      const wrongList = document.querySelector("[data-result-wrong-list]");
      if (wrongList) {
        wrongList.innerHTML = "<div class=\"premium-copy-muted\">Chưa có dữ liệu.</div>";
      }
      return;
    }

    const result = payload?.result || {};
    const quiz = payload?.quiz || {};
    const content = payload?.content || {};
    const totalQuestions = Number(result.totalQuestions || 0);
    const correctCount = Number(result.correctCount || 0);
    const wrongCount = Number(result.wrongCount || 0);
    const score = Number(result.score || 0);
    const accuracy = totalQuestions > 0 ? Math.max(0, Math.min(100, (correctCount / totalQuestions) * 100)) : 0;
    const performance = getQuizPerformance(accuracy);

    setText("[data-result-meta]", `${wrongCount} câu sai cần review`);
    setText("[data-result-chip=quiz]", String(content?.name || "Quiz gần nhất"));
    setText("[data-result-chip=count]", `Đúng ${correctCount} • Sai ${wrongCount}`);
    setText("[data-result-chip=saved]", payload.savedAt ? `Lần nộp: ${new Date(payload.savedAt).toLocaleString("vi-VN")}` : "Kết quả gần nhất");

    setText("[data-result-metric=score]", Number.isFinite(score) ? score.toFixed(2) : "0.00");
    setText("[data-result-metric=wrong]", nf.format(wrongCount));
    setText("[data-result-metric=performance]", performance.label);
    setText("[data-result-metric=advice]", wrongCount > 0 ? "10-15m" : "5m");

    setText("[data-result-score]", Number.isFinite(score) ? score.toFixed(2) : "0.00");
    setText("[data-result-score-label]", performance.label);
    setText("[data-result-summary]", wrongCount > 0 ? "Ưu tiên ôn lại các câu sai để tránh lặp lỗi." : "Bạn đang làm rất tốt. Thử tăng độ khó ở lượt tiếp theo.");

    const wrongQuestions = Array.isArray(result.wrongQuestions) ? result.wrongQuestions : [];
    const wrongList = document.querySelector("[data-result-wrong-list]");
    if (wrongList) {
      if (wrongQuestions.length === 0) {
        wrongList.innerHTML = "<div class=\"premium-copy-muted\">Không có câu sai trong lần nộp gần nhất.</div>";
      } else {
        wrongList.innerHTML = wrongQuestions.slice(0, 4).map((item) => `
          <article class="premium-review-card warn">
            <strong>${escapeHtml(item.questionText || "Câu hỏi")}</strong>
            <div class="premium-card-copy">
              Bạn chọn: ${escapeHtml(item.selectedAnswer || "(bỏ trống)")} • Đáp án đúng: ${escapeHtml(item.correctAnswer || "-")}
            </div>
          </article>
        `).join("");
      }
    }
  };

  const hydratePremiumQuiz = async () => {
    try {
      await hydratePremiumUser();

      const params = new URLSearchParams(window.location.search);
      const quizId = Number(params.get("quizId") || 0);
      const headers = buildAuthHeaders();

      setQuizStatus("Đang tải dữ liệu quiz...", "warning");
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
          ? "Chưa có quiz nào. Hãy tạo quiz từ nội dung học tập trước."
          : "Không thể tải quiz. Vui lòng thử lại.";
        setQuizStatus(message, "danger");
        return;
      }

      const quiz = await quizResponse.json().catch(() => null);
      if (!quiz) {
        setQuizStatus("Không có dữ liệu quiz hợp lệ.", "danger");
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
        "Nội dung",
      ).trim();
      const fileName = String(content?.fileName || content?.FileName || "Nội dung học tập").trim();
      const summary = String(
        content?.aiProcess?.summary ||
        content?.aiProcess?.Summary ||
        content?.AiProcess?.summary ||
        content?.AiProcess?.Summary ||
        "",
      ).trim();

      const imageUrl = (/\.(png|jpg|jpeg|gif|webp)$/i).test(sourceUrl) ? sourceUrl : "";

      setText("[data-quiz-plan]", `Quiz #${quiz.quizId} • ${quiz.totalQuestions} câu • ${formatDifficulty(quiz.difficulty)}`);
      setText("[data-quiz-eyebrow]", `Premium Quiz • ${subject}`);
      setText("[data-quiz-title]", `Quiz tu ${fileName}`);
      setText("[data-quiz-subtitle]", summary || "Bộ đề được tạo từ nội dung của bạn.");

      setText("[data-quiz-topic]", `Chuyên đề: ${subject}`);
      setText("[data-quiz-difficulty]", `Độ khó: ${formatDifficulty(quiz.difficulty)}`);
      setText("[data-quiz-count]", `Tổng câu: ${quiz.totalQuestions}`);

      setText("[data-quiz-metric=total]", quiz.totalQuestions);
      setText("[data-quiz-metric=difficulty]", formatDifficulty(quiz.difficulty));
      setText("[data-quiz-metric=type]", formatQuizType(quiz.quizType));
      setText("[data-quiz-metric=created]", formatDate(quiz.createdAt));

      setText("[data-content-name]", fileName);
      setText("[data-content-subject]", subject);
      setText("[data-content-quiz-count]", content?.quizCount ? `${content.quizCount} bộ` : "0 bộ");

      const summaryBox = document.querySelector("[data-quiz-summary-box]");
      if (summaryBox) {
        if (summary) {
          summaryBox.style.display = "block";
          setText("[data-quiz-summary]", summary);
        } else {
          summaryBox.style.display = "none";
        }
      }

      setText("[data-quiz-caption]", `Nội dung: ${fileName}`);
      setText("[data-quiz-footer]", `Quiz #${quiz.quizId} tạo lúc ${formatDate(quiz.createdAt)} tu ${fileName}.`);

      setImage("[data-quiz-image]", imageUrl, fileName);

      setLink("[data-quiz-content-link]", `content-detail.html?contentId=${quiz.contentId}`);
      setLink("[data-quiz-result-link]", "quiz-result.html");

      renderQuestions(quiz.questions || []);
      setQuizStatus("");
    } catch {
      setQuizStatus("Có lỗi khi đồng bộ dữ liệu quiz.", "danger");
    }
  };

  const renderQuizPage = async () => {
    setQuizStatus("Đang tải dữ liệu quiz...", "warning");
    await hydratePremiumUser();
    await hydratePremiumTier();

    const params = new URLSearchParams(window.location.search);
    const quizId = params.get("quizId");
    const apiUrl = quizId ? `/api/quiz/${quizId}` : "/api/quiz/latest";

    const { ok, data, status } = await fetchJson(apiUrl);

    if (!ok) {
      if (status === 404) {
        setQuizStatus("Không tìm thấy quiz nào. Hãy tạo một quiz mới từ không gian học của bạn.", "info");
        setText("[data-quiz-title]", "Chưa có quiz");
        setText("[data-quiz-subtitle]", "Không có dữ liệu quiz để hiển thị.");
      } else {
        setQuizStatus("Không thể tải dữ liệu quiz. Vui lòng thử lại sau.", "danger");
        setText("[data-quiz-title]", "Lỗi tải dữ liệu");
        setText("[data-quiz-subtitle]", "Đã có lỗi xảy ra khi kết nối tới máy chủ.");
      }
      return;
    }

    if (!data) {
      setQuizStatus("Dữ liệu quiz không hợp lệ.", "danger");
      return;
    }

    const { quiz, content, questions } = data;

    // Hydrate ribbon
    setText("[data-quiz-eyebrow]", `Quiz cho nội dung: ${content.title}`);
    setText("[data-quiz-title]", quiz.title);
    setText("[data-quiz-subtitle]", quiz.description || "Hãy bắt đầu làm bài quiz được tạo ra từ AI.");
    setText("[data-quiz-topic]", `Chuyên đề: ${content.topic || "Chung"}`);
    setText("[data-quiz-difficulty]", `Độ khó: ${formatDifficulty(quiz.difficulty)}`);
    setText("[data-quiz-count]", `Tổng câu: ${questions.length}`);
    setLink("[data-quiz-result-link]", `quiz-result.html?quizId=${quiz.quizId}`);
    setLink("[data-quiz-workspace-link]", "study-workspace.html");
    setLink("[data-quiz-content-link]", `content-detail.html?contentId=${content.contentId}`);
    setImage("[data-quiz-image]", content.coverImageUrl, content.title);
    setText("[data-quiz-caption]", content.title);

    // Hydrate metric strip
    setText("[data-quiz-metric='total']", questions.length);
    setText("[data-quiz-metric='difficulty']", formatDifficulty(quiz.difficulty));
    setText("[data-quiz-metric='type']", formatQuizType(quiz.quizType));
    setText("[data-quiz-metric='created']", formatDate(quiz.createdAt));

    // Hydrate side panel
    setText("[data-content-name]", content.title);
    setText("[data-content-subject]", content.topic || "Chung");
    setText("[data-content-quiz-count]", content.quizCount ?? 0);
    setText("[data-quiz-summary]", content.summary);

    // Render questions
    renderQuestions(questions);

    // Finalize
    setText("[data-quiz-footer]", `Quiz ID: ${quiz.quizId} | Content ID: ${content.contentId}`);
    setQuizStatus(""); // Clear status on success
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
          <div class="premium-sidebar-eyebrow">Điều hướng Premium</div>
          <div class="premium-sidebar-title">Không gian học tập</div>
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
        <div class="premium-sidebar-status">Đang đồng bộ gói...</div>
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
  void hydratePremiumTier();

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

  if (currentPage === "dashboard") {
    void renderDashboardPage();
  } else if (currentPage === "analytics") {
    void renderAnalyticsPage();
  } else if (currentPage === "workspace") {
    void renderWorkspacePage();
  } else if (currentPage === "library") {
    void renderLibraryPage();
  } else if (currentPage === "detail") {
    void renderDetailPage();
  } else if (currentPage === "plan") {
    void renderPlanPage();
  } else if (currentPage === "result") {
    void renderResultPage();
  } else if (currentPage === "quiz") {
      renderQuizPage();
  }
})();
