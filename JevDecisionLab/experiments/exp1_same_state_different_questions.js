const JevClient = require('../JevClient');

async function runExperiment1(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 1: Same State, Radically Different Questions');
    console.log('================================================================\n');

    const state = `A person has been shot in an alley in South Central Los Santos.
A blood-stained metallic briefcase lies on the wet asphalt 3 meters from the body.
A discarded semi-automatic pistol is visible under a discarded cardboard box.
Police sirens are wailing in the distance, approximately 4 blocks away and approaching.
A frightened civilian witness is crouching behind a nearby commercial dumpster, clutching a phone.
Night time, heavy rain, poor street lighting, dead-end alley with one primary egress route.`;

    console.log('WORLD STATE:\n' + state + '\n');

    const questions = {
        q1_will_escalate: {
            question: 'Will this situation violently escalate within the next 60 seconds?',
            type: 'noul'
        },
        q2_danger_level: {
            question: 'How dangerous is the current immediate environment on a scale of 1 to 5?',
            type: 'score',
            criteria: [
                '1: Calm, secure, no threats present',
                '2: Low tension, incidental bystanders, no weapons',
                '3: Moderate hazard, potential conflict, uncertain intentions',
                '4: High threat, armed actors or incoming law enforcement, severe bodily risk',
                '5: Lethal combat zone, active crossfire, imminent catastrophic casualty'
            ]
        },
        q3_immediate_priority: {
            question: 'What is the single most urgent tactical priority for a neutral third-party arriving right now?',
            type: 'choice',
            criteria: {
                opt1_call_911: 'Call 911 / emergency services immediately',
                opt2_secure_briefcase: 'Grab and secure the bloody briefcase before police arrive',
                opt3_run_egress: 'Flee the alley immediately via the primary egress route',
                opt4_hide_cover: 'Take cover behind the dumpster adjacent to the witness and observe'
            }
        },
        q4_witness_in_danger: {
            question: 'Is the civilian witness behind the dumpster in imminent physical danger?',
            type: 'noul'
        }
    };

    console.log('Dispatching parallel evaluation questions to Jev...');
    const result = await client.evaluate(state, questions);

    console.log(`\nEvaluation completed in ${result.latencyMs}ms (Simulated: ${result.isSimulated})`);
    console.log('Model:', result.model);
    console.log('Usage:', JSON.stringify(result.usage));
    console.log('\nRESULTS:');
    console.log('----------------------------------------------------------------');

    for (const [id, ans] of Object.entries(result.answers)) {
        console.log(`Question ID: [${id}]`);
        if (ans.type === 'noul' || ans.type === 'boolean') {
            const prob = ans.noul !== undefined ? ans.noul : ans.probability;
            console.log(`  Type: Noul (Boolean Probability)`);
            console.log(`  P(True): ${(prob * 100).toFixed(1)}%`);
        } else if (ans.type === 'score') {
            console.log(`  Type: Score`);
            console.log(`  Weighted Score: ${ans.score} / 5.0 (Confidence: ${ans.confidence || 'N/A'})`);
            console.log(`  Probabilities:`, ans.probabilities);
        } else if (ans.type === 'choice') {
            console.log(`  Type: Choice`);
            console.log(`  Top Choice: [${ans.choice}] (Confidence: ${ans.confidence || 'N/A'})`);
            console.log(`  Distribution:`, ans.probabilities);
        }
        console.log('----------------------------------------------------------------');
    }

    return { experiment: 'Exp1_SameStateDifferentQuestions', state, questions, result };
}

if (require.main === module) {
    runExperiment1().catch(console.error);
}

module.exports = runExperiment1;
