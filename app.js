const IDX = {
  name: 0,
  lv: 1,
  maxhp: 2,
  maxen: 3,
  xp: 9,
  gold: 10,
  kills: 11,
  weapon: 28,
  armor: 29,
  flags: 30,
  plot: 542,
  song: 546,
  room: 547,
  time: 548,
};

const HP_FOR_LV = [
  0, 20, 24, 28, 32, 36, 40, 44, 48, 52, 56,
  60, 64, 68, 72, 76, 80, 84, 88, 92, 99,
];

const XP_FOR_LV = [
  0, 0, 10, 30, 70, 120, 200, 300, 500, 800, 1200,
  1700, 2500, 3500, 5000, 7000, 10000, 15000, 25000, 50000, 99999,
];

const POWER_MAX = 999999999;
const VANILLA_LIMITS = {
  lv: 20,
  hp: 99,
  xp: 99999,
  gold: 9999,
  damage: 9999,
  kills: 9999,
};

const DEFAULT_INI = "[General]\r\nfun=\"1.000000\"\r\n";
const DEFAULT_LIVE = "[Player]\r\ndamage=-1\r\n[Controls]\r\nwasd=0\r\n";

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];

const el = {
  savePath: $("#savePath"),
  routeReadout: $("#routeReadout"),
  fileStatus: $("#fileStatus"),
  liveStatus: $("#liveStatus"),
  lineCount: $("#lineCount"),
  statusLog: $("#statusLog"),
  reloadBtn: $("#reloadBtn"),
  installAppBtn: $("#installAppBtn"),
  importBtn: $("#importBtn"),
  downloadBtn: $("#downloadBtn"),
  writeBtn: $("#writeBtn"),
  filePicker: $("#filePicker"),
  nameInput: $("#nameInput"),
  routeButtons: $$("[data-route]"),
  quickButtons: $$("[data-quick]"),
  murderInput: $("#murderInput"),
  murderRange: $("#murderRange"),
  routeTrackFill: $("#routeTrackFill"),
  funInput: $("#funInput"),
  funRange: $("#funRange"),
  lvInput: $("#lvInput"),
  hpInput: $("#hpInput"),
  xpInput: $("#xpInput"),
  goldInput: $("#goldInput"),
  damageInput: $("#damageInput"),
  killsInput: $("#killsInput"),
  randomFunBtn: $("#randomFunBtn"),
  syncStats: $("#syncStats"),
  capBreakToggle: $("#capBreakToggle"),
  mirrorFile9: $("#mirrorFile9"),
  wasdToggle: $("#wasdToggle"),
  plotInput: $("#plotInput"),
  roomInput: $("#roomInput"),
  roomPreset: $("#roomPreset"),
  applyRoomBtn: $("#applyRoomBtn"),
  timeInput: $("#timeInput"),
  lineInput: $("#lineInput"),
  lineValueInput: $("#lineValueInput"),
  readLineBtn: $("#readLineBtn"),
  setLineBtn: $("#setLineBtn"),
};

const state = {
  apiOnline: false,
  saveDir: "",
  iniText: DEFAULT_INI,
  iniLoaded: false,
  liveText: DEFAULT_LIVE,
  liveLoaded: false,
  file0Lines: null,
  file0Newline: "\r\n",
  file0LoadedName: "",
  dirty: false,
  installPromptEvent: null,
};

function flagLine(flag) {
  return IDX.flags + flag;
}

function clamp(value, min, max) {
  const n = Number(value);
  if (!Number.isFinite(n)) return min;
  return Math.min(max, Math.max(min, Math.round(n)));
}

function currentLimits() {
  if (el.capBreakToggle && el.capBreakToggle.checked) {
    return {
      lv: POWER_MAX,
      hp: POWER_MAX,
      xp: POWER_MAX,
      gold: POWER_MAX,
      damage: POWER_MAX,
      kills: POWER_MAX,
    };
  }
  return VANILLA_LIMITS;
}

