const fs = require('fs');
const path = require('path');
const readline = require('readline');

let WebSocket;
try {
    WebSocket = require('ws');
} catch (e) {
    try {
        WebSocket = require(path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LLAI SERVER', 'LLAIBridge', 'node_modules', 'ws'));
    } catch (e2) {
        try {
            WebSocket = require('C:\\Program Files\\Epic Games\\GTAVEnhanced\\scripts\\LLAI SERVER\\LLAIBridge\\node_modules\\ws');
        } catch (e3) {
            console.error("❌ Could not load 'ws' module. Please run from workspace directory.");
            process.exit(1);
        }
    }
}

console.log("=========================================================");
console.log("  GTA V NPC TALK TESTER & INTERACTIVE CONVERSATION CLI   ");
console.log("=========================================================\n");

// 1. Read API keys from LivingLSAIs.ini
const possibleIniPaths = [
    'C:\\Program Files\\Epic Games\\GTAVEnhanced\\scripts\\LivingLSAIs.ini',
    'C:\\Program Files\\Rockstar Games\\Grand Theft Auto V\\scripts\\LivingLSAIs.ini',
    path.join(__dirname, '2234fb-LivingLSAIs4.5', 'LivingLSAIs.ini'),
    path.join(__dirname, 'LivingLSAIs.ini')
];

const keysPool = [];
for (const p of possibleIniPaths) {
    if (fs.existsSync(p)) {
        const text = fs.readFileSync(p, 'utf8');
        const matches = text.match(/GeminiKey[1-9]\s*=\s*([^\r\n;]+)/gi);
        if (matches) {
            for (const m of matches) {
                const val = m.split('=')[1].trim();
                if (val.length > 10 && !val.startsWith("YOUR_")) {
                    keysPool.push(val);
                }
            }
        }
        if (keysPool.length > 0) {
            console.log(`📌 Loaded ${keysPool.length} key(s) from: ${p}`);
            break;
        }
    }
}

if (keysPool.length === 0) {
    console.error("❌ ERROR: No valid Gemini keys found in LivingLSAIs.ini!");
    process.exit(1);
}

keysPool.forEach((k, idx) => {
    console.log(`🔑 Key #${idx + 1}: ${k.substring(0, 8)}...${k.substring(k.length - 4)}`);
});
console.log("");

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});

let currentKeyIndex = 0;

async function runInteractiveTest() {
    console.log(`[Phase 1/3] Connecting to Local Node.js Bridge (ws://127.0.0.1:8765)...`);
    
    let bridgeWs;
    try {
        bridgeWs = new WebSocket("ws://127.0.0.1:8765");
    } catch (e) {
        console.error("❌ FAILED to connect to local bridge server. Is `server.js` running?");
        rl.close();
        return;
    }

    const bridgeTimeout = setTimeout(() => {
        console.error("❌ TIMEOUT: Local bridge server did not respond in 3 seconds.");
        console.error("👉 DIAGNOSIS: Start `start.bat` or `install_tools.bat` in `LLAI SERVER\\LLAIBridge\\`.");
        rl.close();
        process.exit(1);
    }, 3000);

    bridgeWs.on('error', (err) => {
        clearTimeout(bridgeTimeout);
        console.error("❌ FAILED to connect to local bridge server (ws://127.0.0.1:8765):", err.message);
        rl.close();
    });

    bridgeWs.on('open', () => {
        clearTimeout(bridgeTimeout);
        console.log("✅ Connected to Local Node.js Bridge server!");
        
        const activeKey = keysPool[currentKeyIndex % keysPool.length];
        console.log(`\n[Phase 2/3] Initializing session with Key #${currentKeyIndex + 1}/${keysPool.length} (${activeKey.substring(0, 8)}...)...`);

        const sessionId = Math.floor(Math.random() * 9000) + 1000;
        const startSessionMsg = {
            type: "startSession",
            sessionId: sessionId,
            npcHandle: 9999,
            npcName: "Darnell (Drug Dealer)",
            apiKey: activeKey,
            model: "gemini-3.1-flash-live-preview",
            voiceName: "Puck",
            languageMode: "english",
            systemPrompt: "You are Darnell 'D-Lance' Jenkins, a street drug dealer in Chamberlain Hills. Reply in character, stay brief and authentic to GTA V street slang.",
            enableAbusingWords: true
        };

        bridgeWs.send(JSON.stringify(startSessionMsg));

        let sessionTimeout = setTimeout(() => {
            console.error(`\n❌ TIMEOUT (12s): Key #${currentKeyIndex + 1} failed to open Gemini session!`);
            bridgeWs.close();

            if (currentKeyIndex + 1 < keysPool.length) {
                currentKeyIndex++;
                console.log(`🔄 Retrying with Key #${currentKeyIndex + 1}/${keysPool.length}...\n`);
                runInteractiveTest();
            } else {
                console.error("❌ All configured Gemini keys in LivingLSAIs.ini failed or rate-limited.");
                rl.close();
                process.exit(1);
            }
        }, 12000);

        let currentTurnText = "";

        bridgeWs.on('message', (data, isBinary) => {
            if (isBinary) {
                // Audio PCM bytes received from server.js
                return;
            }

            try {
                const msg = JSON.parse(data.toString());

                if (msg.type === "sessionReady") {
                    clearTimeout(sessionTimeout);
                    console.log(`✅ Gemini Live session OPENED successfully using Key #${currentKeyIndex + 1}!\n`);
                    startChatLoop(bridgeWs, sessionId);
                } else if (msg.type === "caption" || msg.type === "npcTextChunk") {
                    currentTurnText += msg.text || "";
                    process.stdout.write(msg.text || "");
                } else if (msg.type === "turnComplete" || msg.type === "npcTurnComplete") {
                    if (currentTurnText.trim().length > 0) {
                        console.log("\n");
                        currentTurnText = "";
                    }
                    promptUser(bridgeWs, sessionId);
                } else if (msg.type === "error" || msg.type === "micAudioError") {
                    console.error(`\n❌ ERROR FROM AI: ${msg.message || msg.error}`);
                    promptUser(bridgeWs, sessionId);
                }
            } catch (e) {
                // Non-JSON packet
            }
        });
    });
}

function startChatLoop(bridgeWs, sessionId) {
    console.log("=========================================================");
    console.log("🗣️  TALKING TO DARNELL (DRUG DEALER) - INTERACTIVE CONVERSATION");
    console.log("=========================================================");
    console.log("Type your question below and press Enter. (Type 'exit' or 'quit' to end)\n");
    promptUser(bridgeWs, sessionId);
}

function promptUser(bridgeWs, sessionId) {
    rl.question("\nYou: ", (input) => {
        const text = input ? input.trim() : "";
        if (!text) {
            promptUser(bridgeWs, sessionId);
            return;
        }
        if (text.toLowerCase() === "exit" || text.toLowerCase() === "quit") {
            console.log("\nEnding conversation test...");
            bridgeWs.send(JSON.stringify({ type: "endSession", sessionId: sessionId }));
            bridgeWs.close();
            rl.close();
            process.exit(0);
        }

        process.stdout.write("Darnell: ");
        
        const playerMsg = {
            type: "playerMessage",
            sessionId: sessionId,
            turnId: Math.floor(Math.random() * 9000) + 100,
            text: text
        };

        bridgeWs.send(JSON.stringify(playerMsg));
    });
}

runInteractiveTest();
