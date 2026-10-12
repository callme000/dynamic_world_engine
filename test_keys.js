const fs = require('fs');
const path = require('path');
const https = require('https');

let WebSocket;
try {
    WebSocket = require('ws');
} catch (e) {
    try {
        WebSocket = require(path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LLAI SERVER', 'LLAIBridge', 'node_modules', 'ws'));
    } catch (e2) {
        WebSocket = null;
    }
}

console.log("=========================================================");
console.log("    DYNAMIC WORLD ENGINE - API KEY HEALTH TESTER         ");
console.log("=========================================================\n");

const possibleIniPaths = [
    'C:\\Program Files\\Epic Games\\GTAVEnhanced\\scripts\\LivingLSAIs.ini',
    'C:\\Program Files\\Rockstar Games\\Grand Theft Auto V\\scripts\\LivingLSAIs.ini',
    path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LivingLSAIs.ini'),
    path.join(__dirname, 'LivingLSAIs.ini')
];

let iniPathFound = null;
let iniContent = '';

for (const p of possibleIniPaths) {
    if (fs.existsSync(p)) {
        iniPathFound = p;
        iniContent = fs.readFileSync(p, 'utf8');
        break;
    }
}

if (!iniPathFound) {
    console.log("⚠️ Could not locate LivingLSAIs.ini automatically.");
} else {
    console.log(`📌 Found config file: ${iniPathFound}\n`);
}

const geminiKeys = [];
let groqKey = '';
let openRouterKey = '';

if (iniContent) {
    const geminiMatches = iniContent.match(/GeminiKey[1-9]\s*=\s*([^\r\n;]+)/gi);
    if (geminiMatches) {
        for (const m of geminiMatches) {
            const val = m.split('=')[1].trim();
            if (val.length > 10 && !val.startsWith("YOUR_")) {
                geminiKeys.push(val);
            }
        }
    }

    const groqMatch = iniContent.match(/GroqKey\s*=\s*([^\r\n;]+)/i);
    if (groqMatch && groqMatch[1].trim().length > 5 && !groqMatch[1].trim().startsWith("YOUR_")) {
        groqKey = groqMatch[1].trim();
    }

    const openRouterMatch = iniContent.match(/OpenRouterKey\s*=\s*([^\r\n;]+)/i);
    if (openRouterMatch && openRouterMatch[1].trim().length > 5 && !openRouterMatch[1].trim().startsWith("YOUR_")) {
        openRouterKey = openRouterMatch[1].trim();
    }
}

const desktopApiTxt = path.join(require('os').homedir(), 'Desktop', 'api.txt');
if (!groqKey && fs.existsSync(desktopApiTxt)) {
    const raw = fs.readFileSync(desktopApiTxt, 'utf8').trim();
    if (raw.startsWith('gsk_')) {
        groqKey = raw;
        console.log(`📌 Loaded Groq Key from Desktop/api.txt\n`);
    }
}

process.argv.slice(2).forEach(arg => {
    if (arg.startsWith("AIza") || arg.startsWith("AQ.")) geminiKeys.push(arg);
    if (arg.startsWith("gsk_")) groqKey = arg;
    if (arg.startsWith("sk-or-")) openRouterKey = arg;
});

function testGeminiRest(key) {
    return new Promise((resolve) => {
        const start = Date.now();
        const url = `https://generativelanguage.googleapis.com/v1beta/models/gemini-3.7-flash:generateContent?key=${encodeURIComponent(key)}`;
        const bodyData = JSON.stringify({
            contents: [{ parts: [{ text: "Hello! Reply with OK." }] }]
        });

        const req = https.request(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(bodyData)
            },
            timeout: 7000
        }, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                const elapsed = Date.now() - start;
                if (res.statusCode === 200) {
                    resolve({ ok: true, msg: `200 OK (${elapsed}ms, gemini-3.7-flash)` });
                } else {
                    let errMsg = `HTTP ${res.statusCode}`;
                    if (res.statusCode === 429) errMsg += " (Quota / Rate Limit Exceeded)";
                    else if (res.statusCode === 400 || res.statusCode === 403) errMsg += " (Invalid API Key)";
                    resolve({ ok: false, msg: errMsg });
                }
            });
        });

        req.on('error', (err) => resolve({ ok: false, msg: err.message }));
        req.on('timeout', () => { req.destroy(); resolve({ ok: false, msg: "Timeout (7s)" }); });
        req.write(bodyData);
        req.end();
    });
}

