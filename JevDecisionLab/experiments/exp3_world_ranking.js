const JevClient = require('../JevClient');

async function runExperiment3(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 3: World Ranking (5 Environments A through E)');
    console.log('================================================================\n');

    const state = `A covert faction diplomat needs to conduct an unauthorized, high-stakes meeting with an undercover FIB informant in San Andreas.
Requirements: Minimal chance of bystander eavesdropping, rapid vehicular egress routes in at least two directions, low likelihood of random police patrol, and sufficient sightlines to detect ambushes within 100 meters.`;

    const environments = {
        Env_A: 'Sandy Shores Abandoned Airfield Hangar (Remote desert, zero civilian traffic, dual runway egress, completely visible flat terrain)',
        Env_B: 'Del Perro Pier Maintenance Deck (Underneath pier wooden boardwalk, muffled by crashing ocean waves, multiple pedestrian footpaths, restricted car access)',
        Env_C: 'Pillbox Hill Construction Site 3rd Floor (Unfinished concrete high-rise, elevated vantage point, urban core, requires vertical stair climb, single vehicle exit)',
        Env_D: 'Mirror Park Suburban Cul-de-sac (Quiet residential neighborhood, manicured lawns, high resident presence, neighborhood watch cameras, limited dead-end turnaround)',
        Env_E: 'Murrieta Oil Fields Pumpjack Valley (Industrial basin, loud machinery noise masking speech, winding dirt tracks, zero pedestrians, blind corners between storage tanks)'
    };

    const questions = {
        best_discreet_meeting_venue: {
            question: 'Which of the candidate environments is best suited for this discreet, high-stakes covert meeting?',
            type: 'choice',
            criteria: environments
        }
    };

    console.log('Candidate Environments:');
    for (const [k, v] of Object.entries(environments)) {
        console.log(`  [${k}]: ${v}`);
    }

    console.log('\nQuerying Jev Choice ranking...');
    const result = await client.evaluate(state, questions);
    const ans = result.answers.best_discreet_meeting_venue;

    console.log(`\nDecision Result: Top Choice = [${ans.choice}] (Confidence: ${ans.confidence || 'N/A'})`);
    console.log('Probability Distribution Across All 5 Options:');
    console.log('----------------------------------------------------------------');
    
    // Sort options by probability descending
    const sortedProbs = Object.entries(ans.probabilities || {}).sort((a, b) => b[1] - a[1]);
    for (const [envKey, prob] of sortedProbs) {
        const bar = '█'.repeat(Math.round(prob * 40));
        console.log(`  ${envKey.padEnd(8)}: ${(prob * 100).toFixed(1).padStart(5)}% | ${bar}`);
    }

    // Measure entropy / sharpness
    const probs = Object.values(ans.probabilities || {});
    const maxProb = Math.max(...probs);
    const minProb = Math.min(...probs);
    const spreadRatio = (maxProb / (minProb || 0.001)).toFixed(2);
    console.log(`\nDistribution Sharpness Analysis:`);
    console.log(`  Peak Probability: ${(maxProb * 100).toFixed(1)}%`);
    console.log(`  Spread Ratio (Max/Min): ${spreadRatio}x`);
    console.log(`  Characterization: ${maxProb > 0.45 ? 'Decisive Separation' : 'Diffused / Cowardly Flatness'}`);

    return { experiment: 'Exp3_WorldRanking', environments, result };
}

if (require.main === module) {
    runExperiment3().catch(console.error);
}

module.exports = runExperiment3;