function applyInputLimits() {
  const limits = currentLimits();
  el.lvInput.max = String(limits.lv);
  el.hpInput.max = String(limits.hp);
  el.xpInput.max = String(limits.xp);
  el.goldInput.max = String(limits.gold);
  el.damageInput.max = String(limits.damage);
  el.killsInput.max = String(limits.kills);
}

function log(message, tone = "info") {
  if (!el.statusLog) return;
  const color = tone === "ok" ? "var(--green)" : tone === "warn" ? "var(--gold)" : "var(--muted)";
  const line = document.createElement("div");
  line.innerHTML = `<strong style="color:${color}">&gt;</strong> ${escapeHtml(message)}`;
  el.statusLog.prepend(line);
}

function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, (ch) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#039;",
  }[ch]));
}

function detectNewline(text) {
  return text && text.includes("\r\n") ? "\r\n" : "\n";
}

function splitLines(text) {
  const newline = detectNewline(text);
  const normalized = String(text || "").replace(/\r\n/g, "\n").replace(/\r/g, "\n");
  const lines = normalized.split("\n");
  if (lines.length > 1 && lines[lines.length - 1] === "") {
    lines.pop();
  }
  return { lines, newline };
}

function splitSaveLines(text) {
  const parsed = splitLines(text);
  return { lines: parsed.lines, newline: parsed.newline };
}

function ensureFile0Length() {
  if (!state.file0Lines) return;
  while (state.file0Lines.length <= IDX.time) {
    state.file0Lines.push("0");
  }
}

function getLine(index, fallback = "") {
  if (!state.file0Lines || index < 0) return fallback;
  return state.file0Lines[index] ?? fallback;
}

function setLine(index, value) {
  if (!state.file0Lines || index < 0) return;
  ensureFile0Length();
  state.file0Lines[index] = String(value);
  state.dirty = true;
}

function getNumber(index, fallback = 0) {
  const n = Number(getLine(index, fallback));
  return Number.isFinite(n) ? n : fallback;
}

function setNumber(index, value, min = -999999, max = 999999) {
  setLine(index, String(clamp(value, min, max)));
}

function file0Text() {
  if (!state.file0Lines) return "";
  ensureFile0Length();
  return state.file0Lines.join(state.file0Newline);
}

function parseIniValue(text, sectionName, keyName, fallback = "") {
  const parsed = splitLines(text || "");
  let section = "";
  for (const raw of parsed.lines) {
    const line = raw.trim();
    if (!line || line.startsWith("#") || line.startsWith(";")) continue;
    if (line.startsWith("[") && line.endsWith("]")) {
      section = line.slice(1, -1).trim();
      continue;
    }
    const eq = line.indexOf("=");
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim();
    const value = line.slice(eq + 1).trim().replace(/^"|"$/g, "");
    if (section.toLowerCase() === sectionName.toLowerCase() && key.toLowerCase() === keyName.toLowerCase()) {
      return value;
    }
  }
  return fallback;
}

function setIniValue(text, sectionName, keyName, rawValue) {
  const source = text || "";
  const parsed = splitLines(source);
  const lines = parsed.lines.length ? parsed.lines : [];
  let sectionStart = -1;
  let sectionEnd = lines.length;

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i].trim();
    if (line.startsWith("[") && line.endsWith("]")) {
      const section = line.slice(1, -1).trim();
      if (section.toLowerCase() === sectionName.toLowerCase()) {
        sectionStart = i;
        sectionEnd = lines.length;
        for (let j = i + 1; j < lines.length; j += 1) {
          const next = lines[j].trim();
          if (next.startsWith("[") && next.endsWith("]")) {
            sectionEnd = j;
            break;
          }
        }
        break;
      }
    }
  }

  const entry = `${keyName}=${rawValue}`;
  if (sectionStart === -1) {
    if (lines.length && lines[lines.length - 1] !== "") lines.push("");
    lines.push(`[${sectionName}]`, entry);
    return lines.join(parsed.newline) + parsed.newline;
  }

  for (let i = sectionStart + 1; i < sectionEnd; i += 1) {
    const eq = lines[i].indexOf("=");
    if (eq <= 0) continue;
    const key = lines[i].slice(0, eq).trim();
    if (key.toLowerCase() === keyName.toLowerCase()) {
      lines[i] = entry;
      return lines.join(parsed.newline) + parsed.newline;
    }
  }

  lines.splice(sectionStart + 1, 0, entry);
  return lines.join(parsed.newline) + parsed.newline;
}

