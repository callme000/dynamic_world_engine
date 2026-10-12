const JevClient = require('../JevClient');

async function runExperiment8(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 8: Jev as Second Opinion (Disagreement Matrix)');
    console.log('================================================================\n');

    const stressPrompts = [
        {
            id: 'case_1_shootout_bbq',
            prompt: 'Host an outdoor neighborhood BBQ party with beer kegs and picnic tables in Legion Square.',
            environmentCondition: 'Active shootout occurring: 6 armed gangsters exchanging automatic fire with 4 LSPD cruisers. Bullets ricocheting off statues.',
            llmVerdict: 'EXECUTE (Spawn tables, grills, and party NPCs in park centre)'
        },
        {
            id: 'case_2_storm_drain_yacht',
            prompt: 'Anchor a 40-foot Marquis luxury yacht with sun loungers.',
            environmentCondition: 'Location is the concrete Los Santos Flood Drain canal (water depth: 0.4 meters, width: 14 meters).',
            llmVerdict: 'EXECUTE (Spawn yacht at coordinates 412, -1450, -28)'
        },
        {
            id: 'case_3_prison_riot_yoga',
            prompt: 'Lead a tranquil sunset yoga and mindfulness circle with yoga mats.',
            environmentCondition: 'Inside the exercise yard of Bolingbroke State Penitentiary during an active prison break and riot with tear gas deployed.',
            llmVerdict: 'EXECUTE (Spawn instructor and 4 yoga practitioners)'
        },
        {
            id: 'case_4_arena_food_truck',
            prompt: 'Park a gourmet taco food truck with picnic benches and patrons.',
            environmentCondition: 'Sidewalk plaza outside Maze Bank Arena 45 minutes prior to a scheduled evening event. Normal pedestrian flow, clear weather.',
            llmVerdict: 'EXECUTE (Spawn food truck and standing eating props)'
        },
        {
            id: 'case_5_airport_runway_vendor',
            prompt: 'Open an umbrella hot-dog cart selling snacks.',
            environmentCondition: 'Directly on the active center threshold line of Runway 03L at Los Santos International Airport with incoming commercial jetliner on final approach.',
            llmVerdict: 'EXECUTE (Spawn hot-dog cart on tarmac center)'
        }
    ];

    const matrix = [];
    console.log('| Case ID | Scenario | LLM Default Verdict | Jev Viability P(Viable) | Jev Recommendation | Status |');
    console.log('|---|---|---|---|---|---|');

    for (const testCase of stressPrompts) {
        const state = `DIRECTOR REQUEST: "${testCase.prompt}"\nACTIVE PHYSICAL REALITY: ${testCase.environmentCondition}`;
        
        const questions = {
            is_viable: {
                question: 'Is this staging physically, narratively, and operationally viable without immersion collapse or instant destruction?',
                type: 'noul'
            },
            recommended_action: {
                question: 'What is the authoritative recommendation?',
                type: 'choice',
                criteria: {
                    approve: 'APPROVE: Safe to realize in game world',
                    divert: 'DIVERT: Relocate to adjacent suitable zone',
                    reject: 'REJECT: Completely absurd, physically impossible, or catastrophic hazard'
                }
            }
        };

        const res = await client.evaluate(state, questions);
        const viableAns = res.answers.is_viable;
        const pViable = viableAns.noul !== undefined ? viableAns.noul : viableAns.probability;
        const recAns = res.answers.recommended_action;

        const doesAgree = (testCase.llmVerdict.includes('EXECUTE') && recAns.choice === 'approve') ||
                          (!testCase.llmVerdict.includes('EXECUTE') && recAns.choice !== 'approve');
        const status = doesAgree ? 'AGREE' : '⚡ DISAGREEMENT (Jev Veto)';

        console.log(`| ${testCase.id} | "${testCase.prompt.slice(0, 24)}..." | ${testCase.llmVerdict.slice(0, 10)} | ${(pViable * 100).toFixed(1)}% | **${recAns.choice.toUpperCase()}** | ${status} |`);

        matrix.push({
            id: testCase.id,
            llmVerdict: testCase.llmVerdict,
            jevViableProb: pViable,
            jevChoice: recAns.choice,
            disagreement: !doesAgree
        });
    }

    const vetoes = matrix.filter(m => m.disagreement).length;
    console.log(`\nDisagreement Summary: Jev vetoed ${vetoes} out of ${matrix.length} hallucinated LLM realizations.`);
    return { experiment: 'Exp8_SecondOpinionMatrix', matrix };
}

if (require.main === module) {
    runExperiment8().catch(console.error);
}

module.exports = runExperiment8;
