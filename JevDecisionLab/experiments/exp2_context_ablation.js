const JevClient = require('../JevClient');

async function runExperiment2(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 2: Progressive Context Ablation');
    console.log('================================================================\n');

    const ablationSteps = [
        {
            level: '100% Context',
            pct: 100,
            state: `A person has been shot in an alley in South Central Los Santos. A blood-stained metallic briefcase lies on the wet asphalt 3 meters from the body. A discarded semi-automatic pistol is visible under a cardboard box. Police sirens are wailing in the distance, approximately 4 blocks away and approaching. A frightened civilian witness is crouching behind a nearby dumpster, clutching a phone. Heavy rain, dead-end alley.`
        },
        {
            level: '75% Context',
            pct: 75,
            state: `A person has been shot in an alley in South Central Los Santos. A metallic briefcase lies on the asphalt near the body. A discarded pistol is visible. Police sirens are audible in the distance. Someone is crouching behind a dumpster.`
        },
        {
            level: '50% Context',
            pct: 50,
            state: `A person is lying wounded in an alley. There is a gun nearby and sirens can be heard in the distance.`
        },
        {
            level: '25% Context',
            pct: 25,
            state: `A person is lying on the ground in an alley. Sirens in the distance.`
        },
        {
            level: '10% Context',
            pct: 10,
            state: `A person in an alley.`
        }
    ];

    const questions = {
        danger_level: {
            question: 'How dangerous is the current situation on a scale of 1 to 5?',
            type: 'score',
            criteria: [
                '1: Calm, secure, no threats present',
                '2: Low tension, incidental bystanders, no weapons',
                '3: Moderate hazard, potential conflict, uncertain intentions',
                '4: High threat, armed actors or incoming law enforcement, severe bodily risk',
                '5: Lethal combat zone, active crossfire, imminent catastrophic casualty'
            ]
        }
    };

    const results = [];
    console.log('| Context Level | State Snippet | Danger Score | P(1) | P(2) | P(3) | P(4) | P(5) | Confidence |');
    console.log('|---|---|---|---|---|---|---|---|---|');

    for (const step of ablationSteps) {
        const res = await client.evaluate(step.state, questions);
        const ans = res.answers.danger_level;
        const probs = ans.probabilities || {};
        const p1 = (probs['1'] || 0).toFixed(2);
        const p2 = (probs['2'] || 0).toFixed(2);
        const p3 = (probs['3'] || 0).toFixed(2);
        const p4 = (probs['4'] || 0).toFixed(2);
        const p5 = (probs['5'] || 0).toFixed(2);
        const conf = ans.confidence !== undefined ? ans.confidence.toFixed(2) : 'N/A';

        console.log(`| ${step.level} | "${step.state.slice(0, 32)}..." | **${ans.score}** | ${p1} | ${p2} | ${p3} | ${p4} | ${p5} | ${conf} |`);
        results.push({ step, res });
    }

    console.log('\nObservation: Evaluates whether danger rating and confidence decay gracefully or exhibit sudden cliff collapse.');
    return { experiment: 'Exp2_ContextAblation', ablationSteps, results };
}

if (require.main === module) {
    runExperiment2().catch(console.error);
}

module.exports = runExperiment2;