function parseFun(text) {
  return clamp(parseIniValue(text, "General", "fun", "1"), 1, 100);
}

function setFun(text, value) {
  const fun = `${clamp(value, 1, 100)}.000000`;
  return setIniValue(text || DEFAULT_INI, "General", "fun", `"${fun}"`);
}

function parseLiveDamage() {
  return clamp(parseIniValue(state.liveText, "Player", "damage", "-1"), -1, POWER_MAX);
}

function parseLiveWasd() {
  const value = parseIniValue(state.liveText, "Controls", "wasd", "0").toLowerCase();
  return value === "1" || value === "true" || value === "on" || value === "yes";
}

function syncLiveConfig() {
  const limits = currentLimits();
  const damage = clamp(el.damageInput.value, -1, limits.damage);
  state.liveText = setIniValue(state.liveText || DEFAULT_LIVE, "Player", "damage", String(damage));
  state.liveText = setIniValue(state.liveText, "Controls", "wasd", el.wasdToggle.checked ? "1" : "0");
  state.liveLoaded = true;
}

function setActiveRoute(route) {
  el.routeButtons.forEach((button) => {
    button.classList.toggle("active", button.dataset.route === route);
  });
}

function currentMurderLevel() {
  return clamp(el.murderInput.value, 0, 16);
}

function inferredRoute() {
  const murder = currentMurderLevel();
  const lv = clamp(el.lvInput.value, 1, POWER_MAX);
  const kills = clamp(el.killsInput.value, 0, POWER_MAX);
  const xp = clamp(el.xpInput.value, 0, POWER_MAX);
  if (!state.file0Lines) return "No file0";
  if (murder >= 16) return "Genocide";
  if (murder > 0) return `Murder ${murder}/16`;
  if (lv === 1 && kills === 0 && xp === 0) return "Pacifist";
  return "Neutral";
}

function updateRouteVisuals() {
  const murder = currentMurderLevel();
  el.murderRange.value = String(murder);
  el.routeTrackFill.style.width = `${(murder / 16) * 100}%`;
  el.routeReadout.value = inferredRoute();
  const route = inferredRoute().toLowerCase().startsWith("murder") ? "custom" : inferredRoute().toLowerCase();
  if (["pacifist", "neutral", "genocide"].includes(route)) {
    setActiveRoute(route);
  } else {
    setActiveRoute("custom");
  }
}

function setFile0ControlsEnabled(enabled) {
  [
    el.nameInput,
    el.lvInput,
    el.hpInput,
    el.xpInput,
    el.goldInput,
    el.killsInput,
    el.murderInput,
    el.murderRange,
    el.plotInput,
    el.roomInput,
    el.roomPreset,
    el.applyRoomBtn,
    el.timeInput,
    el.lineInput,
    el.lineValueInput,
    el.readLineBtn,
    el.setLineBtn,
    ...el.routeButtons,
    ...el.quickButtons,
  ].forEach((node) => {
    if (node) node.disabled = !enabled;
  });
}