function testGeminiWebSocket(key) {
    return new Promise((resolve) => {
        if (!WebSocket) {
            resolve({ ok: false, msg: "WebSocket module unavailable for test" });
            return;
        }

        const wsUrl = `wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=${encodeURIComponent(key)}`;
        let ws;
        let timer = setTimeout(() => {
            if (ws) ws.terminate();
            resolve({ ok: false, msg: "WebSocket Connection Timeout (5s)" });
        }, 5000);

        try {
            ws = new WebSocket(wsUrl);
            ws.on('open', () => {
                clearTimeout(timer);
                ws.close();
                resolve({ ok: true, msg: "Handshake Successful (Live Voice Ready)" });
            });

            ws.on('error', (err) => {
                clearTimeout(timer);
                let msg = err.message || "Connection Failed";
                if (msg.includes("429")) msg += " (Quota Limit 0)";
                else if (msg.includes("403") || msg.includes("400")) msg += " (Key Rejected)";
                resolve({ ok: false, msg: msg });
            });
        } catch (e) {
            clearTimeout(timer);
            resolve({ ok: false, msg: e.message });
        }
    });
}

function testGroqRest(key) {
    return new Promise((resolve) => {
        const start = Date.now();
        const bodyData = JSON.stringify({
            model: "qwen/qwen3.8-27b",
            messages: [{ role: "user", content: "Hello" }],
            max_tokens: 10
        });

        const req = https.request({
            hostname: 'api.groq.com',
            path: '/openai/v1/chat/completions',
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${key}`,
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(bodyData)
            },
            timeout: 7000
        }, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                const elapsed = Date.now() - start;
                if (res.statusCode === 200) {
                    resolve({ ok: true, msg: `200 OK (${elapsed}ms, Qwen 3.8 27B)` });
                } else {
                    let errMsg = `HTTP ${res.statusCode}`;
                    if (res.statusCode === 401 || res.statusCode === 403) errMsg += " (Invalid Groq Key)";
                    else if (res.statusCode === 429) errMsg += " (Rate Limit Exceeded)";
                    resolve({ ok: false, msg: errMsg });
                }
            });
        });

        req.on('error', (err) => resolve({ ok: false, msg: err.message }));
        req.on('timeout', () => { req.destroy(); resolve({ ok: false, msg: "Timeout (7s)" }); });
        req.write(bodyData);
        req.end();
    });
}

async function runTests() {
    console.log("--- GOOGLE GEMINI KEYS ---");
    if (geminiKeys.length === 0) {
        console.log("❌ No Gemini keys found in LivingLSAIs.ini (GeminiKey1=...)");
    } else {
        for (let i = 0; i < geminiKeys.length; i++) {
            const k = geminiKeys[i];
            const masked = k.substring(0, 8) + "..." + k.substring(k.length - 4);
            console.log(`\n🔑 Key #${i + 1}: ${masked}`);

            const restResult = await testGeminiRest(k);
            console.log(`   • Gemini REST API:      ${restResult.ok ? '✅' : '❌'} ${restResult.msg}`);

            const wsResult = await testGeminiWebSocket(k);
            console.log(`   • Gemini Live WebSocket: ${wsResult.ok ? '✅' : '❌'} ${wsResult.msg}`);
        }
    }

    console.log("\n--- OTHER AI PROVIDERS ---");
    if (groqKey) {
        const groqResult = await testGroqRest(groqKey);
        const maskedGroq = groqKey.substring(0, 6) + "..." + groqKey.substring(groqKey.length - 4);
        console.log(`🔑 Groq Key (${maskedGroq}): ${groqResult.ok ? '✅' : '❌'} ${groqResult.msg}`);
    } else {
        console.log("⚪ Groq Key: Not configured (GroqKey= in LivingLSAIs.ini)");
    }

    console.log("\n=========================================================");
    console.log("                HEALTH CHECK COMPLETED                    ");
    console.log("=========================================================");
}

runTests();
