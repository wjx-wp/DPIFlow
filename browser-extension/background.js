const AUTO_DPI_RATIO = 1.25;
const AUTO_HIGH_DPI_ZOOM = 0.90;
const AUTO_NORMAL_ZOOM = 1.00;

function intersectionArea(a, b) {
  const left = Math.max(a.left ?? 0, b.left ?? 0);
  const top = Math.max(a.top ?? 0, b.top ?? 0);
  const right = Math.min((a.left ?? 0) + (a.width ?? 0), (b.left ?? 0) + (b.width ?? 0));
  const bottom = Math.min((a.top ?? 0) + (a.height ?? 0), (b.top ?? 0) + (b.height ?? 0));
  return Math.max(0, right - left) * Math.max(0, bottom - top);
}

function selectDisplay(win, displays) {
  if (!win || !displays?.length) return null;
  let best = null;
  let bestArea = -1;
  for (const display of displays.filter(d => d.isEnabled !== false)) {
    const area = intersectionArea(win, display.bounds);
    if (area > bestArea) { bestArea = area; best = display; }
  }
  return best || displays[0];
}

function suggestedZoom(display, displays) {
  const values = displays.map(d => Number(d.dpiX) || 96).filter(v => v > 0);
  const minDpi = values.length ? Math.min(...values) : 96;
  const dpi = Number(display?.dpiX) || minDpi;
  return dpi / minDpi >= AUTO_DPI_RATIO ? AUTO_HIGH_DPI_ZOOM : AUTO_NORMAL_ZOOM;
}

async function getTargetZoom(display, displays) {
  const data = await chrome.storage.local.get({ enabled: true, displayZooms: {} });
  if (!data.enabled) return null;
  const explicitValue = data.displayZooms?.[display.id];
  if (Number.isFinite(Number(explicitValue)) && Number(explicitValue) > 0) return Number(explicitValue);
  return suggestedZoom(display, displays);
}

async function applyZoomToTab(tabId, zoom) {
  if (!tabId || !zoom) return;
  try {
    await chrome.tabs.setZoomSettings(tabId, { mode: 'automatic', scope: 'per-tab' });
    await chrome.tabs.setZoom(tabId, zoom);
  } catch (_) {
    // Browser-internal pages and some special tabs reject zoom. Fail closed.
  }
}

async function applyWindow(windowId) {
  if (!windowId || windowId === chrome.windows.WINDOW_ID_NONE) return;
  try {
    const [win, displays] = await Promise.all([chrome.windows.get(windowId), chrome.system.display.getInfo()]);
    if (!win || win.type !== 'normal') return;
    const display = selectDisplay(win, displays);
    if (!display) return;
    const zoom = await getTargetZoom(display, displays);
    if (!zoom) return;
    const tabs = await chrome.tabs.query({ windowId });
    await Promise.all(tabs.map(tab => applyZoomToTab(tab.id, zoom)));
  } catch (_) { }
}

async function applyAllWindows() {
  try {
    const windows = await chrome.windows.getAll({ windowTypes: ['normal'] });
    await Promise.all(windows.map(w => applyWindow(w.id)));
  } catch (_) { }
}

chrome.runtime.onInstalled.addListener(() => applyAllWindows());
chrome.runtime.onStartup.addListener(() => applyAllWindows());
chrome.windows.onCreated.addListener(win => applyWindow(win.id));
chrome.windows.onBoundsChanged.addListener(win => applyWindow(win.id));
chrome.windows.onFocusChanged.addListener(windowId => applyWindow(windowId));
chrome.tabs.onActivated.addListener(info => applyWindow(info.windowId));
chrome.tabs.onAttached.addListener((tabId, info) => applyWindow(info.newWindowId));
chrome.tabs.onDetached.addListener((tabId, info) => applyWindow(info.oldWindowId));
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'loading' || changeInfo.status === 'complete') applyWindow(tab.windowId);
});
chrome.system.display.onDisplayChanged.addListener(() => applyAllWindows());
chrome.storage.onChanged.addListener(() => applyAllWindows());
