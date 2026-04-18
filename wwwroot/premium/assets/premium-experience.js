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
    const buttons = questionGroup.querySelectorAll("[data-question-button]");
    const cards = document.querySelectorAll("[data-question-card]");

    questionGroup.addEventListener("click", (event) => {
      const button = event.target.closest("[data-question-button]");
      if (!button) {
        return;
      }

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
})();
