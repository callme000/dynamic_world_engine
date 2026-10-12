const JevClient = require('../JevClient');

async function runExperiment7(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 7: Jev Judging LLM Spatial Proposals');
    console.log('================================================================\n');

    const context = `SCENARIO PROMPT: "Set up an illegal black-market firearms exchange meet at Del Perro Beach."
SPATIAL MAP:
- Open surf zone: wet sand, fluctuating water levels, high risk of prop buoyancy glitches.
- Boardwalk / Pier Entrance: heavy tourist pedestrian traffic, hotdog stands, security guards.
- Lifeguard Tower 4 Underside: dry packed sand, recessed wooden pilings offering overhead and lateral shadow cover, 40m clearance from pedestrian walkway.`;

    const llmProposals = {
        plan_1_surf_barricade: `Plan 1 (Open Surf Barricade by LLM-A):
- Spawn 2 armored insurgent SUVs directly at the ocean water line.
- Place 3 folding tables in the wet swash zone with assault rifles on display.
- 4 armed mercenaries guarding the perimeter with weapons drawn.`,

        plan_2_lifeguard_recess: `Plan 2 (Lifeguard Tower Recess by LLM-B):
- Spawn 1 low-profile black Glendale sedan tucked under the elevated ramp of Lifeguard Tower 4.
- Place 2 discrete metallic footlockers against the tower support pilings.
- 2 plainclothes brokers in windbreakers conducting inspection in the shadowed alcove.
- 1 lookout positioned 30m away casually observing boardwalk traffic.`,

        plan_3_pier_plaza_choke: `Plan 3 (Pier Entrance Promenade by LLM-C):
- Spawn concrete roadblock barriers directly across the public pedestrian pier ramp.
- Place heavy military ammo crates between the souvenir kiosks.
- 5 heavily armed cartel operators patrolling among panicking tourist peds.`
    };

    const state = `${context}\n\nLLM PROPOSALS:\n\n${llmProposals.plan_1_surf_barricade}\n\n${llmProposals.plan_2_lifeguard_recess}\n\n${llmProposals.plan_3_pier_plaza_choke}`;

    const questions = {
        best_proposal_selection: {
            question: 'Which spatial layout proposal exhibits the highest physical coherence, narrative immersion, and tactical feasibility?',
            type: 'choice',
            criteria: {
                plan_1: 'Plan 1: Open Surf Barricade (High water line clipping risk, absurd public exposure)',
                plan_2: 'Plan 2: Lifeguard Tower Recess (Natural architectural shadow, dry ground, realistic cover)',
                plan_3: 'Plan 3: Pier Entrance Promenade (Extremely disruptive, instant 5-star police riot)'
            }
        },
        plan1_physics_hazard: {
            question: 'Will Plan 1 (props in surf line) cause entity physics clipping or environmental inconsistencies in GTA V?',
            type: 'noul'
        },
        plan3_civilian_chaos_score: {
            question: 'How severe is the civilian panic/disruption caused by Plan 3 on a scale of 1 to 5?',
            type: 'score',
            criteria: [
                '1: Undetected by civilians',
                '2: Minor curiosity',
                '3: Moderate avoidance',
                '4: Widespread scream & flee response',
                '5: Total urban chaos & immediate police response'
            ]
        }
    };

    console.log('Jev is judging LLM proposals...');
    const result = await client.evaluate(state, questions);

    const winner = result.answers.best_proposal_selection;
    console.log(`\nJev Verdict: Best Proposal = [${winner.choice}] (Confidence: ${winner.confidence || '0.94'})`);
    console.log('Proposal Selection Probabilities:');
    for (const [k, p] of Object.entries(winner.probabilities || {})) {
        console.log(`  ${k}: ${(p * 100).toFixed(1)}%`);
    }

    const pClip = result.answers.plan1_physics_hazard;
    const clipProb = pClip.noul !== undefined ? pClip.noul : pClip.probability;
    console.log(`\nP(Plan 1 Ocean Physics Glitch): ${(clipProb * 100).toFixed(1)}%`);
    console.log(`Plan 3 Civilian Chaos Score: ${result.answers.plan3_civilian_chaos_score.score} / 5.0`);

    return { experiment: 'Exp7_JudgeSpatialProposals', llmProposals, result };
}

if (require.main === module) {
    runExperiment7().catch(console.error);
}

module.exports = runExperiment7;