function renderFromState() {
  applyInputLimits();
  const limits = currentLimits();
  const fun = parseFun(state.iniText);
  el.funInput.value = String(fun);
  el.funRange.value = String(fun);
  el.damageInput.value = String(clamp(parseLiveDamage(), -1, limits.damage));
  el.wasdToggle.checked = parseLiveWasd();
  el.liveStatus.value = state.liveLoaded ? "loaded" : "ready";

  if (!state.file0Lines) {
    setFile0ControlsEnabled(false);
    el.nameInput.value = "";
    el.fileStatus.value = state.iniLoaded ? "FUN loaded" : "Import file0";
    el.lineCount.value = "0 lines";
    updateRouteVisuals();
    return;
  }

  ensureFile0Length();
  setFile0ControlsEnabled(true);
  el.nameInput.value = getLine(IDX.name, "");
  el.lvInput.value = String(clamp(getNumber(IDX.lv, 1), 1, limits.lv));
  el.hpInput.value = String(clamp(getNumber(IDX.maxhp, 20), 1, limits.hp));
  el.xpInput.value = String(clamp(getNumber(IDX.xp, 0), 0, limits.xp));
  el.goldInput.value = String(clamp(getNumber(IDX.gold, 0), 0, limits.gold));
  el.killsInput.value = String(clamp(getNumber(IDX.kills, 0), 0, limits.kills));
  el.murderInput.value = String(clamp(getNumber(flagLine(26), 0), 0, 16));
  el.murderRange.value = el.murderInput.value;
  el.plotInput.value = String(getNumber(IDX.plot, 0));
  el.roomInput.value = String(getNumber(IDX.room, 0));
  el.timeInput.value = String(getNumber(IDX.time, 0));
  el.fileStatus.value = `${state.file0LoadedName || "file0"} loaded`;
  el.lineCount.value = `${state.file0Lines.length} lines`;
  readManualLine();
  updateRouteVisuals();
}

function syncFormToState() {
  applyInputLimits();
  const limits = currentLimits();
  const fun = clamp(el.funInput.value, 1, 100);
  state.iniText = setFun(state.iniText || DEFAULT_INI, fun);
  state.iniLoaded = true;
  syncLiveConfig();

  if (!state.file0Lines) {
    return;
  }

  setLine(IDX.name, el.nameInput.value.trim() || "FRISK");
  setNumber(IDX.lv, el.lvInput.value, 1, limits.lv);
  setNumber(IDX.maxhp, el.hpInput.value, 1, limits.hp);
  setNumber(IDX.maxen, el.hpInput.value, 1, limits.hp);
  setNumber(IDX.xp, el.xpInput.value, 0, limits.xp);
  setNumber(IDX.gold, el.goldInput.value, 0, limits.gold);
  setNumber(IDX.kills, el.killsInput.value, 0, limits.kills);
  setNumber(flagLine(26), el.murderInput.value, 0, 16);
  setNumber(IDX.plot, el.plotInput.value, -999999, 999999);
  setNumber(IDX.room, el.roomInput.value, -999999, 999999);
  setNumber(IDX.time, el.timeInput.value, 0, POWER_MAX);
  updateRouteVisuals();
}

function loadIni(text, source) {
  state.iniText = text || DEFAULT_INI;
  state.iniLoaded = true;
  log(`Loaded ${source}.`, "ok");
}

function loadLiveConfig(text, source) {
  state.liveText = text || DEFAULT_LIVE;
  state.liveLoaded = true;
  log(`Loaded ${source}.`, "ok");
}

function loadFile0(text, source) {
  const parsed = splitSaveLines(text);
  state.file0Lines = parsed.lines;
  state.file0Newline = parsed.newline;
  state.file0LoadedName = source;
  ensureFile0Length();
  log(`Loaded ${source}.`, "ok");
}

