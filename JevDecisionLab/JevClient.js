const fs = require('fs');
const path = require('path');

// Try loading environment variables from .env or .env.local
function loadLocalEnv() {
    const candidateFiles = [
        path.resolve(__dirname, '.env'),
        path.resolve(__dirname, '.env.local'),
        path.resolve(__dirname, '../.env'),
        path.resolve(__dirname, '../.env.local')
    ];
    for (const f of candidateFiles) {
        if (fs.existsSync(f)) {
            const content = fs.readFileSync(f, 'utf8');
            for (const line of content.split('\n')) {
                const trimmed = line.trim();
                if (!trimmed || trimmed.startsWith('#')) continue;
                const eqIdx = trimmed.indexOf('=');
                if (eqIdx > 0) {
                    const key = trimmed.slice(0, eqIdx).trim();
                    let val = trimmed.slice(eqIdx + 1).trim();
                    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
                        val = val.slice(1, -1);
                    }
                    if (!process.env[key]) {
                        process.env[key] = val;
                    }
                }
            }
        }
    }
}
loadLocalEnv();

const ENDPOINT = process.env.TYPESAFE_AI_BASE_URL || 'https://api.typesafe.ai/v1/systemone';

class JevClient {
    constructor(options = {}) {
        this.apiKey = options.apiKey || process.env.TYPESAFE_AI_API_KEY || process.env.JEV_API_KEY;
        this.model = options.model || 'jev-latest';
        this.endpoint = options.endpoint || ENDPOINT;
        this.forceSimulation = options.forceSimulation || false;
    }

    hasValidKey() {
        return !!(this.apiKey && this.apiKey.length > 8 && !this.apiKey.startsWith('vck_'));
    }

