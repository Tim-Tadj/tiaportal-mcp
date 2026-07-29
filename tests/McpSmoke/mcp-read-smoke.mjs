import { spawn } from "node:child_process";
import { once } from "node:events";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";

const options = parseArguments(process.argv.slice(2));
const brokerPath = resolve(options.broker);
const tiaMajorVersion = parseTiaVersion(options.tiaVersion);
const traceEnabled = process.env.TIA_MCP_SMOKE_TRACE === "1";

if (!existsSync(brokerPath)) {
  throw new Error(`Broker executable was not found: ${brokerPath}`);
}

const child = spawn(
  brokerPath,
  ["--tia-version", `V${tiaMajorVersion}`],
  {
    cwd: dirname(brokerPath),
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true
  });

let nextId = 1;
let stdoutBuffer = "";
let stderrBuffer = "";
let completed = false;
const pending = new Map();

child.stderr.on("data", chunk => {
  stderrBuffer = (stderrBuffer + chunk.toString("utf8")).slice(-30000);
});

child.stdout.on("data", chunk => {
  stdoutBuffer += chunk.toString("utf8");

  for (;;) {
    const newline = stdoutBuffer.indexOf("\n");
    if (newline < 0) {
      break;
    }

    const line = stdoutBuffer.slice(0, newline).replace(/\r$/, "");
    stdoutBuffer = stdoutBuffer.slice(newline + 1);
    if (!line.trim()) {
      continue;
    }

    let message;
    try {
      message = JSON.parse(line);
    } catch {
      rejectPending(new Error(`Invalid JSON from broker: ${line}`));
      continue;
    }

    if (Object.prototype.hasOwnProperty.call(message, "id") &&
        pending.has(message.id)) {
      const entry = pending.get(message.id);
      pending.delete(message.id);
      clearTimeout(entry.timer);

      if (message.error) {
        entry.reject(
          new Error(
            `JSON-RPC ${message.error.code}: ${message.error.message}`));
      } else {
        entry.resolve(message.result);
      }
    }
  }
});

child.on("error", error => {
  rejectPending(
    new Error(`Could not start the broker: ${error.message}`));
});

child.on("exit", (code, signal) => {
  if (!completed) {
    rejectPending(
      new Error(
        `Broker exited early with code ${code}, signal ${signal}. ` +
        `stderr: ${stderrBuffer}`));
  }
});

try {
  const summary = await runSmokeTest();
  process.stdout.write(`${JSON.stringify(summary)}\n`);
} catch (error) {
  process.stderr.write(`${error.stack ?? String(error)}\n`);
  if (stderrBuffer) {
    process.stderr.write(stderrBuffer);
  }
  process.exitCode = 1;
} finally {
  completed = true;
  await stopChild();
}

async function runSmokeTest() {
  const initialised = await request("initialize", {
    protocolVersion: "2024-11-05",
    capabilities: {},
    clientInfo: {
      name: "tia-mcp-read-smoke",
      version: "1.0.0"
    }
  });
  assert(initialised.serverInfo?.name, "The initialise result has no serverInfo.");
  notify("notifications/initialized");

  const listed = await request("tools/list", {});
  const tools = listed.tools ?? [];
  verifyNegotiatedListTools(tools);

  const capabilitiesResult = await callTool("GetCapabilities", {});
  assertToolSucceeded("GetCapabilities", capabilitiesResult);
  const capabilities = JSON.parse(capabilitiesResult.content[0].text);
  assert(
    capabilities.tiaMajorVersion === tiaMajorVersion,
    `Expected TIA V${tiaMajorVersion}, got ${capabilities.tiaMajorVersion}.`);
  assert(
    capabilities.accessProfile === "Read",
    `Expected Read profile, got ${capabilities.accessProfile}.`);
  assert(
    capabilities.defaultResponseFormat === "Auto",
    "GetCapabilities did not advertise Auto as the default response format.");

  if (options.offlineOnly) {
    return {
      server: initialised.serverInfo.name,
      protocolVersion: initialised.protocolVersion,
      tools: tools.length,
      mode: "offline-contract",
      profile: capabilities.accessProfile,
      tiaMajorVersion: capabilities.tiaMajorVersion
    };
  }

  const connectResult = await callTool("Connect", {});
  assertToolSucceeded("Connect", connectResult);

  const projectsCsv = await callTool("ListProjects", {
    detailLevel: "Summary",
    responseFormat: "Auto"
  });
  const projectMetadata = verifyFormattedResult(
    projectsCsv,
    "csv",
    payload => {
      assert(
        payload.startsWith("path,name"),
        `Unexpected project CSV payload: ${payload}`);
    });
  assert(
    projectMetadata.returned > 0,
    "The open TIA Portal instance returned no project or session.");

  const projectsToon = await callTool("ListProjects", {
    detailLevel: "Standard",
    responseFormat: "Auto"
  });
  verifyFormattedResult(
    projectsToon,
    "toon-v4.1",
    verifyToonItems);

  const projectsJson = await callTool("ListProjects", {
    detailLevel: "Summary",
    responseFormat: "Json"
  });
  verifyFormattedResult(
    projectsJson,
    "json",
    payload => {
      const parsed = JSON.parse(payload);
      assert(Array.isArray(parsed.items), "JSON project payload has no items.");
    });

  const devicesCsv = await callTool("GetDevices", {
    detailLevel: "Summary",
    limit: 10,
    responseFormat: "Auto"
  });
  const deviceMetadata = verifyFormattedResult(
    devicesCsv,
    "csv",
    payload => {
      assert(
        payload.startsWith("path,name"),
        `Unexpected device CSV payload: ${payload}`);
    });

  const devicesToon = await callTool("GetDevices", {
    detailLevel: "Standard",
    limit: 10,
    responseFormat: "Auto"
  });
  verifyFormattedResult(
    devicesToon,
    "toon-v4.1",
    verifyToonItems);

  const devicesJson = await callTool("GetDevices", {
    detailLevel: "Summary",
    limit: 10,
    responseFormat: "Json"
  });
  verifyFormattedResult(
    devicesJson,
    "json",
    payload => {
      const parsed = JSON.parse(payload);
      assert(Array.isArray(parsed.items), "JSON device payload has no items.");
      assert(
        typeof parsed.page?.returned === "number",
        "JSON device payload has no page metadata.");
    });

  return {
    server: initialised.serverInfo.name,
    protocolVersion: initialised.protocolVersion,
    tools: tools.length,
    projectRows: projectMetadata.returned,
    deviceRows: deviceMetadata.returned,
    formats: ["csv", "toon-v4.1", "json"],
    profile: capabilities.accessProfile,
    tiaMajorVersion: capabilities.tiaMajorVersion
  };
}

