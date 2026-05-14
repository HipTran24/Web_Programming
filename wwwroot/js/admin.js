(function () {
  let adminKeydownHandler = null;
  let adminVisibilityHandler = null;
  let adminDocumentClickHandler = null;
  let adminInteractionHandler = null;
  let adminHashChangeHandler = null;
  let adminAlertSeenStorageKey = "admin.alerts.lastSeenAt:default";

  const state = {
    users: { page: 1, pageSize: 12, totalPages: 1 },
    userItems: [],
    premiumTransactions: { page: 1, pageSize: 12, totalPages: 1 },
    premiumTransactionItems: [],
    premiumUsers: { page: 1, pageSize: 12, totalPages: 1 },
    premiumUserItems: [],
    premiumOverview: null,
    contents: { page: 1, pageSize: 12, totalPages: 1 },
    contentItems: [],
    aiLogs: { page: 1, pageSize: 15, totalPages: 1 },
    aiLogItems: [],
    aiSystem: null,
    audit: { page: 1, pageSize: 15, totalPages: 1 },
    auditItems: [],
    notifications: { page: 1, pageSize: 10, totalPages: 1 },
    adminAlerts: null,
    adminAlertItems: [],
    dashboardWindow: {
      mode: "week",
      offset: 0,
      dayCount: 7,
      startDate: "",
      endDate: "",
      label: "Tuần này",
      preset: "week_current",
    },
    adminProfile: null,
    activeTab: "users",
    autoRefreshTimer: 0,
    actionContext: null,
    interactionLockUntil: 0,
    renderSignatures: {
      overview: "",
      adminAccounts: "",
      notifications: "",
      adminAlerts: "",
      users: "",
      premiumOverview: "",
      premiumTransactions: "",
      premiumUsers: "",
      contents: "",
      aiSystem: "",
      aiLogs: "",
      audit: "",
      adminProfile: "",
    },
  };

  const el = {
    refreshAllBtn: document.getElementById("refreshAllBtn"),
    openOverviewWindowModal: document.getElementById("openOverviewWindowModal"),
    overviewWindowLabel: document.getElementById("overviewWindowLabel"),
    overviewWindowModal: document.getElementById("overviewWindowModal"),
    overviewWindowForm: document.getElementById("overviewWindowForm"),
    overviewWindowPreset: document.getElementById("overviewWindowPreset"),
    overviewStartDate: document.getElementById("overviewStartDate"),
    overviewEndDate: document.getElementById("overviewEndDate"),
    overviewWindowFeedback: document.getElementById("overviewWindowFeedback"),
    healthBadge: document.getElementById("healthBadge"),
    lastUpdatedValue: document.getElementById("lastUpdatedValue"),
    healthSignals: document.getElementById("healthSignals"),
    adminRosterMeta: document.getElementById("adminRosterMeta"),
    adminAccounts: document.getElementById("adminAccounts"),
    adminCreationTrend: document.getElementById("adminCreationTrend"),
    adminCreationTrendMeta: document.getElementById("adminCreationTrendMeta"),
    adminNotificationForm: document.getElementById("adminNotificationForm"),
    adminNotificationTargetScope: document.getElementById("adminNotificationTargetScope"),
    adminNotificationTargetEmail: document.getElementById("adminNotificationTargetEmail"),
    adminNotificationCategory: document.getElementById("adminNotificationCategory"),
    adminNotificationSeverity: document.getElementById("adminNotificationSeverity"),
    adminNotificationTitle: document.getElementById("adminNotificationTitle"),
    adminNotificationMessage: document.getElementById("adminNotificationMessage"),
    adminNotificationActionUrl: document.getElementById("adminNotificationActionUrl"),
    adminNotificationSendEmail: document.getElementById("adminNotificationSendEmail"),
    adminNotificationSubmit: document.getElementById("adminNotificationSubmit"),
    adminNotificationFeedback: document.getElementById("adminNotificationFeedback"),
    refreshNotificationHistory: document.getElementById("refreshNotificationHistory"),
    notificationHistoryTableBody: document.querySelector("#notificationHistoryTable tbody"),
    notificationHistoryPager: document.getElementById("notificationHistoryPager"),
    adminToastStack: document.getElementById("adminToastStack"),
    adminSessionName: document.getElementById("adminSessionName"),
    adminSessionRole: document.getElementById("adminSessionRole"),
    adminSessionAvatar: document.getElementById("adminSessionAvatar"),
    adminAlertsToggle: document.getElementById("adminAlertsToggle"),
    adminAlertsFlyout: document.getElementById("adminAlertsFlyout"),
    adminAlertsDot: document.getElementById("adminAlertsDot"),
    adminAlertsMeta: document.getElementById("adminAlertsMeta"),
    adminAlertsList: document.getElementById("adminAlertsList"),
    adminPortalLogout: document.getElementById("adminPortalLogout"),

    adminCreateForm: document.getElementById("adminCreateForm"),
    adminUsername: document.getElementById("adminUsername"),
    adminFullName: document.getElementById("adminFullName"),
    adminEmail: document.getElementById("adminEmail"),
    adminPassword: document.getElementById("adminPassword"),
    adminConfirmPassword: document.getElementById("adminConfirmPassword"),
    adminCreateSubmit: document.getElementById("adminCreateSubmit"),
    adminCreateFeedback: document.getElementById("adminCreateFeedback"),
    adminCreateModal: document.getElementById("adminCreateModal"),
    openAdminCreatePanel: document.getElementById("openAdminCreatePanel"),

    kpiTotalUsers: document.getElementById("kpiTotalUsers"),
    kpiTotalAdmins: document.getElementById("kpiTotalAdmins"),
    kpiActiveUsers: document.getElementById("kpiActiveUsers"),
    kpiLockedUsers: document.getElementById("kpiLockedUsers"),
    kpiPendingModeration: document.getElementById("kpiPendingModeration"),
    kpiTotalContents: document.getElementById("kpiTotalContents"),
    kpiTotalQuizzes: document.getElementById("kpiTotalQuizzes"),
    kpiAiCalls24h: document.getElementById("kpiAiCalls24h"),
    kpiAiErrors24h: document.getElementById("kpiAiErrors24h"),
    kpiAiErrorRate: document.getElementById("kpiAiErrorRate"),
    kpiAiAvgMs: document.getElementById("kpiAiAvgMs"),
    kpiVerifiedUsers: document.getElementById("kpiVerifiedUsers"),
    usageTrend: document.getElementById("usageTrend"),
    usageTrendSummary: document.getElementById("usageTrendSummary"),
    aiStabilityChart: document.getElementById("aiStabilityChart"),
    aiStabilitySummary: document.getElementById("aiStabilitySummary"),
    serviceBreakdown: document.getElementById("serviceBreakdown"),
    topContributors: document.getElementById("topContributors"),
    recentActivities: document.getElementById("recentActivities"),
    recentActivityCount: document.getElementById("recentActivityCount"),
    serverHealthTag: document.getElementById("serverHealthTag"),
    serverMetricRequests: document.getElementById("serverMetricRequests"),
    serverMetricRequestsMeta: document.getElementById("serverMetricRequestsMeta"),
    serverMetricAvgLatency: document.getElementById("serverMetricAvgLatency"),
    serverMetricAvgLatencyMeta: document.getElementById("serverMetricAvgLatencyMeta"),
    serverMetricP95Latency: document.getElementById("serverMetricP95Latency"),
    serverMetricP95LatencyMeta: document.getElementById("serverMetricP95LatencyMeta"),
    serverMetricAvailability: document.getElementById("serverMetricAvailability"),
    serverMetricAvailabilityMeta: document.getElementById("serverMetricAvailabilityMeta"),
    serverStabilityScore: document.getElementById("serverStabilityScore"),
    serverStabilityPill: document.getElementById("serverStabilityPill"),
    serverReliabilityGauge: document.getElementById("serverReliabilityGauge"),
    serverHealthSummary: document.getElementById("serverHealthSummary"),

    tabs: Array.from(document.querySelectorAll(".workspace-tab")),
    panels: Array.from(document.querySelectorAll(".workspace-panel")),

    userQuery: document.getElementById("userQuery"),
    userStatus: document.getElementById("userStatus"),
    userRole: document.getElementById("userRole"),
    openUserCreateModal: document.getElementById("openUserCreateModal"),
    applyUserFilter: document.getElementById("applyUserFilter"),
    usersTableBody: document.querySelector("#usersTable tbody"),
    usersPager: document.getElementById("usersPager"),

    premiumSettingsForm: document.getElementById("premiumSettingsForm"),
    premiumAmount: document.getElementById("premiumAmount"),
    premiumDays: document.getElementById("premiumDays"),
    premiumSettingsMeta: document.getElementById("premiumSettingsMeta"),
    premiumSettingsFeedback: document.getElementById("premiumSettingsFeedback"),
    premiumSettingsSubmit: document.getElementById("premiumSettingsSubmit"),
    premiumMetricActive: document.getElementById("premiumMetricActive"),
    premiumMetricExpired: document.getElementById("premiumMetricExpired"),
    premiumMetricRevenue: document.getElementById("premiumMetricRevenue"),
    premiumMetricPending: document.getElementById("premiumMetricPending"),
    premiumTransactionQuery: document.getElementById("premiumTransactionQuery"),
    premiumTransactionStatus: document.getElementById("premiumTransactionStatus"),
    applyPremiumTransactionFilter: document.getElementById("applyPremiumTransactionFilter"),
    premiumTransactionsTableBody: document.querySelector("#premiumTransactionsTable tbody"),
    premiumTransactionsPager: document.getElementById("premiumTransactionsPager"),
    premiumUserQuery: document.getElementById("premiumUserQuery"),
    premiumUserStatus: document.getElementById("premiumUserStatus"),
    applyPremiumUserFilter: document.getElementById("applyPremiumUserFilter"),
    premiumUsersTableBody: document.querySelector("#premiumUsersTable tbody"),
    premiumUsersPager: document.getElementById("premiumUsersPager"),
    premiumExtendModal: document.getElementById("premiumExtendModal"),
    premiumExtendForm: document.getElementById("premiumExtendForm"),
    premiumExtendUserId: document.getElementById("premiumExtendUserId"),
    premiumExtendUserLabel: document.getElementById("premiumExtendUserLabel"),
    premiumExtendDays: document.getElementById("premiumExtendDays"),
    premiumExtendReason: document.getElementById("premiumExtendReason"),
    premiumExtendFeedback: document.getElementById("premiumExtendFeedback"),
    premiumExtendSubmit: document.getElementById("premiumExtendSubmit"),

    contentQuery: document.getElementById("contentQuery"),
    contentStatus: document.getElementById("contentStatus"),
    applyContentFilter: document.getElementById("applyContentFilter"),
    contentsTableBody: document.querySelector("#contentsTable tbody"),
    contentsPager: document.getElementById("contentsPager"),

    errorsOnly: document.getElementById("errorsOnly"),
    aiSystemForm: document.getElementById("aiSystemForm"),
    aiSystemMeta: document.getElementById("aiSystemMeta"),
    aiPrimaryTextProvider: document.getElementById("aiPrimaryTextProvider"),
    aiPrimaryVisionProvider: document.getElementById("aiPrimaryVisionProvider"),
    aiTextOutputTokenBudget: document.getElementById("aiTextOutputTokenBudget"),
    aiQuizOutputTokenBudget: document.getElementById("aiQuizOutputTokenBudget"),
    aiImageOutputTokenBudget: document.getElementById("aiImageOutputTokenBudget"),
    aiApproxCharsPerToken: document.getElementById("aiApproxCharsPerToken"),
    aiGeminiDailyTokenBudget: document.getElementById("aiGeminiDailyTokenBudget"),
    aiGroqDailyTokenBudget: document.getElementById("aiGroqDailyTokenBudget"),
    aiMinReservedTokensPerProvider: document.getElementById("aiMinReservedTokensPerProvider"),
    aiProviderExecutionTimeoutMs: document.getElementById("aiProviderExecutionTimeoutMs"),
    aiSlowRequestThresholdMs: document.getElementById("aiSlowRequestThresholdMs"),
    aiSlowRequestStreakThreshold: document.getElementById("aiSlowRequestStreakThreshold"),
    aiConsecutiveFailureThreshold: document.getElementById("aiConsecutiveFailureThreshold"),
    aiProviderCooldownSeconds: document.getElementById("aiProviderCooldownSeconds"),
    aiEnforceDailyTokenBudget: document.getElementById("aiEnforceDailyTokenBudget"),
    aiEnableProviderHealthSwitch: document.getElementById("aiEnableProviderHealthSwitch"),
    aiPreferFastestHealthyProvider: document.getElementById("aiPreferFastestHealthyProvider"),
    aiGeminiKeyStatus: document.getElementById("aiGeminiKeyStatus"),
    aiGeminiApiKey: document.getElementById("aiGeminiApiKey"),
    aiGeminiClearApiKey: document.getElementById("aiGeminiClearApiKey"),
    aiGeminiBaseUrl: document.getElementById("aiGeminiBaseUrl"),
    aiGeminiTextModel: document.getElementById("aiGeminiTextModel"),
    aiGeminiVisionModel: document.getElementById("aiGeminiVisionModel"),
    aiGeminiMaxInputCharacters: document.getElementById("aiGeminiMaxInputCharacters"),
    aiGeminiMaxQuizInputCharacters: document.getElementById("aiGeminiMaxQuizInputCharacters"),
    aiGeminiRequestTimeoutSeconds: document.getElementById("aiGeminiRequestTimeoutSeconds"),
    aiGeminiMaxModelCandidates: document.getElementById("aiGeminiMaxModelCandidates"),
    aiGeminiMaxRetriesPerModel: document.getElementById("aiGeminiMaxRetriesPerModel"),
    aiGeminiFallbackModels: document.getElementById("aiGeminiFallbackModels"),
    aiGroqKeyStatus: document.getElementById("aiGroqKeyStatus"),
    aiGroqApiKey: document.getElementById("aiGroqApiKey"),
    aiGroqClearApiKey: document.getElementById("aiGroqClearApiKey"),
    aiGroqBaseUrl: document.getElementById("aiGroqBaseUrl"),
    aiGroqTextModel: document.getElementById("aiGroqTextModel"),
    aiGroqVisionModel: document.getElementById("aiGroqVisionModel"),
    aiGroqAudioModel: document.getElementById("aiGroqAudioModel"),
    aiGroqMaxInputCharacters: document.getElementById("aiGroqMaxInputCharacters"),
    aiGroqMaxQuizInputCharacters: document.getElementById("aiGroqMaxQuizInputCharacters"),
    aiGroqRequestTimeoutSeconds: document.getElementById("aiGroqRequestTimeoutSeconds"),
    aiGroqMaxModelCandidates: document.getElementById("aiGroqMaxModelCandidates"),
    aiGroqMaxConcurrentRequests: document.getElementById("aiGroqMaxConcurrentRequests"),
    aiGroqQueueWaitTimeoutSeconds: document.getElementById("aiGroqQueueWaitTimeoutSeconds"),
    aiGroqMaxRetriesPerModel: document.getElementById("aiGroqMaxRetriesPerModel"),
    aiGroqEnableResponseCache: document.getElementById("aiGroqEnableResponseCache"),
    aiGroqResponseCacheDays: document.getElementById("aiGroqResponseCacheDays") || document.getElementById("aiGroqResponseCacheMinutes"),
    aiGroqFallbackModels: document.getElementById("aiGroqFallbackModels"),
    aiSystemFeedback: document.getElementById("aiSystemFeedback"),
    aiSystemSubmit: document.getElementById("aiSystemSubmit"),
    aiSystemReset: document.getElementById("aiSystemReset"),
    refreshAiLogs: document.getElementById("refreshAiLogs"),
    aiLogsTableBody: document.querySelector("#aiLogsTable tbody"),
    aiLogsPager: document.getElementById("aiLogsPager"),
    aiReportTotal24h: document.getElementById("aiReportTotal24h"),
    aiReportErrors24h: document.getElementById("aiReportErrors24h"),
    aiReportErrorRate24h: document.getElementById("aiReportErrorRate24h"),
    aiReportAvgLatency24h: document.getElementById("aiReportAvgLatency24h"),
    aiReportP95Latency24h: document.getElementById("aiReportP95Latency24h"),
    aiReportMaxLatency24h: document.getElementById("aiReportMaxLatency24h"),
    aiLatencyTrend: document.getElementById("aiLatencyTrend"),
    aiActionBreakdown: document.getElementById("aiActionBreakdown"),
    aiSlowestItems: document.getElementById("aiSlowestItems"),

    refreshAuditLogs: document.getElementById("refreshAuditLogs"),
    auditTableBody: document.querySelector("#auditTable tbody"),
    auditPager: document.getElementById("auditPager"),

    detailsDrawer: document.getElementById("detailsDrawer"),
    detailsDrawerEyebrow: document.getElementById("detailsDrawerEyebrow"),
    detailsDrawerTitle: document.getElementById("detailsDrawerTitle"),
    detailsDrawerMeta: document.getElementById("detailsDrawerMeta"),
    detailsDrawerBody: document.getElementById("detailsDrawerBody"),
    closeDetailsDrawer: document.getElementById("closeDetailsDrawer"),
    drawerClosers: Array.from(document.querySelectorAll("[data-drawer-close]")),

    adminActionModal: document.getElementById("adminActionModal"),
    adminActionForm: document.getElementById("adminActionForm"),
    adminActionEyebrow: document.getElementById("adminActionEyebrow"),
    adminActionTitle: document.getElementById("adminActionTitle"),
    adminActionDescription: document.getElementById("adminActionDescription"),
    adminActionKind: document.getElementById("adminActionKind"),
    adminActionTargetId: document.getElementById("adminActionTargetId"),
    adminActionReason: document.getElementById("adminActionReason"),
    adminActionReasonLabel: document.getElementById("adminActionReasonLabel"),
    adminActionFeedback: document.getElementById("adminActionFeedback"),
    adminActionSubmit: document.getElementById("adminActionSubmit"),

    userEditorModal: document.getElementById("userEditorModal"),
    userEditorForm: document.getElementById("userEditorForm"),
    userEditorEyebrow: document.getElementById("userEditorEyebrow"),
    userEditorTitle: document.getElementById("userEditorTitle"),
    userEditorMode: document.getElementById("userEditorMode"),
    userEditorId: document.getElementById("userEditorId"),
    userEditorUsername: document.getElementById("userEditorUsername"),
    userEditorFullName: document.getElementById("userEditorFullName"),
    userEditorEmail: document.getElementById("userEditorEmail"),
    userEditorRole: document.getElementById("userEditorRole"),
    userEditorPasswordFields: document.getElementById("userEditorPasswordFields"),
    userEditorPassword: document.getElementById("userEditorPassword"),
    userEditorConfirmPassword: document.getElementById("userEditorConfirmPassword"),
    userEditorLocked: document.getElementById("userEditorLocked"),
    userEditorVerified: document.getElementById("userEditorVerified"),
    userEditorFeedback: document.getElementById("userEditorFeedback"),
    userEditorSubmit: document.getElementById("userEditorSubmit"),

    adminProfileCardName: document.getElementById("adminProfileCardName"),
    adminProfileCardEmail: document.getElementById("adminProfileCardEmail"),
    adminProfileCardRole: document.getElementById("adminProfileCardRole"),
    adminProfileCardUsername: document.getElementById("adminProfileCardUsername"),
    adminProfileCardCreatedAt: document.getElementById("adminProfileCardCreatedAt"),
    adminProfileCardStatus: document.getElementById("adminProfileCardStatus"),
    adminProfileCardVerified: document.getElementById("adminProfileCardVerified"),

    profileStatAuditActions: document.getElementById("profileStatAuditActions"),
    profileStatManagedUsers: document.getElementById("profileStatManagedUsers"),
    profileStatReviewedContents: document.getElementById("profileStatReviewedContents"),
    profileStatCreatedAdmins: document.getElementById("profileStatCreatedAdmins"),
    profileStatUploads: document.getElementById("profileStatUploads"),
    profileStatQuizAttempts: document.getElementById("profileStatQuizAttempts"),
    profileStatAverageScore: document.getElementById("profileStatAverageScore"),
    profileStatActiveDays: document.getElementById("profileStatActiveDays"),
    profileStatLastActionAt: document.getElementById("profileStatLastActionAt"),

    adminProfileForm: document.getElementById("adminProfileForm"),
    adminProfileUsername: document.getElementById("adminProfileUsername"),
    adminProfileRole: document.getElementById("adminProfileRole"),
    adminProfileCreatedAt: document.getElementById("adminProfileCreatedAt"),
    adminProfileFullName: document.getElementById("adminProfileFullName"),
    adminProfileEmail: document.getElementById("adminProfileEmail"),
    adminProfilePhone: document.getElementById("adminProfilePhone"),
    adminProfileBio: document.getElementById("adminProfileBio"),
    adminProfileInfoFeedback: document.getElementById("adminProfileInfoFeedback"),
    adminProfileSubmit: document.getElementById("adminProfileSubmit"),
    adminProfileReset: document.getElementById("adminProfileReset"),
    openAdminPasswordModal: document.getElementById("openAdminPasswordModal"),

    adminPasswordModal: document.getElementById("adminPasswordModal"),
    adminPasswordForm: document.getElementById("adminPasswordForm"),
    adminCurrentPassword: document.getElementById("adminCurrentPassword"),
    adminNewPassword: document.getElementById("adminNewPassword"),
    adminConfirmPassword: document.getElementById("adminConfirmPassword"),
    adminPasswordFeedback: document.getElementById("adminPasswordFeedback"),
    adminPasswordSubmit: document.getElementById("adminPasswordSubmit"),
  };

  if (!document.body.classList.contains("page-admin")) {
    return;
  }

  const pageType = document.body.getAttribute("data-admin-page") || "dashboard";

  const buildRenderSignature = (value) => {
    try {
      return JSON.stringify(value ?? null);
    } catch {
      return String(Date.now());
    }
  };

  const hasRenderChanged = (key, value) => {
    const nextSignature = buildRenderSignature(value);
    const didChange = state.renderSignatures[key] !== nextSignature;
    state.renderSignatures[key] = nextSignature;
    return didChange;
  };

  const numberFormatter = new Intl.NumberFormat("vi-VN");
  const currencyFormatter = new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  });
  const actionModal = window.bootstrap?.Modal && el.adminActionModal
    ? window.bootstrap.Modal.getOrCreateInstance(el.adminActionModal)
    : null;
  const overviewWindowModal = window.bootstrap?.Modal && el.overviewWindowModal
    ? window.bootstrap.Modal.getOrCreateInstance(el.overviewWindowModal)
    : null;
  const adminCreateModal = window.bootstrap?.Modal && el.adminCreateModal
    ? window.bootstrap.Modal.getOrCreateInstance(el.adminCreateModal)
    : null;
  const passwordModal = window.bootstrap?.Modal && el.adminPasswordModal
    ? window.bootstrap.Modal.getOrCreateInstance(el.adminPasswordModal)
    : null;
  const userEditorModal = window.bootstrap?.Modal && el.userEditorModal
    ? window.bootstrap.Modal.getOrCreateInstance(el.userEditorModal)
    : null;
  const premiumExtendModal = window.bootstrap?.Modal && el.premiumExtendModal
    ? window.bootstrap.Modal.getOrCreateInstance(el.premiumExtendModal)
    : null;

  const syncSessionUi = async () => {
    const me = await window.AuthClient?.requireAuth?.({ roles: ["admin"] });
    if (!me) {
      return null;
    }

    const displayName = toText(me.fullName || me.username || me.email) || "Quản trị viên";
    if (el.adminSessionName) {
      el.adminSessionName.textContent = displayName;
    }
    if (el.adminSessionRole) {
      el.adminSessionRole.textContent = translateLabel(me.role || "Admin");
    }
    if (el.adminSessionAvatar) {
      el.adminSessionAvatar.textContent = getInitials(displayName);
    }

    const identityKey = toText(me.userId || me.id || me.email || me.username).toLowerCase() || "default";
    adminAlertSeenStorageKey = `admin.alerts.lastSeenAt:${identityKey}`;

    return me;
  };

  const toText = (value) => String(value ?? "").trim();
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const formatNumber = (value) => numberFormatter.format(Number(value || 0));
  const formatCurrency = (value) => currencyFormatter.format(Number(value || 0));
  const formatMonthLabel = (date) => new Intl.DateTimeFormat("vi-VN", {
    month: "2-digit",
    year: "2-digit",
  }).format(date);
  const setText = (node, value) => {
    if (node) {
      node.textContent = value;
    }
  };
  const setValue = (node, value) => {
    if (node) {
      node.value = value;
    }
  };
  const getAdminAlertsSeenAtMs = () => {
    const raw = window.localStorage.getItem(adminAlertSeenStorageKey) || "";
    const parsed = Date.parse(raw);
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const closeAdminAlertsFlyout = () => {
    if (!el.adminAlertsFlyout || !el.adminAlertsToggle) {
      return;
    }

    el.adminAlertsFlyout.hidden = true;
    el.adminAlertsToggle.setAttribute("aria-expanded", "false");
  };
  const markAdminAlertsSeen = (items = state.adminAlertItems) => {
    const latestMs = Array.isArray(items)
      ? items.reduce((max, item) => Math.max(max, Date.parse(item?.createdAt || "") || 0), 0)
      : 0;

    if (latestMs > 0) {
      window.localStorage.setItem(adminAlertSeenStorageKey, new Date(latestMs).toISOString());
    }

    if (el.adminAlertsDot) {
      el.adminAlertsDot.hidden = true;
    }
  };
  const markAdminInteraction = (durationMs = 4000) => {
    state.interactionLockUntil = Date.now() + Math.max(800, Number(durationMs || 0));
  };

  const shouldPauseLiveRefresh = () => {
    if (Date.now() < Number(state.interactionLockUntil || 0)) {
      return true;
    }

    if (document.body.classList.contains("modal-open")) {
      return true;
    }

    if (document.querySelector(".modal.show, .offcanvas.show")) {
      return true;
    }

    if (el.adminAlertsFlyout && !el.adminAlertsFlyout.hidden) {
      return true;
    }

    if (el.detailsDrawer?.classList.contains("is-open")) {
      return true;
    }

    return false;
  };

  const cleanupTransientAdminUi = () => {
    document.querySelectorAll(".modal-backdrop, .offcanvas-backdrop").forEach((node) => node.remove());
    document.body.classList.remove("modal-open");
    document.body.style.removeProperty("overflow");
    document.body.style.removeProperty("padding-right");
    closeAdminAlertsFlyout();
    closeDetailsDrawer();
  };
  const displayLabels = {
    admin: "Quản trị viên",
    administrator: "Quản trị viên",
    user: "Học viên",
    approved: "Đã duyệt",
    active: "Đang hoạt động",
    verified: "Đã xác thực",
    unverified: "Chưa xác thực",
    success: "Thành công",
    rejected: "Từ chối",
    locked: "Đã khóa",
    error: "Lỗi",
    pending: "Chờ duyệt",
    allusers: "Toàn bộ user",
    none: "Chưa có",
    useraccess: "Tài khoản người dùng",
    userlifecycle: "Vòng đời tài khoản",
    moderation: "Kiểm duyệt",
    ailog: "Nhật ký AI",
    audittrail: "Nhật ký quản trị",
    action: "Hành động",
    target: "Đối tượng",
    aierror: "AI lỗi",
    ai: "AI",
    content: "Nội dung",
    createadmin: "Tạo quản trị viên",
    createuser: "Tạo người dùng",
    summarytext: "Tóm tắt văn bản",
    summarypdf: "Tóm tắt PDF",
    summarydocx: "Tóm tắt DOCX",
    summarywebpage: "Tóm tắt trang web",
    summaryimage: "Tóm tắt hình ảnh",
    summaryvideo: "Tóm tắt video",
    quizgenerate: "Sinh quiz",
    updateuser: "Cập nhật người dùng",
    deleteuser: "Xóa người dùng",
    approvecontent: "Duyệt nội dung",
    rejectcontent: "Từ chối nội dung",
    unknownadmin: "Quản trị viên không xác định",
    guestsystem: "Khách/Hệ thống",
  };

  const normalizeDisplayKey = (value) => toText(value).toLowerCase().replace(/[^a-z0-9]+/g, "");
  const translateLabel = (value, fallback = "--") => {
    const text = toText(value);
    if (!text) {
      return fallback;
    }

    return displayLabels[normalizeDisplayKey(text)] || text;
  };

  const formatDateTime = (value) => {
    if (!value) {
      return "--";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return "--";
    }

    return date.toLocaleString("vi-VN", {
      hour12: false,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };
  const formatModelList = (values) => Array.isArray(values) ? values.join("\n") : "";
  const parseModelList = (value) => String(value || "")
    .split(/\r?\n|,/)
    .map((item) => toText(item))
    .filter(Boolean);
  const normalizeCacheDays = (days, legacyMinutes, fallback = 7) => {
    const numericDays = Number(days);
    if (Number.isFinite(numericDays) && numericDays > 0) {
      return Math.min(30, Math.max(1, Math.trunc(numericDays)));
    }

    const numericMinutes = Number(legacyMinutes);
    if (Number.isFinite(numericMinutes) && numericMinutes > 0) {
      return Math.min(30, Math.max(1, Math.ceil(numericMinutes / 1440)));
    }

    return fallback;
  };

  const compactNumberFormatter = new Intl.NumberFormat("vi-VN", {
    notation: "compact",
    maximumFractionDigits: 1,
  });
  const formatCompactNumber = (value) => compactNumberFormatter.format(Number(value || 0));
  const formatLatency = (value) => `${formatNumber(Math.round(Number(value || 0)))} ms`;
  const formatPercent = (value, digits = 1) => `${Number(value || 0).toFixed(digits)}%`;
  const buildIsoDateValue = (yearValue, monthValue, dayValue) => {
    const year = Number(yearValue);
    const month = Number(monthValue);
    const day = Number(dayValue);
    if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) {
      return "";
    }

    const candidate = new Date(Date.UTC(year, month - 1, day));
    if (
      candidate.getUTCFullYear() !== year
      || candidate.getUTCMonth() !== month - 1
      || candidate.getUTCDate() !== day
    ) {
      return "";
    }

    return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
  };
  const toIsoDateValue = (value) => {
    if (!value) {
      return "";
    }

    if (value instanceof Date) {
      if (Number.isNaN(value.getTime())) {
        return "";
      }

      return buildIsoDateValue(value.getFullYear(), value.getMonth() + 1, value.getDate());
    }

    const text = toText(value);
    if (!text) {
      return "";
    }

    const isoMatch = text.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
    if (isoMatch) {
      const [, yearPart, monthPart, dayPart] = isoMatch;
      return buildIsoDateValue(yearPart, monthPart, dayPart);
    }

    const fallbackDate = new Date(text);
    if (Number.isNaN(fallbackDate.getTime())) {
      return "";
    }

    return buildIsoDateValue(fallbackDate.getFullYear(), fallbackDate.getMonth() + 1, fallbackDate.getDate());
  };
  const formatDisplayDateValue = (value) => {
    const iso = toIsoDateValue(value);
    if (!iso) {
      return "";
    }

    const [year, month, day] = iso.split("-");
    return `${day}/${month}/${year}`;
  };
  const parseDisplayDateValue = (value) => {
    const text = toText(value);
    if (!text) {
      return "";
    }

    const slashMatch = text.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (slashMatch) {
      const [, dayPart, monthPart, yearPart] = slashMatch;
      return buildIsoDateValue(yearPart, monthPart, dayPart);
    }

    return toIsoDateValue(text);
  };
  const normalizeOverviewDateInput = (input) => {
    if (!input) {
      return;
    }

    const iso = parseDisplayDateValue(input.value);
    input.value = iso ? formatDisplayDateValue(iso) : toText(input.value);
  };
  const buildDashboardWindowQuery = () => {
    const params = new URLSearchParams({
      windowMode: state.dashboardWindow.mode,
      windowOffset: String(state.dashboardWindow.offset),
      windowDays: String(state.dashboardWindow.dayCount || 7),
    });

    if (state.dashboardWindow.mode === "custom") {
      params.set("windowStart", state.dashboardWindow.startDate);
      params.set("windowEnd", state.dashboardWindow.endDate);
    }

    return params;
  };
  const updateDashboardWindowControls = (selectedWindow) => {
    const effectiveMode = toText(selectedWindow?.mode || state.dashboardWindow.mode).toLowerCase() || "week";
    const effectiveOffset = Math.max(0, Number(selectedWindow?.offset ?? state.dashboardWindow.offset ?? 0));
    const effectiveDayCount = Math.max(1, Number(selectedWindow?.dayCount ?? state.dashboardWindow.dayCount ?? 7));
    const effectiveLabel = toText(selectedWindow?.label || state.dashboardWindow.label || "Tuần này");
    const effectiveStartDate = toIsoDateValue(selectedWindow?.startDate || state.dashboardWindow.startDate);
    const effectiveEndDate = toIsoDateValue(selectedWindow?.endDate || state.dashboardWindow.endDate);
    const effectivePreset = effectiveMode === "custom"
      ? "custom"
      : effectiveMode === "rolling" && effectiveDayCount >= 30
        ? "rolling_30"
        : effectiveMode === "rolling"
          ? "rolling_7"
          : effectiveOffset === 1
            ? "week_previous"
            : "week_current";

    state.dashboardWindow.mode = effectiveMode;
    state.dashboardWindow.offset = effectiveOffset;
    state.dashboardWindow.dayCount = effectiveDayCount;
    state.dashboardWindow.startDate = effectiveStartDate;
    state.dashboardWindow.endDate = effectiveEndDate;
    state.dashboardWindow.label = effectiveLabel;
    state.dashboardWindow.preset = effectivePreset;

    setText(el.overviewWindowLabel, effectiveLabel);
    setValue(el.overviewWindowPreset, effectivePreset);
    setValue(el.overviewStartDate, formatDisplayDateValue(effectiveStartDate));
    setValue(el.overviewEndDate, formatDisplayDateValue(effectiveEndDate));
  };
  const getDashboardTone = (score) => {
    if (score >= 85) {
      return {
        pillClass: "status-success",
        chartClass: "is-good",
        label: "Ổn định",
      };
    }

    if (score >= 65) {
      return {
        pillClass: "status-warning",
        chartClass: "is-warning",
        label: "Theo dõi sát",
      };
    }

    return {
      pillClass: "status-danger",
      chartClass: "is-danger",
      label: "Cần can thiệp",
    };
  };

  const setDashboardPill = (node, tone, text) => {
    if (!node) {
      return;
    }

    node.classList.remove("status-success", "status-warning", "status-danger", "status-neutral");
    node.classList.add(tone?.pillClass || "status-neutral");
    node.textContent = text;
  };

  const renderEmptyChart = (node, message) => {
    if (!node) {
      return;
    }

    node.innerHTML = `<div class="chart-empty">${escapeHtml(message)}</div>`;
  };

  const buildLinePath = (points) => {
    if (!Array.isArray(points) || points.length === 0) {
      return "";
    }

    return points
      .map((point, index) => `${index === 0 ? "M" : "L"} ${point.x.toFixed(1)} ${point.y.toFixed(1)}`)
      .join(" ");
  };

  const buildAreaPath = (points, bottom) => {
    if (!Array.isArray(points) || points.length === 0) {
      return "";
    }

    const line = buildLinePath(points);
    return `${line} L ${points[points.length - 1].x.toFixed(1)} ${bottom.toFixed(1)} L ${points[0].x.toFixed(1)} ${bottom.toFixed(1)} Z`;
  };

  const calculateChartPoints = (rows, selector, topPadding, bottomY) => {
    const maxValue = Math.max(1, ...rows.map((row) => Number(selector(row) || 0)));
    return rows.map((row, index) => {
      const x = 56 + (index * ((648 - 56) / Math.max(1, rows.length - 1)));
      const ratio = Number(selector(row) || 0) / maxValue;
      return {
        x,
        y: bottomY - ((bottomY - topPadding) * ratio),
        value: Number(selector(row) || 0),
      };
    });
  };

  const calculateServerStability = (kpis, aiInsights) => {
    const summary = aiInsights?.windowSummary || aiInsights?.summary || {};
    const latencyTrend = Array.isArray(aiInsights?.latencyTrend) ? aiInsights.latencyTrend : [];
    const windowLabel = toText(aiInsights?.selectedWindow?.label || state.dashboardWindow.label || "khoảng đã chọn");
    const totalRequests = Number(summary.totalLogs24h || kpis.aiTotal24h || 0);
    const avgLatencyMs = Number(summary.avgLatencyMs24h || kpis.aiAvgTimeMs24h || 0);
    const p95LatencyMs = Number(summary.p95LatencyMs24h || 0);
    const errorRate = Number(summary.errorRate24h || kpis.aiErrorRate24h || 0);
    const guestCalls = Number(summary.guestCalls24h || 0);
    const pendingModeration = Number(kpis.pendingModeration || 0);
    const latencyPenalty = Math.max(0, avgLatencyMs - 260) / 10;
    const p95Penalty = Math.max(0, p95LatencyMs - 900) / 22;
    const moderationPenalty = pendingModeration * 0.8;
    const score = totalRequests === 0 && latencyTrend.every((row) => Number(row?.total || 0) === 0)
      ? 92
      : clamp(100 - (errorRate * 3.2) - latencyPenalty - p95Penalty - moderationPenalty, 24, 99);
    const availability = totalRequests === 0
      ? 100
      : clamp(99.95 - (errorRate * 0.26) - (Math.max(0, p95LatencyMs - 1200) / 5500), 91, 99.95);
    const tone = getDashboardTone(score);
    const note = totalRequests === 0
      ? `Chưa ghi nhận yêu cầu AI trong ${windowLabel.toLowerCase()}, hệ thống đang ở trạng thái rảnh và sẵn sàng xử lý.`
      : score >= 85
        ? `Hệ thống phản hồi ổn định với ${formatNumber(totalRequests)} yêu cầu AI trong ${windowLabel.toLowerCase()} và ${formatPercent(errorRate)} lỗi.`
        : score >= 65
          ? `Có dấu hiệu tăng áp lực xử lý: phản hồi P95 ${formatLatency(p95LatencyMs)} và tồn đọng kiểm duyệt ${formatNumber(pendingModeration)}.`
          : `Cần tối ưu thêm năng lực đáp ứng do độ trễ cao hoặc lỗi tăng lên trong khung thời gian gần đây.`;

    return {
      score: Math.round(score),
      availability,
      totalRequests,
      avgLatencyMs,
      p95LatencyMs,
      errorRate,
      guestCalls,
      tone,
      note,
    };
  };

  const syncStoredProfileIdentity = (profile) => {
    if (!profile) {
      return;
    }

    [window.localStorage, window.sessionStorage].forEach((storage) => {
      try {
        const raw = storage.getItem("auth.currentUser");
        if (!raw) {
          return;
        }

        const parsed = JSON.parse(raw);
        parsed.fullName = toText(profile.fullName);
        parsed.email = toText(profile.email);
        storage.setItem("auth.currentUser", JSON.stringify(parsed));
        storage.setItem("name", toText(profile.fullName) || toText(profile.email));
      } catch {
        // Ignore malformed session payloads.
      }
    });
  };

  const setAdminCreateModalVisible = (visible) => {
    if (!adminCreateModal) {
      return;
    }

    el.openAdminCreatePanel?.setAttribute("aria-expanded", visible ? "true" : "false");

    if (visible) {
      adminCreateModal.show();
      window.setTimeout(() => {
        el.adminUsername?.focus();
      }, 180);
      return;
    }

    adminCreateModal.hide();
  };

  const escapeHtml = (value) => toText(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");

  const getInitials = (value) => {
    const source = toText(value);
    if (!source) {
      return "AD";
    }

    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }

    return source.slice(0, 2).toUpperCase();
  };

  const flattenValidationErrors = (errors) => {
    if (!errors || typeof errors !== "object") {
      return [];
    }

    return Object.values(errors)
      .flatMap((value) => Array.isArray(value) ? value : [])
      .map((value) => toText(value))
      .filter(Boolean);
  };

  const getAuthToken = () => {
    if (window.AuthClient?.getAccessToken) {
      return window.AuthClient.getAccessToken();
    }

    return window.localStorage.getItem("auth.accessToken") || window.sessionStorage.getItem("auth.accessToken") || "";
  };

  const createApiError = (payload, fallbackMessage) => {
    const validationMessages = flattenValidationErrors(payload?.errors);
    const message = validationMessages[0] || payload?.message || fallbackMessage;
    const error = new Error(message);
    error.payload = payload || null;
    return error;
  };

  const apiFetch = async (url, options = {}) => {
    const token = getAuthToken();
    const headers = {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(options.headers || {}),
    };
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      ...options,
      headers,
    });

    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.toLowerCase().includes("application/json")
      ? await response.json().catch(() => null)
      : null;

    if (response.status === 401) {
      window.AuthClient?.clearSession?.();
      const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
      window.location.replace(`/home/login.html?returnUrl=${returnUrl}`);
      return null;
    }

    if (response.status === 403) {
      showToast("Bạn không có quyền vào khu quản trị.", "error");
      window.location.replace("/unauthorized");
      return null;
    }

    if (!response.ok) {
      throw createApiError(payload, "Yêu cầu thất bại.");
    }

    return payload;
  };

  const showToast = (message, type = "success") => {
    if (!el.adminToastStack) {
      return;
    }

    const toast = document.createElement("div");
    toast.className = `admin-toast ${type === "error" ? "is-error" : "is-success"}`;
    toast.textContent = toText(message) || (type === "error" ? "Có lỗi xảy ra." : "Đã hoàn tất.");
    el.adminToastStack.appendChild(toast);

    window.setTimeout(() => {
      toast.remove();
    }, 3600);
  };

  const setInlineFeedback = (node, message, type) => {
    if (!node) {
      return;
    }

    node.textContent = toText(message);
    node.classList.remove("is-error", "is-success");

    if (type === "error") {
      node.classList.add("is-error");
    }

    if (type === "success") {
      node.classList.add("is-success");
    }
  };

  const toggleOverviewCustomDateInputs = () => {
    const isCustom = toText(el.overviewWindowPreset?.value || "") === "custom";
    if (el.overviewStartDate) {
      el.overviewStartDate.disabled = !isCustom;
    }
    if (el.overviewEndDate) {
      el.overviewEndDate.disabled = !isCustom;
    }
  };

  const openOverviewWindowPicker = () => {
    setInlineFeedback(el.overviewWindowFeedback, "", "");
    setValue(el.overviewWindowPreset, state.dashboardWindow.preset || "week_current");
    setValue(el.overviewStartDate, formatDisplayDateValue(state.dashboardWindow.startDate));
    setValue(el.overviewEndDate, formatDisplayDateValue(state.dashboardWindow.endDate));
    toggleOverviewCustomDateInputs();
    overviewWindowModal?.show();
  };

  const applyOverviewWindowPreset = (preset) => {
    if (preset === "week_previous") {
      state.dashboardWindow = {
        ...state.dashboardWindow,
        mode: "week",
        offset: 1,
        dayCount: 7,
        startDate: "",
        endDate: "",
        preset,
      };
      return;
    }

    if (preset === "rolling_7") {
      state.dashboardWindow = {
        ...state.dashboardWindow,
        mode: "rolling",
        offset: 0,
        dayCount: 7,
        startDate: "",
        endDate: "",
        preset,
      };
      return;
    }

    if (preset === "rolling_30") {
      state.dashboardWindow = {
        ...state.dashboardWindow,
        mode: "rolling",
        offset: 0,
        dayCount: 30,
        startDate: "",
        endDate: "",
        preset,
      };
      return;
    }

    state.dashboardWindow = {
      ...state.dashboardWindow,
      mode: "week",
      offset: 0,
      dayCount: 7,
      startDate: "",
      endDate: "",
      preset: "week_current",
    };
  };

  const handleOverviewWindowSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.overviewWindowFeedback, "", "");

    const preset = toText(el.overviewWindowPreset?.value || "week_current");
    if (preset === "custom") {
      const startDate = parseDisplayDateValue(el.overviewStartDate?.value);
      const endDate = parseDisplayDateValue(el.overviewEndDate?.value);

      if (!startDate || !endDate) {
        setInlineFeedback(el.overviewWindowFeedback, "Vui lòng nhập đúng định dạng ngày/tháng/năm cho cả hai mốc.", "error");
        return;
      }

      const start = new Date(startDate);
      const end = new Date(endDate);
      if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end < start) {
        setInlineFeedback(el.overviewWindowFeedback, "Khoảng thời gian không hợp lệ.", "error");
        return;
      }

      const diffDays = Math.round((end.getTime() - start.getTime()) / 86400000) + 1;
      if (diffDays > 62) {
        setInlineFeedback(el.overviewWindowFeedback, "Khoảng tùy chỉnh tối đa là 62 ngày để biểu đồ vẫn dễ đọc.", "error");
        return;
      }

      setValue(el.overviewStartDate, formatDisplayDateValue(startDate));
      setValue(el.overviewEndDate, formatDisplayDateValue(endDate));

      state.dashboardWindow = {
        ...state.dashboardWindow,
        mode: "custom",
        offset: 0,
        dayCount: diffDays,
        startDate,
        endDate,
        preset,
      };
    } else {
      applyOverviewWindowPreset(preset);
    }

    overviewWindowModal?.hide();
    await loadOverview();
    showToast("Đã áp dụng bộ lọc thời gian.");
  };

  const setButtonBusy = (button, busy, busyLabel) => {
    if (!button) {
      return;
    }

    if (!button.dataset.originalLabel) {
      button.dataset.originalLabel = button.textContent || "";
    }

    button.disabled = Boolean(busy);
    button.textContent = busy ? (busyLabel || "Đang xử lý...") : button.dataset.originalLabel;
  };

  const statusPill = (status) => {
    const normalized = toText(status).toLowerCase();
    if (!normalized) {
      return '<span class="status-pill status-neutral">--</span>';
    }

    if (["approved", "active", "verified", "success", "paid"].includes(normalized)) {
      return `<span class="status-pill status-success">${escapeHtml(translateLabel(status))}</span>`;
    }

    if (["rejected", "locked", "error", "failed", "cancelled", "expired"].includes(normalized)) {
      return `<span class="status-pill status-danger">${escapeHtml(translateLabel(status))}</span>`;
    }

    if (normalized === "pending") {
      return `<span class="status-pill status-warning">${escapeHtml(translateLabel(status))}</span>`;
    }

    return `<span class="status-pill status-neutral">${escapeHtml(translateLabel(status, status))}</span>`;
  };

  const setTableLoading = (tbody, colspan, message) => {
    if (!tbody) {
      return;
    }

    tbody.innerHTML = `<tr><td colspan="${colspan}" class="text-center row-meta py-4">${escapeHtml(message)}</td></tr>`;
  };

  const createTableRow = (rowHtml) => {
    const template = document.createElement("tbody");
    template.innerHTML = String(rowHtml || "").trim();
    return template.firstElementChild;
  };

  const patchTableRows = (tbody, items, options) => {
    if (!tbody) {
      return;
    }

    const rows = Array.isArray(items) ? items : [];
    if (rows.length === 0) {
      const emptyHtml = options.emptyHtml || "";
      if (tbody.dataset.emptyHtml !== emptyHtml) {
        tbody.innerHTML = emptyHtml;
        tbody.dataset.emptyHtml = emptyHtml;
      }
      return;
    }

    const existingRows = new Map(
      Array.from(tbody.querySelectorAll("tr[data-row-key]"))
        .map((row) => [String(row.dataset.rowKey || ""), row])
        .filter(([key]) => key)
    );

    const nextDescriptors = rows.map((item, index) => {
      const key = String(options.getKey(item, index));
      const rowHtml = String(options.renderRow(item, index) || "").trim();
      const existingRow = existingRows.get(key);

      if (existingRow && existingRow.dataset.rowHtml === rowHtml) {
        return { key, rowHtml, row: existingRow };
      }

      const nextRow = createTableRow(rowHtml);
      if (nextRow) {
        nextRow.dataset.rowKey = key;
        nextRow.dataset.rowHtml = rowHtml;
      }
      return nextRow ? { key, rowHtml, row: nextRow } : null;
    }).filter(Boolean);

    nextDescriptors.forEach((descriptor, index) => {
      const referenceNode = tbody.children[index] || null;
      if (referenceNode === descriptor.row) {
        return;
      }

      tbody.insertBefore(descriptor.row, referenceNode);
    });

    while (tbody.children.length > nextDescriptors.length) {
      tbody.lastElementChild?.remove();
    }

    tbody.dataset.emptyHtml = "";
  };

  const renderNotificationHistoryRow = (item) => `
    <tr>
      <td>
        <div><strong>${escapeHtml(item.title || "Thông báo hệ thống")}</strong></div>
        <div class="row-meta">${escapeHtml((item.message || "").slice(0, 120))}</div>
      </td>
      <td>${statusPill(item.targetScope === "user" ? "Riêng lẻ" : "AllUsers")}<div class="row-meta">${escapeHtml(item.targetLabel || (item.targetScope === "user" ? "Không rõ email" : "Toàn bộ người dùng"))} • ${formatNumber(item.recipientCount)} người nhận</div></td>
      <td>${statusPill(item.category || "announcement")}<div class="row-meta">${escapeHtml(translateLabel(item.severity || "info", item.severity || "info"))}</div></td>
      <td>${item.emailRequested ? statusPill("Enabled") : statusPill("Disabled")}</td>
      <td>${formatDateTime(item.createdAt)}</td>
    </tr>
  `;

  const premiumStatusPill = (premium) => {
    const status = toText(premium?.status || "none").toLowerCase();
    const label = toText(premium?.label) || (status === "active" ? "Đang Premium" : status === "expired" ? "Hết hạn" : "Chưa đăng ký");
    const className = status === "active"
      ? "status-success"
      : status === "expired"
        ? "status-danger"
        : "status-neutral";

    return `<span class="status-pill ${className}">${escapeHtml(label)}</span>`;
  };

  const renderPremiumTransactionRow = (item) => {
    const user = item.user || {};
    const orderId = item.orderId || item.providerReference || item.requestId || "--";
    const status = toText(item.status || "Pending").toLowerCase();
    const canApprove = status === "pending";
    const approveAction = canApprove
      ? `<button class="btn-row" data-premium-transaction-action="approve" data-transaction-id="${item.paymentTransactionId}" data-transaction-order="${escapeHtml(orderId)}" data-transaction-user="${escapeHtml(user.fullName || user.username || `User #${user.userId || ""}`)}">Duyệt</button>`
      : '<span class="row-meta">--</span>';
    return `
      <tr>
        <td>
          <div><strong>${escapeHtml(user.fullName || user.username || `User #${user.userId || item.paymentTransactionId}`)}</strong></div>
          <div class="row-meta">@${escapeHtml(user.username || "--")} • ${escapeHtml(user.email || "--")}</div>
        </td>
        <td><strong>${escapeHtml(formatCurrency(item.amount))}</strong><div class="row-meta">${escapeHtml(item.currency || "VND")}</div></td>
        <td>${statusPill(item.status || "Pending")}</td>
        <td>${formatDateTime(item.paidAt)}<div class="row-meta">Tạo ${formatDateTime(item.createdAt)}</div></td>
        <td><strong>${escapeHtml(orderId)}</strong><div class="row-meta">${escapeHtml(item.requestId || item.providerReference || "--")}</div></td>
        <td>${escapeHtml(item.provider || "--")}<div class="row-meta">${escapeHtml(item.providerMessage || "")}</div></td>
        <td class="text-end"><div class="action-group">${approveAction}</div></td>
      </tr>
    `;
  };

  const renderPremiumUserRow = (item) => {
    const premium = item.premium || {};
    const displayName = item.fullName || item.username;
    const canCancel = premium.status === "active";
    return `
      <tr>
        <td>
          <div><strong>${escapeHtml(displayName)}</strong></div>
          <div class="row-meta">@${escapeHtml(item.username)} • ${escapeHtml(item.email)}</div>
        </td>
        <td>${statusPill(item.role)}</td>
        <td>${premiumStatusPill(premium)}<div class="row-meta">${premium.startedAt ? `Bắt đầu ${formatDateTime(premium.startedAt)}` : "Chưa có ngày bắt đầu"}</div></td>
        <td>${formatDateTime(premium.expiresAt)}</td>
        <td>${escapeHtml(formatCurrency(item.totalPaid))}<div class="row-meta">${item.lastPaidAt ? `Lần cuối ${formatDateTime(item.lastPaidAt)}` : "Chưa có giao dịch paid"}</div></td>
        <td class="text-end">
          <div class="action-group">
            <button class="btn-row" data-premium-action="extend" data-user-id="${item.userId}" data-user-name="${escapeHtml(displayName)}">Gia hạn</button>
            <button class="btn-row btn-row-danger" data-premium-action="cancel" data-user-id="${item.userId}" data-user-name="${escapeHtml(displayName)}" ${canCancel ? "" : "disabled"}>Hủy</button>
          </div>
        </td>
      </tr>
    `;
  };

  const renderUserRow = (item) => {
    const displayName = item.fullName || item.username;
    const editAction = `<button class="btn-row" data-action="edit" data-user-id="${item.userId}">Sửa</button>`;
    const deleteAction = `<button class="btn-row btn-row-danger" data-action="delete" data-user-id="${item.userId}" data-user-name="${escapeHtml(displayName)}">Xóa</button>`;
    const lockAction = item.isLocked
      ? `<button class="btn-row" data-action="unlock" data-user-id="${item.userId}" data-user-name="${escapeHtml(displayName)}">Mở khóa</button>`
      : `<button class="btn-row btn-row-danger" data-action="lock" data-user-id="${item.userId}" data-user-name="${escapeHtml(displayName)}">Khóa</button>`;

    return `
      <tr>
        <td>
          <div><strong>${escapeHtml(displayName)}</strong></div>
          <div class="row-meta">@${escapeHtml(item.username)} • ${escapeHtml(item.email)}</div>
        </td>
        <td>${statusPill(item.role)}</td>
        <td>
          ${statusPill(item.isLocked ? "Locked" : "Active")}
          ${item.isEmailVerified ? statusPill("Verified") : statusPill("Unverified")}
        </td>
        <td>${premiumStatusPill(item.premium)}<div class="row-meta">${formatDateTime(item.premium?.expiresAt)}</div></td>
        <td>${formatNumber(item.contentsCount)}</td>
        <td>${formatNumber(item.quizAttemptsCount)}</td>
        <td>${formatDateTime(item.createdAt)}</td>
        <td class="text-end"><div class="action-group">${editAction}${lockAction}${deleteAction}</div></td>
      </tr>
    `;
  };

  const renderContentRow = (item) => {
    const moderationStatus = item.moderation?.status || "None";
    const reason = toText(item.moderation?.reason);
    const isPolicyViolation = Boolean(item.moderation?.isPolicyViolation);
    const normalizedModeration = toText(moderationStatus).toLowerCase();
    const rowClass = isPolicyViolation
      ? `admin-content-row admin-content-row--policy ${normalizedModeration === "rejected" ? "admin-content-row--rejected" : "admin-content-row--pending"}`
      : "admin-content-row";
    const policyBadge = isPolicyViolation
      ? `<div class="policy-flag ${normalizedModeration === "rejected" ? "policy-flag--danger" : "policy-flag--warning"}">
          ${normalizedModeration === "rejected" ? "Vi phạm chính sách: đã từ chối" : "Cảnh báo chính sách hệ thống"}
        </div>`
      : "";

    return `
      <tr class="${rowClass}">
        <td>
          <div><strong>${escapeHtml(item.fileName || `Nội dung #${item.contentId}`)}</strong></div>
          ${policyBadge}
          <div class="row-meta">ID ${item.contentId} • Quiz ${formatNumber(item.quizCount)}</div>
        </td>
        <td>
          ${item.user
            ? `${escapeHtml(item.user.username)}<div class="row-meta">${escapeHtml(item.user.email)}</div>`
            : '<span class="row-meta">Khách/Không xác định</span>'}
        </td>
        <td>${statusPill(item.sourceType)}<div class="row-meta">${escapeHtml(translateLabel(item.fetchStatus || "--", item.fetchStatus || "--"))}</div></td>
        <td>
          ${statusPill(moderationStatus)}
          ${isPolicyViolation ? '<div class="policy-flag-inline">Nội dung bị hệ thống gắn cờ vi phạm chính sách.</div>' : ""}
          ${reason ? `<div class="row-meta">${escapeHtml(reason)}</div>` : ""}
        </td>
        <td>${formatDateTime(item.createdAt)}</td>
        <td class="text-end">
          <div class="action-group">
            <button class="btn-row" data-action="approve" data-policy-violation="${isPolicyViolation ? "true" : "false"}" data-content-id="${item.contentId}" data-content-name="${escapeHtml(item.fileName || `Nội dung #${item.contentId}`)}">Duyệt</button>
            <button class="btn-row btn-row-danger" data-action="reject" data-policy-violation="${isPolicyViolation ? "true" : "false"}" data-content-id="${item.contentId}" data-content-name="${escapeHtml(item.fileName || `Nội dung #${item.contentId}`)}">Từ chối</button>
          </div>
        </td>
      </tr>
    `;
  };

  const renderAiLogRow = (item, index) => `
    <tr>
      <td><strong>${escapeHtml(translateLabel(item.actionType, item.actionType))}</strong></td>
      <td>${item.user ? `${escapeHtml(item.user.username)}<div class="row-meta">${escapeHtml(item.user.email)}</div>` : '<span class="row-meta">Khách/Hệ thống</span>'}</td>
      <td>${statusPill(item.isError ? "Error" : "Success")}</td>
      <td>${formatNumber(Math.round(Number(item.processingTime || 0) * 1000))} ms</td>
      <td>${formatDateTime(item.createdAt)}</td>
      <td class="text-end"><button class="log-link" type="button" data-ai-log-index="${index}">Xem</button></td>
    </tr>
  `;

  const renderAuditRow = (item, index) => `
    <tr>
      <td>${item.admin ? `${escapeHtml(item.admin.username)}<div class="row-meta">${escapeHtml(item.admin.email)}</div>` : '<span class="row-meta">Không xác định</span>'}</td>
      <td>${escapeHtml(translateLabel(item.actionType, item.actionType))}</td>
      <td>${escapeHtml(translateLabel(item.targetType, item.targetType))}${item.targetId ? ` #${escapeHtml(item.targetId)}` : ""}</td>
      <td><span class="row-meta">${escapeHtml((item.detailJson || "{}").slice(0, 120))}</span></td>
      <td>${formatDateTime(item.createdAt)}</td>
      <td class="text-end"><button class="log-link" type="button" data-audit-index="${index}">Xem</button></td>
    </tr>
  `;

  const renderPager = (container, paging, totalItems, pagerKey) => {
    if (!container) {
      return;
    }

    const page = clamp(Number(paging?.page || 1), 1, Number(paging?.totalPages || 1));
    const totalPages = Math.max(1, Number(paging?.totalPages || 1));
    const total = Number(totalItems || 0);

    container.innerHTML = `
      <span>${formatNumber(total)} mục • Trang ${page}/${totalPages}</span>
      <button class="btn-row" data-pager-key="${escapeHtml(pagerKey || "")}" data-page-action="prev" ${page <= 1 ? "disabled" : ""}>Trước</button>
      <button class="btn-row" data-pager-key="${escapeHtml(pagerKey || "")}" data-page-action="next" ${page >= totalPages ? "disabled" : ""}>Sau</button>
    `;
  };

  const deriveHealthState = (kpis) => {
    const errorRate = Number(kpis.aiErrorRate24h || 0);
    const pendingModeration = Number(kpis.pendingModeration || 0);
    const lockedUsers = Number(kpis.lockedUsers || 0);

    if (errorRate >= 20 || pendingModeration >= 12 || lockedUsers >= 15) {
      return {
        label: "Cần can thiệp",
        className: "is-critical",
      };
    }

    if (errorRate >= 8 || pendingModeration >= 5 || lockedUsers >= 5) {
      return {
        label: "Đang cần chú ý",
        className: "is-attention",
      };
    }

    return {
      label: "Ổn định",
      className: "",
    };
  };

  const renderHealthSignals = (kpis) => {
    if (!el.healthSignals) {
      return;
    }

    const totalUsers = Number(kpis.totalUsers || 0);
    const verifiedUsers = Number(kpis.verifiedUsers || 0);
    const aiErrorRate = Number(kpis.aiErrorRate24h || 0);
    const pendingModeration = Number(kpis.pendingModeration || 0);
    const lockedUsers = Number(kpis.lockedUsers || 0);
    const aiCalls = Number(kpis.aiTotal24h || 0);
    const verifiedRatio = totalUsers <= 0 ? 100 : Math.round((verifiedUsers / totalUsers) * 100);
    const moderationPressure = clamp(pendingModeration * 8, 4, 100);
    const reliabilityScore = clamp(100 - Math.round(aiErrorRate * 3.5), 4, 100);
    const trustScore = clamp(verifiedRatio - Math.round((lockedUsers / Math.max(1, totalUsers)) * 100), 4, 100);

    const signals = [
      {
        title: "Độ ổn định AI",
        value: `${clamp(reliabilityScore, 0, 100)}%`,
        progress: reliabilityScore,
        meta: `${formatNumber(aiCalls)} lượt gọi trong 24h • lỗi ${aiErrorRate.toFixed(1)}%`,
        tone: aiErrorRate >= 20 ? "is-danger" : aiErrorRate >= 8 ? "is-warning" : "",
      },
      {
        title: "Áp lực kiểm duyệt",
        value: `${formatNumber(pendingModeration)} chờ duyệt`,
        progress: moderationPressure,
        meta: pendingModeration > 0
          ? "Cần theo dõi để tránh dồn tồn đọng nội dung"
          : "Không có nội dung bị nghẽn ở kiểm duyệt",
        tone: pendingModeration >= 12 ? "is-danger" : pendingModeration >= 5 ? "is-warning" : "",
      },
      {
        title: "Độ tin cậy tài khoản",
        value: `${clamp(trustScore, 0, 100)}%`,
        progress: trustScore,
        meta: `${formatNumber(verifiedUsers)} đã xác thực • ${formatNumber(lockedUsers)} đang bị khóa`,
        tone: lockedUsers >= 15 ? "is-danger" : lockedUsers >= 5 ? "is-warning" : "",
      },
    ];

    el.healthSignals.innerHTML = signals.map((signal) => `
      <article class="signal-card ${signal.tone}">
        <div class="signal-top">
          <div class="signal-title">${escapeHtml(signal.title)}</div>
          <div class="signal-value">${escapeHtml(signal.value)}</div>
        </div>
        <div class="signal-track">
          <div class="signal-bar" style="width:${clamp(signal.progress, 0, 100)}%;"></div>
        </div>
        <div class="signal-meta">${escapeHtml(signal.meta)}</div>
      </article>
    `).join("");
  };

  const buildSummaryCard = (label, value, meta) => `
    <article class="chart-summary-card">
      <p>${escapeHtml(label)}</p>
      <strong>${escapeHtml(value)}</strong>
      <span>${escapeHtml(meta)}</span>
    </article>
  `;

  const encodeTooltipLines = (lines) => escapeHtml(lines.join("||"));

  const bindChartTooltips = (container) => {
    if (!container) {
      return;
    }

    const zones = Array.from(container.querySelectorAll("[data-chart-tooltip-title]"));
    if (zones.length === 0) {
      return;
    }

    let tooltip = container.querySelector(".chart-tooltip");
    if (!tooltip) {
      tooltip = document.createElement("div");
      tooltip.className = "chart-tooltip";
      tooltip.setAttribute("aria-hidden", "true");
      container.appendChild(tooltip);
    }

    const hideTooltip = () => {
      tooltip.classList.remove("is-visible");
      tooltip.setAttribute("aria-hidden", "true");
    };

    const showTooltip = (event, zone) => {
      const title = zone.getAttribute("data-chart-tooltip-title") || "";
      const lines = (zone.getAttribute("data-chart-tooltip-lines") || "")
        .split("||")
        .filter(Boolean);

      tooltip.innerHTML = `
        <p class="chart-tooltip-title">${escapeHtml(title)}</p>
        ${lines.map((line) => `<p class="chart-tooltip-line">${escapeHtml(line)}</p>`).join("")}
      `;

      const rect = container.getBoundingClientRect();
      const tooltipRect = tooltip.getBoundingClientRect();
      let left = event.clientX - rect.left + 16;
      let top = event.clientY - rect.top - tooltipRect.height - 14;

      if (left + tooltipRect.width > rect.width - 8) {
        left = rect.width - tooltipRect.width - 8;
      }

      if (left < 8) {
        left = 8;
      }

      if (top < 8) {
        top = event.clientY - rect.top + 18;
      }

      tooltip.style.left = `${left}px`;
      tooltip.style.top = `${top}px`;
      tooltip.classList.add("is-visible");
      tooltip.setAttribute("aria-hidden", "false");
    };

    zones.forEach((zone) => {
      zone.addEventListener("mouseenter", (event) => showTooltip(event, zone));
      zone.addEventListener("mousemove", (event) => showTooltip(event, zone));
      zone.addEventListener("mouseleave", hideTooltip);
      zone.addEventListener("blur", hideTooltip);
    });
  };

  const renderReliabilityGauge = (stability) => {
    if (!el.serverReliabilityGauge) {
      return;
    }

    const radius = 84;
    const circumference = 2 * Math.PI * radius;
    const score = clamp(Number(stability?.score || 0), 0, 100);
    const offset = circumference - ((score / 100) * circumference);

    el.serverReliabilityGauge.innerHTML = `
      <div class="gauge-shell">
        <svg class="gauge-svg" viewBox="0 0 220 220" role="img" aria-label="Điểm ổn định ${score} trên 100">
          <defs>
            <linearGradient id="gaugeGradientGood" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#65d9cb"></stop>
              <stop offset="100%" stop-color="#ffd58c"></stop>
            </linearGradient>
            <linearGradient id="gaugeGradientWarning" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#f0b45a"></stop>
              <stop offset="100%" stop-color="#ffd978"></stop>
            </linearGradient>
            <linearGradient id="gaugeGradientDanger" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#ff8476"></stop>
              <stop offset="100%" stop-color="#f0b45a"></stop>
            </linearGradient>
          </defs>
          <circle class="gauge-track" cx="110" cy="110" r="${radius}"></circle>
          <circle
            class="gauge-progress ${stability?.tone?.chartClass || "is-good"}"
            cx="110"
            cy="110"
            r="${radius}"
            stroke-dasharray="${circumference.toFixed(2)}"
            stroke-dashoffset="${offset.toFixed(2)}"
          ></circle>
        </svg>
        <div class="gauge-core">
          <div>
            <span class="gauge-score">${score}</span>
            <span class="gauge-label">/100 độ ổn định</span>
            <span class="gauge-meta">${escapeHtml(formatPercent(stability?.availability || 0, 2))} sẵn sàng ước tính</span>
          </div>
        </div>
      </div>
    `;
  };

  const renderServerCommandDeck = (kpis, aiInsights) => {
    const stability = calculateServerStability(kpis, aiInsights);
    const selectedWindowLabel = toText(aiInsights?.selectedWindow?.label || state.dashboardWindow.label || "kỳ đã chọn");

    setText(el.serverMetricRequests, formatNumber(stability.totalRequests));
    setText(el.serverMetricAvgLatency, formatLatency(stability.avgLatencyMs));
    setText(el.serverMetricP95Latency, formatLatency(stability.p95LatencyMs));
    setText(el.serverMetricAvailability, formatPercent(stability.availability, 2));
    setText(el.serverStabilityScore, `${stability.score}/100`);
    setText(el.serverHealthSummary, stability.note);

    setText(
      el.serverMetricRequestsMeta,
      stability.totalRequests > 0
        ? `${selectedWindowLabel} • ${formatNumber(stability.guestCalls)} yêu cầu khách • lỗi ${formatPercent(stability.errorRate)}`
        : `${selectedWindowLabel} • Không có yêu cầu AI trong khoảng này.`
    );
    setText(
      el.serverMetricAvgLatencyMeta,
      stability.avgLatencyMs > 0
        ? `Độ trễ trung bình của toàn bộ log AI trong khoảng đã chọn.`
        : "Chưa đủ dữ liệu phản hồi trong khoảng đang xem."
    );
    setText(
      el.serverMetricP95LatencyMeta,
      stability.p95LatencyMs > 0
        ? "Dùng để theo dõi phản hồi ở thời điểm hệ thống chịu tải cao."
        : "P95 sẽ xuất hiện khi khoảng thời gian này có thêm log."
    );
    setText(
      el.serverMetricAvailabilityMeta,
      `${selectedWindowLabel} • Ước tính từ tỷ lệ lỗi, thời gian phản hồi và áp lực kiểm duyệt hiện tại.`
    );

    setText(el.serverHealthTag, `${stability.tone.label} • ${formatPercent(stability.availability, 2)}`);
    setDashboardPill(el.serverStabilityPill, stability.tone, stability.tone.label);
    renderReliabilityGauge(stability);

    return stability;
  };

  const renderUsageResponseChart = (usageTrend, aiInsights) => {
    if (!el.usageTrend) {
      return;
    }

    const selectedWindowLabel = toText(aiInsights?.selectedWindow?.label || state.dashboardWindow.label || "kỳ đã chọn");

    const latencyMap = new Map(
      (Array.isArray(aiInsights?.latencyTrend) ? aiInsights.latencyTrend : [])
        .map((item) => [toText(item?.day), item])
    );
    const rows = (Array.isArray(usageTrend) ? usageTrend : []).map((row) => {
      const latencyRow = latencyMap.get(toText(row?.day)) || {};
      const uploads = Number(row?.uploads || 0);
      const aiCalls = Number(row?.aiCalls || 0);
      const quizGenerations = Number(row?.quizGenerations || 0);
      return {
        day: toText(row?.day) || "--",
        uploads,
        aiCalls,
        quizGenerations,
        totalLoad: uploads + aiCalls + quizGenerations,
        avgLatencyMs: Number(latencyRow?.avgLatencyMs || 0),
      };
    });

    if (rows.length === 0) {
      renderEmptyChart(el.usageTrend, "Chưa có dữ liệu yêu cầu và phản hồi cho 7 ngày gần đây.");
      if (el.usageTrendSummary) {
        el.usageTrendSummary.innerHTML = "";
      }
      return;
    }

    const svgWidth = 704;
    const svgHeight = 300;
    const topY = 26;
    const bottomY = 224;
    const leftX = 56;
    const rightX = 648;
    const step = (rightX - leftX) / Math.max(1, rows.length - 1);
    const barSlot = 42;
    const labelStep = rows.length > 10 ? Math.ceil(rows.length / 8) : 1;
    const maxLoad = Math.max(1, ...rows.map((row) => row.totalLoad));
    const latencyPoints = calculateChartPoints(rows, (row) => row.avgLatencyMs, topY, bottomY);
    const latencyPath = buildLinePath(latencyPoints);

    const gridLines = [0, 1, 2, 3].map((index) => {
      const y = topY + (((bottomY - topY) / 3) * index);
      return `<line class="gridline" x1="${leftX}" y1="${y.toFixed(1)}" x2="${rightX}" y2="${y.toFixed(1)}"></line>`;
    }).join("");

    const bars = rows.map((row, index) => {
      const centerX = leftX + (index * step);
      const totalHeight = ((bottomY - topY) * (row.totalLoad / maxLoad));
      const aiHeight = ((bottomY - topY) * (row.aiCalls / maxLoad));
      const totalY = bottomY - totalHeight;
      const aiY = bottomY - aiHeight;

      return `
        <rect
          class="bar-primary"
          x="${(centerX - (barSlot / 2)).toFixed(1)}"
          y="${totalY.toFixed(1)}"
          width="${barSlot}"
          height="${Math.max(totalHeight, 6).toFixed(1)}"
          rx="12"
        ></rect>
        <rect
          class="bar-secondary"
          x="${(centerX - (barSlot / 4)).toFixed(1)}"
          y="${aiY.toFixed(1)}"
          width="${barSlot / 2}"
          height="${Math.max(aiHeight, 4).toFixed(1)}"
          rx="10"
        ></rect>
        ${(index % labelStep === 0 || index === rows.length - 1)
          ? `<text class="label" x="${centerX.toFixed(1)}" y="254" text-anchor="middle">${escapeHtml(row.day)}</text>`
          : ""}
      `;
    }).join("");

    const points = latencyPoints.map((point) => `
      <circle class="point-primary" cx="${point.x.toFixed(1)}" cy="${point.y.toFixed(1)}" r="4"></circle>
    `).join("");
    const hoverZones = rows.map((row, index) => {
      const centerX = leftX + (index * step);
      const zoneWidth = Math.max(step, 56);
      const x = index === 0
        ? leftX - 16
        : centerX - (zoneWidth / 2);
      const lines = [
        `Tổng tải: ${formatNumber(row.totalLoad)}`,
        `Yêu cầu AI: ${formatNumber(row.aiCalls)}`,
        `Tải lên: ${formatNumber(row.uploads)} • Quiz: ${formatNumber(row.quizGenerations)}`,
        `Độ trễ TB: ${formatLatency(row.avgLatencyMs)}`,
      ];

      return `
        <rect
          class="chart-hover-zone"
          x="${x.toFixed(1)}"
          y="${topY}"
          width="${zoneWidth.toFixed(1)}"
          height="${(bottomY - topY).toFixed(1)}"
          rx="12"
          data-chart-tooltip-title="Ngày ${escapeHtml(row.day)}"
          data-chart-tooltip-lines="${encodeTooltipLines(lines)}"
        ></rect>
      `;
    }).join("");

    el.usageTrend.innerHTML = `
      <svg class="dashboard-chart" viewBox="0 0 ${svgWidth} ${svgHeight}" aria-label="Biểu đồ áp lực yêu cầu và thời gian phản hồi">
        ${gridLines}
        <line class="axis" x1="${leftX}" y1="${bottomY}" x2="${rightX}" y2="${bottomY}"></line>
        ${bars}
        <path class="line-primary" d="${latencyPath}"></path>
        ${points}
        ${hoverZones}
        <text class="label" x="${leftX}" y="18">Độ trễ</text>
        <text class="label" x="${rightX}" y="18" text-anchor="end">Cột ngoài: tổng tải | Cột trong: yêu cầu AI</text>
      </svg>
    `;
    bindChartTooltips(el.usageTrend);

    if (el.usageTrendSummary) {
      const peakRow = rows.reduce((best, row) => row.totalLoad > best.totalLoad ? row : best, rows[0]);
      const avgLatency7Days = rows.reduce((sum, row) => sum + row.avgLatencyMs, 0) / Math.max(1, rows.length);
      const totalAiCalls = rows.reduce((sum, row) => sum + row.aiCalls, 0);
      const aiLoadRatio = rows.reduce((sum, row) => sum + row.totalLoad, 0) === 0
        ? 0
        : (totalAiCalls * 100) / rows.reduce((sum, row) => sum + row.totalLoad, 0);

      el.usageTrendSummary.innerHTML = [
        buildSummaryCard("Đỉnh tải", formatNumber(peakRow.totalLoad), `${peakRow.day} là ngày có tổng tác vụ cao nhất.`),
        buildSummaryCard("Độ trễ trung bình", formatLatency(avgLatency7Days), `${selectedWindowLabel} • Dùng để ước lượng độ mượt khi hệ thống hoạt động liên tục.`),
        buildSummaryCard("Tỷ trọng yêu cầu AI", formatPercent(aiLoadRatio), `${formatNumber(totalAiCalls)} yêu cầu AI trong ${selectedWindowLabel.toLowerCase()}.`),
      ].join("");
    }
  };

  const renderAiStabilityChart = (kpis, aiInsights, stability) => {
    if (!el.aiStabilityChart) {
      return;
    }

    const selectedWindowLabel = toText(aiInsights?.selectedWindow?.label || state.dashboardWindow.label || "kỳ đã chọn");

    const rows = Array.isArray(aiInsights?.latencyTrend) ? aiInsights.latencyTrend.map((row) => {
      const total = Number(row?.total || 0);
      const errors = Number(row?.errors || 0);
      return {
        day: toText(row?.day) || "--",
        total,
        errors,
        successRate: total === 0 ? 100 : clamp(100 - ((errors * 100) / total), 0, 100),
      };
    }) : [];

    if (rows.length === 0) {
      renderEmptyChart(el.aiStabilityChart, "Chưa có dữ liệu độ ổn định log AI cho 7 ngày gần đây.");
      if (el.aiStabilitySummary) {
        el.aiStabilitySummary.innerHTML = "";
      }
      return;
    }

    const svgWidth = 704;
    const svgHeight = 300;
    const topY = 26;
    const bottomY = 224;
    const leftX = 56;
    const rightX = 648;
    const step = (rightX - leftX) / Math.max(1, rows.length - 1);
    const labelStep = rows.length > 10 ? Math.ceil(rows.length / 8) : 1;
    const maxErrors = Math.max(1, ...rows.map((row) => row.errors));
    const successPoints = rows.map((row, index) => {
      const x = leftX + (index * step);
      const y = bottomY - (((bottomY - topY) * (row.successRate / 100)));
      return { x, y, value: row.successRate };
    });

    const successPath = buildLinePath(successPoints);
    const successArea = buildAreaPath(successPoints, bottomY);

    const bars = rows.map((row, index) => {
      const x = (leftX + (index * step)) - 11;
      const barHeight = ((bottomY - topY) * (row.errors / maxErrors));
      const y = bottomY - barHeight;
      return `
        <rect class="bar-primary" x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="22" height="${Math.max(barHeight, 4).toFixed(1)}" rx="10"></rect>
        ${(index % labelStep === 0 || index === rows.length - 1)
          ? `<text class="label" x="${(x + 11).toFixed(1)}" y="254" text-anchor="middle">${escapeHtml(row.day)}</text>`
          : ""}
      `;
    }).join("");

    const points = successPoints.map((point) => `
      <circle class="point-primary" cx="${point.x.toFixed(1)}" cy="${point.y.toFixed(1)}" r="4"></circle>
    `).join("");
    const hoverZones = rows.map((row, index) => {
      const centerX = leftX + (index * step);
      const zoneWidth = Math.max(step, 56);
      const x = index === 0
        ? leftX - 16
        : centerX - (zoneWidth / 2);
      const lines = [
        `Tỷ lệ thành công: ${formatPercent(row.successRate)}`,
        `Tổng log: ${formatNumber(row.total)}`,
        `Lỗi: ${formatNumber(row.errors)}`,
      ];

      return `
        <rect
          class="chart-hover-zone"
          x="${x.toFixed(1)}"
          y="${topY}"
          width="${zoneWidth.toFixed(1)}"
          height="${(bottomY - topY).toFixed(1)}"
          rx="12"
          data-chart-tooltip-title="Ngày ${escapeHtml(row.day)}"
          data-chart-tooltip-lines="${encodeTooltipLines(lines)}"
        ></rect>
      `;
    }).join("");

    el.aiStabilityChart.innerHTML = `
      <svg class="dashboard-chart" viewBox="0 0 ${svgWidth} ${svgHeight}" aria-label="Biểu đồ độ ổn định AI">
        <line class="axis" x1="${leftX}" y1="${bottomY}" x2="${rightX}" y2="${bottomY}"></line>
        <line class="gridline" x1="${leftX}" y1="72" x2="${rightX}" y2="72"></line>
        <line class="gridline" x1="${leftX}" y1="126" x2="${rightX}" y2="126"></line>
        <line class="gridline" x1="${leftX}" y1="180" x2="${rightX}" y2="180"></line>
        ${bars}
        <path class="area-primary" d="${successArea}"></path>
        <path class="line-primary" d="${successPath}"></path>
        ${points}
        ${hoverZones}
        <text class="label" x="${leftX}" y="18">Tỷ lệ thành công</text>
        <text class="label" x="${rightX}" y="18" text-anchor="end">Cột: số lỗi mỗi ngày</text>
      </svg>
    `;
    bindChartTooltips(el.aiStabilityChart);

    if (el.aiStabilitySummary) {
      const averageSuccessRate = rows.reduce((sum, row) => sum + row.successRate, 0) / Math.max(1, rows.length);
      const weakestDay = rows.reduce((worst, row) => row.successRate < worst.successRate ? row : worst, rows[0]);

      el.aiStabilitySummary.innerHTML = [
        buildSummaryCard("Ổn định trung bình", formatPercent(averageSuccessRate), `Tỷ lệ yêu cầu AI thành công trung bình trong ${selectedWindowLabel.toLowerCase()}.`),
        buildSummaryCard("Ngày thấp nhất", `${weakestDay.day} • ${formatPercent(weakestDay.successRate)}`, `${formatNumber(weakestDay.errors)} lỗi trên ${formatNumber(weakestDay.total)} log.`),
        buildSummaryCard("Lỗi trong kỳ", formatNumber((aiInsights?.windowSummary?.errors ?? aiInsights?.summary?.errors24h ?? 0)), `Trạng thái tổng quan hiện là ${stability.tone.label.toLowerCase()}.`),
      ].join("");
    }
  };

  const renderServiceBreakdown = (aiInsights) => {
    if (!el.serviceBreakdown) {
      return;
    }

    const breakdown = Array.isArray(aiInsights?.actionBreakdown) ? aiInsights.actionBreakdown.slice(0, 6) : [];
    if (breakdown.length === 0) {
      el.serviceBreakdown.innerHTML = '<div class="empty-state">Chưa có phân rã tác vụ AI trong 7 ngày gần đây.</div>';
      return;
    }

    const maxTotal = Math.max(1, ...breakdown.map((item) => Number(item?.total || 0)));
    el.serviceBreakdown.innerHTML = breakdown.map((item) => {
      const total = Number(item?.total || 0);
      const errors = Number(item?.errors || 0);
      const avgLatencyMs = Number(item?.avgLatencyMs || 0);
      const width = (total / maxTotal) * 100;
      return `
        <article class="service-breakdown-item">
          <div class="service-breakdown-head">
            <strong>${escapeHtml(translateLabel(item?.actionType, item?.actionType || "Tác vụ AI"))}</strong>
            <span>${formatCompactNumber(total)} yêu cầu</span>
          </div>
          <div class="service-breakdown-track">
            <div class="service-breakdown-bar" style="width:${width.toFixed(1)}%;"></div>
          </div>
          <div class="service-breakdown-meta">
            Lỗi ${formatNumber(errors)} • Trung bình ${formatLatency(avgLatencyMs)} • Đỉnh ${formatLatency(item?.maxLatencyMs || 0)}
          </div>
        </article>
      `;
    }).join("");
  };

  const renderOverview = (data, aiInsights = null) => {
    const kpis = data?.kpis || {};
    const health = deriveHealthState(kpis);
    updateDashboardWindowControls(data?.selectedWindow || aiInsights?.selectedWindow);

    if (el.healthBadge) {
      el.healthBadge.textContent = health.label;
      el.healthBadge.classList.remove("is-attention", "is-critical");
      if (health.className) {
        el.healthBadge.classList.add(health.className);
      }
    }

    if (el.lastUpdatedValue) {
      el.lastUpdatedValue.textContent = formatDateTime(data?.generatedAt);
    }

    setText(el.kpiTotalUsers, formatNumber(kpis.totalUsers));
    setText(el.kpiTotalAdmins, formatNumber(kpis.totalAdmins));
    setText(el.kpiActiveUsers, formatNumber(kpis.activeUsers7Days));
    setText(el.kpiLockedUsers, formatNumber(kpis.lockedUsers));
    setText(el.kpiPendingModeration, formatNumber(kpis.pendingModeration));
    setText(el.kpiTotalContents, formatNumber(kpis.totalContents));
    setText(el.kpiTotalQuizzes, formatNumber(kpis.totalQuizzes));
    setText(el.kpiAiCalls24h, formatNumber(kpis.aiTotal24h));
    setText(el.kpiAiErrors24h, formatNumber(kpis.aiErrors24h));
    setText(el.kpiAiErrorRate, `${Number(kpis.aiErrorRate24h || 0).toFixed(1)}%`);
    setText(el.kpiAiAvgMs, `${formatNumber(kpis.aiAvgTimeMs24h)} ms`);
    setText(el.kpiVerifiedUsers, formatNumber(kpis.verifiedUsers));

    if (el.healthSignals) {
      renderHealthSignals(kpis);
    }

    const usageTrend = Array.isArray(data?.usageTrend) ? data.usageTrend : [];
    const stability = renderServerCommandDeck(kpis, aiInsights);
    renderUsageResponseChart(usageTrend, aiInsights);
    renderAiStabilityChart(kpis, aiInsights, stability);
    renderServiceBreakdown(aiInsights);

    const topContributors = Array.isArray(data?.topContributors) ? data.topContributors : [];
    if (el.topContributors) {
      el.topContributors.innerHTML = topContributors.length === 0
      ? '<div class="empty-state">Chưa có dữ liệu người dùng nổi bật.</div>'
      : topContributors.map((item) => `
          <div class="contributor-item">
            <strong>${escapeHtml(item.fullName || item.username || "Người dùng")}</strong>
            <div class="row-meta">@${escapeHtml(item.username)} • Nội dung ${formatNumber(item.contents)} • Quiz ${formatNumber(item.attempts)}</div>
          </div>
        `).join("");
    }

    const recentActivities = Array.isArray(data?.recentActivities) ? data.recentActivities : [];
    setText(el.recentActivityCount, `${formatNumber(recentActivities.length)} sự kiện`);
    if (el.recentActivities) {
      el.recentActivities.innerHTML = recentActivities.length === 0
      ? '<div class="empty-state">Chưa có hoạt động hệ thống gần đây.</div>'
      : recentActivities.map((item) => `
          <div class="activity-item">
            <strong>${escapeHtml(item.title)}</strong>
            <div class="row-meta">${escapeHtml(translateLabel(item.kind, item.kind))} • ${escapeHtml(item.meta)} • ${formatDateTime(item.at)}</div>
          </div>
        `).join("");
    }
  };

  const renderAdminAccounts = (data) => {
    if (!el.adminAccounts) {
      return;
    }

    const items = Array.isArray(data?.items) ? data.items : [];
    renderAdminCreationChart(items);

    if (el.adminRosterMeta) {
      el.adminRosterMeta.textContent = `${formatNumber(data?.totalItems || 0)} tài khoản • ${formatNumber(data?.activeItems || 0)} đang hoạt động`;
    }

    el.adminAccounts.innerHTML = items.length === 0
      ? '<div class="empty-state">Chưa có tài khoản quản trị nào trong hệ thống.</div>'
      : items.map((item) => `
          <article class="admin-account-card">
            <div class="admin-account-top">
              <div>
                <strong>${escapeHtml(item.fullName || item.username)}</strong>
                <p>@${escapeHtml(item.username)}</p>
              </div>
              <span class="admin-avatar">${escapeHtml(getInitials(item.fullName || item.username))}</span>
            </div>
            <p>${escapeHtml(item.email)}</p>
            <div class="admin-account-metrics">
              ${statusPill(item.isLocked ? "Locked" : "Active")}
              ${item.isEmailVerified ? statusPill("Verified") : statusPill("Unverified")}
              <span class="metric-chip">Nội dung ${formatNumber(item.contentsCount)}</span>
              <span class="metric-chip">Quiz ${formatNumber(item.quizAttemptsCount)}</span>
            </div>
            <p>Tạo lúc ${formatDateTime(item.createdAt)}</p>
          </article>
        `).join("");
  };

  const renderAdminCreationChart = (items) => {
    if (!el.adminCreationTrend) {
      return;
    }

    if (!Array.isArray(items) || items.length === 0) {
      el.adminCreationTrend.innerHTML = '<div class="empty-state">Chưa có tài khoản quản trị để hiển thị biểu đồ.</div>';
      setText(el.adminCreationTrendMeta, "6 tháng gần đây");
      return;
    }

    const now = new Date();
    const months = Array.from({ length: 6 }, (_, index) => {
      const date = new Date(now.getFullYear(), now.getMonth() - (5 - index), 1);
      return {
        key: `${date.getFullYear()}-${date.getMonth()}`,
        label: formatMonthLabel(date),
        fullLabel: new Intl.DateTimeFormat("vi-VN", { month: "long", year: "numeric" }).format(date),
        count: 0,
      };
    });

    const monthMap = new Map(months.map((item) => [item.key, item]));
    items.forEach((item) => {
      const createdAt = item?.createdAt ? new Date(item.createdAt) : null;
      if (!createdAt || Number.isNaN(createdAt.getTime())) {
        return;
      }

      const key = `${createdAt.getFullYear()}-${createdAt.getMonth()}`;
      const target = monthMap.get(key);
      if (target) {
        target.count += 1;
      }
    });

    const maxValue = Math.max(1, ...months.map((item) => item.count));
    const totalRecentAdmins = months.reduce((sum, item) => sum + item.count, 0);
    const peakMonth = months.reduce((best, item) => item.count > best.count ? item : best, months[0]);
    const averagePerMonth = totalRecentAdmins / Math.max(1, months.length);
    setText(el.adminCreationTrendMeta, `${formatNumber(totalRecentAdmins)} tài khoản • 6 tháng gần đây`);

    const svgWidth = 640;
    const svgHeight = 252;
    const topY = 42;
    const bottomY = 166;
    const leftX = 42;
    const rightX = 598;
    const slotWidth = (rightX - leftX) / Math.max(1, months.length);
    const barWidth = Math.min(52, slotWidth * 0.42);
    const points = months.map((item, index) => {
      const centerX = leftX + (slotWidth * index) + (slotWidth / 2);
      const height = item.count === 0 ? 6 : Math.max(18, ((bottomY - topY) * (item.count / maxValue)));
      return {
        ...item,
        centerX,
        barHeight: height,
        top: bottomY - height,
      };
    });
    const trendPath = buildLinePath(points.map((item) => ({
      x: item.centerX,
      y: item.top + 12,
    })));
    const trendArea = buildAreaPath(points.map((item) => ({
      x: item.centerX,
      y: item.top + 12,
    })), bottomY);
    const gridLines = [0, 1, 2, 3].map((index) => {
      const y = topY + (((bottomY - topY) / 3) * index);
      return `<line class="gridline" x1="${leftX}" y1="${y.toFixed(1)}" x2="${rightX}" y2="${y.toFixed(1)}"></line>`;
    }).join("");
    const bars = points.map((item, index) => {
      const isPeak = item.key === peakMonth.key && peakMonth.count > 0;
      const x = item.centerX - (barWidth / 2);
      const placeValueInsideBar = item.top <= topY + 18;
      const valueY = placeValueInsideBar ? item.top + 20 : item.top - 8;
      const badgeY = item.top <= topY + 30
        ? item.top + 38
        : Math.max(topY - 4, item.top - 28);

      return `
        <rect
          class="admin-chart-bar-svg${isPeak ? " is-peak" : ""}${item.count === 0 ? " is-empty" : ""}"
          x="${x.toFixed(1)}"
          y="${item.top.toFixed(1)}"
          width="${barWidth.toFixed(1)}"
          height="${item.barHeight.toFixed(1)}"
          rx="18"
        ></rect>
        <text class="admin-chart-value${isPeak ? " is-peak" : ""}${placeValueInsideBar ? " is-inside" : ""}" x="${item.centerX.toFixed(1)}" y="${valueY.toFixed(1)}" text-anchor="middle">
          ${formatNumber(item.count)}
        </text>
        <text class="admin-chart-month" x="${item.centerX.toFixed(1)}" y="214" text-anchor="middle">
          ${escapeHtml(item.label)}
        </text>
        ${isPeak ? `<text class="admin-chart-badge${item.top <= topY + 30 ? " is-inside" : ""}" x="${item.centerX.toFixed(1)}" y="${badgeY.toFixed(1)}" text-anchor="middle">Cao nhất</text>` : ""}
      `;
    }).join("");
    const hoverZones = points.map((item, index) => {
      const x = index === 0 ? leftX - 12 : item.centerX - (slotWidth / 2);
      const lines = [
        `Tài khoản tạo mới: ${formatNumber(item.count)}`,
        `Tỷ trọng: ${formatPercent((item.count / Math.max(1, totalRecentAdmins)) * 100)}`,
        item.count > 0
          ? `So với trung bình tháng: ${formatPercent((item.count / Math.max(0.1, averagePerMonth)) * 100)}`
          : "Chưa ghi nhận tài khoản mới trong tháng này",
      ];

      return `
        <rect
          class="chart-hover-zone"
          x="${x.toFixed(1)}"
          y="${topY}"
          width="${slotWidth.toFixed(1)}"
          height="${(bottomY - topY + 36).toFixed(1)}"
          rx="18"
          data-chart-tooltip-title="${escapeHtml(item.fullLabel)}"
          data-chart-tooltip-lines="${encodeTooltipLines(lines)}"
        ></rect>
      `;
    }).join("");

    el.adminCreationTrend.innerHTML = `
      <div class="admin-chart-shell">
        <div class="admin-chart-stage">
          <div class="admin-chart-glow"></div>
          <svg class="dashboard-chart admin-dashboard-chart" viewBox="0 0 ${svgWidth} ${svgHeight}" role="img" aria-label="Biểu đồ số tài khoản quản trị được tạo trong 6 tháng gần đây">
            <defs>
              <linearGradient id="adminCreationBarGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stop-color="#ffd58c"></stop>
                <stop offset="52%" stop-color="#f4b65d"></stop>
                <stop offset="100%" stop-color="#ef7f73"></stop>
              </linearGradient>
              <linearGradient id="adminCreationLineGradient" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stop-color="#65d9cb"></stop>
                <stop offset="100%" stop-color="#ffd58c"></stop>
              </linearGradient>
            </defs>
            ${gridLines}
            <line class="axis" x1="${leftX}" y1="${bottomY}" x2="${rightX}" y2="${bottomY}"></line>
            <path class="admin-chart-area" d="${trendArea}"></path>
            <path class="admin-chart-line" d="${trendPath}"></path>
            ${bars}
            ${hoverZones}
            <text class="admin-chart-axis-label" x="${leftX}" y="22">Tạo mới theo tháng</text>
            <text class="admin-chart-axis-label admin-chart-axis-label-end" x="${rightX}" y="22" text-anchor="end">6 tháng gần đây</text>
          </svg>
        </div>
        <div class="admin-chart-insights">
          <article class="admin-chart-insight">
            <span>Tổng 6 tháng</span>
            <strong>${formatNumber(totalRecentAdmins)}</strong>
            <p>Tổng số tài khoản quản trị mới trong giai đoạn đang theo dõi.</p>
          </article>
          <article class="admin-chart-insight">
            <span>Tháng cao nhất</span>
            <strong>${escapeHtml(peakMonth.count > 0 ? peakMonth.label : "--")}</strong>
            <p>${peakMonth.count > 0 ? `${formatNumber(peakMonth.count)} tài khoản, là tháng tăng mạnh nhất.` : "Chưa có tài khoản quản trị mới trong giai đoạn này."}</p>
          </article>
          <article class="admin-chart-insight">
            <span>Nhịp trung bình</span>
            <strong>${averagePerMonth.toFixed(1)}</strong>
            <p>Số tài khoản admin tạo mới trung bình mỗi tháng.</p>
          </article>
        </div>
      </div>
    `;
    bindChartTooltips(el.adminCreationTrend);
  };

  const renderAdminProfile = (data) => {
    const profile = data?.profile || {};
    const adminStats = data?.adminStats || {};
    const displayName = toText(profile.fullName || profile.username) || "Quản trị viên";

    state.adminProfile = {
      fullName: toText(profile.fullName),
      email: toText(profile.email),
      phone: toText(profile.phone),
      bio: toText(profile.bio),
    };

    setText(el.adminProfileCardName, displayName);
    setText(el.adminProfileCardEmail, toText(profile.email) || "--");
    setText(el.adminProfileCardRole, translateLabel(profile.role || "Admin"));
    setText(el.adminProfileCardUsername, `@${toText(profile.username) || "--"}`);
    setText(el.adminProfileCardCreatedAt, formatDateTime(profile.createdAt));
    setText(el.adminProfileCardStatus, profile.isLocked ? "Đang bị khóa" : "Đang hoạt động");
    setText(el.adminProfileCardVerified, profile.isEmailVerified ? "Email đã xác thực" : "Email chưa xác thực");

    setText(el.profileStatAuditActions, formatNumber(adminStats.totalAuditActions));
    setText(el.profileStatManagedUsers, formatNumber(adminStats.managedUsers));
    setText(el.profileStatReviewedContents, formatNumber(adminStats.reviewedContents));
    setText(el.profileStatCreatedAdmins, formatNumber(adminStats.createdAdmins));
    setText(el.profileStatUploads, formatNumber(profile.totalUploads));
    setText(el.profileStatQuizAttempts, formatNumber(profile.totalQuizAttempts));
    setText(el.profileStatAverageScore, `${Number(profile.averageQuizScore || 0).toFixed(2)} điểm`);
    setText(el.profileStatActiveDays, formatNumber(profile.activeLearningDays));
    setText(el.profileStatLastActionAt, formatDateTime(adminStats.lastAdminActionAt));

    setValue(el.adminProfileUsername, toText(profile.username));
    setValue(el.adminProfileRole, translateLabel(profile.role || "Admin"));
    setValue(el.adminProfileCreatedAt, formatDateTime(profile.createdAt));
    setValue(el.adminProfileFullName, toText(profile.fullName));
    setValue(el.adminProfileEmail, toText(profile.email));
    setValue(el.adminProfilePhone, toText(profile.phone));
    setValue(el.adminProfileBio, toText(profile.bio));

    setText(el.adminSessionName, displayName);
    setText(el.adminSessionRole, translateLabel(profile.role || "Admin"));
    setText(el.adminSessionAvatar, getInitials(displayName));

    syncStoredProfileIdentity(profile);
  };

  const openDetailsDrawer = (config) => {
    if (!el.detailsDrawer) {
      return;
    }

    el.detailsDrawerEyebrow.textContent = toText(config?.eyebrow) || "Chi tiết hệ thống";
    el.detailsDrawerTitle.textContent = toText(config?.title) || "Chi tiết";
    el.detailsDrawerMeta.textContent = toText(config?.meta);
    if (el.detailsDrawerBody) {
      el.detailsDrawerBody.innerHTML = config?.bodyHtml || "";
      if (!config?.bodyHtml) {
        el.detailsDrawerBody.textContent = toText(config?.body);
      }
    }
    el.detailsDrawer.classList.add("is-open");
    el.detailsDrawer.setAttribute("aria-hidden", "false");
  };

  const closeDetailsDrawer = () => {
    if (!el.detailsDrawer) {
      return;
    }

    el.detailsDrawer.classList.remove("is-open");
    el.detailsDrawer.setAttribute("aria-hidden", "true");
  };

  const openActionModal = (config) => {
    if (!actionModal) {
      return;
    }

    state.actionContext = config || null;
    el.adminActionEyebrow.textContent = toText(config?.eyebrow) || "Thao tác quản trị";
    el.adminActionTitle.textContent = toText(config?.title) || "Xác nhận thao tác";
    el.adminActionDescription.textContent = toText(config?.description) || "";
    el.adminActionKind.value = toText(config?.kind);
    el.adminActionTargetId.value = String(config?.targetId || "");
    el.adminActionReasonLabel.textContent = toText(config?.reasonLabel) || "Ghi chú";
    el.adminActionReason.placeholder = toText(config?.reasonPlaceholder) || "Nhập ghi chú để lưu vào nhật ký quản trị";
    el.adminActionReason.value = toText(config?.defaultReason);
    setInlineFeedback(el.adminActionFeedback, "", "");

    if (!el.adminActionSubmit.dataset.originalLabel) {
      el.adminActionSubmit.dataset.originalLabel = el.adminActionSubmit.textContent || "";
    }

    el.adminActionSubmit.textContent = toText(config?.submitText) || "Xác nhận";
    el.adminActionSubmit.dataset.originalLabel = el.adminActionSubmit.textContent;
    actionModal.show();
  };

  const openUserEditor = (item) => {
    if (!userEditorModal || !item) {
      return;
    }

    el.userEditorMode.value = "edit";
    el.userEditorEyebrow.textContent = "Quản lý người dùng";
    el.userEditorTitle.textContent = "Cập nhật tài khoản";
    el.userEditorId.value = String(item.userId || "");
    el.userEditorUsername.value = toText(item.username);
    el.userEditorFullName.value = toText(item.fullName || item.username);
    el.userEditorEmail.value = toText(item.email);
    el.userEditorRole.value = String(item.role || "User").toLowerCase() === "admin" ? "Admin" : "User";
    el.userEditorPasswordFields?.classList.add("d-none");
    el.userEditorPassword.value = "";
    el.userEditorConfirmPassword.value = "";
    el.userEditorLocked.checked = Boolean(item.isLocked);
    el.userEditorVerified.checked = Boolean(item.isEmailVerified);
    setInlineFeedback(el.userEditorFeedback, "", "");
    userEditorModal.show();
  };

  const openCreateUserModal = () => {
    if (!userEditorModal) {
      return;
    }

    el.userEditorForm?.reset();
    el.userEditorMode.value = "create";
    el.userEditorId.value = "";
    el.userEditorEyebrow.textContent = "Tạo tài khoản";
    el.userEditorTitle.textContent = "Tạo tài khoản mới";
    el.userEditorRole.value = "User";
    el.userEditorVerified.checked = true;
    el.userEditorLocked.checked = false;
    el.userEditorPasswordFields?.classList.remove("d-none");
    setInlineFeedback(el.userEditorFeedback, "", "");
    userEditorModal.show();
  };

  const buildPrettyJson = (value) => {
    if (value == null) {
      return "{}";
    }

    if (typeof value === "string") {
      const trimmed = value.trim();
      if (!trimmed) {
        return "{}";
      }

      try {
        return JSON.stringify(JSON.parse(trimmed), null, 2);
      } catch {
        return trimmed;
      }
    }

    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return String(value);
    }
  };

  const buildDetailField = (label, value) => `
    <article class="details-report-field">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value || "--")}</strong>
    </article>
  `;

  const buildDetailMetric = (label, value, meta, tone = "") => `
    <article class="details-report-metric ${tone}">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value || "--")}</strong>
      <small>${escapeHtml(meta || "")}</small>
    </article>
  `;

  const buildDetailSection = (title, fieldsHtml) => `
    <section class="details-report-section">
      <div class="details-report-section-title">${escapeHtml(title)}</div>
      <div class="details-report-grid">
        ${fieldsHtml}
      </div>
    </section>
  `;

  const getAdminAlertKindLabel = (kind) => {
    const value = toText(kind).toLowerCase();
    if (value === "incident") {
      return "Sự cố";
    }

    if (value === "moderation") {
      return "Kiểm duyệt";
    }

    return "Thông báo";
  };

  const renderAdminAlerts = (data) => {
    if (!el.adminAlertsList || !el.adminAlertsMeta || !el.adminAlertsDot || !el.adminAlertsToggle) {
      return;
    }

    state.adminAlerts = data || null;
    state.adminAlertItems = Array.isArray(data?.items) ? data.items : [];
    const seenAtMs = getAdminAlertsSeenAtMs();
    const unreadCount = state.adminAlertItems.filter((item) => (Date.parse(item?.createdAt || "") || 0) > seenAtMs).length;
    const pendingModeration = Number(data?.counts?.pendingModeration || 0);
    const aiIncidents24h = Number(data?.counts?.aiIncidents24h || 0);

    el.adminAlertsDot.hidden = unreadCount <= 0;
    el.adminAlertsToggle.setAttribute(
      "aria-label",
      unreadCount > 0
        ? `Thông báo quản trị, có ${unreadCount} mục mới`
        : "Thông báo quản trị"
    );
    el.adminAlertsMeta.textContent = unreadCount > 0
      ? `${unreadCount} mục mới • ${pendingModeration} chờ duyệt • ${aiIncidents24h} sự cố AI 24h`
      : `${pendingModeration} chờ duyệt • ${aiIncidents24h} sự cố AI 24h`;

    if (state.adminAlertItems.length === 0) {
      el.adminAlertsList.innerHTML = '<div class="admin-alerts-empty">Chưa có sự cố AI hoặc nội dung chờ duyệt mới.</div>';
      return;
    }

    el.adminAlertsList.innerHTML = state.adminAlertItems.map((item, index) => {
      const tone = toText(item?.severity).toLowerCase();
      const itemMs = Date.parse(item?.createdAt || "") || 0;
      const isNew = itemMs > seenAtMs;
      return `
        <button type="button" class="admin-alert-item ${tone === "danger" ? "is-danger" : tone === "warning" ? "is-warning" : ""}" data-admin-alert-index="${index}">
          <div class="admin-alert-item-top">
            <span class="admin-alert-item-title">${escapeHtml(item?.title || "Thông báo quản trị")}</span>
            <span class="admin-alert-item-badge">${escapeHtml(isNew ? "Mới" : getAdminAlertKindLabel(item?.kind))}</span>
          </div>
          <div class="admin-alert-item-message">${escapeHtml(item?.message || "")}</div>
          <div class="admin-alert-item-meta">
            <span>${escapeHtml(getAdminAlertKindLabel(item?.kind))}</span>
            <span>${escapeHtml(formatDateTime(item?.createdAt))}</span>
          </div>
        </button>
      `;
    }).join("");

    Array.from(el.adminAlertsList.querySelectorAll("[data-admin-alert-index]")).forEach((node) => {
      node.addEventListener("click", async () => {
        const index = Number(node.getAttribute("data-admin-alert-index"));
        const item = state.adminAlertItems[index];
        const targetUrl = toText(item?.actionUrl);
        markAdminAlertsSeen();
        closeAdminAlertsFlyout();

        if (!targetUrl) {
          return;
        }

        if (window.AjaxNavigation?.navigate) {
          await window.AjaxNavigation.navigate(targetUrl);
          return;
        }

        window.location.assign(targetUrl);
      });
    });
  };

  const loadAdminAlerts = async (options = {}) => {
    if (!el.adminAlertsToggle || !el.adminAlertsList || !el.adminAlertsMeta) {
      return;
    }

    if (!options.silent) {
      el.adminAlertsMeta.textContent = "Đang tải cảnh báo hệ thống...";
      el.adminAlertsList.innerHTML = '<div class="admin-alerts-empty">Đang đồng bộ log sự cố và hàng chờ kiểm duyệt...</div>';
    }

    const data = await apiFetch("/api/admin/alerts?limit=8");
    if (!data) {
      return;
    }

    if (!options.silent || hasRenderChanged("adminAlerts", data)) {
      renderAdminAlerts(data);
      return;
    }

    renderAdminAlerts(state.adminAlerts || data);
  };

  const buildAiLogReport = (item) => {
    const processingMs = formatNumber(Math.round(Number(item?.processingTime || 0) * 1000));
    const createdAt = formatDateTime(item?.createdAt);
    const actorName = item?.user?.username || "Khách/Hệ thống";
    const actorEmail = item?.user?.email || (item?.isGuest ? "Phiên khách" : "Không có email");
    const actorId = item?.user?.userId ? `#${item.user.userId}` : "--";
    const sourceType = item?.isGuest ? "Khách" : item?.user ? "Người dùng" : "Hệ thống";
    const statusTone = item?.isError ? "is-danger" : "is-good";
    const statusMeta = item?.isError ? "Cần kiểm tra luồng xử lý" : "Yêu cầu hoàn tất bình thường";
    const payload = buildPrettyJson(item);

    return `
      <div class="details-report">
        <section class="details-report-metrics">
          ${buildDetailMetric("Trạng thái", translateLabel(item?.isError ? "Error" : "Success"), statusMeta, statusTone)}
          ${buildDetailMetric("Độ trễ", `${processingMs} ms`, "Thời gian xử lý của tác vụ AI")}
          ${buildDetailMetric("Nguồn gọi", sourceType, actorName)}
          ${buildDetailMetric("Mã log", toText(item?.logId) || "--", createdAt)}
        </section>

        ${buildDetailSection("Tổng quan sự kiện", [
          buildDetailField("Tác vụ AI", translateLabel(item?.actionType, item?.actionType || "Tác vụ AI")),
          buildDetailField("Kết quả", translateLabel(item?.isError ? "Error" : "Success")),
          buildDetailField("Thời điểm", createdAt),
          buildDetailField("Phiên khách", item?.isGuest ? "Có" : "Không"),
        ].join(""))}

        ${buildDetailSection("Danh tính người gửi", [
          buildDetailField("Username", actorName),
          buildDetailField("Email", actorEmail),
          buildDetailField("User ID", actorId),
          buildDetailField("Loại nguồn", sourceType),
        ].join(""))}

        ${buildDetailSection("Chi tiết kỹ thuật", [
          buildDetailField("Processing time", `${processingMs} ms`),
          buildDetailField("Action type", toText(item?.actionType) || "--"),
          buildDetailField("Is error", item?.isError ? "true" : "false"),
          buildDetailField("Created at (raw)", toText(item?.createdAt) || "--"),
        ].join(""))}

        <section class="details-report-section">
          <div class="details-report-section-title">Payload gốc</div>
          <pre class="details-drawer-code">${escapeHtml(payload)}</pre>
        </section>
      </div>
    `;
  };

  const buildAuditLogReport = (item) => {
    const payload = buildPrettyJson(item?.detailJson || item);
    const adminName = item?.admin?.username || "Quản trị viên không xác định";
    const adminEmail = item?.admin?.email || "Không có email";
    const targetText = `${translateLabel(item?.targetType, item?.targetType || "Đối tượng")}${item?.targetId ? ` #${item.targetId}` : ""}`;

    return `
      <div class="details-report">
        <section class="details-report-metrics">
          ${buildDetailMetric("Hành động", translateLabel(item?.actionType, item?.actionType || "Hành động"), "Nhật ký quản trị")}
          ${buildDetailMetric("Đối tượng", targetText, "Bản ghi bị tác động")}
          ${buildDetailMetric("Quản trị viên", adminName, adminEmail)}
          ${buildDetailMetric("Địa chỉ IP", toText(item?.ipAddress) || "--", formatDateTime(item?.createdAt))}
        </section>

        ${buildDetailSection("Tổng quan kiểm toán", [
          buildDetailField("Action type", translateLabel(item?.actionType, item?.actionType || "--")),
          buildDetailField("Target type", translateLabel(item?.targetType, item?.targetType || "--")),
          buildDetailField("Target ID", toText(item?.targetId) || "--"),
          buildDetailField("Thời điểm", formatDateTime(item?.createdAt)),
        ].join(""))}

        ${buildDetailSection("Người thực hiện", [
          buildDetailField("Username", adminName),
          buildDetailField("Email", adminEmail),
          buildDetailField("IP address", toText(item?.ipAddress) || "--"),
          buildDetailField("Vai trò", "Quản trị viên"),
        ].join(""))}

        <section class="details-report-section">
          <div class="details-report-section-title">Payload gốc</div>
          <pre class="details-drawer-code">${escapeHtml(payload)}</pre>
        </section>
      </div>
    `;
  };

  const loadOverview = async (options = {}) => {
    if (!el.healthBadge &&
        !el.kpiTotalUsers &&
        !el.usageTrend &&
        !el.topContributors &&
        !el.recentActivities) {
      return;
    }

    const windowQuery = buildDashboardWindowQuery().toString();

    const [overviewResult, aiInsightsResult] = await Promise.allSettled([
      apiFetch(`/api/admin/overview?${windowQuery}`),
      apiFetch(`/api/admin/ai-logs?page=1&pageSize=1&${windowQuery}`),
    ]);

    if (overviewResult.status !== "fulfilled" || !overviewResult.value) {
      if (overviewResult.status === "rejected") {
        throw overviewResult.reason;
      }
      return;
    }

    if (aiInsightsResult.status === "rejected") {
      console.warn("Không thể tải analytics AI cho dashboard admin.", aiInsightsResult.reason);
    }

    const aiInsightsPayload = aiInsightsResult.status === "fulfilled" ? aiInsightsResult.value : null;
    const combinedPayload = {
      overview: overviewResult.value,
      aiInsights: aiInsightsPayload,
    };
    if (options.silent && !hasRenderChanged("overview", combinedPayload)) {
      return;
    }

    renderOverview(
      overviewResult.value,
      aiInsightsPayload
    );
  };

  const loadAdminAccounts = async (options = {}) => {
    if (!el.adminAccounts) {
      return;
    }

    const data = await apiFetch("/api/admin/admin-users");
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("adminAccounts", data)) {
      return;
    }

    renderAdminAccounts(data);
  };

  const renderNotificationHistory = (data) => {
    if (!el.notificationHistoryTableBody) {
      return;
    }

    const items = Array.isArray(data?.items) ? data.items : [];
    patchTableRows(el.notificationHistoryTableBody, items, {
      emptyHtml: '<tr><td colspan="5" class="text-center row-meta py-4">Chưa có thông báo quản trị nào được gửi.</td></tr>',
      getKey: (item) => item.logId || `${item.createdAt || "--"}-${item.title || "--"}-${item.targetScope || "--"}`,
      renderRow: (item) => renderNotificationHistoryRow(item),
    });

    state.notifications.page = Number(data?.page || 1);
    state.notifications.totalPages = Number(data?.totalPages || 1);
    renderPager(el.notificationHistoryPager, state.notifications, data?.totalItems, "notifications");
  };

  const loadNotificationHistory = async (options = {}) => {
    if (!el.notificationHistoryTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.notificationHistoryTableBody, 5, "Đang tải lịch sử thông báo...");
    }
    const query = new URLSearchParams({
      page: String(state.notifications.page),
      pageSize: String(state.notifications.pageSize),
    });

    const data = await apiFetch(`/api/admin/notifications?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("notifications", data)) {
      return;
    }

    renderNotificationHistory(data);
  };

  const loadAdminProfile = async (options = {}) => {
    if (!el.adminProfileForm &&
        !el.adminPasswordForm &&
        !el.adminProfileCardName &&
        !el.profileStatAuditActions) {
      return;
    }

    const data = await apiFetch("/api/admin/profile");
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("adminProfile", data)) {
      return;
    }

    renderAdminProfile(data);
  };

  const renderUsers = (data) => {
    const items = Array.isArray(data?.items) ? data.items : [];
    state.userItems = items;

    patchTableRows(el.usersTableBody, items, {
      emptyHtml: '<tr><td colspan="8" class="text-center row-meta py-4">Không có người dùng phù hợp.</td></tr>',
      getKey: (item) => item.userId,
      renderRow: (item) => renderUserRow(item),
    });

    state.users.page = Number(data?.page || 1);
    state.users.totalPages = Number(data?.totalPages || 1);
    renderPager(el.usersPager, state.users, data?.totalItems, "users");
  };

  const loadUsers = async (options = {}) => {
    if (!el.usersTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.usersTableBody, 8, "Đang tải danh sách người dùng...");
    }

    const query = new URLSearchParams({
      page: String(state.users.page),
      pageSize: String(state.users.pageSize),
      query: toText(el.userQuery.value),
      status: toText(el.userStatus.value || "all"),
      role: toText(el.userRole.value || "all"),
    });

    const data = await apiFetch(`/api/admin/users?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("users", data)) {
      return;
    }

    renderUsers(data);
  };

  const renderPremiumOverview = (data) => {
    state.premiumOverview = data || null;
    const settings = data?.settings || {};
    const metrics = data?.metrics || {};

    setValue(el.premiumAmount, settings.amount ?? 0);
    setValue(el.premiumDays, settings.days ?? 30);
    setText(el.premiumSettingsMeta, settings.updatedAt ? `Cập nhật ${formatDateTime(settings.updatedAt)}` : "Theo cấu hình mặc định");
    setText(el.premiumMetricActive, formatNumber(metrics.activePremiumUsers));
    setText(el.premiumMetricExpired, formatNumber(metrics.expiredPremiumUsers));
    setText(el.premiumMetricRevenue, formatCurrency(metrics.totalRevenue));
    setText(el.premiumMetricPending, formatNumber(metrics.pendingTransactions));
  };

  const loadPremiumOverview = async (options = {}) => {
    if (!el.premiumSettingsForm && !el.premiumMetricActive) {
      return;
    }

    const data = await apiFetch("/api/admin/premium/overview");
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("premiumOverview", data)) {
      return;
    }

    renderPremiumOverview(data);
  };

  const renderPremiumTransactions = (data) => {
    const items = Array.isArray(data?.items) ? data.items : [];
    state.premiumTransactionItems = items;

    patchTableRows(el.premiumTransactionsTableBody, items, {
      emptyHtml: '<tr><td colspan="7" class="text-center row-meta py-4">Không có giao dịch Premium phù hợp.</td></tr>',
      getKey: (item) => item.paymentTransactionId,
      renderRow: (item) => renderPremiumTransactionRow(item),
    });

    state.premiumTransactions.page = Number(data?.page || 1);
    state.premiumTransactions.totalPages = Number(data?.totalPages || 1);
    renderPager(el.premiumTransactionsPager, state.premiumTransactions, data?.totalItems, "premiumTransactions");
  };

  const loadPremiumTransactions = async (options = {}) => {
    if (!el.premiumTransactionsTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.premiumTransactionsTableBody, 7, "Đang tải giao dịch Premium...");
    }

    const query = new URLSearchParams({
      page: String(state.premiumTransactions.page),
      pageSize: String(state.premiumTransactions.pageSize),
      query: toText(el.premiumTransactionQuery?.value),
      status: toText(el.premiumTransactionStatus?.value || "all"),
    });

    const data = await apiFetch(`/api/admin/premium/transactions?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("premiumTransactions", data)) {
      return;
    }

    renderPremiumTransactions(data);
  };

  const renderPremiumUsers = (data) => {
    const items = Array.isArray(data?.items) ? data.items : [];
    state.premiumUserItems = items;

    patchTableRows(el.premiumUsersTableBody, items, {
      emptyHtml: '<tr><td colspan="6" class="text-center row-meta py-4">Không có user Premium phù hợp.</td></tr>',
      getKey: (item) => item.userId,
      renderRow: (item) => renderPremiumUserRow(item),
    });

    state.premiumUsers.page = Number(data?.page || 1);
    state.premiumUsers.totalPages = Number(data?.totalPages || 1);
    renderPager(el.premiumUsersPager, state.premiumUsers, data?.totalItems, "premiumUsers");
  };

  const loadPremiumUsers = async (options = {}) => {
    if (!el.premiumUsersTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.premiumUsersTableBody, 6, "Đang tải danh sách Premium...");
    }

    const query = new URLSearchParams({
      page: String(state.premiumUsers.page),
      pageSize: String(state.premiumUsers.pageSize),
      query: toText(el.premiumUserQuery?.value),
      status: toText(el.premiumUserStatus?.value || "all"),
    });

    const data = await apiFetch(`/api/admin/premium/users?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("premiumUsers", data)) {
      return;
    }

    renderPremiumUsers(data);
  };

  const loadPremiumPage = async (options = {}) => {
    await Promise.all([
      loadPremiumOverview(options),
      loadPremiumTransactions(options),
      loadPremiumUsers(options),
    ]);
  };

  const renderContents = (data) => {
    const items = Array.isArray(data?.items) ? data.items : [];
    state.contentItems = items;

    patchTableRows(el.contentsTableBody, items, {
      emptyHtml: '<tr><td colspan="6" class="text-center row-meta py-4">Không có nội dung phù hợp.</td></tr>',
      getKey: (item) => item.contentId,
      renderRow: (item) => renderContentRow(item),
    });

    state.contents.page = Number(data?.page || 1);
    state.contents.totalPages = Number(data?.totalPages || 1);
    renderPager(el.contentsPager, state.contents, data?.totalItems, "contents");
  };

  const loadContents = async (options = {}) => {
    if (!el.contentsTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.contentsTableBody, 6, "Đang tải danh sách nội dung...");
    }

    const query = new URLSearchParams({
      page: String(state.contents.page),
      pageSize: String(state.contents.pageSize),
      query: toText(el.contentQuery.value),
      moderationStatus: toText(el.contentStatus.value || "all"),
    });

    const data = await apiFetch(`/api/admin/contents?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("contents", data)) {
      return;
    }

    renderContents(data);
  };

  const renderAiSystemSettings = (data) => {
    state.aiSystem = data || null;

    const routing = data?.routing || {};
    const gemini = data?.gemini || {};
    const groq = data?.groq || {};

    setValue(el.aiPrimaryTextProvider, routing.primaryTextProvider || "gemini");
    setValue(el.aiPrimaryVisionProvider, routing.primaryVisionProvider || "gemini");
    setValue(el.aiTextOutputTokenBudget, routing.textOutputTokenBudget ?? 1500);
    setValue(el.aiQuizOutputTokenBudget, routing.quizOutputTokenBudget ?? 2200);
    setValue(el.aiImageOutputTokenBudget, routing.imageOutputTokenBudget ?? 900);
    setValue(el.aiApproxCharsPerToken, routing.approxCharsPerToken ?? 4);
    setValue(el.aiGeminiDailyTokenBudget, routing.geminiDailyTokenBudget ?? 0);
    setValue(el.aiGroqDailyTokenBudget, routing.groqDailyTokenBudget ?? 0);
    setValue(el.aiMinReservedTokensPerProvider, routing.minReservedTokensPerProvider ?? 0);
    setValue(el.aiProviderExecutionTimeoutMs, routing.providerExecutionTimeoutMs ?? 12000);
    setValue(el.aiSlowRequestThresholdMs, routing.slowRequestThresholdMs ?? 9000);
    setValue(el.aiSlowRequestStreakThreshold, routing.slowRequestStreakThreshold ?? 1);
    setValue(el.aiConsecutiveFailureThreshold, routing.consecutiveFailureThreshold ?? 2);
    setValue(el.aiProviderCooldownSeconds, routing.providerCooldownSeconds ?? 45);

    if (el.aiEnforceDailyTokenBudget) el.aiEnforceDailyTokenBudget.checked = Boolean(routing.enforceDailyTokenBudget);
    if (el.aiEnableProviderHealthSwitch) el.aiEnableProviderHealthSwitch.checked = Boolean(routing.enableProviderHealthSwitch);
    if (el.aiPreferFastestHealthyProvider) el.aiPreferFastestHealthyProvider.checked = Boolean(routing.preferFastestHealthyProvider);

    if (el.aiGeminiKeyStatus) {
      el.aiGeminiKeyStatus.textContent = gemini.hasApiKey ? "Đã có API key" : "Chưa có API key";
    }
    if (el.aiGroqKeyStatus) {
      el.aiGroqKeyStatus.textContent = groq.hasApiKey ? "Đã có API key" : "Chưa có API key";
    }

    if (el.aiGeminiApiKey) el.aiGeminiApiKey.value = "";
    if (el.aiGeminiClearApiKey) el.aiGeminiClearApiKey.checked = false;
    setValue(el.aiGeminiBaseUrl, gemini.baseUrl || "");
    setValue(el.aiGeminiTextModel, gemini.textModel || "");
    setValue(el.aiGeminiVisionModel, gemini.visionModel || "");
    setValue(el.aiGeminiMaxInputCharacters, gemini.maxInputCharacters ?? 0);
    setValue(el.aiGeminiMaxQuizInputCharacters, gemini.maxQuizInputCharacters ?? 0);
    setValue(el.aiGeminiRequestTimeoutSeconds, gemini.requestTimeoutSeconds ?? 20);
    setValue(el.aiGeminiMaxModelCandidates, gemini.maxModelCandidates ?? 2);
    setValue(el.aiGeminiMaxRetriesPerModel, gemini.maxRetriesPerModel ?? 1);
    setValue(el.aiGeminiFallbackModels, formatModelList(gemini.fallbackModels));

    if (el.aiGroqApiKey) el.aiGroqApiKey.value = "";
    if (el.aiGroqClearApiKey) el.aiGroqClearApiKey.checked = false;
    setValue(el.aiGroqBaseUrl, groq.baseUrl || "");
    setValue(el.aiGroqTextModel, groq.textModel || "");
    setValue(el.aiGroqVisionModel, groq.visionModel || "");
    setValue(el.aiGroqAudioModel, groq.audioModel || "");
    setValue(el.aiGroqMaxInputCharacters, groq.maxInputCharacters ?? 0);
    setValue(el.aiGroqMaxQuizInputCharacters, groq.maxQuizInputCharacters ?? 0);
    setValue(el.aiGroqRequestTimeoutSeconds, groq.requestTimeoutSeconds ?? 20);
    setValue(el.aiGroqMaxModelCandidates, groq.maxModelCandidates ?? 2);
    setValue(el.aiGroqMaxConcurrentRequests, groq.maxConcurrentRequests ?? 2);
    setValue(el.aiGroqQueueWaitTimeoutSeconds, groq.queueWaitTimeoutSeconds ?? 8);
    setValue(el.aiGroqMaxRetriesPerModel, groq.maxRetriesPerModel ?? 1);
    if (el.aiGroqEnableResponseCache) el.aiGroqEnableResponseCache.checked = Boolean(groq.enableResponseCache);
    setValue(el.aiGroqResponseCacheDays, normalizeCacheDays(groq.responseCacheDays, groq.responseCacheMinutes, 7));
    setValue(el.aiGroqFallbackModels, formatModelList(groq.fallbackModels));

    if (el.aiSystemMeta) {
      el.aiSystemMeta.textContent = data?.updatedAt
        ? `Cấu hình AI được cập nhật lần cuối lúc ${formatDateTime(data.updatedAt)}${data.updatedByUserId ? ` • Admin #${data.updatedByUserId}` : ""}.`
        : "Điều chỉnh provider, model, token budget và các tham số vận hành AI theo thời gian thực.";
    }
  };

  const loadAiSystemSettings = async (options = {}) => {
    if (!el.aiSystemForm) {
      return;
    }

    const data = await apiFetch("/api/admin/ai-settings");
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("aiSystem", data)) {
      return;
    }

    renderAiSystemSettings(data);
  };

  const buildAiSystemPayload = () => ({
    routing: {
      primaryTextProvider: toText(el.aiPrimaryTextProvider?.value || "gemini"),
      primaryVisionProvider: toText(el.aiPrimaryVisionProvider?.value || "gemini"),
      enforceDailyTokenBudget: Boolean(el.aiEnforceDailyTokenBudget?.checked),
      approxCharsPerToken: Number(el.aiApproxCharsPerToken?.value || 4),
      textOutputTokenBudget: Number(el.aiTextOutputTokenBudget?.value || 1500),
      quizOutputTokenBudget: Number(el.aiQuizOutputTokenBudget?.value || 2200),
      imageOutputTokenBudget: Number(el.aiImageOutputTokenBudget?.value || 900),
      geminiDailyTokenBudget: Number(el.aiGeminiDailyTokenBudget?.value || 0),
      groqDailyTokenBudget: Number(el.aiGroqDailyTokenBudget?.value || 0),
      minReservedTokensPerProvider: Number(el.aiMinReservedTokensPerProvider?.value || 0),
      enableProviderHealthSwitch: Boolean(el.aiEnableProviderHealthSwitch?.checked),
      slowRequestThresholdMs: Number(el.aiSlowRequestThresholdMs?.value || 9000),
      slowRequestStreakThreshold: Number(el.aiSlowRequestStreakThreshold?.value || 1),
      consecutiveFailureThreshold: Number(el.aiConsecutiveFailureThreshold?.value || 2),
      providerCooldownSeconds: Number(el.aiProviderCooldownSeconds?.value || 45),
      preferFastestHealthyProvider: Boolean(el.aiPreferFastestHealthyProvider?.checked),
      providerExecutionTimeoutMs: Number(el.aiProviderExecutionTimeoutMs?.value || 12000),
    },
    gemini: {
      apiKey: toText(el.aiGeminiApiKey?.value),
      clearApiKey: Boolean(el.aiGeminiClearApiKey?.checked),
      baseUrl: toText(el.aiGeminiBaseUrl?.value),
      textModel: toText(el.aiGeminiTextModel?.value),
      visionModel: toText(el.aiGeminiVisionModel?.value),
      maxInputCharacters: Number(el.aiGeminiMaxInputCharacters?.value || 0),
      maxQuizInputCharacters: Number(el.aiGeminiMaxQuizInputCharacters?.value || 0),
      requestTimeoutSeconds: Number(el.aiGeminiRequestTimeoutSeconds?.value || 20),
      maxModelCandidates: Number(el.aiGeminiMaxModelCandidates?.value || 2),
      maxRetriesPerModel: Number(el.aiGeminiMaxRetriesPerModel?.value || 1),
      fallbackModels: parseModelList(el.aiGeminiFallbackModels?.value),
    },
    groq: {
      apiKey: toText(el.aiGroqApiKey?.value),
      clearApiKey: Boolean(el.aiGroqClearApiKey?.checked),
      baseUrl: toText(el.aiGroqBaseUrl?.value),
      textModel: toText(el.aiGroqTextModel?.value),
      visionModel: toText(el.aiGroqVisionModel?.value),
      audioModel: toText(el.aiGroqAudioModel?.value),
      maxInputCharacters: Number(el.aiGroqMaxInputCharacters?.value || 0),
      maxQuizInputCharacters: Number(el.aiGroqMaxQuizInputCharacters?.value || 0),
      requestTimeoutSeconds: Number(el.aiGroqRequestTimeoutSeconds?.value || 20),
      maxModelCandidates: Number(el.aiGroqMaxModelCandidates?.value || 2),
      maxConcurrentRequests: Number(el.aiGroqMaxConcurrentRequests?.value || 2),
      queueWaitTimeoutSeconds: Number(el.aiGroqQueueWaitTimeoutSeconds?.value || 8),
      maxRetriesPerModel: Number(el.aiGroqMaxRetriesPerModel?.value || 1),
      enableResponseCache: Boolean(el.aiGroqEnableResponseCache?.checked),
      responseCacheDays: Number(el.aiGroqResponseCacheDays?.value || 7),
      fallbackModels: parseModelList(el.aiGroqFallbackModels?.value),
    },
  });

  const openAiLogDetails = (item) => {
    openDetailsDrawer({
      eyebrow: "Nhật ký AI",
      title: translateLabel(item.actionType, item.actionType || "Sự kiện AI"),
      meta: `${item.user ? `${item.user.username} • ${item.user.email}` : "Khách/Hệ thống"} • ${translateLabel(item.isError ? "Error" : "Success")} • ${formatDateTime(item.createdAt)}`,
      bodyHtml: buildAiLogReport(item),
    });
  };

  const renderAiReportInsights = (data) => {
    const summary = data?.summary || {};
    setText(el.aiReportTotal24h, formatNumber(summary.totalLogs24h));
    setText(el.aiReportErrors24h, formatNumber(summary.errors24h));
    setText(el.aiReportErrorRate24h, `${Number(summary.errorRate24h || 0).toFixed(1)}%`);
    setText(el.aiReportAvgLatency24h, `${formatNumber(summary.avgLatencyMs24h)} ms`);
    setText(el.aiReportP95Latency24h, `${formatNumber(summary.p95LatencyMs24h)} ms`);
    setText(el.aiReportMaxLatency24h, `${formatNumber(summary.maxLatencyMs24h)} ms`);

    const latencyTrend = Array.isArray(data?.latencyTrend) ? data.latencyTrend : [];
    const maxLatency = Math.max(1, ...latencyTrend.map((row) => Number(row?.avgLatencyMs || 0)));
    if (el.aiLatencyTrend) {
      el.aiLatencyTrend.innerHTML = latencyTrend.length === 0
        ? '<div class="empty-state">Chưa có dữ liệu độ trễ AI trong 7 ngày gần đây.</div>'
        : latencyTrend.map((row) => {
            const avgLatencyMs = Number(row?.avgLatencyMs || 0);
            const total = Number(row?.total || 0);
            const errors = Number(row?.errors || 0);
            const width = maxLatency === 0 ? 0 : (avgLatencyMs / maxLatency) * 100;
            return `
              <div class="trend-row">
                <span class="label">${escapeHtml(row?.day || "--")}</span>
                <div class="trend-stack" title="Độ trễ TB: ${formatNumber(avgLatencyMs)} ms | Log: ${formatNumber(total)} | Lỗi: ${formatNumber(errors)}">
                  <span class="bar-ai" style="width:${width}%;"></span>
                </div>
                <span class="value">${formatNumber(avgLatencyMs)} ms</span>
              </div>
              <div class="row-meta mb-2">Log ${formatNumber(total)} • Lỗi ${formatNumber(errors)}</div>
            `;
          }).join("");
    }

    const actionBreakdown = Array.isArray(data?.actionBreakdown) ? data.actionBreakdown : [];
    if (el.aiActionBreakdown) {
      el.aiActionBreakdown.innerHTML = actionBreakdown.length === 0
        ? '<div class="empty-state">Chưa có breakdown theo tác vụ AI.</div>'
        : actionBreakdown.map((item) => `
            <div class="contributor-item">
              <strong>${escapeHtml(translateLabel(item.actionType, item.actionType || "Tác vụ AI"))}</strong>
              <div class="row-meta">
                Tổng ${formatNumber(item.total)} • Lỗi ${formatNumber(item.errors)} • TB ${formatNumber(item.avgLatencyMs)} ms • Max ${formatNumber(item.maxLatencyMs)} ms
              </div>
            </div>
          `).join("");
    }

    const slowestItems = Array.isArray(data?.slowestItems) ? data.slowestItems : [];
    if (el.aiSlowestItems) {
      el.aiSlowestItems.innerHTML = slowestItems.length === 0
        ? '<div class="empty-state">Chưa có request AI chậm nổi bật.</div>'
        : slowestItems.map((item) => `
            <div class="activity-item">
              <strong>${escapeHtml(translateLabel(item.actionType, item.actionType || "Tác vụ AI"))}</strong>
              <div class="row-meta">
                ${escapeHtml(item.user || "Không xác định")} • ${formatNumber(item.latencyMs)} ms • ${item.isError ? "Có lỗi" : "Thành công"} • ${formatDateTime(item.createdAt)}
              </div>
            </div>
          `).join("");
    }
  };

  const renderAiLogs = (data) => {
    const items = Array.isArray(data?.items) ? data.items : [];
    state.aiLogItems = items;
    renderAiReportInsights(data);

    patchTableRows(el.aiLogsTableBody, items, {
      emptyHtml: '<tr><td colspan="6" class="text-center row-meta py-4">Không có nhật ký AI phù hợp.</td></tr>',
      getKey: (item) => item.logId || `${item.actionType}-${item.createdAt}`,
      renderRow: (item, index) => renderAiLogRow(item, index),
    });

    state.aiLogs.page = Number(data?.page || 1);
    state.aiLogs.totalPages = Number(data?.totalPages || 1);
    renderPager(el.aiLogsPager, state.aiLogs, data?.totalItems, "aiLogs");
  };

  const loadAiLogs = async (options = {}) => {
    if (!el.aiLogsTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.aiLogsTableBody, 6, "Đang tải nhật ký AI...");
    }

    const query = new URLSearchParams({
      page: String(state.aiLogs.page),
      pageSize: String(state.aiLogs.pageSize),
      errorsOnly: String(Boolean(el.errorsOnly.checked)),
    });

    const data = await apiFetch(`/api/admin/ai-logs?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("aiLogs", data)) {
      return;
    }

    renderAiLogs(data);
  };

  const openAuditDetails = (item) => {
    openDetailsDrawer({
      eyebrow: "Nhật ký kiểm toán",
      title: `${translateLabel(item.actionType, item.actionType || "Hành động")} • ${translateLabel(item.targetType, item.targetType || "Đối tượng")} ${item.targetId ? `#${item.targetId}` : ""}`,
      meta: `${item.admin ? `${item.admin.username} • ${item.admin.email}` : "Quản trị viên không xác định"} • IP ${item.ipAddress || "--"} • ${formatDateTime(item.createdAt)}`,
      bodyHtml: buildAuditLogReport(item),
    });
  };

  const renderAuditLogs = (data) => {
    const items = Array.isArray(data?.items) ? data.items : [];
    state.auditItems = items;

    patchTableRows(el.auditTableBody, items, {
      emptyHtml: '<tr><td colspan="6" class="text-center row-meta py-4">Chưa có nhật ký kiểm toán nào.</td></tr>',
      getKey: (item) => item.auditLogId || `${item.actionType}-${item.targetType}-${item.targetId}-${item.createdAt}`,
      renderRow: (item, index) => renderAuditRow(item, index),
    });

    state.audit.page = Number(data?.page || 1);
    state.audit.totalPages = Number(data?.totalPages || 1);
    renderPager(el.auditPager, state.audit, data?.totalItems, "audit");
  };

  const loadAuditLogs = async (options = {}) => {
    if (!el.auditTableBody) {
      return;
    }

    if (!options.silent) {
      setTableLoading(el.auditTableBody, 6, "Đang tải nhật ký kiểm toán...");
    }

    const query = new URLSearchParams({
      page: String(state.audit.page),
      pageSize: String(state.audit.pageSize),
    });

    const data = await apiFetch(`/api/admin/audit-logs?${query.toString()}`);
    if (!data) {
      return;
    }

    if (options.silent && !hasRenderChanged("audit", data)) {
      return;
    }

    renderAuditLogs(data);
  };

  const setActiveTab = (tabName) => {
    state.activeTab = tabName;
    el.tabs.forEach((tab) => {
      const active = tab.getAttribute("data-tab") === tabName;
      tab.classList.toggle("active", active);
      tab.setAttribute("aria-selected", active ? "true" : "false");
    });

    el.panels.forEach((panel) => {
      const active = panel.getAttribute("data-panel") === tabName;
      panel.classList.toggle("active", active);
      panel.hidden = !active;
    });
  };

  const validAdminTabs = new Set(["users", "moderation", "aiSystem", "aiLogs", "audit"]);
  const resolveTabFromHash = () => {
    const candidate = String(window.location.hash || "").replace(/^#/, "").trim();
    return validAdminTabs.has(candidate) ? candidate : "users";
  };

  const syncHashToActiveTab = () => {
    const nextHash = `#${state.activeTab}`;
    if (window.location.hash !== nextHash) {
      window.history.replaceState(null, "", nextHash);
    }
  };

  const loadActiveTabData = async (options = {}) => {
    if (state.activeTab === "users") {
      await loadUsers(options);
      return;
    }

    if (state.activeTab === "moderation") {
      await loadContents(options);
      return;
    }

     if (state.activeTab === "aiSystem") {
      await loadAiSystemSettings(options);
      return;
    }

    if (state.activeTab === "aiLogs") {
      await loadAiLogs(options);
      return;
    }

    await loadAuditLogs(options);
  };

  const loadCurrentPageData = async (options = {}) => {
    if (pageType === "users") {
      await loadUsers(options);
      return;
    }

    if (pageType === "premium") {
      await loadPremiumPage(options);
      return;
    }

    if (pageType === "content") {
      await loadContents(options);
      return;
    }

    if (pageType === "reports") {
      await loadAiLogs(options);
      return;
    }

    if (pageType === "ai-system") {
      await loadAiSystemSettings(options);
      return;
    }

    if (pageType === "notifications") {
      await loadNotificationHistory(options);
      return;
    }

    if (pageType === "settings") {
      await Promise.all([loadAdminAccounts(options), loadAuditLogs(options)]);
      return;
    }

    if (pageType === "profile") {
      await loadAdminProfile(options);
      return;
    }

    await Promise.all([loadOverview(options), loadActiveTabData(options)]);
  };

  const handleCreateAdminSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.adminCreateFeedback, "", "");
    setButtonBusy(el.adminCreateSubmit, true, "Đang tạo...");

    try {
      const payload = await apiFetch("/api/admin/admin-users", {
        method: "POST",
        body: JSON.stringify({
          username: toText(el.adminUsername.value),
          fullName: toText(el.adminFullName.value),
          email: toText(el.adminEmail.value),
          password: el.adminPassword.value || "",
          confirmPassword: el.adminConfirmPassword.value || "",
        }),
      });

      if (!payload) {
        return;
      }

      el.adminCreateForm.reset();
      setInlineFeedback(el.adminCreateFeedback, payload?.message || "Đã tạo quản trị viên mới.", "success");
      showToast(payload?.message || "Đã tạo quản trị viên mới.");
      setAdminCreateModalVisible(false);
      await Promise.all([loadAdminAccounts(), loadOverview(), loadUsers()]);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể tạo tài khoản quản trị.";
      setInlineFeedback(el.adminCreateFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.adminCreateSubmit, false);
    }
  };

  const handleAdminNotificationSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.adminNotificationFeedback, "", "");
    setButtonBusy(el.adminNotificationSubmit, true, "Đang gửi...");

    try {
      const payload = await apiFetch("/api/admin/notifications", {
        method: "POST",
        body: JSON.stringify({
          targetScope: toText(el.adminNotificationTargetScope?.value || "all-users"),
          userEmail: el.adminNotificationTargetScope?.value === "user"
            ? toText(el.adminNotificationTargetEmail?.value)
            : "",
          title: toText(el.adminNotificationTitle?.value),
          message: toText(el.adminNotificationMessage?.value),
          category: toText(el.adminNotificationCategory?.value || "announcement"),
          severity: toText(el.adminNotificationSeverity?.value || "info"),
          actionUrl: toText(el.adminNotificationActionUrl?.value),
          sendEmail: Boolean(el.adminNotificationSendEmail?.checked),
        }),
      });

      if (!payload) {
        return;
      }

      el.adminNotificationForm?.reset();
      if (el.adminNotificationTargetScope) {
        el.adminNotificationTargetScope.value = "all-users";
      }
      if (el.adminNotificationCategory) {
        el.adminNotificationCategory.value = "announcement";
      }
      if (el.adminNotificationSeverity) {
        el.adminNotificationSeverity.value = "info";
      }
      if (el.adminNotificationSendEmail) {
        el.adminNotificationSendEmail.checked = true;
      }
      if (el.adminNotificationTargetEmail) {
        el.adminNotificationTargetEmail.disabled = true;
      }

      setInlineFeedback(el.adminNotificationFeedback, payload.message || "Đã gửi thông báo hệ thống.", "success");
      showToast(payload.message || "Đã gửi thông báo hệ thống.");
      state.notifications.page = 1;
      await loadNotificationHistory();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể gửi thông báo hệ thống.";
      setInlineFeedback(el.adminNotificationFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.adminNotificationSubmit, false);
    }
  };

  const handleAiSystemSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.aiSystemFeedback, "", "");
    setButtonBusy(el.aiSystemSubmit, true, "Đang lưu...");

    try {
      const payload = await apiFetch("/api/admin/ai-settings", {
        method: "PUT",
        body: JSON.stringify(buildAiSystemPayload()),
      });

      if (!payload?.settings) {
        return;
      }

      renderAiSystemSettings(payload.settings);
      setInlineFeedback(el.aiSystemFeedback, payload.message || "Đã cập nhật cấu hình AI.", "success");
      showToast(payload.message || "Đã cập nhật cấu hình AI.");
      await Promise.all([loadOverview({ silent: true }), loadAiLogs({ silent: true })]);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể cập nhật cấu hình AI.";
      setInlineFeedback(el.aiSystemFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.aiSystemSubmit, false);
    }
  };

  const handlePremiumSettingsSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.premiumSettingsFeedback, "", "");
    setButtonBusy(el.premiumSettingsSubmit, true, "Đang lưu...");

    try {
      const payload = await apiFetch("/api/admin/premium/settings", {
        method: "PUT",
        body: JSON.stringify({
          amount: Number(el.premiumAmount?.value || 0),
          days: Number(el.premiumDays?.value || 30),
        }),
      });

      if (!payload) {
        return;
      }

      renderPremiumOverview({ ...(state.premiumOverview || {}), settings: payload.settings });
      setInlineFeedback(el.premiumSettingsFeedback, payload.message || "Đã cập nhật cấu hình Premium.", "success");
      showToast(payload.message || "Đã cập nhật cấu hình Premium.");
      await loadPremiumOverview({ silent: true });
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể cập nhật cấu hình Premium.";
      setInlineFeedback(el.premiumSettingsFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.premiumSettingsSubmit, false);
    }
  };

  const openPremiumExtendModal = (item) => {
    if (!item) {
      return;
    }

    setValue(el.premiumExtendUserId, item.userId);
    setValue(el.premiumExtendDays, state.premiumOverview?.settings?.days || 30);
    setValue(el.premiumExtendReason, "");
    setText(el.premiumExtendUserLabel, `Gia hạn Premium cho ${item.fullName || item.username} (${item.email}).`);
    setInlineFeedback(el.premiumExtendFeedback, "", "");
    premiumExtendModal?.show();
    window.setTimeout(() => el.premiumExtendDays?.focus(), 180);
  };

  const handlePremiumExtendSubmit = async (event) => {
    event.preventDefault();
    const userId = Number(el.premiumExtendUserId?.value || 0);
    if (!userId) {
      return;
    }

    setInlineFeedback(el.premiumExtendFeedback, "", "");
    setButtonBusy(el.premiumExtendSubmit, true, "Đang gia hạn...");

    try {
      const payload = await apiFetch(`/api/admin/premium/users/${userId}/extend`, {
        method: "POST",
        body: JSON.stringify({
          days: Number(el.premiumExtendDays?.value || 30),
          reason: toText(el.premiumExtendReason?.value),
        }),
      });

      if (!payload) {
        return;
      }

      premiumExtendModal?.hide();
      showToast(payload.message || "Đã gia hạn Premium.");
      await Promise.all([loadPremiumUsers(), loadPremiumOverview(), loadUsers({ silent: true })]);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể gia hạn Premium.";
      setInlineFeedback(el.premiumExtendFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.premiumExtendSubmit, false);
    }
  };

  const handleAdminActionSubmit = async (event) => {
    event.preventDefault();
    const context = state.actionContext;
    if (!context) {
      return;
    }

    setInlineFeedback(el.adminActionFeedback, "", "");
    setButtonBusy(el.adminActionSubmit, true, "Đang xử lý...");

    try {
      const reason = toText(el.adminActionReason.value);
      if (context.kind === "lock-user" || context.kind === "unlock-user") {
        await apiFetch(`/api/admin/users/${context.targetId}/lock`, {
          method: "PUT",
          body: JSON.stringify({
            isLocked: context.kind === "lock-user",
            reason,
          }),
        });
        await Promise.all([loadUsers(), loadOverview()]);
      } else if (context.kind === "delete-user") {
        await apiFetch(`/api/admin/users/${context.targetId}`, {
          method: "DELETE",
          body: JSON.stringify({ reason }),
        });
        await Promise.all([loadUsers(), loadOverview(), loadAdminAccounts()]);
      } else if (context.kind === "approve-content" || context.kind === "reject-content") {
        await apiFetch(`/api/admin/contents/${context.targetId}/moderation`, {
          method: "PUT",
          body: JSON.stringify({
            status: context.kind === "approve-content" ? "Approved" : "Rejected",
            reason,
          }),
        });
        await Promise.all([loadContents(), loadOverview()]);
      } else if (context.kind === "cancel-premium") {
        await apiFetch(`/api/admin/premium/users/${context.targetId}/cancel`, {
          method: "POST",
          body: JSON.stringify({ reason }),
        });
        await Promise.all([loadPremiumUsers(), loadPremiumOverview(), loadUsers({ silent: true })]);
      } else if (context.kind === "approve-premium-transaction") {
        await apiFetch(`/api/admin/premium/transactions/${context.targetId}/approve`, {
          method: "POST",
          body: JSON.stringify({ reason }),
        });
        await Promise.all([
          loadPremiumTransactions(),
          loadPremiumUsers(),
          loadPremiumOverview(),
          loadUsers({ silent: true }),
        ]);
      }

      showToast("Đã lưu thay đổi.");
      actionModal?.hide();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể cập nhật thao tác quản trị.";
      setInlineFeedback(el.adminActionFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.adminActionSubmit, false);
    }
  };

  const handleUserEditorSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.userEditorFeedback, "", "");
    setButtonBusy(el.userEditorSubmit, true, "Đang lưu...");

    try {
      const mode = toText(el.userEditorMode?.value || "edit");
      const payload = {
        username: toText(el.userEditorUsername.value),
        fullName: toText(el.userEditorFullName.value),
        email: toText(el.userEditorEmail.value),
        role: toText(el.userEditorRole.value || "User"),
        isLocked: Boolean(el.userEditorLocked.checked),
        isEmailVerified: Boolean(el.userEditorVerified.checked),
      };

      if (mode === "create") {
        await apiFetch("/api/admin/users", {
          method: "POST",
          body: JSON.stringify({
            ...payload,
            password: el.userEditorPassword.value || "",
            confirmPassword: el.userEditorConfirmPassword.value || "",
          }),
        });
      } else {
        const userId = Number(el.userEditorId.value || 0);
        await apiFetch(`/api/admin/users/${userId}`, {
          method: "PUT",
          body: JSON.stringify(payload),
        });
      }

      userEditorModal?.hide();
      showToast(mode === "create" ? "Đã tạo người dùng mới." : "Đã cập nhật tài khoản người dùng.");
      await Promise.all([loadUsers(), loadOverview(), loadAdminAccounts()]);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể cập nhật tài khoản.";
      setInlineFeedback(el.userEditorFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.userEditorSubmit, false);
    }
  };

  const handleAdminProfileSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.adminProfileInfoFeedback, "", "");
    setButtonBusy(el.adminProfileSubmit, true, "Đang lưu...");

    try {
      const payload = await apiFetch("/api/admin/profile", {
        method: "PUT",
        body: JSON.stringify({
          fullName: toText(el.adminProfileFullName.value),
          email: toText(el.adminProfileEmail.value),
          phone: toText(el.adminProfilePhone.value),
          bio: toText(el.adminProfileBio.value),
        }),
      });

      if (!payload) {
        return;
      }

      renderAdminProfile(payload);
      setInlineFeedback(el.adminProfileInfoFeedback, payload.message || "Đã lưu hồ sơ quản trị.", "success");
      showToast(payload.message || "Đã lưu hồ sơ quản trị.");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể cập nhật hồ sơ quản trị.";
      setInlineFeedback(el.adminProfileInfoFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.adminProfileSubmit, false);
    }
  };

  const handleAdminPasswordSubmit = async (event) => {
    event.preventDefault();
    setInlineFeedback(el.adminPasswordFeedback, "", "");

    const currentPassword = el.adminCurrentPassword?.value || "";
    const newPassword = el.adminNewPassword?.value || "";
    const confirmPassword = el.adminConfirmPassword?.value || "";

    if (newPassword !== confirmPassword) {
      setInlineFeedback(el.adminPasswordFeedback, "Xác nhận mật khẩu không khớp.", "error");
      return;
    }

    setButtonBusy(el.adminPasswordSubmit, true, "Đang cập nhật...");

    try {
      const payload = await apiFetch("/api/admin/profile/password", {
        method: "PUT",
        body: JSON.stringify({
          currentPassword,
          newPassword,
        }),
      });

      if (!payload) {
        return;
      }

      el.adminPasswordForm?.reset();
      passwordModal?.hide();
      setInlineFeedback(el.adminPasswordFeedback, payload.message || "Đã đổi mật khẩu.", "success");
      showToast(payload.message || "Đã đổi mật khẩu.");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể đổi mật khẩu quản trị.";
      setInlineFeedback(el.adminPasswordFeedback, message, "error");
      showToast(message, "error");
    } finally {
      setButtonBusy(el.adminPasswordSubmit, false);
    }
  };

  const refreshLiveData = async () => {
    if (shouldPauseLiveRefresh()) {
      return;
    }

    await loadAdminAlerts({ silent: true });
    if (pageType === "profile") {
      return;
    }

    await loadCurrentPageData({ silent: true });
  };

  const startAutoRefresh = () => {
    if (state.autoRefreshTimer) {
      window.clearInterval(state.autoRefreshTimer);
    }

    state.autoRefreshTimer = window.setInterval(() => {
      if (document.visibilityState !== "visible") {
        return;
      }

      void refreshLiveData().catch(() => {});
    }, 45000);
  };

  const stopAutoRefresh = () => {
    if (!state.autoRefreshTimer) {
      return;
    }

    window.clearInterval(state.autoRefreshTimer);
    state.autoRefreshTimer = 0;
  };

  const disposePage = () => {
    stopAutoRefresh();
    cleanupTransientAdminUi();

    if (adminKeydownHandler) {
      document.removeEventListener("keydown", adminKeydownHandler);
      adminKeydownHandler = null;
    }

    if (adminVisibilityHandler) {
      document.removeEventListener("visibilitychange", adminVisibilityHandler);
      adminVisibilityHandler = null;
    }

    if (adminDocumentClickHandler) {
      document.removeEventListener("click", adminDocumentClickHandler);
      adminDocumentClickHandler = null;
    }

    if (adminInteractionHandler) {
      document.removeEventListener("pointerdown", adminInteractionHandler, true);
      document.removeEventListener("focusin", adminInteractionHandler, true);
      adminInteractionHandler = null;
    }

    if (adminHashChangeHandler) {
      window.removeEventListener("hashchange", adminHashChangeHandler);
      adminHashChangeHandler = null;
    }
  };

  const wireEvents = () => {
    cleanupTransientAdminUi();

    const portalMain = document.querySelector(".page-admin .portal-main");

    if (adminInteractionHandler) {
      document.removeEventListener("pointerdown", adminInteractionHandler, true);
      document.removeEventListener("focusin", adminInteractionHandler, true);
    }

    adminInteractionHandler = (event) => {
      if (!(event.target instanceof Element)) {
        return;
      }

      if (!portalMain?.contains(event.target)) {
        return;
      }

      markAdminInteraction();
    };

    document.addEventListener("pointerdown", adminInteractionHandler, true);
    document.addEventListener("focusin", adminInteractionHandler, true);

    if (adminDocumentClickHandler) {
      document.removeEventListener("click", adminDocumentClickHandler);
    }

    adminDocumentClickHandler = async (event) => {
      const target = event.target instanceof Element ? event.target : null;
      if (!target) {
        return;
      }

      if (el.adminAlertsFlyout &&
          el.adminAlertsToggle &&
          !target.closest("#adminAlertsFlyout") &&
          !target.closest("#adminAlertsToggle")) {
        closeAdminAlertsFlyout();
      }

      const refreshAllButton = target.closest("#refreshAllBtn");
      if (refreshAllButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        try {
          await Promise.all([
            loadCurrentPageData(),
            loadAdminAlerts(),
          ]);
          showToast("Đã cập nhật dữ liệu trang.");
        } catch (error) {
          showToast(error instanceof Error ? error.message : "Không thể làm mới dữ liệu.", "error");
        }
        return;
      }

      const alertsToggle = target.closest("#adminAlertsToggle");
      if (alertsToggle) {
        event.preventDefault();
        event.stopPropagation();
        markAdminInteraction(2500);

        const willOpen = el.adminAlertsFlyout?.hidden !== false;
        if (!willOpen) {
          closeAdminAlertsFlyout();
          return;
        }

        await loadAdminAlerts();
        if (el.adminAlertsFlyout) {
          el.adminAlertsFlyout.hidden = false;
        }
        el.adminAlertsToggle?.setAttribute("aria-expanded", "true");
        markAdminAlertsSeen();
        renderAdminAlerts(state.adminAlerts);
        return;
      }

      const overviewButton = target.closest("#openOverviewWindowModal");
      if (overviewButton) {
        event.preventDefault();
        openOverviewWindowPicker();
        return;
      }

      const userActionButton = target.closest("[data-action][data-user-id]");
      if (userActionButton) {
        event.preventDefault();
        const userId = Number(userActionButton.getAttribute("data-user-id"));
        const userName = userActionButton.getAttribute("data-user-name") || "người dùng";
        const action = userActionButton.getAttribute("data-action") || "";
        const item = state.userItems.find((entry) => Number(entry?.userId || 0) === userId);

        if (action === "edit") {
          if (item) {
            openUserEditor(item);
          }
          return;
        }

        if (action === "lock" || action === "unlock") {
          const isLock = action === "lock";
          openActionModal({
            kind: isLock ? "lock-user" : "unlock-user",
            targetId: userId,
            eyebrow: "Truy cập người dùng",
            title: isLock ? "Khóa tài khoản người dùng" : "Mở khóa tài khoản người dùng",
            description: `${isLock ? "Khóa" : "Mở khóa"} tài khoản ${userName}. Ghi chú này sẽ được lưu vào nhật ký quản trị.`,
            reasonLabel: isLock ? "Lý do khóa" : "Ghi chú mở khóa",
            reasonPlaceholder: isLock ? "Nhập lý do khóa tài khoản" : "Nhập ghi chú mở khóa tài khoản",
            submitText: isLock ? "Khóa tài khoản" : "Mở khóa",
          });
          return;
        }

        if (action === "delete") {
          openActionModal({
            kind: "delete-user",
            targetId: userId,
            eyebrow: "Vòng đời tài khoản",
            title: "Xóa tài khoản người dùng",
            description: `Xóa tài khoản ${userName}. Hành động này sẽ được ghi vào nhật ký quản trị.`,
            reasonLabel: "Lý do xóa",
            reasonPlaceholder: "Nhập lý do xóa tài khoản",
            submitText: "Xóa tài khoản",
          });
        }
        return;
      }

      const premiumActionButton = target.closest("[data-premium-action][data-user-id]");
      if (premiumActionButton) {
        event.preventDefault();
        if (premiumActionButton.hasAttribute("disabled")) {
          return;
        }

        const userId = Number(premiumActionButton.getAttribute("data-user-id"));
        const userName = premiumActionButton.getAttribute("data-user-name") || "người dùng";
        const action = premiumActionButton.getAttribute("data-premium-action") || "";
        const item = state.premiumUserItems.find((entry) => Number(entry?.userId || 0) === userId);

        if (action === "extend") {
          openPremiumExtendModal(item);
          return;
        }

        if (action === "cancel") {
          openActionModal({
            kind: "cancel-premium",
            targetId: userId,
            eyebrow: "Premium thủ công",
            title: "Hủy Premium",
            description: `Hủy Premium của ${userName}. User sẽ mất quyền Premium ngay sau khi xác nhận.`,
            reasonLabel: "Lý do hủy",
            reasonPlaceholder: "Nhập lý do hủy Premium thủ công",
            submitText: "Hủy Premium",
          });
        }
        return;
      }

      const premiumTransactionActionButton = target.closest("[data-premium-transaction-action][data-transaction-id]");
      if (premiumTransactionActionButton) {
        event.preventDefault();
        const transactionId = Number(premiumTransactionActionButton.getAttribute("data-transaction-id"));
        const orderId = premiumTransactionActionButton.getAttribute("data-transaction-order") || `#${transactionId}`;
        const userName = premiumTransactionActionButton.getAttribute("data-transaction-user") || "người dùng";
        const action = premiumTransactionActionButton.getAttribute("data-premium-transaction-action") || "";

        if (action === "approve") {
          openActionModal({
            kind: "approve-premium-transaction",
            targetId: transactionId,
            eyebrow: "Duyệt thanh toán",
            title: "Duyệt giao dịch Premium",
            description: `Duyệt giao dịch ${orderId} cho ${userName}. Hệ thống sẽ chuyển giao dịch sang Paid và kích hoạt/gia hạn Premium.`,
            reasonLabel: "Ghi chú duyệt",
            reasonPlaceholder: "Nhập ghi chú duyệt thủ công",
            submitText: "Duyệt giao dịch",
          });
        }
        return;
      }

      const contentActionButton = target.closest("[data-action][data-content-id]");
      if (contentActionButton) {
        event.preventDefault();
        const contentId = Number(contentActionButton.getAttribute("data-content-id"));
        const contentName = contentActionButton.getAttribute("data-content-name") || `Nội dung #${contentId}`;
        const approve = contentActionButton.getAttribute("data-action") === "approve";
        const isPolicyViolation = contentActionButton.getAttribute("data-policy-violation") === "true";

        openActionModal({
          kind: approve ? "approve-content" : "reject-content",
          targetId: contentId,
          eyebrow: "Kiểm duyệt",
          title: approve ? "Duyệt nội dung" : "Từ chối nội dung",
          description: isPolicyViolation
            ? `${approve ? "Duyệt" : "Từ chối"} nội dung ${contentName}. Hệ thống đã gắn cờ nội dung này là có dấu hiệu vi phạm chính sách và mọi quyết định sẽ được ghi vào nhật ký quản trị.`
            : `${approve ? "Duyệt" : "Từ chối"} nội dung ${contentName}. Thông tin này sẽ được ghi lại trong nhật ký quản trị.`,
          reasonLabel: approve ? "Ghi chú duyệt" : "Lý do từ chối",
          reasonPlaceholder: approve
            ? (isPolicyViolation ? "Nhập ghi chú duyệt để cho phép hệ thống phân tích nội dung này" : "Nhập ghi chú duyệt (tuỳ chọn)")
            : (isPolicyViolation ? "Nhập lý do từ chối do vi phạm chính sách hệ thống" : "Nhập lý do từ chối nội dung"),
          submitText: approve ? "Duyệt nội dung" : "Từ chối nội dung",
        });
        return;
      }

      const aiLogButton = target.closest("[data-ai-log-index]");
      if (aiLogButton) {
        event.preventDefault();
        const item = state.aiLogItems[Number(aiLogButton.getAttribute("data-ai-log-index"))];
        if (item) {
          openAiLogDetails(item);
        }
        return;
      }

      const auditButton = target.closest("[data-audit-index]");
      if (auditButton) {
        event.preventDefault();
        const item = state.auditItems[Number(auditButton.getAttribute("data-audit-index"))];
        if (item) {
          openAuditDetails(item);
        }
        return;
      }

      const tabButton = target.closest(".workspace-tab");
      if (tabButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        const tabName = tabButton.getAttribute("data-tab") || "users";
        setActiveTab(tabName);
        syncHashToActiveTab();
        await loadActiveTabData();
        return;
      }

      const aiSystemResetButton = target.closest("#aiSystemReset");
      if (aiSystemResetButton) {
        event.preventDefault();
        if (state.aiSystem) {
          renderAiSystemSettings(state.aiSystem);
          setInlineFeedback(el.aiSystemFeedback, "", "");
        }
        return;
      }

      const applyUserFilterButton = target.closest("#applyUserFilter");
      if (applyUserFilterButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        state.users.page = 1;
        await loadUsers();
        return;
      }

      const applyPremiumTransactionFilterButton = target.closest("#applyPremiumTransactionFilter");
      if (applyPremiumTransactionFilterButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        state.premiumTransactions.page = 1;
        await loadPremiumTransactions();
        return;
      }

      const applyPremiumUserFilterButton = target.closest("#applyPremiumUserFilter");
      if (applyPremiumUserFilterButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        state.premiumUsers.page = 1;
        await loadPremiumUsers();
        return;
      }

      const openUserCreateButton = target.closest("#openUserCreateModal");
      if (openUserCreateButton) {
        event.preventDefault();
        openCreateUserModal();
        return;
      }

      const openAdminPasswordButton = target.closest("#openAdminPasswordModal");
      if (openAdminPasswordButton) {
        event.preventDefault();
        passwordModal?.show();
        window.setTimeout(() => {
          el.adminCurrentPassword?.focus();
        }, 180);
        return;
      }

      const openAdminCreateButton = target.closest("#openAdminCreatePanel");
      if (openAdminCreateButton) {
        event.preventDefault();
        setAdminCreateModalVisible(true);
        return;
      }

      const applyContentFilterButton = target.closest("#applyContentFilter");
      if (applyContentFilterButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        state.contents.page = 1;
        await loadContents();
        return;
      }

      const refreshAiLogsButton = target.closest("#refreshAiLogs");
      if (refreshAiLogsButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        await loadAiLogs();
        showToast("Đã làm mới nhật ký AI.");
        return;
      }

      const refreshAuditLogsButton = target.closest("#refreshAuditLogs");
      if (refreshAuditLogsButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        await loadAuditLogs();
        showToast("Đã làm mới nhật ký kiểm toán.");
        return;
      }

      const refreshNotificationHistoryButton = target.closest("#refreshNotificationHistory");
      if (refreshNotificationHistoryButton) {
        event.preventDefault();
        markAdminInteraction(5000);
        await loadNotificationHistory();
        showToast("Đã làm mới lịch sử thông báo.");
        return;
      }

      const adminLogoutButton = target.closest("#adminPortalLogout");
      if (adminLogoutButton) {
        event.preventDefault();
        await window.AuthClient?.logout?.();
        window.location.replace("/home/login.html");
        return;
      }

      const drawerCloseButton = target.closest("#closeDetailsDrawer, [data-drawer-close]");
      if (drawerCloseButton) {
        event.preventDefault();
        closeDetailsDrawer();
        return;
      }

      const pagerButton = target.closest("[data-pager-key][data-page-action]");
      if (!pagerButton) {
        return;
      }

      event.preventDefault();
      markAdminInteraction(5000);

      const pagerKey = pagerButton.getAttribute("data-pager-key") || "";
      const pageAction = pagerButton.getAttribute("data-page-action") || "";
      if (!pageAction || pagerButton.hasAttribute("disabled")) {
        return;
      }

      if (pagerKey === "users") {
        state.users.page = clamp(state.users.page + (pageAction === "next" ? 1 : -1), 1, state.users.totalPages);
        await loadUsers();
        return;
      }

      if (pagerKey === "premiumTransactions") {
        state.premiumTransactions.page = clamp(state.premiumTransactions.page + (pageAction === "next" ? 1 : -1), 1, state.premiumTransactions.totalPages);
        await loadPremiumTransactions();
        return;
      }

      if (pagerKey === "premiumUsers") {
        state.premiumUsers.page = clamp(state.premiumUsers.page + (pageAction === "next" ? 1 : -1), 1, state.premiumUsers.totalPages);
        await loadPremiumUsers();
        return;
      }

      if (pagerKey === "contents") {
        state.contents.page = clamp(state.contents.page + (pageAction === "next" ? 1 : -1), 1, state.contents.totalPages);
        await loadContents();
        return;
      }

      if (pagerKey === "aiLogs") {
        state.aiLogs.page = clamp(state.aiLogs.page + (pageAction === "next" ? 1 : -1), 1, state.aiLogs.totalPages);
        await loadAiLogs();
        return;
      }

      if (pagerKey === "audit") {
        state.audit.page = clamp(state.audit.page + (pageAction === "next" ? 1 : -1), 1, state.audit.totalPages);
        await loadAuditLogs();
        return;
      }

      if (pagerKey === "notifications") {
        state.notifications.page = clamp(state.notifications.page + (pageAction === "next" ? 1 : -1), 1, state.notifications.totalPages);
        await loadNotificationHistory();
      }
    };

    document.addEventListener("click", adminDocumentClickHandler);
    el.overviewWindowPreset?.addEventListener("change", () => {
      toggleOverviewCustomDateInputs();
      setInlineFeedback(el.overviewWindowFeedback, "", "");
    });
    el.overviewStartDate?.addEventListener("blur", () => {
      normalizeOverviewDateInput(el.overviewStartDate);
    });
    el.overviewEndDate?.addEventListener("blur", () => {
      normalizeOverviewDateInput(el.overviewEndDate);
    });
    el.overviewWindowForm?.addEventListener("submit", handleOverviewWindowSubmit);
    el.overviewWindowModal?.addEventListener("hidden.bs.modal", () => {
      setInlineFeedback(el.overviewWindowFeedback, "", "");
    });

    el.adminCreateForm?.addEventListener("submit", handleCreateAdminSubmit);
    el.adminNotificationForm?.addEventListener("submit", handleAdminNotificationSubmit);
    const syncNotificationScopeUi = () => {
      if (!el.adminNotificationTargetEmail) {
        return;
      }

      const isSingleUser = el.adminNotificationTargetScope?.value === "user";
      el.adminNotificationTargetEmail.disabled = !isSingleUser;
      if (!isSingleUser) {
        el.adminNotificationTargetEmail.value = "";
      }
    };
    el.adminNotificationTargetScope?.addEventListener("change", syncNotificationScopeUi);
    syncNotificationScopeUi();
    el.aiSystemForm?.addEventListener("submit", handleAiSystemSubmit);
    el.premiumSettingsForm?.addEventListener("submit", handlePremiumSettingsSubmit);
    el.premiumExtendForm?.addEventListener("submit", handlePremiumExtendSubmit);
    el.adminActionForm?.addEventListener("submit", handleAdminActionSubmit);
    el.userEditorForm?.addEventListener("submit", handleUserEditorSubmit);
    el.adminProfileForm?.addEventListener("submit", handleAdminProfileSubmit);
    el.adminPasswordForm?.addEventListener("submit", handleAdminPasswordSubmit);
    [
      el.adminActionModal,
      el.userEditorModal,
      el.adminCreateModal,
      el.adminPasswordModal,
      el.premiumExtendModal,
      el.overviewWindowModal,
    ].forEach((modalNode) => {
      modalNode?.addEventListener("hidden.bs.modal", cleanupTransientAdminUi);
    });

    el.adminProfileReset?.addEventListener("click", () => {
      if (!state.adminProfile) {
        return;
      }

      setValue(el.adminProfileFullName, state.adminProfile.fullName);
      setValue(el.adminProfileEmail, state.adminProfile.email);
      setValue(el.adminProfilePhone, state.adminProfile.phone);
      setValue(el.adminProfileBio, state.adminProfile.bio);
      setInlineFeedback(el.adminProfileInfoFeedback, "", "");
    });

    el.adminCreateModal?.addEventListener("hidden.bs.modal", () => {
      el.openAdminCreatePanel?.setAttribute("aria-expanded", "false");
      el.adminCreateForm?.reset();
      setInlineFeedback(el.adminCreateFeedback, "", "");
    });

    el.adminPasswordModal?.addEventListener("hidden.bs.modal", () => {
      el.adminPasswordForm?.reset();
      setInlineFeedback(el.adminPasswordFeedback, "", "");
    });

    el.premiumExtendModal?.addEventListener("hidden.bs.modal", () => {
      el.premiumExtendForm?.reset();
      setInlineFeedback(el.premiumExtendFeedback, "", "");
    });

    el.errorsOnly?.addEventListener("change", async () => {
      markAdminInteraction(5000);
      state.aiLogs.page = 1;
      await loadAiLogs();
    });

    if (adminKeydownHandler) {
      document.removeEventListener("keydown", adminKeydownHandler);
    }

    adminKeydownHandler = (event) => {
      if (event.key === "Escape") {
        closeAdminAlertsFlyout();
        closeDetailsDrawer();
      }
    };

    document.addEventListener("keydown", adminKeydownHandler);

    if (adminVisibilityHandler) {
      document.removeEventListener("visibilitychange", adminVisibilityHandler);
    }

    adminVisibilityHandler = () => {
      if (document.visibilityState === "visible") {
        void refreshLiveData().catch(() => {});
      }
    };

    document.addEventListener("visibilitychange", adminVisibilityHandler);
    if (adminHashChangeHandler) {
      window.removeEventListener("hashchange", adminHashChangeHandler);
    }

    adminHashChangeHandler = async () => {
      const nextTab = resolveTabFromHash();
      if (nextTab === state.activeTab) {
        return;
      }

      setActiveTab(nextTab);
      await loadActiveTabData();
    };

    window.addEventListener("hashchange", adminHashChangeHandler);
  };

  const init = async () => {
    try {
      cleanupTransientAdminUi();

      if (window.AuthClient?.whenReady) {
        try {
          await window.AuthClient.whenReady();
        } catch {
          // AuthClient already handles redirects and session cleanup.
        }
      }

      const me = await syncSessionUi();
      if (!me) {
        return;
      }

      setActiveTab(resolveTabFromHash());
      syncHashToActiveTab();
      wireEvents();
      updateDashboardWindowControls(state.dashboardWindow);
      startAutoRefresh();
      await loadAdminAlerts();
      await loadCurrentPageData();
    } catch (error) {
      showToast(error instanceof Error ? error.message : "Không thể tải dữ liệu quản trị.", "error");
    }
  };

  document.addEventListener("ajax:before-swap", disposePage, { once: true });
  init();
})();