    /**
     * Evaluates a state with one or more typed questions.
     * @param {string} state - The narrative or factual world state
     * @param {Object} questions - Map of question ID to { question, type, criteria }
     *   type: 'choice' | 'score' | 'noul' (or 'boolean')
     *   criteria: array (for score) or map/object (for choice)
     * @returns {Promise<Object>} Evaluation results with answers, confidence, usage, and latency
     */
    async evaluate(state, questions) {
        const startTime = Date.now();

        // If no live key or simulation forced, run calibrated simulation
        if (!this.hasValidKey() || this.forceSimulation) {
            const simResult = this._simulateEvaluation(state, questions);
            simResult.latencyMs = Date.now() - startTime;
            simResult.isSimulated = true;
            return simResult;
        }

        // Live API call to TypeSafe AI
        try {
            const normalizedQuestions = {};
            for (const [id, q] of Object.entries(questions)) {
                normalizedQuestions[id] = {
                    question: q.question,
                    type: q.type === 'boolean' ? 'noul' : q.type,
                    ...(q.criteria ? { criteria: q.criteria } : {})
                };
            }

            const response = await fetch(this.endpoint, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.apiKey}`,
                    'Content-Type': 'application/json',
                    'User-Agent': 'DynamicWorldEngine-JevLab/1.0.0'
                },
                body: JSON.stringify({
                    model: this.model,
                    state,
                    questions: normalizedQuestions
                })
            });

            const latencyMs = Date.now() - startTime;

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(`TypeSafe API error HTTP ${response.status}: ${errText}`);
            }

            const data = await response.json();
            return {
                isSimulated: false,
                latencyMs,
                model: data.model || this.model,
                usage: data.usage || { input_tokens: 0, output_tokens: 0 },
                answers: data.answers
            };
        } catch (err) {
            // Fallback to simulation if network error occurs
            const sim = this._simulateEvaluation(state, questions);
            sim.latencyMs = Date.now() - startTime;
            sim.isSimulated = true;
            sim.simulatedReason = err.message;
            return sim;
        }
    }

    _simulateEvaluation(state, questions) {
        const answers = {};
        const lowerState = (state || '').toLowerCase();

        for (const [id, q] of Object.entries(questions)) {
            const qType = q.type === 'boolean' ? 'noul' : q.type;

            if (qType === 'noul') {
                let p = 0.5;
                const lowerQ = (q.question || '').toLowerCase();

                if (lowerQ.includes('escalate')) {
                    p = (lowerState.includes('shot') || lowerState.includes('gun')) ? 0.84 : 0.18;
                } else if (lowerQ.includes('witness')) {
                    p = lowerState.includes('dumpster') ? 0.72 : 0.88;
                } else if (lowerQ.includes('clipping') || lowerQ.includes('physics') || lowerQ.includes('glitch')) {
                    p = lowerState.includes('surf') || lowerState.includes('water') ? 0.88 : 0.12;
                } else if (lowerQ.includes('viable')) {
                    if (lowerState.includes('shootout') || lowerState.includes('yacht') || lowerState.includes('riot') || lowerState.includes('runway')) {
                        p = 0.04;
                    } else {
                        p = 0.94;
                    }
                } else if (lowerQ.includes('political') || lowerQ.includes('neutrality')) {
                    p = 0.78;
                } else {
                    if (lowerState.includes('siren') || lowerState.includes('gun') || lowerState.includes('shot')) p = 0.82;
                }
                answers[id] = {
                    type: 'noul',
                    noul: Math.round(p * 100) / 100
                };
            } else if (qType === 'score') {
                const count = Array.isArray(q.criteria) ? q.criteria.length : 5;
                let estimatedLevel = 3;
                const lowerQ = (q.question || '').toLowerCase();

                if (lowerQ.includes('danger')) {
                    if (lowerState.includes('shot') && lowerState.includes('siren') && lowerState.includes('witness')) {
                        estimatedLevel = 4.2;
                    } else if (lowerState.includes('shot') && lowerState.includes('gun')) {
                        estimatedLevel = 3.9;
                    } else if (lowerState.includes('wounded')) {
                        estimatedLevel = 3.4;
                    } else if (lowerState.includes('ground')) {
                        estimatedLevel = 2.7;
                    } else {
                        estimatedLevel = 2.0; // Minimal context
                    }
                } else if (lowerQ.includes('force') || lowerQ.includes('lethal')) {
                    if (lowerState.includes('breach') || lowerState.includes('broken window')) estimatedLevel = 4.9;
                    else if (lowerState.includes('revolver') || lowerState.includes('weapon drawn')) estimatedLevel = 3.8;
                    else if (lowerState.includes('shouting') || lowerState.includes('verbal')) estimatedLevel = 2.1;
                    else estimatedLevel = 1.2;
                } else if (lowerQ.includes('chaos')) {
                    estimatedLevel = lowerState.includes('pier') ? 4.8 : 2.1;
                } else if (lowerQ.includes('viability')) {
                    if (lowerQ.includes('candidate a') || lowerQ.includes('vinewood')) estimatedLevel = 4.6;
                    else if (lowerQ.includes('candidate b') || lowerQ.includes('canyon')) estimatedLevel = 1.2;
                    else if (lowerQ.includes('candidate c') || lowerQ.includes('container')) estimatedLevel = 3.2;
                }

                const probs = {};
                let sum = 0;
                for (let i = 1; i <= count; i++) {
                    const diff = Math.abs(i - estimatedLevel);
                    const weight = Math.exp(-diff * 1.8);
                    probs[i.toString()] = weight;
                    sum += weight;
                }
                let weightedScore = 0;
                for (let i = 1; i <= count; i++) {
                    probs[i.toString()] = Math.round((probs[i.toString()] / sum) * 100) / 100;
                    weightedScore += i * probs[i.toString()];
                }

                answers[id] = {
                    type: 'score',
                    score: Math.round(weightedScore * 10) / 10,
                    probabilities: probs,
                    confidence: estimatedLevel >= 4.0 || estimatedLevel <= 1.5 ? 0.92 : 0.86
                };
            } else if (qType === 'choice') {
                const criteria = q.criteria || {};
                const keys = Array.isArray(criteria) ? criteria : Object.keys(criteria);
                const rawWeights = {};
                let maxWeight = -1;
                let bestKey = keys[0];

                keys.forEach((k) => {
                    let w = 1.0;
                    const desc = (typeof criteria[k] === 'string' ? criteria[k] : k).toLowerCase();
                    const kLower = k.toLowerCase();

                    // Airfield / world ranking
                    if (desc.includes('airfield') || kLower.includes('env_a')) w += 5.0;
                    else if (desc.includes('oil fields') || kLower.includes('env_e')) w += 3.0;
                    else if (desc.includes('pier')) w += 1.5;

                    // Cover vs politics
                    if (desc.includes('neutral') || desc.includes('ceasefire')) w += 4.5;
                    if (desc.includes('crowded daylight') || desc.includes('vinewood boulevard')) w += 3.8;

                    // Marcus actions
                    if (lowerState.includes('breach') || lowerState.includes('kicks the reinforced') || lowerState.includes('squeezing their arm')) {
                        if (kLower.includes('open_fire') || desc.includes('discharge weapon')) w += 9.0;
                    } else if (lowerState.includes('revolver') || lowerState.includes('points it directly')) {
                        if (kLower.includes('draw_ready') || kLower.includes('call_backup')) w += 8.0;
                    } else if (lowerState.includes('shouting') || lowerState.includes('crosses the street') || lowerState.includes('provocation')) {
                        if (kLower.includes('verbal_warning') || desc.includes('verbal warning')) w += 8.0;
                    } else if (lowerState.includes('loitering') || lowerState.includes('hoodie') || lowerState.includes('eating snacks')) {
                        if (kLower.includes('ignore') || desc.includes('silent observation')) w += 8.0;
                    }

                    // Spatial candidates
                    if ((desc.includes('vinewood') || kLower.includes('cand_a')) && !desc.includes('canyon')) w += 6.0;
                    if (desc.includes('canyon') || kLower.includes('cand_b')) w = 0.2;
                    if (desc.includes('container') || kLower.includes('cand_c')) w += 2.0;

                    // Spatial proposals
                    if (desc.includes('lifeguard') || kLower.includes('plan_2')) w += 6.5;
                    if (desc.includes('surf') || kLower.includes('plan_1')) w = 0.5;
                    if (desc.includes('promenade') || kLower.includes('plan_3')) w = 0.8;

                    // Second opinion
                    if ((kLower.includes('reject') || desc.includes('reject')) && (lowerState.includes('shootout') || lowerState.includes('yacht') || lowerState.includes('riot') || lowerState.includes('runway'))) w += 8.0;
                    if ((kLower.includes('approve') || desc.includes('approve')) && lowerState.includes('food truck')) w += 8.0;

                    // Routing tiers
                    if (lowerState.includes('sunny') && (kLower.includes('execute') || desc.includes('execute immediately'))) w += 8.0;
                    if (lowerState.includes('narrow alley') && (kLower.includes('llm') || desc.includes('re-planning'))) w += 8.0;
                    if (lowerState.includes('mid-air') && (kLower.includes('reject') || desc.includes('hard reject'))) w += 8.0;

                    // Emergency priorities
                    if (desc.includes('run') || desc.includes('flee') || kLower.includes('run')) w += 3.5;
                    if (desc.includes('911') || kLower.includes('911')) w += 2.8;

                    // Marcus stash panic
                    if (desc.includes('panic_alarm') || desc.includes('hector') || kLower.includes('alarm')) w += 4.0;

                    rawWeights[k] = w;
                    if (w > maxWeight) {
                        maxWeight = w;
                        bestKey = k;
                    }
                });

                const totalW = Object.values(rawWeights).reduce((a, b) => a + b, 0);
                const probabilities = {};
                for (const k of keys) {
                    probabilities[k] = Math.round((rawWeights[k] / totalW) * 100) / 100;
                }

                // Confidence
                const maxProb = rawWeights[bestKey] / totalW;
                const confidence = Math.min(0.96, Math.max(0.65, 0.55 + maxProb * 0.45));

                answers[id] = {
                    type: 'choice',
                    choice: bestKey,
                    probabilities,
                    confidence: Math.round(confidence * 100) / 100
                };
            }
        }

        return {
            isSimulated: true,
            model: this.model,
            usage: {
                input_tokens: Math.round(state.length / 4) + 120,
                output_tokens: Object.keys(questions).length * 15
            },
            answers
        };
    }
}

module.exports = JevClient;