async function loadLiveState() {
  try {
    const response = await fetch("/api/state", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    state.apiOnline = true;
    state.saveDir = data.saveDir;
    el.savePath.textContent = data.saveDir;

    if (data.files.ini.exists) {
      loadIni(data.files.ini.text, "live undertale.ini");
    } else {
      state.iniText = DEFAULT_INI;
      state.iniLoaded = false;
      log("No live undertale.ini found. Mobile exports can create one.", "warn");
    }

    if (data.files.live && data.files.live.exists) {
      loadLiveConfig(data.files.live.text, "live codex_live.ini");
    } else {
      state.liveText = DEFAULT_LIVE;
      state.liveLoaded = false;
    }

    if (data.files.file0.exists) {
      loadFile0(data.files.file0.text, "live file0");
    } else if (data.files.file9.exists) {
      loadFile0(data.files.file9.text, "live file9");
    } else {
      state.file0Lines = null;
      state.file0LoadedName = "";
      log("No live file0/file9 found yet.", "warn");
    }

    state.dirty = false;
    renderFromState();
  } catch (error) {
    state.apiOnline = false;
    el.savePath.textContent = "Offline mobile file mode";
    log(`Local server unavailable: ${error.message}. Import files instead.`, "warn");
    renderFromState();
  }
}

function applyRoutePreset(route) {
  if (!state.file0Lines) return;

  if (route === "pacifist") {
    el.lvInput.value = "1";
    el.hpInput.value = "20";
    el.xpInput.value = "0";
    el.killsInput.value = "0";
    el.murderInput.value = "0";
  }

  if (route === "neutral") {
    const lv = Math.max(2, clamp(el.lvInput.value, 1, POWER_MAX));
    el.lvInput.value = String(lv);
    el.hpInput.value = String(hpForLevel(lv));
    el.xpInput.value = String(Math.max(10, xpForLevel(lv)));
    el.killsInput.value = String(Math.max(1, clamp(el.killsInput.value, 0, POWER_MAX)));
    el.murderInput.value = "0";
  }

  if (route === "genocide") {
    el.lvInput.value = "20";
    el.hpInput.value = "99";
    el.xpInput.value = "99999";
    el.killsInput.value = String(Math.max(99, clamp(el.killsInput.value, 0, POWER_MAX)));
    el.murderInput.value = "16";
  }

  if (route === "custom") {
    setActiveRoute("custom");
  }

  syncFormToState();
  renderFromState();
  log(`Applied ${route} route preset.`, "ok");
}

function hpForLevel(lv) {
  if (lv >= 0 && lv < HP_FOR_LV.length) return HP_FOR_LV[lv];
  return clamp(16 + (lv * 4), 1, POWER_MAX);
}

function xpForLevel(lv) {
  if (lv >= 0 && lv < XP_FOR_LV.length) return XP_FOR_LV[lv];
  return clamp(99999 + ((lv - 20) * 50000), 0, POWER_MAX);
}

function syncLvStats() {
  const limits = currentLimits();
  const lv = clamp(el.lvInput.value, 1, limits.lv);
  el.lvInput.value = String(lv);
  if (el.syncStats.checked) {
    el.hpInput.value = String(clamp(hpForLevel(lv), 1, limits.hp));
    el.xpInput.value = String(clamp(xpForLevel(lv), 0, limits.xp));
  }
  syncFormToState();
  renderFromState();
}

function readManualLine() {
  if (!state.file0Lines) return;
  const line = clamp(el.lineInput.value, 1, 999) - 1;
  el.lineValueInput.value = getLine(line, "");
}

function setManualLine() {
  if (!state.file0Lines) return;
  const line = clamp(el.lineInput.value, 1, 999) - 1;
  setLine(line, el.lineValueInput.value);
  renderFromState();
  log(`Set line ${line + 1}.`, "ok");
}

function downloadFile(name, text) {
  const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.append(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function downloadEditedFiles() {
  syncFormToState();
  downloadFile("undertale.ini", state.iniText);
  downloadFile("codex_live.ini", state.liveText);
  if (state.file0Lines) {
    downloadFile("file0", file0Text());
    if (el.mirrorFile9.checked) {
      downloadFile("file9", file0Text());
    }
  }
  log("Downloaded edited mobile files.", "ok");
}

async function writeLiveSave() {
  syncFormToState();
  if (!state.apiOnline) {
    log("Write Live needs the local PC server. On mobile, use Save Files.", "warn");
    return;
  }

  el.writeBtn.disabled = true;
  try {
    const response = await fetch("/api/write", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        iniText: state.iniText,
        liveText: state.liveText,
        file0Text: state.file0Lines ? file0Text() : "",
        writeIni: true,
        writeLiveConfig: true,
        writeFile0: Boolean(state.file0Lines),
        mirrorFile9: el.mirrorFile9.checked,
      }),
    });
    const result = await response.json();
    if (!response.ok || !result.ok) {
      throw new Error(result.error || `HTTP ${response.status}`);
    }
    state.dirty = false;
    const backupText = result.backupDir ? ` Backup: ${result.backupDir}` : "";
    log(`Wrote ${result.wrote.join(", ")}.${backupText}`, "ok");
    await loadLiveState();
  } catch (error) {
    log(`Write failed: ${error.message}`, "warn");
  } finally {
    el.writeBtn.disabled = false;
  }
}

