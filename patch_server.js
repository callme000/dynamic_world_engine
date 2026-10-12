const fs = require('fs');
const path = require('path');

const serverJsPath = path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LLAI SERVER', 'LLAIBridge', 'server.js');
let code = fs.readFileSync(serverJsPath, 'utf8');

// 1. Insert key pool & rotator helper functions after `let apiKey = null;`
const targetMarker = 'let apiKey         = null;';
const rotatorCode = `let apiKey         = null;   // set on first startSession

// === AUTOMATED MULTI-KEY POOL & ROTATOR PATCH (Option 2) ===
let apiKeyPool = [];
let currentKeyIndex = 0;

function loadApiKeyPoolFromIni() {
  try {
    const fs = require("fs");
    const path = require("path");
    const candidatePaths = [
      path.join(__dirname, "..", "..", "LivingLSAIs.ini"),
      path.join(__dirname, "..", "..", "..", "LivingLSAIs.ini"),
      path.join(process.cwd(), "LivingLSAIs.ini"),
      path.join(process.cwd(), "..", "LivingLSAIs.ini")
    ];
    for (const iniPath of candidatePaths) {
      if (fs.existsSync(iniPath)) {
        const text = fs.readFileSync(iniPath, "utf8");
        const matches = text.match(/GeminiKey[1-9]\\s*=\\s*([^\\r\\n;]+)/gi);
        if (matches && matches.length > 0) {
          const keys = matches.map(m => m.split("=")[1].trim()).filter(k => k.length > 10);
          if (keys.length > 0) {
            apiKeyPool = Array.from(new Set(keys));
            console.log(\`[KEY POOL] Loaded \${apiKeyPool.length} free Gemini key(s) from \${iniPath}\`);
            return;
          }
        }
      }
    }
  } catch (e) {
    console.warn("[KEY POOL] Warning reading LivingLSAIs.ini:", e.message);
  }
}

function getActiveApiKey(fallbackKey) {
  if (apiKeyPool.length === 0) loadApiKeyPoolFromIni();
  if (fallbackKey && !apiKeyPool.includes(fallbackKey)) {
    apiKeyPool.push(fallbackKey);
  }
  if (apiKeyPool.length === 0) return fallbackKey || apiKey;
  return apiKeyPool[currentKeyIndex % apiKeyPool.length];
}

function rotateApiKeyOnQuotaError(reason) {
  if (apiKeyPool.length === 0) loadApiKeyPoolFromIni();
  if (apiKeyPool.length <= 1) return false;
  const oldIdx = currentKeyIndex;
  currentKeyIndex = (currentKeyIndex + 1) % apiKeyPool.length;
  apiKey = apiKeyPool[currentKeyIndex];
  restQuotaBlockedUntil = 0; // Reset quota cooldown ladder
  restQuotaStrikes = 0;
  console.log(\`[KEY ROTATOR] 🔄 Quota/Rate Limit (\${reason}). Instantly rotated from Key #\${oldIdx + 1} -> Key #\${currentKeyIndex + 1}/\${apiKeyPool.length} (\${apiKey.substring(0, 8)}...)\`);
  return true;
}`;

if (!code.includes('loadApiKeyPoolFromIni()')) {
  code = code.replace(targetMarker, rotatorCode);
}

// 2. Patch openGeminiSession to resolve active key and auto-rotate on rate limit
const openSessionMarker = 'function openGeminiSession(startMsg, ws) {\n  sessionReady = false;';
const openSessionReplacement = `function openGeminiSession(startMsg, ws) {
  sessionReady = false;
  apiKey = getActiveApiKey(startMsg ? startMsg.apiKey : null);
  let initialCooldown = quotaBackoffRemainingMs();
  if (initialCooldown > 0 && rotateApiKeyOnQuotaError("initial cooldown active")) {
    initialCooldown = quotaBackoffRemainingMs();
  }`;

if (!code.includes('getActiveApiKey(startMsg')) {
  code = code.replace(openSessionMarker, openSessionReplacement);
}

// 3. Patch REST 429 quota handler to trigger rotation
const restQuotaMarker = `console.warn(\`[Gemini REST] QUOTA HIT on \${modelName} - pausing REST calls for \${Math.round(waitMs / 1000)}s (strike \${restQuotaStrikes}).\`);`;
const restQuotaReplacement = `rotateApiKeyOnQuotaError("REST 429 quota hit");
        console.warn(\`[Gemini REST] QUOTA HIT on \${modelName} - pausing REST calls for \${Math.round(waitMs / 1000)}s (strike \${restQuotaStrikes}).\`);`;

if (!code.includes('rotateApiKeyOnQuotaError("REST 429 quota hit")')) {
  code = code.replace(restQuotaMarker, restQuotaReplacement);
}

fs.writeFileSync(serverJsPath, code, 'utf8');
console.log('Successfully patched server.js with Multi-Key Pool & Automated 429 Failover Rotator!');
