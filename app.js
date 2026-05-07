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

const el = {
  savePath: document.querySelector("#savePath"),
  routeReadout: document.querySelector("#routeReadout"),
  fileStatus: document.querySelector("#fileStatus"),
  lineCount: document.querySelector("#lineCount"),
  statusLog: document.querySelector("#statusLog"),
  reloadBtn: document.querySelector("#reloadBtn"),
  importBtn: document.querySelector("#importBtn"),
  downloadBtn: document.querySelector("#downloadBtn"),
  writeBtn: document.querySelector("#writeBtn"),
  filePicker: document.querySelector("#filePicker"),
  nameInput: document.querySelector("#nameInput"),
  routeButtons: [...document.querySelectorAll("[data-route]")],
  murderInput: document.querySelector("#murderInput"),
  murderRange: document.querySelector("#murderRange"),
  routeTrackFill: document.querySelector("#routeTrackFill"),
  funInput: document.querySelector("#funInput"),
  funRange: document.querySelector("#funRange"),
  lvInput: document.querySelector("#lvInput"),
  hpInput: document.querySelector("#hpInput"),
  xpInput: document.querySelector("#xpInput"),
  goldInput: document.querySelector("#goldInput"),
  killsInput: document.querySelector("#killsInput"),
  randomFunBtn: document.querySelector("#randomFunBtn"),
  syncStats: document.querySelector("#syncStats"),
  mirrorFile9: document.querySelector("#mirrorFile9"),
  plotInput: document.querySelector("#plotInput"),
  roomInput: document.querySelector("#roomInput"),
  timeInput: document.querySelector("#timeInput"),
  lineInput: document.querySelector("#lineInput"),
  lineValueInput: document.querySelector("#lineValueInput"),
  readLineBtn: document.querySelector("#readLineBtn"),
  setLineBtn: document.querySelector("#setLineBtn"),
};

const state = {
  apiOnline: false,
  saveDir: "",
  iniText: "",
  iniLoaded: false,
  file0Lines: null,
  file0Newline: "\r\n",
  file0LoadedName: "",
  dirty: false,
};

function flagLine(flag) {
  return IDX.flags + flag;
}

function clamp(value, min, max) {
  const n = Number(value);
  if (!Number.isFinite(n)) return min;
  return Math.min(max, Math.max(min, Math.round(n)));
}

function log(message, tone = "info") {
  const color = tone === "ok" ? "var(--green)" : tone === "warn" ? "var(--gold)" : "var(--muted)";
  const line = document.createElement("div");
  line.innerHTML = `<strong style="color:${color}">></strong> ${escapeHtml(message)}`;
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
  return text.includes("\r\n") ? "\r\n" : "\n";
}

function splitSaveLines(text) {
  const newline = detectNewline(text);
  const normalized = text.replace(/\r\n/g, "\n").replace(/\r/g, "\n");
  const lines = normalized.split("\n");
  if (lines.length > 1 && lines[lines.length - 1] === "") {
    lines.pop();
  }
  return { lines, newline };
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

function parseFun(text) {
  const match = text.match(/^\s*fun\s*=\s*"?([0-9.]+)"?\s*$/im);
  if (!match) return 1;
  return clamp(Number.parseFloat(match[1]), 1, 100);
}

function setFun(text, value) {
  const fun = `${clamp(value, 1, 100)}.000000`;
  if (/^\s*fun\s*=/im.test(text)) {
    return text.replace(/^\s*fun\s*=\s*"?[0-9.]+"?\s*$/im, `fun="${fun}"`);
  }
  if (/^\s*\[General\]\s*$/im.test(text)) {
    return text.replace(/^\s*\[General\]\s*$/im, `[General]\r\nfun="${fun}"`);
  }
  return `[General]\r\nfun="${fun}"\r\n${text || ""}`;
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
  const kills = clamp(el.killsInput.value, 0, 9999);
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
    el.timeInput,
    el.lineInput,
    el.lineValueInput,
    el.readLineBtn,
    el.setLineBtn,
    ...el.routeButtons,
  ].forEach((node) => {
    node.disabled = !enabled;
  });
}