function classifyImportedFile(fileName, text) {
  const name = fileName.toLowerCase();
  if (name === "undertale.ini") return "ini";
  if (name === "codex_live.ini") return "live";
  if (name === "file0" || name === "file9" || !name.includes(".")) return "file0";
  if (/\[controls\]/i.test(text) || /\[player\]/i.test(text) && /damage\s*=/i.test(text)) return "live";
  if (/\[general\]/i.test(text) || /fun\s*=/i.test(text)) return "ini";
  return "file0";
}

function applyQuickAction(action) {
  if (!state.file0Lines) return;
  el.capBreakToggle.checked = true;
  applyInputLimits();

  if (action === "fresh-start") {
    el.nameInput.value = "FRISK";
    el.lvInput.value = "1";
    el.hpInput.value = "20";
    el.xpInput.value = "0";
    el.goldInput.value = "0";
    el.damageInput.value = "-1";
    el.killsInput.value = "0";
    el.murderInput.value = "0";
    el.roomInput.value = "4";
    el.plotInput.value = "0";
    el.timeInput.value = "0";
  }

  if (action === "omega-ready") {
    el.lvInput.value = "20";
    el.hpInput.value = "99";
    el.xpInput.value = "99999";
    el.goldInput.value = "9999";
    el.damageInput.value = "9999";
    el.killsInput.value = "99";
    el.murderInput.value = "16";
    el.roomInput.value = "237";
  }

  if (action === "max-all") {
    el.lvInput.value = String(POWER_MAX);
    el.hpInput.value = String(POWER_MAX);
    el.xpInput.value = String(POWER_MAX);
    el.goldInput.value = String(POWER_MAX);
    el.damageInput.value = String(POWER_MAX);
    el.killsInput.value = String(POWER_MAX);
    el.murderInput.value = "16";
  }

  if (action === "random-run") {
    const rooms = [4, 19, 44, 82, 155, 207, 231, 237];
    const lv = Math.floor(Math.random() * 20) + 1;
    el.funInput.value = String(Math.floor(Math.random() * 100) + 1);
    el.funRange.value = el.funInput.value;
    el.lvInput.value = String(lv);
    el.hpInput.value = String(hpForLevel(lv));
    el.xpInput.value = String(xpForLevel(lv));
    el.goldInput.value = String(Math.floor(Math.random() * 9999));
    el.damageInput.value = String(Math.floor(Math.random() * 999) + 1);
    el.killsInput.value = String(Math.floor(Math.random() * 120));
    el.murderInput.value = String(Math.floor(Math.random() * 17));
    el.roomInput.value = String(rooms[Math.floor(Math.random() * rooms.length)]);
  }

  syncFormToState();
  renderFromState();
  log(`Applied ${action.replace("-", " ")}.`, "ok");
}

async function promptInstallApp() {
  if (state.installPromptEvent) {
    state.installPromptEvent.prompt();
    await state.installPromptEvent.userChoice;
    state.installPromptEvent = null;
    return;
  }
  log("Use your browser menu, then Add to Home Screen.", "warn");
}