function verifyNegotiatedListTools(tools) {
  for (const name of [
    "ListProjects",
    "GetDevices",
    "GetBlocks",
    "GetTypes"
  ]) {
    const matches = tools.filter(tool => tool.name === name);
    assert(
      matches.length === 1,
      `Expected exactly one ${name} tool, found ${matches.length}.`);
    assert(
      matches[0].inputSchema?.properties?.responseFormat,
      `${name} is missing the responseFormat input schema.`);
  }
}

function verifyFormattedResult(result, expectedFormat, verifyPayload) {
  assertToolSucceeded("formatted tool", result);
  assert(
    Array.isArray(result.content) && result.content.length === 2,
    "Expected metadata and payload text blocks.");
  assert(
    result.content.every(item => item.type === "text"),
    "Expected only text content blocks.");

  const metadataText = JSON.parse(result.content[0].text);
  const metadata = result.structuredContent;
  assert(
    metadata?.format === expectedFormat,
    `Expected ${expectedFormat}, got ${JSON.stringify(metadata)}.`);
  assert(
    metadataText.format === metadata.format &&
    metadataText.returned === metadata.returned &&
    metadataText.hasMore === metadata.hasMore &&
    metadataText.nextCursor === metadata.nextCursor,
    "Metadata text does not match structuredContent.");

  verifyPayload(result.content[1].text, metadata);
  return metadata;
}

function verifyToonItems(payload) {
  assert(
    payload.startsWith("items[") || payload === "items: []",
    `Unexpected TOON payload: ${payload}`);
}

function assertToolSucceeded(name, result) {
  assert(
    result && result.isError !== true,
    `${name} failed: ${JSON.stringify(result)}`);
}

function callTool(name, argumentsValue) {
  trace(`Calling ${name}`);
  return request(
    "tools/call",
    {
      name,
      arguments: argumentsValue
    },
    90000,
    `tools/call ${name}`);
}

function request(
  method,
  params,
  timeoutMs = 90000,
  description = method) {
  const id = nextId++;
  const payload = `${JSON.stringify({
    jsonrpc: "2.0",
    id,
    method,
    params
  })}\n`;

  return new Promise((resolvePromise, rejectPromise) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      rejectPromise(
        new Error(
          `Timed out waiting for ${description}. ` +
          `partial stdout: ${stdoutBuffer}; stderr: ${stderrBuffer}`));
    }, timeoutMs);

    pending.set(id, {
      resolve: resolvePromise,
      reject: rejectPromise,
      timer
    });
    child.stdin.write(payload, "utf8");
  });
}

function notify(method, params = {}) {
  child.stdin.write(
    `${JSON.stringify({
      jsonrpc: "2.0",
      method,
      params
    })}\n`,
    "utf8");
}

function rejectPending(error) {
  for (const entry of pending.values()) {
    clearTimeout(entry.timer);
    entry.reject(error);
  }
  pending.clear();
}

async function stopChild() {
  if (child.exitCode !== null) {
    return;
  }

  const closePromise = once(child, "close").catch(() => {});
  child.stdin.end();
  const stopTimer = setTimeout(() => child.kill(), 5000);
  await closePromise;
  clearTimeout(stopTimer);
}

function parseArguments(args) {
  const parsed = {};

  for (let index = 0; index < args.length; index++) {
    const argument = args[index];
    if (argument === "--offline-only") {
      parsed.offlineOnly = true;
      continue;
    }

    if (argument === "--broker" || argument === "--tia-version") {
      const value = args[++index];
      if (!value) {
        throw new Error(`${argument} requires a value.`);
      }
      parsed[toCamelCase(argument.slice(2))] = value;
      continue;
    }

    throw new Error(`Unknown argument: ${argument}`);
  }

  if (!parsed.broker || !parsed.tiaVersion) {
    throw new Error(
      "Usage: node mcp-read-smoke.mjs --broker <path> " +
      "--tia-version <V17|V18|V19|V20> [--offline-only]");
  }

  return parsed;
}

function parseTiaVersion(value) {
  const match = /^V?(17|18|19|20)$/i.exec(value);
  if (!match) {
    throw new Error(`Unsupported TIA Portal version: ${value}`);
  }
  return Number(match[1]);
}

function toCamelCase(value) {
  return value.replace(
    /-([a-z])/g,
    (_, character) => character.toUpperCase());
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function trace(message) {
  if (traceEnabled) {
    process.stderr.write(`[smoke] ${message}\n`);
  }
}
