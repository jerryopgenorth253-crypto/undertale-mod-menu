const http = require("http");
const fs = require("fs/promises");
const fssync = require("fs");
const path = require("path");
const os = require("os");

const ROOT = __dirname;
const SAVE_DIR = process.env.LOCALAPPDATA
  ? path.join(process.env.LOCALAPPDATA, "UNDERTALE")
  : path.join(os.homedir(), "AppData", "Local", "UNDERTALE");

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".txt": "text/plain; charset=utf-8",
  ".svg": "image/svg+xml",
};

function stamp() {
  const d = new Date();
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}-${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`;
}

function sendJson(res, status, data) {
  const body = JSON.stringify(data, null, 2);
  res.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "content-length": Buffer.byteLength(body),
    "cache-control": "no-store",
  });
  res.end(body);
}

function sendText(res, status, body, type = "text/plain; charset=utf-8") {
  res.writeHead(status, {
    "content-type": type,
    "content-length": Buffer.byteLength(body),
    "cache-control": "no-store",
  });
  res.end(body);
}

async function readIfExists(fileName) {
  const fullPath = path.join(SAVE_DIR, fileName);
  try {
    const [stat, text] = await Promise.all([
      fs.stat(fullPath),
      fs.readFile(fullPath, "utf8"),
    ]);
    return {
      exists: true,
      name: fileName,
      path: fullPath,
      length: stat.size,
      modified: stat.mtime.toISOString(),
      text,
    };
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return {
        exists: false,
        name: fileName,
        path: fullPath,
        length: 0,
        modified: null,
        text: "",
      };
    }
    throw error;
  }
}

async function apiState(res) {
  const [ini, file0, file9] = await Promise.all([
    readIfExists("undertale.ini"),
    readIfExists("file0"),
    readIfExists("file9"),
  ]);

  sendJson(res, 200, {
    saveDir: SAVE_DIR,
    files: { ini, file0, file9 },
  });
}

async function readBody(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 2_000_000) {
      throw new Error("Request body is too large.");
    }
    chunks.push(chunk);
  }
  return Buffer.concat(chunks).toString("utf8");
}

async function backupExisting(fileNames) {
  const existing = [];
  for (const fileName of fileNames) {
    const source = path.join(SAVE_DIR, fileName);
    if (fssync.existsSync(source)) {
      existing.push({ fileName, source });
    }
  }

  if (existing.length === 0) {
    return null;
  }

  const backupDir = path.join(ROOT, "backups", stamp());
  await fs.mkdir(backupDir, { recursive: true });
  await Promise.all(
    existing.map(({ fileName, source }) =>
      fs.copyFile(source, path.join(backupDir, fileName))
    )
  );
  return backupDir;
}

async function apiWrite(req, res) {
  const body = await readBody(req);
  const payload = JSON.parse(body || "{}");
  const writes = [];

  if (typeof payload.iniText === "string" && payload.writeIni !== false) {
    writes.push({ fileName: "undertale.ini", text: payload.iniText });
  }

  if (typeof payload.file0Text === "string" && payload.writeFile0) {
    writes.push({ fileName: "file0", text: payload.file0Text });
    if (payload.mirrorFile9 !== false) {
      writes.push({ fileName: "file9", text: payload.file0Text });
    }
  }

  if (writes.length === 0) {
    sendJson(res, 400, { ok: false, error: "Nothing to write." });
    return;
  }

  await fs.mkdir(SAVE_DIR, { recursive: true });
  const backupDir = await backupExisting([...new Set(writes.map((w) => w.fileName))]);

  for (const write of writes) {
    await fs.writeFile(path.join(SAVE_DIR, write.fileName), write.text, "utf8");
  }

  sendJson(res, 200, {
    ok: true,
    saveDir: SAVE_DIR,
    wrote: writes.map((w) => w.fileName),
    backupDir,
  });
}

async function serveStatic(req, res) {
  const url = new URL(req.url, "http://127.0.0.1");
  const rawPath = decodeURIComponent(url.pathname === "/" ? "/index.html" : url.pathname);
  const normalized = path.normalize(rawPath).replace(/^(\.\.[/\\])+/, "");
  const fullPath = path.join(ROOT, normalized);

  if (!fullPath.startsWith(ROOT)) {
    sendText(res, 403, "Forbidden");
    return;
  }

  try {
    const data = await fs.readFile(fullPath);
    const type = MIME[path.extname(fullPath).toLowerCase()] || "application/octet-stream";
    res.writeHead(200, {
      "content-type": type,
      "content-length": data.length,
      "cache-control": "no-store",
    });
    res.end(data);
  } catch (error) {
    if (error && error.code === "ENOENT") {
      sendText(res, 404, "Not found");
      return;
    }
    throw error;
  }
}

const server = http.createServer(async (req, res) => {
  try {
    if (req.method === "GET" && req.url.startsWith("/api/state")) {
      await apiState(res);
      return;
    }

    if (req.method === "POST" && req.url.startsWith("/api/write")) {
      await apiWrite(req, res);
      return;
    }

    if (req.method === "GET" || req.method === "HEAD") {
      await serveStatic(req, res);
      return;
    }

    sendText(res, 405, "Method not allowed");
  } catch (error) {
    sendJson(res, 500, {
      ok: false,
      error: error && error.message ? error.message : String(error),
    });
  }
});

const requestedPort = Number(process.env.PORT || process.argv[2] || 17380);
let attempts = 0;

function listen(port) {
  server.listen(port, "127.0.0.1");
}

server.on("listening", async () => {
  const address = server.address();
  const port = typeof address === "object" && address ? address.port : requestedPort;
  await fs.writeFile(path.join(ROOT, ".server-port"), String(port), "utf8");
  console.log(`Undertale editor running at http://127.0.0.1:${port}`);
  console.log(`Save folder: ${SAVE_DIR}`);
});

server.on("error", (error) => {
  if (error.code === "EADDRINUSE" && attempts < 20) {
    attempts += 1;
    listen(requestedPort + attempts);
    return;
  }
  throw error;
});

listen(requestedPort);