el.reloadBtn.addEventListener("click", loadLiveState);
el.installAppBtn.addEventListener("click", promptInstallApp);
el.importBtn.addEventListener("click", () => el.filePicker.click());
el.downloadBtn.addEventListener("click", downloadEditedFiles);
el.writeBtn.addEventListener("click", writeLiveSave);

el.filePicker.addEventListener("change", async () => {
  const files = [...el.filePicker.files];
  for (const file of files) {
    const text = await file.text();
    const kind = classifyImportedFile(file.name, text);
    if (kind === "ini") loadIni(text, file.name);
    if (kind === "live") loadLiveConfig(text, file.name);
    if (kind === "file0") loadFile0(text, file.name);
  }
  el.filePicker.value = "";
  renderFromState();
});

el.routeButtons.forEach((button) => {
  button.addEventListener("click", () => applyRoutePreset(button.dataset.route));
});

el.quickButtons.forEach((button) => {
  button.addEventListener("click", () => applyQuickAction(button.dataset.quick));
});

el.funInput.addEventListener("input", () => {
  const fun = clamp(el.funInput.value, 1, 100);
  el.funRange.value = String(fun);
  syncFormToState();
});
el.funRange.addEventListener("input", () => {
  el.funInput.value = el.funRange.value;
  syncFormToState();
});
el.randomFunBtn.addEventListener("click", () => {
  const value = Math.floor(Math.random() * 100) + 1;
  el.funInput.value = String(value);
  el.funRange.value = String(value);
  syncFormToState();
  log(`FUN set to ${value}.`, "ok");
});

el.lvInput.addEventListener("input", syncLvStats);
[
  el.nameInput,
  el.hpInput,
  el.xpInput,
  el.goldInput,
  el.damageInput,
  el.killsInput,
  el.plotInput,
  el.roomInput,
  el.timeInput,
].forEach((node) => {
  node.addEventListener("input", () => {
    syncFormToState();
    updateRouteVisuals();
  });
});

el.capBreakToggle.addEventListener("change", () => {
  applyInputLimits();
  syncFormToState();
  renderFromState();
  log(el.capBreakToggle.checked ? "Caps removed." : "Vanilla caps restored.", "ok");
});

el.wasdToggle.addEventListener("change", () => {
  syncFormToState();
  log(el.wasdToggle.checked ? "WASD Move enabled in codex_live.ini." : "WASD Move disabled in codex_live.ini.", "ok");
});

el.mirrorFile9.addEventListener("change", syncFormToState);
el.syncStats.addEventListener("change", syncLvStats);

el.murderInput.addEventListener("input", () => {
  el.murderInput.value = String(clamp(el.murderInput.value, 0, 16));
  syncFormToState();
  renderFromState();
});
el.murderRange.addEventListener("input", () => {
  el.murderInput.value = el.murderRange.value;
  syncFormToState();
  renderFromState();
});

el.roomPreset.addEventListener("change", () => {
  if (el.roomPreset.value) el.roomInput.value = el.roomPreset.value;
});
el.applyRoomBtn.addEventListener("click", () => {
  if (el.roomPreset.value) el.roomInput.value = el.roomPreset.value;
  syncFormToState();
  renderFromState();
  log(`Room set to ${el.roomInput.value}.`, "ok");
});

el.readLineBtn.addEventListener("click", readManualLine);
el.setLineBtn.addEventListener("click", setManualLine);
el.lineInput.addEventListener("input", readManualLine);

window.addEventListener("beforeinstallprompt", (event) => {
  event.preventDefault();
  state.installPromptEvent = event;
});

if ("serviceWorker" in navigator) {
  window.addEventListener("load", () => {
    navigator.serviceWorker.register("./sw.js").catch(() => {});
  });
}

setFile0ControlsEnabled(false);
loadLiveState();
