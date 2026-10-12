const fs = require('fs');
const path = require('path');

const serverJsPath = path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LLAI SERVER', 'LLAIBridge', 'server.js');
let code = fs.readFileSync(serverJsPath, 'utf8');

// 1. Helper function for global key pool
const poolLoaderCode = `
// === MULTI-KEY GEMINI FAILOVER ROTATOR ===
let globalGeminiKeys = [];

function getGeminiKeyPool() {
  if (globalGeminiKeys.length === 0) {
    try {
      const fs = require("fs");
      const path = require("path");
      const candidatePaths = [
        path.join(__dirname, "..", "..", "LivingLSAIs.ini"),
        path.join(__dirname, "..", "..", "..", "LivingLSAIs.ini"),
        "C:\\\\Program Files\\\\Epic Games\\\\GTAVEnhanced\\\\scripts\\\\LivingLSAIs.ini",
        "C:\\\\Program Files\\\\Rockstar Games\\\\Grand Theft Auto V\\\\scripts\\\\LivingLSAIs.ini",
        path.join(process.cwd(), "LivingLSAIs.ini"),
        path.join(process.cwd(), "..", "LivingLSAIs.ini")
      ];
      for (const p of candidatePaths) {
        if (fs.existsSync(p)) {
          const text = fs.readFileSync(p, "utf8");
          const matches = text.match(/GeminiKey[1-9]\\s*=\\s*([^\\r\\n;]+)/gi);
          if (matches && matches.length > 0) {
            const keys = matches.map(m => m.split("=")[1].trim()).filter(k => k.length > 10 && !k.startsWith("YOUR_"));
            if (keys.length > 0) {
              globalGeminiKeys = Array.from(new Set(keys));
              console.log(\`[KEY_POOL] Loaded \${globalGeminiKeys.length} free Gemini key(s) from \${p}\`);
              break;
            }
          }
        }
      }
    } catch (e) {
      console.warn("[KEY_POOL] Read error: " + e.message);
    }
  }
  return globalGeminiKeys;
}
`;

if (!code.includes('getGeminiKeyPool()')) {
  code = poolLoaderCode + "\n" + code;
}

// 2. Patch openGeminiSession to select key from pool based on startMsg._keyIndex
const openSessionTarget = 'function openGeminiSession(startMsg, ws) {\n  sessionReady = false;';
const openSessionReplacement = `function openGeminiSession(startMsg, ws) {
  sessionReady = false;
  const pool = getGeminiKeyPool();
  if (startMsg._keyIndex == null) startMsg._keyIndex = 0;
  if (pool.length > 0) {
    apiKey = pool[startMsg._keyIndex % pool.length];
  } else if (startMsg && startMsg.apiKey) {
    apiKey = startMsg.apiKey;
  }`;

if (!code.includes('startMsg._keyIndex')) {
  code = code.replace(openSessionTarget, openSessionReplacement);
}

// 3. Patch socket close handler in openGeminiSession to auto-failover on close before setup
const socketCloseTarget = 'if (!sessionReady) {\n      audioDiag("[GEMINI_CLOSE] The session closed BEFORE setup completed - Gemini rejected the connection itself, " +';
const socketCloseReplacement = `if (!sessionReady) {
      const pool = getGeminiKeyPool();
      if (pool.length > 1 && (startMsg._keyIndex || 0) + 1 < pool.length) {
        startMsg._keyIndex = (startMsg._keyIndex || 0) + 1;
        const nextKey = pool[startMsg._keyIndex % pool.length];
        audioDiag(\`[KEY_FAILOVER] Socket closed before setup (code \${code}). Auto-retrying session with Key #\${startMsg._keyIndex + 1}/\${pool.length} (\${nextKey.substring(0,8)}...)\`);
        if (geminiWs === socket) { sessionReady = false; geminiWs = null; }
        try { openGeminiSession(startMsg, ws); return; } catch (e) {}
      }
      audioDiag("[GEMINI_CLOSE] The session closed BEFORE setup completed - Gemini rejected the connection itself, " +`;

if (!code.includes('[KEY_FAILOVER] Socket closed before setup')) {
  code = code.replace(socketCloseTarget, socketCloseReplacement);
}

fs.writeFileSync(serverJsPath, code, 'utf8');
console.log('Successfully patched server.js with Key Failover Rotator!');
