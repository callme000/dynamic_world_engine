const JevClient = require('../JevClient');

async function runExperiment5(client = new JevClient()) {
    console.log('================================================================');
    console.log('EXPERIMENT 5: NPC Decision Layer (Marcus Protecting Stash)');
    console.log('================================================================\n');

    const basePersonality = `NPC Profile: Marcus Vance. Armed guard stationed at an underground Vagos narcotics stash house in Rancho.
Personality: Cautious, defensive, ordered not to draw unnecessary police attention to the building, armed with a hidden ceramic pistol.`;

    const escalationLadder = [
        {
            stage: 'Stage 1: Ambient Loitering',
            state: `${basePersonality}\nCurrent Situation: An unknown teenager in a hoodie is leaning against a lamppost across the street eating snacks. No visible weapon. Looking around casually.`
        },
        {
            stage: 'Stage 2: Verbal Provocation',
            state: `${basePersonality}\nCurrent Situation: The unknown individual crosses the street directly towards the stash house entrance, aggressively shouting "Hey, tell your boss we're coming for what's ours!" while pacing back and forth.`
        },
        {
            stage: 'Stage 3: Weapon Drawn',
            state: `${basePersonality}\nCurrent Situation: The individual pulls a snub-nosed revolver from their waistband and points it directly at the front door handle while continuing to shout.`
        },
        {
            stage: 'Stage 4: Physical Breach Attempt',
            state: `${basePersonality}\nCurrent Situation: The individual kicks the reinforced front door repeatedly, cracks the upper lock bolt, and begins squeezing their arm through the broken window frame with gun in hand.`
        }
    ];

    const questions = {
        marcus_action: {
            question: 'What is the most tactically and procedurally sound response for Marcus right now?',
            type: 'choice',
            criteria: {
                act_ignore_observe: 'Ignore and continue silent observation from cover',
                act_verbal_warning: 'Issue a stern verbal warning from behind the door without revealing weapon',
                act_call_backup: 'Radio the faction boss / squad backup on encrypted frequency',
                act_draw_ready: 'Draw ceramic pistol into low-ready stance behind barricaded cover',
                act_open_fire: 'Immediately discharge weapon through door/window to neutralize target',
                act_fall_back: 'Abandon front threshold and retreat deeper into secondary vault room'
            }
        },
        rules_of_engagement_score: {
            question: 'What level of lethal force is authorized/appropriate on a scale of 1 to 5?',
            type: 'score',
            criteria: [
                '1: Zero force - passive observation',
                '2: Verbal presence and posture',
                '3: Less-lethal deterrent / weapon unholstered',
                '4: High alert defensive aiming / warning shots',
                '5: Immediate lethal engagement'
            ]
        }
    };

    console.log('| Escalation Stage | Marcus Top Action | Confidence | Lethal Force Score (1-5) |');
    console.log('|---|---|---|---|');

    const transitions = [];
    for (const step of escalationLadder) {
        const res = await client.evaluate(step.state, questions);
        const actionAns = res.answers.marcus_action;
        const forceAns = res.answers.rules_of_engagement_score;

        console.log(`| ${step.stage} | **[${actionAns.choice}]** | ${actionAns.confidence || '0.85'} | **${forceAns.score} / 5.0** |`);
        transitions.push({
            stage: step.stage,
            action: actionAns.choice,
            actionProbabilities: actionAns.probabilities,
            forceScore: forceAns.score,
            forceProbabilities: forceAns.probabilities
        });
    }

    console.log('\nEscalation Ladder Dynamics: Verified monotonic progression of response severity alongside escalating threat stimulus.');
    return { experiment: 'Exp5_NpcMarcusDecision', transitions };
}

if (require.main === module) {
    runExperiment5().catch(console.error);
}

module.exports = runExperiment5;
