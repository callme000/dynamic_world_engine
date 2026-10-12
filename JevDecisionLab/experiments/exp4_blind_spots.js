const JevClient = require('../JevClient');

async function runExperiment4(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 4: Blind Spots (Physical Suitability vs Political Faction Turf)');
    console.log('================================================================\n');

    const tradeOffScenarios = [
        {
            title: 'Scenario 1: Cover vs Faction Hostility',
            state: `A VIP negotiator needs to take shelter while awaiting pickup.
Location A is a fortified brick warehouse with ballistic shutter windows, reinforced doors, and excellent 360-degree cover, BUT it is situated deep in hostile Ballas gang turf where armed lookouts actively shoot trespassers on sight.
Location B is an open park pavilion with zero ballistic cover, simple wooden picnic tables, and exposed sightlines from four streets, BUT it is strictly neutral sovereign territory recognized as a demilitarized ceasefire zone by all active syndicates.`,
            question: 'Which location is overall safer for the VIP while awaiting pickup?',
            options: {
                loc_a_fortified_hostile: 'Location A: Fortified brick warehouse with heavy cover (in hostile gang turf)',
                loc_b_open_neutral: 'Location B: Open wooden pavilion with zero ballistic cover (in neutral ceasefire zone)'
            }
        },
        {
            title: 'Scenario 2: Police Density vs Concealment',
            state: `A covert arms courier is transporting an illegal prototype sniper rifle.
Option X is an underground maintenance subway tunnel with zero civilian cameras and no civilian witnesses, BUT local police transit units patrol the single exit turnstile every 10 minutes with canine search teams.
Option Y is a crowded sidewalk along Vinewood Boulevard in broad daylight surrounded by tourists, food trucks, and street performers, with zero canine patrols and no static checkpoints.`,
            question: 'Which transit route has a lower probability of interception and arrest?',
            options: {
                opt_x_subway_patrol: 'Option X: Secluded dark subway tunnel (with periodic canine checkpoints)',
                opt_y_crowded_vinewood: 'Option Y: Crowded daylight tourist boulevard (with ambient public scrutiny but no canine searches)'
            }
        }
    ];

    const results = [];

    for (const scn of tradeOffScenarios) {
        console.log(`--- ${scn.title} ---`);
        console.log(`STATE:\n${scn.state}\n`);

        const questions = {
            safety_choice: {
                question: scn.question,
                type: 'choice',
                criteria: scn.options
            },
            political_dominance_check: {
                question: 'Does political territory neutrality outweigh physical ballistic fortifications in this scenario?',
                type: 'noul'
            }
        };

        const res = await client.evaluate(scn.state, questions);
        const choiceAns = res.answers.safety_choice;
        const noulAns = res.answers.political_dominance_check;

        console.log(`Top Choice: [${choiceAns.choice}] (Confidence: ${choiceAns.confidence || 'N/A'})`);
        console.log('Option Probabilities:');
        for (const [k, p] of Object.entries(choiceAns.probabilities || {})) {
            console.log(`  ${k}: ${(p * 100).toFixed(1)}%`);
        }
        const pTrue = noulAns.noul !== undefined ? noulAns.noul : noulAns.probability;
        console.log(`P(Political Neutrality Outweighs Physical Cover): ${(pTrue * 100).toFixed(1)}%\n`);

        results.push({ scenario: scn.title, res });
    }

    return { experiment: 'Exp4_BlindSpots', tradeOffScenarios, results };
}

if (require.main === module) {
    runExperiment4().catch(console.error);
}

module.exports = runExperiment4;
