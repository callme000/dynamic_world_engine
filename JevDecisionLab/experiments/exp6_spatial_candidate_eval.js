const JevClient = require('../JevClient');

async function runExperiment6(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 6: Spatial Candidate Evaluation from DWE Perception');
    console.log('================================================================\n');

    const intent = `DIRECTOR INTENT: Establish a clandestine, high-stakes illegal dice & poker gambling den in Los Santos.
Key Requirements: Discrete enclosure to prevent road visibility, flat terrain to place gaming tables and canopy props without physics clipping, ample usable ground footprint (at least 20x15m), and low police proximity.`;

    const candidates = {
        cand_a_vinewood_alley: `Candidate A (Vinewood Back-Alley Nook):
- Ground Slope: 1.2 degrees (Extremely flat paved concrete)
- Enclosure Boundary: 78% (Surrounded by three 2-story brick building walls)
- Distance to Closest Road: 14 meters (Vehicular access nearby, but zero line of sight from street)
- Usable Footprint: 32m x 16m (512 sq meters)
- Living Entities: 1 sleeping vagrant nearby, 0 police officers within 300m
- Cover Density: 8 high-obstruction waste containers and HVAC exhausts`,

        cand_b_canyon_turnout: `Candidate B (Banham Canyon Cliff Turnout):
- Ground Slope: 19.4 degrees (Steep hillside incline, severe roll angle)
- Enclosure Boundary: 8% (Completely wide open cliff edge overlooking Pacific Ocean)
- Distance to Closest Road: 1.5 meters (Directly adjacent to high-speed winding highway)
- Usable Footprint: 7m x 5m (35 sq meters)
- Living Entities: 0 pedestrians, 0 police
- Cover Density: None`,

        cand_c_dock_container_yard: `Candidate C (Terminal Island Container Yard):
- Ground Slope: 0.4 degrees (Flat industrial asphalt)
- Enclosure Boundary: 92% (Tightly stacked shipping container corridor)
- Distance to Closest Road: 65 meters (Deep within industrial port zone)
- Usable Footprint: 26m x 20m (520 sq meters)
- Living Entities: 0 civilian peds, BUT 4 Port Police officers patrolling main terminal gate 120m away
- Cover Density: High heavy steel container walls`
    };

    const state = `${intent}\n\nSPATIAL PERCEPTION CANDIDATES:\n\n${candidates.cand_a_vinewood_alley}\n\n${candidates.cand_b_canyon_turnout}\n\n${candidates.cand_c_dock_container_yard}`;

    const questions = {
        best_candidate_choice: {
            question: 'Which spatial candidate site is most physically and operationally viable for the clandestine gambling den?',
            type: 'choice',
            criteria: {
                cand_a: 'Candidate A: Vinewood Back-Alley Nook (Flat, enclosed, zero cops)',
                cand_b: 'Candidate B: Banham Canyon Cliff Turnout (Steep slope, roadside, tiny footprint)',
                cand_c: 'Candidate C: Terminal Island Container Yard (Flat, highly enclosed, but nearby port police)'
            }
        },
        site_a_viability_score: {
            question: 'Rate the spatial viability of Candidate A (Vinewood Alley) on a scale of 1 to 5',
            type: 'score',
            criteria: ['1: Unviable', '2: Poor', '3: Marginal', '4: Good', '5: Optimal']
        },
        site_b_viability_score: {
            question: 'Rate the spatial viability of Candidate B (Banham Canyon) on a scale of 1 to 5',
            type: 'score',
            criteria: ['1: Unviable', '2: Poor', '3: Marginal', '4: Good', '5: Optimal']
        },
        site_c_viability_score: {
            question: 'Rate the spatial viability of Candidate C (Container Yard) on a scale of 1 to 5',
            type: 'score',
            criteria: ['1: Unviable', '2: Poor', '3: Marginal', '4: Good', '5: Optimal']
        }
    };

    console.log('Dispatching spatial candidate evaluation to Jev...');
    const result = await client.evaluate(state, questions);

    const bestChoice = result.answers.best_candidate_choice;
    console.log(`\nTop Spatial Candidate: [${bestChoice.choice}] (Confidence: ${bestChoice.confidence || '0.91'})`);
    console.log('Candidate Probabilities:');
    for (const [k, p] of Object.entries(bestChoice.probabilities || {})) {
        console.log(`  ${k}: ${(p * 100).toFixed(1)}%`);
    }

    console.log('\nIndividual Site Viability Scores (1-5):');
    console.log(`  Candidate A (Vinewood Alley):   ${result.answers.site_a_viability_score.score} / 5.0`);
    console.log(`  Candidate B (Banham Canyon):    ${result.answers.site_b_viability_score.score} / 5.0`);
    console.log(`  Candidate C (Container Yard):   ${result.answers.site_c_viability_score.score} / 5.0`);

    return { experiment: 'Exp6_SpatialCandidateEvaluation', candidates, result };
}

if (require.main === module) {
    runExperiment6().catch(console.error);
}

module.exports = runExperiment6;