function renderFromState() {
  const fun = parseFun(state.iniText);
  el.funInput.value = String(fun);
  el.funRange.value = String(fun);

  if (!state.file0Lines) {
    setFile0ControlsEnabled(false);
    el.nameInput.value = "";
    el.fileStatus.value = state.iniLoaded ? "FUN loaded" : "No save loaded";
    el.lineCount.value = "0 lines";
    updateRouteVisuals();
    return;
  }

  ensureFile0Length();
  setFile0ControlsEnabled(true);
  el.nameInput.value = getLine(IDX.name, "");
  el.lvInput.value = String(clamp(getNumber(IDX.lv, 1), 1, POWER_MAX));
  el.hpInput.value = String(clamp(getNumber(IDX.maxhp, 20), 1, POWER_MAX));
  el.xpInput.value = String(clamp(getNumber(IDX.xp, 0), 0, POWER_MAX));
  el.goldInput.value = String(clamp(getNumber(IDX.gold, 0), 0, POWER_MAX));
  el.killsInput.value = String(clamp(getNumber(IDX.kills, 0), 0, 9999));
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
  const fun = clamp(el.funInput.value, 1, 100);
  state.iniText = setFun(state.iniText || "[General]\r\n", fun);
  state.iniLoaded = true;

  if (!state.file0Lines) {
    return;
  }

  setLine(IDX.name, el.nameInput.value.trim() || "FRISK");
  setNumber(IDX.lv, el.lvInput.value, 1, POWER_MAX);
  setNumber(IDX.maxhp, el.hpInput.value, 1, POWER_MAX);
  setNumber(IDX.maxen, el.hpInput.value, 1, POWER_MAX);
  setNumber(IDX.xp, el.xpInput.value, 0, POWER_MAX);
  setNumber(IDX.gold, el.goldInput.value, 0, POWER_MAX);
  setNumber(IDX.kills, el.killsInput.value, 0, 9999);
  setNumber(flagLine(26), el.murderInput.value, 0, 16);
  setNumber(IDX.plot, el.plotInput.value, -999999, 999999);
  setNumber(IDX.room, el.roomInput.value, -999999, 999999);
  setNumber(IDX.time, el.timeInput.value, 0, 999999999);
  updateRouteVisuals();
}

function loadIni(text, source) {
  state.iniText = text || "[General]\r\nfun=\"1.000000\"";
  state.iniLoaded = true;
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
      state.iniText = "[General]\r\nfun=\"1.000000\"";
      state.iniLoaded = false;
      log("No live undertale.ini yet. FUN edits can create one.", "warn");
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
    el.savePath.textContent = "Offline file mode";
    log(`Server API unavailable: ${error.message}`, "warn");
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
    el.killsInput.value = String(Math.max(1, clamp(el.killsInput.value, 0, 9999)));
    el.murderInput.value = "0";
  }

  if (route === "genocide") {
    el.lvInput.value = "20";
    el.hpInput.value = "99";
    el.xpInput.value = "99999";
    el.killsInput.value = String(Math.max(99, clamp(el.killsInput.value, 0, 9999)));
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
  const lv = clamp(el.lvInput.value, 1, POWER_MAX);
  el.lvInput.value = String(lv);
  if (el.syncStats.checked) {
    el.hpInput.value = String(hpForLevel(lv));
    el.xpInput.value = String(xpForLevel(lv));
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

async function writeLiveSave() {
  syncFormToState();
  if (!state.apiOnline) {
    log("Live write needs the local server. Download files instead.", "warn");
    return;
  }

  el.writeBtn.disabled = true;
  try {
    const response = await fetch("/api/write", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        iniText: state.iniText,
        file0Text: state.file0Lines ? file0Text() : "",
        writeIni: true,
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

el.reloadBtn.addEventListener("click", loadLiveState);
el.importBtn.addEventListener("click", () => el.filePicker.click());
el.downloadBtn.addEventListener("click", () => {
  syncFormToState();
  downloadFile("undertale.ini", state.iniText);
  if (state.file0Lines) {
    downloadFile("file0", file0Text());
  }
});
el.writeBtn.addEventListener("click", writeLiveSave);

el.filePicker.addEventListener("change", async () => {
  const files = [...el.filePicker.files];
  for (const file of files) {
    const text = await file.text();
    const name = file.name.toLowerCase();
    if (name === "undertale.ini" || name.endsWith(".ini")) {
      loadIni(text, file.name);
    } else if (name === "file0" || name === "file9" || /^[^.]+$/.test(name)) {
      loadFile0(text, file.name);
    }
  }
  el.filePicker.value = "";
  renderFromState();
});

el.routeButtons.forEach((button) => {
  button.addEventListener("click", () => applyRoutePreset(button.dataset.route));
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

el.readLineBtn.addEventListener("click", readManualLine);
el.setLineBtn.addEventListener("click", setManualLine);
el.lineInput.addEventListener("input", readManualLine);

setFile0ControlsEnabled(false);
loadLiveState();
