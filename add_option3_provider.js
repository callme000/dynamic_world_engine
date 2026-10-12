const fs = require('fs');
const path = require('path');

const serverJsPath = path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LLAI SERVER', 'LLAIBridge', 'server.js');
let code = fs.readFileSync(serverJsPath, 'utf8');

// 1. Add Groq & OpenRouter key readers
const groqCode = `
// === MULTI-PROVIDER FREE FALLBACK (OPTION 3: GROQ & OPENROUTER) ===
let groqApiKey = process.env.GROQ_API_KEY || "";
let openRouterApiKey = process.env.OPENROUTER_API_KEY || "";

function loadMultiProviderKeysFromIni() {
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
        const groqMatch = text.match(/GroqKey\\s*=\\s*([^\\r\\n;]+)/i);
        if (groqMatch && groqMatch[1].trim().length > 5) {
          groqApiKey = groqMatch[1].trim();
          console.log("[PROVIDER POOL] Loaded Groq Key from LivingLSAIs.ini");
        }
        const openRouterMatch = text.match(/OpenRouterKey\\s*=\\s*([^\\r\\n;]+)/i);
        if (openRouterMatch && openRouterMatch[1].trim().length > 5) {
          openRouterApiKey = openRouterMatch[1].trim();
          console.log("[PROVIDER POOL] Loaded OpenRouter Key from LivingLSAIs.ini");
        }
      }
    }
  } catch (e) {
    console.warn("[PROVIDER POOL] Warning reading provider keys:", e.message);
  }
}

async function generateViaFreeProviderFallback(prompt, sysPrompt) {
  loadMultiProviderKeysFromIni();
  const apiKey = groqApiKey || openRouterApiKey;
  if (!apiKey) {
    throw new Error("No Groq or OpenRouter key found in LivingLSAIs.ini. Set GroqKey=gsk_... or OpenRouterKey=sk-or-v1-...");
  }
  const isGroq = !!groqApiKey;
  const hostname = isGroq ? "api.groq.com" : "openrouter.ai";
  const reqPath = isGroq ? "/openai/v1/chat/completions" : "/api/v1/chat/completions";
  const model = isGroq ? "llama-3.3-70b-versatile" : "meta-llama/llama-3.3-70b-instruct:free";

  const postData = JSON.stringify({
    model: model,
    messages: [
      { role: "system", content: sysPrompt || "You are a character in Grand Theft Auto V. Reply in character, stay brief and authentic to GTA V tone." },
      { role: "user", content: prompt }
    ],
    temperature: 0.8,
    max_tokens: 500
  });

  const https = require("https");
  return new Promise((resolve, reject) => {
    const req = https.request({
      hostname: hostname,
      port: 443,
      path: reqPath,
      method: "POST",
      headers: {
        "Authorization": "Bearer " + apiKey,
        "Content-Type": "application/json",
        "Content-Length": Buffer.byteLength(postData)
      }
    }, res => {
      let body = "";
      res.on("data", chunk => body += chunk);
      res.on("end", () => {
        try {
          const json = JSON.parse(body);
          if (json.choices && json.choices.length > 0 && json.choices[0].message) {
            console.log(\`[\${isGroq ? "GROQ" : "OPENROUTER"} FALLBACK OK] Received response powered by \${model}\`);
            resolve(json.choices[0].message.content);
          } else {
            reject(new Error(\`[\${isGroq ? "GROQ" : "OPENROUTER"}] Invalid response: \` + body.substring(0, 150)));
          }
        } catch (e) {
          reject(e);
        }
      });
    });

    req.on("error", reject);
    req.write(postData);
    req.end();
  });
}
`;

if (!code.includes('generateViaFreeProviderFallback')) {
  code += groqCode;
}

// 2. Wrap generateScenarioFromGemini error fallback to try generateViaFreeProviderFallback
const restFailMarker = 'if (quotaBlocked) throw new Error(quotaBlocked);';
const restFailReplacement = `if (quotaBlocked) {
    try {
      console.log("[PROVIDER FALLBACK] Gemini Quota Exhausted. Attempting Groq / OpenRouter Fallback...");
      const freeResult = await generateViaFreeProviderFallback(prompt, opts ? opts.systemPrompt : null);
      return freeResult;
    } catch (fallbackErr) {
      console.warn("[PROVIDER FALLBACK] Provider fallback note: " + fallbackErr.message);
    }
    throw new Error(quotaBlocked);
  }`;

if (!code.includes('generateViaFreeProviderFallback(prompt')) {
  code = code.replace(restFailMarker, restFailReplacement);
}

fs.writeFileSync(serverJsPath, code, 'utf8');
console.log('Option 3 Free Multi-Provider (Groq & OpenRouter) Fallback successfully patched into server.js!');
